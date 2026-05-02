using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5.2 Spike Phase 1 — 무공 list game-self method discovery.
///
/// 5 modes:
///   Step1 = method dump (Lose/Learn/Add/Clear*Kungfu* 시그니처)
///   Step2 = clear 후보 시도
///   Step3 = add 후보 시도
///   Step4 = 통합 (clear + add all)
///   Step5 = persistence baseline (현재 list count + first 10 entries 출력)
///
/// release 전 cleanup.
/// </summary>
public static class ProbeKungfuList
{
    public enum Mode { Step1, Step2, Step3, Step4, Step5, Step6 }

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
            case Mode.Step1: RunStep1(player); break;
            case Mode.Step2: RunStep2(player, ksList); break;
            case Mode.Step3: RunStep3(player, ksList); break;
            case Mode.Step4: RunStep4(player, ksList); break;
            case Mode.Step5: RunStep5(ksList, n); break;
            case Mode.Step6: RunStep6(ksList); break;
        }
    }

    private static void RunStep1(object player)
    {
        var t = player.GetType();
        var pattern = new System.Text.RegularExpressions.Regex(
            @"^(Lose|Learn|Add|Clear|Remove|Get|Drop)(All)?(Kungfu|Skill)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        Logger.Info("=== Spike Step1 — method dump ===");
        foreach (var m in t.GetMethods(F))
        {
            if (!pattern.IsMatch(m.Name)) continue;
            var ps = m.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"method: {m.ReturnType.Name} {m.Name}({sig})");
        }
        Logger.Info("=== Spike Step1 end ===");
    }

    private static void RunStep2(object player, object ksList)
    {
        string[] candidates = { "LoseAllSkill", "LoseAllKungfu", "ClearAllKungfu", "RemoveAllKungfu", "ClearKungfu", "LoseKungfu" };
        foreach (var name in candidates)
        {
            var m = player.GetType().GetMethod(name, F, null, Type.EmptyTypes, null);
            if (m == null) { Logger.Info($"Spike Step2: {name}() not found"); continue; }
            int beforeCount = IL2CppListOps.Count(ksList);
            try { m.Invoke(player, null); }
            catch (Exception ex) { Logger.Warn($"Spike Step2 {name}: {ex.GetType().Name}: {ex.Message}"); continue; }
            int afterCount = IL2CppListOps.Count(ksList);
            Logger.Info($"Spike Step2: {name}() — count {beforeCount} → {afterCount}");
            return;
        }
        Logger.Warn("Spike Step2: 모든 후보 not found");
    }

    private static void RunStep3(object player, object ksList)
    {
        // 후보 add method 시도 — (int skillID, int lv) 시그니처 가정. 첫 학습된 무공의 skillID 사용
        int testSkillID = -1;
        int n = IL2CppListOps.Count(ksList);
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            int sid = (int)(ReadField(w, "skillID") ?? -1);
            if (sid >= 0) { testSkillID = sid; break; }
        }
        if (testSkillID < 0)
        {
            // ksList 비어있음 (Step2 PASS 후) — hardcoded ID 시도
            testSkillID = 100;
            Logger.Info($"Spike Step3: list 비어있음, hardcoded skillID={testSkillID} 시도");
        }

        int testLv = 1;
        string[] candidates = { "LearnKungfu", "AddKungfuSkill", "GetKungfu", "AddKungfu" };
        foreach (var name in candidates)
        {
            var m = player.GetType().GetMethod(name, F, null, new[] { typeof(int), typeof(int) }, null);
            if (m == null) { Logger.Info($"Spike Step3: {name}(int, int) not found"); continue; }
            int beforeCount = IL2CppListOps.Count(ksList);
            try { m.Invoke(player, new object[] { testSkillID, testLv }); }
            catch (Exception ex) { Logger.Warn($"Spike Step3 {name}: {ex.GetType().Name}: {ex.Message}"); continue; }
            int afterCount = IL2CppListOps.Count(ksList);
            Logger.Info($"Spike Step3: {name}({testSkillID}, {testLv}) — count {beforeCount} → {afterCount}");
            return;
        }
        Logger.Warn("Spike Step3: (int, int) 후보 모두 not found");
    }

    private static void RunStep4(object player, object ksList)
    {
        Logger.Info("Spike Step4: 통합 시나리오는 Step 1-3 분석 후 implementation 단계에서 검증");
    }

    /// <summary>
    /// v0.5.2 Step 6 — KungfuSkillLvData wrapper type 자체의 ctor / static factory dump.
    /// Step 1 의 method dump 결과 add method 가 wrapper 인자 (GetSkill(KungfuSkillLvData, ...)).
    /// wrapper ctor 발견 위해 type 의 ctor + static method 모두 dump.
    /// </summary>
    private static void RunStep6(object ksList)
    {
        // 첫 wrapper 의 type 사용 (ksList 가 비어있지 않다고 가정)
        if (IL2CppListOps.Count(ksList) == 0)
        {
            Logger.Warn("Spike Step6: ksList 비어있음 — wrapper type 알 수 없음. game 안에서 ksList 채워진 상태에서 시도");
            return;
        }
        var sample = IL2CppListOps.Get(ksList, 0);
        if (sample == null) { Logger.Warn("Spike Step6: sample wrapper null"); return; }
        var wrapperType = sample.GetType();
        Logger.Info($"=== Spike Step6 — KungfuSkillLvData ({wrapperType.FullName}) dump ===");

        // Constructors
        Logger.Info("--- Constructors ---");
        foreach (var ctor in wrapperType.GetConstructors(F | BindingFlags.Static))
        {
            var ps = ctor.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"ctor: ({sig})");
        }

        // Static methods (factory 후보)
        Logger.Info("--- Static methods ---");
        foreach (var m in wrapperType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var ps = m.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"static: {m.ReturnType.Name} {m.Name}({sig})");
        }
        Logger.Info("=== Spike Step6 end ===");
    }

    private static void RunStep5(object ksList, int n)
    {
        Logger.Info($"Spike Step5: kungfuSkills count={n}");
        int dumpN = System.Math.Min(n, 10);
        for (int i = 0; i < dumpN; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            int sid = (int)(ReadField(w, "skillID") ?? -1);
            int lv = (int)(ReadField(w, "lv") ?? -1);
            float fe = (float)(ReadField(w, "fightExp") ?? -1f);
            bool eq = (bool)(ReadField(w, "equiped") ?? false);
            Logger.Info($"Spike Step5: [{i}] skillID={sid} lv={lv} fightExp={fe} equiped={eq}");
        }
        Logger.Info("Spike Step5: 게임 메뉴 → save → 종료 → 재시작 → load → 위 list 와 일치하는지 사용자 확인");
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
}
