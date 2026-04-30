using System;
using System.Linq;
using FluentAssertions;
using LongYinRoster.Core;
using Xunit;

namespace LongYinRoster.Tests;

public class ApplyResultTests
{
    [Fact]
    public void ApplyResult_StartsEmpty()
    {
        var r = new ApplyResult();
        r.AppliedFields.Should().BeEmpty();
        r.SkippedFields.Should().BeEmpty();
        r.WarnedFields.Should().BeEmpty();
        r.StepErrors.Should().BeEmpty();
        r.HasFatalError.Should().BeFalse();
    }

    [Fact]
    public void ApplyResult_TracksAppliedSkippedWarned()
    {
        var r = new ApplyResult();
        r.AppliedFields.Add("heroName");
        r.SkippedFields.Add("portraitID — no setter mapped");
        r.WarnedFields.Add("hp — InvalidCastException");
        r.StepErrors.Add(new InvalidOperationException("step6 throw"));
        r.HasFatalError = true;

        r.AppliedFields.Should().ContainSingle().Which.Should().Be("heroName");
        r.SkippedFields.Should().ContainSingle();
        r.WarnedFields.Should().ContainSingle();
        r.StepErrors.Should().ContainSingle();
        r.HasFatalError.Should().BeTrue();
    }
}

public class IL2CppListOpsTests
{
    [Fact]
    public void Count_ReturnsItemCount_ForStandardList()
    {
        var list = new System.Collections.Generic.List<int> { 10, 20, 30 };
        IL2CppListOps.Count(list).Should().Be(3);
    }

    [Fact]
    public void Get_ReturnsItemAt_ForStandardList()
    {
        var list = new System.Collections.Generic.List<string> { "a", "b", "c" };
        IL2CppListOps.Get(list, 1).Should().Be("b");
    }

    [Fact]
    public void Clear_ClearsStandardList()
    {
        var list = new System.Collections.Generic.List<int> { 1, 2, 3 };
        IL2CppListOps.Clear(list);
        list.Count.Should().Be(0);
    }
}

public class SimpleFieldMatrixTests
{
    [Fact]
    public void Schema_FrozenShape()
    {
        SimpleFieldMatrix.Entries.Should().NotBeNull();
        SimpleFieldMatrix.Entries.Should().NotBeEmpty();
        foreach (var e in SimpleFieldMatrix.Entries)
        {
            e.Name.Should().NotBeNullOrWhiteSpace();
            e.JsonPath.Should().NotBeNullOrWhiteSpace();
            e.PropertyName.Should().NotBeNullOrWhiteSpace();
            e.Type.Should().NotBeNull();
        }
    }

    [Fact]
    public void Entries_HasSeventeenAfterV04Refactor()
    {
        // v0.3: 18 entries (22 dump rows - 4 force-related strip).
        // v0.4: -1 (active kungfu nowActiveSkill removed → 별도 SetActiveKungfu step in Task B10).
        // 변경 시 spec §5.7 / §7.2.1 동기화.
        SimpleFieldMatrix.Entries.Count.Should().Be(17);
    }

    [Fact]
    public void Entries_HasNoActiveKungfuEntry()
    {
        SimpleFieldMatrix.Entries.Should().NotContain(
            e => e.PropertyName == "nowActiveSkill");
    }

    [Fact]
    public void Entries_InjuryAndLoyalAndFavor_AreCategoryNone()
    {
        // v0.4: 부상/충성/호감 backup 폐기 — Category=None 으로 표시되어 Apply 안 함 (영구 보존).
        var noneNames = new[] { "externalInjury", "internalInjury", "poisonInjury", "loyal", "favor" };
        foreach (var name in noneNames)
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == name);
            e.Should().NotBeNull($"{name} entry must exist (kept in matrix as None — not deleted)");
            e!.Category.Should().Be(FieldCategory.None);
        }
    }

    [Fact]
    public void Entries_HpManaPower_AreCategoryStat()
    {
        foreach (var name in new[] { "hp", "mana", "power" })
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == name);
            e.Should().NotBeNull();
            e!.Category.Should().Be(FieldCategory.Stat);
        }
    }

    [Fact]
    public void Entries_FameAndBadFame_AreCategoryHonor()
    {
        foreach (var name in new[] { "fame", "badFame" })
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == name);
            e.Should().NotBeNull();
            e!.Category.Should().Be(FieldCategory.Honor);
        }
    }

    [Fact]
    public void Entries_SkinID_IsCategorySkin()
    {
        var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == "skinID");
        e.Should().NotBeNull();
        e!.Category.Should().Be(FieldCategory.Skin);
    }

    [Fact]
    public void Entries_SelfHouse_IsCategorySelfHouse()
    {
        var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == "selfHouseTotalAdd");
        e.Should().NotBeNull();
        e!.Category.Should().Be(FieldCategory.SelfHouse);
    }

    [Fact]
    public void Entries_HeroTagPoint_IsCategoryTalentPoint()
    {
        var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == "heroTagPoint");
        e.Should().NotBeNull();
        e!.Category.Should().Be(FieldCategory.TalentPoint);
    }

    [Fact]
    public void Entries_BaseStatLists_AreCategoryStat()
    {
        foreach (var name in new[] { "baseAttri", "baseFightSkill", "baseLivingSkill", "expLivingSkill" })
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == name);
            e.Should().NotBeNull();
            e!.Category.Should().Be(FieldCategory.Stat);
        }
    }
}
