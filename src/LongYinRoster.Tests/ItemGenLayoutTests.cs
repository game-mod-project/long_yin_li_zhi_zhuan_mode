using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S2 — 아이템 생성 패널 계산기. 세로 확장 = 결과 리스트(페이지 크기 자동), 탭은 폭에 맞춰 줄바꿈.</summary>
public class ItemGenLayoutTests
{
    // 기본 창 620×560 → 내용 596×508
    private static readonly Rect Default = new(12, 40, 596, 508);

    [Fact]
    public void Compute_DefaultWindow_TabsFitOneRowEach()
    {
        var L = ItemGenLayout.Compute(Default, hasSecondary: true);
        L.CategoryPerRow.ShouldBeGreaterThanOrEqualTo(7);
        L.CategoryRows.ShouldBe(1);
        L.SecondaryRows.ShouldBe(1);
    }

    [Fact]
    public void Compute_DefaultWindow_PageSizeFromHeight()
    {
        // 탭 2줄(2×32) + 고정 행(검색 24 + 페이저 24 + 등급/품질/수량 3×28 + 5×4 = 152) + IMGUI 암묵 여백 32
        // → 리스트 508-64-152-32 = 260 → floor(264/28) = 9
        var L = ItemGenLayout.Compute(Default, hasSecondary: true);
        L.ListH.ShouldBe(508f - 64f - 152f - DialogStyle.ImguiSlack);
        L.PageSize.ShouldBe(9);
    }

    [Fact]
    public void Compute_NoSecondary_ListGetsTheRow()
    {
        var a = ItemGenLayout.Compute(Default, hasSecondary: true);
        var b = ItemGenLayout.Compute(Default, hasSecondary: false);
        b.SecondaryRows.ShouldBe(0);
        b.ListH.ShouldBe(a.ListH + DialogStyle.ButtonRowHeight + DialogStyle.Gap);
    }

    [Fact]
    public void Compute_NarrowContent_WrapsTabs()
    {
        var wide = ItemGenLayout.Compute(Default, hasSecondary: true);
        var L = ItemGenLayout.Compute(new Rect(12, 40, 300, 508), hasSecondary: true);
        L.CategoryPerRow.ShouldBe(5);    // floor(304/59)
        L.CategoryRows.ShouldBe(2);
        L.SecondaryPerRow.ShouldBe(6);   // floor(304/49)
        L.SecondaryRows.ShouldBe(2);
        L.PageSize.ShouldBeLessThan(wide.PageSize);   // 탭이 두 줄씩 → 리스트가 줄어든다
    }

    [Fact]
    public void Compute_TallerContent_MorePageRows()
    {
        var a = ItemGenLayout.Compute(Default, true);
        var b = ItemGenLayout.Compute(new Rect(12, 40, 596, 900), true);
        b.PageSize.ShouldBeGreaterThan(a.PageSize);
    }

    [Fact]
    public void Compute_TinyContent_ListFloorsAtThreeRows()
    {
        var L = ItemGenLayout.Compute(new Rect(12, 40, 200, 100), true);
        L.ListH.ShouldBe(3 * DialogStyle.RowHeight);
        L.PageSize.ShouldBe(3);
    }

    [Fact]
    public void Compute_WidthsFollowContent()
    {
        var L = ItemGenLayout.Compute(Default, true);
        L.SearchFieldW.ShouldBe(596f - ItemGenLayout.SearchLabelW - DialogStyle.Gap);
        L.RowButtonW.ShouldBe(596f - DialogStyle.ScrollbarW);
    }

    [Fact]
    public void MinSize_CoversWidestFixedRowAndThreeListRows()
    {
        var m = ItemGenLayout.MinSize;
        // 카테고리 탭 한 줄(7×55 + 6×4 = 409)이 등급 줄(356)보다 넓다 → 409 + 24 = 433
        m.MinW.ShouldBe(ItemGenLayout.CategoryCount * ItemGenLayout.CategoryCellW + (ItemGenLayout.CategoryCount - 1) * DialogStyle.Gap + 2 * DialogStyle.Padding);
        var L = ItemGenLayout.Compute(new Rect(12, 40, m.MinW - 2 * DialogStyle.Padding, m.MinH - DialogStyle.HeaderHeight - 2 * DialogStyle.Padding), true);
        L.CategoryRows.ShouldBe(1);   // 최소 폭에서도 탭은 한 줄
        L.PageSize.ShouldBe(3);
    }
}
