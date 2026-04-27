using System;
using System.IO;
using FluentAssertions;
using LongYinRoster.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class PortabilityFilterTests
{
    private static JObject Player =>
        JArray.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json")))
        [0] as JObject ?? throw new InvalidOperationException("heroID=0 not found");

    [Fact]
    public void StripForApply_Removes_All_Faction_Fields()
    {
        var filtered = JObject.Parse(PortabilityFilter.StripForApply(Player.ToString()));

        foreach (var k in new[] {
            "belongForceID", "skillForceID", "outsideForce",
            "forceJobType", "forceJobID", "forceJobCD", "branchLeaderAreaID",
            "thisMonthContribution", "lastMonthContribution",
            "thisYearContribution", "lastYearContribution", "lastFightContribution",
            "isLeader", "heroForceLv",
            "isGovern", "governLv", "governContribution",
            "isHornord", "hornorLv", "forceContribution",
            "forceMission", "servantForceID", "recruitByPlayer", "salary"
        })
        {
            filtered.ContainsKey(k).Should().BeFalse($"{k} is faction-related and must be stripped");
        }
    }

    [Fact]
    public void StripForApply_Removes_All_Runtime_Fields()
    {
        var filtered = JObject.Parse(PortabilityFilter.StripForApply(Player.ToString()));

        foreach (var k in new[] {
            "heroAIData", "heroAIDataArriveTargetRecord", "heroAISettingData",
            "atAreaID", "bigMapPos", "inSafeArea", "inPrison",
            "inTeam", "teamLeader", "teamMates",
            "missions", "plotNumCount", "missionNumCount",
            "Teacher", "Students", "Lover", "PreLovers",
            "Relatives", "Brothers", "Friends", "Haters"
        })
        {
            filtered.ContainsKey(k).Should().BeFalse($"{k} is runtime/relational and must be stripped");
        }
    }

    [Fact]
    public void StripForApply_Preserves_Core_Character_Fields()
    {
        var filtered = JObject.Parse(PortabilityFilter.StripForApply(Player.ToString()));

        foreach (var k in new[] {
            "heroID", "heroName", "heroFamilyName", "heroNickName", "isFemale",
            "age", "generation", "faceData", "skinID",
            "baseAttri", "totalAttri", "maxAttri",
            "baseFightSkill", "totalFightSkill",
            "baseLivingSkill", "totalLivingSkill",
            "hp", "maxhp", "power", "mana",
            "kungfuSkills", "itemListData", "selfStorage", "nowEquipment",
            "fame", "heroTagData", "heroTagPoint", "fightScore"
        })
        {
            filtered.ContainsKey(k).Should().BeTrue($"{k} is character-essential and must be preserved");
        }
    }

    [Fact]
    public void ExcludedFields_Has_45_Entries_Total()
    {
        // 24 faction + 21 runtime = 45
        // (Plan comment said "18 runtime / 42 total" but the actual runtime field
        // enumeration in StripForApply_Removes_All_Runtime_Fields contains 21
        // entries; the documented field lists are the source of truth.)
        PortabilityFilter.ExcludedFields.Count.Should().Be(45);
    }
}
