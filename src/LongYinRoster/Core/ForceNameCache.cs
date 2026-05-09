using System;
using System.Collections.Generic;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 — forceDataBase (GameDataController) iterate → forceID → 한글 문파명 cache.
/// Cheat 패턴 mirror — `gDC.forceDataBase[i].forceID + .forceName + TranslationHelper.Translate`.
/// 우리는 HangulDict.Translate (v0.7.5 자산) 사용.
/// </summary>
public static class ForceNameCache
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Dictionary<int, string>? _cache;
    private static readonly object _lock = new();

    /// <summary>forceID → 한글 문파명. miss 시 "강호" (cheat 패턴) 또는 "문파(N)".</summary>
    public static string Get(int forceID)
    {
        if (forceID < 0) return "강호";
        EnsureBuilt();
        if (_cache != null && _cache.TryGetValue(forceID, out var name) && !string.IsNullOrEmpty(name)) return name;
        return $"문파({forceID})";
    }

    public static void ResetForTests()
    {
        lock (_lock) { _cache = null; }
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
        try
        {
            var gdcType = Type.GetType("GameDataController, Assembly-CSharp");
            if (gdcType == null) { _cache = dict; return; }
            var instProp = gdcType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            object? gdc = instProp?.GetValue(null);
            if (gdc == null) { _cache = dict; return; }

            var dbProp = gdc.GetType().GetProperty("forceDataBase", F);
            object? db = dbProp?.GetValue(gdc);
            if (db == null) { _cache = dict; return; }

            var countProp = db.GetType().GetProperty("Count", F);
            int n = countProp != null ? Convert.ToInt32(countProp.GetValue(db)) : 0;
            var indexer = db.GetType().GetMethod("get_Item", F);
            if (indexer == null) { _cache = dict; return; }

            for (int i = 0; i < n; i++)
            {
                try
                {
                    var entry = indexer.Invoke(db, new object[] { i });
                    if (entry == null) continue;
                    int forceID = ReadInt(entry, "forceID");
                    string raw  = ReadStr(entry, "forceName");
                    string nameKr = !string.IsNullOrEmpty(raw)
                        ? HangulDict.Translate(raw)
                        : $"문파({forceID})";
                    dict[forceID] = nameKr;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"ForceNameCache build entry {i}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            Logger.Info($"ForceNameCache: built {dict.Count} entries");
        }
        catch (Exception ex)
        {
            Logger.Warn($"ForceNameCache.BuildFromGame: {ex.GetType().Name}: {ex.Message}");
        }
        _cache = dict;
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
