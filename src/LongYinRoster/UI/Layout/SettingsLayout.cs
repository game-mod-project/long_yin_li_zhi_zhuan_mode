using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 — SettingsPanel 계산기. 고정: 단축키 행(라벨 120 / 표시 180 / 버튼 80), rect 라벨 20, 하단 버튼 줄 28, 스크롤바 20, 암묵 여백 32.
/// 세로 확장: 설정 스크롤(남는 높이 전부). 가로 확장: rect 입력 필드 두 개가 잔여 폭을 반씩.
/// </summary>
public readonly record struct SettingsLayout(float ScrollH, float FieldW)
{
    public const float HotkeyLabelW   = 120f;
    public const float HotkeyDisplayW = 180f;
    public const float HotkeyButtonW  = 80f;
    public const float RectLabelW     = 20f;
    public const float RectPairSpace  = 8f;
    public const int   MinListRows    = 3;

    /// <summary>단축키 행 컨트롤 3개의 좌우 margin 합(GUILayout 기본 4px × 4).</summary>
    public const float HotkeyRowMargins = 4f * DialogStyle.Gap;
    /// <summary>rect 행(라벨·필드·간격·라벨·필드) 컨트롤 margin 합(4px × 6).</summary>
    public const float RectRowMargins   = 6f * DialogStyle.Gap;

    public static PanelBounds MinSize => new(
        MinW: HotkeyLabelW + HotkeyDisplayW + HotkeyButtonW + HotkeyRowMargins + DialogStyle.ScrollbarW + 2f * DialogStyle.Padding,
        MinH: DialogStyle.HeaderHeight + 2f * DialogStyle.Padding
              + MinListRows * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight + DialogStyle.ImguiSlack);

    public static SettingsLayout Compute(Rect content)
    {
        // 세로: 버튼 줄 + gap + IMGUI 암묵 여백을 빼야 버튼이 창 안에 남는다(smoke 실측). 가로: 세로 스크롤바 폭 + 컨트롤 margin.
        float scrollH = Math.Max(MinListRows * DialogStyle.RowHeight,
                                 content.height - DialogStyle.ButtonRowHeight - DialogStyle.Gap - DialogStyle.ImguiSlack);
        float fieldW  = Math.Max(40f, (content.width - DialogStyle.ScrollbarW - 2f * RectLabelW - RectPairSpace - RectRowMargins) / 2f);
        return new SettingsLayout(scrollH, fieldW);
    }
}
