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
}
