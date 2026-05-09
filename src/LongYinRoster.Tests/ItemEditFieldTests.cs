using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.7 Task 1 — ItemEditField parse / range / matrix.
/// </summary>
public class ItemEditFieldTests
{
    // ───── TryParse Int ─────

    [Theory]
    [InlineData("3", true, 3)]
    [InlineData("0", true, 0)]
    [InlineData("9", true, 9)]
    [InlineData("-1", false, 0)]    // < min
    [InlineData("10", false, 0)]    // > max
    [InlineData("foo", false, 0)]   // parse 실패
    [InlineData("", false, 0)]      // 빈 입력
    public void TryParseInt_RangeValidated(string input, bool expectOk, int expectVal)
    {
        var f = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
        var ok = f.TryParse(input, out object value, out string error);
        ok.ShouldBe(expectOk);
        if (ok) ((int)value).ShouldBe(expectVal);
        else error.ShouldNotBeNullOrEmpty();
    }

    // ───── TryParse Float ─────

    [Theory]
    [InlineData("0.5", true, 0.5f)]
    [InlineData("9.99", true, 9.99f)]
    [InlineData("0.01", true, 0.01f)]
    [InlineData("0.005", false, 0f)]  // < min 0.01
    [InlineData("10.0", false, 0f)]   // > max 9.99
    [InlineData("foo", false, 0f)]
    public void TryParseFloat_RangeValidated(string input, bool expectOk, float expectVal)
    {
        var f = new ItemEditField("horseData.favorRate", "호감 율", ItemEditFieldKind.Float, 0.01f, 9.99f);
        var ok = f.TryParse(input, out object value, out string error);
        ok.ShouldBe(expectOk);
        if (ok) ((float)value).ShouldBe(expectVal);
    }

    // ───── TryParse Bool ─────

    [Theory]
    [InlineData("true", true, true)]
    [InlineData("false", true, false)]
    [InlineData("1", true, true)]
    [InlineData("0", true, false)]
    [InlineData("yes", true, true)]
    [InlineData("예", true, true)]
    [InlineData("아니오", true, false)]
    [InlineData("foo", false, false)]
    public void TryParseBool_AcceptsCommonForms(string input, bool expectOk, bool expectVal)
    {
        var f = new ItemEditField("treasureData.fullIdentified", "완전 감정", ItemEditFieldKind.Bool, 0, 1);
        var ok = f.TryParse(input, out object value, out string error);
        ok.ShouldBe(expectOk);
        if (ok) ((bool)value).ShouldBe(expectVal);
    }

    // ───── Matrix ─────

    [Fact]
    public void Matrix_Equipment_HasSpeWeightOnly()
    {
        // v0.7.7 사용자 피드백 — enhanceLv / speEnhanceLv 단순 수치 수정은 게임 로직 상 무의미
        // (강화 = stat 추가 메커니즘). 매트릭스에서 제거. speWeightLv 만 유지.
        var fields = ItemEditFieldMatrix.ForCategory(0);
        fields.ShouldNotContain(f => f.Path == "equipmentData.enhanceLv");
        fields.ShouldNotContain(f => f.Path == "equipmentData.speEnhanceLv");
        fields.ShouldContain(f => f.Path == "equipmentData.speWeightLv");
    }

    [Fact]
    public void Matrix_Material_OnlyCommon()
    {
        var fields = ItemEditFieldMatrix.ForCategory(5);
        fields.Count.ShouldBe(3);
        fields.ShouldContain(f => f.Path == "rareLv");
        fields.ShouldContain(f => f.Path == "itemLv");
        fields.ShouldContain(f => f.Path == "value");
    }

    [Fact]
    public void Matrix_Horse_AllStats()
    {
        var fields = ItemEditFieldMatrix.ForCategory(6);
        fields.ShouldContain(f => f.Path == "horseData.speedAdd");
        fields.ShouldContain(f => f.Path == "horseData.powerAdd");
        fields.ShouldContain(f => f.Path == "horseData.sprintAdd");
        fields.ShouldContain(f => f.Path == "horseData.resistAdd");
        fields.ShouldContain(f => f.Path == "horseData.maxWeightAdd");
        fields.ShouldContain(f => f.Path == "horseData.favorRate");
    }

    [Fact]
    public void Matrix_Book_HasSkillID()
    {
        var fields = ItemEditFieldMatrix.ForCategory(3);
        fields.ShouldContain(f => f.Path == "bookData.skillID");
    }

    [Fact]
    public void Matrix_MedFood_HasEnhanceAndRandomAdd()
    {
        var fields = ItemEditFieldMatrix.ForCategory(2);
        fields.ShouldContain(f => f.Path == "medFoodData.enhanceLv");
        fields.ShouldContain(f => f.Path == "medFoodData.randomSpeAddValue");
    }

    [Fact]
    public void Matrix_Treasure_HasIdentifyFields()
    {
        var fields = ItemEditFieldMatrix.ForCategory(4);
        fields.ShouldContain(f => f.Path == "treasureData.fullIdentified");
        fields.ShouldContain(f => f.Path == "treasureData.identifyKnowledgeNeed");
    }

    [Fact]
    public void Matrix_UnknownType_Empty()
    {
        var fields = ItemEditFieldMatrix.ForCategory(99);
        fields.Count.ShouldBe(0);
    }

    [Fact]
    public void Matrix_AllCategories_IncludeCommonThree()
    {
        foreach (int t in new[] { 0, 2, 3, 4, 5, 6 })
        {
            var fields = ItemEditFieldMatrix.ForCategory(t);
            fields.ShouldContain(f => f.Path == "rareLv", $"type={t}");
            fields.ShouldContain(f => f.Path == "itemLv", $"type={t}");
            fields.ShouldContain(f => f.Path == "value",  $"type={t}");
        }
    }
}
