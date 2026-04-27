using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Core;

public static class PortabilityFilter
{
    private static readonly HashSet<string> _faction = new()
    {
        "belongForceID", "skillForceID", "outsideForce",
        "forceJobType", "forceJobID", "forceJobCD", "branchLeaderAreaID",
        "thisMonthContribution", "lastMonthContribution",
        "thisYearContribution", "lastYearContribution", "lastFightContribution",
        "isLeader", "heroForceLv",
        "isGovern", "governLv", "governContribution",
        "isHornord", "hornorLv", "forceContribution",
        "forceMission", "servantForceID", "recruitByPlayer", "salary",
    };

    private static readonly HashSet<string> _runtime = new()
    {
        "heroAIData", "heroAIDataArriveTargetRecord", "heroAISettingData",
        "atAreaID", "bigMapPos", "inSafeArea", "inPrison",
        "inTeam", "teamLeader", "teamMates",
        "missions", "plotNumCount", "missionNumCount",
        "Teacher", "Students", "Lover", "PreLovers",
        "Relatives", "Brothers", "Friends", "Haters",
    };

    public static IReadOnlyList<string> ExcludedFields { get; } = BuildExcludedList();

    private static List<string> BuildExcludedList()
    {
        var list = new List<string>(_faction.Count + _runtime.Count);
        list.AddRange(_faction);
        list.AddRange(_runtime);
        return list;
    }

    public static string StripForApply(string fullPlayerJson)
    {
        var obj = JObject.Parse(fullPlayerJson);
        foreach (var key in _faction)  obj.Remove(key);
        foreach (var key in _runtime)  obj.Remove(key);
        return obj.ToString(Newtonsoft.Json.Formatting.None);
    }
}
