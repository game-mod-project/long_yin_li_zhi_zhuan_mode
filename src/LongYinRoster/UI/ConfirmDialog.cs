using System;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// 재사용 가능한 IMGUI 모달 확인 다이얼로그.
///
/// IL2CPP 호환 주의:
/// - 이 게임의 IL2CPP 빌드는 GUIStyle 인자를 받는 GUILayout overload 와
///   GUILayout.FlexibleSpace() 를 strip 한다. Il2CppInterop 의 method-unstripping 이
///   매 프레임 NotSupportedException 을 던져 callback 자체가 부분 누락되므로,
///   GUILayout.Space(N) 만으로 명시 정렬한다.
/// - GUI.enabled 가 호출자(메인 창)에서 false 로 carry over 되면 버튼이 disabled
///   톤으로 그려져 거의 안 보인다. 진입 시 강제로 true 복원.
/// </summary>
public sealed class ConfirmDialog
{
    private static readonly int WindowId = "LongYinRosterConfirm".GetHashCode();

    private bool   _visible;
    private string _title        = "";
    private string _body         = "";
    private string _confirmLabel = "확인";
    private string _cancelLabel  = "취소";
    private Action? _onConfirm;

    private float _lastW;
    private float _lastH;

    public bool IsVisible => _visible;

    public void Show(string title, string body, string confirmLabel, Action onConfirm,
                     string? cancelLabel = null)
    {
        _title        = title;
        _body         = body;
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

        // body 의 줄 수에 비례해 다이얼로그 높이를 가변. wordWrap GUIStyle 은 strip 되어
        // 사용 불가하므로 호출자가 \n 으로 줄을 구분해 전달한다.
        int lines = _body.Length == 0 ? 1 : (CountChar(_body, '\n') + 1);
        float h = 140f + lines * 22f;
        const float W = 500f;
        _lastW = W;
        _lastH = h;
        var rect = new Rect((Screen.width - W) / 2f, (Screen.height - h) / 2f, W, h);
        GUILayout.Window(WindowId, rect, (GUI.WindowFunction)DrawWindow, _title);

        GUI.enabled = prev;
    }

    private static int CountChar(string s, char c)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++) if (s[i] == c) n++;
        return n;
    }

    private void DrawWindow(int id)
    {
        GUI.enabled = true;
        try
        {
            DialogStyle.FillBackground(_lastW, _lastH);
            GUILayout.Space(18);
            // wordWrap label 이 strip 되었으므로 호출자가 \n 으로 분리한 라인을 각각 Label.
            foreach (var line in _body.Split('\n'))
                GUILayout.Label(line);
            GUILayout.Space(20);

            // FlexibleSpace 사용 불가 (strip). 명시 Space 로 좌-버튼-간격-버튼-우 정렬.
            // 총 폭 480 - window padding 약간 = 약 460. 40 + 140 + 60 + 140 + 40 = 420.
            GUILayout.BeginHorizontal();
            GUILayout.Space(40);
            if (GUILayout.Button(_cancelLabel, GUILayout.Width(140), GUILayout.Height(36)))
                Close(invokeConfirm: false);
            GUILayout.Space(60);
            if (GUILayout.Button(_confirmLabel, GUILayout.Width(140), GUILayout.Height(36)))
                Close(invokeConfirm: true);
            GUILayout.Space(40);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
        }
        catch (Exception ex)
        {
            // strip 된 method 가 또 발견되면 log 폭주 대신 한 번만 logging.
            Logger.Warn($"ConfirmDialog.DrawWindow: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Close(bool invokeConfirm)
    {
        var cb = _onConfirm;
        _visible   = false;
        _onConfirm = null;
        if (invokeConfirm) cb?.Invoke();
    }
}
