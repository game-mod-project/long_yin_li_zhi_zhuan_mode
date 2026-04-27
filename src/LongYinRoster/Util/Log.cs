using BepInEx.Logging;

namespace LongYinRoster.Util;

public static class Log
{
    private static ManualLogSource? _src;

    public static void Init(ManualLogSource src) => _src = src;

    public static void Info (string msg) => _src?.LogInfo(msg);
    public static void Warn (string msg) => _src?.LogWarning(msg);
    public static void Error(string msg) => _src?.LogError(msg);
    public static void Debug(string msg) => _src?.LogDebug(msg);
}
