using System;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 — Player editor 결과.
/// </summary>
public sealed class PlayerEditResult
{
    public bool   Success                   { get; init; }
    public string? Error                     { get; init; }
    public string? Method                    { get; init; }   // "game-self" | "reflection" | null
    public bool   TriggeredRefreshSelfState { get; init; }
}

/// <summary>
/// v0.7.8 Phase 1 — Player resource stat editor (hp/maxhp/power/maxpower/mana/maxmana/fame).
///
/// Hybrid pipeline (Q3=C):
///   1. delta = newValue - oldValue
///   2. game-self method 시도 — `ChangeHp(delta, false, false, false, false)` / `ChangeMaxHp(delta, false)` / `ChangeFame(delta, false)`
///   3. read-back 검증
///   4. silent fail → reflection setter (`set_<fieldName>`) fallback
///   5. RefreshMaxAttriAndSkill (v0.7.7 검증)
///
/// Quick actions:
///   - QuickFullHeal: hp = maxhp
///   - QuickRestoreEnergy: mana = maxmana + power = maxpower
///   - QuickCureInjuries: 부상 field = 0 (Task 0.1 spike 결과로 정확한 field name 결정)
/// </summary>
public static class PlayerEditApplier
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// fieldName 의 게임-self method 매핑.
    /// HeroData property 명: maxhp (lowercase) / maxMana / maxPower (camelCase) / fame.
    /// power/mana 는 game-self method 부재 → reflection setter 만 사용.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> GameSelfMethods = new()
    {
        { "hp",       "ChangeHp"      },
        { "maxhp",    "ChangeMaxHp"   },
        { "maxPower", "ChangeMaxPower"},
        { "maxMana",  "ChangeMaxMana" },
        { "fame",     "ChangeFame"    },
    };

    /// <summary>
    /// cheat StatEditor 검증 패턴 — max 변경 시 realMax* 도 동기화 (game internal clamp 회피).
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> RealMaxMirror = new()
    {
        { "maxhp",    "realMaxHp"    },
        { "maxMana",  "realMaxMana"  },
        { "maxPower", "realMaxPower" },
    };

    /// <summary>
    /// v0.7.8 — 현재값(hp/mana/power) 변경 시 해당 max 로 자동 clamp.
    /// 사용자가 max=5885 인 hp 에 99999 입력 → 5885 로 clamp.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, string> CurrentToMax = new()
    {
        { "hp",    "maxhp"    },
        { "mana",  "maxMana"  },
        { "power", "maxPower" },
    };

    /// <summary>Resource stat 편집. fieldName: hp/maxhp/power/maxPower/mana/maxMana/fame.</summary>
    public static PlayerEditResult ApplyResource(object player, string fieldName, float newValue)
    {
        if (player == null) return new() { Success = false, Error = "player is null" };
        if (string.IsNullOrEmpty(fieldName)) return new() { Success = false, Error = "fieldName is empty" };

        // 0. 현재값 (hp/mana/power) 은 해당 max 로 자동 clamp (사용자 피드백 v0.7.8)
        if (CurrentToMax.TryGetValue(fieldName, out var maxField))
        {
            float maxVal = ReadFloat(player, maxField);
            if (maxVal > 0 && newValue > maxVal) newValue = maxVal;
        }

        // 1. read oldValue
        float oldValue = ReadFloat(player, fieldName);
        float delta = newValue - oldValue;

        // 2. game-self method 시도 (delta 기반)
        string? method = null;
        if (GameSelfMethods.TryGetValue(fieldName, out var methodName))
        {
            if (TryGameSelfMethod(player, methodName, delta))
            {
                method = "game-self";
            }
        }

        // 3. read-back 검증 + 4. fallback reflection setter
        float current = ReadFloat(player, fieldName);
        if (Math.Abs(current - newValue) > 0.001f)
        {
            // game-self 실패 또는 method 없음 — reflection setter 시도
            if (TryReflectionSetter(player, fieldName, newValue))
            {
                method = "reflection";
            }
            else
            {
                return new() { Success = false, Error = $"setter 실패: {fieldName} = {newValue}, current = {current}", Method = method };
            }

            // re-verify
            current = ReadFloat(player, fieldName);
            if (Math.Abs(current - newValue) > 0.001f)
            {
                return new() { Success = false, Error = $"silent fail: {fieldName} {oldValue} → {current} (target={newValue})", Method = method };
            }
        }

        // 4.5 max 필드 변경 시 realMax* 동기화 (cheat StatEditor 검증 패턴)
        if (RealMaxMirror.TryGetValue(fieldName, out var realMaxName))
        {
            TryReflectionSetter(player, realMaxName, newValue);
        }

        // 5. RefreshMaxAttriAndSkill (v0.7.7 검증)
        bool refreshed = TryInvokeRefreshMaxAttriAndSkill(player);

        return new()
        {
            Success = true,
            Method = method ?? "noop",
            TriggeredRefreshSelfState = refreshed,
        };
    }

    /// <summary>v0.7.8 사용자 피드백 — 전체 회복 통합. hp + mana + power 모두 max 로 채움.</summary>
    public static bool QuickFullHeal(object player)
    {
        if (player == null) return false;
        float maxhp    = ReadFloat(player, "maxhp");
        float maxMana  = ReadFloat(player, "maxMana");
        float maxPower = ReadFloat(player, "maxPower");
        var r1 = ApplyResource(player, "hp",    maxhp);
        var r2 = ApplyResource(player, "mana",  maxMana);
        var r3 = ApplyResource(player, "power", maxPower);
        return r1.Success && r2.Success && r3.Success;
    }

    /// <summary>v0.7.8 — QuickRestoreEnergy 는 QuickFullHeal 에 통합 (사용자 피드백). 호환 유지용.</summary>
    [System.Obsolete("v0.7.8 — QuickFullHeal 에 통합. UI 에서 호출 안 함.")]
    public static bool QuickRestoreEnergy(object player) => QuickFullHeal(player);

    /// <summary>
    /// Quick: 부상 field = 0. Task 0.1 spike 결과로 정확한 field name 확인.
    /// 추정 field: externalInjury / internalInjury / poisonInjury / poisonNum.
    /// 미발견 field 는 try/catch 로 silent skip.
    /// </summary>
    public static bool QuickCureInjuries(object player)
    {
        if (player == null) return false;
        // 추정 field 들을 best-effort 로 0 set
        string[] candidates = { "externalInjury", "internalInjury", "poisonInjury", "poisonNum" };
        bool any = false;
        foreach (var name in candidates)
        {
            if (TryReflectionSetter(player, name, 0f) || TryReflectionSetter(player, name, 0))
                any = true;
        }
        if (any) TryInvokeRefreshMaxAttriAndSkill(player);
        return any;
    }

    // ───── helpers ─────

    /// <summary>game-self ChangeXxx(delta, ...) 호출. arg count 다양 — first arg = delta float, 나머지 = false bool.</summary>
    internal static bool TryGameSelfMethod(object player, string methodName, float delta)
    {
        try
        {
            var methods = player.GetType().GetMethods(F);
            foreach (var m in methods)
            {
                if (m.Name != methodName) continue;
                var ps = m.GetParameters();
                if (ps.Length == 0) continue;
                if (ps[0].ParameterType != typeof(float) && ps[0].ParameterType != typeof(System.Single)) continue;

                // 인자 array 구성 — first = delta, 나머지 = default (bool=false, int=0)
                var args = new object[ps.Length];
                args[0] = delta;
                for (int i = 1; i < ps.Length; i++)
                {
                    var t = ps[i].ParameterType;
                    if (t == typeof(bool)) args[i] = false;
                    else if (t == typeof(int)) args[i] = 0;
                    else if (t == typeof(float)) args[i] = 0f;
                    else args[i] = null!;
                }
                m.Invoke(player, args);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("PlayerEditApplier", $"PlayerEditApplier.TryGameSelfMethod({methodName}, {delta}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>reflection field/property setter — float/int/bool 자동 변환.</summary>
    internal static bool TryReflectionSetter(object obj, string name, object value)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null && p.CanWrite)
            {
                object converted = Convert.ChangeType(value, p.PropertyType);
                p.SetValue(obj, converted);
                return true;
            }
            var f = t.GetField(name, F);
            if (f != null)
            {
                object converted = Convert.ChangeType(value, f.FieldType);
                f.SetValue(obj, converted);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("PlayerEditApplier", $"PlayerEditApplier.TryReflectionSetter({name}, {value}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    private static float ReadFloat(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return Convert.ToSingle(p.GetValue(obj));
            var f = t.GetField(name, F);
            if (f != null) return Convert.ToSingle(f.GetValue(obj));
        }
        catch { }
        return 0f;
    }

    private static bool TryInvokeRefreshMaxAttriAndSkill(object player)
    {
        try
        {
            var m = player.GetType().GetMethod("RefreshMaxAttriAndSkill", F, null, Type.EmptyTypes, null);
            if (m != null) { m.Invoke(player, null); return true; }
            return false;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("RefreshMaxAttriAndSkill", $"RefreshMaxAttriAndSkill invoke threw: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
