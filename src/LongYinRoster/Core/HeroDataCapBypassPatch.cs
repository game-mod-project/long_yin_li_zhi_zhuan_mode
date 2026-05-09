using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 3 — HeroData 의 자질값 cap (속성 120 / 무학 120 / 기예 100) 돌파 4 Postfix.
///
/// cheat LongYinCheat.Patches.MultiplierPatch (line 193-322) 100% mirror + player-only constraint.
/// Plugin.Config.EnableUncapMax + UncapMaxAttri/FightSkill/LivingSkill 으로 동작 제어.
///
/// **Patch 4 sites**:
/// - `HeroData.GetMaxAttri(int id)` Postfix → __result override
/// - `HeroData.GetMaxFightSkill(int id)` Postfix → 동상
/// - `HeroData.GetMaxLivingSkill(int id)` Postfix → 동상
/// - `HeroData.RefreshMaxAttriAndSkill()` Prefix/Postfix → snapshot + restore (refresh 중 user-set 값 보존)
///
/// **분기 logic** = `HeroDataCapBypassLogic.ApplyMaxOverride` (test 가능 추출).
///
/// 등록 = manual via Plugin.cs Harmony.Patch (RestKeepHeroTagPatch / GetMaxTagNumPatch mirror —
/// generic game type 은 attribute 기반 patch 불가).
/// </summary>
public static class HeroDataCapBypassPatch
{
    private const float ATTRI_BAR_CAP  = 120f;
    private const float FIGHT_BAR_CAP  = 120f;
    private const float LIVING_BAR_CAP = 100f;

    [ThreadStatic] private static float[]? _savedMaxAttri;
    [ThreadStatic] private static float[]? _savedMaxFight;
    [ThreadStatic] private static float[]? _savedMaxLiving;

