using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.2 — 개별 ItemData reflection helper.
/// Task 0 spike (docs/superpowers/dumps/2026-05-XX-v0.7.2-grade-quality-spike.md) 결과를
/// 후보 array 에 반영한다. spike 가 enum 한자 string 으로 노출하면 GradeMap / QualityMap 사용.
///
/// 기존 ItemListReflector 는 list-level (maxWeight) 전용이라 분리.
/// </summary>
public static class ItemReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Task 0 spike 결과 채움. 미발견 시 빈 array → -1 반환.
    private static readonly string[] GRADE_NAMES   = new[] { "grade", "level", "lv", "tier", "rank" };
    private static readonly string[] QUALITY_NAMES = new[] { "quality", "purity", "pin", "pinji", "pinzhi" };

    // Task 0 spike 결과 한자 string 으로 노출되면 enable. int 노출이면 dictionary lookup skip.
    private static readonly Dictionary<string, int> GradeMap = new()
    {
        ["劣"]    = 0, ["普"] = 1, ["优"] = 2, ["精"] = 3, ["完美"] = 4, ["绝世"] = 5,
        ["열악"] = 0, ["보통"] = 1, ["우수"] = 2, ["정량"] = 3, ["완벽"] = 4, ["절세"] = 5,
    };
    private static readonly Dictionary<string, int> QualityMap = new()
    {
        ["残"]    = 0, ["下"] = 1, ["中"] = 2, ["上"] = 3, ["珍"] = 4, ["极"] = 5,
        ["잔품"] = 0, ["하품"] = 1, ["중품"] = 2, ["상품"] = 3, ["진품"] = 4, ["극품"] = 5,
    };

    public static int GetGradeOrder(object? item)   => Read(item, GRADE_NAMES, GradeMap);
    public static int GetQualityOrder(object? item) => Read(item, QUALITY_NAMES, QualityMap);

    public static string GetCategoryKey(object? item)
    {
        if (item == null) return "";
        // ContainerRowBuilder 가 이미 Type / SubType 을 채우므로 그것을 합성. 직접 reflection 도 가능.
        int t  = ReadInt(item, "type");
        int st = ReadInt(item, "subType");
        return $"{t:D3}.{st:D3}";
    }

    public static string GetNameRaw(object? item)
    {
        if (item == null) return "";
        var v = ReadObj(item, "name");
        return v as string ?? "";
    }

    // ------- internals -------

    private static int Read(object? item, string[] names, Dictionary<string, int> map)
    {
        if (item == null) return -1;
        var t = item.GetType();
        foreach (var name in names)
        {
            var raw = ReadFieldOrProperty(t, item, name);
            if (raw == null) continue;
            // int / byte / short / long → int 캐스팅
            if (raw is System.IConvertible)
            {
                try
                {
                    int n = System.Convert.ToInt32(raw);
                    if (raw is string s) { return map.TryGetValue(s, out var ord) ? ord : -1; }
                    return n;
                }
                catch (System.Exception ex) { Logger.Warn($"ItemReflector.Read int cast {name}: {ex.Message}"); }
            }
            // string (한자 enum)
            if (raw is string str)
            {
                return map.TryGetValue(str, out var ord) ? ord : -1;
            }
            // Il2CppSystem.Enum 또는 .NET enum 의 ToString
            var s2 = raw.ToString() ?? "";
            if (map.TryGetValue(s2, out var ord2)) return ord2;
        }
        return -1;
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
        catch (System.Exception ex) { Logger.Warn($"ItemReflector read {name}: {ex.Message}"); }
        return null;
    }

    private static object? ReadObj(object obj, string name) => ReadFieldOrProperty(obj.GetType(), obj, name);

    private static int ReadInt(object obj, string name)
    {
        var v = ReadObj(obj, name);
        if (v == null) return 0;
        try { return System.Convert.ToInt32(v); } catch { return 0; }
    }
}
