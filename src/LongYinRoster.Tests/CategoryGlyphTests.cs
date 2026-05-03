using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CategoryGlyphTests
{
    [Theory]
    [InlineData(0, 0, "装")]   // Equipment
    [InlineData(0, 4, "装")]   // Equipment 모든 subType
    [InlineData(2, 0, "药")]   // Medicine (subType=0)
    [InlineData(2, 1, "食")]   // Food (subType≥1)
    [InlineData(2, 2, "食")]
    [InlineData(3, 0, "书")]   // Book
    [InlineData(4, 0, "宝")]   // Treasure
    [InlineData(5, 0, "材")]   // Material
    [InlineData(6, 0, "马")]   // Horse
    [InlineData(6, 1, "马")]
    public void For_KnownTypes_ReturnsCategoryGlyph(int type, int subType, string expected)
    {
        CategoryGlyph.For(type, subType).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(99, 0)]
    [InlineData(-1, 0)]
    public void For_UnknownType_ReturnsDot(int type, int subType)
    {
        CategoryGlyph.For(type, subType).ShouldBe("·");
    }
}
