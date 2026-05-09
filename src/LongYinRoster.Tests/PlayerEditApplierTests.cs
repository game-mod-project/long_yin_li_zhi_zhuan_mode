#pragma warning disable CS0649  // POCO mock fields default-initialized

using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.8 Phase 1 — PlayerEditApplier POCO mock 검증.
/// IL2CPP runtime 의존 (RefreshMaxAttriAndSkill / ChangeXxx delta math) 은 인게임 smoke.
/// 본 test 는 reflection setter / fallback / Quick action flow 만 검증.
/// </summary>
public class PlayerEditApplierTests
{
    /// <summary>game-self method 없는 simple POCO — reflection setter only. game property camelCase mirror.</summary>
    private sealed class FakePlayerNoMethod
    {
        public float hp;
        public float maxhp;
        public float realMaxHp;       // cheat-style realMax mirror
        public float power;
        public float maxPower;        // camelCase
        public float realMaxPower;
        public float mana;
        public float maxMana;         // camelCase
        public float realMaxMana;
        public float fame;
        public float externalInjury;
        public float internalInjury;
        public int   poisonInjury;
    }

    /// <summary>game-self method 있는 POCO — ChangeHp(delta) 등 mirror.</summary>
    private sealed class FakePlayerWithMethods
    {
        public float hp;
        public float maxhp;
        public float fame;
        public int ChangeHpCalls;

        // ChangeHp(float delta, bool, bool, bool, bool) — game 의 5-arg 시그니처 mirror
        public void ChangeHp(float delta, bool a, bool b, bool c, bool d)
        {
            ChangeHpCalls++;
            hp += delta;
        }

        // ChangeMaxHp(float delta, bool sync)
        public void ChangeMaxHp(float delta, bool sync)
        {
            maxhp += delta;
        }

        // ChangeFame(float delta, bool ratio)
        public void ChangeFame(float delta, bool ratio)
        {
            fame += delta;
        }
    }

    // ───── ApplyResource ─────

    [Fact]
    public void ApplyResource_NoGameSelfMethod_FallbackReflection()
    {
        var p = new FakePlayerNoMethod { hp = 100, maxhp = 500 };
        var r = PlayerEditApplier.ApplyResource(p, "hp", 250);
        r.Success.ShouldBeTrue();
        r.Method.ShouldBe("reflection");
        p.hp.ShouldBe(250f);
    }

    [Fact]
    public void ApplyResource_GameSelfMethod_PreferredOverReflection()
    {
        var p = new FakePlayerWithMethods { hp = 100, maxhp = 500 };
        var r = PlayerEditApplier.ApplyResource(p, "hp", 250);
        r.Success.ShouldBeTrue();
        r.Method.ShouldBe("game-self");
        p.hp.ShouldBe(250f);
        p.ChangeHpCalls.ShouldBe(1);
    }

    [Fact]
    public void ApplyResource_DeltaCalculation()
    {
        var p = new FakePlayerWithMethods { hp = 100 };
        // 100 → 250 = +150 delta
        var r = PlayerEditApplier.ApplyResource(p, "hp", 250);
        r.Success.ShouldBeTrue();
        p.hp.ShouldBe(250f);  // ChangeHp 가 100 + 150 = 250 으로 계산
    }

    [Fact]
    public void ApplyResource_NegativeDelta_DecreasesValue()
    {
        var p = new FakePlayerWithMethods { hp = 200 };
        var r = PlayerEditApplier.ApplyResource(p, "hp", 50);
        r.Success.ShouldBeTrue();
        p.hp.ShouldBe(50f);
    }

    [Fact]
    public void ApplyResource_NoChange_NoCall()
    {
        var p = new FakePlayerWithMethods { hp = 100 };
        var r = PlayerEditApplier.ApplyResource(p, "hp", 100);
        r.Success.ShouldBeTrue();
        // delta=0 이지만 game-self method 호출 시도 (no harm)
    }

    [Fact]
    public void ApplyResource_MaxHp_GameSelfMethod()
    {
        var p = new FakePlayerWithMethods { maxhp = 500 };
        var r = PlayerEditApplier.ApplyResource(p, "maxhp", 1000);
        r.Success.ShouldBeTrue();
        r.Method.ShouldBe("game-self");
        p.maxhp.ShouldBe(1000f);
    }

    [Fact]
    public void ApplyResource_Fame_GameSelfMethod()
    {
        var p = new FakePlayerWithMethods { fame = 0 };
        var r = PlayerEditApplier.ApplyResource(p, "fame", 12345);
        r.Success.ShouldBeTrue();
        p.fame.ShouldBe(12345f);
    }

    [Fact]
    public void ApplyResource_Power_NoGameSelfMethod_Reflection()
    {
        // power 는 GameSelfMethods 매핑 없음 — reflection setter only
        var p = new FakePlayerNoMethod { power = 100 };
        var r = PlayerEditApplier.ApplyResource(p, "power", 500);
        r.Success.ShouldBeTrue();
        r.Method.ShouldBe("reflection");
        p.power.ShouldBe(500f);
    }

    [Fact]
    public void ApplyResource_Mana_NoGameSelfMethod_Reflection()
    {
        var p = new FakePlayerNoMethod { mana = 0 };
        var r = PlayerEditApplier.ApplyResource(p, "mana", 999);
        r.Success.ShouldBeTrue();
        r.Method.ShouldBe("reflection");
        p.mana.ShouldBe(999f);
    }

