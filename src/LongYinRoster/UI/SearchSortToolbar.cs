using LongYinRoster.Containers;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.2 — IMGUI 1줄 검색·정렬 toolbar.
/// [TextField (폭 = 호출자, 기본 200)] [카테고리][이름][등급][품질] [▲ 또는 ▼]
/// 호스트 (ContainerPanel) 가 state 보유 + 변경 시 ContainerView.Invalidate.
///
/// IL2CPP strip 회피: GUILayout.TextField, GUILayout.Button(string), GUILayout.Label(string)
/// default skin 만 사용. GUIStyle 받는 overload 금지.
/// </summary>
public static class SearchSortToolbar
{
    /// <summary>
    /// state 를 in-place 가능 위치 — 입력 변경 시 새 SearchSortState 반환.
    /// 같은 frame 에서 반환값을 host state 에 할당.
    /// </summary>
    public static SearchSortState Draw(SearchSortState current, bool gradeQualityEnabled = true, float searchFieldW = 200f)
    {
        var result = current;
        GUILayout.BeginHorizontal();

        // 검색 box (폭 = searchFieldW — ContainerLayout.SearchFieldW)
        string newText = GUILayout.TextField(current.Search ?? "", GUILayout.Width(searchFieldW), GUILayout.Height(DialogStyle.RowHeight));
        if (!ReferenceEquals(newText, current.Search) && newText != current.Search)
            result = result.WithSearch(newText);

        GUILayout.Space(4);

        // 정렬 키 4 segmented
        result = DrawKeyButton(result, SortKey.Category, "카테고리", 70);
        result = DrawKeyButton(result, SortKey.Name,     "이름",     60);
        result = DrawKeyButton(result, SortKey.Grade,    "등급",     60, gradeQualityEnabled);
        result = DrawKeyButton(result, SortKey.Quality,  "품질",     60, gradeQualityEnabled);

        GUILayout.Space(4);

        // 방향 토글
        string arrow = result.Ascending ? "▲" : "▼";
        if (GUILayout.Button(arrow, GUILayout.Width(32), GUILayout.Height(DialogStyle.RowHeight)))
            result = result.ToggleDirection();

        GUILayout.EndHorizontal();
        return result;
    }

    private static SearchSortState DrawKeyButton(SearchSortState s, SortKey key, string label, int width, bool enabled = true)
    {
        bool active = s.Key == key;
        var prevColor = GUI.color;
        var prevEnabled = GUI.enabled;
        if (!enabled) { GUI.enabled = false; }
        else if (active) { GUI.color = Color.cyan; }
        if (GUILayout.Button(label, GUILayout.Width(width), GUILayout.Height(DialogStyle.RowHeight)) && enabled)
            s = s.WithKey(key);
        GUI.color = prevColor;
        GUI.enabled = prevEnabled;
        return s;
    }
}
