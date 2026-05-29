using System;
using System.Linq;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — GameController.Instance reflection 접근. HeroLocator 패턴 mirror.
/// generator 메서드(GenerateWeapon 등)의 this 객체.
/// </summary>
public static class GameControllerLocator
{
    private const BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    public static object? GetGameController()
    {
        try
        {
            var t = FindTypeByName("GameController");
            if (t == null) { Logger.WarnOnce("GCLoc", "GameControllerLocator: GameController type 미발견"); return null; }
            var inst = ReadStaticMember(t, "Instance");
            if (inst == null) { Logger.WarnOnce("GCLoc", "GameControllerLocator: GameController.Instance null"); return null; }
            return inst;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("GCLoc", $"GameControllerLocator threw: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static object? ReadStaticMember(Type t, string name)
    {
        var p = t.GetProperty(name, StaticFlags);
        if (p != null) return p.GetValue(null);
        var f = t.GetField(name, StaticFlags);
        if (f != null) return f.GetValue(null);
        foreach (var alt in new[] { "instance", "_instance", "s_Instance", "s_instance" })
        {
            var pa = t.GetProperty(alt, StaticFlags);
            if (pa != null) return pa.GetValue(null);
            var fa = t.GetField(alt, StaticFlags);
            if (fa != null) return fa.GetValue(null);
        }
        return null;
    }

    private static Type? FindTypeByName(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.Name == name
                && (t.Namespace == null || !t.Namespace.StartsWith("LongYinRoster", StringComparison.Ordinal)));
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
    }
}
