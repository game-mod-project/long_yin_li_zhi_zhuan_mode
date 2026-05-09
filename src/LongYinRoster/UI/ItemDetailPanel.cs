using System;
using System.Collections.Generic;
using LongYinRoster.Containers;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.4 D-1 — focus 된 item 의 상세 정보 표시 non-modal window.
/// v0.7.7 — [편집] mode 토글 + curated 행마다 textfield + [적용] 버튼.
/// 인벤·창고 area 만 edit 가능. 외부 컨테이너 area 시 토글 disabled.
///
/// strip-safe IMGUI 패턴만 (v0.7.3+v0.7.6 검증):
/// GUI.DrawTexture / GUI.Label(Rect) / GUI.Button(Rect) / GUI.color / GUI.Window / GUI.DragWindow / GUI.enabled
/// GUILayout.Label/Button/TextField(string, options) / GUILayout.BeginScrollView / GUILayout.Space
/// </summary>
public sealed class ItemDetailPanel
{
    public bool Visible { get; set; } = false;
    private Rect _rect = new Rect(820, 100, 380, 500);
    private const int WindowID = 0x4C593734;   // "LY74"
    private bool _rawExpanded = false;
    private Vector2 _scroll = Vector2.zero;
    private ContainerPanel? _hostPanel;

    // v0.7.7 — edit mode 상태 + textfield buffer (path → text)
    private bool _editMode = false;
    private readonly Dictionary<string, string> _textBuf = new();
    private object? _lastFocusedRawRef;

    // v0.7.7 — selector popup (등급/품질/SpeAddType 통합)
    private readonly SelectorDialog _selector = new();
    public SelectorDialog Selector => _selector;

    /// <summary>ModWindow 가 wire — Apply 후 ContainerPanel row 갱신 트리거.</summary>
    public Action? OnAppliedRefreshRequest;

    /// <summary>ModWindow 가 wire — HeroLocator.GetPlayer() 결과 제공 (test 환경 decouple 용).</summary>
    public Func<object?>? GetPlayer;

    public void Init(ContainerPanel host, float defaultX, float defaultY, float defaultWidth, float defaultHeight)
    {
        _hostPanel = host;
        // v0.7.7 — stat editor / selector 표시 위해 최소 480×640 보장 (기존 사용자 cfg auto-bump)
        float w = Math.Max(defaultWidth, 480f);
        float h = Math.Max(defaultHeight, 640f);
        _rect = new Rect(defaultX, defaultY, w, h);
    }

    public Rect WindowRect => _rect;

    public void OnGUI()
    {
        if (!Visible) return;
        try
        {
            // Focus 변경 감지 → textfield buffer reset (v0.7.4 D-1 stale focus 패턴 mirror)
            var current = _hostPanel?.GetFocusedRawItem();
            if (!ReferenceEquals(current, _lastFocusedRawRef))
            {
                _textBuf.Clear();
                _lastFocusedRawRef = current;
            }

            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        }
        catch (Exception ex)
        {
            Util.Logger.WarnOnce("ItemDetailPanel", $"ItemDetailPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }

        // v0.7.7 — modal selector popup (등급/품질/SpeAddType 통합)
        _selector.OnGUI();
    }

    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "Item 상세");

            // v0.7.7 — [편집] 토글 (X 버튼 옆) — 외부 컨테이너 area 시 disabled
            bool isExternalContainer = _hostPanel?.Focus?.Area == ContainerArea.Container;
            var prevEnabled = GUI.enabled;
            GUI.enabled = !isExternalContainer;
            var prevColor = GUI.color;
            if (_editMode && !isExternalContainer) GUI.color = Color.cyan;
            if (GUI.Button(new Rect(_rect.width - 76, 4, 44, 20), KoreanStrings.EditModeBtn))
            {
                _editMode = !_editMode;
                _textBuf.Clear();
            }
            GUI.color = prevColor;
            GUI.enabled = prevEnabled;

            // X 닫기
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
                Visible = false;

            GUILayout.Space(DialogStyle.HeaderHeight);

            var raw = _hostPanel?.GetFocusedRawItem();
            if (raw == null) DrawEmpty();
            else DrawDetails(raw, isExternalContainer);

            GUI.DragWindow(new Rect(0, 0, _rect.width - 80, DialogStyle.HeaderHeight));
        }
        catch (Exception ex)
        {
            Util.Logger.WarnOnce("ItemDetailPanel", $"ItemDetailPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void DrawEmpty()
    {
        GUILayout.Space(60);
        GUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUILayout.Label("item 의 cell 을 클릭하세요");
        GUILayout.EndHorizontal();
    }

    private void DrawDetails(object raw, bool isExternalContainer)
    {
        // 1. header — focused item 의 이름 + 등급 색상
        string name = HangulDict.Translate(ItemReflector.GetNameRaw(raw));
        int grade = ItemReflector.GetGradeOrder(raw);
        var prevColor = GUI.color;
        GUI.color = ItemCellRenderer.GradeColor(grade);
        GUILayout.Label($"  {name}");
        GUI.color = prevColor;
        GUILayout.Space(4);

        // v0.7.7 — disclaimer (edit mode 활성 + 인벤·창고 area)
        if (_editMode && !isExternalContainer)
        {
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.5f, 1f);
            GUILayout.Label(KoreanStrings.EditDisclaimer);
            GUI.color = prev;
        }
        if (_editMode && isExternalContainer)
        {
            // 강제 off — 외부 컨테이너 시
            _editMode = false;
            ToastService.Push(KoreanStrings.EditModeContainerOnly, ToastKind.Info);
        }

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 140));

