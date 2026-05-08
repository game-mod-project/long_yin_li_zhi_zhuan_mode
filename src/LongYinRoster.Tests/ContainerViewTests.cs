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

    // v0.7.5 — bilingual 검색 + Korean-aware 정렬
    private static List<ContainerPanel.ItemRow> BilingualSample()
    {
        return new List<ContainerPanel.ItemRow>
        {
            new() { Index = 0, Name = "九阳神功", NameRaw = "九阳神功", NameKr = "구양신공", Type = 3, GradeOrder = 5, QualityOrder = 5, CategoryKey = "003.000" },
            new() { Index = 1, Name = "古今图书", NameRaw = "古今图书", NameKr = "고금도서", Type = 3, GradeOrder = 4, QualityOrder = 5, CategoryKey = "003.000" },
            new() { Index = 2, Name = "九转还魂丹", NameRaw = "九转还魂丹", NameKr = null, Type = 2, GradeOrder = 5, QualityOrder = 5, CategoryKey = "002.000" },
        };
    }

    [Fact]
    public void Search_KoreanKeyword_MatchesViaNameKr()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default.WithSearch("구양");
        var result = view.ApplyView(BilingualSample(), state);
        Assert.Single(result);
        Assert.Equal("九阳神功", result[0].NameRaw);
    }

    [Fact]
    public void Search_ChineseKeyword_MatchesViaNameRaw()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default.WithSearch("九阳");
        var result = view.ApplyView(BilingualSample(), state);
        Assert.Single(result);
        Assert.Equal("구양신공", result[0].NameKr);
    }

    [Fact]
    public void Search_NameKrNull_FallsBackToNameRaw()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default.WithSearch("九转");
        var result = view.ApplyView(BilingualSample(), state);
        Assert.Single(result);
        Assert.Equal(2, result[0].Index);
    }

    [Fact]
    public void Sort_NameKey_PrefersNameKr()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default.WithKey(SortKey.Name);   // ascending default
        var result = view.ApplyView(BilingualSample(), state);
        // Korean 자모순: "고금도서" < "구양신공" — NameKr 기준 정렬
        Assert.Equal("고금도서", result[0].NameKr);
        Assert.Equal("구양신공", result[1].NameKr);
    }
}
