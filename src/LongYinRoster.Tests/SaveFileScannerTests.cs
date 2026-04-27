using System;
using System.IO;
using FluentAssertions;
using LongYinRoster.Slots;
using Xunit;

namespace LongYinRoster.Tests;

public class SaveFileScannerTests
{
    private static string Fixture =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json");

    [Fact]
    public void ParseHeader_Extracts_HeroName_FightScore_From_First_4KB()
    {
        var hdr = SaveFileScanner.ParseHeader(Fixture, headerByteLimit: 262144);

        hdr.HeroName.Should().Be("초한월");
        hdr.HeroNickName.Should().Be("단월검");
        hdr.FightScore.Should().BeApproximately(353738f, 1f);
    }

    [Fact]
    public void ParseHeader_Returns_Empty_On_Truncated_Json()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trunc.json");
        File.WriteAllText(tmp, @"[{""heroID"":0,""heroName"":""");
        try
        {
            var hdr = SaveFileScanner.ParseHeader(tmp, headerByteLimit: 262144);
            hdr.HeroName.Should().Be("");
            hdr.HeroNickName.Should().Be("");
        }
        finally { File.Delete(tmp); }
    }
}
