using System;
using System.IO;
using FluentAssertions;
using LongYinRoster.Slots;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class SlotMetadataTests
{
    private static JObject Player =>
        JArray.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json")))
        [0] as JObject ?? throw new InvalidOperationException();

    [Fact]
    public void FromPlayerJson_Populates_All_Summary_Fields_From_Frozen_Fixture()
    {
        var meta = SlotMetadata.FromPlayerJson(Player);

        meta.HeroName.Should().Be("초한월");
        meta.HeroNickName.Should().Be("단월검");
        meta.IsFemale.Should().BeTrue();
        meta.Age.Should().Be(18);
        meta.Generation.Should().Be(1);
        meta.FightScore.Should().BeApproximately(353738.0f, 1f);
        meta.KungfuCount.Should().Be(130);
        meta.KungfuMaxLvCount.Should().Be(117);
        meta.ItemCount.Should().Be(156);
        meta.StorageCount.Should().Be(217);
        meta.Money.Should().Be(98179248);
        meta.TalentCount.Should().Be(16);
    }
}
