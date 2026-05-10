using System.Collections.Generic;
using HarmonyLib;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// HeroData.ManageTagTime() Harmony Prefix/Postfix.
///
/// Bug: 휴식(rest) 명령 후 ManageTagTime() 이 heroTagData IL2CPP list 를 clear/rebuild
/// 하면서 PinpointPatcher 가 inject 한 천부(태그) 가 모두 사라짐.
///
/// Fix: player(heroID==0) 전용 — 진입 직전 heroTagData snapshot, 종료 후 list 가
/// snapshot 보다 작아졌으면 snapshot 의 tag 객체들을 다시 추가(restore).
/// NPC 의 ManageTagTime 에는 무간섭 (heroID != 0 이면 즉시 return).
/// </summary>
public static class RestKeepHeroTagPatch
{
    // ManageTagTime snapshot — player heroID==0 only. Unity is single-threaded.
    private static List<object>? _snapshot;
    // ClearAllTempTag snapshot — 별도 (메소드 nesting 가능성 대비).
    private static List<object>? _catSnapshot;

    /// <summary>
    /// PinpointPatcher.Apply 가 ClearAllTempTag 를 의도적으로 호출 시 true. 휴식 routine 의
    /// ClearAllTempTag (Apply 외부) 만 wipe → restore 하도록 분기. 진단 결과 (Phase 4 iteration 2):
    /// ManageTagTime Postfix 후 ClearAllTempTag 가 17 → 0 으로 추가 wipe.
    /// </summary>
    public static bool ApplyInProgress;