    [Fact]
    public void ApplyResource_NullPlayer_ReturnsError()
    {
        var r = PlayerEditApplier.ApplyResource(null!, "hp", 100);
        r.Success.ShouldBeFalse();
        r.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ApplyResource_EmptyFieldName_ReturnsError()
    {
        var p = new FakePlayerNoMethod();
        var r = PlayerEditApplier.ApplyResource(p, "", 100);
        r.Success.ShouldBeFalse();
    }

    [Fact]
    public void ApplyResource_NonExistentField_ReturnsError()
    {
        var p = new FakePlayerNoMethod();
        var r = PlayerEditApplier.ApplyResource(p, "nonExistent", 100);
        r.Success.ShouldBeFalse();
    }

    // ───── Quick actions ─────

    [Fact]
    public void QuickFullHeal_HpEqualsMaxhp()
    {
        var p = new FakePlayerNoMethod { hp = 50, maxhp = 1000 };
        bool ok = PlayerEditApplier.QuickFullHeal(p);
        ok.ShouldBeTrue();
        p.hp.ShouldBe(1000f);
    }

    [Fact]
    public void QuickFullHeal_AlreadyFull_NoOp()
    {
        var p = new FakePlayerNoMethod { hp = 1000, maxhp = 1000 };
        bool ok = PlayerEditApplier.QuickFullHeal(p);
        ok.ShouldBeTrue();
        p.hp.ShouldBe(1000f);
    }

    [Fact]
    public void QuickFullHeal_NullPlayer_ReturnsFalse()
    {
        bool ok = PlayerEditApplier.QuickFullHeal(null!);
        ok.ShouldBeFalse();
    }

    [Fact]
    public void QuickRestoreEnergy_BothMaxed()
    {
        // v0.7.8 — QuickRestoreEnergy 가 QuickFullHeal 으로 통합. obsolete 호출도 같은 효과.
        var p = new FakePlayerNoMethod { mana = 0, maxMana = 500, power = 0, maxPower = 800 };
        #pragma warning disable CS0618
        bool ok = PlayerEditApplier.QuickRestoreEnergy(p);
        #pragma warning restore CS0618
        ok.ShouldBeTrue();
        p.mana.ShouldBe(500f);
        p.power.ShouldBe(800f);
    }

    [Fact]
    public void QuickFullHeal_AllResources()
    {
        // v0.7.8 — 통합: hp + mana + power 모두 max 로 set
        var p = new FakePlayerNoMethod
        {
            hp = 0, maxhp = 1000,
            mana = 0, maxMana = 500,
            power = 0, maxPower = 800,
        };
        bool ok = PlayerEditApplier.QuickFullHeal(p);
        ok.ShouldBeTrue();
        p.hp.ShouldBe(1000f);
        p.mana.ShouldBe(500f);
        p.power.ShouldBe(800f);
    }

    [Fact]
    public void ApplyResource_HpAboveMaxhp_ClampedToMax()
    {
        // v0.7.8 — 현재값(hp/mana/power) 입력 시 max 로 자동 clamp
        var p = new FakePlayerNoMethod { hp = 100, maxhp = 500 };
        var r = PlayerEditApplier.ApplyResource(p, "hp", 99999);
        r.Success.ShouldBeTrue();
        p.hp.ShouldBe(500f);   // clamped
    }

    [Fact]
    public void ApplyResource_ManaAboveMaxMana_ClampedToMax()
    {
        var p = new FakePlayerNoMethod { mana = 0, maxMana = 300 };
        var r = PlayerEditApplier.ApplyResource(p, "mana", 9999);
        r.Success.ShouldBeTrue();
        p.mana.ShouldBe(300f);
    }

    [Fact]
    public void ApplyResource_MaxhpItself_NotClamped()
    {
        // maxhp 자체는 clamp 안 됨 (CurrentToMax 매핑에 hp/mana/power 만)
        var p = new FakePlayerNoMethod { maxhp = 100 };
        var r = PlayerEditApplier.ApplyResource(p, "maxhp", 99999);
        r.Success.ShouldBeTrue();
        p.maxhp.ShouldBe(99999f);
    }

    [Fact]
    public void QuickCureInjuries_AllZero()
    {
        var p = new FakePlayerNoMethod
        {
            externalInjury = 50,
            internalInjury = 30,
            poisonInjury   = 20,
        };
        bool ok = PlayerEditApplier.QuickCureInjuries(p);
        ok.ShouldBeTrue();
        p.externalInjury.ShouldBe(0f);
        p.internalInjury.ShouldBe(0f);
        p.poisonInjury.ShouldBe(0);
    }

    [Fact]
    public void QuickCureInjuries_NoInjuryFields_ReturnsFalse()
    {
        // POCO 에 injury field 없으면 best-effort 모두 실패
        var anonymousObj = new { hp = 100f };
        bool ok = PlayerEditApplier.QuickCureInjuries(anonymousObj);
        ok.ShouldBeFalse();
    }

    [Fact]
    public void QuickCureInjuries_NullPlayer_ReturnsFalse()
    {
        bool ok = PlayerEditApplier.QuickCureInjuries(null!);
        ok.ShouldBeFalse();
    }
}
