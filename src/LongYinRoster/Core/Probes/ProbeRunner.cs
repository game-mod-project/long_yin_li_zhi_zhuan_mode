using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

public static class ProbeRunner
{
    public static ProbeKungfuList.Mode Mode { get; set; } = ProbeKungfuList.Mode.Step1;

    public static void Trigger()
    {
        Logger.Info($"=== ProbeRunner: F12 → KungfuList / {Mode} ===");
        ProbeKungfuList.Run(Mode);
        Logger.Info("=== ProbeRunner: end ===");
    }

    public static void CycleMode()
    {
        var cur = Mode;
        Mode = (ProbeKungfuList.Mode)(((int)cur + 1) % 6);
        Logger.Info($"ProbeRunner.Mode = {Mode}");
    }

    public static void SetMode(ProbeKungfuList.Mode m)
    {
        Mode = m;
        Logger.Info($"ProbeRunner.Mode = {m}");
    }
}
