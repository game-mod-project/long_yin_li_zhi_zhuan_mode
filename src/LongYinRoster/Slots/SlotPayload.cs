using System;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Slots;

/// <summary>슬롯 파일 1개의 _meta + player. 디스크 표현체.</summary>
public sealed class SlotPayload
{
    public SlotPayloadMeta Meta   { get; init; } = default!;
    public JObject         Player { get; init; } = default!;
}

public sealed record SlotPayloadMeta(
    int      SchemaVersion,
    string   ModVersion,
    int      SlotIndex,
    string   UserLabel,
    string   UserComment,
    string   CaptureSource,         // "live" | "file"
    string   CaptureSourceDetail,
    DateTime CapturedAt,
    string   GameSaveVersion,
    string   GameSaveDetail,
    SlotMetadata Summary);
