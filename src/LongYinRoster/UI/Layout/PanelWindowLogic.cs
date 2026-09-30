using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>v0.8.0 — PanelWindow 의 순수 부분. Screen 크기는 호출자가 숫자로 넘긴다(테스트 가능).</summary>
public static class PanelWindowLogic
{
    public const float HandleSize = 16f;

    /// <summary>코너 리사이즈 핸들(화면 좌표). 창 밖 OnGUI 에서 MouseDown 판정에 쓴다 — 콜백 안(창 로컬)에서 잡으면 창 밖으로 나간 드래그를 놓친다.</summary>
    public static Rect ResizeHandleRect(Rect window)
        => new(window.x + window.width - HandleSize, window.y + window.height - HandleSize, HandleSize, HandleSize);

    /// <summary>헤더·여백·하단 암묵 여백(GUILayout margin + window skin padding)을 뺀 내용 영역. 창 로컬 좌표(0,0 = 창 좌상단).
    /// 폭·높이는 0 미만으로 내려가지 않음. 계산기는 이 높이를 그대로 쓴다(슬랙을 다시 빼지 않음).</summary>
    public static Rect ContentRect(Rect window, float headerH, float padding, float bottomSlack)
    {
        float w = Math.Max(0f, window.width - 2f * padding);
        float h = Math.Max(0f, window.height - headerH - 2f * padding - bottomSlack);
        return new Rect(padding, headerH + padding, w, h);
    }

    /// <summary>코너 드래그: 시작 크기 + 마우스 델타 → 경계·화면 클램프. 위치는 ClampToScreen 이 필요할 때만 옮긴다.</summary>
    public static Rect Resize(Rect r, Vector2 startMouse, Vector2 startSize, Vector2 mouse,
                              PanelBounds b, float screenW, float screenH)
    {
        // UnityStubs 의 Vector2 에 - 연산자가 없어 성분별로 계산
        float dx = mouse.x - startMouse.x;
        float dy = mouse.y - startMouse.y;
        var resized = new Rect(r.x, r.y, startSize.x + dx, startSize.y + dy);
        return ClampToScreen(resized, screenW, screenH, b);
    }

    /// <summary>크기를 [min, screen] 로 자르고 창 전체가 화면 안에 오도록 이동. 화면 &lt; 최소면 (0,0) 에 최소 크기.</summary>
    public static Rect ClampToScreen(Rect r, float screenW, float screenH, PanelBounds b)
    {
        var c = LayoutMath.Clamp(r, b, screenW, screenH);
        float x = c.x, y = c.y;
        if (x + c.width  > screenW) x = screenW - c.width;
        if (y + c.height > screenH) y = screenH - c.height;
        if (x < 0f) x = 0f;
        if (y < 0f) y = 0f;
        return new Rect(x, y, c.width, c.height);
    }
}
