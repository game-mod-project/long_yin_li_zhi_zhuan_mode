using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerMetadataTests
{
    [Fact]
    public void Default_HasSensibleDefaults()
    {
        var m = new ContainerMetadata();
        m.SchemaVersion.ShouldBe(1);
        m.ContainerIndex.ShouldBe(0);
        m.ContainerName.ShouldBe("");
        m.UserComment.ShouldBe("");
        m.ModVersion.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void Default_ItemCountAndTotalWeight_AreZero()
    {
        // v0.7.11 Cat 5B — transient stats default to 0
        var m = new ContainerMetadata();
        m.ItemCount.ShouldBe(0);
        m.TotalWeight.ShouldBe(0f);
    }

    [Fact]
    public void ComputeStats_EmptyArray_ReturnsZero()
    {
        var (count, weight) = ContainerFile.ComputeStats("[]");
        count.ShouldBe(0);
        weight.ShouldBe(0f);
    }

    [Fact]
    public void ComputeStats_TwoItems_SumsWeight()
    {
        var (count, weight) = ContainerFile.ComputeStats(
            "[{\"itemID\":1,\"weight\":5},{\"itemID\":2,\"weight\":3.5}]");
        count.ShouldBe(2);
        weight.ShouldBe(8.5f);
    }

    [Fact]
    public void ComputeStats_MissingWeight_TreatedAsZero()
    {
        var (count, weight) = ContainerFile.ComputeStats(
            "[{\"itemID\":1},{\"itemID\":2,\"weight\":7}]");
        count.ShouldBe(2);
        weight.ShouldBe(7f);
    }

    [Fact]
    public void ComputeStats_InvalidJson_ReturnsZero()
    {
        var (count, weight) = ContainerFile.ComputeStats("not-json");
        count.ShouldBe(0);
        weight.ShouldBe(0f);
    }

    [Fact]
    public void ComputeStats_NullOrEmpty_ReturnsZero()
    {
        ContainerFile.ComputeStats("").Count.ShouldBe(0);
        ContainerFile.ComputeStats(null!).Count.ShouldBe(0);
    }
}
