using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Slots;

public sealed class UnsupportedSchemaException : Exception
{
    public UnsupportedSchemaException(int actual)
        : base($"Unsupported slot schemaVersion={actual} (expected 1)") { }
}

public static class SlotFile
{
    public const int CurrentSchemaVersion = 1;

    public static void Write(string path, SlotPayload payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";

        var root = new JObject
        {
            ["_meta"]  = MetaToJson(payload.Meta),
            ["player"] = payload.Player,
        };

        File.WriteAllText(tmp, root.ToString(Formatting.Indented), System.Text.Encoding.UTF8);

        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }

    public static SlotPayload Read(string path)
    {
        var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var root = JObject.Parse(text);

        var metaTok = root["_meta"] as JObject;
        var sv = (int?)metaTok?["schemaVersion"] ?? -1;
        if (sv != CurrentSchemaVersion) throw new UnsupportedSchemaException(sv);

        var meta   = MetaFromJson(metaTok!);
        var player = (JObject)root["player"]!;

        return new SlotPayload { Meta = meta, Player = player };
    }

    private static JObject MetaToJson(SlotPayloadMeta m)
    {
        var summary = m.Summary;
        return new JObject
        {
            ["schemaVersion"]       = m.SchemaVersion,
            ["modVersion"]          = m.ModVersion,
            ["slotIndex"]           = m.SlotIndex,
            ["userLabel"]           = m.UserLabel,
            ["userComment"]         = m.UserComment,
            ["captureSource"]       = m.CaptureSource,
            ["captureSourceDetail"] = m.CaptureSourceDetail,
            ["capturedAt"]          = m.CapturedAt.ToString("o"),
            ["gameSaveVersion"]     = m.GameSaveVersion,
            ["gameSaveDetail"]      = m.GameSaveDetail,
            ["summary"] = new JObject
            {
                ["heroName"]         = summary.HeroName,
                ["heroNickName"]     = summary.HeroNickName,
                ["isFemale"]         = new JValue(summary.IsFemale),
                ["age"]              = summary.Age,
                ["generation"]       = summary.Generation,
                ["fightScore"]       = new JValue(summary.FightScore),
                ["kungfuCount"]      = summary.KungfuCount,
                ["kungfuMaxLvCount"] = summary.KungfuMaxLvCount,
                ["itemCount"]        = summary.ItemCount,
                ["storageCount"]     = summary.StorageCount,
                ["money"]            = summary.Money,
                ["talentCount"]      = summary.TalentCount,
            },
        };
    }

    private static SlotPayloadMeta MetaFromJson(JObject m)
    {
        var s = (JObject)m["summary"]!;
        var summary = new SlotMetadata(
            HeroName:         (string?)s["heroName"]         ?? "",
            HeroNickName:     (string?)s["heroNickName"]     ?? "",
            IsFemale:         (bool?)s["isFemale"]            ?? false,
            Age:              (int?)s["age"]                  ?? 0,
            Generation:       (int?)s["generation"]           ?? 1,
            FightScore:       (float?)s["fightScore"]         ?? 0f,
            KungfuCount:      (int?)s["kungfuCount"]          ?? 0,
            KungfuMaxLvCount: (int?)s["kungfuMaxLvCount"]     ?? 0,
            ItemCount:        (int?)s["itemCount"]            ?? 0,
            StorageCount:     (int?)s["storageCount"]         ?? 0,
            Money:            (long?)s["money"]               ?? 0L,
            TalentCount:      (int?)s["talentCount"]          ?? 0);

        var capturedAtStr = (string?)m["capturedAt"];
        var capturedAt = string.IsNullOrEmpty(capturedAtStr)
            ? default
            : DateTime.Parse(capturedAtStr, System.Globalization.CultureInfo.InvariantCulture,
                             System.Globalization.DateTimeStyles.RoundtripKind);

        return new SlotPayloadMeta(
            SchemaVersion:       (int?)m["schemaVersion"]    ?? 1,
            ModVersion:          (string?)m["modVersion"]    ?? "",
            SlotIndex:           (int?)m["slotIndex"]        ?? 0,
            UserLabel:           (string?)m["userLabel"]     ?? "",
            UserComment:         (string?)m["userComment"]   ?? "",
            CaptureSource:       (string?)m["captureSource"] ?? "",
            CaptureSourceDetail: (string?)m["captureSourceDetail"] ?? "",
            CapturedAt:          capturedAt,
            GameSaveVersion:     (string?)m["gameSaveVersion"] ?? "",
            GameSaveDetail:      (string?)m["gameSaveDetail"]  ?? "",
            Summary:             summary);
    }
}
