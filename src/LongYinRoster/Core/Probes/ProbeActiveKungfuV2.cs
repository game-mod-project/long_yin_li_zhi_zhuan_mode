using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>v0.5 무공 active PoC. 본문은 T12/T13/T14 에서 작성.</summary>
internal static class ProbeActiveKungfuV2
{
    public static void RunPhaseA(object player)
    {
        Logger.Warn("ProbeActiveKungfuV2.RunPhaseA: stub — T12 에서 본문 작성 예정 (save-diff)");
    }

    public static void RunPhaseB(object player)
    {
        Logger.Warn("ProbeActiveKungfuV2.RunPhaseB: stub — T13 에서 본문 작성 예정 (Harmony trace)");
    }

    public static void RunPhaseC(object player)
    {
        Logger.Warn("ProbeActiveKungfuV2.RunPhaseC: stub — T14 에서 본문 작성 예정 (in-memory)");
    }
}
