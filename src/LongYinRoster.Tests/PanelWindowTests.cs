using System;
using System.IO;
using BepInEx.Configuration;
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — PanelWindow 의 IMGUI 아닌 부분(Hydrate/SetRect/Persist/ContentRect). Screen 스텁 = 1920×1080.</summary>
public class PanelWindowTests
{
    private static readonly PanelBounds B = new(300f, 200f);

    private static RectBinding MakeBinding(float x, float y, float w, float h, bool? open = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lyr-panelwindow-{Guid.NewGuid():N}.cfg");
        var cfg = new ConfigFile(path, false);
        return RectBinding.Of(
            cfg.Bind("T", "X", x, ""), cfg.Bind("T", "Y", y, ""),
            cfg.Bind("T", "W", w, ""), cfg.Bind("T", "H", h, ""),
            open.HasValue ? cfg.Bind("T", "Open", open.Value, "") : null);
    }

    private static PanelWindow Make(RectBinding b) => new(0x1234, "test", () => B, b);

    [Fact]
    public void Hydrate_ClampsOffscreenRectIntoScreen()
    {
        var w = Make(MakeBinding(1800, 1000, 400, 300));
        w.Hydrate(1920, 1080);
        (w.Rect.x, w.Rect.y, w.Rect.width, w.Rect.height).ShouldBe((1520f, 780f, 400f, 300f));
    }

    [Fact]
    public void Hydrate_RaisesBelowMinToMin()
    {
        var w = Make(MakeBinding(0, 0, 100, 100));
        w.Hydrate(1920, 1080);
        (w.Rect.width, w.Rect.height).ShouldBe((300f, 200f));
    }

    [Fact]
    public void Hydrate_ReadsOpenEntryIntoVisible()
    {
        var w = Make(MakeBinding(10, 10, 400, 300, open: true));
        w.Visible.ShouldBeFalse();
        w.Hydrate(1920, 1080);
        w.Visible.ShouldBeTrue();
    }

    [Fact]
    public void SetRect_ClampsAndPersistsToConfig()
    {
        var b = MakeBinding(0, 0, 400, 300);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.SetRect(10, 20, 500, 400);
        (b.X.Value, b.Y.Value, b.W.Value, b.H.Value).ShouldBe((10f, 20f, 500f, 400f));
        (w.Rect.width, w.Rect.height).ShouldBe((500f, 400f));
    }

    [Fact]
    public void SetRect_BelowMin_ClampsToMin()
    {
        var b = MakeBinding(0, 0, 400, 300);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.SetRect(10, 20, 10, 10);
        (b.W.Value, b.H.Value).ShouldBe((300f, 200f));
    }

    [Fact]
    public void Persist_WritesVisibleToOpenEntry()
    {
        var b = MakeBinding(10, 10, 400, 300, open: false);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.Visible = true;
        w.Persist();
        b.Open!.Value.ShouldBeTrue();
    }

    [Fact]
    public void Close_HidesPersistsAndRaisesOnClosed()
    {
        var b = MakeBinding(10, 10, 400, 300, open: true);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        bool raised = false;
        w.OnClosed = () => raised = true;
        w.Close();
        w.Visible.ShouldBeFalse();
        b.Open!.Value.ShouldBeFalse();
        raised.ShouldBeTrue();
    }

    [Fact]
    public void ContentRect_UsesDialogStyleConstants()
    {
        var w = Make(MakeBinding(0, 0, 400, 300));
        w.Hydrate(1920, 1080);
        var c = w.ContentRect;
        (c.x, c.y, c.width, c.height).ShouldBe(
            (DialogStyle.Padding, DialogStyle.HeaderHeight + DialogStyle.Padding,
             400f - 2 * DialogStyle.Padding, 300f - DialogStyle.HeaderHeight - 2 * DialogStyle.Padding));
    }

    [Fact]
    public void UnboundBinding_HydrateAndPersist_DoNotThrow()
    {
        // 테스트 환경의 SettingsPanel 은 Config 가 Bind 되지 않아 null 엔트리로 생성된다
        var w = Make(RectBinding.Of(null!, null!, null!, null!));
        Should.NotThrow(() => w.Hydrate(1920, 1080));
        Should.NotThrow(() => w.Persist());
        (w.Rect.width, w.Rect.height).ShouldBe((300f, 200f));   // 최소 크기로 시작
    }

