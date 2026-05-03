using Xunit;
using LongYinRoster.Core;

namespace LongYinRoster.Tests;

public class ItemReflectorTests
{
    private sealed class FakeItem
    {
        public int grade;
        public int quality;
        public string name = "";
    }

    [Fact]
    public void GetGradeOrder_reads_int_grade_field_when_present()
    {
        var item = new FakeItem { grade = 3, quality = 5 };
        Assert.Equal(3, ItemReflector.GetGradeOrder(item));
    }

    [Fact]
    public void GetQualityOrder_reads_int_quality_field_when_present()
    {
        var item = new FakeItem { grade = 0, quality = 4 };
        Assert.Equal(4, ItemReflector.GetQualityOrder(item));
    }

    [Fact]
    public void Returns_negative_one_for_missing_or_null()
    {
        var item = new object();   // grade/quality 없음
        Assert.Equal(-1, ItemReflector.GetGradeOrder(item));
        Assert.Equal(-1, ItemReflector.GetQualityOrder(item));
        Assert.Equal(-1, ItemReflector.GetGradeOrder(null));
        Assert.Equal(-1, ItemReflector.GetQualityOrder(null));
    }

    private sealed class FakeStringEnumItem
    {
        public string grade   = "";
        public string quality = "";
    }

    private sealed class FakePropItem
    {
        public int grade   { get; set; }
        public int quality { get; set; }
    }

    private sealed class FakeCategoryItem
    {
        public int    type    = 0;
        public int    subType = 0;
        public string name    = "";
    }

    [Fact]
    public void GetGradeOrder_reads_chinese_enum_string_via_GradeMap()
    {
        var item = new FakeStringEnumItem { grade = "劣" };
        Assert.Equal(0, ItemReflector.GetGradeOrder(item));
    }

    [Fact]
    public void GetGradeOrder_reads_korean_enum_string_via_GradeMap()
    {
        var item = new FakeStringEnumItem { grade = "절세" };
        Assert.Equal(5, ItemReflector.GetGradeOrder(item));
    }

    [Fact]
    public void GetQualityOrder_reads_chinese_enum_string_via_QualityMap()
    {
        var item = new FakeStringEnumItem { quality = "极" };
        Assert.Equal(5, ItemReflector.GetQualityOrder(item));
    }

    [Fact]
    public void GetGradeOrder_reads_property_not_just_field()
    {
        var item = new FakePropItem { grade = 4, quality = 2 };
        Assert.Equal(4, ItemReflector.GetGradeOrder(item));
        Assert.Equal(2, ItemReflector.GetQualityOrder(item));
    }

    [Fact]
    public void GetGradeOrder_returns_negative_one_for_unmapped_string()
    {
        var item = new FakeStringEnumItem { grade = "unknown_value" };
        Assert.Equal(-1, ItemReflector.GetGradeOrder(item));
    }

    [Fact]
    public void GetCategoryKey_returns_zero_padded_type_subType_form()
    {
        var item = new FakeCategoryItem { type = 1, subType = 23 };
        Assert.Equal("001.023", ItemReflector.GetCategoryKey(item));
    }

    [Fact]
    public void GetCategoryKey_returns_empty_for_null()
    {
        Assert.Equal("", ItemReflector.GetCategoryKey(null));
    }

    [Fact]
    public void GetNameRaw_returns_string_field_value()
    {
        var item = new FakeCategoryItem { name = "无名刀" };
        Assert.Equal("无名刀", ItemReflector.GetNameRaw(item));
    }

    [Fact]
    public void GetNameRaw_returns_empty_for_null()
    {
        Assert.Equal("", ItemReflector.GetNameRaw(null));
    }
}
