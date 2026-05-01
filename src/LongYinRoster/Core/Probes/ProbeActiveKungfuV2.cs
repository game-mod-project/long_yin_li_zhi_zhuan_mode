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
        Logger.Info("PhaseC: in-memory active toggle PoC.");

        // ── 1. player.kungfuSkills 획득 (property → field fallback)
        var listObj = ReadFieldOrProperty(player, "kungfuSkills");
        if (listObj == null) { Logger.Warn("PhaseC: kungfuSkills not found on player"); return; }

        // ── 2. Il2CppSystem List reflection — Count + get_Item(int)
        var listType = listObj.GetType();
        var countProp = listType.GetProperty("Count", BF);
        if (countProp == null) { Logger.Warn("PhaseC: kungfuSkills.Count property not found"); return; }
        int count = Convert.ToInt32(countProp.GetValue(listObj));
        Logger.Info($"PhaseC: kungfuSkills count = {count}");

        var indexer = listType.GetMethod("get_Item", BF, null, new[] { typeof(int) }, null);
        if (indexer == null) { Logger.Warn("PhaseC: kungfuSkills get_Item not found"); return; }

        // ── 3. 1차 열거 — 식별
        object? firstEquipped          = null;
        int     firstEquippedKungfuId  = -1;
        int     firstEquippedIdx       = -1;

        object? firstUnequippedDiff    = null;
        int     firstUnequippedKungfuId = -1;
        int     firstUnequippedIdx     = -1;

        int eqCount = 0, uneqCount = 0;

        for (int i = 0; i < count; i++)
        {
            var entry = indexer.Invoke(listObj, new object[] { i });
            if (entry == null) continue;

            bool equiped  = ReadBool(entry, "equiped");
            int  kungfuID = ReadInt(entry, "kungfuID");

            if (equiped) eqCount++; else uneqCount++;

            // 처음 5개씩 샘플 로그
            if (eqCount <= 5 && equiped)
                Logger.Info($"PhaseC: [eq]   idx={i} kungfuID={kungfuID}");
            if (uneqCount <= 5 && !equiped)
                Logger.Info($"PhaseC: [uneq] idx={i} kungfuID={kungfuID}");

            // 첫 equiped wrapper 발견 시 — KungfuSkillLvData 의 모든 field/property dump (production 코드 대비 ID 필드 식별용)
            if (equiped && firstEquipped == null)
            {
                firstEquipped         = entry;
                firstEquippedKungfuId = kungfuID;
                firstEquippedIdx      = i;
                DumpWrapperShape(entry);
            }
            // equiped=false 첫 번째 — idx 매칭 (wrapper instance 가 firstEquipped 와 다르면 OK).
            // ID 매칭 안 하는 이유: KungfuSkillLvData 의 ID 필드 이름 미상 — kungfuID 가 -1 fallback.
            if (!equiped && firstUnequippedDiff == null)
            {
                firstUnequippedDiff     = entry;
                firstUnequippedKungfuId = kungfuID;
                firstUnequippedIdx      = i;
            }
        }

        Logger.Info($"PhaseC: 전체 — equiped={eqCount}, unequipped={uneqCount}");

        if (firstEquipped == null)
        {
            Logger.Warn("PhaseC: equiped=true 항목 없음 — 후보 부족. skip.");
            return;
        }
        if (firstUnequippedDiff == null)
        {
            Logger.Warn("PhaseC: equiped=false 항목 없음 — 후보 부족. skip.");
            return;
        }
        if (ReferenceEquals(firstEquipped, firstUnequippedDiff))
        {
            Logger.Warn("PhaseC: firstEquipped == firstUnequippedDiff — 동일 wrapper, swap 무의미. skip.");
            return;
        }

        Logger.Info($"PhaseC: 시도 — Unequip kungfuID={firstEquippedKungfuId} (idx {firstEquippedIdx}); Equip kungfuID={firstUnequippedKungfuId} (idx {firstUnequippedIdx})");

        // ── 4. EquipSkill / UnequipSkill 메서드 lookup (HeroData 인스턴스 메서드)
        var heroType      = player.GetType();
        var unequipMethod = heroType.GetMethod("UnequipSkill", BF);
        var equipMethod   = heroType.GetMethod("EquipSkill",   BF);

        if (unequipMethod == null)
        { Logger.Warn("PhaseC: UnequipSkill method not found on HeroData"); return; }
        if (equipMethod == null)
        { Logger.Warn("PhaseC: EquipSkill method not found on HeroData"); return; }

        // ── 5. 호출 — Phase B trace 에서 확인된 sig: (KungfuSkillLvData, bool), bool 은 항상 true
        try
        {
            unequipMethod.Invoke(player, new object[] { firstEquipped, true });
            Logger.Info("PhaseC: UnequipSkill OK");
        }
        catch (Exception ex)
        {
            Logger.Warn($"PhaseC: UnequipSkill threw: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        try
        {
            equipMethod.Invoke(player, new object[] { firstUnequippedDiff, true });
            Logger.Info("PhaseC: EquipSkill OK");
        }
        catch (Exception ex)
        {
            Logger.Warn($"PhaseC: EquipSkill threw: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        // ── 6. 검증 read-back — IL2CPP wrapper identity 유지되므로 동일 object ref 재읽기
        bool afterUneq = ReadBool(firstEquipped,     "equiped");
        bool afterEq   = ReadBool(firstUnequippedDiff, "equiped");
        Logger.Info($"PhaseC: read-back — old kungfuID={firstEquippedKungfuId} equiped={afterUneq} (expect false); new kungfuID={firstUnequippedKungfuId} equiped={afterEq} (expect true)");

        bool success = !afterUneq && afterEq;
        if (success)
            Logger.Info("PhaseC: SUCCESS — read-back 확인. 게임 UI active 무공 변경 관찰 필요.");
        else
            Logger.Warn("PhaseC: WARN — read-back 불일치. Equip/Unequip 호출은 성공했으나 equiped flag 미반영 (lazy update 가능성).");

        Logger.Info("PhaseC: 사용자 — 게임 UI 의 active 무공 변경 확인 + save → reload → active 유지 확인 (G3 게이트).");
    }

    // ───────────────────────────────────────────── reflection helpers (Phase C)

    /// <summary>
    /// Il2CPP 호환 BindingFlags: FlattenHierarchy 포함 (베이스 클래스 멤버 검색).
    /// </summary>
    private const BindingFlags BF =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    /// <summary>property → field fallback 으로 인스턴스 멤버 읽기.</summary>
    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name, BF);
        if (prop != null) { try { return prop.GetValue(obj); } catch { } }
        var fld = obj.GetType().GetField(name, BF);
        if (fld != null) { try { return fld.GetValue(obj); } catch { } }
        return null;
    }

    /// <summary>bool 멤버 읽기. 읽기 실패 시 false 반환.</summary>
    private static bool ReadBool(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj, name);
        return v is bool b && b;
    }

    /// <summary>int 멤버 읽기. uint → int 캐스트 지원. 실패 시 -1.</summary>
    private static int ReadInt(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj, name);
        if (v is int  i) return i;
        if (v is uint u) return (int)u;
        if (v == null)   return -1;
        try { return Convert.ToInt32(v); } catch { return -1; }
    }

    /// <summary>
    /// KungfuSkillLvData 의 fields / properties 전부 dump — kungfuID 가 fallback -1 인 원인 진단.
    /// 첫 equiped wrapper 1회만 호출됨.
    /// </summary>
    private static void DumpWrapperShape(object wrapper)
    {
        var t = wrapper.GetType();
        Logger.Info($"PhaseC: --- wrapper shape: {t.FullName} ---");
        foreach (var p in t.GetProperties(BF))
        {
            try
            {
                var v = p.CanRead ? p.GetValue(wrapper) : null;
                var disp = v == null ? "null" : (v is string s ? "\"" + s + "\"" : v.ToString());
                if (disp != null && disp.Length > 80) disp = disp.Substring(0, 80) + "…";
                Logger.Info($"  property: {p.PropertyType.Name} {p.Name} = {disp}");
            }
            catch (Exception ex) { Logger.Warn($"  property {p.Name} read threw: {ex.GetType().Name}: {ex.Message}"); }
        }
        foreach (var f in t.GetFields(BF))
        {
            try
            {
                var v = f.GetValue(wrapper);
                var disp = v == null ? "null" : (v is string s ? "\"" + s + "\"" : v.ToString());
                if (disp != null && disp.Length > 80) disp = disp.Substring(0, 80) + "…";
                Logger.Info($"  field:    {f.FieldType.Name} {f.Name} = {disp}");
            }
            catch (Exception ex) { Logger.Warn($"  field {f.Name} read threw: {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
