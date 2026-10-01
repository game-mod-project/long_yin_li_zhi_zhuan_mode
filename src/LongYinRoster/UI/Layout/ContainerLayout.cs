using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>ContainerPanel 계산기 입력 상태. ExtraRightRows = 우측 열의 가변 행(신규/이름변경 입력 행 + 열린 드롭다운 항목 수).</summary>
public readonly record struct ContainerLayoutState(bool HasSecondaryTabs, bool InvCollapsed, bool StoCollapsed, int SplitPreset, int ExtraRightRows);

/// <summary>
/// v0.8.0 S3 — ContainerPanel 계산기. 고정: 카테고리 탭(8×70)·2차 탭(50)·툴바 2행·섹션 헤더/일괄선택/이동복사 행(28)·프리셋 행·우측 선택/버튼 행.
/// 가로: 좌/우 열 = SplitWidth(1:1, 최소 365 씩), 검색창 = 잔여. 세로: 인벤/창고 = SplitHeight(접힘·프리셋), 컨테이너 리스트 = 우측 잔여. 리스트 최소 3행.
/// content.height 는 PanelWindowLogic.ContentRect 가 ImguiSlack 을 이미 뺀 값(D1) — 여기서 다시 빼지 않는다.
/// </summary>
public readonly record struct ContainerLayout(
    float LeftW, float RightW, float SearchFieldW,
    float InvListH, float StoListH, float ContainerListH)
{
    public const int   CategoryTabCount = 8;     // 전체/장비/단약/음식/비급/보물/재료/말
    public const float CategoryTabW     = 70f;
    public const float SecondaryTabW    = 50f;
    // 우측 선택 행: [이름 ▼ 150][신규 45][이름변경 60][복사 45][삭제 45] + 컨트롤 margin 5×Gap = 365 — 두 열의 최소 폭
    public const float RightSelectRowW  = 150f + 45f + 60f + 45f + 45f + 5f * DialogStyle.Gap;
    public const float MinColW          = RightSelectRowW;
    // 툴바 1행의 고정부: Space 4 + 정렬 버튼 70+60+60+60 + Space 4 + 방향 32 + margin 7×Gap = 318 → 검색창 = 폭 − 318 (최소 120)
    public const float ToolbarFixedW    = 4f + 70f + 60f + 60f + 60f + 4f + 32f + 7f * DialogStyle.Gap;
    public const float MinSearchFieldW  = 120f;
    public const int   ToolbarRows      = 2;     // [검색/정렬] + [상세/착용중 제외/Undo/카운터]
    public const int   MinListRows      = 3;

    public static float MinListH                 => MinListRows * DialogStyle.RowHeight + (MinListRows - 1) * DialogStyle.Gap;   // 80
    public static float SectionExpandedOverhead  => 3f * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);   // 헤더 + 일괄선택 + 이동/복사 = 96
    public static float SectionCollapsedOverhead => DialogStyle.ButtonRowHeight + DialogStyle.Gap;          // 헤더만 = 32
    public static float PresetRowH               => DialogStyle.ButtonRowHeight + DialogStyle.Gap;          // 32
    public static float ToolbarH                 => ToolbarRows * (DialogStyle.RowHeight + DialogStyle.Gap); // 56
    // 우측 고정: 선택 행 32 + 라벨 28 + 이동/복사 2행 + 삭제 행 = 156
    public static float RightFixedH              => (DialogStyle.ButtonRowHeight + DialogStyle.Gap) + (DialogStyle.RowHeight + DialogStyle.Gap) + 3f * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);

    public static float TabsH(bool hasSecondaryTabs) => (hasSecondaryTabs ? 2f : 1f) * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);

    public static PanelBounds MinSize => new(
        MinW: Math.Max(CategoryTabCount * CategoryTabW + CategoryTabCount * DialogStyle.Gap,   // 탭 한 줄 592
                       2f * MinColW + DialogStyle.Gap)                                          // 두 열 734
              + 2f * DialogStyle.Padding,                                                        // 758
        MinH: DialogStyle.ChromeH + TabsH(true) + ToolbarH + PresetRowH
              + 2f * SectionExpandedOverhead + 2f * MinListH);                                   // 588

    public static ContainerLayout Compute(Rect content, ContainerLayoutState s)
    {
        float colsH = Math.Max(0f, content.height - TabsH(s.HasSecondaryTabs) - ToolbarH);

        var cols = LayoutMath.SplitWidth(content.width, new[] { 1f, 1f }, DialogStyle.Gap, new[] { MinColW, MinColW });

        // 좌측: 프리셋 행을 뺀 나머지를 인벤/창고에 분배. 접힌 섹션은 헤더만 차지하고 0.
        float leftAvail = colsH - PresetRowH;
        float invOver = s.InvCollapsed ? SectionCollapsedOverhead : SectionExpandedOverhead;
        float stoOver = s.StoCollapsed ? SectionCollapsedOverhead : SectionExpandedOverhead;
        float[] weights;
        if (s.InvCollapsed || s.StoCollapsed) weights = new[] { 1f, 1f };            // 펼친 쪽만 가중치가 살아남는다
        else weights = s.SplitPreset switch
        {
            1 => new[] { 7f, 3f },
            2 => new[] { 3f, 7f },
            3 => new[] { 1f, 0f },   // 확장:최소 — 창고는 아래서 3행 고정
            _ => new[] { 1f, 1f },
        };
        bool stoFixedMin = s.SplitPreset == 3 && !s.InvCollapsed && !s.StoCollapsed;
        if (stoFixedMin) stoOver += MinListH;
        var split = LayoutMath.SplitHeight(leftAvail, new[] { invOver, stoOver }, weights,
                                           new[] { s.InvCollapsed, s.StoCollapsed }, 0f);
        float invListH = s.InvCollapsed ? 0f : Math.Max(MinListH, split[0]);
        float stoListH = s.StoCollapsed ? 0f : Math.Max(MinListH, split[1] + (stoFixedMin ? MinListH : 0f));

        // 우측: 선택 행 + 가변 행(신규/이름변경/드롭다운) + 라벨 + 버튼 3행을 빼고 전부 컨테이너 리스트
        float rightFixed = RightFixedH + s.ExtraRightRows * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);
        float conListH   = Math.Max(MinListH, colsH - rightFixed);

        float searchW = Math.Max(MinSearchFieldW, content.width - ToolbarFixedW);
        return new ContainerLayout(cols[0], cols[1], searchW, invListH, stoListH, conListH);
    }
}
