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

        // v0.5.3 fix — Probe cache 도 SaveSlot 전환 후 stale 가능. 특히 Probe() 가
        // 게임 진입 전 (player==null) 한 번이라도 호출되면 _capCache = AllOff 영구 캐시되어
        // 모든 capability 가 false 로 잠김. Apply 진입 시 강제 invalidate 로 다음 Probe()
        // 가 fresh fetch 보장. (ModWindow 의 토글 UI 가 Probe.X false 시 비활성화되므로
        // 사용자 체감으로는 "토글했는데 적용 안 됨" 형태로 나타남.)
        InvalidateProbeCache();

        var res = new ApplyResult();
        using var doc = JsonDocument.Parse(slotPlayerJson);
        var slot = doc.RootElement;

        // v0.5.2 — RebuildKungfuSkills 가 SetActiveKungfu 직전 (list 정확화 후 active 매칭)
        TryStep("SetSimpleFields",         () => SetSimpleFields(slot, currentPlayer, selection, res), res);
        TryStep("SetIdentityFields",       () => SetIdentityFields(slot, currentPlayer, selection, res), res);
        TryStep("RebuildKungfuSkills",     () => RebuildKungfuSkills(slot, currentPlayer, selection, res), res);
        TryStep("SetActiveKungfu",         () => SetActiveKungfu(slot, currentPlayer, selection, res), res);
        TryStep("RebuildItemList",         () => RebuildItemList(slot, currentPlayer, selection, res), res);
        TryStep("RebuildSelfStorage",      () => RebuildSelfStorage(slot, currentPlayer, selection, res), res);
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
    /// v0.5.1 — 무공 active full step. ActiveKungfuApplier (kungfuSkills[i].equiped + 11-swap
    /// pattern + UI refresh) 호출. v0.4 의 SetNowActiveSkill 잘못된 path 교체.
    /// </summary>
    private static void SetActiveKungfu(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.ActiveKungfu) { res.SkippedFields.Add("activeKungfu (selection off)"); return; }
        if (!Probe().ActiveKungfu)   { res.SkippedFields.Add("activeKungfu (capability off)"); return; }

        var r = ActiveKungfuApplier.Apply(player, slot, selection);
        if (r.Skipped)
        {
            res.SkippedFields.Add($"activeKungfu — {r.Reason}");
            return;
        }
        res.AppliedFields.Add($"activeKungfu (unequip={r.UnequipCount} equip={r.EquipCount} missing={r.MissingCount})");
        if (r.MissingCount > 0)
            res.WarnedFields.Add($"activeKungfu — {r.MissingCount} skillID 미보유 (v0.6 list sub-project)");
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
    /// v0.5.2 — 무공 list Replace step. KungfuListApplier (LoseAllSkill clear + ctor(int)
    /// wrapper + GetSkill add) 호출. v0.5.1 의 SkipKungfuSkills stub 교체.
    /// </summary>
    private static void RebuildKungfuSkills(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.KungfuList) { res.SkippedFields.Add("kungfuList (selection off)"); return; }
        if (!Probe().KungfuList)   { res.SkippedFields.Add("kungfuList (capability off)"); return; }

        var r = KungfuListApplier.Apply(player, slot, selection);
        if (r.Skipped) { res.SkippedFields.Add($"kungfuList — {r.Reason}"); return; }
        res.AppliedFields.Add($"kungfuList (removed={r.RemovedCount} added={r.AddedCount} failed={r.FailedCount})");
        if (r.FailedCount > 0)
            res.WarnedFields.Add($"kungfuList — {r.FailedCount} entries failed");
    }

    /// <summary>
    /// v0.4 — itemListData.allItem rebuild. LoseAllItem clear → GetItem(itemData) 반복.
    /// PoC Task A4 FAIL — ItemDataFactory.IsAvailable=false 로 capability false 고정.
    /// </summary>
    /// <summary>
    /// v0.5.3 — 인벤토리 list Replace. ItemListApplier (LoseAllItem clear + parameterless ctor +
    /// 모든 property reflection set + GetItem add + 2-pass retry) 호출. v0.4 stub 의
    /// ItemDataFactory 의존 제거.
    /// </summary>
    private static void RebuildItemList(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        // v0.5.3 진단 로그 — 사용자 보고 "인벤토리 반영안됨" 추적용. selection / probe
        // 값을 명시 출력해 어느 게이트에서 막혔는지 식별.
        var probeItem = Probe().ItemList;
        Logger.Info($"RebuildItemList gate: selection.ItemList={selection.ItemList} Probe.ItemList={probeItem}");
        if (!selection.ItemList) { res.SkippedFields.Add("itemList (selection off)"); return; }
        if (!probeItem)          { res.SkippedFields.Add("itemList (capability off)"); return; }

        var r = ItemListApplier.Apply(player, slot, selection);
        if (r.Skipped)
        {
            // v0.5.3 진단 — skip reason 을 SkippedFields list 에만 묻으면 사용자 BepInEx
            // log 에서 거의 안 보임. Info 레벨로 즉시 출력.
            Logger.Info($"ItemList Apply skipped: {r.Reason}");
            res.SkippedFields.Add($"itemList — {r.Reason}");
            return;
        }
        res.AppliedFields.Add($"itemList (removed={r.RemovedCount} added={r.AddedCount} failed={r.FailedCount})");
        if (r.FailedCount > 0)
            res.WarnedFields.Add($"itemList — {r.FailedCount} entries failed");
    }

    /// <summary>
    /// v0.5.3 — selfStorage 는 별도 sub-project (v0.6.x). capability false 로 short-circuit.
    /// 인벤토리 (RebuildItemList) 와 패턴 동일 — ItemListApplier 패턴 mirror 후 enable 가능.
    /// </summary>
    private static void RebuildSelfStorage(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
    {
        if (!selection.SelfStorage) { res.SkippedFields.Add("selfStorage (selection off)"); return; }
        if (!Probe().SelfStorage)   { res.SkippedFields.Add("selfStorage (capability off — v0.6.x sub-project)"); return; }
        // 활성화 시 ItemListApplier 패턴 mirror 으로 구현 (v0.6.x)
        res.SkippedFields.Add("selfStorage — v0.6.x sub-project");
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

        // v0.5.1 fix — 영구 천부 중복 추가 방지. ClearAllTempTag 가 영구 tag 는 clear 안 하므로
        // slot JSON 의 영구 tag 가 player 에 이미 있으면 AddTag 호출 시 중복 추가됨. clear 후
        // 시점의 player.heroTagData 의 tagID 를 수집해 already-exists 검사.
        var existingHeroTagData = ReadFieldOrProperty(player, "heroTagData");
        var existingTagIDs = new System.Collections.Generic.HashSet<int>();
        if (existingHeroTagData != null)
        {
            try
            {
                int existCount = IL2CppListOps.Count(existingHeroTagData);
                for (int j = 0; j < existCount; j++)
                {
                    var e = IL2CppListOps.Get(existingHeroTagData, j);
                    if (e == null) continue;
                    var tid = ReadFieldOrProperty(e, "tagID");
                    if (tid is int tidInt) existingTagIDs.Add(tidInt);
                }
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"heroTagData existing scan — {ex.GetType().Name}: {ex.Message}");
            }
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

            // v0.5.1 fix — already-exists 검사 (영구 천부 중복 방지)
            if (existingTagIDs.Contains(id))
            {
                res.SkippedFields.Add($"heroTag[{id}] (이미 존재 — 중복 방지)");
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
        // v0.5.3 fix — player==null 일 때 cache 하지 않음. 메인 메뉴 / 게임 진입 전
        // 시점에 Probe() 가 호출되면 AllOff 가 영구 캐싱되어 게임 진입 후에도 모든
        // capability 가 false 로 잠긴다. cache 미저장으로 다음 호출 시 retry 가능.
        if (p == null) return Capabilities.AllOff();

        bool identity     = ProbeIdentityCapability(p);
        bool activeKungfu = ProbeActiveKungfuCapability(p);
        bool itemList     = ProbeItemListCapability(p);
        bool selfStorage  = itemList;   // 둘 다 ItemDataFactory 공유
        bool kungfuList   = ProbeKungfuListCapability(p);   // v0.5.2

        _capCache = new Capabilities
        {
            Identity     = identity,
            ActiveKungfu = activeKungfu,
            ItemList     = itemList,
            SelfStorage  = selfStorage,
            KungfuList   = kungfuList,
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
        // v0.5.1 — Spike PASS 후 method path 확정 (kungfuSkills[i].equiped + EquipSkill/UnequipSkill).
        // 두 method 모두 존재하면 capability ok.
        return p.GetType().GetMethod("EquipSkill", F) != null
            && p.GetType().GetMethod("UnequipSkill", F) != null;
    }

    private static bool ProbeItemListCapability(object p)
    {
        // v0.5.3 — ItemDataFactory 폐기. method 존재 검사만 (lazy ctor 검사는 ItemListApplier.Apply 시).
        // GetItem 은 여러 overload (3개) — GetMethod() 로는 ambiguous, GetMethods() 로 name 검사.
        var t = p.GetType();
        if (t.GetMethod("LoseAllItem", F, null, Type.EmptyTypes, null) == null) return false;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name == "GetItem") return true;
        }
        return false;
    }

    private static bool ProbeKungfuListCapability(object p)
    {
        // v0.5.2 — Spike PASS path: LoseAllSkill (clear) + GetSkill (add)
        return p.GetType().GetMethod("LoseAllSkill", F, null, Type.EmptyTypes, null) != null
            && p.GetType().GetMethod("GetSkill", F) != null;
    }
}
