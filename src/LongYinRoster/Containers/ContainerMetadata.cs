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
}
