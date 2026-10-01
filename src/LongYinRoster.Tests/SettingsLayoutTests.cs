using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — 설정 패널 계산기. 세로 확장 = 설정 스크롤, 가로 확장 = rect 입력 필드.</summary>
public class SettingsLayoutTests
{
    // 기본 창 480×600 → 내용 456×516 (ChromeH 84 = 헤더 28 + 여백 24 + 암묵 여백 32 — ContentRect 가 이미 뺀다, D1)
    private static readonly Rect Default = new(12, 40, 456, 516);

    [Fact]
    public void Compute_DefaultWindow_ScrollTakesRemainingHeight()
    {
        var L = SettingsLayout.Compute(Default);
        // 버튼 줄 + gap 만 뺀다 — 암묵 여백은 ContentRect 가 이미 뺐다(D1)
        L.ScrollH.ShouldBe(516f - DialogStyle.ButtonRowHeight - DialogStyle.Gap);   // 484
    }

    [Fact]
    public void Compute_DefaultWindow_FieldWidthSplitsRemaining()
    {
        var L = SettingsLayout.Compute(Default);
        // (456 - 스크롤바 20 - 2*20 - 8 - 6*Gap 24) / 2 = 182 — 세로 스크롤바 폭과 컨트롤 margin 을 빼야 가로 스크롤바가 안 생김
        L.FieldW.ShouldBe(182f);
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
        var a = SettingsLayout.Compute(new Rect(12, 40, 456, 516));
        var b = SettingsLayout.Compute(new Rect(12, 40, 456, 900));
        b.ScrollH.ShouldBeGreaterThan(a.ScrollH);
    }

    [Fact]
    public void MinSize_FitsFixedPartsPlusThreeRows()
    {
        var m = SettingsLayout.MinSize;
        m.MinW.ShouldBe(120f + 180f + 80f + 4 * DialogStyle.Gap + DialogStyle.ScrollbarW + 2 * DialogStyle.Padding);   // 440
        m.MinH.ShouldBe(DialogStyle.ChromeH + 3 * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight);   // 188
    }
}
