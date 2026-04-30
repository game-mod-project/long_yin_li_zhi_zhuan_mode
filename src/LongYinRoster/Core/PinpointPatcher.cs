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

        // HeroLocator 의 cache 가 stale 일 수 있어 (다른 세이브 로드 후 등) Apply 진입 시
        // invalidate. currentPlayer 인자 자체는 호출자가 fresh fetch 했어야 하지만, 후속
        // step 의 helper 가 HeroLocator.GetPlayer() 다시 호출하면 일관 보장.
        HeroLocator.InvalidateCache();

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

    // 각 step 은 Task 7~13 에서 채운다.

    private const System.Reflection.BindingFlags F =
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    private static void SetSimpleFields(JsonElement slot, object player, ApplyResult res)
    {
        foreach (var entry in SimpleFieldMatrix.Entries)
        {
            // Special-case: skinID multi-arg (SetSkin(Int32, Int32))
            if (entry.PropertyName == "skinID")
            {
                ApplySkinSpecialCase(slot, player, entry, res);
                continue;
            }

            // Special-case: list-indexed (baseAttri / baseFightSkill / baseLivingSkill / expLivingSkill)
            if (entry.JsonPath == "baseAttri" || entry.JsonPath == "baseFightSkill" ||
                entry.JsonPath == "baseLivingSkill" || entry.JsonPath == "expLivingSkill")
            {
                ApplyListIndexedSpecialCase(slot, player, entry, res);
                continue;
            }

            // Regular path
            if (!TryReadJsonValue(slot, entry.JsonPath, entry.Type, out var newValue))
            {
                res.SkippedFields.Add($"{entry.Name} — not in slot JSON");
                continue;
            }

            var currentValue = ReadFieldOrProperty(player, entry.PropertyName);
            if (Equals(currentValue, newValue))
            {
                res.AppliedFields.Add($"{entry.Name} (no-op)");
                continue;
            }

            if (entry.SetterMethod == null || entry.SetterStyle == SetterStyle.None)
            {
                res.SkippedFields.Add($"{entry.Name} — no setter mapped");
                continue;
            }

            try
            {
                var primaryArg = entry.SetterStyle switch
                {
                    SetterStyle.Direct => newValue,
                    SetterStyle.Delta  => Subtract(newValue, currentValue, entry.Type),
                    _ => throw new InvalidOperationException("unreachable")
                };
                InvokeMethod(player, entry.SetterMethod!, new[] { primaryArg });
                res.AppliedFields.Add(entry.Name);
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"{entry.Name} — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Special-case: skinID 와 skinLv 를 함께 SetSkin(skinID, skinLv) 로 호출.
    /// dump 시그니처: Void SetSkin(Int32, Int32). spec §7.2.1 Step 1 의 entry "스킨".
    /// </summary>
    private static void ApplySkinSpecialCase(JsonElement slot, object player, SimpleFieldEntry entry, ApplyResult res)
    {
        if (!TryReadJsonValue(slot, "skinID", typeof(int), out var skinIdVal) ||
            !TryReadJsonValue(slot, "skinLv", typeof(int), out var skinLvVal))
        {
            res.SkippedFields.Add("스킨 — skinID/skinLv not in slot JSON");
            return;
        }
        try
        {
            InvokeMethod(player, "SetSkin", new object?[] { skinIdVal, skinLvVal });
            res.AppliedFields.Add("스킨");
        }
        catch (Exception ex)
        {
            res.WarnedFields.Add($"스킨 — {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Special-case: list-indexed (baseAttri / baseFightSkill / baseLivingSkill / expLivingSkill).
    /// JSON array 의 each index 마다 SetterMethod(index, delta, ...) 호출.
    /// dump 시그니처:
    ///   Void ChangeAttri(Int32, Single, Boolean, Boolean)
    ///   Void ChangeFightSkill(Int32, Single, Boolean, Boolean)
    ///   Void ChangeLivingSkill(Int32, Single, Boolean, Boolean)
    ///   Void ChangeLivingSkillExp(Int32, Single, Boolean)
    /// 추가 boolean flag 들은 InvokeMethod 가 default(Boolean)=false 로 padding.
    ///
    /// **Boolean flag default false 의 의미**: dump 의 ChangeAttri / ChangeFightSkill /
    /// ChangeLivingSkill / ChangeLivingSkillExp 시그니처가 (Int32 index, Single delta,
    /// Boolean log, Boolean refreshDerived) — 마지막 두 boolean 의 의미는 dump 만으로 추정.
    /// false default 의 가정: log=false (silent), refreshDerived=false (Step 6
    /// RefreshSelfState 가 derived 재계산 책임). Step 6 (Task 12) 가 미와이어 상태에서
    /// SetSimpleFields 만 호출하면 base stat 는 변경되지만 정보창 의 totalAttri 갱신 안 됨.
    /// 통합 Apply (Task 16) 에서는 Step 6 가 항상 같이 호출되므로 일관 동작.
    /// </summary>
    private static void ApplyListIndexedSpecialCase(JsonElement slot, object player, SimpleFieldEntry entry, ApplyResult res)
    {
        if (entry.SetterMethod == null) { res.SkippedFields.Add($"{entry.Name} — no setter mapped"); return; }
        if (!slot.TryGetProperty(entry.JsonPath, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add($"{entry.Name} — not array in slot JSON");
            return;
        }

        // 현재 player 의 list field 읽기 (delta 계산용)
        var il2List = ReadFieldOrProperty(player, entry.PropertyName);
        if (il2List == null) { res.SkippedFields.Add($"{entry.Name} — current list field missing"); return; }

        int slotN = arr.GetArrayLength();
        int curN  = IL2CppListOps.Count(il2List);
        int n = System.Math.Min(slotN, curN);
        int applied = 0;
        for (int i = 0; i < n; i++)
        {
            try
            {
                float newVal = arr[i].GetSingle();
                var curRaw = IL2CppListOps.Get(il2List, i);
                float curVal = curRaw is float f ? f : System.Convert.ToSingle(curRaw);
                if (System.Math.Abs(newVal - curVal) < 0.0001f) continue;
                float delta = newVal - curVal;
                // SetterMethod(int index, float delta, ...) — 추가 인자는 InvokeMethod 가 padding
                InvokeMethod(player, entry.SetterMethod, new object?[] { i, delta });
                applied++;
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"{entry.Name}[{i}] — {ex.GetType().Name}: {ex.Message}");
            }
        }
        res.AppliedFields.Add($"{entry.Name} ({applied}/{n})");
    }

    private static bool TryReadJsonValue(JsonElement slot, string path, Type type, out object? value)
    {
        value = null;
        var cur = slot;
        foreach (var part in path.Split('.'))
        {
            if (cur.ValueKind != JsonValueKind.Object) return false;
            if (!cur.TryGetProperty(part, out cur)) return false;
        }
        try
        {
            if (type == typeof(int))    { value = cur.GetInt32(); return true; }
            if (type == typeof(long))   { value = cur.GetInt64(); return true; }
            if (type == typeof(string)) { value = cur.GetString() ?? ""; return true; }
            if (type == typeof(bool))   { value = cur.GetBoolean(); return true; }
            if (type == typeof(float))  { value = cur.GetSingle(); return true; }
            if (type == typeof(double)) { value = cur.GetDouble(); return true; }
        }
        catch { return false; }
        return false;
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

    /// <summary>
    /// Reflection-based method invoke. dump 의 game 메서드 다수가 plan 가정보다 많은 인자를
    /// 받는다 (e.g., ChangeFame(Single, Boolean), ChangeAttri(Int32, Single, Boolean, Boolean),
    /// ChangeHp(Single, Boolean, Boolean, Boolean, Boolean)). caller 는 primary 인자만 전달
    /// 하고, 나머지는 default(T) 로 padding 한다 — bool=false, int=0, float=0f, ref-type=null.
    /// 만약 같은 이름의 overload 가 여럿이면 caller 의 args.Length 이상이고 최소인 것을 선택.
    ///
    /// **Tiebreaker**: 동일 길이 overload 가 여러 개면 first-encountered 가 win.
    /// 현재 SimpleFieldMatrix 의 18 entry 는 모두 unambiguous (각 method name 마다 1
    /// signature). 추후 같은 name 의 overload 가 entry 의 setter 로 추가되면 명시
    /// signature mapping 필요.
    /// </summary>
    private static void InvokeMethod(object obj, string methodName, object?[] args)
    {
        var t = obj.GetType();
        // overload 가능성 — caller 가 제공한 args.Length 이상 받으면서 최소 길이의 method 선택
        System.Reflection.MethodInfo? best = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != methodName) continue;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) continue;
            if (best == null || ps.Length < best.GetParameters().Length) best = m;
        }
        if (best == null)
            throw new MissingMethodException(t.FullName, methodName);

        var parameters = best.GetParameters();
        var fullArgs = new object?[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i < args.Length)
            {
                fullArgs[i] = args[i];
            }
            else
            {
                var pt = parameters[i].ParameterType;
                fullArgs[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
            }
        }
        best.Invoke(obj, fullArgs);
    }

    private static object Subtract(object? newValue, object? currentValue, Type type)
    {
        if (type == typeof(int))    return ((int)newValue!)    - ((int?)currentValue ?? 0);
        if (type == typeof(long))   return ((long)newValue!)   - ((long?)currentValue ?? 0L);
        if (type == typeof(float))  return ((float)newValue!)  - ((float?)currentValue ?? 0f);
        if (type == typeof(double)) return ((double)newValue!) - ((double?)currentValue ?? 0d);
        throw new InvalidOperationException($"Delta not supported for type {type.Name}");
    }

    private static void RebuildKungfuSkills(JsonElement slot, object player, ApplyResult res)
    {
        // v0.3: collection rebuild 미지원 — primitive-factory AddKungfuSkill 가 dump 에 부재
        // (모든 Add method 가 KungfuSkillLvData wrapper 객체 인자). v0.4 후보 (spec §12).
        res.SkippedFields.Add("kungfuSkills — collection rebuild deferred to v0.4");
    }

    private static void RebuildItemList(JsonElement slot, object player, ApplyResult res)
    {
        // v0.3: collection rebuild 미지원 — primitive-factory AddItem 가 dump 에 부재
        // (모든 Add method 가 ItemData wrapper 객체 인자). v0.4 후보 (spec §12).
        res.SkippedFields.Add("itemListData.allItem — collection rebuild deferred to v0.4");
    }

    private static void RebuildSelfStorage(JsonElement slot, object player, ApplyResult res) =>
        throw new NotImplementedException("Task 10 에서 채움");

    private static void RebuildHeroTagData(JsonElement slot, object player, ApplyResult res) =>
        throw new NotImplementedException("Task 11 에서 채움");

    private static void RefreshSelfState(object player, ApplyResult res) =>
        throw new NotImplementedException("Task 12 에서 채움");

    private static void RefreshExternalManagers(object player, ApplyResult res) =>
        throw new NotImplementedException("Task 13 에서 채움");
}
