using System;
using System.Collections.Generic;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.4 D-1 — ItemData + sub-data wrapper reflection helper.
/// `GetCuratedFields` 는 카테고리별 한글 라벨 매핑 (장비/비급/단약 우선 — Task 3).
/// `GetRawFields` 는 모든 reflection 필드 dump + IL2CPP wrapper meta 필터.
/// 본 module 은 UI 와 무관 — 단위 테스트 가능.
/// </summary>
public static class ItemDetailReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly HashSet<string> WRAPPER_META = new()
    {
        "ObjectClass", "Pointer", "WasCollected", "isWrapped", "pooledPtr",
    };

    private static readonly string[] SUBDATA_WRAPPERS =
    {
        "equipmentData", "medFoodData", "bookData", "treasureData", "materialData", "horseData",
    };

    /// <summary>
    /// 카테고리별 의미 있는 필드 → (한글 라벨, 표시 값) tuple list.
    /// 우선 cover: type=0 장비 / type=2 단약·음식 / type=3 비급.
    /// 미지원 카테고리 (treasure/material/horse) 는 빈 list — caller 가 raw fallback.
    /// 필드명은 v0.7.4 D-1 spike 결과 (docs/superpowers/dumps/2026-05-03-v0.7.4-subdata-spike.md) 기반.
    /// </summary>
    public static List<(string Label, string Value)> GetCuratedFields(object? item)
    {
        if (item == null) return new();
        int type = ReadInt(item, "type");
        return type switch
        {
            0 => GetEquipmentDetails(item),
            2 => GetMedFoodDetails(item),
            3 => GetBookDetails(item),
            4 => GetTreasureDetails(item),
            5 => GetMaterialDetails(item),
            6 => GetHorseDetails(item),
            _ => new(),   // unknown
        };
    }

    private static List<(string, string)> GetEquipmentDetails(object item)
    {
        var result = new List<(string, string)>();
        var ed = ReadFieldOrProperty(item.GetType(), item, "equipmentData");
        if (ed != null)
        {
            int enh = ReadInt(ed, "enhanceLv");
            if (enh > 0) result.Add(("강화", $"+{enh}"));
            result.Add(("착용중", ReadBool(ed, "equiped") ? "예" : "아니오"));
            int speEnh = ReadInt(ed, "speEnhanceLv");
            if (speEnh > 0) result.Add(("특수 강화", $"+{speEnh}"));
            int speWt = ReadInt(ed, "speWeightLv");
            if (speWt > 0) result.Add(("무게 경감", $"+{speWt}"));
        }
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    private static List<(string, string)> GetBookDetails(object item)
    {
        var result = new List<(string, string)>();
        var bd = ReadFieldOrProperty(item.GetType(), item, "bookData");
        if (bd != null)
        {
            int skillId = ReadInt(bd, "skillID");
            result.Add(("무공 ID", skillId.ToString()));
        }
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    private static List<(string, string)> GetMedFoodDetails(object item)
    {
        var result = new List<(string, string)>();
        var mf = ReadFieldOrProperty(item.GetType(), item, "medFoodData");
        if (mf != null)
        {
            int enh = ReadInt(mf, "enhanceLv");
            if (enh > 0) result.Add(("강화", $"+{enh}"));
            int randAdd = ReadInt(mf, "randomSpeAddValue");
            if (randAdd > 0) result.Add(("추가 보정", randAdd.ToString()));
            // changeHeroState / extraAddData 는 nested 객체 — v0.7.4.x 후속
        }
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    private static List<(string, string)> GetTreasureDetails(object item)
    {
        var result = new List<(string, string)>();
        var td = ReadFieldOrProperty(item.GetType(), item, "treasureData");
        if (td != null)
        {
            result.Add(("완전 감정", ReadBool(td, "fullIdentified") ? "예" : "아니오"));
            float ikn = ReadFloat(td, "identifyKnowledgeNeed");
            if (ikn > 0f) result.Add(("감정 필요 지식", $"{ikn:F0}"));
        }
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    private static List<(string, string)> GetMaterialDetails(object item)
    {
        var result = new List<(string, string)>();
        // materialData.extraAddData (HeroSpeAddData) 는 nested 객체 → raw 위임
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    private static List<(string, string)> GetHorseDetails(object item)
    {
        var result = new List<(string, string)>();
        var hd = ReadFieldOrProperty(item.GetType(), item, "horseData");
        if (hd != null)
        {
            result.Add(("착용중", ReadBool(hd, "equiped") ? "예" : "아니오"));
            AddBaseAdd(result, hd, "속도", "speed", "speedAdd");
            AddBaseAdd(result, hd, "힘", "power", "powerAdd");
            AddBaseAdd(result, hd, "스프린트", "sprint", "sprintAdd");
            AddBaseAdd(result, hd, "인내", "resist", "resistAdd");
            float mwa = ReadFloat(hd, "maxWeightAdd");
            if (mwa > 0f) result.Add(("최대무게 추가", $"+{mwa:F0}"));
            float favor = ReadFloat(hd, "favorRate");
            if (Math.Abs(favor - 1f) > 0.01f) result.Add(("호감 율", $"{favor:F2}"));
        }
        // 동적 필드 (nowPower / sprintTimeLeft / sprintTimeCd) 는 raw 섹션 위임
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    // 말 4 stat 의 base + Add 합산 표시 — Add=0 시 bare value, Add>0 시 "{base} (+{add})"
    private static void AddBaseAdd(List<(string, string)> result, object obj, string label, string baseField, string addField)
    {
        float baseVal = ReadFloat(obj, baseField);
        float addVal = ReadFloat(obj, addField);
        string val = addVal > 0f ? $"{baseVal:F0} (+{addVal:F0})" : $"{baseVal:F0}";
        result.Add((label, val));
    }

    private static int ReadInt(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj.GetType(), obj, name);
        if (v == null) return 0;
        try { return Convert.ToInt32(v); } catch { return 0; }
    }
    private static float ReadFloat(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj.GetType(), obj, name);
        if (v == null) return 0f;
        try { return Convert.ToSingle(v); } catch { return 0f; }
    }
    private static bool ReadBool(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj.GetType(), obj, name);
        return v is bool b && b;
    }

    /// <summary>
    /// item + 활성화 sub-data wrapper 의 모든 reflection 필드 dump.
    /// IL2CPP wrapper meta 필터, 비활성 wrapper 제외.
    /// </summary>
    public static List<(string FieldName, string Value)> GetRawFields(object? item)
    {
        var result = new List<(string, string)>();
        if (item == null) return result;

        DumpFields(result, "", item);
        foreach (var wrapperName in SUBDATA_WRAPPERS)
        {
            var wrapper = ReadFieldOrProperty(item.GetType(), item, wrapperName);
            if (wrapper == null) continue;   // inactive
            DumpFields(result, $"[{wrapperName}] ", wrapper);
        }
        return result;
    }

    private static void DumpFields(List<(string, string)> result, string prefix, object obj)
    {
        var t = obj.GetType();
        foreach (var f in t.GetFields(F))
        {
            if (WRAPPER_META.Contains(f.Name)) continue;
            if (Array.IndexOf(SUBDATA_WRAPPERS, f.Name) >= 0) continue;   // wrapper itself dumped via prefix path
            string val;
            try { val = f.GetValue(obj)?.ToString() ?? "null"; }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            result.Add(($"{prefix}{f.Name}", val));
        }
        foreach (var p in t.GetProperties(F))
        {
            if (WRAPPER_META.Contains(p.Name)) continue;
            if (p.GetIndexParameters().Length > 0) continue;   // skip indexers
            string val;
            try { val = p.GetValue(obj)?.ToString() ?? "null"; }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            result.Add(($"{prefix}{p.Name}", val));
        }
    }

    private static object? ReadFieldOrProperty(Type t, object obj, string name)
    {
        try
        {
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj);
            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj);
        }
        catch { /* swallow */ }
        return null;
    }
}
