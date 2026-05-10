using System;

namespace LongYinRoster.Containers;

/// <summary>
/// 컨테이너 file 의 _meta block. SlotMetadata 패턴 mirror.
/// </summary>
public sealed class ContainerMetadata
{
    public int    SchemaVersion   { get; set; } = 1;
    public int    ContainerIndex  { get; set; }
    public string ContainerName   { get; set; } = "";
    public string UserComment     { get; set; } = "";
    public string CreatedAt       { get; set; } = DateTimeOffset.Now.ToString("o");
    public string ModVersion      { get; set; } = "0.7.0";

    // v0.7.11 Cat 5B — dropdown 표시용 transient stats. 직렬화 안 함 (ContainerFile.Compose 가 _meta 만 write).
    // ContainerRepository.List() 가 ContainerFile.ComputeStats(itemsJson) 결과로 populate.
    public int   ItemCount   { get; set; }
    public float TotalWeight { get; set; }
}
