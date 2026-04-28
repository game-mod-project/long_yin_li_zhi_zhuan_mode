using System;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// IMGUI 텍스트 입력 모달. ConfirmDialog 와 동일한 IL2CPP 호환 규칙:
/// default skin 만 사용 / GUI.enabled 강제 / FlexibleSpace 회피 / try/catch 가드.
/// 텍스트 필드 자체는 GUILayout.TextField — strip 발견 시 fallback 필요.
/// </summary>
public sealed class InputDialog
{
    private static readonly int WindowId = "LongYinRosterInput".GetHashCode();
    private const int MaxLen = 100;

    private bool   _visible;
    private string _title        = "";
    private string _prompt       = "";
    private string _value        = "";
    private string _confirmLabel = "확인";
    private string _cancelLabel  = "취소";
    private Action<string>? _onConfirm;

    public bool IsVisible => _visible;

    public void Show(string title, string prompt, string initialValue, string confirmLabel,
                     Action<string> onConfirm, string? cancelLabel = null)
    {
        _title        = title;
        _prompt       = prompt;
        _value        = initialValue ?? "";
        _confirmLabel = confirmLabel;
        _cancelLabel  = cancelLabel ?? KoreanStrings.Cancel;
        _onConfirm    = onConfirm;
        _visible      = true;
    }

    public void Draw()
    {
        if (!_visible) return;

        var prev = GUI.enabled;
        GUI.enabled = true;

        const float W = 520f, H = 200f;
        var rect = new Rect((Screen.width - W) / 2f, (Screen.height - H) / 2f, W, H);
        GUILayout.Window(WindowId, rect, (GUI.WindowFunction)DrawWindow, _title);

        GUI.enabled = prev;
    }

    private void DrawWindow(int id)
    {
        GUI.enabled = true;
        try
        {
            DialogStyle.FillBackground(520f, 200f);
            GUILayout.Space(14);
            GUILayout.Label(_prompt);
            GUILayout.Space(8);

            // GUILayout.TextField 가 IL2CPP 환경에서 strip 되어 있을 수 있음. 작동 안 하면
            // catch 블록이 한 번 logging — input 불가능 시 사용자가 취소만 가능.
            _value = GUILayout.TextField(_value ?? "", MaxLen);

            GUILayout.Space(20);

            GUILayout.BeginHorizontal();
            GUILayout.Space(40);
            if (GUILayout.Button(_cancelLabel, GUILayout.Width(140), GUILayout.Height(34)))
                Close(invokeConfirm: false);
            GUILayout.Space(80);
            if (GUILayout.Button(_confirmLabel, GUILayout.Width(140), GUILayout.Height(34)))
                Close(invokeConfirm: true);
            GUILayout.Space(40);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }
        catch (Exception ex)
        {
            Logger.Warn($"InputDialog.DrawWindow: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Close(bool invokeConfirm)
    {
        var cb = _onConfirm;
        var v  = _value;
        _visible   = false;
        _onConfirm = null;
        if (invokeConfirm) cb?.Invoke(v ?? "");
    }
}
