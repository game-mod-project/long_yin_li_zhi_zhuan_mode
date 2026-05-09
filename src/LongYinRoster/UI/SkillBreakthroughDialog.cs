using System;
using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.8 — 무공 돌파속성 (sub-data HeroSpeAddData × 3 + Single × 2) 편집 modal popup.
/// PlayerEditorPanel 의 [돌파] 버튼 클릭 시 open. 단일 skill 의 5 sub-section 표시.
/// PlayerEditorPanel 인 inline expand 가 panel 스크롤 길게 만드는 문제를 별도 dialog 로 해결.
///
/// 사용:
///   _breakthroughDialog.Show(player, skillID, skillLabel, _selector);
/// </summary>
public sealed class SkillBreakthroughDialog
{
    public bool Visible { get; private set; }
    public Rect WindowRect => _rect;

    private Rect _rect = new(280, 100, 600, 660);
    private const int WindowID = 0x4C593737;   // "LY77"

    private object? _player;
    private int _skillID = -1;
    private string _skillLabel = "";
    private SelectorDialog? _selector;
    private Vector2 _scroll = Vector2.zero;
    private readonly Dictionary<string, string> _textBuf = new();

    public void Show(object player, int skillID, string skillLabel, SelectorDialog selector)
    {
        _player = player;
        _skillID = skillID;
        _skillLabel = skillLabel;
        _selector = selector;
        _scroll = Vector2.zero;
        _textBuf.Clear();
        Visible = true;
    }

    public void Hide()
    {
        Visible = false;
        _player = null;
        _skillID = -1;
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
            Logger.WarnOnce("SkillBreakthroughDialog", $"SkillBreakthroughDialog.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, $"돌파속성 — {_skillLabel}");

            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
            {
                Hide();
                return;
            }

            GUILayout.Space(DialogStyle.HeaderHeight);

            if (_player == null || _skillID < 0)
            {
                GUILayout.Label("  (player 또는 skill 미설정)");
                GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 70));

            // 3 HeroSpeAddData wrapper (v0.7.7 패턴)
            DrawSubAddDataSection(_player, _skillID, "speEquipData", "장착 시 (speEquipData)");
            GUILayout.Space(6);
            DrawSubAddDataSection(_player, _skillID, "speUseData",   "사용 시 (speUseData)");
            GUILayout.Space(6);
            DrawSubAddDataSection(_player, _skillID, "extraAddData", "영구 (extraAddData)");
            GUILayout.Space(8);

            // 2 Single value
            DrawSingleValueRow(_player, _skillID, "equipUseSpeAddValue", "장착·사용 보정");
            DrawSingleValueRow(_player, _skillID, "damageUseSpeAddValue", "데미지 보정");

            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("SkillBreakthroughDialog", $"SkillBreakthroughDialog.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void DrawSubAddDataSection(object player, int skillID, string fieldName, string sectionLabel)
    {
        var prevColor = GUI.color;
        GUI.color = new Color(0.6f, 0.9f, 1f, 1f);
        GUILayout.Label($"  ▷ {sectionLabel}");
        GUI.color = prevColor;

        var addData = KungfuSkillEditor.GetSubAddData(player, skillID, fieldName);
        if (addData == null)
        {
            GUILayout.Label("    (부재)");
            return;
        }

        var entries = HeroSpeAddDataReflector.GetEntries(addData);
        string keyPrefix = $"{skillID}.{fieldName}";

        foreach (var (entType, entVal) in entries)
        {
            string typeLabel = SpeAddTypeNames.Get(entType);
            string tbKey = $"{keyPrefix}.{entType}";
            if (!_textBuf.ContainsKey(tbKey))
                _textBuf[tbKey] = entVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20);
            GUILayout.Label($"{typeLabel}({entType}):", GUILayout.Width(140));
            _textBuf[tbKey] = GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(70));
            if (GUILayout.Button("수정", GUILayout.Width(50)))
                ApplySet(addData, entType, _textBuf[tbKey]);
            if (GUILayout.Button("삭제", GUILayout.Width(50)))
                ApplyRemove(addData, entType, tbKey);
            GUILayout.EndHorizontal();
        }

        // 신규 추가 row
        string newIdxKey = $"{keyPrefix}.new.idx";
        string newValKey = $"{keyPrefix}.new.value";
        if (!_textBuf.ContainsKey(newIdxKey)) _textBuf[newIdxKey] = "0";
        if (!_textBuf.ContainsKey(newValKey)) _textBuf[newValKey] = "";

        GUILayout.BeginHorizontal();
        GUILayout.Space(20);
        GUILayout.Label("추가:", GUILayout.Width(50));
        int currentIdx = 0;
        int.TryParse(_textBuf[newIdxKey], out currentIdx);
        string currentLabel = SpeAddTypeNames.Get(currentIdx);
        if (GUILayout.Button($"{currentLabel}({currentIdx})▼", GUILayout.Width(180)))
        {
            string capturedKey = newIdxKey;
            _selector?.Show("속성 선택", SpeAddTypeNames.AllOrdered(), selected =>
            {
                _textBuf[capturedKey] = selected.ToString();
            }, width: 380f, height: 480f);
        }
        _textBuf[newValKey] = GUILayout.TextField(_textBuf[newValKey], GUILayout.Width(70));
        if (GUILayout.Button("추가", GUILayout.Width(50)))
            ApplyAddNew(addData, _textBuf[newIdxKey], _textBuf[newValKey], newIdxKey, newValKey);
        GUILayout.EndHorizontal();
    }

