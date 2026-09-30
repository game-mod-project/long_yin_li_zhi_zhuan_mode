using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S3 — 컨테이너 패널 계산기. 좌/우 열 폭 SplitWidth, 인벤/창고 SplitHeight(접힘·프리셋), 컨테이너 리스트 = 우측 잔여.</summary>
public class ContainerLayoutTests
{
    // 기본 창 800×760 → 내용 776×676 (ChromeH 84)
    private static readonly Rect Default = new(12, 40, 776, 676);

    private static ContainerLayoutState S(bool secondary = false, bool inv = false, bool sto = false, int preset = 0, int extra = 0)
        => new(HasSecondaryTabs: secondary, InvCollapsed: inv, StoCollapsed: sto, SplitPreset: preset, ExtraRightRows: extra);

    [Fact]
    public void Compute_Default_ColumnsShareWidthAfterMins()
    {
        // 776 - 2×365 - gap 4 = 42 → 21 씩 → 386 / 386
        var L = ContainerLayout.Compute(Default, S());
        (L.LeftW, L.RightW).ShouldBe((386f, 386f));
    }

    [Fact]
    public void Compute_Default_5050_SplitsLeftRemainingEvenly()
    {
        // 열 높이 676 - 탭 32 - 툴바 56 = 588; 프리셋 행 32 → 556; 펼침 오버헤드 96×2 → 364 → 182 / 182
        var L = ContainerLayout.Compute(Default, S());
        (L.InvListH, L.StoListH).ShouldBe((182f, 182f));
    }

    [Fact]
    public void Compute_Default_ContainerListTakesRightRemaining()
    {
        // 588 - 우측 고정(선택 행 32 + 라벨 28 + 버튼 3행 96 = 156) = 432
        var L = ContainerLayout.Compute(Default, S());
        L.ContainerListH.ShouldBe(432f);
    }

    [Fact]
    public void Compute_Preset7030_WeightsLeftSplit()
    {
        var L = ContainerLayout.Compute(Default, S(preset: 1));
        L.InvListH.ShouldBe(254.8f, 0.01);
        L.StoListH.ShouldBe(109.2f, 0.01);
    }

    [Fact]
    public void Compute_Preset3_StorageGetsExactlyMinRows_InventoryGetsRest()
    {
        // 확장:최소 — 창고 3행(80) 고정, 인벤 = 556 - 96 - (96 + 80) = 284
        var L = ContainerLayout.Compute(Default, S(preset: 3));
        (L.InvListH, L.StoListH).ShouldBe((284f, ContainerLayout.MinListH));
    }

    [Fact]
    public void Compute_InvCollapsed_StorageTakesAll()
    {
        // 접힌 인벤은 헤더(32)만 → 556 - 32 - 96 = 428
        var L = ContainerLayout.Compute(Default, S(inv: true));
        (L.InvListH, L.StoListH).ShouldBe((0f, 428f));
    }

    [Fact]
    public void Compute_StoCollapsed_InventoryTakesAll()
    {
        var L = ContainerLayout.Compute(Default, S(sto: true));
        (L.InvListH, L.StoListH).ShouldBe((428f, 0f));
    }

    [Fact]
    public void Compute_Preset3070_WeightsLeftSplit()
    {
        var L = ContainerLayout.Compute(Default, S(preset: 2));
        L.InvListH.ShouldBe(109.2f, 0.01);
        L.StoListH.ShouldBe(254.8f, 0.01);
    }

    [Fact]
    public void Compute_BothCollapsed_ZeroLists()
    {
        var L = ContainerLayout.Compute(Default, S(inv: true, sto: true));
        (L.InvListH, L.StoListH).ShouldBe((0f, 0f));
    }

    [Fact]
    public void Compute_SecondaryTabs_ShrinkEveryList()
    {
        // 2차 탭 한 줄(32) 만큼 세 리스트가 줄어든다: 좌 166/166, 우 400
        var L = ContainerLayout.Compute(Default, S(secondary: true));
        (L.InvListH, L.StoListH, L.ContainerListH).ShouldBe((166f, 166f, 400f));
    }

    [Fact]
    public void Compute_ExtraRightRows_ShrinkContainerList()
    {
        // 드롭다운 3항목 → 우측 리스트 432 - 3×32 = 336
        var L = ContainerLayout.Compute(Default, S(extra: 3));
        L.ContainerListH.ShouldBe(336f);
        // 항목이 아주 많아도 3행 밑으로는 안 내려간다
        ContainerLayout.Compute(Default, S(extra: 40)).ContainerListH.ShouldBe(ContainerLayout.MinListH);
    }

    [Fact]
    public void Compute_Narrow_ColumnsFloorAtMinColW()
    {
        var L = ContainerLayout.Compute(new Rect(12, 40, 600, 676), S());
        (L.LeftW, L.RightW).ShouldBe((ContainerLayout.MinColW, ContainerLayout.MinColW));
    }

    [Fact]
    public void Compute_Tiny_ListsFloorAtThreeRows()
    {
        var L = ContainerLayout.Compute(new Rect(12, 40, 300, 200), S());
        (L.InvListH, L.StoListH, L.ContainerListH).ShouldBe((ContainerLayout.MinListH, ContainerLayout.MinListH, ContainerLayout.MinListH));
        L.SearchFieldW.ShouldBe(ContainerLayout.MinSearchFieldW);
    }

    [Fact]
    public void Compute_SearchField_FollowsWidth()
    {
        ContainerLayout.Compute(Default, S()).SearchFieldW.ShouldBe(776f - ContainerLayout.ToolbarFixedW);   // 458
    }

    [Fact]
    public void Compute_Taller_MonotonicLists()
    {
        var a = ContainerLayout.Compute(Default, S());
        var b = ContainerLayout.Compute(new Rect(12, 40, 776, 900), S());
        b.InvListH.ShouldBeGreaterThan(a.InvListH);
        b.ContainerListH.ShouldBeGreaterThan(a.ContainerListH);
    }

    [Fact]
    public void MinSize_FitsFixedPartsPlusThreeRowsPerList()
    {
        var m = ContainerLayout.MinSize;
        m.MinW.ShouldBe(758f);   // max(탭 8×70 + 8×4 = 592, 두 열 2×365 + 4 = 734) + 24
        m.MinH.ShouldBe(588f);   // 84 + 탭 2줄 64 + 툴바 56 + 프리셋 32 + 펼침 오버헤드 192 + 리스트 2×80
        var L = ContainerLayout.Compute(new Rect(12, 40, m.MinW - 2 * DialogStyle.Padding, m.MinH - DialogStyle.ChromeH), S(secondary: true));
        (L.InvListH, L.StoListH).ShouldBe((ContainerLayout.MinListH, ContainerLayout.MinListH));   // 정확히 3행
        L.ContainerListH.ShouldBeGreaterThanOrEqualTo(ContainerLayout.MinListH);
        (L.LeftW, L.RightW).ShouldBe((ContainerLayout.MinColW, ContainerLayout.MinColW));
    }
}
