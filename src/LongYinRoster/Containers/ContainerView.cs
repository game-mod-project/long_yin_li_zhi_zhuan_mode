using System;
using System.Collections.Generic;
using System.Linq;
using LongYinRoster.UI;

namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — raw row list + SearchSortState → filtered/sorted view list (cached).
/// raw reference 또는 SearchSortState 가 변하면 재계산. 같으면 캐시 인스턴스 반환.
/// IL2CPP IMGUI 매 frame OnGUI 부담 ↓ 가 핵심 목적.
/// </summary>
public sealed class ContainerView
{
    private object?                       _lastRawRef;   // reference identity
    private SearchSortState?              _lastState;
    private List<ContainerPanel.ItemRow>? _cached;

    public List<ContainerPanel.ItemRow> ApplyView(List<ContainerPanel.ItemRow> raw, SearchSortState state)
    {
        if (raw == null) raw = new List<ContainerPanel.ItemRow>();
        if (state == null) state = SearchSortState.Default;

        if (object.ReferenceEquals(raw, _lastRawRef) && state.Equals(_lastState) && _cached != null)
            return _cached;

        IEnumerable<ContainerPanel.ItemRow> q = raw;

        if (!string.IsNullOrEmpty(state.Search))
        {
            string needle = state.Search;
            q = q.Where(r => (r.NameRaw ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        q = state.Key switch
        {
            SortKey.Category => q.OrderBy(r => r.CategoryKey ?? "").ThenBy(r => r.Index),
            SortKey.Name     => q.OrderBy(r => r.NameRaw ?? "").ThenBy(r => r.Index),
            SortKey.Grade    => q.OrderBy(r => r.GradeOrder).ThenBy(r => r.Index),
            SortKey.Quality  => q.OrderBy(r => r.QualityOrder).ThenBy(r => r.Index),
            _                => q.OrderBy(r => r.Index),
        };

        var result = q.ToList();
        if (!state.Ascending) result.Reverse();

        _lastRawRef = raw;
        _lastState  = state;
        _cached     = result;
        return result;
    }

    public void Invalidate()
    {
        _lastRawRef = null;
        _lastState  = null;
        _cached     = null;
    }
}
