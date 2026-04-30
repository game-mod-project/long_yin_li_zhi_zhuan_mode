using System;
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
    private static void ProbeActiveKungfu(object player) => Logger.Info("ProbeActiveKungfu: TBD A3");
    private static void ProbeItemData(object player) => Logger.Info("ProbeItemData: TBD A4");
    private static void ProbeItemListClear(object player) => Logger.Info("ProbeItemListClear: TBD A4");
}
