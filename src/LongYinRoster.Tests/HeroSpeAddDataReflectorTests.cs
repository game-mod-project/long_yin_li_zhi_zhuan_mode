using System.Collections.Generic;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.7 — HeroSpeAddDataReflector POCO mock 검증.
/// 인게임 IL2CPP wrapper 동작은 smoke 만 (Set/Get/GetKeys/Dictionary.Remove).
/// </summary>
public class HeroSpeAddDataReflectorTests
{
    /// <summary>Spike 결과 mirror — Dictionary&lt;int, float&gt; heroSpeAddData (Property) + Get/Set/GetKeys.</summary>
    private sealed class FakeHeroSpeAddData
    {
        private readonly Dictionary<int, float> _dict = new();
        public Dictionary<int, float> heroSpeAddData => _dict;
        public float Get(int type) => _dict.TryGetValue(type, out var v) ? v : 0f;
        public FakeHeroSpeAddData Set(int type, float value) { _dict[type] = value; return this; }
        public List<int> GetKeys() => new List<int>(_dict.Keys);
    }

    [Fact]
    public void GetEntries_Empty_ReturnsEmpty()
    {
        var data = new FakeHeroSpeAddData();
        var entries = HeroSpeAddDataReflector.GetEntries(data);
        entries.ShouldBeEmpty();
    }

    [Fact]
    public void GetEntries_AfterSet_ReturnsEntry()
    {
        var data = new FakeHeroSpeAddData();
        data.Set(0, 5f);
        data.Set(6, 10f);
        var entries = HeroSpeAddDataReflector.GetEntries(data);
        entries.Count.ShouldBe(2);
        entries.ShouldContain(e => e.Type == 0 && e.Value == 5f);
        entries.ShouldContain(e => e.Type == 6 && e.Value == 10f);
    }

    [Fact]
    public void GetValue_HitMissing_Returns0()
    {
        var data = new FakeHeroSpeAddData();
        HeroSpeAddDataReflector.GetValue(data, 999).ShouldBe(0f);
    }

    [Fact]
    public void GetValue_HitExisting_ReturnsValue()
    {
        var data = new FakeHeroSpeAddData();
        data.Set(3, 7.5f);
        HeroSpeAddDataReflector.GetValue(data, 3).ShouldBe(7.5f);
    }

    [Fact]
    public void TrySet_NewEntry_Added()
    {
        var data = new FakeHeroSpeAddData();
        bool ok = HeroSpeAddDataReflector.TrySet(data, 5, 12f);
        ok.ShouldBeTrue();
        data.Get(5).ShouldBe(12f);
    }

    [Fact]
    public void TrySet_ExistingEntry_Updated()
    {
        var data = new FakeHeroSpeAddData();
        data.Set(5, 1f);
        HeroSpeAddDataReflector.TrySet(data, 5, 99f);
        data.Get(5).ShouldBe(99f);
    }

    [Fact]
    public void TryRemove_DictionaryRemove_RemovesEntry()
    {
        var data = new FakeHeroSpeAddData();
        data.Set(2, 5f);
        data.Set(7, 10f);
        bool ok = HeroSpeAddDataReflector.TryRemove(data, 2);
        ok.ShouldBeTrue();
        // POCO Dictionary 가 실제로 entry 제거 — GetEntries 에서 사라짐
        var entries = HeroSpeAddDataReflector.GetEntries(data);
        entries.ShouldNotContain(e => e.Type == 2);
        entries.ShouldContain(e => e.Type == 7);
    }

    [Fact]
    public void TrySet_NullData_ReturnsFalse()
    {
        bool ok = HeroSpeAddDataReflector.TrySet(null!, 0, 1f);
        ok.ShouldBeFalse();
    }

    [Fact]
    public void TryRemove_NullData_ReturnsFalse()
    {
        bool ok = HeroSpeAddDataReflector.TryRemove(null!, 0);
        ok.ShouldBeFalse();
    }

    [Fact]
    public void GetEntries_NullData_ReturnsEmpty()
    {
        var entries = HeroSpeAddDataReflector.GetEntries(null!);
        entries.ShouldBeEmpty();
    }
}

public class SpeAddTypeNamesTests
{
    [Theory]
    [InlineData(0, "근력")]
    [InlineData(6, "내공")]
    [InlineData(24, "의술")]
    [InlineData(54, "단조잠재")]
    public void Get_KnownIdx_ReturnsKoreanLabel(int idx, string expected)
    {
        SpeAddTypeNames.Get(idx).ShouldBe(expected);
    }

    [Fact]
    public void Get_UnknownIdx_ReturnsFallback()
    {
        SpeAddTypeNames.Get(999).ShouldBe("기타(999)");
    }

    [Fact]
    public void AllOrdered_HasAllEntries()
    {
        var all = SpeAddTypeNames.AllOrdered();
        // v0.7.7 — LongYinCheat 의 풀 dump (134 entries, 0~207, 일부 idx 빠짐 — 117~128, 131~132, 135~163 등)
        all.Count.ShouldBe(134);
        all[0].Type.ShouldBe(0);
        all[0].Label.ShouldBe("근력");
        // 마지막 idx 207 = 장비부하
        all[all.Count - 1].Type.ShouldBe(207);
        all[all.Count - 1].Label.ShouldBe("장비부하");
    }

    [Theory]
    [InlineData(55, "연약잠재")]
    [InlineData(57, "최대생명")]
    [InlineData(60, "공격력")]
    [InlineData(82, "독소")]
    [InlineData(116, "천갑")]
    [InlineData(207, "장비부하")]
    public void Get_ExtendedIdx_ReturnsKoreanLabel(int idx, string expected)
    {
        SpeAddTypeNames.Get(idx).ShouldBe(expected);
    }
}
