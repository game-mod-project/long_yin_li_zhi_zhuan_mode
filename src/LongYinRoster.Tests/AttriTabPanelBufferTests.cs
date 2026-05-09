#pragma warning disable CS0649

using System.Collections.Generic;
using LongYinRoster.UI;
using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class AttriTabPanelBufferTests
{
    private sealed class FakeHero
    {
        public List<float> baseAttri       = new() { 199f, 165f, 196f, 183f, 210f, 186f };
        public List<float> maxAttri        = new() { 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseFightSkill  = new() { 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f };
        public List<float> maxFightSkill   = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseLivingSkill = new() { 200f, 200f, 200f, 200f, 200f, 200f, 200f, 200f, 200f };
        public List<float> maxLivingSkill  = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
    }

    [Fact]
    public void LoadFromHero_PopulatesBuffer()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());

        buf.Get(AttriAxis.Attri, 0).BaseInput.ShouldBe("199");
        buf.Get(AttriAxis.Attri, 0).MaxInput.ShouldBe("999");
        buf.Get(AttriAxis.FightSkill, 0).BaseInput.ShouldBe("100");
        buf.Get(AttriAxis.LivingSkill, 8).BaseInput.ShouldBe("200");
        buf.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void SetInput_DifferentValue_FlagsDirty()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "999");
        buf.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void SetInput_SameValue_DoesNotFlagDirty()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "199");
        buf.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void BulkSetMax_AppliesToAllRows()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.BulkSetMax(AttriAxis.FightSkill, "9999");

        for (int i = 0; i < 9; i++)
            buf.Get(AttriAxis.FightSkill, i).MaxInput.ShouldBe("9999");
        buf.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Reset_RestoresOriginals()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "999");
        buf.SetMaxInput(AttriAxis.Attri, 0, "9999");
        buf.IsDirty.ShouldBeTrue();

        buf.Reset();

        buf.Get(AttriAxis.Attri, 0).BaseInput.ShouldBe("199");
        buf.Get(AttriAxis.Attri, 0).MaxInput.ShouldBe("999");
        buf.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void DirtyRows_ReturnsOnlyChanged()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "999");
        buf.SetMaxInput(AttriAxis.FightSkill, 4, "5000");

        var dirty = buf.GetDirtyRows();
        dirty.Count.ShouldBe(2);
    }
}
