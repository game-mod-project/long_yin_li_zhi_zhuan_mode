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
    private static readonly Color HeaderTint  = new(0.15f, 0.15f, 0.20f, 1.0f);
    public  const float HeaderHeight = 28f;
    // v0.8.0 — 레이아웃 계산기(UI/Layout)와 Draw 가 공유하는 단일 출처. 계산 ≠ 렌더 어긋남 방지.
    public  const float RowHeight       = 24f;   // 리스트 행 · 입력 행
    public  const float ButtonRowHeight = 28f;   // 버튼 줄 (기존 GUILayout.Height(28) 관례)
    public  const float Padding         = 12f;   // 창 내부 여백 (GUI.Window skin padding 흡수, smoke 로 조정)
    public  const float Gap             = 4f;    // 요소 간 간격
    public  const float ScrollbarW      = 20f;   // BeginScrollView 세로 스크롤바 폭 예약 — 행 폭 계산에서 뺀다(안 빼면 가로 스크롤바 생김)
    public  const float ImguiSlack      = 32f;   // GUILayout 암묵 여백(스크롤뷰·버튼 margin) + GUI.Window skin padding — 2026-09-29 smoke 실측(하단 잘림)
    /// <summary>창 크롬 총 높이 = 헤더 + 상하 여백 + 암묵 여백(84). 창 높이 − ChromeH = 계산기가 쓸 수 있는 내용 높이(PanelWindow.ContentRect.height).
    /// 계산기 MinH 는 ChromeH + 내용 최소 — Compute 에서 ImguiSlack 을 다시 빼지 않는다.</summary>
    public  const float ChromeH         = HeaderHeight + 2f * Padding + ImguiSlack;

    /// <summary>DrawWindow callback 시작에 호출. 좌표는 window-local (0,0 = top-left).</summary>
    public static void FillBackground(float width, float height)
    {
        var prev = GUI.color;
        GUI.color = OverlayTint;
        GUI.DrawTexture(new Rect(0f, 0f, width, height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    /// <summary>창 상단에 두꺼운 header bar + 흰색 bold 제목 오버레이.
    /// FillBackground 다음에 호출. content 는 y=HeaderHeight (28) 부터 시작.</summary>
    public static void DrawHeader(float width, string title)
    {
        var prevColor = GUI.color;
        GUI.color = HeaderTint;
        GUI.DrawTexture(new Rect(0f, 0f, width, HeaderHeight), Texture2D.whiteTexture);
        GUI.color = Color.white;
        var prevAlign = GUI.skin.label.alignment;
        var prevSize  = GUI.skin.label.fontSize;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize  = 15;
        GUI.Label(new Rect(0f, 0f, width, HeaderHeight), title);
        GUI.skin.label.alignment = prevAlign;
        GUI.skin.label.fontSize  = prevSize;
        GUI.color = prevColor;
    }
}
