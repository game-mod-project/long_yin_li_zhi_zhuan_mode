#pragma warning disable CS0649  // POCO mock fields default-initialized

using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.7 Task 2 — ItemEditApplier reflection setter + read-back + sanitize.
/// IL2CPP runtime 의존 (CountValueAndWeight / RefreshSelfState / regenerate fallback) 은 인게임 smoke.
/// 본 test 는 POCO mock 만 사용 — reflection 경로만 검증.
/// </summary>
public class ItemEditApplierTests
{
    // ───── POCO mocks ─────

    private sealed class FakeItem
    {
        public int rareLv;
        public int itemLv;
        public int value;
        public int type;
        public FakeEquipment? equipmentData;
        public FakeHorse? horseData;
    }
    private sealed class FakeEquipment
    {
        public int  enhanceLv;
        public int  speEnhanceLv;
        public int  speWeightLv;
        public bool equiped;
    }
    private sealed class FakeHorse
    {
        public float speedAdd;
        public float favorRate = 1f;
        public bool  equiped;
    }

    // ───── Apply (top-level / nested / sanitize / errors) ─────

    [Fact]
    public void Apply_TopLevelInt_SetterPasses()
    {
        var item = new FakeItem { rareLv = 0, type = 0 };
        var field = new ItemEditField("rareLv", "등급", ItemEditFieldKind.Int, 0, 5);
        var r = ItemEditApplier.Apply(item, field, 3, player: null);
        r.Success.ShouldBeTrue();
        r.Method.ShouldBe("reflection");
        item.rareLv.ShouldBe(3);
    }

    [Fact]
    public void Apply_NestedSubData_SetterPasses()
    {
        var item = new FakeItem { type = 0, equipmentData = new FakeEquipment { enhanceLv = 0 } };
        var field = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
        var r = ItemEditApplier.Apply(item, field, 5, player: null);
        r.Success.ShouldBeTrue();
        item.equipmentData!.enhanceLv.ShouldBe(5);
    }

    [Fact]
    public void Apply_DotPath_TwoSegments()
    {
        var item = new FakeItem { equipmentData = new FakeEquipment { speEnhanceLv = 0 } };
        var field = new ItemEditField("equipmentData.speEnhanceLv", "특수 강화", ItemEditFieldKind.Int, 0, 9);
        var r = ItemEditApplier.Apply(item, field, 4, player: null);
        r.Success.ShouldBeTrue();
        item.equipmentData!.speEnhanceLv.ShouldBe(4);
    }

    [Fact]
    public void Apply_Bool_SetterPasses()
    {
        var item = new FakeItem { type = 0, equipmentData = new FakeEquipment { equiped = false } };
        var field = new ItemEditField("equipmentData.equiped", "착용중", ItemEditFieldKind.Bool, 0, 1);
        var r = ItemEditApplier.Apply(item, field, true, player: null);
        r.Success.ShouldBeTrue();
        item.equipmentData!.equiped.ShouldBeTrue();
    }

    [Fact]
    public void Apply_NullSubData_ReturnsError()
    {
        var item = new FakeItem { type = 0, equipmentData = null };
        var field = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
        var r = ItemEditApplier.Apply(item, field, 5, player: null);
        r.Success.ShouldBeFalse();
        r.Error.ShouldNotBeNull();
        r.Error!.ShouldContain("equipmentData");
    }

    [Fact]
    public void Apply_InvalidPath_ReturnsError()
    {
        var item = new FakeItem();
        var field = new ItemEditField("nonExistent", "?", ItemEditFieldKind.Int, 0, 9);
        var r = ItemEditApplier.Apply(item, field, 1, player: null);
        r.Success.ShouldBeFalse();
        r.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Apply_NullItem_ReturnsError()
    {
        var field = new ItemEditField("rareLv", "등급", ItemEditFieldKind.Int, 0, 5);
        var r = ItemEditApplier.Apply(null!, field, 1, player: null);
        r.Success.ShouldBeFalse();
        r.Error.ShouldNotBeNull();
        r.Error!.ShouldContain("null");
    }

    // ───── Sanitize ─────

    [Fact]
    public void Apply_NaN_SanitizedToFallback()
    {
        var item = new FakeItem { type = 6, horseData = new FakeHorse { speedAdd = 100f } };
        var field = new ItemEditField("horseData.speedAdd", "속도", ItemEditFieldKind.Float, 0, 9999);
        var r = ItemEditApplier.Apply(item, field, float.NaN, player: null);
        r.Success.ShouldBeTrue();
        item.horseData!.speedAdd.ShouldBe(0f);   // min=0 으로 fallback
    }

    [Fact]
    public void Apply_PositiveInfinity_SanitizedToMax()
    {
        var item = new FakeItem { type = 6, horseData = new FakeHorse { speedAdd = 100f } };
        var field = new ItemEditField("horseData.speedAdd", "속도", ItemEditFieldKind.Float, 0, 9999);
        var r = ItemEditApplier.Apply(item, field, float.PositiveInfinity, player: null);
        r.Success.ShouldBeTrue();
        item.horseData!.speedAdd.ShouldBe(9999f);
    }

    [Fact]
    public void Apply_BelowMin_ClampedToMin()
    {
        var item = new FakeItem { type = 0, equipmentData = new FakeEquipment { enhanceLv = 5 } };
        var field = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
        var r = ItemEditApplier.Apply(item, field, -3, player: null);
        r.Success.ShouldBeTrue();
        item.equipmentData!.enhanceLv.ShouldBe(0);
    }

    [Fact]
    public void Apply_AboveMax_ClampedToMax()
    {
        var item = new FakeItem { type = 0, equipmentData = new FakeEquipment { enhanceLv = 5 } };
        var field = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
        var r = ItemEditApplier.Apply(item, field, 99, player: null);
        r.Success.ShouldBeTrue();
        item.equipmentData!.enhanceLv.ShouldBe(9);
    }

    // ───── IsEquipped ─────

    [Fact]
    public void IsEquipped_EquipmentTrue()
    {
        var item = new FakeItem { equipmentData = new FakeEquipment { equiped = true } };
        ItemEditApplier.IsEquipped(item).ShouldBeTrue();
    }

    [Fact]
    public void IsEquipped_HorseTrue()
    {
        var item = new FakeItem { horseData = new FakeHorse { equiped = true } };
        ItemEditApplier.IsEquipped(item).ShouldBeTrue();
    }

    [Fact]
    public void IsEquipped_BothFalse_ReturnsFalse()
    {
        var item = new FakeItem
        {
            equipmentData = new FakeEquipment { equiped = false },
            horseData = new FakeHorse { equiped = false },
        };
        ItemEditApplier.IsEquipped(item).ShouldBeFalse();
    }

    [Fact]
    public void IsEquipped_NoSubData_ReturnsFalse()
    {
        var item = new FakeItem();   // 둘 다 null
        ItemEditApplier.IsEquipped(item).ShouldBeFalse();
    }
}
