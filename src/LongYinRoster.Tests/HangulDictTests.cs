using System.Collections.Generic;
using System.IO;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

// 정적 HangulDict 상태를 만지는 모든 test class 가 join — xUnit class-level 병렬 race 차단.
[CollectionDefinition("HangulDict")]
public class HangulDictTestCollection { }

[Collection("HangulDict")]
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

    // ===== v0.7.5.1 — ModFix TranslationEngine fn fallback =====

    [Fact]
    public void Translate_ModFixEngineFn_HandlesCompositeKey_AfterDictMiss()
    {
        // 모든 dict 가 miss 인데 engine fn 이 합성어 한글화 ("절세长矛" → "절세장모")
        HangulDict.SetModFixEngineFnForTests(cn => cn == "절세长矛" ? "절세장모" : null);
        HangulDict.Translate("절세长矛").ShouldBe("절세장모");
    }

    [Fact]
    public void Translate_ModFixEngineFn_NullResult_FallsThroughToRaw()
    {
        HangulDict.SetModFixEngineFnForTests(_ => null);
        HangulDict.Translate("미스").ShouldBe("미스");
    }

    [Fact]
    public void Translate_ModFixEngineFn_SameAsInput_FallsThroughToRaw()
    {
        // engine fn 이 입력 그대로 반환 (변환 안 됨) → raw fallback
        HangulDict.SetModFixEngineFnForTests(cn => cn);
        HangulDict.Translate("미스").ShouldBe("미스");
    }

    [Fact]
    public void Translate_DictHitWins_OverModFixEngineFn()
    {
        // dict hit 이면 engine fn 호출 안 함 (stage 1-3 우선)
        HangulDict.SetModFixDictForTests(new Dictionary<string, string> { { "测试", "딕셔너리" } });
        HangulDict.SetModFixEngineFnForTests(_ => "엔진");
        HangulDict.Translate("测试").ShouldBe("딕셔너리");
    }
}
