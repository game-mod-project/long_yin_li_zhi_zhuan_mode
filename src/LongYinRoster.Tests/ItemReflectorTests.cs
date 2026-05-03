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
}
