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

    private static void ProbeIdentity(object player) => Logger.Info("ProbeIdentity: TBD A2");
    private static void ProbeActiveKungfu(object player) => Logger.Info("ProbeActiveKungfu: TBD A3");
    private static void ProbeItemData(object player) => Logger.Info("ProbeItemData: TBD A4");
    private static void ProbeItemListClear(object player) => Logger.Info("ProbeItemListClear: TBD A4");
}
