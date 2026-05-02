using System;
using LongYinRoster.Core;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>오른쪽 선택 슬롯 상세 + 액션 버튼 + v0.4 9-카테고리 체크박스 grid.</summary>
public sealed class SlotDetailPanel
{
    public Action<int>? OnApplyRequested;
    public Action<int>? OnDeleteRequested;
    public Action<int>? OnRenameRequested;
    public Action<int>? OnCommentRequested;
    public Action<int>? OnRestoreRequested;
    public Action<int, ApplySelection>? OnApplySelectionChanged;

    public void Draw(SlotEntry entry, bool inGame, Capabilities cap)
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
            // Restore (slot 0) — 체크박스 노출 안 함, [↶ 복원] 버튼만
            GUI.enabled = inGame;
            if (GUILayout.Button(KoreanStrings.RestoreBtn))
                OnRestoreRequested?.Invoke(entry.Index);
            GUI.enabled = true;
        }
        else
        {
            // 체크박스 grid (3 컬럼 x 3 행, 9 카테고리)
            DrawApplySelectionGrid(entry.Index, m.ApplySelection, cap);

            GUILayout.Space(6);

            // Apply 버튼
            GUI.enabled = inGame;
            if (GUILayout.Button(KoreanStrings.ApplyBtn))
                OnApplyRequested?.Invoke(entry.Index);
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(KoreanStrings.RenameBtn))  OnRenameRequested ?.Invoke(entry.Index);
            if (GUILayout.Button(KoreanStrings.CommentBtn)) OnCommentRequested?.Invoke(entry.Index);
            if (GUILayout.Button(KoreanStrings.DeleteBtn))  OnDeleteRequested ?.Invoke(entry.Index);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }

    /// <summary>
    /// 9-카테고리 체크박스 grid. Capabilities false 인 카테고리는 disabled + "(v0.5+ 후보)" suffix.
    /// 토글 변경 시 OnApplySelectionChanged 콜백 호출 → ModWindow 가 SlotRepository.UpdateApplySelection 으로 디스크 즉시 저장.
    /// </summary>
    private void DrawApplySelectionGrid(int slotIndex, ApplySelection sel, Capabilities cap)
    {
        GUILayout.Label(KoreanStrings.ApplySectionHeader);
        bool changed = false;

        // Row 1: 스탯 / 명예 / 천부 (v0.3 검증 — 항상 enabled)
        GUILayout.BeginHorizontal();
        changed |= ToggleCell(KoreanStrings.Cat_Stat,         sel.Stat,         enabled: true,            v => sel.Stat = v);
        changed |= ToggleCell(KoreanStrings.Cat_Honor,        sel.Honor,        enabled: true,            v => sel.Honor = v);
        changed |= ToggleCell(KoreanStrings.Cat_TalentTag,    sel.TalentTag,    enabled: true,            v => sel.TalentTag = v);
        GUILayout.EndHorizontal();

        // Row 2: 스킨 / 자기집 add / 정체성 (v0.3 + v0.4 신규 정체성)
        GUILayout.BeginHorizontal();
        changed |= ToggleCell(KoreanStrings.Cat_Skin,         sel.Skin,         enabled: true,            v => sel.Skin = v);
        changed |= ToggleCell(KoreanStrings.Cat_SelfHouse,    sel.SelfHouse,    enabled: true,            v => sel.SelfHouse = v);
        changed |= ToggleCell(KoreanStrings.Cat_Identity,     sel.Identity,     enabled: cap.Identity,    v => sel.Identity = v);
        GUILayout.EndHorizontal();

        // Row 3: 무공 active / 인벤토리 / 창고 (v0.4 신규 — capability gate)
        // v0.6.0: 인벤토리 OFF → 착용 장비 자동 OFF (착용 장비 = 인벤토리 grid index 참조)
        GUILayout.BeginHorizontal();
        changed |= ToggleCell(KoreanStrings.Cat_ActiveKungfu, sel.ActiveKungfu, enabled: cap.ActiveKungfu, v => sel.ActiveKungfu = v);
        changed |= ToggleCell(KoreanStrings.Cat_ItemList,     sel.ItemList,     enabled: cap.ItemList,     v => {
            sel.ItemList = v;
            if (!v) sel.NowEquipment = false;  // 인벤토리 OFF 시 착용 장비도 OFF
        });
        changed |= ToggleCell(KoreanStrings.Cat_SelfStorage,  sel.SelfStorage,  enabled: cap.SelfStorage,  v => sel.SelfStorage = v);
        GUILayout.EndHorizontal();

        // Row 4: 무공 목록 (v0.5.2) / 착용 장비 (v0.6.0 신규 — ItemList capability 공유)
        // 착용 장비 ON → 인벤토리 자동 ON (장비 grid index 가 인벤토리 grid 참조이므로
        // 인벤토리 미적용 시 의미 없음). 착용 장비 OFF → 인벤토리는 그대로 유지.
        GUILayout.BeginHorizontal();
        changed |= ToggleCell(KoreanStrings.Cat_KungfuList,   sel.KungfuList,   enabled: cap.KungfuList,   v => sel.KungfuList = v);
        changed |= ToggleCell(KoreanStrings.Cat_NowEquipment, sel.NowEquipment, enabled: cap.ItemList,     v => {
            sel.NowEquipment = v;
            if (v) sel.ItemList = true;  // ON 시 인벤토리도 강제 ON (linkage)
            // OFF 시 인벤토리 토글 변경 안 함
        });
        GUILayout.EndHorizontal();

        if (changed)
            OnApplySelectionChanged?.Invoke(slotIndex, sel);
    }

    /// <summary>
    /// IMGUI Toggle cell. Capabilities false 면 GUI.enabled=false + label 에 "(v0.5+ 후보)" suffix.
    /// state 변경 시 setter 호출 + true 반환. disabled 면 항상 false 반환 (어떤 클릭도 무시).
    /// </summary>
    private static bool ToggleCell(string label, bool state, bool enabled, Action<bool> setter)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && enabled;
        string lbl = enabled ? label : (label + KoreanStrings.Cat_DisabledSuffix);
        bool before = enabled ? state : false;
        bool after = GUILayout.Toggle(before, lbl, GUILayout.Width(140));
        GUI.enabled = wasEnabled;
        if (!enabled || after == state) return false;
        setter(after);
        return true;
    }

    private static void Row(string k, string v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(k, GUILayout.Width(80));
        GUILayout.Label(v);
        GUILayout.EndHorizontal();
    }
}