    // -------------------------------------------------------------------------
    // Registration (manual Harmony patch — game type 은 attribute 로 참조 불가)
    // -------------------------------------------------------------------------
    public static void Register(HarmonyLib.Harmony harmony)
    {
        try
        {
            var heroDataType = AccessTools.TypeByName("HeroData");
            if (heroDataType == null)
            {
                Logger.Warn("RestKeepHeroTagPatch: HeroData type not found — skip");
                return;
            }

            RegisterPair(harmony, heroDataType, "ManageTagTime",  nameof(Prefix),                nameof(Postfix));
            RegisterPair(harmony, heroDataType, "ClearAllTempTag", nameof(Prefix_ClearAllTempTag), nameof(Postfix_ClearAllTempTag));
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Register threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RegisterPair(HarmonyLib.Harmony harmony, System.Type heroDataType,
                                     string methodName, string prefixName, string postfixName)
    {
        try
        {
            var m = AccessTools.Method(heroDataType, methodName);
            if (m == null) { Logger.Warn($"RestKeepHeroTagPatch: HeroData.{methodName} not found"); return; }
            var prefix  = new HarmonyMethod(typeof(RestKeepHeroTagPatch).GetMethod(
                prefixName,  System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
            var postfix = new HarmonyMethod(typeof(RestKeepHeroTagPatch).GetMethod(
                postfixName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
            harmony.Patch(m, prefix: prefix, postfix: postfix);
            Logger.Info($"RestKeepHeroTagPatch: HeroData.{methodName} patched");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.RegisterPair({methodName}) threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // ClearAllTempTag — 휴식 routine 의 wipe 차단 (Apply 의 ClearAllTempTag 는 통과)
    // -------------------------------------------------------------------------
    public static void Prefix_ClearAllTempTag(object __instance)
    {
        _catSnapshot = null;
        try
        {
            if (GetHeroID(__instance) != 0) return;
            if (ApplyInProgress) return;     // Apply 의 의도된 clear — snapshot 안 남김

            var heroTagData = GetHeroTagData(__instance);
            if (heroTagData == null) return;

            int count = IL2CppListOps.Count(heroTagData);
            var snap  = new List<object>(count);
            for (int i = 0; i < count; i++)
            {
                var tag = IL2CppListOps.Get(heroTagData, i);
                if (tag != null) snap.Add(tag);
            }
            _catSnapshot = snap;
            // v0.7.10.2 — verbose snapshot Info 제거 (휴식 routine 매 호출마다 logging 폭주 원인)
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Prefix_ClearAllTempTag threw: {ex.GetType().Name}: {ex.Message}");
            _catSnapshot = null;
        }
    }

    public static void Postfix_ClearAllTempTag(object __instance)
    {
        try
        {
            if (_catSnapshot == null) return;     // Prefix skipped (NPC, ApplyInProgress, throw)
            if (GetHeroID(__instance) != 0) return;

            var heroTagData = GetHeroTagData(__instance);
            if (heroTagData == null) return;

            int currentCount  = IL2CppListOps.Count(heroTagData);
            int snapshotCount = _catSnapshot.Count;
            if (currentCount >= snapshotCount)
            {
                // v0.7.10.2 — no-op no-restore Info 제거 (no-op 도 매번 logging 했음)
                return;
            }

            var existingIDs = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < currentCount; i++)
            {
                var existing = IL2CppListOps.Get(heroTagData, i);
                if (existing == null) continue;
                var tidVal = ReadFieldOrProperty(existing, "tagID");
                if (tidVal is int tidInt) existingIDs.Add(tidInt);
            }

            int restored = 0;
            foreach (var tag in _catSnapshot)
            {
                var tidVal = ReadFieldOrProperty(tag, "tagID");
                if (tidVal is int tid && existingIDs.Contains(tid)) continue;
                try
                {
                    IL2CppListOps.Add(heroTagData, tag);
                    if (tidVal is int addedTid) existingIDs.Add(addedTid);
                    restored++;
                }
                catch (System.Exception ex)
                {
                    Logger.Warn($"RestKeepHeroTagPatch.Postfix_ClearAllTempTag: Add tag failed — {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (restored > 0)
                Logger.Info($"RestKeepHeroTagPatch.Postfix_ClearAllTempTag: restored {restored} tag(s) (was {currentCount}, snapshot {snapshotCount})");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Postfix_ClearAllTempTag threw: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _catSnapshot = null;
        }
    }

    // -------------------------------------------------------------------------
    // Harmony Prefix — snapshot heroTagData before ManageTagTime runs
    // -------------------------------------------------------------------------
    public static void Prefix(object __instance)
    {
        _snapshot = null;
        try
        {
            if (GetHeroID(__instance) != 0) return;

            var heroTagData = GetHeroTagData(__instance);
            if (heroTagData == null)
            {
                Logger.Warn("RestKeepHeroTagPatch.Prefix: heroTagData is null — snapshot skipped");
                return;
            }

            int count = IL2CppListOps.Count(heroTagData);
            var snap  = new List<object>(count);
            for (int i = 0; i < count; i++)
            {
                var tag = IL2CppListOps.Get(heroTagData, i);
                if (tag != null) snap.Add(tag);
            }
            _snapshot = snap;
            // v0.7.10.2 — verbose snapshot Info 제거 (ManageTagTime 매 tick logging 폭주 원인)
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Prefix threw: {ex.GetType().Name}: {ex.Message}");
            _snapshot = null;
        }
    }

    // -------------------------------------------------------------------------
    // Harmony Postfix — restore snapshot if ManageTagTime wiped heroTagData
    // -------------------------------------------------------------------------
    public static void Postfix(object __instance)
    {
        try
        {
            if (_snapshot == null) return;           // Prefix threw or was NPC — skip
            if (GetHeroID(__instance) != 0) return;

            var heroTagData = GetHeroTagData(__instance);
            if (heroTagData == null)
            {
                Logger.Warn("RestKeepHeroTagPatch.Postfix: heroTagData is null — restore skipped");
                return;
            }

            int currentCount  = IL2CppListOps.Count(heroTagData);
            int snapshotCount = _snapshot.Count;

            if (currentCount >= snapshotCount)
            {
                // Normal flow — no reset detected.
                // v0.7.10.2 — verbose "no restore needed" Info 제거 (ManageTagTime 매 tick 노이즈 원인)
                return;
            }

            // Collect tagIDs already in the list to avoid duplicates.
            var existingIDs = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < currentCount; i++)
            {
                var existing = IL2CppListOps.Get(heroTagData, i);
                if (existing == null) continue;
                var tidVal = ReadFieldOrProperty(existing, "tagID");
                if (tidVal is int tidInt) existingIDs.Add(tidInt);
            }

            int restored = 0;
            foreach (var tag in _snapshot)
            {
                var tidVal = ReadFieldOrProperty(tag, "tagID");
                if (tidVal is int tid && existingIDs.Contains(tid)) continue;
                try
                {
                    IL2CppListOps.Add(heroTagData, tag);
                    if (tidVal is int addedTid) existingIDs.Add(addedTid);
                    restored++;
                }
                catch (System.Exception ex)
                {
                    Logger.Warn($"RestKeepHeroTagPatch.Postfix: Add tag failed — {ex.GetType().Name}: {ex.Message}");
                }
            }

            if (restored > 0)
                Logger.Info($"RestKeepHeroTagPatch.Postfix: restored {restored} tag(s) (was {currentCount}, snapshot {snapshotCount})");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Postfix threw: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _snapshot = null;   // clear stale snapshot regardless of outcome
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static int GetHeroID(object instance)
    {
        try
        {
            var f = instance.GetType().GetField(
                "heroID",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (f != null) { var v = f.GetValue(instance); if (v is int i) return i; }

            var p = instance.GetType().GetProperty(
                "heroID",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (p != null) { var v = p.GetValue(instance); if (v is int i) return i; }
        }
        catch { }
        return -1;
    }

    private static object? GetHeroTagData(object instance)
    {
        try
        {
            var f = instance.GetType().GetField(
                "heroTagData",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (f != null) return f.GetValue(instance);

            var p = instance.GetType().GetProperty(
                "heroTagData",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (p != null) return p.GetValue(instance);
        }
        catch { }
        return null;
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        try
        {
            const System.Reflection.BindingFlags bf =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;
            var f = obj.GetType().GetField(name, bf);
            if (f != null) return f.GetValue(obj);
            var p = obj.GetType().GetProperty(name, bf);
            if (p != null) return p.GetValue(obj);
        }
        catch { }
        return null;
    }
}
