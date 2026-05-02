using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class AppearanceApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void Apply_RespectsAppearanceSelection_SkipsWhenOff()
    {
        var slot = ParseSlot(@"{ ""faceData"": { ""faceID"": [1,2,3] } }");
        var sel = new ApplySelection { Appearance = false };
        var result = AppearanceApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }

    [Fact]
    public void Apply_HandlesMissingPlayer_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""faceData"": { ""faceID"": [1,2,3] } }");
        var sel = new ApplySelection { Appearance = true };
        var result = AppearanceApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("player null");
    }
}
