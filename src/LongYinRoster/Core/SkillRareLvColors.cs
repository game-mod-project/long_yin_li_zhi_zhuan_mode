using UnityEngine;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 — 무공 등급 (rareLv 0~5) 색상 매핑.
/// 0 기초=회색 / 1 진급=녹색 / 2 상승=파란색 / 3 비전=보라색 / 4 정극=주황색 / 5 절세=붉은색
/// </summary>
public static class SkillRareLvColors
{
    public static readonly Color Gray   = new(0.7f, 0.7f, 0.7f, 1f);
    public static readonly Color Green  = new(0.4f, 1.0f, 0.4f, 1f);
    public static readonly Color Blue   = new(0.4f, 0.6f, 1.0f, 1f);
    public static readonly Color Purple = new(0.8f, 0.4f, 1.0f, 1f);
    public static readonly Color Orange = new(1.0f, 0.7f, 0.3f, 1f);
    public static readonly Color Red    = new(1.0f, 0.4f, 0.4f, 1f);
    public static readonly Color White  = Color.white;

    public static Color ForRareLv(int rareLv) => rareLv switch
    {
        0 => Gray,
        1 => Green,
        2 => Blue,
        3 => Purple,
        4 => Orange,
        5 => Red,
        _ => White,
    };

    /// <summary>skillID → 색상 (rareLv 기반).</summary>
    public static Color ForSkillID(int skillID)
    {
        return ForRareLv(SkillNameCache.GetRareLv(skillID));
    }
}
