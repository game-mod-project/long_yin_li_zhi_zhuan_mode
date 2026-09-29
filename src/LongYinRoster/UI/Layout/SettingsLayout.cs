using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 — SettingsPanel 계산기. 고정: 단축키 행(라벨 120 / 표시 180 / 버튼 80), rect 라벨 20, 하단 버튼 줄 28.
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

    public static PanelBounds MinSize => new(
        MinW: HotkeyLabelW + HotkeyDisplayW + HotkeyButtonW + 2f * DialogStyle.Gap + 2f * DialogStyle.Padding,
        MinH: DialogStyle.HeaderHeight + 2f * DialogStyle.Padding
              + MinListRows * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight);

    public static SettingsLayout Compute(Rect content)
    {
        float scrollH = Math.Max(MinListRows * DialogStyle.RowHeight,
                                 content.height - DialogStyle.ButtonRowHeight - DialogStyle.Gap);
        float fieldW  = Math.Max(40f, (content.width - 2f * RectLabelW - RectPairSpace - DialogStyle.Gap) / 2f);
        return new SettingsLayout(scrollH, fieldW);
    }
}
