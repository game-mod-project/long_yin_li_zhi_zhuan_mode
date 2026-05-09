using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.10 Phase 3 — HeroDataCapBypassLogic 의 분기 검증.
/// Postfix wrapper (HeroDataCapBypassPatch) 와 RefreshMax snapshot/restore 는 IL2CPP runtime 의존 → 인게임 smoke.
/// </summary>
public class HeroDataCapBypassTests
{
    private sealed class FakeHero { public int heroID; }

    [Fact]
    public void Apply_UncapOff_ClampsToCap()
    {
        float result = 200f;
        HeroDataCapBypassLogic.ApplyMaxOverride(
            instance: new FakeHero { heroID = 0 },
            isUncapEnabled: false, uncapValue: 999, gameCap: 120f,
            playerHeroID: 0, ref result);
        result.ShouldBe(120f);  // defensive re-clamp when uncap off
    }

    [Fact]
    public void Apply_UncapOff_ResultBelowCap_NoChange()
    {
        float result = 80f;
        HeroDataCapBypassLogic.ApplyMaxOverride(
            instance: new FakeHero { heroID = 0 },
            isUncapEnabled: false, uncapValue: 999, gameCap: 120f,
            playerHeroID: 0, ref result);
        result.ShouldBe(80f);
    }

    [Fact]
    public void Apply_UncapOnPlayerMatch_OverridesToUncapValue()
    {
        float result = 120f;
        HeroDataCapBypassLogic.ApplyMaxOverride(
            instance: new FakeHero { heroID = 0 },
            isUncapEnabled: true, uncapValue: 999, gameCap: 120f,
            playerHeroID: 0, ref result);
        result.ShouldBe(999f);
    }

    [Fact]
    public void Apply_UncapOnHeroIDMismatch_DoesNotOverride()
    {
        float result = 200f;  // assume something else made it >120 (unlikely but defensive)
        HeroDataCapBypassLogic.ApplyMaxOverride(
            instance: new FakeHero { heroID = 5 },
            isUncapEnabled: true, uncapValue: 999, gameCap: 120f,
            playerHeroID: 0, ref result);
        // NPC: when uncap on but mismatch, do NOT override. Result stays as-is (game decides).
        result.ShouldBe(200f);
    }

    [Fact]
    public void Apply_UncapOnPlayerNull_DoesNotOverride()
    {
        float result = 200f;
        HeroDataCapBypassLogic.ApplyMaxOverride(
            instance: new FakeHero { heroID = 0 },
            isUncapEnabled: true, uncapValue: 999, gameCap: 120f,
            playerHeroID: -1, ref result);
        result.ShouldBe(200f);  // player null → no-op
    }

    [Fact]
    public void Apply_LivingSkill_Cap100()
    {
        float result = 150f;
        HeroDataCapBypassLogic.ApplyMaxOverride(
            instance: new FakeHero { heroID = 0 },
            isUncapEnabled: false, uncapValue: 999, gameCap: 100f,
            playerHeroID: 0, ref result);
        result.ShouldBe(100f);
    }

    [Fact]
    public void Apply_UncapValueZero_DoesNotOverride()
    {
        float result = 120f;
        HeroDataCapBypassLogic.ApplyMaxOverride(
            instance: new FakeHero { heroID = 0 },
            isUncapEnabled: true, uncapValue: 0, gameCap: 120f,
            playerHeroID: 0, ref result);
        result.ShouldBe(120f);  // 0 means no override
    }
}
