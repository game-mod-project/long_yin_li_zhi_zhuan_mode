using System.Collections.Generic;
using LongYinRoster.Containers;
using LongYinRoster.UI;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.7.11 Cat 4B/4E — ContainerView 의 등급 범위 + 착용중 제외 filter 검증.</summary>
public class ContainerViewExtraFilterTests
{
    private static List<ContainerPanel.ItemRow> Rows() => new()
    {
        new() { Index = 0, NameRaw = "열악item", GradeOrder = 0, Equipped = false },
        new() { Index = 1, NameRaw = "보통item", GradeOrder = 1, Equipped = false },
        new() { Index = 2, NameRaw = "정량item", GradeOrder = 2, Equipped = true  },
        new() { Index = 3, NameRaw = "비전item", GradeOrder = 3, Equipped = false },
        new() { Index = 4, NameRaw = "정극item", GradeOrder = 4, Equipped = true  },
        new() { Index = 5, NameRaw = "절세item", GradeOrder = 5, Equipped = false },
    };

    [Fact]
    public void Default_NoExtraFilter_ReturnsAll()
    {
        var view = new ContainerView();
        var result = view.ApplyView(Rows(), SearchSortState.Default);
        result.Count.ShouldBe(6);
    }

    [Fact]
    public void MinGradeOrder_3_FiltersBelow()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default.WithMinGradeOrder(3);
        var result = view.ApplyView(Rows(), state);
        result.Count.ShouldBe(3);                 // 비전 / 정극 / 절세
        foreach (var r in result) r.GradeOrder.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void MinGradeOrder_5_OnlyTopGrade()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default.WithMinGradeOrder(5);
        var result = view.ApplyView(Rows(), state);
        result.Count.ShouldBe(1);                 // 절세
        result[0].NameRaw.ShouldBe("절세item");
    }

    [Fact]
    public void ExcludeEquipped_FiltersEquippedItems()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default.WithExcludeEquipped(true);
        var result = view.ApplyView(Rows(), state);
        result.Count.ShouldBe(4);                 // 6 - 2 (idx 2 + 4)
        foreach (var r in result) r.Equipped.ShouldBeFalse();
    }

    [Fact]
    public void Combined_MinGradeAndExcludeEquipped()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default
            .WithMinGradeOrder(3)
            .WithExcludeEquipped(true);
        var result = view.ApplyView(Rows(), state);
        result.Count.ShouldBe(2);                 // 비전 (idx 3) + 절세 (idx 5). 정극 (idx 4) = 등급 OK but equipped → 제외
        foreach (var r in result)
        {
            r.GradeOrder.ShouldBeGreaterThanOrEqualTo(3);
            r.Equipped.ShouldBeFalse();
        }
    }

    [Fact]
    public void StateEquality_IncludesNewFields()
    {
        // 새 fields 가 다르면 not equal — cache invalidation 작동
        var s1 = new SearchSortState("", SortKey.Category, true);
        var s2 = new SearchSortState("", SortKey.Category, true).WithMinGradeOrder(3);
        s1.Equals(s2).ShouldBeFalse();

        var s3 = s1.WithExcludeEquipped(true);
        s1.Equals(s3).ShouldBeFalse();
    }
}
