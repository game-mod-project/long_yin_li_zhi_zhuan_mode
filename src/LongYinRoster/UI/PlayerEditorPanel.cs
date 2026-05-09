using System;
using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.8 — Player editor panel (F11+4 진입). 5 섹션:
/// 1. Resource stats (Phase 1) — hp/maxhp/power/maxpower/mana/maxmana/fame + Quick actions
/// 2-4. HeroSpeAddData × 3 (Phase 2) — baseAddData / heroBuff / totalAddData
/// 5. 천부 (Phase 4 — spike 결과 후 활성)
/// 6. 무공 (Phase 5 — spike 결과 후 활성)
///
/// strip-safe IMGUI 패턴만 (v0.7.7 검증된 것 재사용).
/// </summary>
public sealed class PlayerEditorPanel
{
    public bool Visible { get; set; } = false;
    public Rect WindowRect => _rect;

    private Rect _rect = new(200, 120, 480, 720);
    private const int WindowID = 0x4C593736;   // "LY76"

    private Vector2 _scroll = Vector2.zero;
    private readonly Dictionary<string, string> _textBuf = new();   // path → text
    private readonly Dictionary<string, bool>   _sectionOpen = new()
    {
        { "resource",   true  },   // default 펼침
        { "baseAdd",    true  },
        { "heroBuff",   false },
        { "totalAdd",   false },
        { "tag",        false },
        { "kungfu",     false },
    };

    private readonly SelectorDialog _selector = new();
    public SelectorDialog Selector => _selector;

    // v0.7.8 — 돌파속성 별도 dialog (inline expand 의 panel 스크롤 길어짐 문제 해결)
    private readonly SkillBreakthroughDialog _breakthroughDialog = new();
    public SkillBreakthroughDialog BreakthroughDialog => _breakthroughDialog;

    public Func<object?>? GetPlayer;
    public Action? OnAppliedRefreshRequest;

    public void Init(float defaultX, float defaultY, float defaultWidth, float defaultHeight)
    {
        // v0.7.8 사용자 피드백 — 480 → 720 (무공 9 카테고리 탭 + 등급/문파 표시 위해)
        float w = Math.Max(defaultWidth, 720f);
        float h = Math.Max(defaultHeight, 720f);
        _rect = new Rect(defaultX, defaultY, w, h);
    }

