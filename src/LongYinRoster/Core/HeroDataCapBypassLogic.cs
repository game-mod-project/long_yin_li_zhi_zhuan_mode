using System;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 3 — HeroDataCapBypassPatch 의 pure-logic 추출.
///
/// cheat LongYinCheat.Patches.MultiplierPatch 의 Max*Postfix (line 193-278) 분기 mirror —
/// HarmonyLib 의존 없음. test csproj 에 포함되어 unit test 가능.
///
/// Postfix wrapper 는 `HeroDataCapBypassPatch.cs` (HarmonyLib + HeroLocator + ThreadStatic snapshot
/// state, runtime-only).
/// </summary>
public static class HeroDataCapBypassLogic
{
    /// <summary>
    /// `HeroData.GetMaxAttri/FightSkill/LivingSkill(int)` Postfix 의 분기 logic.
    ///
    /// **Uncap off**: defensive re-clamp `__result > gameCap` → gameCap (cheat 가 같은 패턴).
    /// **Uncap on + heroID match + value > 0**: `__result = uncapValue` override.
    /// **mismatch / player null / value=0**: no-op (instance 가 NPC 또는 안전 상태).
    /// </summary>
    public static void ApplyMaxOverride(object instance, bool isUncapEnabled,
                                        int uncapValue, float gameCap,
                                        int playerHeroID, ref float result,
                                        int instanceHeroID = -2)
    {
        if (!isUncapEnabled)
        {
            // Defensive re-clamp (cheat 가 game cap 보다 큰 값을 수정 안 했으면 강제 환원).
            if (result > gameCap) result = gameCap;
            return;
        }
        if (uncapValue <= 0) return;
        if (playerHeroID < 0) return;          // player null sentinel
        if (instanceHeroID == -2) instanceHeroID = ReadHeroID(instance);
        if (instanceHeroID != playerHeroID) return;
        result = uncapValue;
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
