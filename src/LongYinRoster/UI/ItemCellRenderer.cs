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
/// 본 class 는 IMGUI Draw + helper (BadgeText/EquippedMarker) + 색상 (GradeColor/QualityColor)
/// 통합. IMGUI 호출 strip-safe 패턴은 Draw 메서드의 doc 참고.
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

    /// <summary>
    /// 24×24 (또는 size 지정) placeholder cell.
    /// - 배경: GradeColor (alpha 0.6 — row 텍스트 색상보다 약화)
    /// - 중앙: CategoryGlyph 한자 1자
    /// - 우상단 8×8: QualityColor 마름모 (QualityOrder ≥ 0 일 때만)
    /// - 우하단: 강화 +N (EnhanceLv > 0 일 때만)
    /// - 좌하단: 착 (Equipped 일 때만)
    ///
    /// strip-safe (v0.7.3 smoke 2회 회귀 후 재 fallback):
    /// 1차 strip 발견: `GUILayout.Box(string, options)` + `GUI.Box(Rect, string)`
    /// 2차 strip 발견: `GUILayoutUtility.GetLastRect()` (v0.7.2 어디에서도 미사용 — plan 추정 오류)
    /// 본 fallback: GetLastRect 회피 → `GUILayoutUtility.GetRect(width, height)` 단일 호출 시도
    /// (GetLastRect 와 다른 method, 같이 strip 되었을 가능성 있으나 1회 시도 가치).
    /// 작동하면 spec §4.5 의도대로 cell 표시. 또 strip 시 spec §6 의 마지막 fallback 검토.
    ///
    /// 사용 검증된 IMGUI: GUI.DrawTexture (DialogStyle.FillBackground), GUI.Label(Rect),
    /// GUILayout.Label(string, params) (SlotDetailPanel:152), GUI.color (everywhere).
    /// </summary>
    public static void Draw(ContainerPanel.ItemRow r, int size = 24)
    {
        var prevColor = GUI.color;

        // 1. layout 자리 + rect 동시 — GetRect 1-call (GetLastRect 회피)
        var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));

        // 2. 배경 사각형 — DialogStyle.FillBackground 와 동일 패턴 (검증됨)
        GUI.color = GradeBackground(r.GradeOrder);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        // 3. 중앙 카테고리 한글 라벨 (v0.7.5.2 — 한자 1자 → 장비/단약/음식/비급/보물/재료/말)
        // GUIStyle 미사용 (test stub 호환) — label rect 를 cell 가운데 narrow 영역에 잡아 centering 효과.
        // 라벨 영역 — 양쪽 8px padding, full height (한글 글자 하단 잘림 방지). left-align default.
        GUI.Label(new Rect(rect.xMin + 8, rect.yMin, rect.width - 16, rect.height),
            CategoryGlyph.For(r.Type, r.SubType));

        // 4. 우상단 품질 마름모 (8×8 colored block, alpha 1.0)
        if (r.QualityOrder >= 0)
        {
            GUI.color = QualityColor(r.QualityOrder);
            GUI.DrawTexture(new Rect(rect.xMax - 9, rect.yMin + 1, 8, 8), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }
        // v0.7.5.2 — 강화 +N / 착 마커는 row text 에 이미 표시되어 cell 에서는 제거
        // (BadgeText / EquippedMarker helper 자체는 유지 — BuildLabel 에서 호출).
    }

    /// <summary>
    /// v0.7.4 D-1 — 인자 rect 에 cell overlay.
    /// 호출자가 이미 rect 영역을 layout 으로 잡아둔 경우 사용 (예: ContainerPanel
    /// DrawItemList 의 invisible Button 이 자리 잡고 GetLastRect 로 rect 받은 후 overlay).
    /// `Draw(r, size)` 와 동일 기능 — layout 자리 잡기 단계만 생략.
    /// </summary>
    public static void DrawAtRect(ContainerPanel.ItemRow r, Rect rect)
    {
        var prevColor = GUI.color;

        // 배경 — GradeColor (alpha 0.6)
        GUI.color = GradeBackground(r.GradeOrder);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        // 중앙 카테고리 한글 라벨 (v0.7.5.2 — narrow rect 가운데 정렬 효과)
        // 라벨 영역 — 양쪽 8px padding, full height (한글 글자 하단 잘림 방지). left-align default.
        GUI.Label(new Rect(rect.xMin + 8, rect.yMin, rect.width - 16, rect.height),
            CategoryGlyph.For(r.Type, r.SubType));

        // 우상단 품질 마름모
        if (r.QualityOrder >= 0)
        {
            GUI.color = QualityColor(r.QualityOrder);
            GUI.DrawTexture(new Rect(rect.xMax - 9, rect.yMin + 1, 8, 8), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }
        // v0.7.5.2 — 강화 +N / 착 마커는 row text 에 이미 표시되어 cell 에서는 제거
    }

    private static Color GradeBackground(int grade)
    {
        var c = GradeColor(grade);
        c.a = 0.6f;
        return c;
    }

}
