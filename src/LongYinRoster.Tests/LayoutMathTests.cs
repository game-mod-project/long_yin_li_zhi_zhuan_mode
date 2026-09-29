using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — LayoutMath 순수 수식. UnityStubs 의 Rect 로 검증.</summary>
public class LayoutMathTests
{
    [Theory]
    [InlineData(100f, 24f, 0f, 4)]   // 4*24 = 96 ≤ 100
    [InlineData(100f, 24f, 4f, 3)]   // 3*24 + 2*4 = 80 ≤ 100 < 108
    [InlineData(48f,  24f, 0f, 2)]   // 정확히 두 행
    [InlineData(23f,  24f, 0f, 1)]   // 한 행도 안 들어가도 min 1
    [InlineData(0f,   24f, 0f, 1)]
    [InlineData(-50f, 24f, 0f, 1)]
    public void RowsThatFit_CountsRowsIncludingSpacing(float h, float row, float gap, int expected)
    {
        LayoutMath.RowsThatFit(h, row, gap).ShouldBe(expected);
    }

    [Fact]
    public void RowsThatFit_NeverBelowMin()
    {
        LayoutMath.RowsThatFit(10f, 24f, 0f, min: 3).ShouldBe(3);
    }

    [Fact]
    public void RowsThatFit_ZeroRowHeight_ReturnsMin()
    {
        LayoutMath.RowsThatFit(100f, 0f, 0f, min: 2).ShouldBe(2);
    }

    [Fact]
    public void SplitHeight_TwoExpanded_SharesRemainingByWeight()
    {
        // 컨테이너 인벤/창고: 총 640, 각 오버헤드 56, 가중치 1:1, gap 4 → 잔여 524 → 262/262
        var r = LayoutMath.SplitHeight(640f, new[] { 56f, 56f }, new[] { 1f, 1f }, new[] { false, false }, 4f);
        r.ShouldBe(new[] { 262f, 262f });
    }

    [Fact]
    public void SplitHeight_OneCollapsed_GivesRemainingToOther()
    {
        // 접힌 쪽은 헤더(28)만 차지, 잔여 640-28-56-4 = 552 전부 펼친 쪽
        var r = LayoutMath.SplitHeight(640f, new[] { 28f, 56f }, new[] { 1f, 1f }, new[] { true, false }, 4f);
        r.ShouldBe(new[] { 0f, 552f });
    }

    [Fact]
    public void SplitHeight_AllCollapsed_ReturnsZeros()
    {
        var r = LayoutMath.SplitHeight(640f, new[] { 28f, 28f }, new[] { 1f, 1f }, new[] { true, true }, 4f);
        r.ShouldBe(new[] { 0f, 0f });
    }

    [Fact]
    public void SplitHeight_NotEnoughForOverheads_ReturnsZerosNotNegative()
    {
        var r = LayoutMath.SplitHeight(50f, new[] { 56f, 56f }, new[] { 1f, 1f }, new[] { false, false }, 4f);
        r.ShouldBe(new[] { 0f, 0f });
    }

    [Fact]
    public void SplitWidth_MinsFirstThenWeights()
    {
        // 총 800, 가중치 1:3, gap 8, 최소 100/100 → 잔여 592 → 148/444 → 248/544
        var r = LayoutMath.SplitWidth(800f, new[] { 1f, 3f }, 8f, new[] { 100f, 100f });
        r.ShouldBe(new[] { 248f, 544f });
    }

    [Fact]
    public void SplitWidth_TooNarrow_ReturnsMins()
    {
        var r = LayoutMath.SplitWidth(100f, new[] { 1f, 1f }, 8f, new[] { 100f, 100f });
        r.ShouldBe(new[] { 100f, 100f });
    }

    [Theory]
    [InlineData(400f, 55f, 4f, 6)]   // floor(404/59) = 6
    [InlineData(55f,  55f, 4f, 1)]
    [InlineData(10f,  55f, 4f, 1)]   // 최소 1
    [InlineData(0f,   55f, 4f, 1)]
    public void Wrap_CellsPerRow(float totalW, float cellW, float gap, int expected)
    {
        LayoutMath.Wrap(totalW, cellW, gap).ShouldBe(expected);
    }

    [Fact]
    public void Clamp_BelowMin_RaisesToMin()
    {
        var r = LayoutMath.Clamp(new Rect(0, 0, 100, 100), new PanelBounds(600, 400), 1920, 1080);
        (r.width, r.height).ShouldBe((600f, 400f));
    }

    [Fact]
    public void Clamp_AboveMax_LowersToMax()
    {
        var r = LayoutMath.Clamp(new Rect(0, 0, 3000, 2000), new PanelBounds(600, 400), 1920, 1080);
        (r.width, r.height).ShouldBe((1920f, 1080f));
    }

    [Fact]
    public void Clamp_MinAboveMax_MinWins()
    {
        // 화면(500×300)이 최소(600×400)보다 작으면 최소가 이긴다 — 창이 화면을 넘더라도 사용 가능하게
        var r = LayoutMath.Clamp(new Rect(0, 0, 100, 100), new PanelBounds(600, 400), 500, 300);
        (r.width, r.height).ShouldBe((600f, 400f));
    }

    [Fact]
    public void Clamp_KeepsPosition()
    {
        var r = LayoutMath.Clamp(new Rect(70, 80, 100, 100), new PanelBounds(600, 400), 1920, 1080);
        (r.x, r.y).ShouldBe((70f, 80f));
    }
}
