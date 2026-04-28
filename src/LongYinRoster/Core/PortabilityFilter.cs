using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LongYinRoster.Core;

/// <summary>
/// Apply 흐름에서 슬롯 데이터의 faction(문파)/runtime(위치·관계) 필드를 제거해 현재 게임의
/// 컨텍스트를 보존한다. 캐릭터 본질(스탯/무공/장비/평판) 만 교체.
///
/// IL2CPP 환경의 Newtonsoft type-identity 문제 (HANDOFF §4.1) 를 피하려고
/// System.Text.Json 으로 작성. 디스크에서 읽은 raw player JSON string 을 그대로 받아
/// 새 JSON string 을 반환.
/// </summary>
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
        using var doc = JsonDocument.Parse(fullPlayerJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return fullPlayerJson;

        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (_faction.Contains(prop.Name)) continue;
                if (_runtime.Contains(prop.Name)) continue;
                prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