        // 2. 정보 섹션
        if (_editMode && !isExternalContainer)
        {
            // edit mode: ItemEditFieldMatrix 매트릭스 직접 렌더 (curated conditional 무관, 모든 edit-able 필드 항상 표시)
            int type = ReadIntField(raw, "type");
            var editFields = ItemEditFieldMatrix.ForCategory(type);
            if (editFields.Count == 0)
            {
                GUILayout.Label(KoreanStrings.EditFieldNotFoundForCategory);
            }
            else
            {
                GUILayout.Label("== 편집 (모든 값 직접 입력) ==");
                foreach (var ef in editFields)
                {
                    DrawEditRow(raw, ef);
                }
                // 추가 read-only 정보 — 무게/value 는 CountValueAndWeight 로 자동 재계산되므로 derived 표시
                GUILayout.Space(4);
                GUILayout.Label($"  무게(자동): {ReadFloatField(raw, "weight"):F1} kg");
            }
            GUILayout.Space(8);

            // v0.7.7 — HeroSpeAddData 편집 (속성/값 add/edit/delete)
            // type=0 Equipment: equipmentData.baseAddData + equipmentData.extraAddData
            // type=2 MedFood:  medFoodData.extraAddData
            // type=5 Material: materialData.extraAddData
            switch (type)
            {
                case 0:
                    DrawHeroSpeAddDataSection(raw, "equipmentData", "baseAddData",  KoreanStrings.StatEditSection_Base);
                    DrawHeroSpeAddDataSection(raw, "equipmentData", "extraAddData", KoreanStrings.StatEditSection_Extra);
                    break;
                case 2:
                    DrawHeroSpeAddDataSection(raw, "medFoodData", "extraAddData", KoreanStrings.StatEditSection_Extra);
                    break;
                case 5:
                    DrawHeroSpeAddDataSection(raw, "materialData", "extraAddData", KoreanStrings.StatEditSection_Extra);
                    break;
            }
        }
        else
        {
            // view mode: 기존 curated (rareLv/itemLv 미포함 — v0.7.4.1 의 conditional spec)
            var curated = ItemDetailReflector.GetCuratedFields(raw);
            if (curated.Count > 0)
            {
                GUILayout.Label("== 정보 ==");
                foreach (var (label, value) in curated)
                    GUILayout.Label($"  {label}: {HangulDict.Translate(value)}");
                GUILayout.Space(8);
            }
        }

        // 3. raw fields (접이식, 항상 read-only)
        var rawFields = ItemDetailReflector.GetRawFields(raw);
        var arrow = _rawExpanded ? "▼" : "▶";
        if (GUILayout.Button($"{arrow} Raw fields ({rawFields.Count})"))
            _rawExpanded = !_rawExpanded;
        if (_rawExpanded)
        {
            foreach (var (fname, value) in rawFields)
                GUILayout.Label($"  {fname}: {HangulDict.Translate(value)}");
        }

