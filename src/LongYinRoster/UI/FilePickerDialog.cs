using System;
using System.Collections.Generic;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// 게임 자체 SaveSlot 0~10 목록을 보여주고 사용자가 import 할 슬롯을 선택하게 한다.
/// IL2CPP-safe IMGUI 규칙: default skin, GUILayoutOption[] overload 만, FlexibleSpace 회피,
/// GUI.enabled 강제 true, try/catch 가드.
/// </summary>
public sealed class FilePickerDialog
{
    private static readonly int WindowId = "LongYinRosterFilePicker".GetHashCode();

    private bool _visible;
    private List<SaveSlotInfo> _slots = new();
    private Action<int>? _onConfirm;
    private Vector2 _scroll;

    private const float W = 640f;
    private const float H = 480f;

    public bool IsVisible => _visible;

    public void Show(List<SaveSlotInfo> slots, Action<int> onConfirm)
    {
        _slots = slots;
        _onConfirm = onConfirm;
        _scroll = Vector2.zero;
        _visible = true;
    }

    public void Draw()
    {
        if (!_visible) return;

        var prev = GUI.enabled;
        GUI.enabled = true;

        var rect = new Rect((Screen.width - W) / 2f, (Screen.height - H) / 2f, W, H);
        GUILayout.Window(WindowId, rect, (GUI.WindowFunction)DrawWindow,
                         KoreanStrings.FilePickerTitle);

        GUI.enabled = prev;
    }

    private void DrawWindow(int id)
    {
        GUI.enabled = true;
        try
        {
            DialogStyle.FillBackground(W, H);

            GUILayout.Space(14);
            GUILayout.Label("게임 자체 저장 슬롯에서 캐릭터를 가져옵니다. 슬롯을 클릭하면 mod 슬롯에 캡처됩니다.");
            GUILayout.Space(8);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(340f));
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                var label = BuildLabel(s);

                GUI.enabled = s.Exists && !s.IsCurrentlyLoaded;
                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    // _onConfirm 캡처 후 Close (Close 가 _onConfirm 을 null 로 만들기 때문)
                    var cb = _onConfirm;
                    Close();
                    cb?.Invoke(s.SlotIndex);
                }
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Space(220);
            if (GUILayout.Button(KoreanStrings.Cancel, GUILayout.Width(160), GUILayout.Height(34)))
                Close();
            GUILayout.Space(220);
            GUILayout.EndHorizontal();
        }
        catch (Exception ex)
        {
            Logger.Warn($"FilePickerDialog.DrawWindow: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string BuildLabel(SaveSlotInfo s)
    {
        if (!s.Exists) return $"SaveSlot{s.SlotIndex} · (비어있음)";
        var prefix = s.IsCurrentlyLoaded ? KoreanStrings.FilePickerCurrentLoad + " " : "";
        var time   = s.SaveTime == default ? "" : s.SaveTime.ToString("MM-dd HH:mm");
        var hero   = string.IsNullOrEmpty(s.HeroName)
            ? "(이름없음)"
            : $"{s.HeroName} {s.HeroNickName} · 전투력 {s.FightScore:N0}";
        return $"{prefix}SaveSlot{s.SlotIndex} · {hero} · {time}";
    }

    private void Close()
    {
        _visible = false;
        _onConfirm = null;
    }
}
