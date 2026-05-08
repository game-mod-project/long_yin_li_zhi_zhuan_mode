using System;
using System.Collections.Generic;
using LongYinRoster.Containers;
using LongYinRoster.Core;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.4 D-1 — focus 된 item 의 상세 정보 표시 non-modal window.
/// ContainerPanel 의 ⓘ 토글로 Visible 제어. F11 (ContainerPanel close) 시 sync 닫힘 (caller 책임).
/// 매 frame focus item reflection — Apply / 이동·복사 후 stale 자동 회피.
///
/// strip-safe IMGUI 패턴만 (v0.7.3 검증된 것):
/// GUI.DrawTexture / GUI.Label(Rect) / GUI.Button(Rect) / GUI.color / GUI.Window / GUI.DragWindow
/// GUILayout.Label(string, options) / GUILayout.Button / GUILayout.BeginScrollView / GUILayout.Space
/// </summary>
public sealed class ItemDetailPanel
{
    public bool Visible { get; set; } = false;
    private Rect _rect = new Rect(820, 100, 380, 500);
    private const int WindowID = 0x4C593734;   // "LY74"
    private bool _rawExpanded = false;
    private Vector2 _scroll = Vector2.zero;
    private ContainerPanel? _hostPanel;

    public void Init(ContainerPanel host, float defaultX, float defaultY, float defaultWidth, float defaultHeight)
    {
        _hostPanel = host;
        _rect = new Rect(defaultX, defaultY, defaultWidth, defaultHeight);
    }

    public Rect WindowRect => _rect;

    public void OnGUI()
    {
        if (!Visible) return;
        try
        {
            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        }
        catch (System.Exception ex)
        {
            Util.Logger.Warn($"ItemDetailPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "Item 상세");
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
                Visible = false;
            GUILayout.Space(DialogStyle.HeaderHeight);

            var raw = _hostPanel?.GetFocusedRawItem();
            if (raw == null) DrawEmpty();
            else DrawDetails(raw);

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (System.Exception ex)
        {
            Util.Logger.Warn($"ItemDetailPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
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

    private void DrawDetails(object raw)
    {
        // 1. header — focused item 의 이름 + 등급 색상
        string name = HangulDict.Translate(ItemReflector.GetNameRaw(raw));
        int grade = ItemReflector.GetGradeOrder(raw);
        var prevColor = GUI.color;
        GUI.color = ItemCellRenderer.GradeColor(grade);
        GUILayout.Label($"  {name}");
        GUI.color = prevColor;
        GUILayout.Space(4);

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 110));

        // 2. curated 섹션
        var curated = ItemDetailReflector.GetCuratedFields(raw);
        if (curated.Count > 0)
        {
            GUILayout.Label("== 정보 ==");
            foreach (var (label, value) in curated)
                GUILayout.Label($"  {label}: {HangulDict.Translate(value)}");
            GUILayout.Space(8);
        }

        // 3. raw fields (접이식)
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
}
