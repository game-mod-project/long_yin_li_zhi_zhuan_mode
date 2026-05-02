using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class KungfuListApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void ExtractKungfuList_ReturnsAllEntries()
    {
        var slot = ParseSlot(@"{
          ""kungfuSkills"": [
            {""skillID"": 100, ""lv"": 1, ""fightExp"": 0, ""bookExp"": 0, ""equiped"": false},
            {""skillID"": 200, ""lv"": 5, ""fightExp"": 100, ""bookExp"": 50, ""equiped"": true}
          ]
        }");
        var list = KungfuListApplier.ExtractKungfuList(slot);
        list.Count.ShouldBe(2);
        list[0].SkillID.ShouldBe(100);
        list[1].SkillID.ShouldBe(200);
        list[1].Lv.ShouldBe(5);
        list[1].FightExp.ShouldBe(100f);
    }

    [Fact]
    public void ExtractKungfuList_HandlesEmptyList()
    {
        var slot = ParseSlot(@"{ ""kungfuSkills"": [] }");
        var list = KungfuListApplier.ExtractKungfuList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractKungfuList_MissingKungfuSkills_ReturnsEmpty()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var list = KungfuListApplier.ExtractKungfuList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RespectsApplySelection_SkipsWhenFalse()
    {
        var slot = ParseSlot(@"{ ""kungfuSkills"": [{""skillID"": 100, ""lv"": 1}] }");
        var sel = new ApplySelection { KungfuList = false };
        var result = KungfuListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }

    [Fact]
    public void Apply_HandlesMissingPlayer_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""kungfuSkills"": [{""skillID"": 100, ""lv"": 1}] }");
        var sel = new ApplySelection { KungfuList = true };
        var result = KungfuListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("player null");
    }
}
