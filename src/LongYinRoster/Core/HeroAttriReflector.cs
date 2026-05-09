using System;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 2 — `HeroData.baseAttri[i]` / `maxAttri[i]` / `baseFightSkill[i]` /
/// `maxFightSkill[i]` / `baseLivingSkill[i]` / `maxLivingSkill[i]` reflection read.
///
/// cheat CharacterFeature.cs:1341-1383 (`ChangeAttri` / `ChangeFightSkill` / `ChangeLivingSkill`)
/// 의 read path 와 동일 — `hero.{base|max}Xxx` field/property → indexed list.
///
/// IL2CPP `Il2CppSystem.Collections.Generic.List&lt;float&gt;` 는 .NET IEnumerable 미구현 →
/// `Count` property + `get_Item(int)` indexer 사용.
///
/// heroBuff 는 별도 — `HeroSpeAddDataReflector` 의 idx lookup 으로 fallback. axis idx 와
/// SpeAddType idx 매칭 미확인 (Risk §7.6) — heroBuff 표시 못 하면 0 반환.
/// </summary>
public static class HeroAttriReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static int GetCount(object hero, AttriAxis axis)
    {
        if (hero == null) return 0;
        try
        {
            var list = ReadFieldOrProperty(hero, BaseFieldName(axis));
            if (list == null) return 0;
            var countProp = list.GetType().GetProperty("Count", F);
            if (countProp == null) return 0;
            return Convert.ToInt32(countProp.GetValue(list));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroAttriReflector", $"GetCount({axis}): {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    public static (float Base, float Max) GetEntry(object hero, AttriAxis axis, int idx)
    {
        if (hero == null) return (0f, 0f);
        try
        {
            var baseList = ReadFieldOrProperty(hero, BaseFieldName(axis));
            var maxList  = ReadFieldOrProperty(hero, MaxFieldName(axis));
            float b = ReadIndexedFloat(baseList, idx);
            float m = ReadIndexedFloat(maxList, idx);
            return (b, m);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroAttriReflector", $"GetEntry({axis},{idx}): {ex.GetType().Name}: {ex.Message}");
            return (0f, 0f);
        }
    }

    /// <summary>heroBuff[axis idx] 시도 — HeroSpeAddData 가 axis idx 와 매칭 안 되면 0.</summary>
    public static float GetBuff(object hero, AttriAxis axis, int idx)
    {
        if (hero == null) return 0f;
        try
        {
            var heroBuff = ReadFieldOrProperty(hero, "heroBuff");
            if (heroBuff == null) return 0f;
            var getM = heroBuff.GetType().GetMethod("Get", F, null, new[] { typeof(int) }, null);
            if (getM == null) return 0f;
            int speIdx = AxisToSpeAddIdx(axis, idx);
            if (speIdx < 0) return 0f;
            var v = getM.Invoke(heroBuff, new object[] { speIdx });
            return v is float f ? f : 0f;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroAttriReflector", $"GetBuff({axis},{idx}): {ex.GetType().Name}: {ex.Message}");
            return 0f;
        }
    }

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

    /// <summary>
    /// axis idx → HeroSpeAddData type idx 변환. cheat CharacterFeature.cs SpeAddTypeNames
    /// 매핑 미확인 → 현재 axis idx 그대로 반환 (속성 0~5 = SpeAddType 0~5 추정). spike 검증 필요.
    /// 매칭 안 되면 GetBuff 가 비매칭 type 의 buff 반환 → 사용자에게 노이즈. spike 결과 따라
    /// switch 확장 또는 -1 반환 (buff 표시 안 함).
    /// </summary>
    private static int AxisToSpeAddIdx(AttriAxis axis, int idx)
    {
        // 추정 매핑 — 인게임 spike 후 정확화 (Spec §6 Spike #5).
        return axis switch
        {
            AttriAxis.Attri => idx,                  // 추정 — spike 결과로 정확화
            AttriAxis.FightSkill => -1,              // 미확정 → buff 0 표시
            AttriAxis.LivingSkill => -1,             // 미확정 → buff 0 표시
            _ => -1,
        };
    }

    private static float ReadIndexedFloat(object? list, int idx)
    {
        if (list == null) return 0f;
        var t = list.GetType();
        var countProp = t.GetProperty("Count", F);
        if (countProp == null) return 0f;
        int n = Convert.ToInt32(countProp.GetValue(list));
        if (idx < 0 || idx >= n) return 0f;
        var indexer = t.GetProperty("Item", F);
        if (indexer != null)
        {
            var v = indexer.GetValue(list, new object[] { idx });
            return v is float f ? f : Convert.ToSingle(v);
        }
        var getItem = t.GetMethod("get_Item", F, null, new[] { typeof(int) }, null);
        if (getItem != null)
        {
            var v = getItem.Invoke(list, new object[] { idx });
            return v is float f ? f : Convert.ToSingle(v);
        }
        return 0f;
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
}
