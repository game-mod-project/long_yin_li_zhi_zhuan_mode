using System;
using System.Collections.Generic;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;   // UnityEngine.Logger 모호성 회피

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.7 — modal popup selector. (int Value, string Label) list + 검색 box + scrollable.
/// 사용: 등급 (rareLv) / 품질 (itemLv) / SpeAddType 134 entries 선택.
/// strip-safe IMGUI 패턴만 사용 (v0.7.6 검증된 것 + 신규 없음).
/// </summary>
public sealed class SelectorDialog
{
    public bool Visible { get; private set; }
    public Rect WindowRect => _rect;

    private Rect _rect = new(300, 200, 360, 480);
    private const int WindowID = 0x4C593735;   // "LY75"

    private string _title = "";
    private List<(int Value, string Label)> _items = new();
    private Action<int>? _onSelect;
    private Vector2 _scroll = Vector2.zero;
    private string _searchText = "";

    // v0.7.8 — 카테고리 탭 (optional). null = 탭 없음.
    private IReadOnlyList<(string TabLabel, Func<int, bool> Filter)>? _tabs;
    private int _selectedTab = 0;

    // v0.7.8 — 2단계 secondary tabs (등급 등). null = 단일 탭. 두 탭의 filter 는 AND 연결.
    private IReadOnlyList<(string TabLabel, Func<int, bool> Filter)>? _secondaryTabs;
    private int _selectedSecondaryTab = 0;

    // v0.7.8 — 이미 보유 marker — entry 라벨에 "✓" prefix + cyan 표시
    private Func<int, bool>? _markedFn;

    // v0.7.8 — entry 별 색상 (천부 점수, 무공 등급 등)
    private Func<int, Color>? _colorFn;

    public void Show(string title, IEnumerable<(int Value, string Label)> items, Action<int> onSelect,
        IReadOnlyList<(string TabLabel, Func<int, bool> Filter)>? tabs = null,
        float width = 360f, float height = 480f,
        IReadOnlyList<(string TabLabel, Func<int, bool> Filter)>? secondaryTabs = null,
        Func<int, bool>? markedFn = null,
        Func<int, Color>? colorFn = null)
    {
        _title = title;
        _items = new List<(int Value, string Label)>(items);
        _onSelect = onSelect;
        _searchText = "";
        _scroll = Vector2.zero;
        _tabs = tabs;
        _selectedTab = 0;
        _secondaryTabs = secondaryTabs;
        _selectedSecondaryTab = 0;
        _markedFn = markedFn;
        _colorFn = colorFn;
        _rect = new Rect(_rect.x, _rect.y, width, height);
        Visible = true;
    }

    public void Hide()
    {
        Visible = false;
        _onSelect = null;
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
            Logger.WarnOnce("SelectorDialog", $"SelectorDialog.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, _title);

            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
            {
                Hide();
                return;
            }

            GUILayout.Space(DialogStyle.HeaderHeight);

            // v0.7.8 — 카테고리 탭 (optional)
            if (_tabs != null && _tabs.Count > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < _tabs.Count; i++)
                {
                    bool active = i == _selectedTab;
                    var prevColor = GUI.color;
                    if (active) GUI.color = Color.cyan;
                    if (GUILayout.Button(_tabs[i].TabLabel, GUILayout.Width(60)))
                    {
                        _selectedTab = i;
                        _scroll = Vector2.zero;
                    }
                    GUI.color = prevColor;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            // v0.7.8 — secondary tabs (등급 등)
            if (_secondaryTabs != null && _secondaryTabs.Count > 0)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < _secondaryTabs.Count; i++)
                {
                    bool active = i == _selectedSecondaryTab;
                    var prevColor = GUI.color;
                    if (active) GUI.color = new Color(1f, 0.85f, 0.4f, 1f);
                    if (GUILayout.Button(_secondaryTabs[i].TabLabel, GUILayout.Width(55)))
                    {
                        _selectedSecondaryTab = i;
                        _scroll = Vector2.zero;
                    }
                    GUI.color = prevColor;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            // 검색 box
            GUILayout.BeginHorizontal();
            GUILayout.Label("검색:", GUILayout.Width(50));
            _searchText = GUILayout.TextField(_searchText ?? "", GUILayout.Width(_rect.width - 80));
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Filtered list (탭 + secondary tab + 검색 — AND 연결)
            string lower = (_searchText ?? "").Trim().ToLowerInvariant();
            Func<int, bool>? tabFilter = (_tabs != null && _selectedTab < _tabs.Count) ? _tabs[_selectedTab].Filter : null;
            Func<int, bool>? secondaryFilter = (_secondaryTabs != null && _selectedSecondaryTab < _secondaryTabs.Count) ? _secondaryTabs[_selectedSecondaryTab].Filter : null;

            float listH = _rect.height - 100
                - (_tabs != null ? 30 : 0)
                - (_secondaryTabs != null ? 30 : 0);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(listH));
            foreach (var (val, label) in _items)
            {
                if (tabFilter != null && !tabFilter(val)) continue;
                if (secondaryFilter != null && !secondaryFilter(val)) continue;
                bool match = string.IsNullOrEmpty(lower)
                    || label.ToLowerInvariant().Contains(lower)
                    || val.ToString().Contains(lower);
                if (!match) continue;
                bool owned = _markedFn != null && _markedFn(val);
                string prefix = owned ? "✓ " : "  ";
                var prevColor = GUI.color;
                // v0.7.8 — colorFn 우선, marker 는 prefix 만 (등급/점수 색상 보존)
                if (_colorFn != null) GUI.color = _colorFn(val);
                else if (owned)       GUI.color = new Color(0.6f, 0.9f, 1f, 1f);
                if (GUILayout.Button($"{prefix}{val,3}: {label}"))
                {
                    _onSelect?.Invoke(val);
                    Hide();
                }
                GUI.color = prevColor;
            }
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("SelectorDialog", $"SelectorDialog.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
