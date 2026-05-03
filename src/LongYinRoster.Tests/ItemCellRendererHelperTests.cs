using LongYinRoster.UI;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemCellRendererHelperTests
{
    [Theory]
    [InlineData(0, "")]
    [InlineData(-1, "")]
    [InlineData(1, "+1")]
    [InlineData(3, "+3")]
    [InlineData(15, "+15")]
    public void BadgeText_RendersOnlyWhenPositive(int enhanceLv, string expected)
    {
        ItemCellRenderer.BadgeText(enhanceLv).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, "착")]
    [InlineData(false, "")]
    public void EquippedMarker_RendersOnlyWhenTrue(bool equipped, string expected)
    {
        ItemCellRenderer.EquippedMarker(equipped).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0.61f, 0.64f, 0.69f)]    // 회색
    [InlineData(1, 0.13f, 0.77f, 0.37f)]    // 녹
    [InlineData(2, 0.22f, 0.74f, 0.97f)]    // 하늘
    [InlineData(3, 0.66f, 0.33f, 0.97f)]    // 보라
    [InlineData(4, 0.98f, 0.45f, 0.09f)]    // 오렌지
    [InlineData(5, 0.94f, 0.27f, 0.27f)]    // 빨강
    public void GradeColor_Returns6StepHex(int grade, float r, float g, float b)
    {
        var c = ItemCellRenderer.GradeColor(grade);
        c.r.ShouldBe(r, 0.001f);
        c.g.ShouldBe(g, 0.001f);
        c.b.ShouldBe(b, 0.001f);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(99)]
    public void GradeColor_OutOfRange_ReturnsWhite(int grade)
    {
        ItemCellRenderer.GradeColor(grade).ShouldBe(Color.white);
    }

    [Theory]
    [InlineData(0, 0.61f, 0.64f, 0.69f)]    // 회색 (잔품)
    [InlineData(1, 0.13f, 0.77f, 0.37f)]    // 녹 (하품)
    [InlineData(2, 0.22f, 0.74f, 0.97f)]    // 하늘 (중품)
    [InlineData(3, 0.66f, 0.33f, 0.97f)]    // 보라 (상품)
    [InlineData(4, 0.98f, 0.45f, 0.09f)]    // 오렌지 (진품)
    [InlineData(5, 0.94f, 0.27f, 0.27f)]    // 빨강 (극품)
    public void QualityColor_Returns6StepHex(int quality, float r, float g, float b)
    {
        var c = ItemCellRenderer.QualityColor(quality);
        c.r.ShouldBe(r, 0.001f);
        c.g.ShouldBe(g, 0.001f);
        c.b.ShouldBe(b, 0.001f);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void QualityColor_OutOfRange_ReturnsWhite(int quality)
    {
        ItemCellRenderer.QualityColor(quality).ShouldBe(Color.white);
    }
}
