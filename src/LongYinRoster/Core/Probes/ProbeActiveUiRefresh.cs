using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5.1 Spike Phase 1 — UI cache invalidation discovery.
///
/// 4 modes (cheap → expensive):
///   Step1 = 1-회 swap (v0.5 PoC 와 동일 baseline)
///   Step2 = 11-회 swap (game 자체 패턴 mimic)
///   Step3 = 11-회 swap + flag toggle (skillIconDirty/maxManaChanged/HeroIconDirty/heroIconDirtyCount)
///   Step4 = persistence 검증 (data layer 변경 후 사용자 save→reload)
///
/// 각 step 후 read-back 로그 + 사용자 game UI 확인 보고. PASS = UI 즉시 갱신.
/// release 전 cleanup (D16 패턴 mirror).
/// </summary>
public static class ProbeActiveUiRefresh
{
    public enum Mode { Step1, Step2, Step3, Step4 }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run(Mode mode)
    {
        var player = HeroLocator.GetPlayer();
        if (player == null) { Logger.Warn("Spike: player null"); return; }

        var ksList = ReadField(player, "kungfuSkills");
        if (ksList == null) { Logger.Warn("Spike: kungfuSkills null"); return; }
        int n = IL2CppListOps.Count(ksList);

        Logger.Info($"Spike[{mode}]: kungfuSkills count={n}");

        switch (mode)
        {
            case Mode.Step1: RunStep1(player, ksList, n); break;
            case Mode.Step2: RunStep2(player, ksList, n); break;
            case Mode.Step3: RunStep3(player, ksList, n); break;
            case Mode.Step4: RunStep4(player, ksList, n); break;
        }
    }

    private static void RunStep1(object player, object ksList, int n)
    {
        // 1-회 swap: 첫 equiped wrapper unequip + 첫 unequiped wrapper equip
        object? equippedWrapper = null;
        object? unequippedWrapper = null;
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            bool eq = (bool)(ReadField(w, "equiped") ?? false);
            if (eq && equippedWrapper == null) equippedWrapper = w;
            if (!eq && unequippedWrapper == null) unequippedWrapper = w;
            if (equippedWrapper != null && unequippedWrapper != null) break;
        }
        if (equippedWrapper == null || unequippedWrapper == null)
        { Logger.Warn("Spike Step1: 후보 부족"); return; }

        InvokeMethod(player, "UnequipSkill", new[] { equippedWrapper, (object)true });
        InvokeMethod(player, "EquipSkill",   new[] { unequippedWrapper, (object)true });

        bool eqAfter1 = (bool)(ReadField(equippedWrapper,   "equiped") ?? false);
        bool eqAfter2 = (bool)(ReadField(unequippedWrapper, "equiped") ?? false);
        Logger.Info($"Spike Step1: read-back — old={eqAfter1} (expect false); new={eqAfter2} (expect true)");
        Logger.Info("Spike Step1: F12 후 게임 무공 패널 UI 변경 보이는지 사용자 확인 (예상 NO)");
    }

    private static void RunStep2(object player, object ksList, int n)
    {
        // 11-회 swap: 모든 equiped 를 unequip → 11 개 unequiped (다른 set) 을 equip
        var currentEquipped = new System.Collections.Generic.List<object>();
        var unequippedPool  = new System.Collections.Generic.List<object>();
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            if ((bool)(ReadField(w, "equiped") ?? false)) currentEquipped.Add(w);
            else unequippedPool.Add(w);
        }
        if (unequippedPool.Count < currentEquipped.Count)
        { Logger.Warn($"Spike Step2: pool 부족 (eq={currentEquipped.Count}, pool={unequippedPool.Count})"); return; }

        foreach (var w in currentEquipped)
            InvokeMethod(player, "UnequipSkill", new[] { w, (object)true });
        Logger.Info($"Spike Step2: Unequip × {currentEquipped.Count} 완료");

        for (int i = 0; i < currentEquipped.Count; i++)
            InvokeMethod(player, "EquipSkill", new[] { unequippedPool[i], (object)true });
        Logger.Info($"Spike Step2: Equip × {currentEquipped.Count} 완료");
        Logger.Info("Spike Step2: F12 후 게임 무공 패널 UI 변경 보이는지 사용자 확인");
    }

    private static void RunStep3(object player, object ksList, int n)
    {
        // Step2 swap 후 flag toggle
        var changed = new System.Collections.Generic.List<object>();
        var currentEquipped = new System.Collections.Generic.List<object>();
        var unequippedPool  = new System.Collections.Generic.List<object>();
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            if ((bool)(ReadField(w, "equiped") ?? false)) currentEquipped.Add(w);
            else unequippedPool.Add(w);
        }
        if (unequippedPool.Count < currentEquipped.Count)
        { Logger.Warn("Spike Step3: pool 부족"); return; }

        foreach (var w in currentEquipped)
        {
            InvokeMethod(player, "UnequipSkill", new[] { w, (object)true });
            changed.Add(w);
        }
        for (int i = 0; i < currentEquipped.Count; i++)
        {
            var w = unequippedPool[i];
            InvokeMethod(player, "EquipSkill", new[] { w, (object)true });
            changed.Add(w);
        }
        Logger.Info($"Spike Step3: swap × {currentEquipped.Count} 완료, flag toggle 진행");

        foreach (var w in changed)
        {
            TrySetField(w, "skillIconDirty", true);
            TrySetField(w, "maxManaChanged", true);
        }
        TrySetField(player, "HeroIconDirty", true);

        var cntField = player.GetType().GetField("heroIconDirtyCount", F);
        if (cntField != null)
        {
            int cur = (int)(cntField.GetValue(player) ?? 0);
            cntField.SetValue(player, cur + 1);
        }
        Logger.Info("Spike Step3: flag toggle 완료. F12 후 게임 무공 패널 UI 사용자 확인");
    }

    private static void RunStep4(object player, object ksList, int n)
    {
        // 현재 equiped skillID set 출력 — save → reload 전후 비교 baseline
        var equipped = new System.Collections.Generic.List<int>();
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            if (!(bool)(ReadField(w, "equiped") ?? false)) continue;
            int sid = (int)(ReadField(w, "skillID") ?? -1);
            equipped.Add(sid);
        }
        Logger.Info($"Spike Step4 — 현재 equiped skillID set: [{string.Join(",", equipped)}]");
        Logger.Info("Spike Step4: 게임 메뉴 → save → 게임 종료 → 재시작 → save load → 위 set 과 일치하는지 사용자 확인");
    }

    private static object? ReadField(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        return null;
    }

    private static void TrySetField(object obj, string name, object value)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null && p.CanWrite) { try { p.SetValue(obj, value); } catch { } return; }
        var f = t.GetField(name, F);
        if (f != null) { try { f.SetValue(obj, value); } catch { } }
    }

    private static void InvokeMethod(object obj, string methodName, object[] args)
    {
        var t = obj.GetType();
        MethodInfo? best = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != methodName) continue;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) continue;
            if (best == null || ps.Length < best.GetParameters().Length) best = m;
        }
        if (best == null) { Logger.Warn($"InvokeMethod: {methodName} not found"); return; }
        var ps2 = best.GetParameters();
        var full = new object?[ps2.Length];
        for (int i = 0; i < ps2.Length; i++)
            full[i] = i < args.Length ? args[i]
                : (ps2[i].ParameterType.IsValueType ? Activator.CreateInstance(ps2[i].ParameterType) : null);
        try { best.Invoke(obj, full); }
        catch (Exception ex) { Logger.Warn($"InvokeMethod {methodName}: {ex.GetType().Name}: {ex.Message}"); }
    }
}
