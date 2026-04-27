using System;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

public static class HeroLocator
{
    private static object? _cached;

    /// <summary>heroID=0 인 Hero 객체. 없으면 null.</summary>
    public static object? GetPlayer()
    {
        // 1) 캐시 검증 후 반환
        if (_cached != null && IsValidPlayer(_cached)) return _cached;

        // 2) Assembly-CSharp 의 Hero 타입을 찾고 FindObjectsOfTypeAll 시도
        var heroType = FindHeroType();
        if (heroType == null) { Logger.Warn("Hero type not found in Assembly-CSharp"); return null; }

        try
        {
            var il2Type = Il2CppType.From(heroType);
            var found = Resources.FindObjectsOfTypeAll(il2Type);
            foreach (var obj in found)
            {
                if (obj == null) continue;
                if (TryGetHeroId(obj, out var id) && id == 0) { _cached = obj; return obj; }
            }
        }
        catch (Exception ex) { Logger.Warn($"Hero scan failed: {ex.Message}"); }

        // 3) 게임 매니저 정적 필드 추정 — 흔한 이름 후보
        foreach (var managerName in new[] { "GameManager", "HeroManager", "PlayerManager" })
        {
            var mgrType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.Name == managerName);
            if (mgrType == null) continue;

            var inst = mgrType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null) continue;

            foreach (var pname in new[] { "Player", "PlayerHero", "MainHero" })
            {
                var p = mgrType.GetProperty(pname, BindingFlags.Public | BindingFlags.Instance);
                var v = p?.GetValue(inst);
                if (v != null && IsValidPlayer(v)) { _cached = v; return v; }
            }
        }

        return null;
    }

    public static bool IsInGame() => GetPlayer() != null;

    public static void InvalidateCache() => _cached = null;

    // -- helpers ------------------------------------------------------------

    private static Type? FindHeroType()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.Name == "Hero" && (t.Namespace ?? "") != "LongYinRoster.Core");
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
    }

    private static bool IsValidPlayer(object obj) =>
        TryGetHeroId(obj, out var id) && id == 0;

    private static bool TryGetHeroId(object obj, out int id)
    {
        id = -1;
        var t = obj.GetType();
        try
        {
            var f = t.GetField("heroID");
            if (f != null) { id = (int)f.GetValue(obj)!; return true; }
            var p = t.GetProperty("heroID");
            if (p != null) { id = (int)p.GetValue(obj)!; return true; }
        }
        catch { }
        return false;
    }
}
