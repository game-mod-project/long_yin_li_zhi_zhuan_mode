using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// 임시 진단 도구 — v0.3 release 전 제거. plan Task 21 에서 파일과 [F12] 핸들러 모두 삭제.
///
/// 게임 안에서 reflection 으로 HeroData 의 모든 method/property/field 를 BepInEx 로그에
/// dump 하고, Hero 관련 Refresh API 를 가진 매니저 후보를 enumerate. 그 결과로
/// docs/HeroData-methods.md 를 작성하고 spec §7.2 매트릭스를 보강한다.
/// </summary>
public static class HeroDataDump
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void DumpToLog()
    {
        Logger.Info("============================== HeroDataDump.start ==============================");
        var player = HeroLocator.GetPlayer();
        if (player == null)
        {
            Logger.Warn("HeroDataDump: player null. Game 진입 후 다시 [F12] 누르세요.");
            return;
        }
        var heroType = player.GetType();
        Logger.Info($"HeroDataDump: heroType = {heroType.AssemblyQualifiedName}");

        DumpHeroSelf(heroType);
        DumpManagerCandidates(heroType);
        Logger.Info("============================== HeroDataDump.end ================================");
    }

    private static void DumpHeroSelf(Type heroType)
    {
        foreach (var m in heroType.GetMethods(F).OrderBy(m => m.Name))
        {
            var pars = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
            Logger.Info($"HeroDataDump.self.method: {m.ReturnType.Name} {m.Name}({pars})");
        }
        foreach (var p in heroType.GetProperties(F).OrderBy(p => p.Name))
            Logger.Info($"HeroDataDump.self.prop: {p.PropertyType.Name} {p.Name} {{ get={p.CanRead}, set={p.CanWrite} }}");
        foreach (var f in heroType.GetFields(F).OrderBy(f => f.Name))
            Logger.Info($"HeroDataDump.self.field: {f.FieldType.Name} {f.Name}");
    }

    private static void DumpManagerCandidates(Type heroType)
    {
        var rx = new Regex("^(Refresh|Update|OnHero|Rebuild)");
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        foreach (var t in SafeGetTypes(asm))
        {
            if (t == null) continue;
            if (!t.Name.EndsWith("Manager") && !t.Name.EndsWith("Controller")) continue;
            if (t.Namespace != null && t.Namespace.StartsWith("LongYinRoster")) continue;
            foreach (var m in t.GetMethods(F))
            {
                if (!rx.IsMatch(m.Name)) continue;
                var pars = m.GetParameters();
                bool acceptsHero = pars.Any(p =>
                    p.ParameterType == heroType ||
                    p.ParameterType.Name == "HeroData");
                if (!acceptsHero) continue;
                var sig = string.Join(",", pars.Select(p => p.ParameterType.Name));
                Logger.Info($"HeroDataDump.mgr: {t.FullName}.{m.Name}({sig})");
            }
        }
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
    }
}
