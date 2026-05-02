using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

public static class ProbeRunner
{
    public static ProbeItemList.Mode Mode { get; set; } = ProbeItemList.Mode.Step1;

    public static void Trigger()
    {
        Logger.Info($"=== ProbeRunner: F12 → ItemList / {Mode} ===");
        ProbeItemList.Run(Mode);
        Logger.Info("=== ProbeRunner: end ===");
    }

    public static void SetMode(ProbeItemList.Mode m)
    {
        Mode = m;
        Logger.Info($"ProbeRunner.Mode = {m}");
    }
}
