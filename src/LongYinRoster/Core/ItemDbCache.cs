using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — 7 카테고리 생성 가능 아이템 DB 열거 (lazy, thread-safe). SkillNameCache mirror.
/// 비급은 SkillNameCache 위임. DB property 이름은 인게임 spike 로 확정 (현재 후보값).
/// </summary>
public static class ItemDbCache
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static List<ItemGenEntry>? _cache;
    private static readonly object _lock = new();

    // 인게임 spike 로 확정할 DB property 이름 (현재 후보). (category, subType, dbProperty)
    private static readonly (ItemGenCategory Cat, int SubType, string DbProp)[] DbMap =
    {
        (ItemGenCategory.Equipment, 0, "weaponDataBase"),
        (ItemGenCategory.Equipment, 1, "armorDataBase"),
        (ItemGenCategory.Equipment, 2, "helmetDataBase"),
        (ItemGenCategory.Equipment, 3, "shoesDataBase"),
        (ItemGenCategory.Equipment, 4, "decorationDataBase"),
        (ItemGenCategory.Medicine,  0, "medDataBase"),
        (ItemGenCategory.Food,      0, "foodDataBase"),
        (ItemGenCategory.Material,  0, "materialDataBase"),
        (ItemGenCategory.Treasure,  0, "treasureDataBase"),
        (ItemGenCategory.Horse,     0, "horseDataBase"),
    };

    public static IReadOnlyList<ItemGenEntry> All()
    {
        EnsureBuilt();
        return _cache!;
    }

    public static void ResetForTests()
    {
        lock (_lock) { _cache = null; }
    }

    private static void EnsureBuilt()
    {
        if (_cache != null) return;
        lock (_lock)
        {
            if (_cache != null) return;
            _cache = BuildFromGame();
        }
    }

    private static List<ItemGenEntry> BuildFromGame()
    {
        var list = new List<ItemGenEntry>();
        try
        {
            var gdcType = Type.GetType("GameDataController, Assembly-CSharp");
            if (gdcType == null) { Logger.WarnOnce("ItemDbCache", "GameDataController 미발견"); return list; }
            var gdc = gdcType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null);
            if (gdc == null) { Logger.WarnOnce("ItemDbCache", "GDC.Instance null"); return list; }

            foreach (var (cat, sub, dbProp) in DbMap)
            {
                try { EnumerateDb(gdc, cat, sub, dbProp, list); }
                catch (Exception ex) { Logger.WarnOnce("ItemDbCache", $"{dbProp}: {ex.GetType().Name}: {ex.Message}"); }
            }

            // 비급 = SkillNameCache 위임 (type 0~8 → SubType)
            foreach (var (id, label) in SkillNameCache.AllOrdered())
            {
                list.Add(new ItemGenEntry
                {
                    Id = id, NameRaw = label, NameKr = label,
                    Category = ItemGenCategory.Book, SubType = SkillNameCache.GetType(id),
                });
            }
            Logger.Info($"ItemDbCache: built {list.Count} entries");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("ItemDbCache", $"BuildFromGame: {ex.GetType().Name}: {ex.Message}");
        }
        return list;
    }

    private static void EnumerateDb(object gdc, ItemGenCategory cat, int sub, string dbProp, List<ItemGenEntry> list)
    {
        var db = gdc.GetType().GetProperty(dbProp, F)?.GetValue(gdc);
        if (db == null) { Logger.WarnOnce("ItemDbCache", $"{dbProp} null"); return; }
        int n = IL2CppListOps.Count(db);
        for (int i = 0; i < n; i++)
        {
            var entry = IL2CppListOps.Get(db, i);
            if (entry == null) continue;
            string raw = ReadStr(entry, "name");
            if (string.IsNullOrEmpty(raw)) raw = ReadStr(entry, "itemName");
            int rare = ReadInt(entry, "rareLv");
            list.Add(new ItemGenEntry
            {
                Id = i, NameRaw = raw,
                NameKr = string.IsNullOrEmpty(raw) ? null : HangulDict.Translate(raw),
                Category = cat, SubType = sub, RareLvDefault = rare,
            });
        }
    }

    private static int ReadInt(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return Convert.ToInt32(p.GetValue(obj));
            var f = t.GetField(name, F);
            if (f != null) return Convert.ToInt32(f.GetValue(obj));
        }
        catch { }
        return 0;
    }

    private static string ReadStr(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj)?.ToString() ?? "";
            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj)?.ToString() ?? "";
        }
        catch { }
        return "";
    }
}
