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

    // 인게임 spike 로 확정한 DB property 이름. decorationDataBase/materialDataBase/treasureDataBase
    // 는 GameDataController 에 존재하지 않음 → AddSyntheticEntries/ProbeTreasures 로 대체.
    private static readonly (ItemGenCategory Cat, int SubType, string DbProp)[] DbMap =
    {
        (ItemGenCategory.Equipment, 0, "weaponDataBase"),
        (ItemGenCategory.Equipment, 1, "armorDataBase"),
        (ItemGenCategory.Equipment, 2, "helmetDataBase"),
        (ItemGenCategory.Equipment, 3, "shoesDataBase"),
        (ItemGenCategory.Medicine,  0, "medDataBase"),
        (ItemGenCategory.Food,      0, "foodDataBase"),
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
            AddSyntheticEntries(list);
            try { ProbeTreasures(list); } catch (Exception ex) { Logger.WarnOnce("ItemDbCache", $"ProbeTreasures: {ex.GetType().Name}: {ex.Message}"); }
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
        EnumerateDbEntries(db, cat, sub, list);
    }

    /// <summary>
    /// v0.7.13.1 — 게임 v1.1.0f5 부터 *DataBase 가 Dictionary&lt;int,T&gt;(키 = ID). IL2CppListOps.Entries 로
    /// List(index=ID, 구 게임) / Dictionary(key=ID) 를 같은 경로로 열거 — Count 범위 밖 ID 대역도 도달.
    /// </summary>
    internal static void EnumerateDbEntries(object db, ItemGenCategory cat, int sub, List<ItemGenEntry> list)
    {
        foreach (var (id, entry) in IL2CppListOps.Entries(db))
        {
            if (entry == null) continue;
            string raw = ReadStr(entry, "name");
            if (string.IsNullOrEmpty(raw)) raw = ReadStr(entry, "itemName");
            int rare = ReadInt(entry, "rareLv");
            list.Add(new ItemGenEntry
            {
                Id = id, NameRaw = raw,
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

    // v0.7.13 — DB 없는 type-생성 카테고리는 합성 entry (장식품/마구/재료).
    private static void AddSyntheticEntries(List<ItemGenEntry> list)
    {
        string[] decorations = { "향낭", "옥선", "반지", "옥패", "요대", "면구" };
        for (int i = 0; i < decorations.Length; i++)
            list.Add(new ItemGenEntry { Id = i, NameRaw = decorations[i], NameKr = decorations[i],
                Category = ItemGenCategory.Equipment, SubType = 4 });   // 장신구 secondary

        list.Add(new ItemGenEntry { Id = 0, NameRaw = "안구", NameKr = "안구",
            Category = ItemGenCategory.Equipment, SubType = 5 });       // 마구 secondary

        string[] materials = { "목재", "광석", "약재", "식재" };
        for (int i = 0; i < materials.Length; i++)
            list.Add(new ItemGenEntry { Id = i, NameRaw = materials[i], NameKr = materials[i],
                Category = ItemGenCategory.Material, SubType = i });
    }

    // v0.7.13 — 보물 type 개수 미상 → GenerateTreasure(t,1,1f) probe 로 자동 발견.
    // 반환 ItemData 의 name 을 읽어 entry 생성. 3회 연속 실패 시 중단 (최대 30).
    private static void ProbeTreasures(List<ItemGenEntry> list)
    {
        var gc = GameControllerLocator.GetGameController();
        if (gc == null) return;
        var m = FindTreasureMethod(gc.GetType());
        if (m == null) { Logger.WarnOnce("ItemDbCache", "GenerateTreasure(int,int,Single) 미발견"); return; }
        int consecutiveFail = 0;
        for (int t = 0; t < 30 && consecutiveFail < 3; t++)
        {
            object? item = null;
            try { item = m.Invoke(gc, new object[] { t, 1, 1f }); } catch { }
            string raw = item != null ? ReadStr(item, "name") : "";
            if (item == null || string.IsNullOrEmpty(raw)) { consecutiveFail++; continue; }
            consecutiveFail = 0;
            list.Add(new ItemGenEntry { Id = t, NameRaw = raw,
                NameKr = HangulDict.Translate(raw), Category = ItemGenCategory.Treasure, SubType = 0 });
        }
    }

    private static System.Reflection.MethodInfo? FindTreasureMethod(Type t)
    {
        foreach (var mi in t.GetMethods(F))
        {
            if (mi.Name != "GenerateTreasure") continue;
            var ps = mi.GetParameters();
            if (ps.Length == 3 && ps[0].ParameterType == typeof(int)
                && ps[1].ParameterType == typeof(int) && ps[2].ParameterType == typeof(float))
                return mi;
        }
        return null;
    }
}
