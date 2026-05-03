using LongYinRoster.Containers;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.3 D-2 — strip-safe 24×24 IMGUI placeholder cell + 색상 6단계 단일 source.
///
/// 색상 단일 source (Grade/Quality 6단계 hex) 가 본 class 에 통합. v0.7.2 의
/// `ContainerPanel.GradeColor` (private static) 는 본 release 에서 제거되고 row
/// 텍스트 색상도 `ItemCellRenderer.GradeColor` 호출로 대체.
///
/// IMGUI Draw method 는 Task 3 에서 추가 (본 step 은 helper 만).
/// </summary>
public static class ItemCellRenderer
{
    /// <summary>
    /// v0.7.2 색상과 동일 hex (회색→녹→하늘→보라→오렌지→빨강). 모든 ContainerPanel
    /// row 텍스트와 ItemCellRenderer cell 배경의 단일 source.
    /// </summary>
    public static Color GradeColor(int grade) => grade switch
    {
        0 => new Color(0.61f, 0.64f, 0.69f),    // 회색  #9CA3AF (열악/잔품 baseline)
        1 => new Color(0.13f, 0.77f, 0.37f),    // 녹   #22C55E
        2 => new Color(0.22f, 0.74f, 0.97f),    // 하늘 #38BDF8
        3 => new Color(0.66f, 0.33f, 0.97f),    // 보라 #A855F7
        4 => new Color(0.98f, 0.45f, 0.09f),    // 오렌지 #F97316
        5 => new Color(0.94f, 0.27f, 0.27f),    // 빨강 #EF4444
        _ => Color.white,
    };

    /// <summary>
    /// 품질 6단계 hex — Grade 와 같은 팔레트 (게임 내 색상 매핑이 grade/quality 동일).
    /// 마름모 (cell 우상단 8×8) 색상에 사용.
    /// </summary>
    public static Color QualityColor(int quality) => quality switch
    {
        0 => new Color(0.61f, 0.64f, 0.69f),
        1 => new Color(0.13f, 0.77f, 0.37f),
        2 => new Color(0.22f, 0.74f, 0.97f),
        3 => new Color(0.66f, 0.33f, 0.97f),
        4 => new Color(0.98f, 0.45f, 0.09f),
        5 => new Color(0.94f, 0.27f, 0.27f),
        _ => Color.white,
    };

    /// <summary>강화 lv > 0 일 때 "+N", 아니면 "". 단위 테스트 가능한 helper.</summary>
    public static string BadgeText(int enhanceLv) => enhanceLv > 0 ? $"+{enhanceLv}" : "";

    /// <summary>착용중일 때 "착", 아니면 "". 단위 테스트 가능한 helper.</summary>
    public static string EquippedMarker(bool equipped) => equipped ? "착" : "";
}
