using System;
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
