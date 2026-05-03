using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.7.4 D-1 Spike — sub-data wrapper 필드 inventory.
/// 인벤토리의 각 카테고리 (type=0/2/3/4/5/6) 첫 1+ item 의 ItemData top-level
/// + 활성화 sub-data wrapper (equipmentData/medFoodData/bookData/treasureData/
/// materialData/horseData) 모든 public/non-public field + property dump.
/// 결과: BepInEx LogOutput.log 의 [v0.7.4 spike] 줄.
/// release 직전 (Task 9) ModWindow F12 wire-up 복원 + 본 클래스 제거.
/// </summary>
public static class ProbeSubData
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly string[] SUBDATA_WRAPPERS =
    {
        "equipmentData", "medFoodData", "bookData", "treasureData", "materialData", "horseData",
    };

    public static void Run()
    {
        Logger.Info("=== [v0.7.4 spike] ProbeSubData start ===");
        try
        {
            var player = HeroLocator.GetPlayer();
            if (player == null) { Logger.Warn("[v0.7.4 spike] player null"); return; }

            var inv = ReadObj(player, "itemListData");
            if (inv == null) { Logger.Warn("[v0.7.4 spike] itemListData null"); return; }

            var allItem = ReadObj(inv, "allItem");
            if (allItem == null) { Logger.Warn("[v0.7.4 spike] allItem null"); return; }

            int n = IL2CppListOps.Count(allItem);
            Logger.Info($"[v0.7.4 spike] allItem count = {n}");

            var seenTypes = new HashSet<int>();
            for (int i = 0; i < n && seenTypes.Count < 6; i++)
            {
                var item = IL2CppListOps.Get(allItem, i);
                if (item == null) continue;
                int type = ConvertInt(ReadObj(item, "type"));
                if (seenTypes.Contains(type)) continue;
                seenTypes.Add(type);

                string name = (ReadObj(item, "name") as string) ?? "?";
                Logger.Info($"[v0.7.4 spike] === item idx={i} type={type} name={name} ===");

                DumpFields("[item]", item);

                foreach (var wrapperName in SUBDATA_WRAPPERS)
                {
                    var wrapper = ReadObj(item, wrapperName);
                    if (wrapper == null) continue;
                    Logger.Info($"[v0.7.4 spike]   [{wrapperName}] non-null");
                    DumpFields($"  [{wrapperName}]", wrapper);
                }
            }
            Logger.Info($"[v0.7.4 spike] done — {seenTypes.Count}/6 categories sampled");
        }
        catch (Exception ex)
        {
            Logger.Warn($"[v0.7.4 spike] threw: {ex.GetType().Name}: {ex.Message}");
        }
        Logger.Info("=== [v0.7.4 spike] ProbeSubData end ===");
    }

    private static void DumpFields(string prefix, object obj)
    {
        var t = obj.GetType();
        foreach (var f in t.GetFields(F))
        {
            string val;
            try
            {
                var v = f.GetValue(obj);
                val = v?.ToString() ?? "null";
                if (val.Length > 100) val = val.Substring(0, 100) + "...";
            }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            Logger.Info($"[v0.7.4 spike] {prefix}   F {f.FieldType.Name} {f.Name} = {val}");
        }
        foreach (var p in t.GetProperties(F))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            string val;
            try
            {
                var v = p.GetValue(obj);
                val = v?.ToString() ?? "null";
                if (val.Length > 100) val = val.Substring(0, 100) + "...";
            }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            Logger.Info($"[v0.7.4 spike] {prefix}   P {p.PropertyType.Name} {p.Name} = {val}");
        }
    }

    private static object? ReadObj(object obj, string name)
    {
        var t = obj.GetType();
        try
        {
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj);
            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj);
        }
        catch { }
        return null;
    }

    private static int ConvertInt(object? v)
    {
        if (v == null) return 0;
        try { return Convert.ToInt32(v); } catch { return 0; }
    }
}
