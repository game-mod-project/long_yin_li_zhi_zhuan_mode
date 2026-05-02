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
    // Single static snapshot — player heroID==0 only. Unity is single-threaded.
    private static List<object>? _snapshot;

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

            var manageTagTime = AccessTools.Method(heroDataType, "ManageTagTime");
            if (manageTagTime == null)
            {
                Logger.Warn("RestKeepHeroTagPatch: HeroData.ManageTagTime not found — skip");
                return;
            }

            var prefix  = new HarmonyMethod(typeof(RestKeepHeroTagPatch).GetMethod(
                nameof(Prefix),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));
            var postfix = new HarmonyMethod(typeof(RestKeepHeroTagPatch).GetMethod(
                nameof(Postfix),
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public));

            harmony.Patch(manageTagTime, prefix: prefix, postfix: postfix);
            Logger.Info("RestKeepHeroTagPatch: HeroData.ManageTagTime patched");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Register threw: {ex.GetType().Name}: {ex.Message}");
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
            Logger.Info($"RestKeepHeroTagPatch.Prefix: snapshot {snap.Count} tag(s)");
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
                Logger.Info($"RestKeepHeroTagPatch.Postfix: count={currentCount} >= snapshot={snapshotCount}, no restore needed");
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
