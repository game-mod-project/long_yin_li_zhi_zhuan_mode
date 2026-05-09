using UnityEngine;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 — 천부 단계 점수별 색상 매핑.
/// ≤5 녹색 / ≤10 파란색 / ≤15 보라색 / ≤20 주황색 / >20 붉은색
/// </summary>
public static class TagPointColors
{
    public static readonly Color Green  = new(0.4f, 1.0f, 0.4f, 1f);
    public static readonly Color Blue   = new(0.4f, 0.6f, 1.0f, 1f);
    public static readonly Color Purple = new(0.8f, 0.4f, 1.0f, 1f);
    public static readonly Color Orange = new(1.0f, 0.7f, 0.3f, 1f);
    public static readonly Color Red    = new(1.0f, 0.4f, 0.4f, 1f);
    public static readonly Color White  = Color.white;

    public static Color ForValue(int value)
    {
        if (value <= 0)  return White;
        if (value <= 5)  return Green;
        if (value <= 10) return Blue;
        if (value <= 15) return Purple;
        if (value <= 20) return Orange;
        return Red;
    }

    /// <summary>tagID → 색상 (인게임 표시값 기반 — TagMeta.Value × 4 = 디스플레이 점수).</summary>
    public static Color ForTagID(int tagID)
    {
        var meta = HeroTagNameCache.GetMeta(tagID);
        // v0.7.10.1 — TagMeta.Value 는 raw (1/2/4/8). 인게임 디스플레이 = ×4 (4/8/16/32).
        // ForValue 의 threshold (5/10/15/20) 는 디스플레이 값 기준 → ×4 적용해서 호출.
        return meta != null ? ForValue(meta.Value * 4) : White;
    }
}
