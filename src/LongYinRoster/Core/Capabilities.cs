namespace LongYinRoster.Core;

/// <summary>
/// PinpointPatcher.Probe 결과 cache. Plugin 시작 시 1 회 결정 후 ModWindow 에 cache.
/// SlotDetailPanel 의 disabled 체크박스 결정에 사용.
///
/// v0.3 검증된 카테고리 (Stat / Honor / TalentTag / Skin / SelfHouse) 는
/// Capabilities 검사 안 함 — 항상 true 가정.
/// </summary>
public sealed class Capabilities
{
    public bool Identity     { get; init; }
    public bool ActiveKungfu { get; init; }
    public bool ItemList     { get; init; }
    public bool SelfStorage  { get; init; }
    public bool Appearance   { get; init; }   // v0.5 — portraitID + gender + sprite refresh
    public bool KungfuList   { get; init; }   // v0.5.2 — clear + add all

    public static Capabilities AllOff() => new();
    public static Capabilities AllOn() => new()
    {
        Identity = true, ActiveKungfu = true, ItemList = true, SelfStorage = true,
        Appearance = true, KungfuList = true,
    };

    public override string ToString() =>
        $"Identity={Identity} ActiveKungfu={ActiveKungfu} " +
        $"ItemList={ItemList} SelfStorage={SelfStorage} " +
        $"Appearance={Appearance} KungfuList={KungfuList}";
}
