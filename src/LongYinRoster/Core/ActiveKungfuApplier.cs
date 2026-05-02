using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5.1 — 무공 active full Apply / Restore.
///
/// 기존 v0.4 SetActiveKungfu (nowActiveSkill ID + SetNowActiveSkill(wrapper)) 는 잘못된 source
/// 를 사용. v0.5 Phase A/B PoC + v0.5.1 Spike 결과로 새 path 확정:
///   - Source-of-truth: kungfuSkills[i].equiped (NOT nowActiveSkill)
///   - ID field: KungfuSkillLvData.skillID (NOT kungfuID — wrapper.kungfuID 는 -1 fallback)
///   - Method path: HeroData.EquipSkill(wrapper, true) + HeroData.UnequipSkill(wrapper, true)
///   - 패턴: 11-swap (game 자체 동작 mirror — Phase B trace 결과). v0.5.1 Spike 검증:
///     11-swap 으로 UI cache invalidate trigger + save→reload persistence PASS.
///   - UI refresh: 11-swap 자체 + flag toggle (skillIconDirty/maxManaChanged/HeroIconDirty/heroIconDirtyCount++)
///     으로 추가 robustness.
///
/// IL2CPP 한계: 게임 측 호출 (EquipSkill / UnequipSkill / flag toggle) 은 mock 불가.
/// 본 클래스의 unit test 는 ExtractEquippedSkillIDs (slot JSON 파싱) + Apply 의 selection gate
/// 만 검증. swap 자체는 smoke 로 확인.
/// </summary>
public static class ActiveKungfuApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int UnequipCount { get; set; }
        public int EquipCount { get; set; }
        public int MissingCount { get; set; }
    }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// slot JSON 의 kungfuSkills[].equiped == true 인 entry 의 skillID 수집 (중복 제거).
    /// max 11 entries (game design).
    /// </summary>
    public static IReadOnlyList<int> ExtractEquippedSkillIDs(JsonElement slot)
    {
        var ids = new List<int>();
        if (!slot.TryGetProperty("kungfuSkills", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return ids;

        var seen = new HashSet<int>();
        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("equiped", out var eq) || eq.ValueKind != JsonValueKind.True) continue;
            if (!entry.TryGetProperty("skillID", out var id) || id.ValueKind != JsonValueKind.Number) continue;
            int v = id.GetInt32();
            if (seen.Add(v)) ids.Add(v);
        }
        return ids;
    }

    /// <summary>
    /// Apply slot active set to player. 11-swap pattern (game 자체 mirror).
    /// </summary>
    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.ActiveKungfu)
        {
            res.Skipped = true;
            res.Reason = "activeKungfu (selection off)";
            return res;
        }

        var ids = ExtractEquippedSkillIDs(slot);
        if (player == null)
        {
            res.Skipped = true;
            res.Reason = "player null (test mode)";
            return res;
        }

        var ksList = ReadFieldOrProperty(player, "kungfuSkills");
        if (ksList == null)
        {
            res.Skipped = true;
            res.Reason = "kungfuSkills null";
            return res;
        }

        // Match wrappers
        int n = IL2CppListOps.Count(ksList);
        var equipTargets = new List<object>();
        var idSet = new HashSet<int>(ids);
        var matchedIDs = new HashSet<int>();
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            int sid = (int)(ReadFieldOrProperty(w, "skillID") ?? -1);
            if (sid >= 0 && idSet.Contains(sid))
            {
                equipTargets.Add(w);
                matchedIDs.Add(sid);
            }
        }
        res.MissingCount = ids.Count - matchedIDs.Count;
        if (res.MissingCount > 0)
            Logger.Warn($"ActiveKungfu: slot 의 skillID {res.MissingCount} 개가 현재 list 에 없음 — skip (v0.6 list sub-project)");

        // Current equipped
        var currentEquipped = new List<object>();
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            if ((bool)(ReadFieldOrProperty(w, "equiped") ?? false))
                currentEquipped.Add(w);
        }

        // Unequip phase — game 자체 패턴 mirror (Phase B trace: 11×UnequipSkill → 11×EquipSkill).
        // currentEquipped ∪ equipTargets 모두 unequip — 이게 game 의 UI cache invalidate trigger
        // 핵심 + Equip phase 직전 모든 wrapper 가 false 상태로 reset 되어 silent fail 회피.
        // 0 active → N active 시나리오에서는 equipTargets 만 unequip (no-op but trigger).
        // self-Apply (같은 set) 도 모든 wrapper unequip → 다시 equip 으로 일관 동작.
        var unequipSet = new HashSet<object>(currentEquipped);
        foreach (var t in equipTargets) unequipSet.Add(t);

        foreach (var w in unequipSet)
        {
            try
            {
                InvokeMethod(player, "UnequipSkill", new[] { w, (object)true });
                res.UnequipCount++;
            }
            catch (Exception ex)
            {
                Logger.Warn($"ActiveKungfu UnequipSkill: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Equip phase
        foreach (var w in equipTargets)
        {
            try
            {
                InvokeMethod(player, "EquipSkill", new[] { w, (object)true });
                res.EquipCount++;
            }
            catch (Exception ex)
            {
                Logger.Warn($"ActiveKungfu EquipSkill: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // UI refresh trigger (Spike Step 2 결과: 11-swap 으로 UI invalidate 작동.
        // flag toggle 은 추가 robustness — 일부 wrapper 가 silent fail 해도 UI 는 갱신).
        TriggerUiRefresh(player, currentEquipped, equipTargets);

        Logger.Info($"ActiveKungfu Apply done — unequip={res.UnequipCount} equip={res.EquipCount} missing={res.MissingCount}");
        return res;
    }

    /// <summary>
    /// Restore = backup JSON 으로 Apply 동일 로직. force selection.ActiveKungfu = true.
    /// </summary>
    public static Result Restore(object? player, JsonElement backup)
    {
        return Apply(player, backup, new ApplySelection { ActiveKungfu = true });
    }

    private static void TriggerUiRefresh(object player, List<object> changedFromUnequip, List<object> changedFromEquip)
    {
        foreach (var w in changedFromUnequip) { TrySetField(w, "skillIconDirty", true); TrySetField(w, "maxManaChanged", true); }
        foreach (var w in changedFromEquip)   { TrySetField(w, "skillIconDirty", true); TrySetField(w, "maxManaChanged", true); }
        TrySetField(player, "HeroIconDirty", true);

        var cntField = player.GetType().GetField("heroIconDirtyCount", F);
        if (cntField != null)
        {
            try
            {
                int cur = (int)(cntField.GetValue(player) ?? 0);
                cntField.SetValue(player, cur + 1);
            }
            catch { }
        }
    }

    private static object? ReadFieldOrProperty(object obj, string name)
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
        if (best == null) throw new MissingMethodException(t.FullName, methodName);
        var ps2 = best.GetParameters();
        var full = new object?[ps2.Length];
        for (int i = 0; i < ps2.Length; i++)
            full[i] = i < args.Length ? args[i]
                : (ps2[i].ParameterType.IsValueType ? Activator.CreateInstance(ps2[i].ParameterType) : null);
        best.Invoke(obj, full);
    }
}
