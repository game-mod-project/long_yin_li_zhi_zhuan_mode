#pragma warning disable CS0649

using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class HeroAttriReflectorTests
{
    private sealed class FakeHero
    {
        public List<float> baseAttri        = new() { 199f, 165f, 196f, 183f, 210f, 186f };
        public List<float> maxAttri         = new() { 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseFightSkill   = new() { 155f, 155f, 155f, 162f, 168f, 153f, 315f, 155f, 160f };
        public List<float> maxFightSkill    = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseLivingSkill  = new() { 455f, 255f, 999f, 999f, 702f, 915f, 999f, 250f, 250f };
        public List<float> maxLivingSkill   = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
    }

    [Fact]
    public void GetCount_Attri_Returns6()
        => HeroAttriReflector.GetCount(new FakeHero(), AttriAxis.Attri).ShouldBe(6);

    [Fact]
    public void GetCount_FightSkill_Returns9()
        => HeroAttriReflector.GetCount(new FakeHero(), AttriAxis.FightSkill).ShouldBe(9);

    [Fact]
    public void GetCount_LivingSkill_Returns9()
        => HeroAttriReflector.GetCount(new FakeHero(), AttriAxis.LivingSkill).ShouldBe(9);

    [Theory]
    [InlineData(AttriAxis.Attri, 0, 199f, 999f)]
    [InlineData(AttriAxis.Attri, 3, 183f, 999f)]
    [InlineData(AttriAxis.FightSkill, 6, 315f, 999f)]
    [InlineData(AttriAxis.LivingSkill, 5, 915f, 999f)]
    public void GetEntry_ReturnsBaseAndMax(AttriAxis axis, int idx, float baseExp, float maxExp)
    {
        var (b, m) = HeroAttriReflector.GetEntry(new FakeHero(), axis, idx);
        b.ShouldBe(baseExp);
        m.ShouldBe(maxExp);
    }

    [Fact]
    public void GetEntry_NullHero_ReturnsZeros()
    {
        var (b, m) = HeroAttriReflector.GetEntry(null!, AttriAxis.Attri, 0);
        b.ShouldBe(0f);
        m.ShouldBe(0f);
    }

    [Fact]
    public void GetEntry_OutOfRange_ReturnsZeros()
    {
        var (b, m) = HeroAttriReflector.GetEntry(new FakeHero(), AttriAxis.Attri, 99);
        b.ShouldBe(0f);
        m.ShouldBe(0f);
    }
}
