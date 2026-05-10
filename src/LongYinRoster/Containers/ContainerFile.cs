using System.IO;
using System.Text;
using System.Text.Json;

namespace LongYinRoster.Containers;

/// <summary>
/// 컨테이너 디스크 file schema:
///   {
///     "_meta": { schemaVersion, containerIndex, containerName, ... },
///     "items": [ ItemData... ]
///   }
/// SlotFile 패턴 mirror — _meta + payload.
/// </summary>
public static class ContainerFile
{
    public sealed class ParsedContainer
    {
        public ContainerMetadata Metadata { get; init; } = new();
        public string ItemsJson { get; init; } = "[]";
    }

    public static string Compose(ContainerMetadata m, string itemsJson)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteStartObject("_meta");
            w.WriteNumber("schemaVersion",  m.SchemaVersion);
            w.WriteNumber("containerIndex", m.ContainerIndex);
            w.WriteString("containerName",  m.ContainerName);
            w.WriteString("userComment",    m.UserComment);
            w.WriteString("createdAt",      m.CreatedAt);
            w.WriteString("modVersion",     m.ModVersion);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        var head = Encoding.UTF8.GetString(ms.ToArray());
        int closing = head.LastIndexOf('}');
        return head.Substring(0, closing).TrimEnd() + ",\n  \"items\": " + itemsJson + "\n}";
    }

    public static ParsedContainer Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var m = new ContainerMetadata();
        if (root.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            m.SchemaVersion  = ReadInt(meta, "schemaVersion", 1);
            m.ContainerIndex = ReadInt(meta, "containerIndex", 0);
            m.ContainerName  = ReadStr(meta, "containerName", "");
            m.UserComment    = ReadStr(meta, "userComment", "");
            m.CreatedAt      = ReadStr(meta, "createdAt", "");
            m.ModVersion     = ReadStr(meta, "modVersion", "");
        }
        string items = "[]";
        if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            items = arr.GetRawText();
        return new ParsedContainer { Metadata = m, ItemsJson = items };
    }

    private static int    ReadInt(JsonElement e, string k, int def)    => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    private static string ReadStr(JsonElement e, string k, string def) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;

    /// <summary>
    /// v0.7.11 Cat 5B — itemsJson 의 (count, totalWeight) 계산. ContainerMetadata.ItemCount/TotalWeight 채움.
    /// 비정상 JSON 또는 weight 부재 시 0 반환 (silent — dropdown 표시는 부정확하지만 panic 안 함).
    /// </summary>
    public static (int Count, float TotalWeight) ComputeStats(string itemsJson)
    {
        if (string.IsNullOrEmpty(itemsJson)) return (0, 0f);
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return (0, 0f);
            int count = doc.RootElement.GetArrayLength();
            float weight = 0f;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("weight", out var w) && w.ValueKind == JsonValueKind.Number)
                    weight += (float)w.GetDouble();
            }
            return (count, weight);
        }
        catch
        {
            return (0, 0f);
        }
    }
}
