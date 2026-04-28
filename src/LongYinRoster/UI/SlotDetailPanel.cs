using System;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>오른쪽 선택 슬롯 상세 + 액션 버튼.</summary>
public sealed class SlotDetailPanel
{
    public Action<int>? OnApplyRequested;
    public Action<int>? OnDeleteRequested;
    public Action<int>? OnRenameRequested;
    public Action<int>? OnCommentRequested;
    public Action<int>? OnRestoreRequested;

    public void Draw(SlotEntry entry, bool inGame)
    {
        GUILayout.BeginVertical();

        if (entry.IsEmpty)
        {
            GUILayout.Label(inGame
                ? KoreanStrings.EmptyStateNoSlots
                : KoreanStrings.EmptyStateNoGame);
            GUILayout.EndVertical();
            return;
        }

        var m = entry.Meta!;
        var s = m.Summary;

        GUILayout.Label($"슬롯 {entry.Index:D2} · {s.HeroName} ({s.HeroNickName})");
        GUILayout.Space(4);
        Row("캡처",        m.CapturedAt.ToString("yyyy-MM-dd HH:mm"));
        Row("출처",        m.CaptureSource == "live" ? "라이브" : $"파일 {m.CaptureSourceDetail}");
        Row("세이브 시점", m.GameSaveDetail);
        Row("전투력",      s.FightScore.ToString("N0"));
        Row("무공",        $"{s.KungfuCount} (Lv10 {s.KungfuMaxLvCount})");
        Row("인벤토리",    $"{s.ItemCount} / 창고 {s.StorageCount}");
        Row("금전",        $"{s.Money:N0}냥");
        Row("천부",        $"{s.TalentCount}개");
        if (!string.IsNullOrEmpty(m.UserComment))
            Row("메모", m.UserComment);

        GUILayout.Space(8);

        if (entry.Index == 0)
        {
            // Restore (slot 0 → game) v0.1 미지원 — Apply 와 같은 IL2CPP reference-link 문제.
            GUI.enabled = false;
            GUILayout.Button(KoreanStrings.RestoreBtn + " (v0.2 예정)");
            GUI.enabled = true;
        }
        else
        {
            // Apply (slot → game) v0.1 미지원.
            GUI.enabled = false;
            GUILayout.Button(KoreanStrings.ApplyBtn + " (v0.2 예정)");
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(KoreanStrings.RenameBtn))  OnRenameRequested ?.Invoke(entry.Index);
            if (GUILayout.Button(KoreanStrings.CommentBtn)) OnCommentRequested?.Invoke(entry.Index);
            if (GUILayout.Button(KoreanStrings.DeleteBtn))  OnDeleteRequested ?.Invoke(entry.Index);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }

    private static void Row(string k, string v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(k, GUILayout.Width(80));
        GUILayout.Label(v);
        GUILayout.EndHorizontal();
    }
}
