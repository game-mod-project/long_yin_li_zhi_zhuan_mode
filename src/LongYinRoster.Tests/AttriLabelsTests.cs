using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class AttriLabelsTests
{
    [Fact]
    public void Attri_Returns6Labels()
    {
        AttriLabels.Attri.Length.ShouldBe(6);
        AttriLabels.Attri[0].ShouldBe("근력");
        AttriLabels.Attri[5].ShouldBe("경맥");
    }

    [Fact]
    public void FightSkill_Returns9Labels()
    {
        AttriLabels.FightSkill.Length.ShouldBe(9);
        AttriLabels.FightSkill[0].ShouldBe("내공");
        AttriLabels.FightSkill[8].ShouldBe("사술");
    }

    [Fact]
    public void LivingSkill_Returns9Labels()
    {
        AttriLabels.LivingSkill.Length.ShouldBe(9);
        AttriLabels.LivingSkill[0].ShouldBe("의술");
        AttriLabels.LivingSkill[8].ShouldBe("요리");
    }

    [Theory]
    [InlineData(AttriAxis.Attri, 0, "근력")]
    [InlineData(AttriAxis.Attri, 3, "의지")]
    [InlineData(AttriAxis.FightSkill, 4, "검법")]
    [InlineData(AttriAxis.LivingSkill, 6, "단조")]
    public void For_ReturnsLabelByAxisIndex(AttriAxis axis, int idx, string expected)
        => AttriLabels.For(axis, idx).ShouldBe(expected);

    [Theory]
    [InlineData(AttriAxis.Attri, 6)]
    [InlineData(AttriAxis.Attri, -1)]
    [InlineData(AttriAxis.FightSkill, 9)]
    public void For_OutOfRange_ReturnsBracketedIndex(AttriAxis axis, int idx)
        => AttriLabels.For(axis, idx).ShouldStartWith("[");
}
