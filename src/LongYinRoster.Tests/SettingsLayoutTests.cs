using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — 설정 패널 계산기. 세로 확장 = 설정 스크롤, 가로 확장 = rect 입력 필드.</summary>
public class SettingsLayoutTests
{
    // 기본 창 480×600 → 내용 456×548 (Padding 12, Header 28)
    private static readonly Rect Default = new(12, 40, 456, 548);

    [Fact]
    public void Compute_DefaultWindow_ScrollTakesRemainingHeight()
    {
        var L = SettingsLayout.Compute(Default);
        L.ScrollH.ShouldBe(548f - DialogStyle.ButtonRowHeight - DialogStyle.Gap);   // 516
    }

    [Fact]
    public void Compute_DefaultWindow_FieldWidthSplitsRemaining()
    {
        var L = SettingsLayout.Compute(Default);
        // (456 - 2*20 - 8 - 4) / 2 = 202
        L.FieldW.ShouldBe(202f);
    }

    [Fact]
    public void Compute_TinyContent_UsesFloors()
    {
        var L = SettingsLayout.Compute(new Rect(12, 40, 100, 50));
        L.ScrollH.ShouldBe(3 * DialogStyle.RowHeight);   // 72
        L.FieldW.ShouldBe(40f);
    }

    [Fact]
    public void Compute_TallerContent_MonotonicScroll()
    {
        var a = SettingsLayout.Compute(new Rect(12, 40, 456, 548));
        var b = SettingsLayout.Compute(new Rect(12, 40, 456, 900));
        b.ScrollH.ShouldBeGreaterThan(a.ScrollH);
    }

    [Fact]
    public void MinSize_FitsFixedPartsPlusThreeRows()
    {
        var m = SettingsLayout.MinSize;
        m.MinW.ShouldBe(120f + 180f + 80f + 2 * DialogStyle.Gap + 2 * DialogStyle.Padding);   // 412
        m.MinH.ShouldBe(DialogStyle.HeaderHeight + 2 * DialogStyle.Padding
                        + 3 * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight);   // 156
    }
}
