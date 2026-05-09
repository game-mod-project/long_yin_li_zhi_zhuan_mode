#pragma warning disable CS0649

using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CharacterAttriEditorTests
{
    /// <summary>cheat ChangeAttri 패턴 mirror — game-self method 가 있는 mock.</summary>
    private sealed class FakeHeroWithMethods
    {
        public List<float> baseAttri       = new() { 199f, 165f, 196f, 183f, 210f, 186f };
        public List<float> maxAttri        = new() { 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseFightSkill  = new() { 155f, 155f, 155f, 162f, 168f, 153f, 315f, 155f, 160f };
        public List<float> maxFightSkill   = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseLivingSkill = new() { 455f, 255f, 999f, 999f, 702f, 915f, 999f, 250f, 250f };
        public List<float> maxLivingSkill  = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };

        public int ChangeAttriCalls;
        public int ChangeFightSkillCalls;
        public int ChangeLivingSkillCalls;

        public void ChangeAttri(int idx, float delta, bool a, bool b)
        {
            ChangeAttriCalls++;
            baseAttri[idx] += delta;
        }

        public void ChangeFightSkill(int idx, float delta, bool a, bool b)
        {
            ChangeFightSkillCalls++;
            baseFightSkill[idx] += delta;
        }

        public void ChangeLivingSkill(int idx, float delta, bool a, bool b)
        {
            ChangeLivingSkillCalls++;
            baseLivingSkill[idx] += delta;
        }
    }

    [Fact]
    public void Change_Attri_AppliesViaGameMethod()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.Attri, 0, 999f);
        ok.ShouldBeTrue();
        hero.baseAttri[0].ShouldBe(999f);
        hero.ChangeAttriCalls.ShouldBe(1);
    }

    [Fact]
    public void Change_FightSkill_AppliesViaGameMethod()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.FightSkill, 4, 500f);
        ok.ShouldBeTrue();
        hero.baseFightSkill[4].ShouldBe(500f);
        hero.ChangeFightSkillCalls.ShouldBe(1);
    }

    [Fact]
    public void Change_LivingSkill_AppliesViaGameMethod()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.LivingSkill, 8, 1500f);
        ok.ShouldBeTrue();
        hero.baseLivingSkill[8].ShouldBe(1500f);
        hero.ChangeLivingSkillCalls.ShouldBe(1);
    }

    [Fact]
    public void Change_ValueAboveMax_BumpsMaxFirst()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.Attri, 0, 1500f);
        ok.ShouldBeTrue();
        hero.maxAttri[0].ShouldBe(1500f);
        hero.baseAttri[0].ShouldBe(1500f);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(1000000f, 999999f)]
    public void Change_ClampsToValidRange(float input, float expected)
    {
        var hero = new FakeHeroWithMethods();
        CharacterAttriEditor.Change(hero, AttriAxis.Attri, 0, input);
        hero.baseAttri[0].ShouldBe(expected);
    }

    [Fact]
    public void TryParse_NonNumeric_ReturnsFalse()
        => CharacterAttriEditor.TryParseInput("abc", out _).ShouldBeFalse();

    [Fact]
    public void TryParse_Numeric_ReturnsTrueWithClampedValue()
    {
        CharacterAttriEditor.TryParseInput("12345", out float v).ShouldBeTrue();
        v.ShouldBe(12345f);
    }

    [Fact]
    public void TryParse_Empty_ReturnsFalse()
        => CharacterAttriEditor.TryParseInput("", out _).ShouldBeFalse();

    [Fact]
    public void ChangeMax_BumpsMaxOnly_DoesNotChangeBase()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.ChangeMax(hero, AttriAxis.Attri, 0, 5000f);
        ok.ShouldBeTrue();
        hero.maxAttri[0].ShouldBe(5000f);
        hero.baseAttri[0].ShouldBe(199f);  // unchanged
    }
}
