using System;
using System.IO;
using BepInEx.Configuration;
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S3 — PanelRegistry 가 등록 창의 코너 리사이즈 이벤트를 z-order 로 라우팅한다.
/// 스텁의 Event.current 는 정적 단일 인스턴스라 각 테스트가 finally 로 되돌린다. GUI.Window 스텁은 콜백을 호출하지 않는다.</summary>
public class PanelRegistryTests
{
    private static readonly PanelBounds B = new(300f, 200f);

    private static RectBinding MakeBinding(float x, float y, float w, float h)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lyr-registry-{Guid.NewGuid():N}.cfg");
        var cfg = new ConfigFile(path, false) { SaveOnConfigSet = false };
        return RectBinding.Of(cfg.Bind("T", "X", x, ""), cfg.Bind("T", "Y", y, ""), cfg.Bind("T", "W", w, ""), cfg.Bind("T", "H", h, ""));
    }

    private static PanelWindow Make(int id, float x, float y, float w, float h)
    {
        var win = new PanelWindow(id, $"w{id}", () => B, MakeBinding(x, y, w, h));
        win.Hydrate(1920, 1080);
        win.Visible = true;
        return win;
    }

    // A: (100,100,400,300) → 핸들 (484..500, 384..400). B: (300,200,400,300) 가 A 의 핸들 자리를 덮는다.
    private static (PanelRegistry reg, PanelWindow a, PanelWindow b) Overlapping()
    {
        var a = Make(1, 100, 100, 400, 300);
        var b = Make(2, 300, 200, 400, 300);
        var reg = new PanelRegistry();
        reg.Register(a);
        reg.Register(b);     // 나중 등록 = 맨 앞
        return (reg, a, b);
    }

    private static void Fire(PanelRegistry reg, EventType type, float x, float y)
    {
        var e = Event.current;
        e.type = type; e.mousePosition = new Vector2(x, y);
        reg.HandleEvents();
    }

    private static void Reset() { Event.current.type = EventType.Repaint; Event.current.mousePosition = default; }

    [Fact]
    public void HandleEvents_HandleOfCoveredWindow_DoesNotResize()
    {
        var (reg, a, b) = Overlapping();
        try
        {
            Fire(reg, EventType.MouseDown, 492, 392);   // A 의 핸들이지만 B 가 그 자리를 덮고 있다
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
            (b.Rect.width, b.Rect.height).ShouldBe((400f, 300f));
            a.IsResizing.ShouldBeFalse();
        }
        finally { Reset(); }
    }

    [Fact]
    public void HandleEvents_ClickInsideWindow_BringsItToFront_ThenItsHandleWins()
    {
        var (reg, a, b) = Overlapping();
        try
        {
            Fire(reg, EventType.MouseDown, 150, 150);   // A 만 포함하는 자리 → A 가 맨 앞으로
            Fire(reg, EventType.MouseUp,   150, 150);
            reg.TopmostVisibleAt(new Vector2(492, 392)).ShouldBeSameAs(a);
            Fire(reg, EventType.MouseDown, 492, 392);   // 이제 A 의 핸들이 이긴다
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((608f, 508f));
            (b.Rect.width, b.Rect.height).ShouldBe((400f, 300f));
            Fire(reg, EventType.MouseUp, 700, 600);
            a.IsResizing.ShouldBeFalse();
        }
        finally { Reset(); }
    }

    [Fact]
    public void HandleEvents_HiddenWindowIsIgnored()
    {
        var (reg, a, b) = Overlapping();
        try
        {
            b.Visible = false;
            Fire(reg, EventType.MouseDown, 492, 392);
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((608f, 508f));
            Fire(reg, EventType.MouseUp, 700, 600);
        }
        finally { Reset(); }
    }

    [Fact]
    public void HandleEvents_ForeignCoverBlocksResize()
    {
        // 아직 이관 안 된 창(ItemDetail 등)이 그 자리를 덮고 있으면 등록 창은 리사이즈를 시작하지 않는다
        var (reg, a, b) = Overlapping();
        try
        {
            b.Visible = false;
            reg.IsCoveredByForeign = _ => true;
            Fire(reg, EventType.MouseDown, 492, 392);
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
        }
        finally { Reset(); }
    }

    [Fact]
    public void RegisteredWindow_OnGUI_DoesNotHandleEventsItself()
    {
        // 등록된 창은 레지스트리만 이벤트를 처리한다 — 창의 OnGUI 가 또 처리하면 이중 처리
        var a = Make(1, 100, 100, 400, 300);
        var reg = new PanelRegistry();
        reg.Register(a);
        var e = Event.current;
        try
        {
            e.type = EventType.MouseDown; e.mousePosition = new Vector2(492, 392);
            a.OnGUI(_ => { });
            e.type = EventType.MouseDrag; e.mousePosition = new Vector2(700, 600);
            a.OnGUI(_ => { });
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
        }
        finally { Reset(); }
    }

    // ── 리뷰 후속(I-1): 미이관 창의 앞/뒤를 마지막 클릭으로 추적 — 겹친 자리는 미이관 창이 앞일 때만 막는다 ──
    // F = 미이관 창 (450,350,300,300) → A 의 핸들 (484..500, 384..400) 을 덮는다.
    private static bool InF(Vector2 p) => p.x >= 450 && p.x < 750 && p.y >= 350 && p.y < 650;

    [Fact]
    public void ForeignBehind_RegisteredCornerStillResizable()
    {
        var a = Make(1, 100, 100, 400, 300);
        var reg = new PanelRegistry { IsCoveredByForeign = InF };
        reg.Register(a);
        try
        {
            Fire(reg, EventType.MouseDown, 150, 150);   // A 만 있는 자리 클릭 → A 가 앞, F 는 뒤
            Fire(reg, EventType.MouseUp,   150, 150);
            Fire(reg, EventType.MouseDown, 492, 392);   // A 핸들(F 영역과 겹침) — F 가 뒤라 A 가 잡는다
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((608f, 508f));
            Fire(reg, EventType.MouseUp, 700, 600);
        }
        finally { Reset(); }
    }

    [Fact]
    public void ForeignInFront_BlocksRegisteredCorner()
    {
        var a = Make(1, 100, 100, 400, 300);
        var reg = new PanelRegistry { IsCoveredByForeign = InF };
        reg.Register(a);
        try
        {
            Fire(reg, EventType.MouseDown, 150, 150);   // A 앞
            Fire(reg, EventType.MouseUp,   150, 150);
            Fire(reg, EventType.MouseDown, 700, 600);   // F 만 있는 자리 → F 가 앞
            Fire(reg, EventType.MouseUp,   700, 600);
            Fire(reg, EventType.MouseDown, 492, 392);   // 겹친 자리 — F 가 앞이라 F 의 클릭
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
        }
        finally { Reset(); }
    }

    [Fact]
    public void ForeignClick_DoesNotBringRegisteredWindowToFront()
    {
        // 미이관 창이 앞인 채로 그 창과 A 가 겹친 자리를 클릭 → Unity 는 미이관 창을 올린다. 레지스트리도 A 를 올리면 안 된다.
        var (reg, a, b) = Overlapping();
        reg.IsCoveredByForeign = p => p.x < 200;   // (150,150) 은 A 와 미이관 창이 겹친 자리
        try
        {
            Fire(reg, EventType.MouseDown, 150, 150);
            Fire(reg, EventType.MouseUp,   150, 150);
            reg.TopmostVisibleAt(new Vector2(492, 392)).ShouldBeSameAs(b);
        }
        finally { Reset(); }
    }
}
