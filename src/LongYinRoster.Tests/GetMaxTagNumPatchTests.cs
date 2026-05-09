using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.10 Phase 1 — GetMaxTagNumOverride (test-friendly logic 분리) 검증.
/// Harmony Postfix wrapper (GetMaxTagNumPatch) 는 IL2CPP runtime 의존 → 인게임 smoke.
/// 본 test 는 ApplyOverride 의 분기 (lock off / on / heroID match / value=0 / player null) 만 검증.
/// </summary>
public class GetMaxTagNumPatchTests
{
    private sealed class FakeHero { public int heroID; }

    [Fact]
    public void Apply_LockOff_ReturnsOriginal()
    {
        int result = 30;
        GetMaxTagNumOverride.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: false, lockedValue: 999, playerHeroID: 0, ref result);
        result.ShouldBe(30);
    }

    [Fact]
    public void Apply_LockOnPlayerMatch_OverridesResult()
    {
        int result = 30;
        GetMaxTagNumOverride.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: true, lockedValue: 999, playerHeroID: 0, ref result);
        result.ShouldBe(999);
    }

    [Fact]
    public void Apply_LockOnHeroIDMismatch_DoesNotOverride()
    {
        int result = 30;
        GetMaxTagNumOverride.ApplyOverride(new FakeHero { heroID = 5 },
            isLocked: true, lockedValue: 999, playerHeroID: 0, ref result);
        result.ShouldBe(30);
    }

    [Fact]
    public void Apply_LockOnValueZero_DoesNotOverride()
    {
        int result = 30;
        GetMaxTagNumOverride.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: true, lockedValue: 0, playerHeroID: 0, ref result);
        result.ShouldBe(30);
    }

    [Fact]
    public void Apply_LockOnPlayerNull_DoesNotOverride()
    {
        int result = 30;
        // playerHeroID=-1 simulates "player null" (HeroLocator.GetPlayer() returned null).
        GetMaxTagNumOverride.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: true, lockedValue: 999, playerHeroID: -1, ref result);
        result.ShouldBe(30);
    }
}
