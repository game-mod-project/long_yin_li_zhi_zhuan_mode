using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class SelfStorageApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void ExtractStorageEntries_ReadsSelfStorageAllItem()
    {
        var slot = ParseSlot(@"{
          ""selfStorage"": {
            ""heroID"": -1, ""money"": 0,
            ""allItem"": [
              {""itemID"": 4, ""type"": 6, ""name"": ""劣马"", ""horseData"": {""speed"": 15.0}},
              {""itemID"": 0, ""type"": 0, ""name"": """"},
              {""itemID"": 0, ""type"": 3, ""name"": ""玉带诀"", ""bookData"": {""skillID"": 100}}
            ]
          }
        }");
        var list = SelfStorageApplier.ExtractStorageEntries(slot);
        list.Count.ShouldBe(2);
        list[0].GetProperty("name").GetString().ShouldBe("劣马");
        list[1].GetProperty("name").GetString().ShouldBe("玉带诀");
    }

    [Fact]
    public void ExtractStorageEntries_HandlesEmptyAllItem()
    {
        var slot = ParseSlot(@"{ ""selfStorage"": { ""allItem"": [] } }");
        var list = SelfStorageApplier.ExtractStorageEntries(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractStorageEntries_MissingSelfStorage_ReturnsEmpty()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var list = SelfStorageApplier.ExtractStorageEntries(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RespectsApplySelection_SkipsWhenFalse()
    {
        var slot = ParseSlot(@"{ ""selfStorage"": { ""allItem"": [{""itemID"": 4, ""type"": 6}] } }");
        var sel = new ApplySelection { SelfStorage = false };
        var result = SelfStorageApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }

    [Fact]
    public void Apply_HandlesMissingPlayer_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""selfStorage"": { ""allItem"": [{""itemID"": 4, ""type"": 6}] } }");
        var sel = new ApplySelection { SelfStorage = true };
        var result = SelfStorageApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("player null");
    }
}
