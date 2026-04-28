using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// IMGUI window 의 default skin 위에 그릴 어두운 overlay. 사용자가 메인 창과 다이얼로그를
/// 더 명확하게 인식하도록 시각적 불투명도를 보강.
///
/// IL2CPP-safe 패턴만 사용: Texture2D.whiteTexture (정적 게임 자체 자원) + GUI.color tint
/// + GUI.DrawTexture(Rect, Texture) 2-arg overload (strip 위험 가장 낮음).
/// </summary>
public static class DialogStyle
{
    private static readonly Color OverlayTint = new(0f, 0f, 0f, 0.85f);

    /// <summary>DrawWindow callback 시작에 호출. 좌표는 window-local (0,0 = top-left).</summary>
    public static void FillBackground(float width, float height)
    {
        var prev = GUI.color;
        GUI.color = OverlayTint;
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        GUI.color = prev;
    }
}
