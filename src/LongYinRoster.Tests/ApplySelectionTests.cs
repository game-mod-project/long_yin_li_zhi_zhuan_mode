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
        Assert.False(sel.Appearance);   // v0.5 신규 — 기본값 false (v0.3 호환)
        Assert.False(sel.KungfuList);   // v0.5.2 신규 — 기본값 false
    }

    [Fact]
    public void RestoreAll_HasAllTenOn()
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
        Assert.True(sel.Appearance);    // v0.5 — RestoreAll 은 전체 on
        Assert.True(sel.KungfuList);    // v0.5.2 — RestoreAll 은 전체 on
    }

    [Fact]
    public void JsonRoundTrip_PreservesKungfuList_WhenTrue()
    {
        var orig = new ApplySelection { KungfuList = true };
        string json = ApplySelection.ToJson(orig);
        var parsed = ApplySelection.FromJson(json);
        Assert.True(parsed.KungfuList);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllFields_AppearanceFalse()
    {
        var orig = new ApplySelection
        {
            Stat = true, Honor = false, TalentTag = true, Skin = false,
            SelfHouse = true, Identity = false, ActiveKungfu = true,
            ItemList = false, SelfStorage = true, Appearance = false,
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
        Assert.Equal(orig.Appearance,   parsed.Appearance);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAppearance_WhenTrue()
    {
        var orig = new ApplySelection { Appearance = true };
        string json = ApplySelection.ToJson(orig);
        var parsed = ApplySelection.FromJson(json);
        Assert.True(parsed.Appearance);
    }

    [Fact]
    public void FromJson_MissingFields_FallsBackToV03Default()
    {
        // v0.2 / v0.3 슬롯 호환 — applySelection 자체가 없거나 partial
        var partial = ApplySelection.FromJson("{}");
        Assert.True(partial.Stat);
        Assert.True(partial.Honor);
        Assert.False(partial.Identity);
        Assert.False(partial.Appearance);   // 누락 시 default false 유지
    }

    [Fact]
    public void AnyEnabled_ReturnsTrueWhenOnlyAppearanceIsTrue()
    {
        var sel = new ApplySelection
        {
            Stat = false, Honor = false, TalentTag = false, Skin = false,
            SelfHouse = false, Identity = false, ActiveKungfu = false,
            ItemList = false, SelfStorage = false, Appearance = true,
        };
        Assert.True(sel.AnyEnabled());
    }
}
