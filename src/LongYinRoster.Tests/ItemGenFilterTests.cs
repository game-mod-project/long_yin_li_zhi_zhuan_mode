using System.Collections.Generic;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemGenFilterTests
{
    private static List<ItemGenEntry> Sample() => new()
    {
        new ItemGenEntry { Id = 1, NameRaw = "长矛", NameKr = "장창", Category = ItemGenCategory.Equipment, SubType = 0 },
        new ItemGenEntry { Id = 2, NameRaw = "重甲", NameKr = "중갑", Category = ItemGenCategory.Equipment, SubType = 1 },
        new ItemGenEntry { Id = 3, NameRaw = "金创药", NameKr = "금창약", Category = ItemGenCategory.Medicine, SubType = 0 },
    };

    [Fact]
    public void FilterByCategory_ReturnsOnlyMatching()
    {
        var r = ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, secondary: -1, search: "");
        r.Count.ShouldBe(2);
    }

    [Fact]
    public void FilterBySecondary_NarrowsToSubType()
    {
        var r = ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, secondary: 1, search: "");
        r.Count.ShouldBe(1);
        r[0].Id.ShouldBe(2);
    }

    [Fact]
    public void Search_MatchesKoreanOrRaw()
    {
        ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, -1, "장창").Count.ShouldBe(1);
        ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, -1, "重甲").Count.ShouldBe(1);
    }

    [Fact]
    public void Page_ReturnsSliceAndTotalPages()
    {
        var big = new List<ItemGenEntry>();
        for (int i = 0; i < 25; i++) big.Add(new ItemGenEntry { Id = i, NameKr = $"x{i}", Category = ItemGenCategory.Material });
        var (slice, totalPages) = ItemGenFilter.Page(big, page: 1, pageSize: 10);
        slice.Count.ShouldBe(10);
        totalPages.ShouldBe(3);
        slice[0].Id.ShouldBe(10);
    }

    [Fact]
    public void Page_ClampsOutOfRange()
    {
        var (slice, totalPages) = ItemGenFilter.Page(Sample(), page: 99, pageSize: 10);
        totalPages.ShouldBe(1);
        slice.Count.ShouldBe(3);
    }

    [Fact]
    public void Page_ClampsWhenPageSizeGrows()
    {
        // 25개, 페이지 크기 10 에서 3페이지(idx 2) 보던 중 창을 키워 페이지 크기 20 → 총 2페이지 → idx 1 로 당김
        var items = new List<ItemGenEntry>();
        for (int i = 0; i < 25; i++) items.Add(new ItemGenEntry { Id = i, NameRaw = $"n{i}" });
        var (slice, total) = ItemGenFilter.Page(items, page: 2, pageSize: 20);
        total.ShouldBe(2);
        slice.Count.ShouldBe(5);          // 마지막 페이지(idx 1) = 20..24
        slice[0].Id.ShouldBe(20);
    }
}
