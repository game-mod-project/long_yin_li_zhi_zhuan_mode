using System.Text.Json;

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
    /// <summary>
    /// HeroData JSON 문자열에서 요약 메타를 추출한다. 입력은 SerializerService.Serialize 의
    /// 결과 (Newtonsoft 가 만든 JSON 문자열).
    ///
    /// 왜 string 입력 + System.Text.Json 인가:
    /// IL2CPP 환경에서 JObject.Parse 결과의 JArray 가 우리 컴파일된 표준 Newtonsoft 의 JArray 와
    /// type identity 가 달라 `as JArray` / `(IList&lt;JToken&gt;)` cast 가 silently null 또는 예외를
    /// 던졌다. System.Text.Json 은 .NET BCL 이라 cross-assembly type 충돌 없음.
    /// disk 저장은 JObject 를 그대로 사용하므로 (직렬화 자체는 정상) 두 경로를 분리한다.
    /// </summary>
    public static SlotMetadata FromPlayerJson(string playerJson)
    {
        using var doc = JsonDocument.Parse(playerJson);
        var p = doc.RootElement;

        int CountArray(string key) =>
            p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array
                ? v.GetArrayLength() : 0;

        int CountNestedArray(string parent, string child)
        {
            if (p.TryGetProperty(parent, out var l1) && l1.ValueKind == JsonValueKind.Object
                && l1.TryGetProperty(child, out var l2) && l2.ValueKind == JsonValueKind.Array)
                return l2.GetArrayLength();
            return 0;
        }

        int ksCount = CountArray("kungfuSkills");
        int ksMaxLv = 0;
        if (p.TryGetProperty("kungfuSkills", out var ks) && ks.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in ks.EnumerateArray())
            {
                if (s.ValueKind != JsonValueKind.Object) continue;
                if (s.TryGetProperty("lv", out var lv)
                    && lv.ValueKind == JsonValueKind.Number
                    && lv.TryGetInt32(out var lvi) && lvi >= 10) ksMaxLv++;
            }
        }
        int invCount = CountNestedArray("itemListData", "allItem");
        int stCount  = CountNestedArray("selfStorage",  "allItem");
        int tgCount  = CountArray("heroTagData");

        string GetString(string key) =>
            p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? "" : "";

        bool GetBool(string key) =>
            p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

        int GetInt(string key, int def = 0) =>
            p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                && v.TryGetInt32(out var i) ? i : def;

        float GetFloat(string key, float def = 0f) =>
            p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                && v.TryGetSingle(out var f) ? f : def;

        long money = 0L;
        if (p.TryGetProperty("itemListData", out var il) && il.ValueKind == JsonValueKind.Object
            && il.TryGetProperty("money", out var m) && m.ValueKind == JsonValueKind.Number
            && m.TryGetInt64(out var mv)) money = mv;

        return new SlotMetadata(
            HeroName:         GetString("heroName"),
            HeroNickName:     GetString("heroNickName"),
            IsFemale:         GetBool("isFemale"),
            Age:              GetInt("age"),
            Generation:       GetInt("generation", 1),
            FightScore:       GetFloat("fightScore"),
            KungfuCount:      ksCount,
            KungfuMaxLvCount: ksMaxLv,
            ItemCount:        invCount,
            StorageCount:     stCount,
            Money:            money,
            TalentCount:      tgCount
        );
    }
}
