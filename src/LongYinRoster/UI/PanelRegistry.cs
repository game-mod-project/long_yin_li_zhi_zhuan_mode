using System;
using System.Collections.Generic;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.8.0 — 이관된 패널 창의 Hydrate/Persist 를 묶고(1단계), 코너 리사이즈 이벤트를 z-order 로 라우팅한다(3단계, D2).
/// 리스트 순서 = z-order(끝 = 맨 앞). MouseDown 이 어떤 등록 창 안이면 그 창을 맨 앞으로 올린다 — Unity 도 같은 클릭으로
/// GUI.Window 를 앞으로 가져오므로 두 순서가 함께 움직인다. 핸들 판정은 그 맨 앞 창에만 시킨다.
/// 아직 이관 안 된 창(ItemDetail/PlayerEditor/본체)이 덮은 자리는 IsCoveredByForeign 으로 막는다 — 6단계에 전부 등록되면 제거.
/// </summary>
public sealed class PanelRegistry
{
    private readonly List<PanelWindow> _windows = new();

    /// <summary>등록 안 된 창이 이 화면 좌표를 덮고 있으면 true. ModWindow 가 세팅. null 이면 검사 안 함.</summary>
    public Func<Vector2, bool>? IsCoveredByForeign;

    public void Register(PanelWindow window)
    {
        _windows.Add(window);
        window.Registry = this;
    }

    public void HydrateAll(float screenW, float screenH)
    {
        foreach (var w in _windows) w.Hydrate(screenW, screenH);
    }

    public void ReloadRects(float screenW, float screenH)
    {
        foreach (var w in _windows) w.ReloadRect(screenW, screenH);
    }

    public void PersistAll()
    {
        foreach (var w in _windows) w.Persist();
    }

    /// <summary>ModWindow.OnGUI 맨 앞에서 매 패스 한 번. 패널들의 OnGUI(GUI.Window) 보다 먼저 실행돼야 핸들이 창 내부 컨트롤보다 먼저 이벤트를 잡는다.</summary>
    public void HandleEvents()
    {
        var e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseDown)
        {
            var top = TopmostVisibleAt(e.mousePosition);
            if (top == null) return;
            BringToFront(top);
            if (IsCoveredByForeign != null && IsCoveredByForeign(e.mousePosition)) return;
            top.TryBeginResize(e);
        }
        else if (e.type == EventType.MouseDrag || e.type == EventType.MouseUp)
        {
            foreach (var w in _windows)
                if (w.HandleResizeContinuation(e)) return;
        }
    }

    /// <summary>화면 좌표를 포함하는 보이는 창 중 맨 앞. 없으면 null.</summary>
    internal PanelWindow? TopmostVisibleAt(Vector2 pos)
    {
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var w = _windows[i];
            if (w.Visible && w.Rect.Contains(pos)) return w;
        }
        return null;
    }

    private void BringToFront(PanelWindow w)
    {
        _windows.Remove(w);
        _windows.Add(w);
    }
}
