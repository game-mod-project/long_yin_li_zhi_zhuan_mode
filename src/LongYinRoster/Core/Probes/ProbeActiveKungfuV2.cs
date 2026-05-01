using System;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using LongYinRoster.Slots;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5 무공 active PoC. v0.4 A3 FAIL (wrapper.lv vs nowActiveSkill ID mismatch) 재도전.
/// 가설 변경: save-diff (Phase A) → Harmony trace (Phase B) → in-memory (Phase C).
/// </summary>
internal static class ProbeActiveKungfuV2
{
    /// <summary>
    /// Phase A — save-diff. 사용자 시나리오:
    ///   1. 게임 안에서 active 무공이 X 인 상태로 SaveSlot1 에 save (게임 자체 save 메뉴)
    ///   2. UI 에서 active 를 Y 로 변경
    ///   3. SaveSlot2 에 save
    ///   4. F12 → 이 Probe 가 두 SaveSlot 의 hero[0] JSON 을 깊이 비교 + DIFF 출력.
    ///
    /// 변경 필드 set 식별 — nowActiveSkill / kungfuSkills[*].equiped 등 어느 것이 진짜 active 의 source-of-truth 인지.
    /// </summary>
    public static void RunPhaseA(object _player)
    {
        const int SLOT_BEFORE = 1;
        const int SLOT_AFTER  = 2;

        Logger.Info($"PhaseA: diff SaveSlot{SLOT_BEFORE} vs SaveSlot{SLOT_AFTER}");

        string beforeJson, afterJson;
        try { beforeJson = SaveFileScanner.LoadHero0(SLOT_BEFORE); }
        catch (Exception ex) { Logger.Warn($"PhaseA: SaveSlot{SLOT_BEFORE} 로드 실패: {ex.Message}"); LogUserGuide(); return; }
        try { afterJson = SaveFileScanner.LoadHero0(SLOT_AFTER); }
        catch (Exception ex) { Logger.Warn($"PhaseA: SaveSlot{SLOT_AFTER} 로드 실패: {ex.Message}"); LogUserGuide(); return; }

        Logger.Info($"PhaseA: hero[0] sizes — before={beforeJson.Length} chars, after={afterJson.Length} chars");

        using var docBefore = JsonDocument.Parse(beforeJson);
        using var docAfter  = JsonDocument.Parse(afterJson);

        int diffCount = 0;
        DiffJson("(root)", docBefore.RootElement, docAfter.RootElement, ref diffCount);

        if (diffCount == 0)
        {
            Logger.Warn("PhaseA: DIFF 0 — 두 save 가 동일 (사용자가 active 변경 안 했거나 같은 slot 에 두 번 save). LogUserGuide 참고.");
        }
        else
        {
            Logger.Info($"PhaseA: total DIFF entries = {diffCount}");
        }

        Logger.Info("PhaseA done. dumps/2026-05-01-active-kungfu-diff.md 에 결과 캡처 후 G2 게이트.");
    }

    private static void LogUserGuide()
    {
        Logger.Info("PhaseA 사용자 가이드:");
        Logger.Info("  1. 게임 안에서 active 무공 X 상태");
        Logger.Info("  2. 게임 메뉴 → SaveSlot 1 에 save");
        Logger.Info("  3. 게임 안에서 active 를 Y 로 변경");
        Logger.Info("  4. 게임 메뉴 → SaveSlot 2 에 save");
        Logger.Info("  5. F12 다시 누름 → diff 출력");
    }

