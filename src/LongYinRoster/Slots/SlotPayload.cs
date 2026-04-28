using System;

namespace LongYinRoster.Slots;

/// <summary>슬롯 파일 1개의 _meta + player. 디스크 표현체.</summary>
/// <remarks>
/// Player 는 HeroData JSON 원문(raw string). JObject 의존을 회피해 IL2CPP 환경에서
/// IL2CPP-bound Newtonsoft 와 우리 어셈블리의 JObject 가 type identity 를 공유하지 않을 때
/// 발생하는 cast / indexer 실패를 영구히 우회한다. Apply 흐름은 raw string 을
/// SerializerService.Populate(reader, target) 의 입력으로 그대로 흘려보낸다.
/// </remarks>
public sealed class SlotPayload
{
    public SlotPayloadMeta Meta   { get; init; } = default!;
    public string          Player { get; init; } = "";
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
