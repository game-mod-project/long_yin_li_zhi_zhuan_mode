using System.Text;
using System.Text.Json;

namespace LongYinRoster.Core;

/// <summary>
/// 10-카테고리 selection. 슬롯 JSON 의 _meta.applySelection 으로 영속.
/// V03Default = v0.3 호환 (스탯/명예/천부/스킨 on, 신규 6 off — Appearance 포함).
/// RestoreAll = 10 카테고리 모두 on (slot 0 자동백업 복원 시).
/// </summary>
public sealed class ApplySelection
{
    public bool Stat        { get; set; } = true;
    public bool Honor       { get; set; } = true;
    public bool TalentTag   { get; set; } = true;
    public bool Skin        { get; set; } = true;
    public bool SelfHouse   { get; set; } = false;
    public bool Identity    { get; set; } = false;
    public bool ActiveKungfu{ get; set; } = false;
    public bool ItemList    { get; set; } = false;
    public bool SelfStorage { get; set; } = false;
    public bool Appearance  { get; set; } = false;
    public bool KungfuList  { get; set; } = false;   // v0.5.2

    public static ApplySelection V03Default() => new();

    public static ApplySelection RestoreAll() => new()
    {
        Stat = true, Honor = true, TalentTag = true, Skin = true,
        SelfHouse = true, Identity = true, ActiveKungfu = true,
        ItemList = true, SelfStorage = true, Appearance = true,
        KungfuList = true,
    };

    public bool AnyEnabled() =>
        Stat || Honor || TalentTag || Skin || SelfHouse ||
        Identity || ActiveKungfu || ItemList || SelfStorage || Appearance ||
        KungfuList;

    public static string ToJson(ApplySelection s)
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteBoolean("stat",         s.Stat);
            w.WriteBoolean("honor",        s.Honor);
            w.WriteBoolean("talentTag",    s.TalentTag);
            w.WriteBoolean("skin",         s.Skin);
            w.WriteBoolean("selfHouse",    s.SelfHouse);
            w.WriteBoolean("identity",     s.Identity);
            w.WriteBoolean("activeKungfu", s.ActiveKungfu);
            w.WriteBoolean("itemList",     s.ItemList);
            w.WriteBoolean("selfStorage",  s.SelfStorage);
            w.WriteBoolean("appearance",   s.Appearance);
            w.WriteBoolean("kungfuList",   s.KungfuList);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static ApplySelection FromJson(string json)
    {
        var s = V03Default();
        if (string.IsNullOrWhiteSpace(json)) return s;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return s;

        bool Read(string key, bool def) =>
            root.TryGetProperty(key, out var v) ? v.ValueKind == JsonValueKind.True : def;

        s.Stat         = Read("stat",         s.Stat);
        s.Honor        = Read("honor",        s.Honor);
        s.TalentTag    = Read("talentTag",    s.TalentTag);
        s.Skin         = Read("skin",         s.Skin);
        s.SelfHouse    = Read("selfHouse",    s.SelfHouse);
        s.Identity     = Read("identity",     s.Identity);
        s.ActiveKungfu = Read("activeKungfu", s.ActiveKungfu);
        s.ItemList     = Read("itemList",     s.ItemList);
        s.SelfStorage  = Read("selfStorage",  s.SelfStorage);
        s.Appearance   = Read("appearance",   s.Appearance);
        s.KungfuList   = Read("kungfuList",   s.KungfuList);
        return s;
    }

    public static ApplySelection FromJsonElement(JsonElement el)
    {
        var s = V03Default();
        if (el.ValueKind != JsonValueKind.Object) return s;

        bool Read(string key, bool def) =>
            el.TryGetProperty(key, out var v) ? v.ValueKind == JsonValueKind.True : def;

        s.Stat         = Read("stat",         s.Stat);
        s.Honor        = Read("honor",        s.Honor);
        s.TalentTag    = Read("talentTag",    s.TalentTag);
        s.Skin         = Read("skin",         s.Skin);
        s.SelfHouse    = Read("selfHouse",    s.SelfHouse);
        s.Identity     = Read("identity",     s.Identity);
        s.ActiveKungfu = Read("activeKungfu", s.ActiveKungfu);
        s.ItemList     = Read("itemList",     s.ItemList);
        s.SelfStorage  = Read("selfStorage",  s.SelfStorage);
        s.Appearance   = Read("appearance",   s.Appearance);
        s.KungfuList   = Read("kungfuList",   s.KungfuList);
        return s;
    }
}
