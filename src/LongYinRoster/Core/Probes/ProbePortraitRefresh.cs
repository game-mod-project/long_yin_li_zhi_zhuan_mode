using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5 외형 PoC Phase 2. 1차 시도: portraitID/gender 가 HeroData 에 부재 — pivot:
/// 자가-발견 (HeroData 의 portrait/face/avatar/head/icon/pic 패턴 field/property + UI refresh method enumerate).
/// 결과를 BepInEx log 에 dump → 사용자가 G1 으로 실제 외형 필드 식별 → 다음 iteration.
/// </summary>
internal static class ProbePortraitRefresh
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly Regex AppearancePattern = new(
        @"portrait|face|avatar|head|icon|pic|outfit|cloth|hair|skin|appearance|partposture|body",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RefreshMethodPattern = new(
        @"^(refresh|reload|update|invalidate|set)\w*(portrait|face|avatar|head|icon|pic|outfit|cloth|hair|skin|appearance|sprite|view|self)\w*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void Run(object player)
    {
        var t = player.GetType();
        Logger.Info($"player type: {t.FullName}");

        // 1. 외형 패턴 매칭 field/property enumerate
        Logger.Info("--- HeroData fields/properties (외형 패턴) ---");
        var matchingProps = t.GetProperties(F)
            .Where(p => AppearancePattern.IsMatch(p.Name))
            .ToArray();
        var matchingFields = t.GetFields(F)
            .Where(f => AppearancePattern.IsMatch(f.Name))
            .ToArray();

        foreach (var p in matchingProps)
            Logger.Info($"  property: {p.PropertyType.Name} {p.Name}  (canRead={p.CanRead}, canWrite={p.CanWrite})");
        foreach (var f in matchingFields)
            Logger.Info($"  field:    {f.FieldType.Name} {f.Name}  (isPublic={f.IsPublic})");

        if (matchingProps.Length == 0 && matchingFields.Length == 0)
            Logger.Warn("HeroData 에 외형 패턴 매칭 field/property 0 — pattern 확장 필요");

        // 2. refresh method 패턴 매칭 enumerate (HeroData)
        Logger.Info("--- HeroData methods (refresh + 외형 패턴) ---");
        var matchingMethods = t.GetMethods(F)
            .Where(m => RefreshMethodPattern.IsMatch(m.Name))
            .Take(40)
            .ToArray();

        foreach (var m in matchingMethods)
        {
            var sig = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
            Logger.Info($"  method: {m.ReturnType.Name} {m.Name}({sig})");
        }
        if (matchingMethods.Length == 0)
            Logger.Info("HeroData 에 refresh+외형 패턴 매칭 method 0");

        // 3. 현재 식별된 후보 field 들의 현재 값 (보너스 — int / bool / string 만)
        Logger.Info("--- candidate field 현재 값 ---");
        foreach (var p in matchingProps.Where(p => IsScalar(p.PropertyType) && p.CanRead))
        {
            try { Logger.Info($"  {p.Name} = {p.GetValue(player)}"); }
            catch (Exception ex) { Logger.Warn($"  {p.Name} read threw: {ex.GetType().Name}: {ex.Message}"); }
        }
        foreach (var f in matchingFields.Where(f => IsScalar(f.FieldType)))
        {
            try { Logger.Info($"  {f.Name} = {f.GetValue(player)}"); }
            catch (Exception ex) { Logger.Warn($"  {f.Name} read threw: {ex.GetType().Name}: {ex.Message}"); }
        }

        // 4. 알려진 후보 refresh method 들도 시도 (zero-arg)
        Logger.Info("--- 후보 zero-arg refresh method 호출 시도 ---");
        string[] candidateMethods =
        {
            "RefreshPortrait", "ReloadPortrait", "UpdatePortrait",
            "RefreshFaceData", "RefreshFace", "RefreshAvatar",
            "RefreshSprite",   "RefreshSelfState",
            "RefreshIcon",     "ReloadIcon", "RefreshHead",
            "RefreshOutfit",   "RefreshHair",
        };
        foreach (var name in candidateMethods)
            TryCall(player, name);

        Logger.Info("=== Phase 2 (discovery) done. dump 결과 → 외형 필드 이름 식별 후 다음 iteration ===");
    }

    private static bool IsScalar(Type t) =>
        t == typeof(int)  || t == typeof(uint)   || t == typeof(short) || t == typeof(ushort) ||
        t == typeof(long) || t == typeof(ulong)  || t == typeof(byte)  || t == typeof(sbyte)  ||
        t == typeof(bool) || t == typeof(string) || t == typeof(float) || t == typeof(double);

    private static void TryCall(object obj, string methodName)
    {
        var m = obj.GetType().GetMethod(methodName, F, null, Type.EmptyTypes, null);
        if (m == null) { Logger.Info($"  method '{methodName}': not found (skip)"); return; }
        try { m.Invoke(obj, null); Logger.Info($"  called {methodName}() — ok"); }
        catch (Exception ex) { Logger.Warn($"  {methodName} threw: {ex.GetType().Name}: {ex.Message}"); }
    }
}
