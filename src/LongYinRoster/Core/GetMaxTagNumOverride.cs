using System;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 1 — GetMaxTagNumPatch 의 pure-logic 추출.
///
/// Harmony Postfix 의 분기 로직 (lock off / on / heroID match / value=0 / player null) 만
/// 담당 — HarmonyLib 의존 없음. test csproj 에 포함되어 unit test 가능.
///
/// Postfix wrapper 는 `GetMaxTagNumPatch.cs` (HarmonyLib + HeroLocator 의존, runtime-only).
/// </summary>
public static class GetMaxTagNumOverride
{
    /// <summary>Test-friendly extracted core. heroID 가 missing 시 -2 default.</summary>
    public static void ApplyOverride(object instance, bool isLocked, int lockedValue,
                                     int playerHeroID, ref int result, int instanceHeroID = -2)
    {
        if (!isLocked) return;
        if (lockedValue <= 0) return;
        if (playerHeroID < 0) return;          // player null sentinel
        if (instanceHeroID == -2) instanceHeroID = ReadHeroID(instance);
        if (instanceHeroID != playerHeroID) return;
        result = lockedValue;
    }

    public static int ReadHeroID(object? instance)
    {
        if (instance == null) return -1;
        try
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var t = instance.GetType();
            var f = t.GetField("heroID", F);
            if (f != null) { var v = f.GetValue(instance); if (v is int i) return i; }
            var p = t.GetProperty("heroID", F);
            if (p != null) { var v = p.GetValue(instance); if (v is int i) return i; }
        }
        catch { }
        return -1;
    }
}
