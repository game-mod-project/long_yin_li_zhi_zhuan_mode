using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemDetailReflectorCuratedTests
{
    // ===== Equipment (type=0) — spike fields: enhanceLv / equiped / speEnhanceLv / speWeightLv =====
    private sealed class FakeEquipmentData
    {
        public int enhanceLv = 3;
        public bool equiped = true;
        public int speEnhanceLv = 5;
        public int speWeightLv = 0;
    }
    private sealed class FakeEquipmentItem
    {
        public string name = "多情飞刀";
        public int type = 0;
        public int subType = 0;
        public int itemLv = 5;
        public int rareLv = 5;
        public float weight = 2.5f;
        public int value = 32000;
        public FakeEquipmentData equipmentData = new();
    }

    [Fact]
    public void GetCuratedFields_Equipment_ReturnsLabeledFields()
    {
        var item = new FakeEquipmentItem();
        var curated = ItemDetailReflector.GetCuratedFields(item);
        curated.ShouldNotBeEmpty();
        curated.ShouldContain(x => x.Label == "강화" && x.Value == "+3");
        curated.ShouldContain(x => x.Label == "착용중" && x.Value == "예");
        curated.ShouldContain(x => x.Label == "특수 강화" && x.Value == "+5");
        // speWeightLv = 0 → "무게 경감" 미포함 (조건부)
        curated.ShouldNotContain(x => x.Label == "무게 경감");
        curated.ShouldContain(x => x.Label == "무게" && x.Value == "2.5 kg");
        curated.ShouldContain(x => x.Label == "가격" && x.Value == "32000");
    }

    // ===== Book (type=3) — spike: skillID 단일 =====
    private sealed class FakeBookData { public int skillID = 287; }
    private sealed class FakeBookItem
    {
        public string name = "九阳神功";
        public int type = 3;
        public int subType = 0;
        public int itemLv = 4;
        public int rareLv = 4;
        public float weight = 1.0f;
        public int value = 50000;
        public FakeBookData bookData = new();
    }

    [Fact]
    public void GetCuratedFields_Book_ReturnsLabeledFields()
    {
        var item = new FakeBookItem();
        var curated = ItemDetailReflector.GetCuratedFields(item);
        curated.ShouldNotBeEmpty();
        curated.ShouldContain(x => x.Label == "무공 ID" && x.Value == "287");
        curated.ShouldContain(x => x.Label == "무게" && x.Value == "1.0 kg");
        curated.ShouldContain(x => x.Label == "가격" && x.Value == "50000");
    }

    // ===== Med/Food (type=2) — spike: enhanceLv / randomSpeAddValue =====
    private sealed class FakeMedFoodData
    {
        public int enhanceLv = 2;
        public int randomSpeAddValue = 10;
    }
    private sealed class FakeMedFoodItem
    {
        public string name = "九转还魂丹";
        public int type = 2;
        public int subType = 0;
        public int itemLv = 5;
        public int rareLv = 5;
        public float weight = 0.5f;
        public int value = 8000;
        public FakeMedFoodData medFoodData = new();
    }

    [Fact]
    public void GetCuratedFields_MedFood_ReturnsLabeledFields()
    {
        var item = new FakeMedFoodItem();
        var curated = ItemDetailReflector.GetCuratedFields(item);
        curated.ShouldNotBeEmpty();
        curated.ShouldContain(x => x.Label == "강화" && x.Value == "+2");
        curated.ShouldContain(x => x.Label == "추가 보정" && x.Value == "10");
        curated.ShouldContain(x => x.Label == "무게");
        curated.ShouldContain(x => x.Label == "가격");
    }

    // ===== Unsupported categories =====
    private sealed class FakeTreasureItem { public int type = 4; }
    private sealed class FakeMaterialItem { public int type = 5; }
    private sealed class FakeHorseItem { public int type = 6; }

    [Fact]
    public void GetCuratedFields_Treasure_ReturnsEmpty()
    {
        ItemDetailReflector.GetCuratedFields(new FakeTreasureItem()).ShouldBeEmpty();
    }

    [Fact]
    public void GetCuratedFields_Material_ReturnsEmpty()
    {
        ItemDetailReflector.GetCuratedFields(new FakeMaterialItem()).ShouldBeEmpty();
    }

    [Fact]
    public void GetCuratedFields_NullItem_ReturnsEmpty()
    {
        ItemDetailReflector.GetCuratedFields(null).ShouldBeEmpty();
    }
}
