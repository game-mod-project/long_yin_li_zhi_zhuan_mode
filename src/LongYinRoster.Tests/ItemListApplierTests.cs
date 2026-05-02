using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemListApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void ExtractItemList_SkipsEmptySlots_ReturnsRealItems()
    {
        var slot = ParseSlot(@"{
          ""itemListData"": {
            ""allItem"": [
              {""itemID"": 0, ""type"": 0},
              {""itemID"": 0, ""type"": 0},
              {""itemID"": 45, ""type"": 6, ""subType"": 0, ""itemLv"": 5, ""rareLv"": 5, ""weight"": 30, ""value"": 12800, ""name"": ""神凫马""},
              {""itemID"": 41, ""type"": 1, ""subType"": 2}
            ]
          }
        }");
        var list = ItemListApplier.ExtractItemList(slot);
        list.Count.ShouldBe(2);
        list[0].ItemID.ShouldBe(45);
        list[0].Type.ShouldBe(6);
        list[0].ItemLv.ShouldBe(5);
        list[1].ItemID.ShouldBe(41);
    }

    [Fact]
    public void ExtractItemList_HandlesEmptyAllItem()
    {
        var slot = ParseSlot(@"{ ""itemListData"": { ""allItem"": [] } }");
        var list = ItemListApplier.ExtractItemList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractItemList_MissingItemListData_ReturnsEmpty()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var list = ItemListApplier.ExtractItemList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RespectsApplySelection_SkipsWhenFalse()
    {
        var slot = ParseSlot(@"{ ""itemListData"": { ""allItem"": [{""itemID"": 45, ""type"": 6}] } }");
        var sel = new ApplySelection { ItemList = false };
        var result = ItemListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }

    [Fact]
    public void Apply_HandlesMissingPlayer_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""itemListData"": { ""allItem"": [{""itemID"": 45, ""type"": 6}] } }");
        var sel = new ApplySelection { ItemList = true };
        var result = ItemListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("player null");
    }
}
