using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5.1 Spike — F12 trigger handler. ModWindow.Update 에서 Input.GetKeyDown(F12) 검사 후 호출.
/// F10 = Mode cycling (Step1 → Step2 → Step3 → Step4 → Step1).
/// release 전 cleanup (Probe 와 함께).
/// </summary>
public static class ProbeRunner
{
    public static ProbeActiveUiRefresh.Mode Mode { get; set; } = ProbeActiveUiRefresh.Mode.Step1;

    public static void Trigger()
    {
        Logger.Info($"=== ProbeRunner: F12 → ActiveUiRefresh / {Mode} ===");
        ProbeActiveUiRefresh.Run(Mode);
        Logger.Info("=== ProbeRunner: end ===");
    }

    public static void CycleMode()
    {
        var cur = Mode;
        Mode = (ProbeActiveUiRefresh.Mode)(((int)cur + 1) % 4);
        Logger.Info($"ProbeRunner.Mode = {Mode}");
    }
}
