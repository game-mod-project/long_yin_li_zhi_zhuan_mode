using System;
using System.IO;
using BepInEx;

namespace LongYinRoster.Util;

/// <summary>플러그인이 사용하는 모든 디스크 경로 진입점. 절대경로/플레이스홀더 모두 처리.</summary>
public static class PathProvider
{
    /// <summary>BepInEx/plugins/LongYinRoster/ — 플러그인의 자기 폴더.</summary>
    public static string PluginDir =>
        Path.Combine(Paths.PluginPath, "LongYinRoster");

    /// <summary>설정 문자열에서 &lt;PluginPath&gt; 토큰을 실제 경로로 치환.</summary>
    public static string Resolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PluginDir;
        return raw
            .Replace("<PluginPath>", PluginDir, StringComparison.OrdinalIgnoreCase)
            .Replace("\\", "/");
    }

    /// <summary>게임 루트 (LongYinLiZhiZhuan/) — Save/SaveSlot* 접근용.</summary>
    public static string GameDir =>
        Directory.GetParent(Paths.BepInExRootPath)!.FullName;

    /// <summary>게임 세이브 루트 (LongYinLiZhiZhuan_Data/Save/).</summary>
    public static string GameSaveDir =>
        Path.Combine(GameDir, "LongYinLiZhiZhuan_Data", "Save");
}
