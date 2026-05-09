using System;
using System.Reflection;
using HarmonyLib;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 1 — `HeroData.GetMaxTagNum()` Harmony Postfix.
///
/// cheat LongYinCheat.Patches.GameplayPatch.GetMaxTagNumPostfix (line 84-100) 의 100% mirror.
/// Plugin.Config.LockMaxTagNum=true + LockedMaxTagNumValue&gt;0 + __instance.heroID == player.heroID
/// 일 때 __result 를 LockedMaxTagNumValue 로 override.
///
/// Player 만 적용 — NPC 의 GetMaxTagNum 호출에는 무간섭 (mental model 분리,
/// brainstorm Q2=A 결정).
///
/// 등록 = manual via Plugin.cs Harmony.Patch (RestKeepHeroTagPatch mirror — generic
/// game type 은 attribute 기반 patch 불가).
///
/// 실제 분기 로직은 `GetMaxTagNumOverride.ApplyOverride(...)` 로 추출 — test 가능.
/// </summary>
public static class GetMaxTagNumPatch
{
    /// <summary>Plugin.cs 에서 manual register 호출.</summary>
    public static void Register(Harmony harmony)
    {
        try
        {
            var heroDataType = AccessTools.TypeByName("HeroData");
            if (heroDataType == null)
            {
                Logger.Warn("GetMaxTagNumPatch: HeroData type not found — skip");
                return;
            }
            var m = AccessTools.Method(heroDataType, "GetMaxTagNum");
            if (m == null)
            {
                Logger.Warn("GetMaxTagNumPatch: HeroData.GetMaxTagNum not found — skip");
                return;
            }
            var postfix = new HarmonyMethod(typeof(GetMaxTagNumPatch).GetMethod(
                nameof(Postfix),
                BindingFlags.Static | BindingFlags.Public));
            harmony.Patch(m, postfix: postfix);
            Logger.Info("GetMaxTagNumPatch: HeroData.GetMaxTagNum patched");
        }
        catch (Exception ex)
        {
            Logger.Warn($"GetMaxTagNumPatch.Register threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Harmony Postfix — `__instance` = caller HeroData, `__result` = original return.</summary>
    public static void Postfix(object __instance, ref int __result)
    {
        try
        {
            bool isLocked  = Config.LockMaxTagNum.Value;
            int  lockedVal = Config.LockedMaxTagNumValue.Value;
            var  player    = HeroLocator.GetPlayer();
            int  pid       = player == null ? -1 : GetMaxTagNumOverride.ReadHeroID(player);
            int  hid       = GetMaxTagNumOverride.ReadHeroID(__instance);
            GetMaxTagNumOverride.ApplyOverride(__instance, isLocked, lockedVal, pid, ref __result, hid);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("GetMaxTagNumPatch", $"Postfix threw: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
