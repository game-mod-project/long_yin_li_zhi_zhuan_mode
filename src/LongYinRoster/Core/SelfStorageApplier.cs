using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5.5 — 창고 (selfStorage) Replace.
///
/// 슬롯 JSON 의 selfStorage 구조 = ItemListData 와 동일 패턴
///   { heroID, forceID, money, weight, maxWeight, allItem: [ItemData...] }
///
/// 게임-self method 식별:
///   - 인벤토리 grid 와 달리 selfStorage 는 별도 UI (창고 panel) — 무조건적인
///     game-self method 가 없을 수 있음. 1차 시도: 직접 list manipulation
///     (IL2CppListOps.Clear + reflection Add). 실패 시 game method 후보 dump.
///
/// ItemData wrapper 와 deep-copy logic 은 ItemListApplier 와 100% 동일하므로
/// ApplyJsonToObject 재사용. ItemType ctor + 2-pass retry 도 재사용.
/// </summary>
public static class SelfStorageApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int RemovedCount { get; set; }
        public int AddedCount { get; set; }
        public int FailedCount { get; set; }
    }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly string[] SubDataKeys =
        { "equipmentData", "medFoodData", "bookData", "treasureData", "materialData", "horseData" };

    /// <summary>
    /// 슬롯 JSON 의 selfStorage.allItem 에서 non-empty entry 만 추출.
    /// ItemListApplier 와 동일 필터: type=0 + name="" + 모든 subData null = 빈 슬롯.
    /// </summary>
    public static IReadOnlyList<JsonElement> ExtractStorageEntries(JsonElement slot)
    {
        var list = new List<JsonElement>();
        if (!slot.TryGetProperty("selfStorage", out var ss) || ss.ValueKind != JsonValueKind.Object)
            return list;
        if (!ss.TryGetProperty("allItem", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (IsTrulyEmpty(entry)) continue;
            list.Add(entry);
        }
        return list;
    }

    private static bool IsTrulyEmpty(JsonElement entry)
    {
        int type = entry.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.Number
                   ? tEl.GetInt32() : 0;
        if (type != 0) return false;
        string name = entry.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String
                      ? (nEl.GetString() ?? "") : "";
        if (!string.IsNullOrEmpty(name)) return false;
        foreach (var key in SubDataKeys)
        {
            if (entry.TryGetProperty(key, out var sub) && sub.ValueKind == JsonValueKind.Object) return false;
        }
        return true;
    }

    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.SelfStorage) { res.Skipped = true; res.Reason = "selfStorage (selection off)"; return res; }

        var entries = ExtractStorageEntries(slot);
        if (player == null) { res.Skipped = true; res.Reason = "player null (test mode)"; return res; }

        Logger.Info($"SelfStorage Apply: extracted {entries.Count} non-empty entries from slot JSON");

        var ss = ReadFieldOrProperty(player, "selfStorage");
        if (ss == null)
        {
            Logger.Info("SelfStorage Apply: player.selfStorage null");
            res.Skipped = true; res.Reason = "selfStorage null"; return res;
        }
        Logger.Info($"SelfStorage Apply: selfStorage runtime type = {ss.GetType().FullName}");

        var allItem = ReadFieldOrProperty(ss, "allItem");
        if (allItem == null)
        {
            Logger.Info("SelfStorage Apply: selfStorage.allItem null");
            res.Skipped = true; res.Reason = "selfStorage.allItem null"; return res;
        }
        Logger.Info($"SelfStorage Apply: allItem runtime type = {allItem.GetType().FullName}");

        // selfStorage.allItem 의 element type 발견
        var listType = allItem.GetType();
        Type? wrapperType = null;
        if (listType.IsGenericType && listType.GetGenericArguments().Length == 1)
            wrapperType = listType.GetGenericArguments()[0];
        if (wrapperType == null)
        {
            int initialCount = IL2CppListOps.Count(allItem);
            for (int i = 0; i < initialCount; i++)
            {
                var sample = IL2CppListOps.Get(allItem, i);
                if (sample != null) { wrapperType = sample.GetType(); break; }
            }
        }
        if (wrapperType == null)
        {
            res.Skipped = true; res.Reason = "wrapperType undetermined (selfStorage 비어있음 — generic arg 도 미확인)";
            return res;
        }
        Logger.Info($"SelfStorage Apply: wrapperType = {wrapperType.FullName}");

        // ItemType ctor 확정 (ItemListApplier 와 동일)
        ConstructorInfo? ctor = null;
        Type? itemTypeEnum = null;
        foreach (var c in wrapperType.GetConstructors(F))
        {
            var ps = c.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.IsEnum && ps[0].ParameterType.Name == "ItemType")
            {
                ctor = c; itemTypeEnum = ps[0].ParameterType; break;
            }
        }
        if (ctor == null)
        {
            res.Skipped = true; res.Reason = "ItemType ctor not found"; return res;
        }
        Logger.Info($"SelfStorage Apply: ItemType ctor selected");

        // game-self method 후보 dump (식별용 — 1회 출력)
        DiscoverStorageMethods(player.GetType());

        // Clear — 1차 시도: 직접 list clear (인벤토리와 달리 grid UI 없음, 직접 manipulation 안전 가설)
        int beforeCount = IL2CppListOps.Count(allItem);
        try
        {
            IL2CppListOps.Clear(allItem);
            int afterCount = IL2CppListOps.Count(allItem);
            res.RemovedCount = beforeCount - afterCount;
            Logger.Info($"SelfStorage clear (direct list.Clear): {beforeCount} → {afterCount}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"SelfStorage clear: {ex.GetType().Name}: {ex.Message}");
            res.Skipped = true; res.Reason = $"clear failed: {ex.Message}"; return res;
        }

        // Add — 2-pass retry. 직접 list.Add (reflection)
        var addM = listType.GetMethod("Add", F);
        if (addM == null)
        {
            res.Skipped = true; res.Reason = $"list type {listType.FullName} has no Add method";
            return res;
        }

        for (int pass = 0; pass < 2; pass++)
        {
            int beforePass = IL2CppListOps.Count(allItem);
            int succeeded = 0, failed = 0;
            foreach (var entry in entries)
            {
                try
                {
                    int type = entry.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.Number
                               ? tEl.GetInt32() : 0;
                    var wrapper = ctor.Invoke(new object[] { Enum.ToObject(itemTypeEnum!, type) });

                    // Deep-copy via ItemListApplier helper (re-use)
                    ItemListApplier.ApplyJsonToObject(entry, wrapper, depth: 0);

                    addM.Invoke(allItem, new object[] { wrapper });
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed++;
                    if (pass == 0)
                        Logger.Warn($"SelfStorage add pass={pass}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            int afterPass = IL2CppListOps.Count(allItem);
            Logger.Info($"SelfStorage add pass={pass}: count {beforePass} → {afterPass} " +
                        $"(target={entries.Count}, succeeded={succeeded}, failed={failed})");
            res.FailedCount = failed;
            if (afterPass >= entries.Count) break;
        }

        // 보너스: heroID / forceID / money / weight / maxWeight 도 deep-copy (root-level fields)
        if (slot.TryGetProperty("selfStorage", out var ssJson) && ssJson.ValueKind == JsonValueKind.Object)
        {
            try
            {
                CopyScalarField(ssJson, ss, "heroID");
                CopyScalarField(ssJson, ss, "forceID");
                CopyScalarField(ssJson, ss, "money");
                CopyScalarField(ssJson, ss, "weight");
                CopyScalarField(ssJson, ss, "maxWeight");
            }
            catch (Exception ex)
            {
                Logger.Warn($"SelfStorage scalar fields: {ex.GetType().Name}: {ex.Message}");
            }
        }

        res.AddedCount = entries.Count - res.FailedCount;
        Logger.Info($"SelfStorage Apply done — removed={res.RemovedCount} added={res.AddedCount} failed={res.FailedCount}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
        => Apply(player, backup, new ApplySelection { SelfStorage = true });

    private static bool _methodsDumped;
    private static void DiscoverStorageMethods(Type heroDataType)
    {
        if (_methodsDumped) return;
        _methodsDumped = true;
        try
        {
            var related = heroDataType.GetMethods(F)
                .Where(m => m.Name.IndexOf("Storage", StringComparison.OrdinalIgnoreCase) >= 0
                         || m.Name.IndexOf("Stash", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(m =>
                {
                    var ps = m.GetParameters();
                    return m.Name + "(" + string.Join(",", ps.Select(p => p.ParameterType.Name)) + ")";
                })
                .Distinct()
                .ToList();
            if (related.Count == 0)
                Logger.Info("SelfStorage: HeroData 에 Storage/Stash 관련 method 부재 — 직접 list manipulation 사용");
            else
                Logger.Info("SelfStorage: HeroData storage-related methods = " + string.Join(", ", related));
        }
        catch { }
    }

    private static void CopyScalarField(JsonElement src, object dst, string name)
    {
        if (!src.TryGetProperty(name, out var v)) return;
        var t = dst.GetType();
        var p = t.GetProperty(name, F);
        var f = (p == null) ? t.GetField(name, F) : null;
        if (p == null && f == null) return;
        Type memberType = p?.PropertyType ?? f!.FieldType;
        object? converted = memberType == typeof(int)    ? v.GetInt32()
                          : memberType == typeof(long)   ? v.GetInt64()
                          : memberType == typeof(float)  ? (object)v.GetSingle()
                          : memberType == typeof(double) ? (object)v.GetDouble()
                          : memberType == typeof(bool)   ? (object)v.GetBoolean()
                          : memberType == typeof(string) ? v.GetString() ?? ""
                          : null;
        if (converted == null) return;
        if (p != null && p.CanWrite) p.SetValue(dst, converted);
        else if (f != null) f.SetValue(dst, converted);
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
}
