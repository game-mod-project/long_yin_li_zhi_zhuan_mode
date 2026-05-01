using System;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5 PoC 임시 trigger. F12 시 Current Mode 의 Probe 실행.
/// release 직전 ProbeRunner + ProbePortraitRefresh + ProbeActiveKungfuV2 + ModWindow 의 F12 handler 일괄 제거 (Task 22 / D16 패턴).
/// </summary>
internal static class ProbeRunner
{
    public enum Mode { Portrait, ActiveDiff, ActiveTrace, ActiveInMemory }

    /// <summary>현재 활성 Probe 모드. 개발 중 코드로 변경.</summary>
    /// <remarks>
    /// v0.5 진행 단계:
    ///   - Phase 1 (외형 PoC): Portrait — G1 FAIL (deferred to v0.6)
    ///   - Phase A (active save-diff): ActiveDiff — G2 PASS (kungfuSkills[i].equiped 가 source)
    ///   - Phase B (active Harmony trace): ActiveTrace — Phase B PASS (HeroData.EquipSkill/UnequipSkill 발견)
    ///   - Phase C (active in-memory): ActiveInMemory ← 현재
    /// </remarks>
    public static Mode Current = Mode.ActiveInMemory;

    public static void Run()
    {
        try
        {
            var player = HeroLocator.GetPlayer();
            if (player == null) { Logger.Warn("ProbeRunner: player not found"); return; }

            switch (Current)
            {
                case Mode.Portrait:
                    Logger.Info("=== ProbePortraitRefresh START ===");
                    ProbePortraitRefresh.Run(player);
                    Logger.Info("=== ProbePortraitRefresh END ===");
                    break;
                case Mode.ActiveDiff:
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseA (save-diff) START ===");
                    ProbeActiveKungfuV2.RunPhaseA(player);
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseA END ===");
                    break;
                case Mode.ActiveTrace:
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseB (Harmony trace) START ===");
                    ProbeActiveKungfuV2.RunPhaseB(player);
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseB END ===");
                    break;
                case Mode.ActiveInMemory:
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseC (in-memory) START ===");
                    ProbeActiveKungfuV2.RunPhaseC(player);
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseC END ===");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"ProbeRunner: {ex.GetType().Name}: {ex.Message}");
            Logger.Error(ex.StackTrace ?? "(no stack)");
        }
    }
}