    private void DrawSingleValueRow(object player, int skillID, string fieldName, string label)
    {
        string tbKey = $"{skillID}.{fieldName}";
        float current = KungfuSkillEditor.GetSingleValue(player, skillID, fieldName);
        if (!_textBuf.ContainsKey(tbKey))
            _textBuf[tbKey] = current.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        GUILayout.BeginHorizontal();
        GUILayout.Space(10);
        GUILayout.Label($"▷ {label}:", GUILayout.Width(180));
        _textBuf[tbKey] = GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(80));
        if (GUILayout.Button("수정", GUILayout.Width(50)))
            ApplySingleValue(player, skillID, fieldName, label, _textBuf[tbKey]);
        GUILayout.EndHorizontal();
    }

    private void ApplySet(object addData, int type, string input)
    {
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push("값 형식 잘못됨", ToastKind.Error);
            return;
        }
        bool ok = HeroSpeAddDataReflector.TrySet(addData, type, val);
        if (ok) ToastService.Push($"✔ {SpeAddTypeNames.Get(type)} = {val} 적용", ToastKind.Success);
        else ToastService.Push("✘ 적용 실패", ToastKind.Error);
    }

    private void ApplyRemove(object addData, int type, string tbKey)
    {
        bool ok = HeroSpeAddDataReflector.TryRemove(addData, type);
        if (ok)
        {
            ToastService.Push($"✔ {SpeAddTypeNames.Get(type)} 삭제", ToastKind.Success);
            _textBuf.Remove(tbKey);
        }
        else ToastService.Push("✘ 삭제 실패", ToastKind.Error);
    }

    private void ApplyAddNew(object addData, string idxInput, string valInput, string newIdxKey, string newValKey)
    {
        if (!int.TryParse(idxInput, out int typeIdx) || typeIdx < 0)
        {
            ToastService.Push("type idx 잘못됨", ToastKind.Error);
            return;
        }
        ApplySet(addData, typeIdx, valInput);
        _textBuf[newIdxKey] = "0";
        _textBuf[newValKey] = "";
    }

    private void ApplySingleValue(object player, int skillID, string fieldName, string label, string input)
    {
        if (!float.TryParse(input, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
        {
            ToastService.Push("값 형식 잘못됨", ToastKind.Error);
            return;
        }
        bool ok = KungfuSkillEditor.TrySetSingleValue(player, skillID, fieldName, val);
        if (ok) ToastService.Push($"✔ {label} = {val} 적용", ToastKind.Success);
        else ToastService.Push($"✘ {label} 적용 실패", ToastKind.Error);
    }
}
