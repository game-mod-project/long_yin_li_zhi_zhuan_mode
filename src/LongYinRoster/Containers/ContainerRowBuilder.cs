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
                int grade   = e.TryGetProperty("grade", out var gv)   && gv.ValueKind == JsonValueKind.Number ? gv.GetInt32() : -1;
                int quality = e.TryGetProperty("quality", out var qv) && qv.ValueKind == JsonValueKind.Number ? qv.GetInt32() : -1;
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
