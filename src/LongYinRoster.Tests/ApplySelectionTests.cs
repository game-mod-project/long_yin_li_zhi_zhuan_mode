using LongYinRoster.Core;
using Xunit;

namespace LongYinRoster.Tests;

public class ApplySelectionTests
{
    [Fact]
    public void V03Default_HasFourCategoriesOn()
    {
        var sel = ApplySelection.V03Default();
        Assert.True(sel.Stat);
        Assert.True(sel.Honor);
        Assert.True(sel.TalentTag);
        Assert.True(sel.Skin);
        Assert.False(sel.SelfHouse);
        Assert.False(sel.Identity);
        Assert.False(sel.ActiveKungfu);
        Assert.False(sel.ItemList);
        Assert.False(sel.SelfStorage);
    }

    [Fact]
    public void RestoreAll_HasAllNineOn()
    {
        var sel = ApplySelection.RestoreAll();
        Assert.True(sel.Stat);
        Assert.True(sel.Honor);
        Assert.True(sel.TalentTag);
        Assert.True(sel.Skin);
        Assert.True(sel.SelfHouse);
        Assert.True(sel.Identity);
        Assert.True(sel.ActiveKungfu);
        Assert.True(sel.ItemList);
        Assert.True(sel.SelfStorage);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllNine()
    {
        var orig = new ApplySelection
        {
            Stat = true, Honor = false, TalentTag = true, Skin = false,
            SelfHouse = true, Identity = false, ActiveKungfu = true,
            ItemList = false, SelfStorage = true,
        };
        string json = ApplySelection.ToJson(orig);
        var parsed = ApplySelection.FromJson(json);

        Assert.Equal(orig.Stat,         parsed.Stat);
        Assert.Equal(orig.Honor,        parsed.Honor);
        Assert.Equal(orig.TalentTag,    parsed.TalentTag);
        Assert.Equal(orig.Skin,         parsed.Skin);
        Assert.Equal(orig.SelfHouse,    parsed.SelfHouse);
        Assert.Equal(orig.Identity,     parsed.Identity);
        Assert.Equal(orig.ActiveKungfu, parsed.ActiveKungfu);
        Assert.Equal(orig.ItemList,     parsed.ItemList);
        Assert.Equal(orig.SelfStorage,  parsed.SelfStorage);
    }

    [Fact]
    public void FromJson_MissingFields_FallsBackToV03Default()
    {
        // v0.2 / v0.3 슬롯 호환 — applySelection 자체가 없거나 partial
        var partial = ApplySelection.FromJson("{}");
        Assert.True(partial.Stat);
        Assert.True(partial.Honor);
        Assert.False(partial.Identity);
    }
}
