using System;
using System.Collections.Generic;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.4 D-1 — ItemData + sub-data wrapper reflection helper.
/// `GetCuratedFields` 는 카테고리별 한글 라벨 매핑 (장비/비급/단약 우선 — Task 3).
/// `GetRawFields` 는 모든 reflection 필드 dump + IL2CPP wrapper meta 필터.
/// 본 module 은 UI 와 무관 — 단위 테스트 가능.
/// </summary>
public static class ItemDetailReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly HashSet<string> WRAPPER_META = new()
    {
        "ObjectClass", "Pointer", "WasCollected", "isWrapped", "pooledPtr",
    };

    private static readonly string[] SUBDATA_WRAPPERS =
    {
        "equipmentData", "medFoodData", "bookData", "treasureData", "materialData", "horseData",
    };

    /// <summary>
    /// item + 활성화 sub-data wrapper 의 모든 reflection 필드 dump.
    /// IL2CPP wrapper meta 필터, 비활성 wrapper 제외.
    /// </summary>
    public static List<(string FieldName, string Value)> GetRawFields(object? item)
    {
        var result = new List<(string, string)>();
        if (item == null) return result;

        DumpFields(result, "", item);
        foreach (var wrapperName in SUBDATA_WRAPPERS)
        {
            var wrapper = ReadFieldOrProperty(item.GetType(), item, wrapperName);
            if (wrapper == null) continue;   // inactive
            DumpFields(result, $"[{wrapperName}] ", wrapper);
        }
        return result;
    }

    private static void DumpFields(List<(string, string)> result, string prefix, object obj)
    {
        var t = obj.GetType();
        foreach (var f in t.GetFields(F))
        {
            if (WRAPPER_META.Contains(f.Name)) continue;
            if (Array.IndexOf(SUBDATA_WRAPPERS, f.Name) >= 0) continue;   // wrapper itself dumped via prefix path
            string val;
            try { val = f.GetValue(obj)?.ToString() ?? "null"; }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            result.Add(($"{prefix}{f.Name}", val));
        }
        foreach (var p in t.GetProperties(F))
        {
            if (WRAPPER_META.Contains(p.Name)) continue;
            if (p.GetIndexParameters().Length > 0) continue;   // skip indexers
            string val;
            try { val = p.GetValue(obj)?.ToString() ?? "null"; }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            result.Add(($"{prefix}{p.Name}", val));
        }
    }

    private static object? ReadFieldOrProperty(Type t, object obj, string name)
    {
        try
        {
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj);
            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj);
        }
        catch { /* swallow */ }
        return null;
    }
}