    public static void Register(Harmony harmony)
    {
        try
        {
            var heroDataType = AccessTools.TypeByName("HeroData");
            if (heroDataType == null)
            {
                Logger.Warn("HeroDataCapBypassPatch: HeroData type not found — skip");
                return;
            }

            RegisterPostfix(harmony, heroDataType, "GetMaxAttri", nameof(MaxAttriPostfix));
            RegisterPostfix(harmony, heroDataType, "GetMaxFightSkill", nameof(MaxFightSkillPostfix));
            RegisterPostfix(harmony, heroDataType, "GetMaxLivingSkill", nameof(MaxLivingSkillPostfix));
            RegisterPair(harmony, heroDataType, "RefreshMaxAttriAndSkill",
                         nameof(RefreshMaxPrefix), nameof(RefreshMaxPostfix));
        }
        catch (Exception ex)
        {
            Logger.Warn($"HeroDataCapBypassPatch.Register threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RegisterPostfix(Harmony harmony, Type heroDataType, string methodName, string postfixName)
    {
        try
        {
            var m = AccessTools.Method(heroDataType, methodName);
            if (m == null) { Logger.Warn($"HeroDataCapBypassPatch: HeroData.{methodName} not found"); return; }
            // Priority.Last (=0) — LongYinCheat 의 RefreshMaxPostfix 가 EnableUncapMax=false 일 때 maxXxx 를
            // 120/100 으로 clamp 하므로, 우리 Postfix 가 마지막에 실행되어 cheat 의 clamp 를 덮어써야 한다.
            // Priority 가 작을수록 늦게 실행 (Harmony 규칙).
            var postfix = new HarmonyMethod(typeof(HeroDataCapBypassPatch).GetMethod(
                postfixName, BindingFlags.Static | BindingFlags.Public))
            {
                priority = Priority.Last,
            };
            harmony.Patch(m, postfix: postfix);
            Logger.Info($"HeroDataCapBypassPatch: HeroData.{methodName} patched (Priority.Last)");
        }
        catch (Exception ex)
        {
            Logger.Warn($"HeroDataCapBypassPatch.RegisterPostfix({methodName}) threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RegisterPair(Harmony harmony, Type heroDataType, string methodName,
                                     string prefixName, string postfixName)
    {
        try
        {
            var m = AccessTools.Method(heroDataType, methodName);
            if (m == null) { Logger.Warn($"HeroDataCapBypassPatch: HeroData.{methodName} not found"); return; }
            // Prefix = Priority.First (가장 먼저 snapshot — cheat 보다 먼저 원본 maxXxx 보존),
            // Postfix = Priority.Last (가장 마지막 restore — cheat 의 clamp 후 우리가 999 로 복원).
            var prefix  = new HarmonyMethod(typeof(HeroDataCapBypassPatch).GetMethod(
                prefixName, BindingFlags.Static | BindingFlags.Public))
            {
                priority = Priority.First,
            };
            var postfix = new HarmonyMethod(typeof(HeroDataCapBypassPatch).GetMethod(
                postfixName, BindingFlags.Static | BindingFlags.Public))
            {
                priority = Priority.Last,
            };
            harmony.Patch(m, prefix: prefix, postfix: postfix);
            Logger.Info($"HeroDataCapBypassPatch: HeroData.{methodName} patched (Prefix=First / Postfix=Last)");
        }
        catch (Exception ex)
        {
            Logger.Warn($"HeroDataCapBypassPatch.RegisterPair({methodName}) threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void MaxAttriPostfix(object __instance, int id, ref float __result)
        => ApplyAxis(__instance, ref __result, Config.UncapMaxAttri.Value, ATTRI_BAR_CAP);

    public static void MaxFightSkillPostfix(object __instance, int id, ref float __result)
        => ApplyAxis(__instance, ref __result, Config.UncapMaxFightSkill.Value, FIGHT_BAR_CAP);

    public static void MaxLivingSkillPostfix(object __instance, int id, ref float __result)
        => ApplyAxis(__instance, ref __result, Config.UncapMaxLivingSkill.Value, LIVING_BAR_CAP);

    private static void ApplyAxis(object instance, ref float result, int uncapValue, float gameCap)
    {
        try
        {
            bool isEnabled = Config.EnableUncapMax.Value;
            var player = HeroLocator.GetPlayer();
            int pid = player == null ? -1 : HeroDataCapBypassLogic.ReadHeroID(player);
            int hid = HeroDataCapBypassLogic.ReadHeroID(instance);
            HeroDataCapBypassLogic.ApplyMaxOverride(instance, isEnabled, uncapValue, gameCap,
                                                    pid, ref result, hid);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroDataCapBypassPatch", $"ApplyAxis threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void RefreshMaxPrefix(object __instance)
    {
        if (__instance == null) return;
        if (!Config.EnableUncapMax.Value) return;
        try
        {
            var player = HeroLocator.GetPlayer();
            if (player == null) return;
            if (HeroDataCapBypassLogic.ReadHeroID(__instance) != HeroDataCapBypassLogic.ReadHeroID(player)) return;
            _savedMaxAttri  = SnapshotList(__instance, "maxAttri");
            _savedMaxFight  = SnapshotList(__instance, "maxFightSkill");
            _savedMaxLiving = SnapshotList(__instance, "maxLivingSkill");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroDataCapBypassPatch", $"RefreshMaxPrefix threw: {ex.GetType().Name}: {ex.Message}");
            _savedMaxAttri = _savedMaxFight = _savedMaxLiving = null;
        }
    }

    public static void RefreshMaxPostfix(object __instance)
    {
        if (__instance == null) { _savedMaxAttri = _savedMaxFight = _savedMaxLiving = null; return; }
        try
        {
            if (!Config.EnableUncapMax.Value)
            {
                // Defensive clamp when uncap off — game's refresh might leave high values.
                ClampList(__instance, "maxAttri",       ATTRI_BAR_CAP);
                ClampList(__instance, "maxFightSkill",  FIGHT_BAR_CAP);
                ClampList(__instance, "maxLivingSkill", LIVING_BAR_CAP);
                return;
            }
            var player = HeroLocator.GetPlayer();
            if (player == null) return;
            if (HeroDataCapBypassLogic.ReadHeroID(__instance) != HeroDataCapBypassLogic.ReadHeroID(player)) return;
            RestoreList(__instance, "maxAttri",       _savedMaxAttri,  Config.UncapMaxAttri.Value);
            RestoreList(__instance, "maxFightSkill",  _savedMaxFight,  Config.UncapMaxFightSkill.Value);
            RestoreList(__instance, "maxLivingSkill", _savedMaxLiving, Config.UncapMaxLivingSkill.Value);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroDataCapBypassPatch", $"RefreshMaxPostfix threw: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _savedMaxAttri = _savedMaxFight = _savedMaxLiving = null;
        }
    }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static float[]? SnapshotList(object hero, string fieldName)
    {
        var list = ReadField(hero, fieldName);
        if (list == null) return null;
        var t = list.GetType();
        var countProp = t.GetProperty("Count", F);
        if (countProp == null) return null;
        int n = Convert.ToInt32(countProp.GetValue(list));
        var arr = new float[n];
        var indexer = t.GetProperty("Item", F);
        var getItem = t.GetMethod("get_Item", F, null, new[] { typeof(int) }, null);
        for (int i = 0; i < n; i++)
        {
            object? v = indexer != null
                ? indexer.GetValue(list, new object[] { i })
                : getItem?.Invoke(list, new object[] { i });
            arr[i] = v is float f ? f : Convert.ToSingle(v);
        }
        return arr;
    }

    private static void RestoreList(object hero, string fieldName, float[]? saved, int uncapMax)
    {
        if (saved == null) return;
        var list = ReadField(hero, fieldName);
        if (list == null) return;
        var t = list.GetType();
        var countProp = t.GetProperty("Count", F);
        if (countProp == null) return;
        int n = Convert.ToInt32(countProp.GetValue(list));
        int len = Math.Min(n, saved.Length);
        var setItem = t.GetMethod("set_Item", F, null, new[] { typeof(int), typeof(float) }, null);
        var indexer = t.GetProperty("Item", F);
        var getItem = t.GetMethod("get_Item", F, null, new[] { typeof(int) }, null);
        for (int i = 0; i < len; i++)
        {
            float restoreValue = Math.Min(saved[i], uncapMax);
            object? cur = indexer != null
                ? indexer.GetValue(list, new object[] { i })
                : getItem?.Invoke(list, new object[] { i });
            float curF = cur is float f ? f : Convert.ToSingle(cur);
            if (restoreValue > curF)
            {
                if (setItem != null) setItem.Invoke(list, new object[] { i, restoreValue });
                else if (indexer != null && indexer.CanWrite)
                    indexer.SetValue(list, restoreValue, new object[] { i });
            }
        }
    }

    private static void ClampList(object hero, string fieldName, float cap)
    {
        var list = ReadField(hero, fieldName);
        if (list == null) return;
        var t = list.GetType();
        var countProp = t.GetProperty("Count", F);
        if (countProp == null) return;
        int n = Convert.ToInt32(countProp.GetValue(list));
        var setItem = t.GetMethod("set_Item", F, null, new[] { typeof(int), typeof(float) }, null);
        var indexer = t.GetProperty("Item", F);
        var getItem = t.GetMethod("get_Item", F, null, new[] { typeof(int) }, null);
        for (int i = 0; i < n; i++)
        {
            object? cur = indexer != null
                ? indexer.GetValue(list, new object[] { i })
                : getItem?.Invoke(list, new object[] { i });
            float curF = cur is float f ? f : Convert.ToSingle(cur);
            if (curF > cap)
            {
                if (setItem != null) setItem.Invoke(list, new object[] { i, cap });
                else if (indexer != null && indexer.CanWrite)
                    indexer.SetValue(list, cap, new object[] { i });
            }
        }
    }

    private static object? ReadField(object obj, string name)
    {
        var t = obj.GetType();
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        return null;
    }
}
