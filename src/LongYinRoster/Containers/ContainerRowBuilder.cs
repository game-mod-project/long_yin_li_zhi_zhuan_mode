using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using LongYinRoster.Core;
using LongYinRoster.UI;

namespace LongYinRoster.Containers;

/// <summary>
/// JSON ItemData array / 게임 IL2Cpp List → ContainerPanel.ItemRow list 변환.
/// </summary>
public static class ContainerRowBuilder
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static List<ContainerPanel.ItemRow> FromJsonArray(string itemsJson)
    {
        var list = new List<ContainerPanel.ItemRow>();
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            int i = 0;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                int type    = RI(e, "type");
                int subType = RI(e, "subType");
                string name = R(e, "name", "");
                // v0.7.2 spike: 게임 ItemData JSON 은 itemLv/rareLv 를 사용 (grade/quality 는 best-guess fallback).
                int grade   = (e.TryGetProperty("itemLv", out var glv) && glv.ValueKind == JsonValueKind.Number) ? glv.GetInt32()
                            : (e.TryGetProperty("grade",  out var gv)  && gv.ValueKind  == JsonValueKind.Number) ? gv.GetInt32()
                            : -1;
                int quality = (e.TryGetProperty("rareLv", out var qlv) && qlv.ValueKind == JsonValueKind.Number) ? qlv.GetInt32()
                            : (e.TryGetProperty("quality", out var qv) && qv.ValueKind  == JsonValueKind.Number) ? qv.GetInt32()
                            : -1;
                list.Add(new ContainerPanel.ItemRow
                {
                    Index        = i++,
                    Name         = name,
                    Type         = type,
                    SubType      = subType,
                    EnhanceLv    = ReadEnhance(e),
                    Weight       = RF(e, "weight"),
                    Equipped     = false,
                    CategoryKey  = $"{type:D3}.{subType:D3}",
                    NameRaw      = name,
                    GradeOrder   = grade,
                    QualityOrder = quality,
                });
            }
        }
        catch { }
        return list;
    }

    /// <summary>
    /// v0.7.4 D-1 — IL2Cpp List 의 raw item 객체를 원본 인덱스 정렬로 추출.
    /// ContainerPanel.SetFocus 는 row.Index (= 원본 IL2Cpp 인덱스) 를 저장 → GetFocusedRawItem 이
    /// raws[row.Index] 를 인덱싱하므로 null slot 은 List 안에서도 null 로 유지해야 정렬이 맞는다.
    /// (FromGameAllItem 은 null 을 skip 하지만 row.Index = i 로 원본 인덱스를 보존하므로 정합.)
    /// </summary>
    public static List<object> RawItemsFromGameAllItem(object il2List)
    {
        // List<object> 사용 (ContainerPanel.SetInventoryRows 시그니처 일치). null 슬롯은 null 그대로 보존.
        // GetFocusedRawItem 에서 raws[idx] 가 null 이면 null 반환 → DrawDetails 가 호출 안 됨 (DrawEmpty 분기).
        var raws = new List<object>();
        int n = IL2CppListOps.Count(il2List);
        for (int i = 0; i < n; i++)
        {
            raws.Add(IL2CppListOps.Get(il2List, i)!);   // null 도 그대로 저장 — 인덱스 정렬 보존
        }
        return raws;
    }

    public static List<ContainerPanel.ItemRow> FromGameAllItem(object il2List)
    {
        var list = new List<ContainerPanel.ItemRow>();
        int n = IL2CppListOps.Count(il2List);
        for (int i = 0; i < n; i++)
        {
            var item = IL2CppListOps.Get(il2List, i);
            if (item == null) continue;
            string name    = ReadString(item, "name");
            int type       = ReadInt(item, "type");
            int subType    = ReadInt(item, "subType");
            float weight   = ReadFloat(item, "weight");
            int enh        = 0;
            bool equipped  = false;
            var ed = ReadObj(item, "equipmentData");
            if (ed != null)
            {
                enh = ReadInt(ed, "enhanceLv");
                equipped = ReadBool(ed, "equiped");
            }
            else
            {
                var hd = ReadObj(item, "horseData");
                if (hd != null) equipped = ReadBool(hd, "equiped");
            }
            list.Add(new ContainerPanel.ItemRow
            {
                Index        = i,
                Name         = name,
                Type         = type,
                SubType      = subType,
                EnhanceLv    = enh,
                Weight       = weight,
                Equipped     = equipped,
                CategoryKey  = $"{type:D3}.{subType:D3}",
                NameRaw      = name,
                GradeOrder   = LongYinRoster.Core.ItemReflector.GetGradeOrder(item),
                QualityOrder = LongYinRoster.Core.ItemReflector.GetQualityOrder(item),
            });
        }
        return list;
    }

    private static int    RI(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    private static float  RF(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : 0f;
    private static string R (JsonElement e, string k, string def) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;

    private static int ReadEnhance(JsonElement e)
    {
        if (!e.TryGetProperty("equipmentData", out var ed) || ed.ValueKind != JsonValueKind.Object) return 0;
        return ed.TryGetProperty("enhanceLv", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    }

    private static object? ReadObj(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        return null;
    }
    private static int    ReadInt(object obj, string name)    { var v = ReadObj(obj, name); return v != null ? System.Convert.ToInt32(v) : 0; }
    private static float  ReadFloat(object obj, string name)  { var v = ReadObj(obj, name); return v != null ? System.Convert.ToSingle(v) : 0f; }
    private static string ReadString(object obj, string name) { var v = ReadObj(obj, name); return v as string ?? ""; }
    private static bool   ReadBool(object obj, string name)   { var v = ReadObj(obj, name); return v is bool b && b; }
}