        GUILayout.EndScrollView();
    }

    /// <summary>v0.7.7 edit-mode row — selector(itemLv/rareLv) 또는 textfield + [적용] 버튼.</summary>
    private void DrawEditRow(object raw, ItemEditField ef)
    {
        // 등급(itemLv) / 품질(rareLv) 은 SelectorDialog 사용 — 6 enum 한글 라벨
        if (ef.Path == "itemLv")
        {
            DrawSelectorRow(raw, ef, Core.ItemRareLvNames.GetEquipLv, "등급 선택", Core.ItemRareLvNames.EquipLvOptions());
            return;
        }
        if (ef.Path == "rareLv")
        {
            DrawSelectorRow(raw, ef, Core.ItemRareLvNames.GetQuality, "품질 선택", Core.ItemRareLvNames.QualityOptions());
            return;
        }

        // 기존 textfield + [적용] 패턴
        if (!_textBuf.ContainsKey(ef.Path))
            _textBuf[ef.Path] = ReadFieldValueAsText(raw, ef);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {ef.KrLabel}: ", GUILayout.Width(140));
        _textBuf[ef.Path] = GUILayout.TextField(_textBuf[ef.Path], GUILayout.Width(80));
        if (GUILayout.Button(KoreanStrings.EditApplyBtn, GUILayout.Width(50)))
        {
            ApplyField(raw, ef, _textBuf[ef.Path]);
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>itemLv/rareLv 같은 enum 필드 — 현재 값 표시 버튼 → 클릭 시 SelectorDialog.</summary>
    private void DrawSelectorRow(
        object raw, ItemEditField ef,
        Func<int, string> labelFor, string dialogTitle,
        IReadOnlyList<(int Value, string Label)> options)
    {
        // 현재 값 read (reflection)
        int currentVal = 0;
        var rawText = ReadFieldValueAsText(raw, ef);
        int.TryParse(rawText, out currentVal);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {ef.KrLabel}: ", GUILayout.Width(140));
        string display = $"{labelFor(currentVal)}({currentVal})";
        if (GUILayout.Button($"{display} ▼", GUILayout.Width(140)))
        {
            _selector.Show(dialogTitle, options, selected =>
            {
                ApplyField(raw, ef, selected.ToString());
            });
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>dot path 따라 reflection 으로 현재 값 read → textfield 초기 string. bool/float 포맷 처리.</summary>
    private static string ReadFieldValueAsText(object item, ItemEditField field)
    {
        var segments = field.Path.Split('.');
        object cursor = item;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var sub = ReadFieldOrPropertyRaw(cursor, segments[i]);
            if (sub == null) return "";
            cursor = sub;
        }
        var leaf = segments[segments.Length - 1];
        var val = ReadFieldOrPropertyRaw(cursor, leaf);
        if (val == null) return "";
        if (val is bool b) return b ? "true" : "false";
        if (val is float f) return f.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        return val.ToString() ?? "";
    }

    private static object? ReadFieldOrPropertyRaw(object obj, string name)
    {
        var t = obj.GetType();
        var f = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) return f.GetValue(obj);
        var p = t.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (p != null) return p.GetValue(obj);
        return null;
    }

    private void ApplyField(object raw, ItemEditField field, string input)
    {
        if (!field.TryParse(input, out object value, out string error))
        {
            ToastService.Push(string.Format(KoreanStrings.EditFieldParseError, field.KrLabel, error), ToastKind.Error);
            return;
        }

        var player = GetPlayer?.Invoke();
        var r = ItemEditApplier.Apply(raw, field, value, player);
        if (r.Success)
        {
            ToastService.Push(string.Format(KoreanStrings.EditApplyOk, field.KrLabel, value), ToastKind.Success);
            _textBuf.Remove(field.Path);   // 다음 frame curated 재렌더 시 새 값 자동 반영
            OnAppliedRefreshRequest?.Invoke();
        }
        else
        {
            ToastService.Push(string.Format(KoreanStrings.EditApplyFailed, field.KrLabel, r.Error ?? "?"), ToastKind.Error);
        }
    }

    /// <summary>
    /// v0.7.7 — HeroSpeAddData 편집 섹션. 현재 entry 매트릭스 + 신규 entry 추가 row.
    /// subDataName: "equipmentData" / "medFoodData" / "materialData"
    /// addDataName: "baseAddData" / "extraAddData"
    /// </summary>
    private void DrawHeroSpeAddDataSection(object raw, string subDataName, string addDataName, string sectionLabel)
    {
        var subData = ReadFieldOrPropertyRaw(raw, subDataName);
        if (subData == null) return;
        var addData = ReadFieldOrPropertyRaw(subData, addDataName);
        if (addData == null) return;

        GUILayout.Space(4);
        GUILayout.Label($"== {sectionLabel} ==");

        var entries = Core.HeroSpeAddDataReflector.GetEntries(addData);
        string keyPrefix = $"{subDataName}.{addDataName}";

        // 기존 entry rows
        foreach (var (entType, entVal) in entries)
        {
            string label = Core.SpeAddTypeNames.Get(entType);
            string tbKey = $"{keyPrefix}.{entType}";
            if (!_textBuf.ContainsKey(tbKey))
                _textBuf[tbKey] = entVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {label}({entType}):", GUILayout.Width(110));
            _textBuf[tbKey] = GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(60));
            if (GUILayout.Button(KoreanStrings.StatEditEditBtn, GUILayout.Width(45)))
            {
                ApplyStatEditMutation(raw, addData, entType, _textBuf[tbKey], isAdd: false);
            }
            if (GUILayout.Button(KoreanStrings.StatEditDeleteBtn, GUILayout.Width(45)))
            {
                ApplyStatEditDelete(raw, addData, entType, keyPrefix);
            }
            GUILayout.EndHorizontal();
        }

        // 신규 entry 추가 row — selector popup 패턴 (v0.7.7 사용자 피드백)
        string newIdxKey = $"{keyPrefix}.new.idx";
        string newValKey = $"{keyPrefix}.new.value";
        if (!_textBuf.ContainsKey(newIdxKey)) _textBuf[newIdxKey] = "0";
        if (!_textBuf.ContainsKey(newValKey)) _textBuf[newValKey] = "";

        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {KoreanStrings.StatEditAddRowLabel}", GUILayout.Width(60));
        int currentIdx = 0;
        int.TryParse(_textBuf[newIdxKey], out currentIdx);
        string currentLabel = Core.SpeAddTypeNames.Get(currentIdx);
        if (GUILayout.Button($"{currentLabel}({currentIdx}) ▼", GUILayout.Width(160)))
        {
            // raw / addData / keyPrefix capture by closure
            string capturedNewIdxKey = newIdxKey;
            _selector.Show("속성 선택", Core.SpeAddTypeNames.AllOrdered(), selected =>
            {
                _textBuf[capturedNewIdxKey] = selected.ToString();
            });
        }
        _textBuf[newValKey] = GUILayout.TextField(_textBuf[newValKey], GUILayout.Width(60));
        if (GUILayout.Button(KoreanStrings.StatEditAddBtn, GUILayout.Width(45)))
        {
            ApplyStatEditAdd(raw, addData, _textBuf[newIdxKey], _textBuf[newValKey], newIdxKey, newValKey);
        }
        GUILayout.EndHorizontal();
    }

    private void ApplyStatEditMutation(object raw, object addData, int type, string input, bool isAdd)
    {
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push(KoreanStrings.StatEditValueInvalid, ToastKind.Error);
            return;
        }
        string label = Core.SpeAddTypeNames.Get(type);
        bool ok = Core.HeroSpeAddDataReflector.TrySet(addData, type, val);
        if (ok)
        {
            var player = GetPlayer?.Invoke();
            Core.ItemEditApplier.PostMutationRefresh(raw, player);
            string fmt = isAdd ? KoreanStrings.StatEditAddOk : KoreanStrings.StatEditApplyOk;
            ToastService.Push(string.Format(fmt, label, val), ToastKind.Success);
            OnAppliedRefreshRequest?.Invoke();
        }
        else
        {
            ToastService.Push(string.Format(KoreanStrings.StatEditFailed, label), ToastKind.Error);
        }
    }

    private void ApplyStatEditDelete(object raw, object addData, int type, string keyPrefix)
    {
        string label = Core.SpeAddTypeNames.Get(type);
        bool ok = Core.HeroSpeAddDataReflector.TryRemove(addData, type);
        if (ok)
        {
            _textBuf.Remove($"{keyPrefix}.{type}");   // textfield buffer 제거
            var player = GetPlayer?.Invoke();
            Core.ItemEditApplier.PostMutationRefresh(raw, player);
            ToastService.Push(string.Format(KoreanStrings.StatEditDeleteOk, label), ToastKind.Success);
            OnAppliedRefreshRequest?.Invoke();
        }
        else
        {
            ToastService.Push(string.Format(KoreanStrings.StatEditFailed, label), ToastKind.Error);
        }
    }

    private void ApplyStatEditAdd(object raw, object addData, string idxInput, string valInput, string newIdxKey, string newValKey)
    {
        if (!int.TryParse(idxInput, out int typeIdx) || typeIdx < 0 || typeIdx > 99)
        {
            ToastService.Push(KoreanStrings.StatEditTypeIdxInvalid, ToastKind.Error);
            return;
        }
        ApplyStatEditMutation(raw, addData, typeIdx, valInput, isAdd: true);
        // 입력 reset
        _textBuf[newIdxKey] = "0";
        _textBuf[newValKey] = "";
    }

    private static int ReadIntField(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var f = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) return Convert.ToInt32(f.GetValue(obj));
        }
        catch { }
        return 0;
    }

    private static float ReadFloatField(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var f = t.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) return Convert.ToSingle(f.GetValue(obj));
        }
        catch { }
        return 0f;
    }
}