    [Fact]
    public void Registry_HydrateAllAndPersistAll_TouchEveryWindow()
    {
        var b1 = MakeBinding(1800, 1000, 400, 300);
        var b2 = MakeBinding(0, 0, 100, 100);
        var w1 = Make(b1); var w2 = Make(b2);
        var reg = new PanelRegistry();
        reg.Register(w1); reg.Register(w2);
        reg.HydrateAll(1920, 1080);
        w1.Rect.x.ShouldBe(1520f);
        w2.Rect.width.ShouldBe(300f);
        reg.PersistAll();
        b1.X.Value.ShouldBe(1520f);
        b2.W.Value.ShouldBe(300f);
    }

    // ── 코너 리사이즈는 GUI.Window 콜백 밖(OnGUI, 화면 좌표)에서 처리 — 마우스가 창 밖으로 나가도 드래그가 이어진다 ──
    // 스텁의 GUI.Window 는 콜백을 호출하지 않으므로, 아래가 통과하면 이벤트 처리가 콜백 밖에 있다는 뜻.

    [Fact]
    public void CornerDrag_ContinuesOutsideWindow_AndPersistsOnMouseUp()
    {
        var b = MakeBinding(100, 100, 400, 300);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.Visible = true;
        var e = Event.current;
        try
        {
            e.type = EventType.MouseDown; e.mousePosition = new Vector2(492, 392);   // 핸들(484..500, 384..400) 안
            w.OnGUI(_ => { });
            e.type = EventType.MouseDrag; e.mousePosition = new Vector2(700, 600);   // 창 밖으로 한 번에 이동
            w.OnGUI(_ => { });
            (w.Rect.width, w.Rect.height).ShouldBe((608f, 508f));
            e.type = EventType.MouseUp;
            w.OnGUI(_ => { });
            (b.W.Value, b.H.Value).ShouldBe((608f, 508f));
        }
        finally { e.type = EventType.Repaint; e.mousePosition = default; }
    }

    [Fact]
    public void MouseDownOutsideHandle_DoesNotStartResize()
    {
        var w = Make(MakeBinding(100, 100, 400, 300));
        w.Hydrate(1920, 1080);
        w.Visible = true;
        var e = Event.current;
        try
        {
            e.type = EventType.MouseDown; e.mousePosition = new Vector2(200, 200);
            w.OnGUI(_ => { });
            e.type = EventType.MouseDrag; e.mousePosition = new Vector2(700, 600);
            w.OnGUI(_ => { });
            (w.Rect.width, w.Rect.height).ShouldBe((400f, 300f));
        }
        finally { e.type = EventType.Repaint; e.mousePosition = default; }
    }

    // ── 리뷰 후속(2026-09-29) ──

    [Fact]
    public void Visible_Setter_PersistsOpenEntry()
    {
        // 모드 전환이 Visible 을 직접 세팅해도 Open 이 따라가야 한다 (v0.7.13 은 per-frame 저장이었음)
        var b = MakeBinding(10, 10, 400, 300, open: false);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.Visible = true;
        b.Open!.Value.ShouldBeTrue();
        w.Visible = false;
        b.Open!.Value.ShouldBeFalse();
    }

    [Fact]
    public void ReloadRect_ReadsConfigWithoutTouchingVisible()
    {
        // "영속화 정보 reset" 이 Config 를 바꾼 뒤 창 rect 만 다시 읽는다 — Visible/Open 은 건드리지 않음
        var b = MakeBinding(10, 10, 400, 300, open: false);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.Visible = true;
        b.X.Value = 300; b.Y.Value = 150; b.W.Value = 620; b.H.Value = 560;
        var reg = new PanelRegistry();
        reg.Register(w);
        reg.ReloadRects(1920, 1080);
        (w.Rect.x, w.Rect.y, w.Rect.width, w.Rect.height).ShouldBe((300f, 150f, 620f, 560f));
        w.Visible.ShouldBeTrue();
    }

    [Fact]
    public void Hydrate_ZeroScreen_KeepsSavedRectAndStaysUnhydrated()
    {
        // 초기 프레임에 Screen 이 0×0 이면 클램프가 저장값을 (0,0,min) 으로 파괴한다 → 클램프 없이 읽고 첫 OnGUI 에서 재시도
        var w = Make(MakeBinding(1800, 1000, 400, 300));
        w.Hydrate(0, 0);
        (w.Rect.x, w.Rect.y, w.Rect.width, w.Rect.height).ShouldBe((1800f, 1000f, 400f, 300f));
        w.IsHydrated.ShouldBeFalse();
        w.Hydrate(1920, 1080);
        (w.Rect.x, w.Rect.y).ShouldBe((1520f, 780f));
        w.IsHydrated.ShouldBeTrue();
    }
}
