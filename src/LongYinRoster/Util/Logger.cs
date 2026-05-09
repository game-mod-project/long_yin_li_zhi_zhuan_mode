using System.Collections.Generic;
using BepInEx.Logging;

namespace LongYinRoster.Util;

public static class Logger
{
    private static ManualLogSource? _src;
    private static readonly HashSet<string> _onceKeys = new();

    public static void Init(ManualLogSource src) => _src = src;

    public static void Info (string msg) => _src?.LogInfo(msg);
    public static void Warn (string msg) => _src?.LogWarning(msg);
    public static void Error(string msg) => _src?.LogError(msg);
    public static void Debug(string msg) => _src?.LogDebug(msg);

    /// <summary>v0.7.8 — 매 frame 호출되는 reflection helper 의 silent fail 폭주 회피. key 별 1회만 출력.</summary>
    public static void WarnOnce(string key, string msg)
    {
        if (_onceKeys.Add(key)) _src?.LogWarning(msg);
    }

    /// <summary>v0.7.8 — 매 frame 호출되는 코드의 Info 발화 폭주 회피. key 별 1회만 출력.</summary>
    public static void InfoOnce(string key, string msg)
    {
        if (_onceKeys.Add(key)) _src?.LogInfo(msg);
    }
}
