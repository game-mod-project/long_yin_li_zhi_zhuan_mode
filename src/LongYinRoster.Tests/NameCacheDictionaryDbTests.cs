using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LongYinRoster.Core;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.13.1 — GameDataController.*DataBase 가 Dictionary&lt;int,T&gt; 인 게임 v1.1.0f5 에서
/// 4 캐시(ItemDbCache / SkillNameCache / HeroTagNameCache / ForceNameCache)가 키 기반으로 전 항목을 읽는지.
/// 게임 부재 환경이므로 DB 객체를 직접 주입하는 internal 시임을 POCO Dictionary 로 검증.
/// (ReadInt/ReadStr 은 property → field 순으로 reflection 하므로 POCO 는 public property.)
/// </summary>
public class NameCacheDictionaryDbTests
{
    private sealed class ItemPoco  { public string name { get; set; } = ""; public int rareLv { get; set; } }
    private sealed class SkillPoco { public int skillID { get; set; } public string name { get; set; } = ""; public int type { get; set; } public int rareLv { get; set; } public int belongForceID { get; set; } }
    private sealed class TagPoco   { public string name { get; set; } = ""; public int value { get; set; } public string category { get; set; } = ""; public string sameMeaning { get; set; } = ""; public int order { get; set; } }
    private sealed class ForcePoco { public int forceID { get; set; } public string forceName { get; set; } = ""; }

    // ── ItemDbCache ────────────────────────────────────────────────────────────

    [Fact]
    public void ItemDbCache_EnumerateDbEntries_UsesDictionaryKeyAsId()
    {
        var db = new Dictionary<int, ItemPoco>
        {
            [7]    = new ItemPoco { name = "铁剑", rareLv = 1 },
            [1500] = new ItemPoco { name = "龙泉剑", rareLv = 5 },   // Count(=2) 범위 밖 키
        };
        var list = new List<ItemGenEntry>();

        ItemDbCache.EnumerateDbEntries(db, ItemGenCategory.Equipment, 0, list);

        list.Select(e => e.Id).Should().BeEquivalentTo(new[] { 7, 1500 });
        var sword = list.Single(e => e.Id == 1500);
        sword.NameRaw.Should().Be("龙泉剑");
        sword.RareLvDefault.Should().Be(5);
        sword.Category.Should().Be(ItemGenCategory.Equipment);
        sword.SubType.Should().Be(0);
    }

    [Fact]
    public void ItemDbCache_EnumerateDbEntries_UsesIndexAsId_ForList()
    {
        // 구 게임(List DB) 동작 보존 — index == ID
        var db = new List<ItemPoco> { new() { name = "a" }, new() { name = "b" } };
        var list = new List<ItemGenEntry>();

        ItemDbCache.EnumerateDbEntries(db, ItemGenCategory.Medicine, 0, list);

        list.Select(e => (e.Id, e.NameRaw)).Should().Equal((0, "a"), (1, "b"));
    }

    // ── SkillNameCache ─────────────────────────────────────────────────────────

    [Fact]
    public void SkillNameCache_BuildFromDb_ReadsSkillIdFromEntry_AndFallsBackToKey()
    {
        var db = new Dictionary<int, SkillPoco>
        {
            [1600] = new SkillPoco { skillID = 1600, name = "冰焰狂天舞", type = 4, rareLv = 5, belongForceID = 3 },  // 커스텀 무공 대역
            [42]   = new SkillPoco { skillID = 0,    name = "无名" },                                           // skillID 0 → 키로 fallback
        };
        var dict = new Dictionary<int, string>();
        var typeDict = new Dictionary<int, int>();
        var rareDict = new Dictionary<int, int>();
        var forceDict = new Dictionary<int, int>();

        SkillNameCache.BuildFromDb(db, dict, typeDict, rareDict, forceDict);

        dict.Keys.Should().BeEquivalentTo(new[] { 1600, 42 });
        typeDict[1600].Should().Be(4);
        rareDict[1600].Should().Be(5);
        forceDict[1600].Should().Be(3);
    }

    // ── HeroTagNameCache ───────────────────────────────────────────────────────

    [Fact]
    public void HeroTagNameCache_BuildFromDb_UsesDictionaryKeyAsTagId()
    {
        var db = new Dictionary<int, TagPoco>
        {
            [3]   = new TagPoco { name = "勤奋", value = 2, category = "武学", sameMeaning = "g1", order = 1 },
            [900] = new TagPoco { name = "天才", value = 8, category = "天生", sameMeaning = "g2", order = 2 },
        };
        var meta = new Dictionary<int, HeroTagNameCache.TagMeta>();
        var catOrder = new List<string>();

        HeroTagNameCache.BuildFromDb(db, meta, catOrder);

        meta.Keys.Should().BeEquivalentTo(new[] { 3, 900 });
        meta[900].TagID.Should().Be(900);
        meta[900].Value.Should().Be(8);
        meta[900].SameMeaning.Should().Be("g2");
        meta[3].Order.Should().Be(1);
        catOrder.Should().HaveCount(2);
    }

    // ── ForceNameCache ─────────────────────────────────────────────────────────

    [Fact]
    public void ForceNameCache_BuildFromDb_ReadsForceIdFromEntry()
    {
        var db = new Dictionary<int, ForcePoco>
        {
            [12] = new ForcePoco { forceID = 12, forceName = "华山派" },
        };
        var dict = new Dictionary<int, string>();

        ForceNameCache.BuildFromDb(db, dict);

        dict.Should().ContainKey(12);
        dict[12].Should().NotBeNullOrEmpty();
    }
}