    /// <summary>
    /// 두 JsonElement 의 깊이 비교. 차이 있는 path + 값을 Logger.Info("DIFF[path]: ...") 로 출력.
    /// 큰 array 는 length + 처음 N 개 entry 차이만 출력 (출력 폭주 방지).
    /// </summary>
    private static void DiffJson(string path, JsonElement a, JsonElement b, ref int diffCount, int depth = 0)
    {
        const int MAX_DEPTH        = 6;
        const int MAX_ARRAY_SAMPLE = 20;

        if (depth > MAX_DEPTH) return;

        if (a.ValueKind != b.ValueKind)
        {
            Logger.Info($"DIFF[{path}]: kind {a.ValueKind} → {b.ValueKind}");
            diffCount++;
            return;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in a.EnumerateObject())
                {
                    if (b.TryGetProperty(prop.Name, out var bv))
                        DiffJson($"{path}.{prop.Name}", prop.Value, bv, ref diffCount, depth + 1);
                    else
                    {
                        Logger.Info($"DIFF[{path}.{prop.Name}]: removed");
                        diffCount++;
                    }
                }
                foreach (var prop in b.EnumerateObject())
                    if (!a.TryGetProperty(prop.Name, out _))
                    {
                        Logger.Info($"DIFF[{path}.{prop.Name}]: added (value={Trim(prop.Value.GetRawText(), 80)})");
                        diffCount++;
                    }
                break;

            case JsonValueKind.Array:
                int la = a.GetArrayLength();
                int lb = b.GetArrayLength();
                if (la != lb)
                {
                    Logger.Info($"DIFF[{path}]: array length {la} → {lb}");
                    diffCount++;
                }
                int n      = Math.Min(la, lb);
                int sample = Math.Min(n, MAX_ARRAY_SAMPLE);
                for (int i = 0; i < sample; i++)
                    DiffJson($"{path}[{i}]", a[i], b[i], ref diffCount, depth + 1);
                if (n > sample)
                    Logger.Info($"  (DIFF: array {path} sample first {MAX_ARRAY_SAMPLE} of {n} only — 큰 array)");
                break;

            default:
                if (a.GetRawText() != b.GetRawText())
                {
                    var av = Trim(a.GetRawText(), 80);
                    var bv = Trim(b.GetRawText(), 80);
                    Logger.Info($"DIFF[{path}]: {av} → {bv}");
                    diffCount++;
                }
                break;
        }
    }

    private static string Trim(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "…";

    public static void RunPhaseB(object player)
    {
        if (_phaseBPatched)
        {
            Logger.Info("PhaseB: already patched in this session — game UI 에서 active 변경 후 log 관찰 (재실행 불필요)");
            return;
        }

        Logger.Info("PhaseB: Harmony trace 시작. 후보 method enumerate + patch.");

        // game assembly 가져오기: player.GetType().Assembly 가 Assembly-CSharp.
        // HeroLocator.GetPlayer() 의 반환 type 이 game side 이므로 player 로부터 바로 획득 가능.
        var gameAsm = player.GetType().Assembly;
        Logger.Info($"PhaseB: scanning assembly {gameAsm.GetName().Name}");

        string[] namePatterns =
        {
            "EquipKungfu", "EquipSkill", "Equiped", "SetEquip",
            "SetActive", "SwitchActive", "ChangeActive", "ToggleActive",
            "SetNowActive", "ChangeNowActive",
        };

        // type 후보 — 너무 넓으면 enumerate 비용 큼. 이름 패턴으로 type 자체 제한.
        // equiped flag 가 KungfuSkillLvData(Kungfu) 에 있으므로 Hero/Kungfu/Skill/Player 포함.
        string[] typeNamePatterns = { "Hero", "Kungfu", "Skill", "Player" };

        var harmony = new Harmony("com.deepe.longyinroster.probe.activekungfu");
        var prefix  = typeof(ProbeActiveKungfuV2).GetMethod(nameof(GenericPrefix),
                          BindingFlags.NonPublic | BindingFlags.Static);

        int patchCount = 0;
        int errorCount = 0;

        Type?[] allTypes;
        try { allTypes = gameAsm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { allTypes = ex.Types; }

        foreach (var t in allTypes)
        {
            if (t == null) continue;

            bool typeMatches = false;
            foreach (var p in typeNamePatterns)
                if (t.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) { typeMatches = true; break; }
            if (!typeMatches) continue;

            MethodInfo[] methods;
            try { methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly); }
            catch { continue; }

            foreach (var m in methods)
            {
                bool nameMatches = false;
                foreach (var p in namePatterns)
                    if (m.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0) { nameMatches = true; break; }
                if (!nameMatches) continue;

                // generic method + abstract 은 Harmony patch 불가 — skip
                if (m.IsAbstract || m.ContainsGenericParameters) continue;

                try
                {
                    harmony.Patch(m, prefix: new HarmonyMethod(prefix));
                    patchCount++;
                    Logger.Info($"PhaseB: patched {t.Name}.{m.Name}({string.Join(",", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name))})");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (errorCount <= 5)
                        Logger.Warn($"PhaseB: patch {t.Name}.{m.Name} failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        Logger.Info($"PhaseB: done. patched={patchCount}, errors={errorCount}.");
        Logger.Info("PhaseB: 사용자 — game UI 에서 active 무공 변경 → BepInEx log 의 'TRACE:' 항목 관찰.");
        _phaseBPatched = true;
    }

    private static bool _phaseBPatched = false;

    /// <summary>
    /// Harmony prefix: 패치된 모든 method 진입 시 호출됨.
    /// __originalMethod 은 Harmony 가 자동으로 주입하는 특수 파라미터.
    /// __args 는 인스턴스 메서드의 경우 [this, arg0, arg1, …] 순서.
    /// </summary>
    private static void GenericPrefix(MethodBase __originalMethod, object[] __args)
    {
        var argDesc = __args == null ? "<null>" :
            string.Join(", ", Array.ConvertAll(__args, a => a?.ToString() ?? "null"));
        Logger.Info($"TRACE: {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}({argDesc})");
    }

    public static void RunPhaseC(object player)
    {
        Logger.Warn("ProbeActiveKungfuV2.RunPhaseC: stub — T14 에서 본문 작성 예정 (in-memory)");
    }
}