    public void OnGUI()
    {
        if (!Visible) return;
        try
        {
            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("PlayerEditorPanel", $"PlayerEditorPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }

        _selector.OnGUI();
        _breakthroughDialog.OnGUI();
    }

    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, KoreanStrings.PlayerEditorTitle);

            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
                Visible = false;

            GUILayout.Space(DialogStyle.HeaderHeight);

            var player = GetPlayer?.Invoke();
            if (player == null)
            {
                DrawEmpty();
                GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
                return;
            }

            // 헤더 — 이름 / heroID / 전투력 (있을 때)
            DrawPlayerHeader(player);
            GUILayout.Space(4);

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 100));

            // Section 1: Resource stats + Quick actions
            DrawSectionHeader("resource", KoreanStrings.PlayerEditorSection_Resource);
            if (_sectionOpen["resource"]) DrawResourceSection(player);

            // Section 2: baseAddData
            DrawSectionHeader("baseAdd", KoreanStrings.PlayerEditorSection_BaseAdd);
            if (_sectionOpen["baseAdd"])
                DrawHeroSpeAddDataSection(player, "baseAddData", "base");

            // Section 3: heroBuff
            DrawSectionHeader("heroBuff", KoreanStrings.PlayerEditorSection_HeroBuff);
            if (_sectionOpen["heroBuff"])
                DrawHeroSpeAddDataSection(player, "heroBuff", "buff");

            // Section 4: totalAddData (⚠)
            DrawSectionHeader("totalAdd", KoreanStrings.PlayerEditorSection_TotalAdd);
            if (_sectionOpen["totalAdd"])
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.5f, 1f);
                GUILayout.Label(KoreanStrings.PlayerEditorTotalAddWarn);
                GUI.color = prev;
                DrawHeroSpeAddDataSection(player, "totalAddData", "total");
            }

            // Section 5: 천부 (Phase 4)
            DrawSectionHeader("tag", KoreanStrings.PlayerEditorSection_Tag);
            if (_sectionOpen["tag"]) DrawHeroTagDataSection(player);

            // Section 6: 무공 (Phase 5)
            DrawSectionHeader("kungfu", KoreanStrings.PlayerEditorSection_Kungfu);
            if (_sectionOpen["kungfu"]) DrawKungfuSkillSection(player);

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("PlayerEditorPanel", $"PlayerEditorPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void DrawEmpty()
    {
        GUILayout.Space(60);
        GUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUILayout.Label("게임 진입 후 사용 가능 (player null)");
        GUILayout.EndHorizontal();
    }

    private void DrawPlayerHeader(object player)
    {
        string name  = ReadString(player, "heroName");
        int heroID   = ReadInt(player, "heroID");
        int age      = ReadInt(player, "age");
        float fight  = ReadFloat(player, "fightScore");

        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {HangulDict.Translate(name)} (heroID={heroID})", GUILayout.Width(280));
        GUILayout.Label($"{age}세 / 전투 {fight:F0}", GUILayout.Width(160));
        GUILayout.EndHorizontal();
    }

    private void DrawSectionHeader(string key, string label)
    {
        bool open = _sectionOpen[key];
        string arrow = open ? "▼" : "▶";
        if (GUILayout.Button($"{arrow} {label.TrimStart('▼').TrimStart()}"))
        {
            _sectionOpen[key] = !open;
        }
    }

    // ───── Section 1: Resource ─────

    private void DrawResourceSection(object player)
    {
        // 7 stat row + 3 quick actions
        DrawResourceRow(player, "hp",       "생명",      "maxhp");
        DrawResourceRow(player, "maxhp",    "최대 생명",  null);
        DrawResourceRow(player, "power",    "체력",      "maxPower");
        DrawResourceRow(player, "maxPower", "최대 체력",  null);
        DrawResourceRow(player, "mana",     "내력",      "maxMana");
        DrawResourceRow(player, "maxMana",  "최대 내력",  null);
        DrawResourceRow(player, "fame",     "명예",      null);

        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        // v0.7.8 — 사용자 피드백: 전체 회복 통합 (생명+체력+내력). 별도 RestoreEnergy 버튼 제거.
        if (GUILayout.Button(KoreanStrings.PlayerEditorQuickFullHeal, GUILayout.Width(140)))
            DoQuick(() => Core.PlayerEditApplier.QuickFullHeal(player), KoreanStrings.PlayerEditorQuickFullHeal);
        if (GUILayout.Button(KoreanStrings.PlayerEditorQuickCureInjuries, GUILayout.Width(140)))
            DoQuick(() => Core.PlayerEditApplier.QuickCureInjuries(player), KoreanStrings.PlayerEditorQuickCureInjuries);
        GUILayout.EndHorizontal();
    }

    private void DrawResourceRow(object player, string fieldName, string krLabel, string? maxFieldName)
    {
        // v0.7.8 사용자 피드백 — `/ {max}` 표시 제거 (max row 가 별도 존재). maxFieldName 인자 deprecated.
        if (!_textBuf.ContainsKey(fieldName))
            _textBuf[fieldName] = ReadFloat(player, fieldName).ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {krLabel}: ", GUILayout.Width(120));
        _textBuf[fieldName] = GUILayout.TextField(_textBuf[fieldName], GUILayout.Width(120));
        if (GUILayout.Button("수정", GUILayout.Width(50)))
        {
            ApplyResourceField(player, fieldName, krLabel);
        }
        GUILayout.EndHorizontal();
    }

    private void ApplyResourceField(object player, string fieldName, string krLabel)
    {
        if (!float.TryParse(_textBuf[fieldName], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push("값 형식 잘못됨 (소수)", ToastKind.Error);
            return;
        }
        var r = Core.PlayerEditApplier.ApplyResource(player, fieldName, val);
        if (r.Success)
        {
            ToastService.Push(string.Format(KoreanStrings.PlayerEditorApplyOk, krLabel, val), ToastKind.Success);
            _textBuf.Remove(fieldName);   // 다음 frame 재읽음
            OnAppliedRefreshRequest?.Invoke();
        }
        else
        {
            ToastService.Push(string.Format(KoreanStrings.PlayerEditorApplyFailed, krLabel, r.Error ?? "?"), ToastKind.Error);
        }
    }

    private void DoQuick(Func<bool> action, string label)
    {
        bool ok;
        try { ok = action(); }
        catch (Exception ex) { Logger.Warn($"Quick {label}: {ex.GetType().Name}: {ex.Message}"); ok = false; }
        if (ok)
        {
            ToastService.Push(string.Format(KoreanStrings.PlayerEditorQuickOk, label), ToastKind.Success);
            // textfield 모두 reset (다음 frame 재읽음)
            _textBuf.Clear();
            OnAppliedRefreshRequest?.Invoke();
        }
        else
        {
            ToastService.Push(string.Format(KoreanStrings.PlayerEditorQuickFailed, label), ToastKind.Error);
        }
    }

    // ───── Section 2-4: HeroSpeAddData × 3 (v0.7.7 mirror) ─────

    private void DrawHeroSpeAddDataSection(object player, string addDataName, string keyTag)
    {
        var addData = ReadFieldOrPropertyRaw(player, addDataName);
        if (addData == null)
        {
            GUILayout.Label($"  ({addDataName} 부재)");
            return;
        }

        var entries = Core.HeroSpeAddDataReflector.GetEntries(addData);
        string keyPrefix = $"player.{keyTag}";

        // 기존 entry rows
        foreach (var (entType, entVal) in entries)
        {
            string label = Core.SpeAddTypeNames.Get(entType);
            string tbKey = $"{keyPrefix}.{entType}";
            if (!_textBuf.ContainsKey(tbKey))
                _textBuf[tbKey] = entVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {label}({entType}):", GUILayout.Width(140));
            _textBuf[tbKey] = GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(60));
            if (GUILayout.Button("수정", GUILayout.Width(45)))
            {
                ApplyStatEditMutation(player, addData, addDataName, entType, _textBuf[tbKey], isAdd: false);
            }
            if (GUILayout.Button("삭제", GUILayout.Width(45)))
            {
                ApplyStatEditDelete(player, addData, addDataName, entType, keyPrefix);
            }
            GUILayout.EndHorizontal();
        }

        // 신규 entry 추가 row — selector popup
        string newIdxKey = $"{keyPrefix}.new.idx";
        string newValKey = $"{keyPrefix}.new.value";
        if (!_textBuf.ContainsKey(newIdxKey)) _textBuf[newIdxKey] = "0";
        if (!_textBuf.ContainsKey(newValKey)) _textBuf[newValKey] = "";

        GUILayout.BeginHorizontal();
        GUILayout.Label("  추가:", GUILayout.Width(60));
        int currentIdx = 0;
        int.TryParse(_textBuf[newIdxKey], out currentIdx);
        string currentLabel = Core.SpeAddTypeNames.Get(currentIdx);
        if (GUILayout.Button($"{currentLabel}({currentIdx}) ▼", GUILayout.Width(160)))
        {
            string capturedKey = newIdxKey;
            _selector.Show("속성 선택", Core.SpeAddTypeNames.AllOrdered(), selected =>
            {
                _textBuf[capturedKey] = selected.ToString();
            });
        }
        _textBuf[newValKey] = GUILayout.TextField(_textBuf[newValKey], GUILayout.Width(60));
        if (GUILayout.Button("추가", GUILayout.Width(45)))
        {
            ApplyStatEditAdd(player, addData, addDataName, _textBuf[newIdxKey], _textBuf[newValKey], newIdxKey, newValKey);
        }
        GUILayout.EndHorizontal();
    }

    private void ApplyStatEditMutation(object player, object addData, string addDataName, int type, string input, bool isAdd)
    {
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push("값 형식 잘못됨 (소수)", ToastKind.Error);
            return;
        }
        string label = Core.SpeAddTypeNames.Get(type);
        bool ok = Core.HeroSpeAddDataReflector.TrySet(addData, type, val);
        if (ok)
        {
            // heroBuff 변경 시 dirty flag set
            if (addDataName == "heroBuff")
                Core.PlayerEditApplier.TryReflectionSetter(player, "heroBuffDirty", true);

            // totalAddData 변경 시 RefreshMaxAttriAndSkill skip (사용자 변경 derived 재계산 방지)
            if (addDataName != "totalAddData")
                Core.ItemEditApplier.PostMutationRefresh(player, player);

            string fmt = isAdd ? "✔ {0} = {1} 추가" : KoreanStrings.PlayerEditorApplyOk;
            ToastService.Push(string.Format(fmt, label, val), ToastKind.Success);
            OnAppliedRefreshRequest?.Invoke();
        }
        else
        {
            ToastService.Push(string.Format(KoreanStrings.PlayerEditorApplyFailed, label, "TrySet 실패"), ToastKind.Error);
        }
    }

    private void ApplyStatEditDelete(object player, object addData, string addDataName, int type, string keyPrefix)
    {
        string label = Core.SpeAddTypeNames.Get(type);
        bool ok = Core.HeroSpeAddDataReflector.TryRemove(addData, type);
        if (ok)
        {
            _textBuf.Remove($"{keyPrefix}.{type}");
            if (addDataName == "heroBuff")
                Core.PlayerEditApplier.TryReflectionSetter(player, "heroBuffDirty", true);
            if (addDataName != "totalAddData")
                Core.ItemEditApplier.PostMutationRefresh(player, player);
            ToastService.Push($"✔ {label} 삭제", ToastKind.Success);
            OnAppliedRefreshRequest?.Invoke();
        }
        else
        {
            ToastService.Push(string.Format(KoreanStrings.PlayerEditorApplyFailed, label, "TryRemove 실패"), ToastKind.Error);
        }
    }

    private void ApplyStatEditAdd(object player, object addData, string addDataName, string idxInput, string valInput, string newIdxKey, string newValKey)
    {
        if (!int.TryParse(idxInput, out int typeIdx) || typeIdx < 0 || typeIdx > 999)
        {
            ToastService.Push("type idx 범위 0~999", ToastKind.Error);
            return;
        }
        ApplyStatEditMutation(player, addData, addDataName, typeIdx, valInput, isAdd: true);
        _textBuf[newIdxKey] = "0";
        _textBuf[newValKey] = "";
    }

    // ───── Section 5: 천부 (heroTagData) — Phase 4 ─────

    private int _tagPage = 0;

    private void DrawHeroTagDataSection(object player)
    {
        var entries = Core.HeroTagDataReflector.GetEntries(player);

        // v0.7.8 사용자 피드백 — 천부점 / 보유갯수 / 최대 (read-only) row
        DrawTagPointRow(player);
        DrawTagCountRow(player, entries.Count);
        GUILayout.Space(4);

        // v0.7.8 사용자 피드백 — 신규 추가 row 를 보유 목록 위로 이동
        DrawTagAddRow(player);
        GUILayout.Space(4);

        // 페이지 nav
        int totalPages = (entries.Count + PAGE_SIZE - 1) / PAGE_SIZE;
        if (totalPages == 0) totalPages = 1;
        if (_tagPage >= totalPages) _tagPage = totalPages - 1;
        if (_tagPage < 0) _tagPage = 0;
        int start = _tagPage * PAGE_SIZE;
        int end = System.Math.Min(start + PAGE_SIZE, entries.Count);

        GUILayout.Label($"  보유 천부 ({entries.Count}개)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30)) && _tagPage > 0) _tagPage--;
        GUILayout.Label($"  {_tagPage + 1} / {totalPages} 페이지", GUILayout.Width(120));
        if (GUILayout.Button("▶", GUILayout.Width(30)) && _tagPage < totalPages - 1) _tagPage++;
        GUILayout.EndHorizontal();

        // 현재 페이지의 entry 만 표시
        for (int i = start; i < end; i++)
        {
            var (tagID, leftTime, sourceHero) = entries[i];
            string label = Core.HeroTagNameCache.Get(tagID);
            string ttKey = $"player.tag.{tagID}";
            if (!_textBuf.ContainsKey(ttKey))
                _textBuf[ttKey] = leftTime.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

            GUILayout.BeginHorizontal();
            // v0.7.8 — 점수별 색상 (≤5녹/≤10파/≤15보/≤20주/>20적)
            var prevTagColor = GUI.color;
            GUI.color = Core.TagPointColors.ForTagID(tagID);
            GUILayout.Label($"  {label}({tagID})", GUILayout.Width(160));
            GUI.color = prevTagColor;
            _textBuf[ttKey] = GUILayout.TextField(_textBuf[ttKey], GUILayout.Width(60));
            GUILayout.Label(leftTime < 0 ? "(영구)" : "(임시)", GUILayout.Width(50));
            if (GUILayout.Button("수정", GUILayout.Width(45)))
            {
                ApplyTagSetLeftTime(player, tagID, label, _textBuf[ttKey]);
            }
            if (GUILayout.Button("삭제", GUILayout.Width(45)))
            {
                ApplyTagRemove(player, tagID, label, ttKey);
            }
            GUILayout.EndHorizontal();
        }

        // 신규 추가 row 는 v0.7.8 사용자 피드백으로 위로 이동 (DrawTagAddRow)
    }

    /// <summary>v0.7.8 — 천부점 (heroTagPoint) 편집 row.</summary>
    private void DrawTagPointRow(object player)
    {
        const string tbKey = "player.tagPoint";
        if (!_textBuf.ContainsKey(tbKey))
            _textBuf[tbKey] = Core.HeroTagDataReflector.GetTagPoint(player).ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

        GUILayout.BeginHorizontal();
        GUILayout.Label("  천부점:", GUILayout.Width(80));
        _textBuf[tbKey] = GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(80));
        if (GUILayout.Button("수정", GUILayout.Width(50)))
        {
            if (float.TryParse(_textBuf[tbKey], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
            {
                bool ok = Core.HeroTagDataReflector.TrySetTagPoint(player, val);
                ToastService.Push(ok ? $"✔ 천부점 = {val} 적용" : "✘ 천부점 변경 실패", ok ? ToastKind.Success : ToastKind.Error);
                if (ok) _textBuf.Remove(tbKey);
            }
            else ToastService.Push("값 형식 잘못됨", ToastKind.Error);
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>v0.7.8 — 보유 / 영구 / 최대 천부 갯수 표시 (영구/최대는 game-self method 결과, read-only).</summary>
    private void DrawTagCountRow(object player, int totalCount)
    {
        int permanent = Core.HeroTagDataReflector.GetPermanentTagCount(player);
        int maxNum    = Core.HeroTagDataReflector.GetMaxTagCount(player);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"  보유: {totalCount}개 (영구 {permanent} / 최대 {maxNum})");
        GUILayout.EndHorizontal();
    }

    /// <summary>v0.7.8 — 천부 추가 row (selector 선택 시 즉시 추가, 추가 버튼 제거).</summary>
    private void DrawTagAddRow(object player)
    {
        const string newTimeKey = "player.tag.new.leftTime";
        if (!_textBuf.ContainsKey(newTimeKey)) _textBuf[newTimeKey] = "-1";

        GUILayout.BeginHorizontal();
        GUILayout.Label("  추가:", GUILayout.Width(60));
        if (GUILayout.Button("천부 선택 ▼", GUILayout.Width(180)))
        {
            // 이미 보유한 tagID set 만들기 (marker 용)
            var owned = new System.Collections.Generic.HashSet<int>();
            foreach (var (tid, _, _) in Core.HeroTagDataReflector.GetEntries(player)) owned.Add(tid);

            _selector.Show("천부 선택", Core.HeroTagNameCache.AllOrdered(), selected =>
            {
                ApplyTagAddSmart(player, selected);
            },
            Core.HeroTagNameCache.BuildCategoryTabs(),
            width: 640f, height: 600f,
            markedFn: tid => owned.Contains(tid),
            colorFn: tid => Core.TagPointColors.ForTagID(tid));
        }
        _textBuf[newTimeKey] = GUILayout.TextField(_textBuf[newTimeKey], GUILayout.Width(50));
        GUILayout.Label("(시간, -1=영구)", GUILayout.Width(110));   // v0.7.8: 80→110 줄바꿈 회피
        GUILayout.EndHorizontal();
    }

    /// <summary>
    /// v0.7.8 사용자 피드백 — 천부 추가 인게임 메커니즘 반영:
    ///   고급 카테고리 = 개별 추가 OK
    ///   非고급 = sameMeaning 그룹 내 단계 progression (낮은 value 자동 제거 + 새 추가, downgrade 거부).
    /// </summary>
    private void ApplyTagAddSmart(object player, int newTagID)
    {
        const string newTimeKey = "player.tag.new.leftTime";
        if (!float.TryParse(_textBuf[newTimeKey], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float leftTime))
            leftTime = -1f;

        var meta = Core.HeroTagNameCache.GetMeta(newTagID);
        string label = meta != null ? meta.NameKr : $"태그({newTagID})";

        // 1. 고급 카테고리 — 개별 추가
        if (meta != null && meta.CategoryKr == "고급")
        {
            bool ok1 = Core.HeroTagDataReflector.TryAddTag(player, newTagID, leftTime, "");
            ToastService.Push(ok1 ? $"✔ {label} 추가" : $"✘ {label} 추가 실패",
                ok1 ? ToastKind.Success : ToastKind.Error);
            return;
        }

        // 2. sameMeaning 그룹 처리 (非고급)
        if (meta == null || string.IsNullOrEmpty(meta.SameMeaning))
        {
            // sameMeaning 부재 → 단순 add
            bool ok2 = Core.HeroTagDataReflector.TryAddTag(player, newTagID, leftTime, "");
            ToastService.Push(ok2 ? $"✔ {label} 추가" : $"✘ {label} 추가 실패",
                ok2 ? ToastKind.Success : ToastKind.Error);
            return;
        }

        var entries = Core.HeroTagDataReflector.GetEntries(player);
        // 같은 sameMeaning 그룹의 기존 entry 들 검색
        int existingMaxValue = -1;
        var sameMeaningTagIDs = new System.Collections.Generic.List<int>();
        foreach (var (existingTagID, _, _) in entries)
        {
            var existingMeta = Core.HeroTagNameCache.GetMeta(existingTagID);
            if (existingMeta == null) continue;
            if (existingMeta.SameMeaning == meta.SameMeaning)
            {
                sameMeaningTagIDs.Add(existingTagID);
                if (existingMeta.Value > existingMaxValue) existingMaxValue = existingMeta.Value;
            }
        }

        // 비교: 새 value vs 기존 max
        if (existingMaxValue >= meta.Value && existingMaxValue > 0)
        {
            // 동급 (이미 보유) 또는 상위 (downgrade 거부)
            string reason = existingMaxValue == meta.Value
                ? "이미 보유"
                : $"이미 더 높은 단계 (현재 {existingMaxValue}점, 새 {meta.Value}점) — downgrade 불가";
            ToastService.Push($"⚠ {label} {reason}", ToastKind.Info);
            return;
        }

        // upgrade: 같은 그룹의 기존 entry 모두 제거
        int removed = 0;
        foreach (var oldTagID in sameMeaningTagIDs)
        {
            if (Core.HeroTagDataReflector.TryRemoveTag(player, oldTagID)) removed++;
        }
        bool added = Core.HeroTagDataReflector.TryAddTag(player, newTagID, leftTime, "");
        if (added)
        {
            string msg = removed > 0
                ? $"✔ {label} 추가 (이전 단계 {removed}개 제거)"
                : $"✔ {label} 추가";
            ToastService.Push(msg, ToastKind.Success);
        }
        else
        {
            ToastService.Push($"✘ {label} 추가 실패", ToastKind.Error);
        }
    }

    private void ApplyTagSetLeftTime(object player, int tagID, string label, string input)
    {
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push("값 형식 잘못됨", ToastKind.Error);
            return;
        }
        bool ok = Core.HeroTagDataReflector.TrySetLeftTime(player, tagID, val);
        if (ok)
        {
            ToastService.Push($"✔ {label} 시간 = {val} 적용", ToastKind.Success);
            _textBuf.Remove($"player.tag.{tagID}");
        }
        else
        {
            ToastService.Push($"✘ {label} 변경 실패", ToastKind.Error);
        }
    }

    private void ApplyTagRemove(object player, int tagID, string label, string ttKey)
    {
        bool ok = Core.HeroTagDataReflector.TryRemoveTag(player, tagID);
        if (ok)
        {
            ToastService.Push($"✔ {label} 삭제", ToastKind.Success);
            _textBuf.Remove(ttKey);
        }
        else
        {
            ToastService.Push($"✘ {label} 삭제 실패", ToastKind.Error);
        }
    }

    private void ApplyTagAdd(object player, string idxInput, string timeInput, string newTagKey, string newTimeKey)
    {
        if (!int.TryParse(idxInput, out int tagID) || tagID <= 0)
        {
            ToastService.Push("tag ID 가 잘못됨", ToastKind.Error);
            return;
        }
        if (!float.TryParse(timeInput, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float leftTime))
        {
            leftTime = -1f;
        }
        string label = Core.HeroTagNameCache.Get(tagID);
        bool ok = Core.HeroTagDataReflector.TryAddTag(player, tagID, leftTime, "");
        if (ok)
        {
            ToastService.Push($"✔ {label} 추가 (시간={leftTime})", ToastKind.Success);
            _textBuf[newTagKey] = "0";
            _textBuf[newTimeKey] = "-1";
        }
        else
        {
            ToastService.Push($"✘ {label} 추가 실패", ToastKind.Error);
        }
    }

    // ───── Section 6: 무공 (kungfuSkills) — Phase 5 ─────

    private const int PAGE_SIZE = 10;
    private int _kungfuPage = 0;
    private int _kungfuFilterType = -1;   // -1 = 전체, 0~8 = 9 카테고리 (cheat 검증)
    // v0.7.8 — 돌파속성 inline expand 제거, 별도 dialog 사용

    private void DrawKungfuSkillSection(object player)
    {
        var entries = Core.KungfuSkillEditor.GetEntries(player);
        GUILayout.Label($"  현재 {entries.Count}개 보유 (F=전투exp, B=비급exp — 합산으로 lv 결정)");

        // v0.7.8 — selector 선택 즉시 추가 (추가 버튼 삭제)
        GUILayout.BeginHorizontal();
        GUILayout.Label("  추가:", GUILayout.Width(60));
        if (GUILayout.Button("무공 선택 ▼", GUILayout.Width(220)))
        {
            // 이미 보유한 skillID set 만들기 (marker 용)
            var owned = new System.Collections.Generic.HashSet<int>();
            foreach (var entry in Core.KungfuSkillEditor.GetEntries(player)) owned.Add(entry.SkillID);

            _selector.Show("무공 선택", Core.SkillNameCache.AllOrderedEnriched(), selected =>
            {
                string label = Core.SkillNameCache.Get(selected);
                if (owned.Contains(selected))
                {
                    ToastService.Push($"이미 보유 — {label}", ToastKind.Info);
                    return;
                }
                bool ok = Core.KungfuSkillEditor.TryAddSkill(player, selected);
                ToastService.Push(ok ? $"✔ {label} 추가" : $"✘ {label} 추가 실패",
                    ok ? ToastKind.Success : ToastKind.Error);
            },
            BuildKungfuTabs(),
            width: 720f, height: 640f,
            secondaryTabs: Core.SkillNameCache.BuildRareLvTabs(),
            markedFn: sid => owned.Contains(sid),
            colorFn: sid => Core.SkillRareLvColors.ForSkillID(sid));
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(2);

        // 보유 무공 카테고리 필터 탭
        DrawKungfuFilterTabs();

        // 카테고리별 + 페이징 (10/page)
        var filtered = new List<(int SkillID, int Level, float FightExp, float BookExp, bool Equiped)>();
        foreach (var entry in entries)
        {
            if (_kungfuFilterType >= 0 && Core.SkillNameCache.GetType(entry.SkillID) != _kungfuFilterType) continue;
            filtered.Add(entry);
        }
        int totalPages = (filtered.Count + PAGE_SIZE - 1) / PAGE_SIZE;
        if (totalPages == 0) totalPages = 1;
        if (_kungfuPage >= totalPages) _kungfuPage = totalPages - 1;
        if (_kungfuPage < 0) _kungfuPage = 0;
        int start = _kungfuPage * PAGE_SIZE;
        int end = System.Math.Min(start + PAGE_SIZE, filtered.Count);

        // 페이지 nav
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30)) && _kungfuPage > 0) _kungfuPage--;
        GUILayout.Label($"  {_kungfuPage + 1} / {totalPages} 페이지 ({filtered.Count}개)", GUILayout.Width(180));
        if (GUILayout.Button("▶", GUILayout.Width(30)) && _kungfuPage < totalPages - 1) _kungfuPage++;
        GUILayout.EndHorizontal();

        // 현재 페이지의 entry 표시 — 2줄 row (정보 + 편집)
        for (int i = start; i < end; i++)
        {
            var (skillID, level, fightExp, bookExp, equiped) = filtered[i];
            string label    = Core.SkillNameCache.Get(skillID);
            string typeName = Core.SkillNameCache.GetTypeName(Core.SkillNameCache.GetType(skillID));
            int rareLv      = Core.SkillNameCache.GetRareLv(skillID);
            string rareName = Core.SkillNameCache.GetRareLvName(rareLv);
            int forceID     = Core.SkillNameCache.GetForceID(skillID);
            string forceNm  = Core.ForceNameCache.Get(forceID);
            string fKey = $"player.skill.{skillID}.fight";
            string bKey = $"player.skill.{skillID}.book";
            if (!_textBuf.ContainsKey(fKey)) _textBuf[fKey] = fightExp.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);
            if (!_textBuf.ContainsKey(bKey)) _textBuf[bKey] = bookExp.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

            // 1줄: 무공명 + 등급 + 문파 + lv + equiped (v0.7.8 — 등급별 색상)
            GUILayout.BeginHorizontal();
            string equipedMark = equiped ? "★" : " ";
            var prevSkillColor = GUI.color;
            GUI.color = Core.SkillRareLvColors.ForRareLv(rareLv);
            GUILayout.Label($"  {equipedMark}[{typeName}/{rareName}] {label} (lv{level} · {forceNm})");
            GUI.color = prevSkillColor;
            GUILayout.EndHorizontal();
            // 2줄: F/B + 버튼 + 돌파 expand 토글
            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label("F:", GUILayout.Width(15));
            _textBuf[fKey] = GUILayout.TextField(_textBuf[fKey], GUILayout.Width(60));
            GUILayout.Label("B:", GUILayout.Width(15));
            _textBuf[bKey] = GUILayout.TextField(_textBuf[bKey], GUILayout.Width(60));
            if (GUILayout.Button("수정", GUILayout.Width(45)))
            {
                ApplyKungfuEditExp(player, skillID, label, _textBuf[fKey], _textBuf[bKey], fKey, bKey);
            }
            if (GUILayout.Button("삭제", GUILayout.Width(45)))
            {
                ApplyKungfuRemove(player, skillID, label, fKey, bKey);
            }
            // v0.7.8 사용자 피드백 — inline expand → 별도 dialog
            if (GUILayout.Button("돌파…", GUILayout.Width(55)))
            {
                _breakthroughDialog.Show(player, skillID, label, _selector);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
    }

    /// <summary>
    /// v0.7.8 — 무공 돌파속성 sub-editor.
    /// 3 HeroSpeAddData wrapper (speEquipData / speUseData / extraAddData) — v0.7.7 reflector mirror
    /// 2 single value (equipUseSpeAddValue / damageUseSpeAddValue) — reflection setter
    /// </summary>
    private void DrawSkillBreakthrough(object player, int skillID, string skillLabel)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        var prevColor = GUI.color;
        GUI.color = new Color(0.6f, 0.9f, 1f, 1f);
        GUILayout.Label($"━━━ 돌파속성 ({skillLabel}) ━━━");
        GUI.color = prevColor;
        GUILayout.EndHorizontal();

        // 3 wrapper editor
        DrawSkillSubAddData(player, skillID, "speEquipData", "장착 시 (speEquipData)");
        DrawSkillSubAddData(player, skillID, "speUseData",   "사용 시 (speUseData)");
        DrawSkillSubAddData(player, skillID, "extraAddData", "영구 (extraAddData)");

        // 2 single value
        DrawSkillSingleValue(player, skillID, "equipUseSpeAddValue", "장착·사용 보정");
        DrawSkillSingleValue(player, skillID, "damageUseSpeAddValue", "데미지 보정");

        GUILayout.Space(2);
    }

    private void DrawSkillSubAddData(object player, int skillID, string fieldName, string sectionLabel)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUILayout.Label($"▷ {sectionLabel}");
        GUILayout.EndHorizontal();

        var addData = Core.KungfuSkillEditor.GetSubAddData(player, skillID, fieldName);
        if (addData == null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(40);
            GUILayout.Label("(부재)");
            GUILayout.EndHorizontal();
            return;
        }

        var entries = Core.HeroSpeAddDataReflector.GetEntries(addData);
        string keyPrefix = $"player.skill.{skillID}.{fieldName}";

        // 기존 entry rows
        foreach (var (entType, entVal) in entries)
        {
            string typeLabel = Core.SpeAddTypeNames.Get(entType);
            string tbKey = $"{keyPrefix}.{entType}";
            if (!_textBuf.ContainsKey(tbKey))
                _textBuf[tbKey] = entVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            GUILayout.BeginHorizontal();
            GUILayout.Space(40);
            GUILayout.Label($"{typeLabel}({entType}):", GUILayout.Width(120));
            _textBuf[tbKey] = GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(50));
            if (GUILayout.Button("수정", GUILayout.Width(45)))
                ApplySkillSubAddSet(player, skillID, fieldName, addData, entType, _textBuf[tbKey]);
            if (GUILayout.Button("삭제", GUILayout.Width(45)))
                ApplySkillSubAddRemove(player, skillID, fieldName, addData, entType, tbKey);
            GUILayout.EndHorizontal();
        }

        // 신규 추가 row
        string newIdxKey = $"{keyPrefix}.new.idx";
        string newValKey = $"{keyPrefix}.new.value";
        if (!_textBuf.ContainsKey(newIdxKey)) _textBuf[newIdxKey] = "0";
        if (!_textBuf.ContainsKey(newValKey)) _textBuf[newValKey] = "";

        GUILayout.BeginHorizontal();
        GUILayout.Space(40);
        GUILayout.Label("추가:", GUILayout.Width(50));
        int currentIdx = 0;
        int.TryParse(_textBuf[newIdxKey], out currentIdx);
        string currentLabel = Core.SpeAddTypeNames.Get(currentIdx);
        if (GUILayout.Button($"{currentLabel}({currentIdx})▼", GUILayout.Width(140)))
        {
            string capturedKey = newIdxKey;
            _selector.Show("속성 선택", Core.SpeAddTypeNames.AllOrdered(), selected =>
            {
                _textBuf[capturedKey] = selected.ToString();
            });
        }
        _textBuf[newValKey] = GUILayout.TextField(_textBuf[newValKey], GUILayout.Width(50));
        if (GUILayout.Button("추가", GUILayout.Width(45)))
            ApplySkillSubAddNew(player, skillID, fieldName, addData, _textBuf[newIdxKey], _textBuf[newValKey], newIdxKey, newValKey);
        GUILayout.EndHorizontal();
    }

    private void DrawSkillSingleValue(object player, int skillID, string fieldName, string sectionLabel)
    {
        string tbKey = $"player.skill.{skillID}.{fieldName}";
        float current = Core.KungfuSkillEditor.GetSingleValue(player, skillID, fieldName);
        if (!_textBuf.ContainsKey(tbKey))
            _textBuf[tbKey] = current.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        GUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUILayout.Label($"▷ {sectionLabel}:", GUILayout.Width(150));
        _textBuf[tbKey] = GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(60));
        if (GUILayout.Button("수정", GUILayout.Width(45)))
            ApplySkillSingleValue(player, skillID, fieldName, sectionLabel, _textBuf[tbKey]);
        GUILayout.EndHorizontal();
    }

    private void ApplySkillSubAddSet(object player, int skillID, string fieldName, object addData, int type, string input)
    {
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push("값 형식 잘못됨", ToastKind.Error);
            return;
        }
        bool ok = Core.HeroSpeAddDataReflector.TrySet(addData, type, val);
        if (ok)
            ToastService.Push($"✔ {Core.SpeAddTypeNames.Get(type)} = {val} 적용", ToastKind.Success);
        else
            ToastService.Push($"✘ 적용 실패", ToastKind.Error);
    }

    private void ApplySkillSubAddRemove(object player, int skillID, string fieldName, object addData, int type, string tbKey)
    {
        bool ok = Core.HeroSpeAddDataReflector.TryRemove(addData, type);
        if (ok)
        {
            ToastService.Push($"✔ {Core.SpeAddTypeNames.Get(type)} 삭제", ToastKind.Success);
            _textBuf.Remove(tbKey);
        }
        else ToastService.Push($"✘ 삭제 실패", ToastKind.Error);
    }

    private void ApplySkillSubAddNew(object player, int skillID, string fieldName, object addData, string idxInput, string valInput, string newIdxKey, string newValKey)
    {
        if (!int.TryParse(idxInput, out int typeIdx) || typeIdx < 0)
        {
            ToastService.Push("type idx 잘못됨", ToastKind.Error);
            return;
        }
        ApplySkillSubAddSet(player, skillID, fieldName, addData, typeIdx, valInput);
        _textBuf[newIdxKey] = "0";
        _textBuf[newValKey] = "";
    }

    private void ApplySkillSingleValue(object player, int skillID, string fieldName, string label, string input)
    {
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push("값 형식 잘못됨", ToastKind.Error);
            return;
        }
        bool ok = Core.KungfuSkillEditor.TrySetSingleValue(player, skillID, fieldName, val);
        if (ok)
            ToastService.Push($"✔ {label} = {val} 적용", ToastKind.Success);
        else
            ToastService.Push($"✘ {label} 적용 실패", ToastKind.Error);
    }

    private void DrawKungfuFilterTabs()
    {
        GUILayout.BeginHorizontal();
        bool active = _kungfuFilterType == -1;
        var prev = GUI.color;
        if (active) GUI.color = Color.cyan;
        if (GUILayout.Button("전체", GUILayout.Width(50))) { _kungfuFilterType = -1; _kungfuPage = 0; }
        GUI.color = prev;
        for (int t = 0; t < Core.SkillNameCache.TypeNames.Length; t++)
        {
            active = _kungfuFilterType == t;
            prev = GUI.color;
            if (active) GUI.color = Color.cyan;
            if (GUILayout.Button(Core.SkillNameCache.TypeNames[t], GUILayout.Width(45)))
            {
                _kungfuFilterType = t;
                _kungfuPage = 0;
            }
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>SelectorDialog 의 무공 카테고리 탭 — 9 type + 전체.</summary>
    private static IReadOnlyList<(string TabLabel, Func<int, bool> Filter)> BuildKungfuTabs()
    {
        var tabs = new List<(string, Func<int, bool>)>();
        tabs.Add(("전체", _ => true));
        for (int t = 0; t < Core.SkillNameCache.TypeNames.Length; t++)
        {
            int captured = t;
            tabs.Add((Core.SkillNameCache.TypeNames[t], skillID => Core.SkillNameCache.GetType(skillID) == captured));
        }
        return tabs;
    }

    private void ApplyKungfuAdd(object player, string idxInput, string newSkillKey)
    {
        if (!int.TryParse(idxInput, out int skillID) || skillID <= 0)
        {
            ToastService.Push("skill ID 가 잘못됨", ToastKind.Error);
            return;
        }
        string label = Core.SkillNameCache.Get(skillID);
        bool ok = Core.KungfuSkillEditor.TryAddSkill(player, skillID);
        if (ok)
        {
            ToastService.Push($"✔ {label} 추가", ToastKind.Success);
            _textBuf[newSkillKey] = "0";
        }
        else
        {
            ToastService.Push($"✘ {label} 추가 실패", ToastKind.Error);
        }
    }

    private void ApplyKungfuEditExp(object player, int skillID, string label, string fInput, string bInput, string fKey, string bKey)
    {
        bool any = false;
        if (float.TryParse(fInput, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fightVal))
        {
            if (Core.KungfuSkillEditor.TrySetExp(player, skillID, "fightExp", fightVal)) any = true;
        }
        if (float.TryParse(bInput, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float bookVal))
        {
            if (Core.KungfuSkillEditor.TrySetExp(player, skillID, "bookExp", bookVal)) any = true;
        }
        if (any)
        {
            ToastService.Push($"✔ {label} exp 적용", ToastKind.Success);
            _textBuf.Remove(fKey);
            _textBuf.Remove(bKey);
        }
        else
        {
            ToastService.Push($"✘ {label} exp 적용 실패", ToastKind.Error);
        }
    }

    private void ApplyKungfuRemove(object player, int skillID, string label, string fKey, string bKey)
    {
        bool ok = Core.KungfuSkillEditor.TryRemoveSkill(player, skillID);
        if (ok)
        {
            ToastService.Push($"✔ {label} 삭제", ToastKind.Success);
            _textBuf.Remove(fKey);
            _textBuf.Remove(bKey);
        }
        else
        {
            ToastService.Push($"✘ {label} 삭제 실패", ToastKind.Error);
        }
    }

    // ───── helpers ─────

    private static object? ReadFieldOrPropertyRaw(object obj, string name)
    {
        if (obj == null) return null;
        var t = obj.GetType();
        var f = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) { try { return f.GetValue(obj); } catch { return null; } }
        var p = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (p != null) { try { return p.GetValue(obj); } catch { return null; } }
        return null;
    }

    private static int ReadInt(object obj, string name)
    {
        try { var v = ReadFieldOrPropertyRaw(obj, name); return v == null ? 0 : Convert.ToInt32(v); }
        catch { return 0; }
    }
    private static float ReadFloat(object obj, string name)
    {
        try { var v = ReadFieldOrPropertyRaw(obj, name); return v == null ? 0f : Convert.ToSingle(v); }
        catch { return 0f; }
    }
    private static string ReadString(object obj, string name)
    {
        try { var v = ReadFieldOrPropertyRaw(obj, name); return v?.ToString() ?? ""; }
        catch { return ""; }
    }
}
