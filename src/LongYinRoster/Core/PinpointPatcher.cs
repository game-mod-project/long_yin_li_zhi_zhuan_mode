using System;
using System.Text.Json;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// Apply (slot → game) 의 entry point. 7-step pipeline 으로 game-self method 호출
/// (직접 reflection setter 거부 — Populate 가 silent no-op 인 같은 함정 회피).
///
/// step 1~5: 부분 patch 허용 (catch + WarnedFields). step 6: fatal — throw 시
/// HasFatalError=true 로 자동복원 트리거. step 7: best-effort.
///
/// IL2CPP-bound HeroData 호출은 게임 안에서만 작동. 본 클래스의 unit test 는 ApplyResult
/// 와 IL2CppListOps 같은 framework 부품. step 자체 검증은 smoke.
/// </summary>
public static class PinpointPatcher
{
    public static ApplyResult Apply(string slotPlayerJson, object currentPlayer)
    {
        if (slotPlayerJson == null) throw new ArgumentNullException(nameof(slotPlayerJson));
        if (currentPlayer == null) throw new ArgumentNullException(nameof(currentPlayer));

        var res = new ApplyResult();
        using var doc = JsonDocument.Parse(slotPlayerJson);
        var slot = doc.RootElement;

        TryStep("SetSimpleFields",         () => SetSimpleFields(slot, currentPlayer, res), res);
        TryStep("RebuildKungfuSkills",     () => RebuildKungfuSkills(slot, currentPlayer, res), res);
        TryStep("RebuildItemList",         () => RebuildItemList(slot, currentPlayer, res), res);
        TryStep("RebuildSelfStorage",      () => RebuildSelfStorage(slot, currentPlayer, res), res);
        TryStep("RebuildHeroTagData",      () => RebuildHeroTagData(slot, currentPlayer, res), res);
        TryStep("RefreshSelfState",        () => RefreshSelfState(currentPlayer, res), res, fatal: true);
        TryStep("RefreshExternalManagers", () => RefreshExternalManagers(currentPlayer, res), res);

        Logger.Info($"PinpointPatcher.Apply done — applied={res.AppliedFields.Count} " +
                    $"skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count} " +
                    $"errors={res.StepErrors.Count} fatal={res.HasFatalError}");
        return res;
    }

    private static void TryStep(string name, Action body, ApplyResult res, bool fatal = false)
    {
        try { body(); }
        catch (Exception ex)
        {
            Logger.Warn($"PinpointPatcher.{name} threw: {ex.GetType().Name}: {ex.Message}");
            res.StepErrors.Add(ex);
            if (fatal) res.HasFatalError = true;
        }
    }

    // 각 step 은 Task 7~13 에서 채운다. 본 task 는 골격만 — body 는 throw 로 시작.
    private static void SetSimpleFields(JsonElement slot, object player, ApplyResult res) =>
        throw new NotImplementedException("Task 7 에서 채움");

    private static void RebuildKungfuSkills(JsonElement slot, object player, ApplyResult res) =>
        throw new NotImplementedException("Task 8 에서 채움");

    private static void RebuildItemList(JsonElement slot, object player, ApplyResult res) =>
        throw new NotImplementedException("Task 9 에서 채움");

    private static void RebuildSelfStorage(JsonElement slot, object player, ApplyResult res) =>
        throw new NotImplementedException("Task 10 에서 채움");

    private static void RebuildHeroTagData(JsonElement slot, object player, ApplyResult res) =>
        throw new NotImplementedException("Task 11 에서 채움");

    private static void RefreshSelfState(object player, ApplyResult res) =>
        throw new NotImplementedException("Task 12 에서 채움");

    private static void RefreshExternalManagers(object player, ApplyResult res) =>
        throw new NotImplementedException("Task 13 에서 채움");
}
