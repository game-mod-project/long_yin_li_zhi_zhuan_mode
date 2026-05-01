using System;
using System.Text.Json;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// Apply (slot → game) 의 entry point. v0.4 — 9-step selection-aware pipeline.
///
/// 변경점 vs v0.3:
///   - Apply 가 ApplySelection 추가 인자 받음 — 카테고리별 on/off 필터.
///   - SetIdentityFields / SetActiveKungfu 신규 step (v0.4 PoC 검증 후보군).
///   - RebuildItemList / RebuildSelfStorage 본문 채움 (capability gate 로 short-circuit).
///   - RebuildKungfuSkills 는 SkipKungfuSkills 로 대체 (v0.5+ 후보).
///
/// step 1~5: 부분 patch 허용 (catch + WarnedFields). step 6: fatal — throw 시
/// HasFatalError=true 로 자동복원 트리거. step 7~9: best-effort.
///
/// IL2CPP-bound HeroData 호출은 게임 안에서만 작동. 본 클래스의 unit test 는 ApplyResult
/// 와 IL2CppListOps 같은 framework 부품. step 자체 검증은 smoke.
/// </summary>
public static class PinpointPatcher
{
    public static ApplyResult Apply(string slotPlayerJson, object currentPlayer, ApplySelection selection)
    {
        if (slotPlayerJson == null) throw new ArgumentNullException(nameof(slotPlayerJson));
        if (currentPlayer == null) throw new ArgumentNullException(nameof(currentPlayer));
        if (selection == null) throw new ArgumentNullException(nameof(selection));

        // HeroLocator 의 cache 가 stale 일 수 있어 (다른 세이브 로드 후 등) Apply 진입 시
        // invalidate. currentPlayer 인자 자체는 호출자가 fresh fetch 했어야 하지만, 후속
        // step 의 helper 가 HeroLocator.GetPlayer() 다시 호출하면 일관 보장.
        HeroLocator.InvalidateCache();

        var res = new ApplyResult();
        using var doc = JsonDocument.Parse(slotPlayerJson);
        var slot = doc.RootElement;

        TryStep("SetSimpleFields",         () => SetSimpleFields(slot, currentPlayer, selection, res), res);
        TryStep("SetIdentityFields",       () => SetIdentityFields(slot, currentPlayer, selection, res), res);
        TryStep("SetActiveKungfu",         () => SetActiveKungfu(slot, currentPlayer, selection, res), res);
        TryStep("RebuildItemList",         () => RebuildItemList(slot, currentPlayer, selection, res), res);
        TryStep("RebuildSelfStorage",      () => RebuildSelfStorage(slot, currentPlayer, selection, res), res);
        TryStep("RebuildKungfuSkills",     () => SkipKungfuSkills(res), res);
        TryStep("RebuildHeroTagData",      () => RebuildHeroTagData(slot, currentPlayer, selection, res), res);
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

    private const System.Reflection.BindingFlags F =
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    private static void SetSimpleFields(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        foreach (var entry in SimpleFieldMatrix.Entries)
        {
            // v0.4: selection filter (entry.Category 별 on/off)
            bool enabled = entry.Category switch
            {
                FieldCategory.Stat        => selection.Stat,
                FieldCategory.Honor       => selection.Honor,
                FieldCategory.Skin        => selection.Skin,
                FieldCategory.SelfHouse   => selection.SelfHouse,
                FieldCategory.TalentPoint => selection.TalentTag,
                FieldCategory.Appearance  => selection.Appearance,  // v0.5 — 외형 PASS 시 entries 추가됨
                FieldCategory.None        => false,   // 부상/충성/호감 — 영구 보존, Apply 안 함
                _ => false,
            };
            if (!enabled)
            {
                res.SkippedFields.Add($"{entry.Name} (selection off)");
                continue;
            }

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
    /// false default 의 가정: log=false (silent), refreshDerived=false (Step 8
    /// RefreshSelfState 가 derived 재계산 책임). Step 8 가 미와이어 상태에서
    /// SetSimpleFields 만 호출하면 base stat 는 변경되지만 정보창 의 totalAttri 갱신 안 됨.
    /// 통합 Apply 에서는 Step 8 가 항상 같이 호출되므로 일관 동작.
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

    /// <summary>
    /// v0.4 — 9 정체성 필드 setter 직접 호출. PoC Task A2 PASS 검증.
    /// IdentityPath.Setter / BackingField / Harmony 3 path 분기.
    /// Capabilities.Identity false 면 entire step skip — selection 무관.
    /// </summary>
    private static void SetIdentityFields(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.Identity) { res.SkippedFields.Add("identity (selection off)"); return; }
        if (!Probe().Identity)   { res.SkippedFields.Add("identity (PoC failed — v0.5+ 후보)"); return; }

        foreach (var ifEntry in IdentityFieldMatrix.Entries)
        {
            if (!TryReadJsonValue(slot, ifEntry.JsonPath, ifEntry.Type, out var newVal))
            {
                res.SkippedFields.Add($"identity:{ifEntry.Name} — not in slot JSON");
                continue;
            }
            try
            {
                switch (ifEntry.Path)
                {
                    case IdentityPath.Setter:
                    {
                        var p = player.GetType().GetProperty(ifEntry.PropertyName, F);
                        if (p == null || !p.CanWrite)
                        {
                            res.WarnedFields.Add($"identity:{ifEntry.Name} — property missing or read-only");
                            continue;
                        }
                        p.SetValue(player, newVal);
                        res.AppliedFields.Add($"identity:{ifEntry.Name}");
                        break;
                    }
                    case IdentityPath.BackingField:
                    {
                        if (string.IsNullOrEmpty(ifEntry.BackingFieldName))
                        {
                            res.WarnedFields.Add($"identity:{ifEntry.Name} — BackingFieldName missing for BackingField path");
                            continue;
                        }
                        var bfFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                        var fld = player.GetType().GetField(ifEntry.BackingFieldName, bfFlags);
                        if (fld == null)
                        {
                            res.WarnedFields.Add($"identity:{ifEntry.Name} — backing field {ifEntry.BackingFieldName} not found");
                            continue;
                        }
                        fld.SetValue(player, newVal);
                        res.AppliedFields.Add($"identity:{ifEntry.Name} (backing)");
                        break;
                    }
                    case IdentityPath.Harmony:
                        res.SkippedFields.Add($"identity:{ifEntry.Name} — Harmony path not implemented in v0.4");
                        break;
                }
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"identity:{ifEntry.Name} — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// v0.4 — 활성 무공 (nowActiveSkill) 별도 step. SetNowActiveSkill 은 KungfuSkillLvData
    /// wrapper 인자 — player 의 kungfuSkills 리스트에서 skillID 매칭으로 찾아 전달.
    /// PoC Task A3 FAIL — wrapper.lv 의 의미 불일치로 capability false 고정.
    /// </summary>
    private static void SetActiveKungfu(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.ActiveKungfu) { res.SkippedFields.Add("activeKungfu (selection off)"); return; }
        if (!Probe().ActiveKungfu)   { res.SkippedFields.Add("activeKungfu (PoC failed — v0.5+ 후보)"); return; }

        if (!slot.TryGetProperty("nowActiveSkill", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
        {
            res.SkippedFields.Add("activeKungfu — nowActiveSkill not in slot JSON");
            return;
        }
        int targetID = idEl.GetInt32();

        var ksList = ReadFieldOrProperty(player, "kungfuSkills");
        if (ksList == null) { res.WarnedFields.Add("activeKungfu — kungfuSkills null"); return; }

        int n = IL2CppListOps.Count(ksList);
        object? wrapper = null;
        for (int i = 0; i < n; i++)
        {
            var entry = IL2CppListOps.Get(ksList, i);
            if (entry == null) continue;
            var idVal = ReadFieldOrProperty(entry, "skillID")
                     ?? ReadFieldOrProperty(entry, "ID");
            if (idVal == null) continue;
            if ((int)idVal == targetID) { wrapper = entry; break; }
        }
        if (wrapper == null)
        {
            res.WarnedFields.Add($"activeKungfu — player 가 skillID={targetID} 미보유 (kungfuSkills v0.5+ 후보)");
            return;
        }
        try
        {
            InvokeMethod(player, "SetNowActiveSkill", new[] { wrapper });
            res.AppliedFields.Add($"activeKungfu (skillID={targetID})");
        }
        catch (Exception ex)
        {
            res.WarnedFields.Add($"activeKungfu — {ex.GetType().Name}: {ex.Message}");
        }
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
    /// 현재 SimpleFieldMatrix 의 17 entry 는 모두 unambiguous (각 method name 마다 1
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

    /// <summary>
    /// v0.4 — kungfuSkills collection rebuild 은 v0.5+ 후보 (KungfuSkillLvData wrapper
    /// 생성 path 미해결). 활성 무공만 SetActiveKungfu step 에서 별도 처리.
    /// </summary>
    private static void SkipKungfuSkills(ApplyResult res)
    {
        res.SkippedFields.Add("kungfuSkills — collection rebuild deferred to v0.5+");
    }

    /// <summary>
    /// v0.4 — itemListData.allItem rebuild. LoseAllItem clear → GetItem(itemData) 반복.
    /// PoC Task A4 FAIL — ItemDataFactory.IsAvailable=false 로 capability false 고정.
    /// </summary>
    private static void RebuildItemList(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.ItemList) { res.SkippedFields.Add("itemList (selection off)"); return; }
        if (!Probe().ItemList)   { res.SkippedFields.Add("itemList (PoC failed — v0.5+ 후보)"); return; }

        // Clear via game-self LoseAllItem
        try { InvokeMethod(player, "LoseAllItem", System.Array.Empty<object>()); }
        catch (Exception ex) { res.WarnedFields.Add($"itemList clear — {ex.GetType().Name}: {ex.Message}"); }

        // Add each from slot
        if (!slot.TryGetProperty("itemListData", out var ild) ||
            !ild.TryGetProperty("allItem", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add("itemList — slot JSON 에 itemListData.allItem 없음");
            return;
        }
        int added = 0;
        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            int id    = entry.TryGetProperty("itemID",    out var idEl) ? idEl.GetInt32() : -1;
            int count = entry.TryGetProperty("itemCount", out var cEl)  ? cEl.GetInt32()  : 1;
            if (id < 0) continue;
            try
            {
                var itemData = ItemDataFactory.Create(id, count);
                InvokeMethod(player, "GetItem", new object?[] { itemData });
                added++;
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"itemList[{i}] id={id} — {ex.GetType().Name}: {ex.Message}");
            }
        }
        res.AppliedFields.Add($"itemList ({added}/{arr.GetArrayLength()})");
    }

    /// <summary>
    /// v0.4 — selfStorage.allItem rebuild. selfStorage 에는 LoseAllItem 동등 method 없음 —
    /// IL2CppListOps.Clear 로 raw clear, 그다음 reflection-based Add 로 채움.
    /// PoC Task A4 FAIL — Probe().SelfStorage=false 로 short-circuit (분기 미실행).
    /// </summary>
    private static void RebuildSelfStorage(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.SelfStorage) { res.SkippedFields.Add("selfStorage (selection off)"); return; }
        if (!Probe().SelfStorage)   { res.SkippedFields.Add("selfStorage (PoC failed — v0.5+ 후보)"); return; }

        // Clear via raw IL2CppListOps (selfStorage 에는 LoseAllItem 동등 method 없음)
        var storage = ReadFieldOrProperty(player, "selfStorage");
        var allItem = storage != null ? ReadFieldOrProperty(storage, "allItem") : null;
        if (allItem != null)
        {
            try { IL2CppListOps.Clear(allItem); }
            catch (Exception ex) { res.WarnedFields.Add($"selfStorage clear — {ex.GetType().Name}: {ex.Message}"); }
        }

        // Add each from slot
        if (!slot.TryGetProperty("selfStorage", out var ss) ||
            !ss.TryGetProperty("allItem", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add("selfStorage — slot JSON 에 selfStorage.allItem 없음");
            return;
        }
        int added = 0;
        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            int id    = entry.TryGetProperty("itemID",    out var idEl) ? idEl.GetInt32() : -1;
            int count = entry.TryGetProperty("itemCount", out var cEl)  ? cEl.GetInt32()  : 1;
            if (id < 0) continue;
            try
            {
                var itemData = ItemDataFactory.Create(id, count);
                if (allItem != null)
                {
                    var addMethod = allItem.GetType().GetMethod("Add",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    addMethod?.Invoke(allItem, new[] { itemData });
                }
                added++;
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"selfStorage[{i}] id={id} — {ex.GetType().Name}: {ex.Message}");
            }
        }
        res.AppliedFields.Add($"selfStorage ({added}/{arr.GetArrayLength()})");
    }

    private static void RebuildHeroTagData(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.TalentTag) { res.SkippedFields.Add("heroTagData (selection off)"); return; }

        // Clear: game-self ClearAllTempTag (raw clear 보다 안전 — IL2CPP wrapper 호환)
        try
        {
            InvokeMethod(player, "ClearAllTempTag", System.Array.Empty<object>());
            res.AppliedFields.Add("heroTagData (cleared)");
        }
        catch (Exception ex)
        {
            // ClearAllTempTag 가 dump 에 있는지 미확인 — 없으면 IL2CppListOps fallback
            try
            {
                var heroTagData = ReadFieldOrProperty(player, "heroTagData");
                if (heroTagData != null)
                {
                    var allTag = ReadFieldOrProperty(heroTagData, "allTag");
                    if (allTag != null)
                    {
                        IL2CppListOps.Clear(allTag);
                        res.AppliedFields.Add("heroTagData (cleared via IL2CppListOps fallback)");
                    }
                }
                else
                {
                    res.WarnedFields.Add($"heroTagData clear — {ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception ex2)
            {
                res.WarnedFields.Add($"heroTagData clear — {ex2.GetType().Name}: {ex2.Message}");
            }
        }

        // Add each tag from slot JSON.
        // v0.4 fix (D15 smoke): 실제 슬롯 JSON 구조는 spec 가정과 다름.
        //   spec 가정 (X): heroTagData = { allTag: [{id, lv, source, isPermanent, isHidden}] }
        //   실제 (O):     heroTagData = [{tagID, leftTime, sourceHero}]   ← Array 직접, allTag nesting 없음
        // → htd.TryGetProperty("allTag") 가 InvalidOperationException throw (Array 에 GetProperty 호출).
        // Fix: htd 자체가 Array. entry property 이름도 tagID/leftTime/sourceHero 로 정정.
        if (!slot.TryGetProperty("heroTagData", out var htd) || htd.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add("heroTagData — not array in slot JSON");
            return;
        }

        for (int i = 0; i < htd.GetArrayLength(); i++)
        {
            var entry = htd[i];
            // 실제 슬롯 schema: { tagID: int, leftTime: float, sourceHero: string|null }
            int    id         = entry.TryGetProperty("tagID",      out var idEl) ? idEl.GetInt32()  : -1;
            float  leftTime   = entry.TryGetProperty("leftTime",   out var ltEl) ? ltEl.GetSingle() : -1f;
            string sourceHero = entry.TryGetProperty("sourceHero", out var shEl) && shEl.ValueKind == JsonValueKind.String
                                ? (shEl.GetString() ?? "")
                                : "";
            if (id < 0)
            {
                res.WarnedFields.Add($"heroTagData[{i}] — tagID field missing, skipping");
                continue;
            }

            // AddTag(int id, float lv, string source, bool isPermanent, bool isHidden) 시그니처 매핑:
            //   - lv: 슬롯 JSON 에 없음. game side 가 lookup 으로 결정 — 1.0 default
            //   - source: sourceHero null 이면 ""
            //   - isPermanent: leftTime == -1 이면 영구. 양수면 temporary
            //   - isHidden: 슬롯 JSON 에 없음 — false default
            bool  isPermanent = leftTime < 0f;
            float lv          = 1.0f;
            bool  isHidden    = false;

            try
            {
                InvokeMethod(player, "AddTag", new object[] { id, lv, sourceHero, isPermanent, isHidden });
                res.AppliedFields.Add($"heroTag[{id}]");
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"heroTag[{id}] — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void RefreshSelfState(object player, ApplyResult res)
    {
        // spec §7.2.1 Step 6 매핑 (dump 후 확정).
        // HANDOFF §4.4 의 GetMaxAttri / GetMaxFightSkill / GetMaxLivingSkill / GetMaxFavor
        // / GetFinalTravelSpeed 는 read-only Single-getter — refresh 효과 없음. 호출 안 함.
        TryInvokeNoArg(player, "RefreshMaxAttriAndSkill",        res);
        TryInvokeNoArg(player, "RefreshHeroSalaryAndPopulation", res);
        TryInvokeNoArg(player, "RecoverState",                   res);
    }

    private static void TryInvokeNoArg(object obj, string methodName, ApplyResult res)
    {
        try
        {
            var t = obj.GetType();
            var m = t.GetMethod(methodName, F, null, Type.EmptyTypes, null);
            if (m == null) { res.SkippedFields.Add($"refresh:{methodName} — missing"); return; }
            m.Invoke(obj, null);
            res.AppliedFields.Add($"refresh:{methodName}");
        }
        catch (Exception ex)
        {
            res.WarnedFields.Add($"refresh:{methodName} — {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RefreshExternalManagers(object player, ApplyResult res)
    {
        // spec §7.2.1 Step 7 매핑: BigMapController.RefreshBigMapNPC 만 (HeroIcon /
        // HeroPanel 매니저 미발견). AuctionController 는 hero refresh 의도 아님 — 호출 안 함.
        // 나머지 시각 갱신은 game frame 의 자연 lazy-load 에 위임.
        TryInvokeManager("BigMapController", "RefreshBigMapNPC", player, res);
    }

    private static void TryInvokeManager(string typeName, string methodName, object player, ApplyResult res)
    {
        try
        {
            var t = FindGameType(typeName);
            if (t == null) { res.SkippedFields.Add($"mgr:{typeName} — type not found"); return; }
            var inst = ReadStaticInstance(t);
            if (inst == null) { res.SkippedFields.Add($"mgr:{typeName}.Instance — null"); return; }
            var m = t.GetMethod(methodName, F);
            if (m == null) { res.SkippedFields.Add($"mgr:{typeName}.{methodName} — missing"); return; }
            m.Invoke(inst, new object[] { player });
            res.AppliedFields.Add($"mgr:{typeName}.{methodName}");
        }
        catch (Exception ex)
        {
            res.WarnedFields.Add($"mgr:{typeName}.{methodName} — {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static Type? FindGameType(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var t = asm.GetType(typeName, throwOnError: false);
                if (t != null) return t;
                foreach (var t2 in asm.GetTypes())
                    if (t2.Name == typeName && (t2.Namespace == null || !t2.Namespace.StartsWith("LongYinRoster")))
                        return t2;
            }
            catch { }
        }
        return null;
    }

    private static object? ReadStaticInstance(Type t)
    {
        const System.Reflection.BindingFlags SF = System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.FlattenHierarchy;
        var p = t.GetProperty("Instance", SF);
        if (p != null) return p.GetValue(null);
        var f = t.GetField("Instance", SF);
        if (f != null) return f.GetValue(null);
        foreach (var alt in new[] { "instance", "_instance", "s_Instance", "s_instance" })
        {
            var pa = t.GetProperty(alt, SF);
            if (pa != null) return pa.GetValue(null);
            var fa = t.GetField(alt, SF);
            if (fa != null) return fa.GetValue(null);
        }
        return null;
    }

    // ---------------------------------------------------------------- v0.4 capability probe

    private static Capabilities? _capCache;

    /// <summary>
    /// v0.4 — Capability probe. Plugin / ModWindow 가 lazy 호출 후 cache 사용.
    /// player == null (게임 미진입) 이면 AllOff. 비파괴 — 구조 검사만 (set 안 함).
    /// PoC Phase A 결과 반영: A2 Identity PASS, A3 ActiveKungfu FAIL, A4 ItemList/SelfStorage FAIL.
    /// </summary>
    public static Capabilities Probe()
    {
        if (_capCache != null) return _capCache;
        var p = HeroLocator.GetPlayer();
        if (p == null) return _capCache = Capabilities.AllOff();

        bool identity     = ProbeIdentityCapability(p);
        bool activeKungfu = ProbeActiveKungfuCapability(p);
        bool itemList     = ProbeItemListCapability(p);
        bool selfStorage  = itemList;   // 둘 다 ItemDataFactory 공유

        _capCache = new Capabilities
        {
            Identity     = identity,
            ActiveKungfu = activeKungfu,
            ItemList     = itemList,
            SelfStorage  = selfStorage,
        };
        Logger.Info($"PinpointPatcher.Probe → {_capCache}");
        return _capCache;
    }

    public static void InvalidateProbeCache() => _capCache = null;

    // Simplified capability checks — no destructive set, just structural verification.
    // Per Phase A PoC: A2 PASS (Identity), A3 FAIL (ActiveKungfu), A4 FAIL (ItemList/SelfStorage).
    private static bool ProbeIdentityCapability(object p)
    {
        var prop = p.GetType().GetProperty("heroName", F);
        return prop != null && prop.CanWrite;
    }

    private static bool ProbeActiveKungfuCapability(object p)
    {
        // PoC A3 FAIL — semantic mapping wrong (wrapper.lv vs nowActiveSkill ID).
        // Hardcoded false until v0.5+ resolves the semantic.
        return false;
    }

    private static bool ProbeItemListCapability(object p)
    {
        // PoC A4 FAIL — sub-data wrapper graph (equipmentData/medFoodData/etc) unsolved.
        // ItemDataFactory.IsAvailable returns false in v0.4 stub. Both gates must pass.
        return ItemDataFactory.IsAvailable
            && p.GetType().GetMethod("LoseAllItem", F) != null
            && p.GetType().GetMethod("GetItem", F) != null;
    }
}
