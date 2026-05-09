using System;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 2 — `HeroData.ChangeAttri/FightSkill/LivingSkill(int idx, float delta, bool, bool)`
/// game-self method 호출 wrapper.
///
/// cheat CharacterFeature.cs:1341-1383 의 100% mirror:
/// <code>
///   if (hero.maxXxx[idx] &lt; value) hero.maxXxx[idx] = value;
///   hero.ChangeXxx(idx, value - hero.baseXxx[idx], false, false);
///   // fallback (game-self method 없을 때): hero.baseXxx[idx] = value;
/// </code>
///
/// Clamp [0, 999999] (cheat AddTalent 정렬). Sanitize (RefreshMaxAttriAndSkill) 는
/// AttriTabPanel 의 [저장] 클릭 시 1회 — 본 Editor 는 호출 안 함.
/// </summary>
public static class CharacterAttriEditor
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const float MIN = 0f;
    private const float MAX = 999999f;

    /// <summary>base 값 변경 — value 가 max 초과 시 max 도 같이 bump (cheat 패턴).</summary>
    public static bool Change(object hero, AttriAxis axis, int idx, float value)
    {
        if (hero == null) return false;
        value = Clamp(value);
        try
        {
            // 1. max bump if needed
            float curMax = HeroAttriReflector.GetEntry(hero, axis, idx).Max;
            if (curMax < value)
            {
                if (!ChangeMax(hero, axis, idx, value)) return false;
            }

            // 2. game-self method 우선
            float curBase = HeroAttriReflector.GetEntry(hero, axis, idx).Base;
            float delta = value - curBase;
            if (Math.Abs(delta) < 0.001f) return true;  // no-op

            string methodName = ChangeMethodName(axis);
            var m = hero.GetType().GetMethod(methodName, F, null,
                new[] { typeof(int), typeof(float), typeof(bool), typeof(bool) }, null);
            if (m != null)
            {
                m.Invoke(hero, new object[] { idx, delta, false, false });
                return true;
            }

            // 3. fallback — baseXxx[idx] 직접 set
            return SetIndexed(hero, BaseFieldName(axis), idx, value);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("CharacterAttriEditor", $"Change({axis},{idx},{value}): {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>max 값 변경 — base 는 건드리지 않음.</summary>
    public static bool ChangeMax(object hero, AttriAxis axis, int idx, float value)
    {
        if (hero == null) return false;
        value = Clamp(value);
        return SetIndexed(hero, MaxFieldName(axis), idx, value);
    }

    /// <summary>TextField 입력 parse — 비숫자 / 빈 문자열 → false.</summary>
    public static bool TryParseInput(string input, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!float.TryParse(input, out var v)) return false;
        value = Clamp(v);
        return true;
    }

    private static float Clamp(float v) => Math.Max(MIN, Math.Min(MAX, v));

    private static string BaseFieldName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "baseAttri",
        AttriAxis.FightSkill => "baseFightSkill",
        AttriAxis.LivingSkill => "baseLivingSkill",
        _ => "",
    };

    private static string MaxFieldName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "maxAttri",
        AttriAxis.FightSkill => "maxFightSkill",
        AttriAxis.LivingSkill => "maxLivingSkill",
        _ => "",
    };

    private static string ChangeMethodName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "ChangeAttri",
        AttriAxis.FightSkill => "ChangeFightSkill",
        AttriAxis.LivingSkill => "ChangeLivingSkill",
        _ => "",
    };

    private static bool SetIndexed(object hero, string fieldName, int idx, float value)
    {
        try
        {
            var t = hero.GetType();
            var p = t.GetProperty(fieldName, F);
            object? list = p?.GetValue(hero);
            if (list == null)
            {
                var f = t.GetField(fieldName, F);
                list = f?.GetValue(hero);
            }
            if (list == null) return false;
            var setItem = list.GetType().GetMethod("set_Item", F, null,
                new[] { typeof(int), typeof(float) }, null);
            if (setItem != null)
            {
                setItem.Invoke(list, new object[] { idx, value });
                return true;
            }
            // Some IL2CPP wrappers expose Item indexer property only.
            var indexer = list.GetType().GetProperty("Item", F);
            if (indexer != null && indexer.CanWrite)
            {
                indexer.SetValue(list, value, new object[] { idx });
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("CharacterAttriEditor", $"SetIndexed({fieldName},{idx}): {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
