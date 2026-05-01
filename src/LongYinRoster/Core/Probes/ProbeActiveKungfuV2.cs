using System;
using System.Text.Json;
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
        Logger.Warn("ProbeActiveKungfuV2.RunPhaseB: stub — T13 에서 본문 작성 예정 (Harmony trace)");
    }

    public static void RunPhaseC(object player)
    {
        Logger.Warn("ProbeActiveKungfuV2.RunPhaseC: stub — T14 에서 본문 작성 예정 (in-memory)");
    }
}
