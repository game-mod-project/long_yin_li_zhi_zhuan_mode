using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — 창 틀 순수 로직. 마우스 좌표는 GUI.Window 콜백 안의 창 로컬 좌표 — 델타만 쓰므로 무관.</summary>
public class PanelWindowLogicTests
{
    private static readonly PanelBounds B = new(600f, 400f);

    [Fact]
    public void ContentRect_SubtractsHeaderAndPadding_InLocalCoords()
    {
        var c = PanelWindowLogic.ContentRect(new Rect(100, 50, 800, 600), headerH: 28f, padding: 12f);
        (c.x, c.y, c.width, c.height).ShouldBe((12f, 40f, 776f, 548f));
    }

    [Fact]
    public void ContentRect_NeverNegative()
    {
        var c = PanelWindowLogic.ContentRect(new Rect(0, 0, 10, 10), 28f, 12f);
        (c.width, c.height).ShouldBe((0f, 0f));
    }

    [Fact]
    public void Resize_AppliesMouseDelta()
    {
        var r = PanelWindowLogic.Resize(new Rect(100, 100, 800, 600),
            startMouse: new Vector2(790, 590), startSize: new Vector2(800, 600), mouse: new Vector2(890, 690),
            B, 1920, 1080);
        (r.x, r.y, r.width, r.height).ShouldBe((100f, 100f, 900f, 700f));
    }

    [Fact]
    public void Resize_BelowMin_ClampsToMin()
    {
        var r = PanelWindowLogic.Resize(new Rect(100, 100, 800, 600),
            new Vector2(790, 590), new Vector2(800, 600), new Vector2(90, 90), B, 1920, 1080);
        (r.width, r.height).ShouldBe((600f, 400f));
    }

    [Fact]
    public void Resize_BeyondScreen_ClampsSizeAndMovesInside()
    {
        // (1500,900) 에서 300×100 창을 +1000 끌면 폭 1300 → x+w=2800 > 1920 → x 가 620 으로 밀림
        var r = PanelWindowLogic.Resize(new Rect(1500, 900, 600, 400),
            new Vector2(590, 390), new Vector2(600, 400), new Vector2(1590, 390), B, 1920, 1080);
        r.width.ShouldBe(1600f);
        (r.x + r.width).ShouldBe(1920f);
        r.x.ShouldBe(320f);
    }

    [Fact]
    public void ClampToScreen_MovesOffscreenRectInside()
    {
        var r = PanelWindowLogic.ClampToScreen(new Rect(1800, 1000, 700, 500), 1920, 1080, B);
        (r.x, r.y, r.width, r.height).ShouldBe((1220f, 580f, 700f, 500f));
    }

    [Fact]
    public void ClampToScreen_NegativeOrigin_PinsToZero()
    {
        var r = PanelWindowLogic.ClampToScreen(new Rect(-50, -20, 700, 500), 1920, 1080, B);
        (r.x, r.y).ShouldBe((0f, 0f));
    }

    [Fact]
    public void ClampToScreen_ScreenSmallerThanMin_PinsToOrigin()
    {
        // 화면 500×300 < 최소 600×400 → 최소 크기 유지, (0,0) 에 맞춤, 예외 없음
        var r = PanelWindowLogic.ClampToScreen(new Rect(100, 100, 100, 100), 500, 300, B);
        (r.x, r.y, r.width, r.height).ShouldBe((0f, 0f, 600f, 400f));
    }

    [Fact]
    public void ClampToScreen_AlreadyInside_Unchanged()
    {
        var r = PanelWindowLogic.ClampToScreen(new Rect(100, 100, 700, 500), 1920, 1080, B);
        (r.x, r.y, r.width, r.height).ShouldBe((100f, 100f, 700f, 500f));
    }
}
