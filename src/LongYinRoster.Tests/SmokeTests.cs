using FluentAssertions;
using Xunit;
using System;
using System.IO;

namespace LongYinRoster.Tests;

public class SmokeTests
{
    [Fact]
    public void Fixture_File_Exists_And_Is_Json_Array()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json");
        File.Exists(path).Should().BeTrue();
        var firstChar = (char)File.ReadAllBytes(path)[0];
        firstChar.Should().Be('[', "Hero file is a JSON array of hero records");
    }
}
