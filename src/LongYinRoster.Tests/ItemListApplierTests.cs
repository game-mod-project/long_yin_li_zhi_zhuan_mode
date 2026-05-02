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
    public void ExtractItemEntries_SkipsTrulyEmptySlots_ReturnsRealItems()
    {
        // v0.5.4 — itemID 무관 필터. type=0+name=""+모든 subData null 만 빈 슬롯.
        var slot = ParseSlot(@"{
          ""itemListData"": {
            ""allItem"": [
              {""itemID"": 0, ""type"": 0, ""name"": """"},
              {""itemID"": 0, ""type"": 0, ""name"": """", ""equipmentData"": null, ""bookData"": null},
              {""itemID"": 45, ""type"": 6, ""subType"": 0, ""itemLv"": 5, ""rareLv"": 5, ""weight"": 30, ""value"": 12800, ""name"": ""神凫马""},
              {""itemID"": 41, ""type"": 1, ""subType"": 2}
            ]
          }
        }");
        var list = ItemListApplier.ExtractItemEntries(slot);
        list.Count.ShouldBe(2);
        list[0].GetProperty("itemID").GetInt32().ShouldBe(45);
        list[1].GetProperty("itemID").GetInt32().ShouldBe(41);
    }

    [Fact]
    public void ExtractItemEntries_IncludesItemIdZeroWithName()
    {
        // v0.5.4 핵심 fix — 책 등 itemID=0 인 real item 포함
        var slot = ParseSlot(@"{
          ""itemListData"": {
            ""allItem"": [
              {""itemID"": 0, ""type"": 3, ""name"": ""多情飞刀"", ""bookData"": {""skillID"": 287}},
              {""itemID"": 0, ""type"": 0, ""name"": """"}
            ]
          }
        }");
        var list = ItemListApplier.ExtractItemEntries(slot);
        list.Count.ShouldBe(1);
        list[0].GetProperty("name").GetString().ShouldBe("多情飞刀");
    }

    [Fact]
    public void ExtractItemEntries_IncludesItemIdZeroWithSubData()
    {
        // type=0 + name="" 라도 subData 가 있으면 real item
        var slot = ParseSlot(@"{
          ""itemListData"": {
            ""allItem"": [
              {""itemID"": 0, ""type"": 0, ""name"": """", ""equipmentData"": {""enhanceLv"": 5}}
            ]
          }
        }");
        var list = ItemListApplier.ExtractItemEntries(slot);
        list.Count.ShouldBe(1);
    }

    [Fact]
    public void ExtractItemEntries_HandlesEmptyAllItem()
    {
        var slot = ParseSlot(@"{ ""itemListData"": { ""allItem"": [] } }");
        var list = ItemListApplier.ExtractItemEntries(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractItemEntries_MissingItemListData_ReturnsEmpty()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var list = ItemListApplier.ExtractItemEntries(slot);
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
