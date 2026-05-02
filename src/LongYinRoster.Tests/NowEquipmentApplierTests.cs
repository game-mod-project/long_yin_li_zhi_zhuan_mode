using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class NowEquipmentApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void Apply_RespectsNowEquipmentSelection_SkipsWhenOff()
    {
        var slot = ParseSlot(@"{ ""nowEquipment"": { ""weaponSaveRecord"": [34] } }");
        var sel = new ApplySelection { NowEquipment = false };
        var result = NowEquipmentApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }

    [Fact]
    public void Apply_HandlesMissingPlayer_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""nowEquipment"": { ""weaponSaveRecord"": [34] } }");
        var sel = new ApplySelection { NowEquipment = true };
        var result = NowEquipmentApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("player null");
    }

    [Fact]
    public void Apply_MissingNowEquipmentInSlot_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var sel = new ApplySelection { NowEquipment = true };
        var result = NowEquipmentApplier.Apply(player: new object(), slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("nowEquipment 미존재");
    }
}
