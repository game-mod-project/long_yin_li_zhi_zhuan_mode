using System;
using System.Collections.Generic;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 — heroTagData (List&lt;HeroTagData&gt;) read/write/add/remove 헬퍼.
/// Spike 결과 (2026-05-09):
///   - heroTagData type = `Il2CppSystem.Collections.Generic.List&lt;HeroTagData&gt;` (Property)
///   - HeroTagData.tagID (int) / leftTime (Single, -1=영구) / sourceHero (string)
///   - add: `player.AddTag(int tagID, float leftTime, string sourceHero, bool, bool)`
///   - remove: `player.RemoveTag(int tagID, bool)`
///   - query: `player.FindTag(int)` / `HaveTag(int)`
/// </summary>
public static class HeroTagDataReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>현재 등록된 tag entry list — (tagID, leftTime, sourceHero).</summary>
    public static List<(int TagID, float LeftTime, string SourceHero)> GetEntries(object player)
    {
        var result = new List<(int, float, string)>();
        if (player == null) return result;
        try
        {
            var tagList = ReadFieldOrProperty(player, "heroTagData");
            if (tagList == null) return result;

            var countProp = tagList.GetType().GetProperty("Count", F);
            if (countProp == null) return result;
            int n = Convert.ToInt32(countProp.GetValue(tagList));
            var indexer = tagList.GetType().GetMethod("get_Item", F);
            if (indexer == null) return result;

            for (int i = 0; i < n; i++)
            {
                var entry = indexer.Invoke(tagList, new object[] { i });
                if (entry == null) continue;
                int tagID = ReadInt(entry, "tagID");
                float leftTime = ReadFloat(entry, "leftTime");
                string sourceHero = ReadString(entry, "sourceHero");
                result.Add((tagID, leftTime, sourceHero));
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.GetEntries: {ex.GetType().Name}: {ex.Message}");
        }
        return result;
    }

    /// <summary>player.AddTag(tagID, leftTime, sourceHero, bool, bool) 호출.</summary>
    public static bool TryAddTag(object player, int tagID, float leftTime = -1f, string sourceHero = "")
    {
        if (player == null) return false;
        try
        {
            var addM = FindMethod(player, "AddTag", new[] { typeof(int), typeof(float), typeof(string), typeof(bool), typeof(bool) });
            if (addM != null)
            {
                addM.Invoke(player, new object[] { tagID, leftTime, sourceHero ?? "", false, false });
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.TryAddTag({tagID}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>player.RemoveTag(tagID, bool) 호출.</summary>
    public static bool TryRemoveTag(object player, int tagID)
    {
        if (player == null) return false;
        try
        {
            var removeM = FindMethod(player, "RemoveTag", new[] { typeof(int), typeof(bool) });
            if (removeM != null)
            {
                removeM.Invoke(player, new object[] { tagID, false });
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.TryRemoveTag({tagID}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>FindTag(tagID) → HeroTagData (현재 등록된 tag entry).</summary>
    public static object? FindTag(object player, int tagID)
    {
        if (player == null) return null;
        try
        {
            var findM = FindMethod(player, "FindTag", new[] { typeof(int) });
            if (findM != null) return findM.Invoke(player, new object[] { tagID });
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.FindTag({tagID}): {ex.GetType().Name}: {ex.Message}");
        }
        return null;
    }

    /// <summary>v0.7.8 — 천부점 (heroTagPoint) read.</summary>
    public static float GetTagPoint(object player)
    {
        if (player == null) return 0f;
        try { var v = ReadFieldOrProperty(player, "heroTagPoint"); return v == null ? 0f : Convert.ToSingle(v); }
        catch { return 0f; }
    }

    /// <summary>v0.7.8 — ChangeTagPoint(delta, false) 호출. delta 기반.</summary>
    public static bool TrySetTagPoint(object player, float newValue)
    {
        if (player == null) return false;
        try
        {
            float current = GetTagPoint(player);
            float delta = newValue - current;
            var changeM = FindMethod(player, "ChangeTagPoint", new[] { typeof(float), typeof(bool) });
            if (changeM != null)
            {
                changeM.Invoke(player, new object[] { delta, false });
                return true;
            }
            // fallback — direct setter
            var p = player.GetType().GetProperty("heroTagPoint", F);
            if (p != null && p.CanWrite) { p.SetValue(player, newValue); return true; }
            var f = player.GetType().GetField("heroTagPoint", F);
            if (f != null) { f.SetValue(player, newValue); return true; }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.TrySetTagPoint({newValue}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>v0.7.8 — 영구 천부 보유 수 (game-self method GetHeroPermanentTagNum).</summary>
    public static int GetPermanentTagCount(object player)
    {
        if (player == null) return 0;
        try
        {
            var m = FindMethod(player, "GetHeroPermanentTagNum", Type.EmptyTypes);
            if (m != null) return Convert.ToInt32(m.Invoke(player, null));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.GetPermanentTagCount: {ex.GetType().Name}: {ex.Message}");
        }
        return 0;
    }

    /// <summary>v0.7.8 — 최대 천부 보유 가능 수 (game-self method GetMaxTagNum).</summary>
    public static int GetMaxTagCount(object player)
    {
        if (player == null) return 0;
        try
        {
            var m = FindMethod(player, "GetMaxTagNum", Type.EmptyTypes);
            if (m != null) return Convert.ToInt32(m.Invoke(player, null));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.GetMaxTagCount: {ex.GetType().Name}: {ex.Message}");
        }
        return 0;
    }

    /// <summary>HaveTag(tagID) → bool.</summary>
    public static bool HaveTag(object player, int tagID)
    {
        if (player == null) return false;
        try
        {
            var haveM = FindMethod(player, "HaveTag", new[] { typeof(int) });
            if (haveM != null) return Convert.ToBoolean(haveM.Invoke(player, new object[] { tagID }));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.HaveTag({tagID}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// tagID 의 leftTime 변경 (existing entry 의 value 수정). FindTag → leftTime setter.
    /// 영구 tag = leftTime=-1, 임시 tag = leftTime>0.
    /// </summary>
    public static bool TrySetLeftTime(object player, int tagID, float leftTime)
    {
        var entry = FindTag(player, tagID);
        if (entry == null) return false;
        try
        {
            var t = entry.GetType();
            var prop = t.GetProperty("leftTime", F);
            if (prop != null && prop.CanWrite) { prop.SetValue(entry, leftTime); return true; }
            var fld = t.GetField("leftTime", F);
            if (fld != null) { fld.SetValue(entry, leftTime); return true; }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroTagDataReflector", $"HeroTagDataReflector.TrySetLeftTime({tagID}, {leftTime}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    // ───── helpers ─────

    private static MethodInfo? FindMethod(object obj, string name, Type[] paramTypes)
    {
        foreach (var m in obj.GetType().GetMethods(F))
        {
            if (m.Name != name) continue;
            var ps = m.GetParameters();
            if (ps.Length != paramTypes.Length) continue;
            bool match = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != paramTypes[i]) { match = false; break; }
            }
            if (match) return m;
        }
        return null;
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var f = t.GetField(name, F);
        if (f != null) { try { return f.GetValue(obj); } catch { return null; } }
        var p = t.GetProperty(name, F);
        if (p != null) { try { return p.GetValue(obj); } catch { return null; } }
        return null;
    }

    private static int ReadInt(object obj, string name)
    {
        try { var v = ReadFieldOrProperty(obj, name); return v == null ? 0 : Convert.ToInt32(v); }
        catch { return 0; }
    }

    private static float ReadFloat(object obj, string name)
    {
        try { var v = ReadFieldOrProperty(obj, name); return v == null ? 0f : Convert.ToSingle(v); }
        catch { return 0f; }
    }

    private static string ReadString(object obj, string name)
    {
        try { var v = ReadFieldOrProperty(obj, name); return v?.ToString() ?? ""; }
        catch { return ""; }
    }
}
