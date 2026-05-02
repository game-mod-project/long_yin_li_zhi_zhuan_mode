using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5.4 — 인벤토리 list Replace + subData 디테일 복원.
///
/// v0.5.3 → v0.5.4 변경점:
///   1. Filter fix: itemID 기준 필터 폐기. type=0+name=""+모든 subData null = 빈 슬롯,
///      그 외는 real item (책처럼 itemID=0 인 경우도 모두 포함).
///   2. subData reflection deep-copy: equipmentData / medFoodData / bookData /
///      treasureData / materialData / horseData 의 primitive + nested object +
///      Dictionary<int,float> (heroSpeAddData) 까지 JSON 에서 복원.
///   3. ItemEntry record 폐기 — JsonElement 직접 사용 (subData 까지 풀 복원 위해).
///
/// 게임-self method:
///   - Clear: HeroData.LoseAllItem() — parameterless
///   - Wrapper ctor: ItemData(ItemType _type) — IL2CppInterop wrapper, parameterless 부재
///   - Add: HeroData.GetItem(ItemData wrapper, bool showPopInfo)
///
/// 2-pass retry — game-internal silent fail 회피 (v0.5.2 패턴).
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

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string ClearMethodName = "LoseAllItem";
    private const string AddMethodName   = "GetItem";

    private static readonly string[] SubDataKeys =
        { "equipmentData", "medFoodData", "bookData", "treasureData", "materialData", "horseData" };

    /// <summary>
    /// 슬롯 JSON 의 itemListData.allItem 에서 non-empty entry 만 추출.
    /// v0.5.4 filter: type=0 + empty name + 모든 subData null = 빈 슬롯 → skip.
    /// 그 외는 모두 real item (itemID 무관 — 책 등 itemID=0 인 real item 포함).
    /// </summary>
    public static IReadOnlyList<JsonElement> ExtractItemEntries(JsonElement slot)
    {
        var list = new List<JsonElement>();
        if (!slot.TryGetProperty("itemListData", out var ild) || ild.ValueKind != JsonValueKind.Object)
            return list;
        if (!ild.TryGetProperty("allItem", out var arr) || arr.ValueKind != JsonValueKind.Array)
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

        if (!sel.ItemList) { res.Skipped = true; res.Reason = "itemList (selection off)"; return res; }

        var entries = ExtractItemEntries(slot);
        if (player == null) { res.Skipped = true; res.Reason = "player null (test mode)"; return res; }

        Logger.Info($"ItemList Apply: extracted {entries.Count} non-empty entries from slot JSON");
        Logger.Info($"ItemList Apply: player type = {player.GetType().FullName}");

        var ild = ReadFieldOrProperty(player, "itemListData");
        if (ild == null)
        {
            Logger.Info("ItemList Apply: itemListData null");
            res.Skipped = true; res.Reason = "itemListData null"; return res;
        }
        Logger.Info($"ItemList Apply: itemListData runtime type = {ild.GetType().FullName}");

        var allItem = ReadFieldOrProperty(ild, "allItem");
        if (allItem == null)
        {
            Logger.Info("ItemList Apply: allItem null");
            res.Skipped = true; res.Reason = "allItem null"; return res;
        }
        Logger.Info($"ItemList Apply: allItem runtime type = {allItem.GetType().FullName}");

        // Wrapper type 발견
        Type? wrapperType = null;
        int initialCount = IL2CppListOps.Count(allItem);
        Logger.Info($"ItemList Apply: allItem initialCount = {initialCount}");
        for (int i = 0; i < initialCount; i++)
        {
            var sample = IL2CppListOps.Get(allItem, i);
            if (sample != null) { wrapperType = sample.GetType(); break; }
        }
        if (wrapperType == null)
        {
            res.Skipped = true; res.Reason = "wrapperType null (allItem empty before clear)";
            return res;
        }
        Logger.Info($"ItemList Apply: wrapperType = {wrapperType.FullName}");

        // ItemType ctor 확정 (v0.5.3 spike — IL2CppInterop wrapper 는 parameterless 부재)
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
        Logger.Info($"ItemList Apply: ItemType ctor selected");

        // Clear
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
            res.Skipped = true; res.Reason = $"clear failed: {ex.Message}"; return res;
        }

        // Add — 2-pass retry (v0.5.2 패턴)
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

                    // Deep-copy all root + subData fields from JSON entry to wrapper
                    ApplyJsonToObject(entry, wrapper, depth: 0);

                    InvokeMethod(player, AddMethodName, new object[] { wrapper, false });
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed++;
                    if (pass == 0)
                        Logger.Warn($"ItemList add pass={pass}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            int afterPass = IL2CppListOps.Count(allItem);
            Logger.Info($"ItemList add pass={pass}: count {beforePass} → {afterPass} " +
                        $"(target={entries.Count}, succeeded={succeeded}, failed={failed})");
            res.FailedCount = failed;
            if (afterPass >= entries.Count) break;
        }

        res.AddedCount = entries.Count - res.FailedCount;
        Logger.Info($"ItemList Apply done — removed={res.RemovedCount} added={res.AddedCount} failed={res.FailedCount}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
        => Apply(player, backup, new ApplySelection { ItemList = true });

    // ============================================================ generic deep-copy

    /// <summary>
    /// JSON object 의 모든 property 를 wrapper instance 에 reflection 으로 deep-copy.
    /// - Primitive: 직접 set
    /// - Enum: int → enum 변환
    /// - Nested object: 기존 sub-instance (ctor 자동생성) 가 있으면 recurse
    /// - Dictionary<int,float> (heroSpeAddData 등): clear + add via reflection
    /// - 해당 멤버 부재: silent skip (해당 type 에 없는 field — 정상)
    /// </summary>
    /// <summary>v0.5.5 — SelfStorageApplier 가 동일 deep-copy 재사용. internal 노출.</summary>
    internal static void ApplyJsonToObject(JsonElement json, object obj, int depth)
    {
        if (json.ValueKind != JsonValueKind.Object) return;
        if (depth > 6) return;  // recursion guard

        var t = obj.GetType();
        foreach (var prop in json.EnumerateObject())
        {
            try
            {
                SetMemberFromJson(obj, t, prop.Name, prop.Value, depth);
            }
            catch (Exception ex)
            {
                if (depth == 0)
                    Logger.Info($"  copy {t.Name}.{prop.Name} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void SetMemberFromJson(object obj, Type t, string name, JsonElement val, int depth)
    {
        var p = t.GetProperty(name, F);
        FieldInfo? f = (p == null) ? t.GetField(name, F) : null;
        if (p == null && f == null) return;
        Type memberType = p?.PropertyType ?? f!.FieldType;

        if (val.ValueKind == JsonValueKind.Null)
        {
            // null 은 skip — IL2CPP wrapper 의 자동생성 sub-instance 보호
            // (game ctor 가 type 에 따라 적절한 subData 자동 채움)
            return;
        }

        // ----- primitives
        if (memberType == typeof(int))    { Set(obj, p, f, val.GetInt32());    return; }
        if (memberType == typeof(long))   { Set(obj, p, f, val.GetInt64());    return; }
        if (memberType == typeof(string)) { Set(obj, p, f, val.GetString() ?? ""); return; }
        if (memberType == typeof(bool))   { Set(obj, p, f, val.GetBoolean());  return; }
        if (memberType == typeof(float))  { Set(obj, p, f, val.GetSingle());   return; }
        if (memberType == typeof(double)) { Set(obj, p, f, val.GetDouble());   return; }
        if (memberType.IsEnum)
        {
            try { Set(obj, p, f, Enum.ToObject(memberType, val.GetInt32())); } catch { }
            return;
        }

        // ----- reference types — read existing, modify in place
        object? existing = p != null ? p.GetValue(obj) : f!.GetValue(obj);

        if (val.ValueKind == JsonValueKind.Object)
        {
            // Dictionary<TKey, TValue> (heroSpeAddData 등)
            if (IsDictionary(memberType, out var keyType, out var valueType))
            {
                if (existing == null) return;
                ApplyJsonDict(val, existing, keyType!, valueType!);
                return;
            }
            // Nested object — recurse into existing instance
            if (existing != null) ApplyJsonToObject(val, existing, depth + 1);
            return;
        }

        if (val.ValueKind == JsonValueKind.Array)
        {
            if (existing == null) return;
            ApplyJsonArray(val, existing, memberType);
            return;
        }
    }

    private static void Set(object obj, PropertyInfo? p, FieldInfo? f, object? value)
    {
        if (p != null && p.CanWrite) p.SetValue(obj, value);
        else if (f != null) f.SetValue(obj, value);
    }

    private static bool IsDictionary(Type t, out Type? keyType, out Type? valueType)
    {
        keyType = null; valueType = null;
        if (!t.IsGenericType) return false;
        // CLR Dictionary or Il2CppSystem.Collections.Generic.Dictionary
        if (t.Name != "Dictionary`2") return false;
        var args = t.GetGenericArguments();
        if (args.Length != 2) return false;
        keyType = args[0]; valueType = args[1];
        return true;
    }

    private static void ApplyJsonDict(JsonElement json, object dict, Type keyType, Type valueType)
    {
        var dictType = dict.GetType();
        var clearM = dictType.GetMethod("Clear", F, null, Type.EmptyTypes, null);
        var indexerSet = dictType.GetMethod("set_Item", F, null, new[] { keyType, valueType }, null);
        var addM = (indexerSet == null)
                   ? dictType.GetMethod("Add", F, null, new[] { keyType, valueType }, null)
                   : null;

        try { clearM?.Invoke(dict, null); } catch { }
        if (indexerSet == null && addM == null) return;

        foreach (var kv in json.EnumerateObject())
        {
            try
            {
                object? k = ConvertScalar(kv.Name, keyType);
                object? v = ConvertScalar(kv.Value, valueType);
                if (k == null) continue;
                var args = new[] { k, v! };
                if (indexerSet != null) indexerSet.Invoke(dict, args);
                else addM!.Invoke(dict, args);
            }
            catch { }
        }
    }

    private static void ApplyJsonArray(JsonElement json, object target, Type targetType)
    {
        // IL2Cpp / CLR generic List<T>
        if (targetType.IsGenericType && targetType.Name == "List`1")
        {
            var elemType = targetType.GetGenericArguments()[0];
            try { IL2CppListOps.Clear(target); } catch { }
            var addM = targetType.GetMethod("Add", F);
            if (addM == null) return;
            for (int i = 0; i < json.GetArrayLength(); i++)
            {
                try
                {
                    var jv = json[i];
                    if (jv.ValueKind == JsonValueKind.Null) continue;
                    if (elemType == typeof(int))    { addM.Invoke(target, new object[] { jv.GetInt32() });    continue; }
                    if (elemType == typeof(long))   { addM.Invoke(target, new object[] { jv.GetInt64() });    continue; }
                    if (elemType == typeof(string)) { addM.Invoke(target, new object[] { jv.GetString() ?? "" }); continue; }
                    if (elemType == typeof(bool))   { addM.Invoke(target, new object[] { jv.GetBoolean() });  continue; }
                    if (elemType == typeof(float))  { addM.Invoke(target, new object[] { jv.GetSingle() });   continue; }
                    if (elemType == typeof(double)) { addM.Invoke(target, new object[] { jv.GetDouble() });   continue; }
                    // 중첩 List<List<T>> 같은 복합 type 은 v0.5.4 scope 외 (treasure playerGuessTreasureLv 등)
                }
                catch { }
            }
        }
    }

    private static object? ConvertScalar(string raw, Type targetType)
    {
        if (targetType == typeof(int))    return int.Parse(raw);
        if (targetType == typeof(long))   return long.Parse(raw);
        if (targetType == typeof(string)) return raw;
        if (targetType.IsEnum && int.TryParse(raw, out var i)) return Enum.ToObject(targetType, i);
        return null;
    }

    private static object? ConvertScalar(JsonElement val, Type targetType)
    {
        if (val.ValueKind == JsonValueKind.Null) return null;
        if (targetType == typeof(int))    return val.GetInt32();
        if (targetType == typeof(long))   return val.GetInt64();
        if (targetType == typeof(string)) return val.GetString() ?? "";
        if (targetType == typeof(bool))   return val.GetBoolean();
        if (targetType == typeof(float))  return val.GetSingle();
        if (targetType == typeof(double)) return val.GetDouble();
        if (targetType.IsEnum) return Enum.ToObject(targetType, val.GetInt32());
        return null;
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

    private static void InvokeMethod(object obj, string methodName, object[] args)
    {
        var t = obj.GetType();
        MethodInfo? best = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != methodName) continue;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) continue;
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
