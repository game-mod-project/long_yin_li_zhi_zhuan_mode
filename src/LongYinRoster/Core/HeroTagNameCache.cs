using System;
using System.Collections.Generic;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 — heroTagDataBase (GameDataController) iterate → tagID → meta cache.
/// Spike v3 결과 (2026-05-09): HeroTagDataBase 의 fields:
///   - name (String) — 한자 라벨 (HangulDict 통과)
///   - value (Int32) — raw 단계 점수 (1/2/4/8/...). 인게임 디스플레이 = value × 4 (4/8/16/32)
///   - category (String) — 한자 카테고리 (武学/高级/技艺/天生/志向/趣向/战法)
///   - sameMeaning (String) — 단계 그룹 식별자 (같은 그룹의 단계 모음)
///   - order (Int32) — 정렬용
/// </summary>
public static class HeroTagNameCache
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public sealed class TagMeta
    {
        public int    TagID       { get; init; }
        public string NameKr      { get; init; } = "";
        public int    Value       { get; init; }
        public string CategoryKr  { get; init; } = "";
        public string SameMeaning { get; init; } = "";
        public int    Order       { get; init; }
    }

    private static Dictionary<int, TagMeta>? _meta;
    private static List<string>? _categoryOrder;   // 발견 순서대로 보존
    private static readonly object _lock = new();

    /// <summary>tagID → 한글 라벨 (단계 점수 포함). miss 시 "태그(N)".</summary>
    public static string Get(int tagID)
    {
        EnsureBuilt();
        if (_meta != null && _meta.TryGetValue(tagID, out var m)) return $"{m.NameKr}({m.Value * 4})";
        return $"태그({tagID})";
    }

    /// <summary>tagID → meta 객체 (null = miss).</summary>
    public static TagMeta? GetMeta(int tagID)
    {
        EnsureBuilt();
        if (_meta != null && _meta.TryGetValue(tagID, out var m)) return m;
        return null;
    }

    /// <summary>전체 entry — SelectorDialog 용.</summary>
    public static IReadOnlyList<(int Value, string Label)> AllOrdered()
    {
        EnsureBuilt();
        var list = new List<(int, string)>();
        if (_meta == null) return list;
        var keys = new List<int>(_meta.Keys);
        keys.Sort();
        foreach (var k in keys)
        {
            var m = _meta[k];
            list.Add((k, $"{m.NameKr} [{m.CategoryKr}/{m.Value * 4}점]"));
        }
        return list;
    }

    /// <summary>발견된 카테고리 list (한글) — 탭 source.</summary>
    public static IReadOnlyList<string> CategoryList()
    {
        EnsureBuilt();
        return _categoryOrder ?? (IReadOnlyList<string>)System.Array.Empty<string>();
    }

    /// <summary>SelectorDialog 카테고리 탭 — 7 카테고리 + 전체.</summary>
    public static IReadOnlyList<(string TabLabel, Func<int, bool> Filter)> BuildCategoryTabs()
    {
        EnsureBuilt();
        var tabs = new List<(string, Func<int, bool>)>();
        tabs.Add(("전체", _ => true));
        if (_categoryOrder != null)
        {
            foreach (var cat in _categoryOrder)
            {
                string captured = cat;
                tabs.Add((captured, tagID =>
                {
                    if (_meta != null && _meta.TryGetValue(tagID, out var m)) return m.CategoryKr == captured;
                    return false;
                }));
            }
        }
        return tabs;
    }

    public static void ResetForTests()
    {
        lock (_lock) { _meta = null; _categoryOrder = null; }
    }

    private static void EnsureBuilt()
    {
        if (_meta != null) return;
        lock (_lock)
        {
            if (_meta != null) return;
            BuildFromGame();
        }
    }

    private static void BuildFromGame()
    {
        var meta = new Dictionary<int, TagMeta>();
        var catSet = new HashSet<string>();
        var catOrder = new List<string>();

        try
        {
            var gdcType = Type.GetType("GameDataController, Assembly-CSharp");
            if (gdcType == null) { _meta = meta; _categoryOrder = catOrder; return; }
            var instProp = gdcType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            object? gdc = instProp?.GetValue(null);
            if (gdc == null) { _meta = meta; _categoryOrder = catOrder; return; }

            var dbProp = gdc.GetType().GetProperty("heroTagDataBase", F);
            object? db = dbProp?.GetValue(gdc);
            if (db == null) { _meta = meta; _categoryOrder = catOrder; return; }

            var countProp = db.GetType().GetProperty("Count", F);
            int n = countProp != null ? Convert.ToInt32(countProp.GetValue(db)) : 0;
            var indexer = db.GetType().GetMethod("get_Item", F);
            if (indexer == null) { _meta = meta; _categoryOrder = catOrder; return; }

            for (int i = 0; i < n; i++)
            {
                try
                {
                    var entry = indexer.Invoke(db, new object[] { i });
                    if (entry == null) continue;

                    int tagID  = i;   // index = tagID (cheat 패턴)
                    string raw = ReadStr(entry, "name");
                    int    val = ReadInt(entry, "value");
                    string catRaw  = ReadStr(entry, "category");
                    string sameRaw = ReadStr(entry, "sameMeaning");
                    int    order = ReadInt(entry, "order");

                    string nameKr = !string.IsNullOrEmpty(raw)
                        ? HangulDict.Translate(raw)
                        : $"태그({tagID})";
                    string catKr  = !string.IsNullOrEmpty(catRaw)
                        ? HangulDict.Translate(catRaw)
                        : "";

                    meta[tagID] = new TagMeta
                    {
                        TagID = tagID,
                        NameKr = nameKr,
                        Value = val,
                        CategoryKr = catKr,
                        SameMeaning = sameRaw,
                        Order = order,
                    };

                    if (!string.IsNullOrEmpty(catKr) && catSet.Add(catKr))
                        catOrder.Add(catKr);
                }
                catch (Exception ex)
                {
                    Logger.WarnOnce("HeroTagNameCache", $"HeroTagNameCache build {i}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            Logger.Info($"HeroTagNameCache: built {meta.Count} entries, {catOrder.Count} categories");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagNameCache", $"HeroTagNameCache.BuildFromGame: {ex.GetType().Name}: {ex.Message}");
        }
        _meta = meta;
        _categoryOrder = catOrder;
    }

    private static int ReadInt(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return Convert.ToInt32(p.GetValue(obj));
            var f = t.GetField(name, F);
            if (f != null) return Convert.ToInt32(f.GetValue(obj));
        }
        catch { }
        return 0;
    }

    private static string ReadStr(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj)?.ToString() ?? "";
            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj)?.ToString() ?? "";
        }
        catch { }
        return "";
    }
}
