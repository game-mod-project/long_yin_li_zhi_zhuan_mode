using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CategoryGlyphTests
{
    [Theory]
    [InlineData(0, 0, "장비")]   // Equipment, subType 무관
    [InlineData(0, 4, "장비")]
    // v0.7.11 fix: type=1 → 단약 pill
    [InlineData(1, 0, "단약")]
    [InlineData(1, 1, "단약")]
    // v0.7.11.2 (재수정): type=2 → 음식 전체 (food + wines, subType 무관)
    [InlineData(2, 0, "음식")]
    [InlineData(2, 1, "음식")]   // wine — 인게임 음식 탭 분류
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
    [InlineData(7, 0)]
    [InlineData(99, 0)]
    [InlineData(-1, 0)]
    public void For_UnknownType_ReturnsKitaa(int type, int subType)
    {
        CategoryGlyph.For(type, subType).ShouldBe("기타");
    }
}
