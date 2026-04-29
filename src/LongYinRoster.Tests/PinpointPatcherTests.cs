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
