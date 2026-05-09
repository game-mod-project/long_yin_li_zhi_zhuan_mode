namespace LongYinRoster.Util;

/// <summary>v0.7.10 Phase 2 — 속성/무학/기예 axis enum.</summary>
public enum AttriAxis
{
    Attri,        // 속성 6 (baseAttri)
    FightSkill,   // 무학 9 (baseFightSkill)
    LivingSkill,  // 기예 9 (baseLivingSkill)
}

/// <summary>
/// v0.7.10 Phase 2 — 24 hardcoded 한글 라벨.
///
/// 게임 LTLocalization / HangulDict 가 라벨을 반환하지 못 하는 경우의 fallback.
/// PlayerEditorPanel 의 [속성] secondary tab 에서 row 라벨로 사용.
/// </summary>
public static class AttriLabels
{
    public static readonly string[] Attri = new[]
    {
        "근력", "민첩", "지력", "의지", "체질", "경맥",
    };

    public static readonly string[] FightSkill = new[]
    {
        "내공", "경공", "절기", "권장", "검법", "도법", "장병", "기문", "사술",
    };

    public static readonly string[] LivingSkill = new[]
    {
        "의술", "독술", "학식", "언변", "채벌", "목식", "단조", "제약", "요리",
    };

    public static string For(AttriAxis axis, int idx)
    {
        var arr = axis switch
        {
            AttriAxis.Attri => Attri,
            AttriAxis.FightSkill => FightSkill,
            AttriAxis.LivingSkill => LivingSkill,
            _ => null,
        };
        if (arr == null) return $"[axis={axis}]";
        if (idx < 0 || idx >= arr.Length) return $"[idx={idx}]";
        return arr[idx];
    }

    public static int Count(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => Attri.Length,
        AttriAxis.FightSkill => FightSkill.Length,
        AttriAxis.LivingSkill => LivingSkill.Length,
        _ => 0,
    };
}
