using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 — ItemGeneratorPanel 계산기. 고정: 탭 셀 폭(카테고리 55 / 2차 45), 검색 라벨 40, 등급·품질 버튼 48,
/// 검색 행·페이저 행(24), 등급·품질·수량 줄(28). 세로 확장: 결과 리스트 → PageSize = 높이에서 계산(최소 3).
/// 탭은 폭에 맞춰 줄바꿈(Wrap) — 줄 수가 늘면 리스트가 그만큼 줄어든다.
/// 암묵 여백은 PanelWindowLogic.ContentRect 가 뺀다(D1) — 여기서 다시 빼지 않는다.
/// </summary>
public readonly record struct ItemGenLayout(
    int CategoryPerRow, int CategoryRows, int SecondaryPerRow, int SecondaryRows,
    float SearchFieldW, float ListH, int PageSize, float RowButtonW)
{
    public const int   CategoryCount  = 7;    // ItemGenCategory: 장비/단약/음식/비급/보물/재료/말
    public const int   SecondaryCount = 7;    // 전체 + 무기/갑옷/투구/신발/장신구/마구
    public const float CategoryCellW  = 55f;
    public const float SecondaryCellW = 45f;
    public const float SearchLabelW   = 40f;
    public const float GradeCellW     = 48f;
    public const float ScrollbarSlack = DialogStyle.ScrollbarW;
    public const int   MinListRows    = 3;

    // 검색 행 + 페이저 행 + 등급/품질/수량 3줄, 각 줄 뒤 Gap
    private static float FixedRowsH =>
        2f * DialogStyle.RowHeight + 3f * DialogStyle.ButtonRowHeight + 5f * DialogStyle.Gap;

    public static PanelBounds MinSize => new(
        MinW: Math.Max(6f * GradeCellW + SearchLabelW + 7f * DialogStyle.Gap,                       // 등급/품질 줄 (356)
                       CategoryCount * CategoryCellW + (CategoryCount - 1) * DialogStyle.Gap)       // 카테고리 탭 한 줄 (409) — 최소 폭에서 탭이 접히지 않게
              + 2f * DialogStyle.Padding,
        MinH: DialogStyle.ChromeH
              + 2f * (DialogStyle.ButtonRowHeight + DialogStyle.Gap)                            // 탭 2줄(한 줄씩)
              + FixedRowsH
              + MinListRows * DialogStyle.RowHeight + (MinListRows - 1) * DialogStyle.Gap);

    public static ItemGenLayout Compute(Rect content, bool hasSecondary)
    {
        int catPerRow = LayoutMath.Wrap(content.width, CategoryCellW, DialogStyle.Gap);
        int catRows   = (CategoryCount + catPerRow - 1) / catPerRow;
        int secPerRow = LayoutMath.Wrap(content.width, SecondaryCellW, DialogStyle.Gap);
        int secRows   = hasSecondary ? (SecondaryCount + secPerRow - 1) / secPerRow : 0;

        float tabsH  = (catRows + secRows) * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);
        float listH  = Math.Max(MinListRows * DialogStyle.RowHeight, content.height - tabsH - FixedRowsH);
        int pageSize = LayoutMath.RowsThatFit(listH, DialogStyle.RowHeight, DialogStyle.Gap, MinListRows);

        float searchW = Math.Max(60f, content.width - SearchLabelW - DialogStyle.Gap);
        float rowW    = Math.Max(60f, content.width - ScrollbarSlack);
        return new ItemGenLayout(catPerRow, catRows, secPerRow, secRows, searchW, listH, pageSize, rowW);
    }
}
