using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CapabilitiesTests
{
    [Fact]
    public void AllOff_AllFalseIncludingKungfuList()
    {
        var c = Capabilities.AllOff();
        c.Identity.ShouldBeFalse();
        c.ActiveKungfu.ShouldBeFalse();
        c.ItemList.ShouldBeFalse();
        c.SelfStorage.ShouldBeFalse();
        c.Appearance.ShouldBeFalse();
        c.KungfuList.ShouldBeFalse();
    }

    [Fact]
    public void AllOn_AllTrueIncludingKungfuList()
    {
        var c = Capabilities.AllOn();
        c.Identity.ShouldBeTrue();
        c.ActiveKungfu.ShouldBeTrue();
        c.ItemList.ShouldBeTrue();
        c.SelfStorage.ShouldBeTrue();
        c.Appearance.ShouldBeTrue();
        c.KungfuList.ShouldBeTrue();
    }

    [Fact]
    public void ToString_IncludesAppearanceFlag()
    {
        var c = new Capabilities { Appearance = true };
        c.ToString().ShouldContain("Appearance=True");
    }

    [Fact]
    public void ToString_IncludesKungfuListFlag()
    {
        var c = new Capabilities { KungfuList = true };
        c.ToString().ShouldContain("KungfuList=True");
    }
}
