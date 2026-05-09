using System;
using System.Collections.Generic;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 — kungfuSkillDataBase (GameDataController) iterate → skillID → 한글 무공명 cache.
/// Cheat SkillManager 패턴 — `kungfuSkillDataBase[i].name` + `TranslationHelper.Translate`.
/// 우리 mod 의 HangulDict.Translate (v0.7.5 자산) 사용. Lazy init.
/// </summary>
public static class SkillNameCache
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Dictionary<int, string>? _cache;
    private static Dictionary<int, int>? _typeCache;   // skillID → type (0=내공/1=경공/2=절기/3=권장/4=검법/5=도법/6=장병/7=기문/8=사술)
    private static Dictionary<int, int>? _rareLvCache; // skillID → rareLv 0~5 (기초/진급/상승/비전/정극/절세)
    private static Dictionary<int, int>? _forceCache;  // skillID → belongForceID
    private static readonly object _lock = new();

    /// <summary>cheat SkillManager.cs:163 검증 — KungfuSkillData.type 0~8 매핑.</summary>
    public static readonly string[] TypeNames =
    {
        "내공", "경공", "절기", "권장", "검법", "도법", "장병", "기문", "사술",
    };

    /// <summary>cheat CheatPanels 검증 — KungfuSkillData.rareLv 0~5 매핑.</summary>
    public static readonly string[] RareLvNames =
    {
        "기초", "진급", "상승", "비전", "정극", "절세",
    };

    /// <summary>rareLv idx → 한글 라벨. 외 -> "기타".</summary>
    public static string GetRareLvName(int rareLv)
    {
        if (rareLv >= 0 && rareLv < RareLvNames.Length) return RareLvNames[rareLv];
        return "기타";
    }

    /// <summary>skillID → rareLv (0~5). miss 시 -1.</summary>
    public static int GetRareLv(int skillID)
    {
        EnsureBuilt();
        if (_rareLvCache != null && _rareLvCache.TryGetValue(skillID, out var r)) return r;
        return -1;
    }

    /// <summary>skillID → belongForceID. miss 시 -1.</summary>
    public static int GetForceID(int skillID)
    {
        EnsureBuilt();
        if (_forceCache != null && _forceCache.TryGetValue(skillID, out var f)) return f;
        return -1;
    }

    /// <summary>skillID → 한글 무공명. miss 시 "무공({skillID})".</summary>
    public static string Get(int skillID)
    {
        EnsureBuilt();
        if (_cache != null && _cache.TryGetValue(skillID, out var name)) return name;
        return $"무공({skillID})";
    }

    /// <summary>skillID → type (0~8). miss 시 -1.</summary>
    public static int GetType(int skillID)
    {
        EnsureBuilt();
        if (_typeCache != null && _typeCache.TryGetValue(skillID, out var t)) return t;
        return -1;
    }

    /// <summary>type idx → 한글 카테고리. 0=내공 ... 8=사술. 외 -> "기타".</summary>
    public static string GetTypeName(int type)
    {
        if (type >= 0 && type < TypeNames.Length) return TypeNames[type];
        return "기타";
    }

    /// <summary>전체 entry — SelectorDialog dropdown source. skillID 순서.</summary>
    public static IReadOnlyList<(int Value, string Label)> AllOrdered()
    {
        EnsureBuilt();
        var list = new List<(int, string)>();
        if (_cache == null) return list;
        var keys = new List<int>(_cache.Keys);
        keys.Sort();
        foreach (var k in keys) list.Add((k, _cache[k]));
        return list;
    }

    /// <summary>v0.7.8 — selector dropdown 용 label 풍부화: 무공명 [type/등급/문파].</summary>
    public static IReadOnlyList<(int Value, string Label)> AllOrderedEnriched()
    {
        EnsureBuilt();
        var list = new List<(int, string)>();
        if (_cache == null) return list;
        var keys = new List<int>(_cache.Keys);
        keys.Sort();
        foreach (var k in keys)
        {
            string name = _cache[k];
            string typeName = GetTypeName(GetType(k));
            string rareName = GetRareLvName(GetRareLv(k));
            string forceName = ForceNameCache.Get(GetForceID(k));
            list.Add((k, $"{name} [{typeName}/{rareName}/{forceName}]"));
        }
        return list;
    }

    /// <summary>v0.7.8 — 등급 secondary tab builder (전체/기초/진급/.../절세).</summary>
    public static IReadOnlyList<(string TabLabel, Func<int, bool> Filter)> BuildRareLvTabs()
    {
        EnsureBuilt();
        var tabs = new List<(string, Func<int, bool>)>();
        tabs.Add(("전체", _ => true));
        for (int r = 0; r < RareLvNames.Length; r++)
        {
            int captured = r;
            tabs.Add((RareLvNames[r], skillID => GetRareLv(skillID) == captured));
        }
        return tabs;
    }

    /// <summary>특정 type 의 무공만 — selector tab 용.</summary>
    public static IReadOnlyList<(int Value, string Label)> ByType(int type)
    {
        EnsureBuilt();
        var list = new List<(int, string)>();
        if (_cache == null || _typeCache == null) return list;
        var keys = new List<int>(_cache.Keys);
        keys.Sort();
        foreach (var k in keys)
        {
            if (_typeCache.TryGetValue(k, out var tt) && tt == type)
                list.Add((k, _cache[k]));
        }
        return list;
    }

    public static void ResetForTests()
    {
        lock (_lock) { _cache = null; _typeCache = null; _rareLvCache = null; _forceCache = null; }
    }

    private static void EnsureBuilt()
    {
        if (_cache != null) return;
        lock (_lock)
        {
            if (_cache != null) return;
            BuildFromGame();
        }
    }

    private static void BuildFromGame()
    {
        var dict = new Dictionary<int, string>();
        var typeDict = new Dictionary<int, int>();
        var rareDict = new Dictionary<int, int>();
        var forceDict = new Dictionary<int, int>();

        void AssignAndExit() { _cache = dict; _typeCache = typeDict; _rareLvCache = rareDict; _forceCache = forceDict; }

        try
        {
            var gdcType = Type.GetType("GameDataController, Assembly-CSharp");
            if (gdcType == null) { Logger.Warn("SkillNameCache: GameDataController 미발견"); AssignAndExit(); return; }
            var instProp = gdcType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            object? gdc = instProp?.GetValue(null);
            if (gdc == null) { Logger.Warn("SkillNameCache: GameDataController.Instance null"); AssignAndExit(); return; }

            var dbProp = gdc.GetType().GetProperty("kungfuSkillDataBase", F);
            object? db = dbProp?.GetValue(gdc);
            if (db == null) { Logger.Warn("SkillNameCache: kungfuSkillDataBase null"); AssignAndExit(); return; }

            var countProp = db.GetType().GetProperty("Count", F);
            int n = countProp != null ? Convert.ToInt32(countProp.GetValue(db)) : 0;
            var indexer = db.GetType().GetMethod("get_Item", F);
            if (indexer == null) { Logger.Warn("SkillNameCache: kungfuSkillDataBase indexer 미발견"); AssignAndExit(); return; }

            for (int i = 0; i < n; i++)
            {
                try
                {
                    var entry = indexer.Invoke(db, new object[] { i });
                    if (entry == null) continue;
                    int skillID = ReadIntField(entry, "skillID");
                    if (skillID == 0) skillID = i;
                    string raw = ReadStringField(entry, "name");
                    string nameKr = !string.IsNullOrEmpty(raw)
                        ? HangulDict.Translate(raw)
                        : $"무공({skillID})";
                    dict[skillID] = nameKr;

                    // KungfuSkillData.type (0~8) — cheat SkillManager.cs:167 검증
                    int type = ReadIntField(entry, "type");
                    typeDict[skillID] = type;
                    // KungfuSkillData.rareLv (0~5) — 기초/진급/상승/비전/정극/절세
                    int rareLv = ReadIntField(entry, "rareLv");
                    rareDict[skillID] = rareLv;
                    // KungfuSkillData.belongForceID — 문파 ID
                    int forceID = ReadIntField(entry, "belongForceID");
                    forceDict[skillID] = forceID;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"SkillNameCache build entry {i}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            Logger.Info($"SkillNameCache: built {dict.Count} entries");
        }
        catch (Exception ex)
        {
            Logger.Warn($"SkillNameCache.BuildFromGame: {ex.GetType().Name}: {ex.Message}");
        }
        AssignAndExit();
    }

    private static int ReadIntField(object obj, string name)
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

    private static string ReadStringField(object obj, string name)
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
