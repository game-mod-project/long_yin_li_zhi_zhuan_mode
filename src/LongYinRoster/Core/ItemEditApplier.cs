using System;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.7 — Item edit Apply 결과.
/// </summary>
public sealed class ItemEditResult
{
    public bool   Success                   { get; init; }
    public string? Error                     { get; init; }   // null = PASS
    public string? Method                    { get; init; }   // "reflection" | "regenerate" | null
    public bool   TriggeredRefreshSelfState { get; init; }
}

/// <summary>
/// v0.7.7 — Hybrid (reflection + regenerate fallback) + Aggressive sanitize pipeline.
///
/// 8 step:
///   1. (caller) ItemEditField.TryParse — range/parse 검증
///   2. SaveDataSanitizer (NaN/Infinity → fallback)
///   3. Reflection setter (dot path)
///   4. Read-back 검증 (silent fail detect)
///   5. (TODO Layer 2 patch — IL2CPP only) Regenerate fallback (Clone + setter + list swap)
///   6. ItemData.CountValueAndWeight() — game-self method (IL2CPP runtime only)
///   7. equipped 검사 → HeroData.RefreshSelfState() (IL2CPP runtime only)
///   8. (caller) UI refresh
///
/// POCO mock 환경에서는 step 3·4 만 검증 가능 — step 5/6/7 은 인게임 smoke.
/// </summary>
public static class ItemEditApplier
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static ItemEditResult Apply(object item, ItemEditField field, object value, object? player)
    {
        if (item == null) return new() { Success = false, Error = "item is null" };
        if (field == null) return new() { Success = false, Error = "field is null" };

        // 2. Sanitize (NaN/Infinity/range clamp)
        object sanitized = Sanitize(value, field);

        // 3 + 4. Reflection setter + read-back
        if (TryReflectionSetter(item, field.Path, sanitized, out string error))
        {
            // 6. CountValueAndWeight (IL2CPP runtime only — POCO mock 미존재 시 skip)
            TryInvokeCountValueAndWeight(item);

            // 7. RefreshSelfState if equipped
            bool refreshed = false;
            if (player != null && IsEquipped(item))
            {
                refreshed = TryInvokeRefreshSelfState(player);
            }

            return new()
            {
                Success = true,
                Method = "reflection",
                TriggeredRefreshSelfState = refreshed,
            };
        }

        // 5. Regenerate fallback (TODO — Task 0 spike 결과 B/C 시 활성)
        // POCO mock 환경에서 Clone 미구현 → reflection 결과 그대로 return
        return new() { Success = false, Error = error };
    }

    /// <summary>
    /// dot-path resolve + reflection setter + read-back 검증.
    /// 마지막 segment 가 leaf (실제 set 대상). 그 앞 segment 들은 sub-data wrapper navigation.
    /// IL2CPP wrapper 의 silent no-op (HeroData v0.2 strip 교훈) 검출 위해 read-back 비교.
    /// </summary>
    internal static bool TryReflectionSetter(object item, string path, object value, out string error)
    {
        error = "";
        if (string.IsNullOrEmpty(path)) { error = "path 빈 문자열"; return false; }

        var segments = path.Split('.');
        object cursor = item;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var sub = ReadFieldOrProperty(cursor, segments[i]);
            if (sub == null)
            {
                error = $"{segments[i]} is null (또는 미존재)";
                return false;
            }
            cursor = sub;
        }

        var leafName = segments[segments.Length - 1];
        var t = cursor.GetType();

        // 1) Field 우선
        var f = t.GetField(leafName, F);
        if (f != null)
        {
            object? before = null;
            try { before = f.GetValue(cursor); } catch { }
            try { f.SetValue(cursor, value); }
            catch (Exception ex) { error = $"setter threw: {ex.GetType().Name}: {ex.Message}"; return false; }

            object? after = null;
            try { after = f.GetValue(cursor); } catch { }
            if (!Equals(after, value))
            {
                error = $"silent fail: {leafName} {before} → {after} (target={value})";
                return false;
            }
            return true;
        }

        // 2) Property fallback
        var p = t.GetProperty(leafName, F);
        if (p != null && p.CanWrite)
        {
            try { p.SetValue(cursor, value); }
            catch (Exception ex) { error = $"property setter threw: {ex.GetType().Name}: {ex.Message}"; return false; }
            object? after = null;
            try { after = p.GetValue(cursor); } catch { }
            if (!Equals(after, value))
            {
                error = $"silent fail (property): {leafName} → {after} (target={value})";
                return false;
            }
            return true;
        }

        error = $"{leafName} field/property 미발견";
        return false;
    }

    /// <summary>
    /// SaveDataSanitizer pattern — NaN/Infinity 보정 + range clamp.
    /// LongYinCheat dump §9 의 maxCap fallback 패턴 차용.
    /// </summary>
    internal static object Sanitize(object value, ItemEditField field)
    {
        if (value is float f)
        {
            if (float.IsNaN(f)) return field.Min >= 0 ? 0f : field.Min;
            if (float.IsPositiveInfinity(f)) return field.Max;
            if (float.IsNegativeInfinity(f)) return field.Min;
            if (f < field.Min) return field.Min;
            if (f > field.Max) return field.Max;
            return f;
        }
        if (value is int i)
        {
            if (i < field.Min) return (int)field.Min;
            if (i > field.Max) return (int)field.Max;
            return i;
        }
        if (value is bool) return value;
        return value;
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        return null;
    }

    /// <summary>equipmentData.equiped == true OR horseData.equiped == true.</summary>
    internal static bool IsEquipped(object item)
    {
        var ed = ReadFieldOrProperty(item, "equipmentData");
        if (ed != null && ReadFieldOrProperty(ed, "equiped") is bool eb && eb) return true;
        var hd = ReadFieldOrProperty(item, "horseData");
        if (hd != null && ReadFieldOrProperty(hd, "equiped") is bool hb && hb) return true;
        return false;
    }

    /// <summary>game-self method 호출. POCO mock 시 method 미존재 → skip.</summary>
    internal static void TryInvokeCountValueAndWeight(object item)
    {
        try
        {
            var m = item.GetType().GetMethod("CountValueAndWeight", F, null, Type.EmptyTypes, null);
            m?.Invoke(item, null);
        }
        catch (Exception ex)
        {
            Logger.Warn($"CountValueAndWeight invoke threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>v0.7.7 spike 결과 — RefreshSelfState 부재. 진짜 method = RefreshMaxAttriAndSkill().</summary>
    internal static bool TryInvokeRefreshSelfState(object player)
    {
        try
        {
            var m = player.GetType().GetMethod("RefreshMaxAttriAndSkill", F, null, Type.EmptyTypes, null);
            if (m != null) { m.Invoke(player, null); return true; }
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warn($"RefreshMaxAttriAndSkill invoke threw: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>v0.7.7 stat editor 후처리 — CountValueAndWeight + RefreshMaxAttriAndSkill (equipped).</summary>
    public static bool PostMutationRefresh(object item, object? player)
    {
        TryInvokeCountValueAndWeight(item);
        if (player != null && IsEquipped(item))
            return TryInvokeRefreshSelfState(player);
        return false;
    }
}
