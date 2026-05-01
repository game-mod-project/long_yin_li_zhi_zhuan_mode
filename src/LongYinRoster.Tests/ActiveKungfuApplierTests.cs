using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.5.1 — ActiveKungfuApplier 의 slot JSON 파싱 + selection gate unit tests.
/// IL2CPP 게임 측 호출 (EquipSkill / UnequipSkill / flag toggle) 은 mock 불가 — smoke 로만 검증.
/// </summary>
public class ActiveKungfuApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void ExtractEquippedSkillIDs_ReturnsAllEquippedTrueIDs()
    {
        var slot = ParseSlot(@"{
          ""kungfuSkills"": [
            {""skillID"": 100, ""equiped"": true},
            {""skillID"": 200, ""equiped"": false},
            {""skillID"": 300, ""equiped"": true}
          ]
        }");
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBe(new[] { 100, 300 });
    }

    [Fact]
    public void ExtractEquippedSkillIDs_HandlesEmptyActiveSet()
    {
        var slot = ParseSlot(@"{
          ""kungfuSkills"": [
            {""skillID"": 100, ""equiped"": false},
            {""skillID"": 200, ""equiped"": false}
          ]
        }");
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractEquippedSkillIDs_HandlesDuplicateSkillID_ReturnsOnce()
    {
        var slot = ParseSlot(@"{
          ""kungfuSkills"": [
            {""skillID"": 100, ""equiped"": true},
            {""skillID"": 100, ""equiped"": true}
          ]
        }");
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBe(new[] { 100 });
    }

    [Fact]
    public void ExtractEquippedSkillIDs_MissingKungfuSkills_ReturnsEmpty()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RespectsApplySelection_SkipsWhenFalse()
    {
        var slot = ParseSlot(@"{
          ""kungfuSkills"": [{""skillID"": 100, ""equiped"": true}]
        }");
        var sel = new ApplySelection { ActiveKungfu = false };
        var result = ActiveKungfuApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }
}
