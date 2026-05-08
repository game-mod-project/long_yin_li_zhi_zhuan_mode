using System.Collections.Generic;
using System.IO;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class HangulDictTests
{
    public HangulDictTests() => HangulDict.ResetForTests();

    [Fact]
    public void Translate_Null_ReturnsEmpty()
    {
        HangulDict.Translate(null).ShouldBe("");
    }

    [Fact]
    public void Translate_Empty_ReturnsEmpty()
    {
        HangulDict.Translate("").ShouldBe("");
    }

    [Fact]
    public void Translate_Miss_ReturnsRaw()
    {
        HangulDict.Translate("不存在的词").ShouldBe("不存在的词");
    }

    [Fact]
    public void Translate_AlreadyKorean_ReturnsAsIs()
    {
        HangulDict.Translate("한글").ShouldBe("한글");
    }

    [Fact]
    public void Translate_HitInSelfDict_ReturnsKr()
    {
        var fake = new Dictionary<string, string> { { "测试", "테스트" } };
        HangulDict.SetSelfDictForTests(fake);
        HangulDict.Translate("测试").ShouldBe("테스트");
    }

    [Fact]
    public void Translate_HitInModFixDict_PreferredOverSelf()
    {
        var modfix = new Dictionary<string, string> { { "测试", "모드픽스" } };
        var self   = new Dictionary<string, string> { { "测试", "자체" } };
        HangulDict.SetModFixDictForTests(modfix);
        HangulDict.SetSelfDictForTests(self);
        HangulDict.Translate("测试").ShouldBe("모드픽스");
    }

    [Fact]
    public void Translate_HitInSiriusDict_PreferredOverSelf()
    {
        var sirius = new Dictionary<string, string> { { "测试", "시리우스" } };
        var self   = new Dictionary<string, string> { { "测试", "자체" } };
        HangulDict.SetSiriusDictForTests(sirius);
        HangulDict.SetSelfDictForTests(self);
        HangulDict.Translate("测试").ShouldBe("시리우스");
    }

    [Fact]
    public void Translate_ModFixWins_OverSirius()
    {
        var modfix = new Dictionary<string, string> { { "测试", "모드픽스" } };
        var sirius = new Dictionary<string, string> { { "测试", "시리우스" } };
        HangulDict.SetModFixDictForTests(modfix);
        HangulDict.SetSiriusDictForTests(sirius);
        HangulDict.Translate("测试").ShouldBe("모드픽스");
    }

    [Fact]
    public void EnsureInitialized_Idempotent()
    {
        HangulDict.EnsureInitialized();
        bool first = HangulDict.IsInitialized;
        HangulDict.EnsureInitialized();
        bool second = HangulDict.IsInitialized;
        first.ShouldBeTrue();
        second.ShouldBeTrue();
    }

    [Fact]
    public void LoadCsvLines_Skips_Blank_Lines()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "", "  ", "测试;테스트", "\t" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict.Count.ShouldBe(1);
        dict["测试"].ShouldBe("테스트");
    }

    [Fact]
    public void LoadCsvLines_Skips_NoSeparatorLines()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "noSeparatorHere", "测试;테스트", "trailingSep;" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict.Count.ShouldBe(1);
        dict["测试"].ShouldBe("테스트");
    }

    [Fact]
    public void LoadCsvLines_Unescapes_NewlineEscapes()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { @"测试\n第二行;테스트\n둘째줄" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict["测试\n第二行"].ShouldBe("테스트\n둘째줄");
    }

    [Fact]
    public void LoadCsvLines_Skips_KeyEqualsValue()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "same;same", "测试;테스트" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict.Count.ShouldBe(1);
    }

    [Fact]
    public void LoadCsvLines_AtSeparator_Works()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "key@value", "测试@테스트" };
        HangulDict.LoadCsvLinesForTests(lines, '@', dict);
        dict["key"].ShouldBe("value");
        dict["测试"].ShouldBe("테스트");
    }

    [Fact]
    public void LoadedCount_ReflectsSelfDictSize()
    {
        var fake = new Dictionary<string, string> { { "a", "A" }, { "b", "B" } };
        HangulDict.SetSelfDictForTests(fake);
        HangulDict.LoadedCount.ShouldBe(2);
    }
}
