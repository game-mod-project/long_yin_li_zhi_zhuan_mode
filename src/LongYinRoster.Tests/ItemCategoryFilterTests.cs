using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemCategoryFilterTests
{
    [Theory]
    [InlineData(0, 0, ItemCategory.Equipment)]
    [InlineData(0, 4, ItemCategory.Equipment)]
    // v0.7.11.1 fix: type=1 → 단약 (보혈단/통락단/황련환 등 pill)
    [InlineData(1, 0, ItemCategory.Medicine)]
    [InlineData(1, 1, ItemCategory.Medicine)]
    // v0.7.11.1 fix: type=2 subType=0 → 음식 / type=2 subType≥1 → 단약 (약주 e.g. 용뇌주)
    [InlineData(2, 0, ItemCategory.Food)]
    [InlineData(2, 1, ItemCategory.Medicine)]
    [InlineData(2, 2, ItemCategory.Medicine)]
    [InlineData(3, 0, ItemCategory.Book)]
    [InlineData(4, 0, ItemCategory.Treasure)]
    [InlineData(5, 0, ItemCategory.Material)]
    [InlineData(6, 0, ItemCategory.Horse)]
    [InlineData(6, 1, ItemCategory.Horse)]
    public void Classify_KnownTypes(int type, int subType, ItemCategory expected)
    {
        ItemCategoryFilter.Classify(type, subType).ShouldBe(expected);
    }

    [Fact]
    public void Classify_UnknownType_ReturnsOther()
    {
        ItemCategoryFilter.Classify(7, 0).ShouldBe(ItemCategory.Other);
        ItemCategoryFilter.Classify(99, 0).ShouldBe(ItemCategory.Other);
        ItemCategoryFilter.Classify(-1, 0).ShouldBe(ItemCategory.Other);
    }

    [Fact]
    public void Matches_AllCategoryShowsAll()
    {
        ItemCategoryFilter.Matches(ItemCategory.All, 0, 0).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.All, 99, 99).ShouldBeTrue();
    }

    [Fact]
    public void Matches_SpecificCategoryFilters()
    {
        ItemCategoryFilter.Matches(ItemCategory.Equipment, 0, 0).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.Equipment, 3, 0).ShouldBeFalse();
        // 단약 = type 1 OR (type 2 + subType≥1)
        ItemCategoryFilter.Matches(ItemCategory.Medicine, 1, 0).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.Medicine, 2, 1).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.Medicine, 2, 0).ShouldBeFalse();   // 음식
        // 음식 = type 2 + subType=0 only
        ItemCategoryFilter.Matches(ItemCategory.Food, 2, 0).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.Food, 2, 1).ShouldBeFalse();       // 단약
    }
}
