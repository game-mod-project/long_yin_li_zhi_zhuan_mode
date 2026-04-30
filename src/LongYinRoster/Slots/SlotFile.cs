using System;
using System.IO;
using System.Text;
using System.Text.Json;
using LongYinRoster.Core;

namespace LongYinRoster.Slots;

public sealed class UnsupportedSchemaException : Exception
{
    public UnsupportedSchemaException(int actual)
        : base($"Unsupported slot schemaVersion={actual} (expected 1)") { }
}

/// <summary>
/// 슬롯 디스크 I/O. Read/Write 모두 System.Text.Json 사용 — IL2CPP-bound Newtonsoft 의
/// type identity 충돌을 피한다. Player 는 raw JSON string 으로 root 에 inline inject.
/// </summary>
public static class SlotFile
{
    public const int CurrentSchemaVersion = 1;

    public static void Write(string path, SlotPayload payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";

        var sb = new StringBuilder();
        sb.Append("{\n  \"_meta\": ");
        sb.Append(SerializeMeta(payload.Meta));
        sb.Append(",\n  \"player\": ");
        sb.Append(string.IsNullOrWhiteSpace(payload.Player) ? "{}" : payload.Player);
        sb.Append("\n}\n");

        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }

    public static SlotPayload Read(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        if (!root.TryGetProperty("_meta", out var metaEl))
            throw new InvalidDataException("missing _meta");

        int sv = metaEl.TryGetProperty("schemaVersion", out var svEl)
                 && svEl.ValueKind == JsonValueKind.Number
                 && svEl.TryGetInt32(out var i) ? i : -1;
        if (sv != CurrentSchemaVersion) throw new UnsupportedSchemaException(sv);

        var meta = ParseMeta(metaEl);
        var player = root.TryGetProperty("player", out var pEl)
            ? pEl.GetRawText()
            : "{}";

        return new SlotPayload { Meta = meta, Player = player };
    }

    // ---------------------------------------------------------------- meta serialize

    private static string SerializeMeta(SlotPayloadMeta m)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions
        {
            Indented = true,
            Encoder  = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            var s = m.Summary;
            w.WriteStartObject();
            w.WriteNumber("schemaVersion",       m.SchemaVersion);
            w.WriteString("modVersion",          m.ModVersion ?? "");
            w.WriteNumber("slotIndex",           m.SlotIndex);
            w.WriteString("userLabel",           m.UserLabel ?? "");
            w.WriteString("userComment",         m.UserComment ?? "");
            w.WriteString("captureSource",       m.CaptureSource ?? "");
            w.WriteString("captureSourceDetail", m.CaptureSourceDetail ?? "");
            w.WriteString("capturedAt",          m.CapturedAt.ToString("o"));
            w.WriteString("gameSaveVersion",     m.GameSaveVersion ?? "");
            w.WriteString("gameSaveDetail",      m.GameSaveDetail ?? "");

            w.WriteStartObject("summary");
            w.WriteString("heroName",         s.HeroName ?? "");
            w.WriteString("heroNickName",     s.HeroNickName ?? "");
            w.WriteBoolean("isFemale",        s.IsFemale);
            w.WriteNumber("age",              s.Age);
            w.WriteNumber("generation",       s.Generation);
            w.WriteNumber("fightScore",       s.FightScore);
            w.WriteNumber("kungfuCount",      s.KungfuCount);
            w.WriteNumber("kungfuMaxLvCount", s.KungfuMaxLvCount);
            w.WriteNumber("itemCount",        s.ItemCount);
            w.WriteNumber("storageCount",     s.StorageCount);
            w.WriteNumber("money",            s.Money);
            w.WriteNumber("talentCount",      s.TalentCount);
            w.WriteEndObject();

            w.WriteStartObject("applySelection");
            w.WriteBoolean("stat",         m.ApplySelection.Stat);
            w.WriteBoolean("honor",        m.ApplySelection.Honor);
            w.WriteBoolean("talentTag",    m.ApplySelection.TalentTag);
            w.WriteBoolean("skin",         m.ApplySelection.Skin);
            w.WriteBoolean("selfHouse",    m.ApplySelection.SelfHouse);
            w.WriteBoolean("identity",     m.ApplySelection.Identity);
            w.WriteBoolean("activeKungfu", m.ApplySelection.ActiveKungfu);
            w.WriteBoolean("itemList",     m.ApplySelection.ItemList);
            w.WriteBoolean("selfStorage",  m.ApplySelection.SelfStorage);
            w.WriteEndObject();

            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // ---------------------------------------------------------------- meta parse

    private static SlotPayloadMeta ParseMeta(JsonElement m)
    {
        var s = m.GetProperty("summary");
        var summary = new SlotMetadata(
            HeroName:         GetStr(s, "heroName"),
            HeroNickName:     GetStr(s, "heroNickName"),
            IsFemale:         GetBool(s, "isFemale"),
            Age:              GetInt(s, "age"),
            Generation:       GetInt(s, "generation", 1),
            FightScore:       GetFloat(s, "fightScore"),
            KungfuCount:      GetInt(s, "kungfuCount"),
            KungfuMaxLvCount: GetInt(s, "kungfuMaxLvCount"),
            ItemCount:        GetInt(s, "itemCount"),
            StorageCount:     GetInt(s, "storageCount"),
            Money:            GetLong(s, "money"),
            TalentCount:      GetInt(s, "talentCount"));

        var capturedAtStr = GetStr(m, "capturedAt");
        var capturedAt = string.IsNullOrEmpty(capturedAtStr)
            ? default
            : DateTime.Parse(capturedAtStr,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);

        ApplySelection sel;
        if (m.TryGetProperty("applySelection", out var selEl))
            sel = ApplySelection.FromJsonElement(selEl);
        else
            sel = ApplySelection.V03Default();

        return new SlotPayloadMeta(
            SchemaVersion:       GetInt(m, "schemaVersion", 1),
            ModVersion:          GetStr(m, "modVersion"),
            SlotIndex:           GetInt(m, "slotIndex"),
            UserLabel:           GetStr(m, "userLabel"),
            UserComment:         GetStr(m, "userComment"),
            CaptureSource:       GetStr(m, "captureSource"),
            CaptureSourceDetail: GetStr(m, "captureSourceDetail"),
            CapturedAt:          capturedAt,
            GameSaveVersion:     GetStr(m, "gameSaveVersion"),
            GameSaveDetail:      GetStr(m, "gameSaveDetail"),
            Summary:             summary,
            ApplySelection:      sel);
    }

    private static string GetStr(JsonElement el, string key, string def = "") =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? def : def;

    private static int GetInt(JsonElement el, string key, int def = 0) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt32(out var i) ? i : def;

    private static long GetLong(JsonElement el, string key, long def = 0L) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt64(out var l) ? l : def;

    private static bool GetBool(JsonElement el, string key, bool def = false)
    {
        if (!el.TryGetProperty(key, out var v)) return def;
        return v.ValueKind switch
        {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            _                   => def,
        };
    }

    private static float GetFloat(JsonElement el, string key, float def = 0f) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetSingle(out var f) ? f : def;
}
