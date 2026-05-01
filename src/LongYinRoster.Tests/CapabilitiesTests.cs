using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CapabilitiesTests
{
    [Fact]
    public void AllOff_AllFalseIncludingAppearance()
    {
        var c = Capabilities.AllOff();
        c.Identity.ShouldBeFalse();
        c.ActiveKungfu.ShouldBeFalse();
        c.ItemList.ShouldBeFalse();
        c.SelfStorage.ShouldBeFalse();
        c.Appearance.ShouldBeFalse();
    }

    [Fact]
    public void AllOn_AllTrueIncludingAppearance()
    {
        var c = Capabilities.AllOn();
        c.Identity.ShouldBeTrue();
        c.ActiveKungfu.ShouldBeTrue();
        c.ItemList.ShouldBeTrue();
        c.SelfStorage.ShouldBeTrue();
        c.Appearance.ShouldBeTrue();
    }

    [Fact]
    public void ToString_IncludesAppearanceFlag()
    {
        var c = new Capabilities { Appearance = true };
        c.ToString().ShouldContain("Appearance=True");
    }
}
