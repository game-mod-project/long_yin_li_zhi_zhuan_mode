using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerFileTests
{
    [Fact]
    public void RoundTrip_PreservesMetadataAndItems()
    {
        var m = new ContainerMetadata { ContainerIndex = 3, ContainerName = "테스트" };
        string itemsJson = @"[{""itemID"":34,""type"":0,""name"":""검""},{""itemID"":0,""type"":3,""name"":""책""}]";
        var json = ContainerFile.Compose(m, itemsJson);
        var parsed = ContainerFile.Parse(json);
        parsed.Metadata.ContainerIndex.ShouldBe(3);
        parsed.Metadata.ContainerName.ShouldBe("테스트");
        parsed.ItemsJson.ShouldContain("\"name\":\"검\"");
        parsed.ItemsJson.ShouldContain("\"name\":\"책\"");
    }

    [Fact]
    public void Parse_HandlesEmptyItems()
    {
        var m = new ContainerMetadata { ContainerIndex = 1 };
        var json = ContainerFile.Compose(m, "[]");
        var parsed = ContainerFile.Parse(json);
        parsed.ItemsJson.ShouldBe("[]");
    }
}
