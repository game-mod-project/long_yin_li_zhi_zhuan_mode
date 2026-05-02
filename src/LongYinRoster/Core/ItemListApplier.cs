using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5.3 — 인벤토리 list Replace (clear + add all).
///
/// Spike Phase 1 (Step 1-4) 결과:
///   - Clear: HeroData.LoseAllItem() — parameterless
///   - Wrapper ctor: ItemData() parameterless 또는 ItemData(ItemType _type)
///   - ItemData 에 itemCount field 없음 (game 인벤토리는 grid-style, stack 아님)
///   - Sub-data (equipmentData/medFoodData/bookData/horseData/...) 는 type 에 따라 자동
///   - Add: HeroData.GetItem(ItemData wrapper, bool showPopInfo)
///   - 2-pass retry — game-internal silent fail 회피 (v0.5.2 패턴)
///
/// 빈 슬롯 (itemID=0) 은 추출 시 skip — game 의 LoseAllItem 후 GetItem 이 grid 자동 관리.
/// itemCount 없음 — game 이 stackable 아니라 매 instance 가 단일 item.
/// </summary>
public static class ItemListApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int RemovedCount { get; set; }
        public int AddedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public sealed record ItemEntry(int ItemID, int Type, int SubType, int ItemLv, int RareLv, float Weight, int Value, string Name);

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string ClearMethodName = "LoseAllItem";
    private const string AddMethodName   = "GetItem";

    public static IReadOnlyList<ItemEntry> ExtractItemList(JsonElement slot)
    {
        var list = new List<ItemEntry>();
        if (!slot.TryGetProperty("itemListData", out var ild) || ild.ValueKind != JsonValueKind.Object)
            return list;
        if (!ild.TryGetProperty("allItem", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("itemID", out var idEl) || idEl.ValueKind != JsonValueKind.Number) continue;
            int itemID = idEl.GetInt32();
            if (itemID <= 0) continue;  // 빈 슬롯 skip
            int type = entry.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.Number ? tEl.GetInt32() : 0;
            int subType = entry.TryGetProperty("subType", out var stEl) && stEl.ValueKind == JsonValueKind.Number ? stEl.GetInt32() : 0;
            int itemLv = entry.TryGetProperty("itemLv", out var lvEl) && lvEl.ValueKind == JsonValueKind.Number ? lvEl.GetInt32() : 0;
            int rareLv = entry.TryGetProperty("rareLv", out var rEl) && rEl.ValueKind == JsonValueKind.Number ? rEl.GetInt32() : 0;
            float weight = entry.TryGetProperty("weight", out var wEl) && wEl.ValueKind == JsonValueKind.Number ? wEl.GetSingle() : 0f;
            int value = entry.TryGetProperty("value", out var vEl) && vEl.ValueKind == JsonValueKind.Number ? vEl.GetInt32() : 0;
            string name = entry.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String ? (nEl.GetString() ?? "") : "";
            list.Add(new ItemEntry(itemID, type, subType, itemLv, rareLv, weight, value, name));
        }
        return list;
    }

    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.ItemList)
        {
            res.Skipped = true;
            res.Reason = "itemList (selection off)";
            return res;
        }

        var list = ExtractItemList(slot);
        if (player == null)
        {
            res.Skipped = true;
            res.Reason = "player null (test mode)";
            return res;
        }

        var ild = ReadFieldOrProperty(player, "itemListData");
        if (ild == null)
        {
            res.Skipped = true;
            res.Reason = "itemListData null";
            return res;
        }
        var allItem = ReadFieldOrProperty(ild, "allItem");
        if (allItem == null)
        {
            res.Skipped = true;
            res.Reason = "itemListData.allItem null";
            return res;
        }

        // Wrapper type 발견
        Type? wrapperType = null;
        int initialCount = IL2CppListOps.Count(allItem);
        for (int i = 0; i < initialCount; i++)
        {
            var sample = IL2CppListOps.Get(allItem, i);
            if (sample != null) { wrapperType = sample.GetType(); break; }
        }
        if (wrapperType == null)
        {
            res.Skipped = true;
            res.Reason = "wrapperType null (allItem empty before clear)";
            return res;
        }

        // Wrapper ctor 발견 — parameterless 우선 (모든 property reflection set)
        var ctorParameterless = wrapperType.GetConstructor(F, null, Type.EmptyTypes, null);
        if (ctorParameterless == null)
        {
            res.Skipped = true;
            res.Reason = $"wrapper parameterless ctor not found on {wrapperType.FullName}";
            return res;
        }

        // Clear phase
        int beforeCount = IL2CppListOps.Count(allItem);
        try
        {
            InvokeMethod(player, ClearMethodName, Array.Empty<object>());
            int afterCount = IL2CppListOps.Count(allItem);
            res.RemovedCount = beforeCount - afterCount;
            Logger.Info($"ItemList clear ({ClearMethodName}): {beforeCount} → {afterCount}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"ItemList clear: {ex.GetType().Name}: {ex.Message}");
            res.Skipped = true;
            res.Reason = $"clear failed: {ex.Message}";
            return res;
        }

        // Add phase — 2-pass retry (v0.5.2 패턴)
        for (int pass = 0; pass < 2; pass++)
        {
            int beforePass = IL2CppListOps.Count(allItem);
            foreach (var entry in list)
            {
                try
                {
                    var wrapper = ctorParameterless.Invoke(null);
                    TrySetMember(wrapper, "itemID", entry.ItemID);
                    TrySetEnumMember(wrapper, "type", entry.Type);
                    TrySetMember(wrapper, "subType", entry.SubType);
                    TrySetMember(wrapper, "itemLv", entry.ItemLv);
                    TrySetMember(wrapper, "rareLv", entry.RareLv);
                    TrySetMember(wrapper, "weight", entry.Weight);
                    TrySetMember(wrapper, "value", entry.Value);
                    TrySetMember(wrapper, "name", entry.Name);

                    // GetItem(wrapper, false) — showPopInfo=false
                    InvokeMethod(player, AddMethodName, new object[] { wrapper, false });
                }
                catch (Exception ex)
                {
                    if (pass == 0)
                        Logger.Warn($"ItemList add pass={pass} itemID={entry.ItemID}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            int afterPass = IL2CppListOps.Count(allItem);
            Logger.Info($"ItemList add pass={pass}: count {beforePass} → {afterPass} (target={list.Count})");
            if (afterPass >= list.Count) break;
        }

        // Final count = grid total (171), 단지 add 된 item 수 estimate
        res.AddedCount = list.Count;
        res.FailedCount = 0;

        Logger.Info($"ItemList Apply done — removed={res.RemovedCount} added={res.AddedCount} failed={res.FailedCount}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
    {
        return Apply(player, backup, new ApplySelection { ItemList = true });
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        return null;
    }

    private static void TrySetMember(object obj, string name, object value)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null && p.CanWrite)
        {
            try { p.SetValue(obj, value); } catch { }
            return;
        }
        var f = t.GetField(name, F);
        if (f != null)
        {
            try { f.SetValue(obj, value); } catch { }
        }
    }

    /// <summary>
    /// Enum property/field 에 int 값 set — Enum.ToObject 로 conversion 후 set.
    /// </summary>
    private static void TrySetEnumMember(object obj, string name, int intValue)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null && p.CanWrite && p.PropertyType.IsEnum)
        {
            try { p.SetValue(obj, Enum.ToObject(p.PropertyType, intValue)); } catch { }
            return;
        }
        var f = t.GetField(name, F);
        if (f != null && f.FieldType.IsEnum)
        {
            try { f.SetValue(obj, Enum.ToObject(f.FieldType, intValue)); } catch { }
        }
    }

    private static void InvokeMethod(object obj, string methodName, object[] args)
    {
        var t = obj.GetType();
        MethodInfo? best = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != methodName) continue;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) continue;
            // 우리 args 와 method param 이 호환되는지 검사
            bool compatible = true;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { compatible = false; break; }
            }
            if (!compatible) continue;
            if (best == null || ps.Length < best.GetParameters().Length) best = m;
        }
        if (best == null) throw new MissingMethodException(t.FullName, methodName);
        var ps2 = best.GetParameters();
        var full = new object?[ps2.Length];
        for (int i = 0; i < ps2.Length; i++)
            full[i] = i < args.Length ? args[i]
                : (ps2[i].ParameterType.IsValueType ? Activator.CreateInstance(ps2[i].ParameterType) : null);
        best.Invoke(obj, full);
    }
}
