using System;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>왼쪽 슬롯 21행 + 상단 [+ 저장] / [F 파일에서] 버튼.</summary>
public sealed class SlotListPanel
{
    public int Selected { get; private set; } = 1;
    public Action? OnSaveCurrentRequested;
    public Action? OnImportFromFileRequested;

    private Vector2 _scroll;

    public void Draw(SlotRepository repo, float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width));

        // Top action buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(KoreanStrings.SaveCurrentBtn, GUILayout.ExpandWidth(true)))
            OnSaveCurrentRequested?.Invoke();
        if (GUILayout.Button(KoreanStrings.ImportFromFileBtn, GUILayout.Width(100)))
            OnImportFromFileRequested?.Invoke();
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        // Slot list
        _scroll = GUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < repo.All.Count; i++)
        {
            var entry = repo.All[i];
            var label = i == 0
                ? (entry.IsEmpty
                    ? $"00 · {KoreanStrings.AutoBackupEmpty}"
                    : $"00 · {KoreanStrings.SlotAutoBackup}")
                : (entry.IsEmpty
                    ? $"{i:D2} · {KoreanStrings.SlotEmpty}"
                    : $"{i:D2} · {entry.Meta!.UserLabel}");

            var prev = GUI.color;
            if (i == Selected) GUI.color = new Color(0.4f, 0.55f, 0.85f);
            else if (entry.IsEmpty) GUI.color = new Color(0.6f, 0.6f, 0.6f);

            if (GUILayout.Button(label, GUILayout.Height(22))) Selected = i;

            GUI.color = prev;
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }
}
