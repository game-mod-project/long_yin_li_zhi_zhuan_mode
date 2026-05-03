using System.Collections.Generic;
using Xunit;
using LongYinRoster.Containers;
using LongYinRoster.UI;

namespace LongYinRoster.Tests;

public class ContainerViewTests
{
    private static ContainerPanel.ItemRow Row(int idx, string name, string cat, int g, int q)
        => new ContainerPanel.ItemRow {
            Index = idx, Name = name, NameRaw = name, CategoryKey = cat,
            GradeOrder = g, QualityOrder = q,
        };

    [Fact]
    public void Search_filters_substring_case_insensitive_on_NameRaw()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "Sword", "001.000", 1, 1), Row(1, "Shield", "001.001", 2, 2), Row(2, "potion", "002.000", 0, 0) };
        var view = new ContainerView();
        var s = SearchSortState.Default.WithSearch("S");
        var result = view.ApplyView(raw, s);
        Assert.Equal(2, result.Count);   // Sword + Shield
    }

    [Fact]
    public void Sort_by_Category_then_Index_ascending()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(2, "B", "002.000", 0, 0), Row(0, "C", "001.001", 0, 0), Row(1, "A", "001.000", 0, 0) };
        var view = new ContainerView();
        var result = view.ApplyView(raw, SearchSortState.Default);   // Category asc default
        Assert.Equal(new[] { 1, 0, 2 }, result.ConvertAll(r => r.Index).ToArray());
    }

    [Fact]
    public void Sort_by_Grade_descending_via_ToggleDirection()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "A", "X", 1, 0), Row(1, "B", "X", 5, 0), Row(2, "C", "X", 3, 0) };
        var view = new ContainerView();
        var s = SearchSortState.Default.WithKey(SortKey.Grade).ToggleDirection();
        var result = view.ApplyView(raw, s);
        Assert.Equal(new[] { 1, 2, 0 }, result.ConvertAll(r => r.Index).ToArray());
    }

    [Fact]
    public void Reflection_failed_rows_with_negative_one_grade_sink_to_end_in_asc()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "A", "X", -1, 0), Row(1, "B", "X", 0, 0), Row(2, "C", "X", 5, 0) };
        var view = new ContainerView();
        var s = SearchSortState.Default.WithKey(SortKey.Grade);   // asc
        var result = view.ApplyView(raw, s);
        // -1 가 가장 작아서 asc 시 맨 앞에 옴
        Assert.Equal(new[] { 0, 1, 2 }, result.ConvertAll(r => r.Index).ToArray());
    }

    [Fact]
    public void Cache_returns_same_array_when_raw_and_state_unchanged()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "A", "X", 1, 1), Row(1, "B", "Y", 2, 2) };
        var view = new ContainerView();
        var s = SearchSortState.Default;
        var first  = view.ApplyView(raw, s);
        var second = view.ApplyView(raw, s);
        Assert.Same(first, second);   // cache hit — 같은 인스턴스
    }
}
