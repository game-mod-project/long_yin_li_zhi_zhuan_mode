using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.4 PoC 임시 진단 helper. Release 전 Task D16 에서 제거.
/// [F12] 핸들러가 mode 별로 다른 PoC 분기 호출.
///
/// PoC mode:
///   1. Identity        — heroName setter / backing field / Harmony 검증
///   2. ActiveKungfu    — kungfuSkills wrapper 찾기 + SetNowActiveSkill 호출
///   3. ItemData        — IntPtr ctor / static factory / GetItem hijack 후보
///   4. ItemListClear   — LoseAllItem 부수효과 검증
/// </summary>
public static class HeroDataDumpV04
{
    public enum Mode { Identity, ActiveKungfu, ItemData, ItemListClear }

    public static void Run(Mode mode)
    {
        var player = HeroLocator.GetPlayer();
        if (player == null) { Logger.Warn("HeroDataDumpV04: no player"); return; }

        try
        {
            switch (mode)
            {
                case Mode.Identity:        ProbeIdentity(player); break;
                case Mode.ActiveKungfu:    ProbeActiveKungfu(player); break;
                case Mode.ItemData:        ProbeItemData(player); break;
                case Mode.ItemListClear:   ProbeItemListClear(player); break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"HeroDataDumpV04({mode}) threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ProbeIdentity(object player)
    {
        var t = player.GetType();
        string original = (string)(t.GetProperty("heroName")?.GetValue(player) ?? "");
        Logger.Info($"ProbeIdentity: original heroName={original}");

        // 시도 A — setter 직접
        string testA = original + "_A";
        try
        {
            t.GetProperty("heroName")?.SetValue(player, testA);
            string after = (string)(t.GetProperty("heroName")?.GetValue(player) ?? "");
            Logger.Info($"ProbeIdentity A (setter): set='{testA}' got='{after}' " +
                        $"{(after == testA ? "PASS" : "FAIL silent no-op")}");
        }
        catch (Exception ex) { Logger.Warn($"ProbeIdentity A threw: {ex.Message}"); }

        // 시도 B — backing field
        var bf = t.GetField("<heroName>k__BackingField",
                             BindingFlags.NonPublic | BindingFlags.Instance)
              ?? t.GetField("_heroName",
                             BindingFlags.NonPublic | BindingFlags.Instance);
        if (bf != null)
        {
            string testB = original + "_B";
            try
            {
                bf.SetValue(player, testB);
                string after = (string)(t.GetProperty("heroName")?.GetValue(player) ?? "");
                Logger.Info($"ProbeIdentity B (backing field {bf.Name}): set='{testB}' got='{after}' " +
                            $"{(after == testB ? "PASS" : "FAIL")}");
            }
            catch (Exception ex) { Logger.Warn($"ProbeIdentity B threw: {ex.Message}"); }
        }
        else
        {
            Logger.Warn("ProbeIdentity B: backing field not found via standard names");
            // enumerate fields 모두 dump
            foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance |
                                           BindingFlags.Public | BindingFlags.FlattenHierarchy))
                if (f.Name.ToLowerInvariant().Contains("name"))
                    Logger.Info($"  field candidate: {f.Name} ({f.FieldType.Name})");
        }

        // 원래 값 복구 (게임 상태 오염 방지)
        try { t.GetProperty("heroName")?.SetValue(player, original); } catch { }
    }
    private static void ProbeActiveKungfu(object player)
    {
        var t = player.GetType();

        // 1) 현재 nowActiveSkill 읽기
        int currentID = (int)(t.GetProperty("nowActiveSkill")?.GetValue(player) ?? -1);
        Logger.Info($"ProbeActiveKungfu: current nowActiveSkill={currentID}");

        // 2) kungfuSkills list 의 entry 들 enumerate
        var ksList = t.GetProperty("kungfuSkills")?.GetValue(player);
        if (ksList == null) { Logger.Warn("kungfuSkills null"); return; }
        int n = IL2CppListOps.Count(ksList);
        Logger.Info($"  kungfuSkills count = {n}");

        object? testWrapper = null;
        int testID = -1;
        for (int i = 0; i < n; i++)
        {
            var entry = IL2CppListOps.Get(ksList, i);
            if (entry == null) continue;
            var idProp = entry.GetType().GetProperty("skillID")
                      ?? entry.GetType().GetProperty("ID")
                      ?? entry.GetType().GetProperty("id");
            var lvProp = entry.GetType().GetProperty("lv");
            int id = idProp != null ? (int)idProp.GetValue(entry)! : -1;
            int lv = lvProp != null ? (int)lvProp.GetValue(entry)! : -1;
            Logger.Info($"  [{i}] type={entry.GetType().Name} skillID={id} lv={lv}");

            // 첫 entry 를 test 후보 (currentID 아닌 것)
            if (testWrapper == null && id != currentID && id > 0) { testWrapper = entry; testID = id; }
        }

        if (testWrapper == null) { Logger.Warn("no test wrapper candidate"); return; }
        Logger.Info($"  test candidate: skillID={testID}");

        // 3) SetNowActiveSkill 호출
        var m = t.GetMethod("SetNowActiveSkill",
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Logger.Warn("SetNowActiveSkill missing"); return; }
        try
        {
            m.Invoke(player, new[] { testWrapper });
            int after = (int)(t.GetProperty("nowActiveSkill")?.GetValue(player) ?? -1);
            Logger.Info($"ProbeActiveKungfu: SetNowActiveSkill done — nowActiveSkill={after} " +
                        $"{(after == testID ? "PASS" : "FAIL — value not changed")}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"SetNowActiveSkill threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 원복
        if (currentID > 0)
        {
            // 원래 wrapper 다시 찾아서 복원
            for (int i = 0; i < n; i++)
            {
                var entry = IL2CppListOps.Get(ksList, i);
                if (entry == null) continue;
                int id = (int)(entry.GetType().GetProperty("skillID")?.GetValue(entry) ?? -1);
                if (id == currentID) { try { m.Invoke(player, new[] { entry }); } catch { } break; }
            }
        }
    }
    private static void ProbeItemData(object player)
    {
        // 1) ItemData type 찾기
        Type? itemDataType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var t in asm.GetTypes())
                    if (t.Name == "ItemData" && t.Namespace != "LongYinRoster.Core")
                    { itemDataType = t; break; }
            }
            catch { }
            if (itemDataType != null) break;
        }
        if (itemDataType == null) { Logger.Warn("ItemData type not found"); return; }
        Logger.Info($"ItemData type: {itemDataType.FullName}");

        // 2) ctors enumerate
        foreach (var ctor in itemDataType.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            var ps = ctor.GetParameters();
            var sig = string.Join(",", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"  ctor: ({sig})");
        }

        // 3) IntPtr ctor 존재 여부
        var ipCtor = itemDataType.GetConstructor(new[] { typeof(IntPtr) });
        if (ipCtor != null)
            Logger.Info("  IntPtr ctor exists — Il2CppInterop wrapper 패턴");

        // 4) static factory candidates
        foreach (var m in itemDataType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (m.ReturnType == itemDataType || m.ReturnType.Name == "ItemData")
                Logger.Info($"  static factory candidate: {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
        }

        // 5) game 안의 기존 ItemData 확인 (player.itemListData.allItem[0])
        var pt = player.GetType();
        var itemListData = pt.GetProperty("itemListData")?.GetValue(player);
        if (itemListData == null) { Logger.Warn("  player.itemListData null"); return; }
        var allItem = itemListData.GetType().GetProperty("allItem")?.GetValue(itemListData);
        if (allItem == null) { Logger.Warn("  player.itemListData.allItem null"); return; }
        int n = IL2CppListOps.Count(allItem);
        Logger.Info($"  player.itemListData.allItem count = {n}");
        if (n > 0)
        {
            var first = IL2CppListOps.Get(allItem, 0);
            Logger.Info($"    [0] type={first?.GetType().FullName}");
            if (first != null)
                foreach (var p in first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Logger.Info($"      .{p.Name} = {p.GetValue(first)}");
        }
    }
    private static void ProbeItemListClear(object player)
    {
        var t = player.GetType();
        var itemListData = t.GetProperty("itemListData")?.GetValue(player);
        var allItem = itemListData != null
            ? itemListData.GetType().GetProperty("allItem")?.GetValue(itemListData)
            : null;
        int preCount = allItem != null ? IL2CppListOps.Count(allItem) : -1;
        Logger.Info($"ProbeItemListClear: pre LoseAllItem allItem count={preCount}");

        var m = t.GetMethod("LoseAllItem", BindingFlags.Public | BindingFlags.Instance);
        if (m == null) { Logger.Warn("LoseAllItem missing"); return; }
        Logger.Info($"  LoseAllItem method found: {m}");

        // 주의 — destructive! 이 PoC 는 진단 only. 실제 clear 는 Phase B (RebuildItemList) 에서.
        Logger.Warn("ProbeItemListClear: NOT calling LoseAllItem (destructive). diagnostic only.");
    }
}
