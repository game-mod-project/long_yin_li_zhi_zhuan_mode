using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CategoryGlyphTests
{
    [Theory]
    [InlineData(0, 0, "장비")]   // Equipment, subType 무관
    [InlineData(0, 4, "장비")]
    [InlineData(2, 0, "단약")]   // Medicine (subType=0)
    [InlineData(2, 1, "음식")]   // Food (subType≥1)
    [InlineData(2, 2, "음식")]
    [InlineData(3, 0, "비급")]   // Book
    [InlineData(4, 0, "보물")]   // Treasure
    [InlineData(5, 0, "재료")]   // Material
    [InlineData(6, 0, "말")]     // Horse
    [InlineData(6, 1, "말")]
    public void For_KnownTypes_ReturnsKoreanLabel(int type, int subType, string expected)
    {
        CategoryGlyph.For(type, subType).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(99, 0)]
    [InlineData(-1, 0)]
    public void For_UnknownType_ReturnsKitaa(int type, int subType)
    {
        CategoryGlyph.For(type, subType).ShouldBe("기타");
    }
}
