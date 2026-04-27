using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Slots;

public sealed record SlotMetadata(
    string   HeroName,
    string   HeroNickName,
    bool     IsFemale,
    int      Age,
    int      Generation,
    float    FightScore,
    int      KungfuCount,
    int      KungfuMaxLvCount,
    int      ItemCount,
    int      StorageCount,
    long     Money,
    int      TalentCount)
{
    public static SlotMetadata FromPlayerJson(JObject player)
    {
        var ks  = player["kungfuSkills"] as JArray ?? new JArray();
        var inv = player["itemListData"]?["allItem"] as JArray ?? new JArray();
        var st  = player["selfStorage"]?["allItem"] as JArray ?? new JArray();
        var tg  = player["heroTagData"] as JArray ?? new JArray();

        var ksList  = (IList<JToken>)ks;
        var invList = (IList<JToken>)inv;
        var stList  = (IList<JToken>)st;
        var tgList  = (IList<JToken>)tg;

        int ksCount = ksList.Count;
        int ksMaxLv = 0;
        for (int i = 0; i < ksCount; i++)
        {
            if (((int?)ksList[i]["lv"] ?? 0) >= 10) ksMaxLv++;
        }
        int invCount = invList.Count;
        int stCount  = stList.Count;
        int tgCount  = tgList.Count;

        return new SlotMetadata(
            HeroName:         (string?)player["heroName"]      ?? "",
            HeroNickName:     (string?)player["heroNickName"]  ?? "",
            IsFemale:         (bool?)player["isFemale"]        ?? false,
            Age:              (int?)player["age"]              ?? 0,
            Generation:       (int?)player["generation"]       ?? 1,
            FightScore:       (float?)player["fightScore"]     ?? 0f,
            KungfuCount:      ksCount,
            KungfuMaxLvCount: ksMaxLv,
            ItemCount:        invCount,
            StorageCount:     stCount,
            Money:            (long?)player["itemListData"]?["money"] ?? 0L,
            TalentCount:      tgCount
        );
    }
}
