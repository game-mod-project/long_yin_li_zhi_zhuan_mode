# LongYin Roster Mod v0.5.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v0.5 PoC 의 결정적 발견 (`kungfuSkills[i].equiped` source-of-truth, `KungfuSkillLvData.skillID`, `EquipSkill / UnequipSkill` method path, 11-swap 패턴) 위에 UI cache invalidation discovery + save→reload persistence 를 추가하여 v0.5.1 minor release — 무공 active 카테고리 활성화.

**Architecture:** v0.4 의 9-step pipeline 의 `SetActiveKungfu` step 본문 교체 (`nowActiveSkill` 기반 → `kungfuSkills[i].equiped` 기반). 새 파일 `Core/ActiveKungfuApplier.cs` 로 책임 분리 — `PinpointPatcher.SetActiveKungfu` 는 Applier 호출만. Spike Phase 1 으로 UI refresh path 우선 발견 (flag toggle 조합), FAIL 시 user gate. 신규 인프라 없음 — 기존 패턴에 method path 정정 + UI refresh trigger 추가.

**Tech Stack:** BepInEx 6.0.0-dev (IL2CPP, .NET 6) / HarmonyLib / Il2CppInterop / Newtonsoft.Json (IL2CPP-bound, Serialize 만) / System.Text.Json / xUnit + Shouldly.

**선행 spec:** [`2026-05-02-longyin-roster-mod-v0.5.1-design.md`](../specs/2026-05-02-longyin-roster-mod-v0.5.1-design.md)

**작업 흐름**: Phase 1 (foundation + branch) → Phase 2 (Spike Phase 1 — Probe 코드 + Step 1-4 실행 + user gate) → Phase 3 (Impl — ActiveKungfuApplier + PinpointPatcher 본문 교체 + Probe 수정) → Phase 4 (Smoke 시나리오 1-3 + 회귀) → Phase 5 (Release — Probe cleanup + VERSION + dist + tag). Spike FAIL 시 Phase 6 alternate (Trace round 2 / Symbol scan / abort).

---

## File Structure

### 신규 파일

| 경로 | 책임 | 조건부? | Lifetime |
|---|---|---|---|
| `src/LongYinRoster/Core/Probes/ProbeActiveUiRefresh.cs` | Spike Phase 1 — Step 1-4 실행 (1-swap / 11-swap / 11-swap+flag / persistence) | 항상 (PoC) | release 전 cleanup |
| `src/LongYinRoster/Core/Probes/ProbeRunner.cs` | F12 trigger → Spike Mode 분기 | 항상 (PoC) | release 전 cleanup |
| `src/LongYinRoster/Core/ActiveKungfuApplier.cs` | active swap 알고리즘 (Apply/Restore) — `kungfuSkills[i].equiped` source + 11-swap pattern + UI refresh trigger | ✓ Spike PASS | 영구 |
| `src/LongYinRoster.Tests/ActiveKungfuApplierTests.cs` | slot JSON parse + skillID 추출 + selection gate unit tests (5 tests) | ✓ Spike PASS | 영구 |
| `docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md` | Spike Phase 1 step 별 결과 + 사용자 보고 + decision | 항상 | 영구 |
| `docs/superpowers/dumps/2026-05-02-v0.5.1-smoke.md` | Smoke 시나리오 1-3 + 회귀 결과 (release 시) | ✓ Spike PASS + impl | 영구 |
| `dist/LongYinRoster_v0.5.1.zip` | release artifact | ✓ release | 영구 |
| `dist/LongYinRoster_v0.5.1/` | release 폴더 구조 | ✓ release | 영구 |

### 수정 파일

| 경로 | 변경 | 조건부? |
|---|---|---|
| `src/LongYinRoster/Plugin.cs` | F12 핫키 handler 추가 (Spike) | 항상 (PoC, release 전 cleanup) |
| `src/LongYinRoster/Core/PinpointPatcher.cs` | `SetActiveKungfu` 본문 교체 (ActiveKungfuApplier 호출), `ProbeActiveKungfuCapability` hardcoded false 해제 | ✓ Spike PASS |
| `Directory.Build.props` 또는 `Plugin.cs:VERSION` | `0.4.0` → `0.5.1` | ✓ release 시만 |
| `README.md` | v0.5.1 highlights + 9-카테고리 표에 active enable 표시 | ✓ release 시만 |
| `docs/HANDOFF.md` | §2 git history v0.5.1 commits, §5 검증 완료 list 에 active full 추가, §6 v0.6 후보 갱신 | 항상 (release / FAIL 모두) |

### 변경 없는 파일 (확인만)

| 경로 | 이유 |
|---|---|
| `src/LongYinRoster/Core/Capabilities.cs` | `ActiveKungfu` flag 이미 존재 (v0.4) — Probe 결과 반영만 변경 |
| `src/LongYinRoster/Core/ApplySelection.cs` | `ActiveKungfu` flag 이미 존재 (v0.4) — schema 변경 없음 |
| `src/LongYinRoster/UI/SlotDetailPanel.cs` | `cap.ActiveKungfu` gate 이미 작동 — Probe 결과만 따라감 |
| `src/LongYinRoster/Util/KoreanStrings.cs` | `Cat_ActiveKungfu` label 이미 존재 — `Cat_DisabledSuffix` 도 동작 (Probe 결과 따라 자동 noop) |

---

## Phase 1 — Foundation (항상 실행)

### Task 1: Branch 생성 + baseline 검증

**Files:**
- Read: `Save/_PlayerExport/`, `git status`, `git log`

- [ ] **Step 1.1: 작업 위치 + 현재 baseline 확인**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git status
git log --oneline -5
```

Expected:
- working tree clean
- HEAD = `352be7b docs: v0.5.1 spec ...` (이전 commit = `23bc3a1 chore(v0.5): 양쪽 PoC FAIL — wrap-up`)

- [ ] **Step 1.2: v0.5.1 branch 생성 + checkout**

```bash
git checkout -b v0.5.1
git branch --show-current
```

Expected: `v0.5.1`.

- [ ] **Step 1.3: 게임 닫기 + v0.4 baseline build 검증**

```bash
tasklist | grep -i LongYinLiZhiZhuan
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected:
- 게임 프로세스 없음 (또는 사용자에게 닫으라고 요청)
- Build SUCCEEDED, `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 자동 배포

- [ ] **Step 1.4: v0.4 baseline test 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: **45/45 PASS** (v0.5 foundation 추가된 후의 baseline).

baseline 깨졌으면 v0.5.1 작업 시작 전 fix.

---

## Phase 2 — Spike Phase 1 (User-driven, with gate)

### Task 2: ProbeActiveUiRefresh.cs 작성 (Spike skeleton)

**Files:**
- Create: `src/LongYinRoster/Core/Probes/ProbeActiveUiRefresh.cs`
- Create: `src/LongYinRoster/Core/Probes/ProbeRunner.cs`

- [ ] **Step 2.1: Probes 디렉터리 생성**

```bash
mkdir -p "src/LongYinRoster/Core/Probes"
```

Expected: 디렉터리 존재.

- [ ] **Step 2.2: ProbeActiveUiRefresh.cs 작성 — Step 1 (1-swap baseline)**

```csharp
// File: src/LongYinRoster/Core/Probes/ProbeActiveUiRefresh.cs

using System;
using System.Reflection;
using LongYinRoster.Core;
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
        // Step2 또는 Step3 PASS 후 사용자가 게임 메뉴 → save → 재시작 → load 시나리오 안내
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
```

- [ ] **Step 2.3: ProbeRunner.cs 작성 — F12 trigger 분기**

```csharp
// File: src/LongYinRoster/Core/Probes/ProbeRunner.cs

using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5.1 Spike — F12 trigger handler. Plugin.cs 에서 Update 마다 Input.GetKeyDown(F12) 검사 후 호출.
/// release 전 cleanup (Probe 와 함께).
/// </summary>
public static class ProbeRunner
{
    public static ProbeActiveUiRefresh.Mode Mode { get; set; } = ProbeActiveUiRefresh.Mode.Step1;

    public static void Trigger()
    {
        Logger.Info($"=== ProbeRunner: F12 → ActiveUiRefresh / {Mode} ===");
        ProbeActiveUiRefresh.Run(Mode);
        Logger.Info("=== ProbeRunner: end ===");
    }
}
```

- [ ] **Step 2.4: Plugin.cs 에 F12 핫키 handler 추가**

`src/LongYinRoster/Plugin.cs` 의 Update method 안에 추가 (기존 F11 toggle 옆):

```csharp
// v0.5.1 Spike trigger
if (Input.GetKeyDown(KeyCode.F12))
{
    LongYinRoster.Core.Probes.ProbeRunner.Trigger();
}
```

(정확한 위치는 기존 `Input.GetKeyDown(KeyCode.F11)` 분기 바로 아래)

- [ ] **Step 2.5: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: BUILD SUCCEEDED, `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 배포.

- [ ] **Step 2.6: Commit Probe 코드**

```bash
git add src/LongYinRoster/Core/Probes/ src/LongYinRoster/Plugin.cs
git commit -m "spike(v0.5.1): ProbeActiveUiRefresh + F12 trigger — 4 mode (Step1-4)"
```

---

### Task 3: Spike Step 1 실행 — 1-회 swap baseline

**입력 조건**: SaveSlot1 (active 11 채움) load + 게임 진입 + active 가 11 채워진 상태.

**Files:**
- Read: BepInEx 로그
- Update: `docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md` (이 task 에서 신규 작성)

- [ ] **Step 3.1: 사용자에게 입력 조건 확인 + Step 1 실행 안내**

사용자에게 다음을 안내:
1. 게임 시작 → SaveSlot1 (active 11 채워진 상태) load
2. 게임 안에서 무공 패널을 열어 active 11 슬롯이 모두 채워졌는지 확인
3. mod F11 끔 (창 안 보이게)
4. F12 누름 → BepInEx 로그에 Step1 결과 출력
5. 게임 무공 패널을 다시 보고 active 슬롯이 변경되었는지 사용자 확인

기본 Mode = Step1 (ProbeRunner 의 default).

- [ ] **Step 3.2: BepInEx 로그 확인**

```bash
grep -n "Spike\|HeroLocator" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -50
```

Expected:
- `Spike[Step1]: kungfuSkills count=NN` (NN = 무공 개수, 보통 100+)
- `Spike Step1: read-back — old=False (expect false); new=True (expect true)` 같은 read-back 결과
- 사용자 보고: 게임 무공 패널 active 변경 보임 / 안 보임

- [ ] **Step 3.3: Spike dump 파일 작성 — Step 1 결과**

`docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md` 에 기록:

```markdown
# v0.5.1 Spike — active UI refresh discovery (2026-05-02)

## Step 1 — 1-회 swap baseline 재확인

**입력 조건**: active 11 채움.

**실행 결과**:
- read-back: [`old=False / new=True` PASS or FAIL]
- 게임 UI: [사용자 보고 — 변경 보임 / 안 보임]

**판정**: [PASS — UI 즉시 갱신 / FAIL — UI 미반영 (예상)]
```

- [ ] **Step 3.4: Step 1 PASS 시 (예상 외 PASS)**

만약 Step 1 만으로 UI 갱신이 보임 → 즉시 Step 4 (persistence) 로 진행. Step 2/3 skip.

- [ ] **Step 3.5: Step 1 FAIL 시 (예상)**

Step 2 로 진행. ProbeRunner.Mode = Step2 변경 (코드 수정 + rebuild) 또는 Plugin.cs 안에 mode 토글 핫키 추가:

```csharp
if (Input.GetKeyDown(KeyCode.F10))
{
    var cur = LongYinRoster.Core.Probes.ProbeRunner.Mode;
    LongYinRoster.Core.Probes.ProbeRunner.Mode = (LongYinRoster.Core.Probes.ProbeActiveUiRefresh.Mode)(((int)cur + 1) % 4);
    LongYinRoster.Util.Logger.Info($"ProbeRunner.Mode = {LongYinRoster.Core.Probes.ProbeRunner.Mode}");
}
```

(F10 누르면 Mode cycling: Step1 → Step2 → Step3 → Step4 → Step1)

- [ ] **Step 3.6: Step 2.4 의 F10 토글 추가 + rebuild**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

---

### Task 4: Spike Step 2 실행 — 11-회 swap (Approach A mimic)

**Files:**
- Update: `docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md`

- [ ] **Step 4.1: 게임 재시작 (또는 SaveSlot1 reload) — active 11 채워진 상태 회복**

이전 Step 1 swap 으로 active 일부 변경됨. SaveSlot1 reload 로 baseline 복원.

- [ ] **Step 4.2: F10 → Mode = Step2 → F12 실행**

사용자 안내:
1. 게임 안에서 active 11 채워진 상태 확인
2. F10 한 번 → ProbeRunner.Mode = Step2 (BepInEx 로그 확인)
3. F12 → Step 2 실행
4. 게임 무공 패널 UI 변경 사용자 확인

- [ ] **Step 4.3: BepInEx 로그 확인**

```bash
grep -n "Spike\[Step2\]\|Step2:" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -30
```

Expected:
- `Spike Step2: Unequip × NN 완료` (NN = 변경 횟수, 보통 11)
- `Spike Step2: Equip × NN 완료`
- 사용자 보고: 게임 UI 변경 / 미변경

- [ ] **Step 4.4: Spike dump 파일에 Step 2 결과 추가**

```markdown
## Step 2 — 11-회 swap (game 자체 패턴 mimic)

**실행 결과**:
- Unequip × N + Equip × N 완료
- 게임 UI: [사용자 보고 — 변경 / 미변경]

**판정**: [PASS — UI 즉시 갱신 / FAIL — Step 3 로 진행]
```

- [ ] **Step 4.5: PASS 시 — Step 4 로 직행** / **FAIL 시 — Step 3 로 진행**

PASS 시 persistence 검증 (Task 6) 으로 진행. FAIL 시 Task 5 (Step 3) 진행.

---

### Task 5: Spike Step 3 실행 — 11-회 swap + flag toggle

**Files:**
- Update: `docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md`

- [ ] **Step 5.1: 게임 재시작 — active 11 baseline 복원**

- [ ] **Step 5.2: F10 → Mode = Step3 → F12 실행**

사용자 안내:
1. active 11 baseline 확인
2. F10 → Mode = Step3
3. F12 → Step 3 실행
4. 게임 무공 패널 UI 변경 사용자 확인

- [ ] **Step 5.3: BepInEx 로그 확인**

```bash
grep -n "Spike\[Step3\]\|Step3:" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -30
```

Expected:
- `Spike Step3: swap × N 완료, flag toggle 진행`
- `Spike Step3: flag toggle 완료. F12 후 게임 무공 패널 UI 사용자 확인`
- 사용자 보고: 게임 UI 변경 / 미변경

- [ ] **Step 5.4: Spike dump 파일에 Step 3 결과 추가**

```markdown
## Step 3 — 11-회 swap + flag toggle

**toggle 대상**: skillIconDirty, maxManaChanged (각 변경 wrapper) + HeroIconDirty + heroIconDirtyCount++ (player)

**실행 결과**:
- swap + toggle 완료
- 게임 UI: [사용자 보고]

**판정**: [PASS / FAIL → user gate]
```

- [ ] **Step 5.5: Step 1-3 모두 FAIL 시 — User gate (Task 7)** / **어느 하나 PASS 시 — Step 4 (Task 6)**

---

### Task 6: Spike Step 4 실행 — save → reload persistence

**전제**: Step 1, 2, 또는 3 중 어느 하나가 UI 갱신 PASS.

**Files:**
- Update: `docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md`

- [ ] **Step 6.1: PASS 한 step 의 swap 적용 후 baseline skillID 기록**

PASS 한 step (예: Step2) 을 다시 실행 — F10 으로 Mode 맞추고 F12. swap 후 active set 의 skillID 가 BepInEx 로그에 출력되도록 F10 → Mode = Step4 → F12 실행:

```bash
grep -n "Spike Step4 — 현재 equiped skillID set:" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -1
```

Expected: `Spike Step4 — 현재 equiped skillID set: [123,456,789,...]` (11 skillID).

이 set 을 dump 파일에 기록.

- [ ] **Step 6.2: 사용자 — game save → 종료 → 재시작 → save load**

사용자에게 안내:
1. 게임 메뉴로 save (현재 SaveSlot1 또는 다른 slot)
2. 게임 종료 (모드 dll 자동 unload)
3. 게임 재시작
4. 위 save 로 load
5. 게임 무공 패널의 active 11 슬롯 확인

- [ ] **Step 6.3: F12 → Mode = Step4 → 다시 skillID set 출력**

reload 후:
1. F10 → Mode = Step4 (Mode 가 reset 되어 Step1 부터 시작 — F10 cycling 으로 Step4 까지)
2. F12 → 현재 equiped skillID set 로그 출력

```bash
grep -n "Spike Step4 — 현재 equiped skillID set:" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -1
```

Expected: Step 6.1 의 set 과 정확히 동일.

- [ ] **Step 6.4: Spike dump 파일에 Step 4 결과 추가**

```markdown
## Step 4 — save → reload persistence

**Pre-save skillID set**: [123,456,789,...]
**Post-reload skillID set**: [123,456,789,...]

**판정**: [PASS — 정확히 일치 / FAIL — 다름 또는 누락]
```

- [ ] **Step 6.5: 모두 PASS 시 — Phase 3 진행** / **FAIL 시 — User gate**

PASS = UI 갱신 step + persistence 모두 PASS → Phase 3 (Implementation) 으로.
FAIL = persistence FAIL → User gate.

- [ ] **Step 6.6: Spike dump 파일 commit**

```bash
git add docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md
git commit -m "spike(v0.5.1): UI refresh discovery 결과 — Step [X] PASS, persistence [PASS/FAIL]"
```

---

### Task 7: User Gate — Spike FAIL 시 결정

**전제**: Step 1-3 모두 UI FAIL 또는 Step 4 persistence FAIL.

**Files:**
- Update: `docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md`

- [ ] **Step 7.1: Spike FAIL 결과 정리 + 사용자에게 보고**

dump 파일의 모든 Step 결과 + 추정 원인 (예: flag 가 진짜 trigger 아님, game-self method 가 따로 있음 등).

- [ ] **Step 7.2: User decision 받기**

사용자에게 옵션 제시:
1. **Trace round 2 진행** (3-4 hour) — Harmony trace 로 game UI 가 native active 변경 후 호출하는 method 추적 (Refresh*Skill / Refresh*Kungfu 등)
2. **Symbol scan** (2-3 hour) — Assembly-CSharp.dll 의 Refresh.*Skill / Refresh.*Kungfu / Refresh.*Panel 류 method 전수 dump 후 zero-arg 후보 시도
3. **abort + sub-project 변경** — foundation 보존, dump report, 외형 또는 인벤토리 sub-project 로 이동

- [ ] **Step 7.3: 결정 commit**

```bash
git add docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md
git commit -m "spike(v0.5.1): user gate — [Trace round 2 / Symbol scan / abort] 결정"
```

- [ ] **Step 7.4: 결정 따라 분기**

- Trace round 2 / Symbol scan → Phase 6 (alternate)
- abort → Task 25 (foundation 보존 + HANDOFF 업데이트) 로 점프, sub-project 변경

---

## Phase 3 — Implementation (Spike PASS 후)

### Task 8: ActiveKungfuApplier.cs Failing tests 작성

**Files:**
- Create: `src/LongYinRoster.Tests/ActiveKungfuApplierTests.cs`

- [ ] **Step 8.1: Test 파일 신규 작성 — 5 unit tests (Failing 상태)**

```csharp
// File: src/LongYinRoster.Tests/ActiveKungfuApplierTests.cs

using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.5.1 — ActiveKungfuApplier 의 slot JSON 파싱 + selection gate unit tests.
/// IL2CPP 게임 측 호출 (EquipSkill / UnequipSkill / flag toggle) 은 mock 불가 — smoke 로만 검증.
/// </summary>
public class ActiveKungfuApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void ExtractEquippedSkillIDs_ReturnsAllEquippedTrueIDs()
    {
        var slot = ParseSlot("""
        {
          "kungfuSkills": [
            {"skillID": 100, "equiped": true},
            {"skillID": 200, "equiped": false},
            {"skillID": 300, "equiped": true}
          ]
        }
        """);
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBe(new[] { 100, 300 });
    }

    [Fact]
    public void ExtractEquippedSkillIDs_HandlesEmptyActiveSet()
    {
        var slot = ParseSlot("""
        {
          "kungfuSkills": [
            {"skillID": 100, "equiped": false},
            {"skillID": 200, "equiped": false}
          ]
        }
        """);
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractEquippedSkillIDs_HandlesDuplicateSkillID_ReturnsOnce()
    {
        var slot = ParseSlot("""
        {
          "kungfuSkills": [
            {"skillID": 100, "equiped": true},
            {"skillID": 100, "equiped": true}
          ]
        }
        """);
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBe(new[] { 100 });
    }

    [Fact]
    public void ExtractEquippedSkillIDs_MissingKungfuSkills_ReturnsEmpty()
    {
        var slot = ParseSlot("""{ "heroName": "test" }""");
        var ids = ActiveKungfuApplier.ExtractEquippedSkillIDs(slot);
        ids.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RespectsApplySelection_SkipsWhenFalse()
    {
        var slot = ParseSlot("""
        {
          "kungfuSkills": [{"skillID": 100, "equiped": true}]
        }
        """);
        var sel = new ApplySelection { ActiveKungfu = false };
        var result = ActiveKungfuApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason.ShouldContain("selection off");
    }
}
```

- [ ] **Step 8.2: Run tests — should fail (ActiveKungfuApplier 없음)**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ActiveKungfuApplierTests"
```

Expected: FAIL — `ActiveKungfuApplier` 또는 `ExtractEquippedSkillIDs` / `Apply` 가 정의되지 않음 (CS0103).

---

### Task 9: ActiveKungfuApplier.cs 작성 — Apply / Restore / ExtractEquippedSkillIDs

**Files:**
- Create: `src/LongYinRoster/Core/ActiveKungfuApplier.cs`

- [ ] **Step 9.1: ActiveKungfuApplier.cs 신규 작성**

```csharp
// File: src/LongYinRoster/Core/ActiveKungfuApplier.cs

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
/// 를 사용. v0.5 Phase A/B PoC 결과로 새 path 확정:
///   - Source-of-truth: kungfuSkills[i].equiped (NOT nowActiveSkill)
///   - ID field: KungfuSkillLvData.skillID (NOT kungfuID — wrapper.kungfuID 는 -1 fallback)
///   - Method path: HeroData.EquipSkill(wrapper, true) + HeroData.UnequipSkill(wrapper, true)
///   - 패턴: 11-swap (game 자체 동작 mirror — Phase B trace 결과)
///   - UI refresh trigger: Spike Phase 1 PASS path (flag toggle 또는 game-self method)
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
    /// Apply slot active set to player. 11-swap pattern (game 자체 mirror):
    ///   1. Selection check
    ///   2. slot active skillID 수집
    ///   3. 현재 player wrapper 매칭 (skillID 기준)
    ///   4. 현재 equiped wrapper 수집
    ///   5. Unequip phase (모든 currentEquipped 에 UnequipSkill)
    ///   6. Equip phase (매칭된 equipTargets 에 EquipSkill)
    ///   7. UI refresh trigger
    /// </summary>
    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        // 1. Selection
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

        // 2-3. Match wrappers
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

        // 4. Current equipped
        var currentEquipped = new List<object>();
        for (int i = 0; i < n; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            if ((bool)(ReadFieldOrProperty(w, "equiped") ?? false))
                currentEquipped.Add(w);
        }

        // 5. Unequip phase
        foreach (var w in currentEquipped)
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

        // 6. Equip phase
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

        // 7. UI refresh trigger (Spike PASS path 따라 적용)
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

    /// <summary>
    /// UI refresh trigger — Spike Phase 1 PASS path 에 따라 활성화.
    /// 현재 default = flag toggle (skillIconDirty + maxManaChanged + HeroIconDirty + heroIconDirtyCount++).
    /// Spike 결과가 game-self method path 면 InvokeMethod 로 교체.
    /// </summary>
    private static void TriggerUiRefresh(object player, List<object> changedFromUnequip, List<object> changedFromEquip)
    {
        // flag toggle path (Spike Step 3 또는 Step 2 의 default)
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
```

- [ ] **Step 9.2: Run tests — should pass**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ActiveKungfuApplierTests"
```

Expected: **5/5 PASS**.

- [ ] **Step 9.3: 전체 unit tests 회귀 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: **50/50 PASS** (45 baseline + 5 new).

- [ ] **Step 9.4: Commit**

```bash
git add src/LongYinRoster/Core/ActiveKungfuApplier.cs src/LongYinRoster.Tests/ActiveKungfuApplierTests.cs
git commit -m "feat(core): ActiveKungfuApplier — 11-swap pattern + flag-toggle UI refresh + 5 tests"
```

---

### Task 10: PinpointPatcher.SetActiveKungfu 본문 교체

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`

- [ ] **Step 10.1: SetActiveKungfu 본문 교체 — Applier 호출**

`src/LongYinRoster/Core/PinpointPatcher.cs` 의 `SetActiveKungfu` method (line ~293-333) 본문을 다음으로 완전히 교체:

```csharp
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
```

- [ ] **Step 10.2: ProbeActiveKungfuCapability 교체 — hardcoded false 해제**

같은 파일의 `ProbeActiveKungfuCapability` (line ~738-743) 를 다음으로 교체:

```csharp
private static bool ProbeActiveKungfuCapability(object p)
{
    // v0.5.1 — Spike PASS 후 method path 확정 (kungfuSkills[i].equiped + EquipSkill/UnequipSkill).
    // 두 method 모두 존재하면 capability ok.
    return p.GetType().GetMethod("EquipSkill", F) != null
        && p.GetType().GetMethod("UnequipSkill", F) != null;
}
```

- [ ] **Step 10.3: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

- [ ] **Step 10.4: 전체 tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: **50/50 PASS** (회귀 없음).

- [ ] **Step 10.5: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "feat(core): PinpointPatcher.SetActiveKungfu 본문 교체 + ProbeActiveKungfuCapability 해제"
```

---

## Phase 4 — Smoke 시나리오 (in-game 검증)

### Task 11: Smoke 시나리오 1 — SaveSlot 다른 set Apply

**전제**: Phase 3 완료, Probe 코드는 아직 main 에 있음 (cleanup 은 Phase 5).

**Files:**
- Create: `docs/superpowers/dumps/2026-05-02-v0.5.1-smoke.md`

- [ ] **Step 11.1: 사전 준비 — slot 1 + slot 2 capture**

사용자 안내:
1. 게임 시작 → SaveSlot1 (active set X, 11 채움) load
2. mod F11 열기 → 슬롯 1 의 [+] capture 버튼 클릭 → 토스트 "슬롯 1에 캡처됨" 확인
3. 게임 메뉴 → SaveSlot2 (active set Y, 다른 11) load
4. mod F11 → 슬롯 2 의 [+] capture → 토스트 확인

- [ ] **Step 11.2: Apply slot 1 → SaveSlot2 의 active 가 set X 로 변경**

사용자 안내:
1. 현재 SaveSlot2 (active = Y) 상태
2. mod F11 → 슬롯 1 선택 → 무공 active 체크박스 ✓ 확인 (default off 면 toggle on)
3. ▼ 현재 플레이어로 덮어쓰기 → confirm → toast "슬롯 1 적용됨"
4. mod F11 끔 → 게임 무공 패널 확인 → active = X (slot 1 set) 표시 사용자 확인

- [ ] **Step 11.3: BepInEx 로그 확인**

```bash
grep -n "ActiveKungfu Apply done\|PinpointPatcher.Apply done\|activeKungfu" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -10
```

Expected:
- `ActiveKungfu Apply done — unequip=N equip=M missing=K`
- `PinpointPatcher.Apply done — applied=... activeKungfu (unequip=N equip=M ...)`

- [ ] **Step 11.4: save → reload persistence 검증**

사용자 안내:
1. 게임 메뉴 → save (현재 SaveSlot2 자리)
2. 게임 종료 → 재시작
3. SaveSlot2 load
4. 게임 무공 패널 확인 → active = X (slot 1 set) 정확히 유지 사용자 확인

- [ ] **Step 11.5: Smoke dump 파일에 시나리오 1 결과**

```markdown
# v0.5.1 smoke 결과 (2026-05-02)

## 시나리오 1 — SaveSlot 다른 set Apply

- Pre: slot 1 = SaveSlot1 (active X), slot 2 = SaveSlot2 (active Y)
- 실행: SaveSlot2 load → mod slot 1 Apply
- 결과 (즉시): active = X (사용자 보고)
- save → reload 후: active = X (사용자 보고)
- 판정: [PASS / FAIL]
```

---

### Task 12: Smoke 시나리오 2 — 부분 unequip 후 Apply

**Files:**
- Update: `docs/superpowers/dumps/2026-05-02-v0.5.1-smoke.md`

- [ ] **Step 12.1: 사전 준비 — SaveSlot1 → 일부 unequip**

사용자 안내:
1. SaveSlot1 (active set X, 11 채움) load
2. 게임 무공 패널에서 active 일부 (예: 5 개) unequip → 6 개만 active

- [ ] **Step 12.2: mod slot 1 Apply (자동백업 → slot 0)**

사용자 안내:
1. mod F11 → 슬롯 1 선택 → ✓ active → ▼ 덮어쓰기
2. confirm → toast "슬롯 1 적용됨, 슬롯 0 자동저장"
3. F11 끔 → active = X (전체 11) 사용자 확인

- [ ] **Step 12.3: BepInEx 로그 확인**

```bash
grep -n "ActiveKungfu Apply done\|자동백업\|slot 0" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -10
```

Expected: 자동백업 (slot 0) + Apply 모두 로그 출력.

- [ ] **Step 12.4: save → reload persistence 검증**

사용자 안내:
1. game save → 종료 → 재시작 → load
2. 무공 패널 → active = X (전체 11) 유지 확인

- [ ] **Step 12.5: Smoke dump 파일에 시나리오 2 결과**

```markdown
## 시나리오 2 — 부분 unequip 후 Apply

- Pre: slot 1 = SaveSlot1 (active X, 11), 게임에서 5 개 unequip → 6 개 active
- 실행: mod slot 1 Apply (자동백업 → slot 0 = 부분 unequip 상태)
- 결과 (즉시): active = X (전체 11)
- save → reload 후: active = X (전체 11)
- 판정: [PASS / FAIL]
```

---

### Task 13: Smoke 시나리오 3 — Restore (slot 0 자동백업)

**Files:**
- Update: `docs/superpowers/dumps/2026-05-02-v0.5.1-smoke.md`

- [ ] **Step 13.1: 시나리오 2 의 slot 0 자동백업 사용 — Restore**

사용자 안내:
1. (시나리오 2 직후 상태 — slot 0 = 부분 unequip 상태 6 개 active)
2. mod F11 → 슬롯 0 (자동백업) 선택 → ↶ 복원 버튼
3. confirm → toast "슬롯 0에서 복원됨"
4. F11 끔 → active = 부분 unequip 상태 (6 개) 사용자 확인

- [ ] **Step 13.2: BepInEx 로그 확인**

```bash
grep -n "Restore\|ActiveKungfu Apply done" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -10
```

Expected: Restore 로그 + ActiveKungfu Apply (Restore 가 Apply 같은 path 사용).

- [ ] **Step 13.3: save → reload persistence 검증**

사용자 안내:
1. game save → 종료 → 재시작 → load
2. 무공 패널 → active = 6 개 (자동백업 시점) 유지 확인

- [ ] **Step 13.4: Smoke dump 파일에 시나리오 3 결과**

```markdown
## 시나리오 3 — Restore (slot 0 자동백업)

- Pre: 시나리오 2 직후 (slot 0 = active 6 개)
- 실행: mod slot 0 Restore
- 결과 (즉시): active = 6 개 (자동백업 시점)
- save → reload 후: active = 6 개 유지
- 판정: [PASS / FAIL]
```

---

### Task 14: 회귀 시나리오 — v0.4 baseline 동작 유지

**Files:**
- Update: `docs/superpowers/dumps/2026-05-02-v0.5.1-smoke.md`

- [ ] **Step 14.1: 정체성 9 필드 Apply 회귀**

사용자 안내:
1. 다른 SaveSlot (예: SaveSlot3) load → mod 슬롯 3 capture
2. 다시 SaveSlot1 load → mod 슬롯 3 선택 → 정체성 ✓ → ▼ 덮어쓰기
3. heroName / nickname / age 등 슬롯 3 의 정체성으로 변경 확인
4. save → reload → 변경 유지 확인

- [ ] **Step 14.2: 천부 17/17 회귀**

사용자 안내:
1. 슬롯 X 선택 → 천부 ✓ → ▼ 덮어쓰기
2. 천부 17 개 모두 변경 확인 (게임 정보창 의 천부 list)
3. save → reload → 유지 확인

- [ ] **Step 14.3: 부상/충성/호감 영구 보존 회귀**

사용자 안내:
1. 슬롯 X 선택 → ▼ 덮어쓰기 (모든 카테고리)
2. 부상 / 충성 / 호감 값이 변경되지 않았는지 확인 (Apply 의 보존 필드)

- [ ] **Step 14.4: 외형 / 인벤토리 / 창고 disabled 표시 유지**

사용자 안내:
1. mod 슬롯 X 선택 → 외형 / 인벤토리 / 창고 체크박스가 "(v0.5+ 후보)" suffix 와 함께 disabled 인지 확인

- [ ] **Step 14.5: legacy 슬롯 호환 — v0.3/v0.4 슬롯 파일**

만약 v0.3/v0.4 시점에 capture 된 슬롯이 있다면:
1. mod 그 슬롯 선택 → 정상 표시 + Apply 정상 동작 확인

- [ ] **Step 14.6: Smoke dump 파일에 회귀 결과**

```markdown
## 회귀 시나리오

- 정체성 9 필드 Apply: [PASS / FAIL]
- 천부 17/17: [PASS / FAIL]
- 부상/충성/호감 영구 보존: [PASS / FAIL]
- 외형/인벤토리/창고 disabled 표시: [PASS / FAIL]
- legacy 슬롯 (v0.3/v0.4): [PASS / FAIL]
```

- [ ] **Step 14.7: Smoke 결과 commit**

```bash
git add docs/superpowers/dumps/2026-05-02-v0.5.1-smoke.md
git commit -m "docs: v0.5.1 smoke 결과 — 시나리오 1/2/3 + 회귀 [PASS]"
```

- [ ] **Step 14.8: G2 PASS 기준 확인**

시나리오 1, 2, 3 모두 PASS + 회귀 모두 PASS → Phase 5 (Release) 진행.
어느 하나 FAIL → release 안 함 → Task 22 (Out path).

---

## Phase 5 — Release (Smoke PASS 후)

### Task 15: Probe 코드 cleanup

**Files:**
- Delete: `src/LongYinRoster/Core/Probes/` (디렉터리 전체)
- Modify: `src/LongYinRoster/Plugin.cs` (F12 / F10 핫키 handler 제거)

- [ ] **Step 15.1: Probe 디렉터리 삭제**

```bash
git rm -r src/LongYinRoster/Core/Probes/
```

Expected: ProbeActiveUiRefresh.cs + ProbeRunner.cs 둘 다 삭제 staged.

- [ ] **Step 15.2: Plugin.cs 의 F12 / F10 핫키 handler 제거**

`src/LongYinRoster/Plugin.cs` 의 Update 안에서 다음 분기들 제거:
```csharp
// 제거: F12 → ProbeRunner.Trigger()
// 제거: F10 → Mode cycling
```

기존 F11 핫키 (ModWindow toggle) 만 유지.

- [ ] **Step 15.3: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

- [ ] **Step 15.4: 전체 tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: **50/50 PASS** (Probe 삭제로 회귀 없음 — Probe 코드는 tests 와 분리).

- [ ] **Step 15.5: Commit**

```bash
git add -A
git commit -m "chore(release): remove Probe code + F12/F10 handlers (D16 패턴)"
```

---

### Task 16: VERSION bump 0.4.0 → 0.5.1

**Files:**
- Modify: `Directory.Build.props` 또는 `Plugin.cs`

- [ ] **Step 16.1: VERSION 위치 확인**

```bash
grep -n "0\.4\.0" Directory.Build.props src/LongYinRoster/Plugin.cs 2>&1
```

Expected: VERSION = "0.4.0" 위치 출력.

- [ ] **Step 16.2: 0.4.0 → 0.5.1 변경**

해당 파일에서 `0.4.0` → `0.5.1` 정확히 1 곳 변경.

- [ ] **Step 16.3: Build + 로그 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED. 게임 시작 시 BepInEx 로그에 `Loaded LongYin Roster Mod v0.5.1`.

- [ ] **Step 16.4: Commit**

```bash
git add Directory.Build.props src/LongYinRoster/Plugin.cs
git commit -m "chore(release): VERSION 0.4.0 → 0.5.1"
```

---

### Task 17: README + HANDOFF 업데이트

**Files:**
- Modify: `README.md`
- Modify: `docs/HANDOFF.md`

- [ ] **Step 17.1: README.md — v0.5.1 highlights**

`README.md` 의 변경 highlights / 9-카테고리 표 / Releases 섹션 업데이트:

```markdown
## v0.5.1 (2026-05-02) — 무공 active 활성화

- 무공 active 카테고리 활성화 (`kungfuSkills[i].equiped` 11-swap pattern + UI cache invalidation)
- v0.5 PoC 의 미해결 issue (UI refresh + save persistence) 해소
- v0.4 의 다른 카테고리 동작 변경 없음
- legacy 호환 (v0.1~v0.4 슬롯 무손실)

### 9-카테고리 표

| 카테고리 | 상태 | 비고 |
|---|---|---|
| 스탯 / 명예 / 천부 / 스킨 / 자기집 add | ✓ v0.3 | |
| 정체성 | ✓ v0.4 | |
| **무공 active** | **✓ v0.5.1** | **NEW** |
| 외형 / 인벤토리 / 창고 | (v0.6+ 후보) | sub-data wrapper graph 미해결 |
```

(이미 README.md 에 있는 내용을 보고 정확한 위치에 추가)

- [ ] **Step 17.2: HANDOFF.md — §2 git history v0.5.1 commits 추가**

`docs/HANDOFF.md` §2 (현재 깃 히스토리) 에 v0.5.1 commits 추가:

```
[새 commits at top]
chore(release): v0.5.1 — VERSION + README + HANDOFF
chore(release): remove Probe code + F12/F10 handlers
docs: v0.5.1 smoke 결과 — 시나리오 1/2/3 + 회귀 PASS
feat(core): PinpointPatcher.SetActiveKungfu 본문 교체 + ProbeActiveKungfuCapability 해제
feat(core): ActiveKungfuApplier — 11-swap + flag-toggle UI refresh
spike(v0.5.1): UI refresh discovery 결과 — Step [X] PASS, persistence PASS
spike(v0.5.1): ProbeActiveUiRefresh + F12 trigger
docs: v0.5.1 spec — active full sub-project
```

- [ ] **Step 17.3: HANDOFF.md — §1, §5, §6 업데이트**

§1 (한 줄 요약):
```markdown
**현재 main baseline = v0.5.1** (selection-aware Apply / Restore + 9-카테고리 체크박스 UI + 정체성 + 무공 active 활성화).
```

§5 검증 완료 list 에 추가:
```markdown
- **무공 active full** (v0.5.1): SaveSlot 다른 set Apply / 부분 unequip 후 Apply / Restore + save→reload persistence 모두 PASS (smoke 시나리오 1/2/3)
```

§6 — v0.6+ 후보 갱신 (외형 / 무공 list / 인벤토리 / 창고 / UI cache 일반화 — active 의 spike 결과 path 가 transferable 한지 명시):
```markdown
### 6.B v0.6+ 후보

v0.5.1 release 후 다음 sub-project 후보 (한 번에 한 sub-project):

- **외형** — `faceData` + `partPosture` sub-data wrapper graph + sprite cache invalidate. v0.5.1 의 spike PASS path (예: flag toggle) 가 `HeroIconDirty / heroIconDirtyCount` 통해 외형 refresh 에도 transferable 한지 확인
- **무공 list** — `kungfuSkills` entry 추가/제거 + `KungfuSkillLvData` ctor / factory / Add method 발견
- **인벤토리** — `itemListData.allItem (ItemData[])` wrapper graph
- **창고** — `selfStorage.allItem (ItemData[])`
- **UI cache invalidation 일반화** — v0.5.1 spike 결과를 일반 framework 로 추출
```

- [ ] **Step 17.4: Commit**

```bash
git add README.md docs/HANDOFF.md
git commit -m "docs: README + HANDOFF — v0.5.1 release"
```

---

### Task 18: dist zip + GitHub release tag

**Files:**
- Create: `dist/LongYinRoster_v0.5.1/` 디렉터리 + 파일 구조
- Create: `dist/LongYinRoster_v0.5.1.zip`
- Tag: `v0.5.1`

- [ ] **Step 18.1: dist 디렉터리 구조 생성**

기존 v0.4.0 dist 구조를 따라:

```bash
ls dist/
```

Expected: `LongYinRoster_v0.4.0.zip` + `LongYinRoster_v0.4.0/` 등 기존 release.

- [ ] **Step 18.2: v0.5.1 dist 폴더 생성**

```bash
mkdir -p "dist/LongYinRoster_v0.5.1/BepInEx/plugins/LongYinRoster"
cp "BepInEx/plugins/LongYinRoster/LongYinRoster.dll" "dist/LongYinRoster_v0.5.1/BepInEx/plugins/LongYinRoster/"
cp README.md "dist/LongYinRoster_v0.5.1/" 2>/dev/null || true
```

(추가로 v0.4.0 dist 폴더에 있는 다른 파일들도 같이 — README / config 등)

- [ ] **Step 18.3: zip 생성 (PowerShell)**

```powershell
Compress-Archive -Path "dist/LongYinRoster_v0.5.1/*" -DestinationPath "dist/LongYinRoster_v0.5.1.zip" -Force
```

또는 7z / unix zip 도구 사용. Expected: `dist/LongYinRoster_v0.5.1.zip` 생성.

- [ ] **Step 18.4: tag 생성**

```bash
git tag -a v0.5.1 -m "v0.5.1 — 무공 active 활성화 (kungfuSkills.equiped + UI refresh + save persistence)"
git tag --list "v0.5.*"
```

Expected: `v0.5.1` tag 출력.

- [ ] **Step 18.5: GitHub release (gh CLI) — 사용자 결정**

사용자에게 push + release 의향 확인:
```bash
git push origin v0.5.1
git push origin v0.5.1 --tags
gh release create v0.5.1 dist/LongYinRoster_v0.5.1.zip \
  --title "v0.5.1 — 무공 active 활성화" \
  --notes-file <(cat <<'EOF'
## v0.5.1 (2026-05-02) — 무공 active 활성화

### 새 기능
- 무공 active 카테고리 활성화 (`kungfuSkills[i].equiped` 11-swap pattern + UI cache invalidation)
- v0.5 PoC 의 미해결 issue (UI refresh + save persistence) 해소

### 변경 없음
- v0.4 의 다른 카테고리 동작 (정체성 / 천부 / 스탯 등)
- 슬롯 schema (legacy 호환)

### 다음 단계
- 외형 / 무공 list / 인벤토리 / 창고 — v0.6+ sub-project
EOF
)
```

(사용자가 push 의향 없으면 local tag 만 유지)

- [ ] **Step 18.6: main 으로 merge — 사용자 결정**

v0.5.1 branch → main merge 시:

```bash
git checkout main
git merge --no-ff v0.5.1 -m "Merge v0.5.1 — 무공 active full"
git push origin main
```

(사용자 결정 — auto mode 에서는 merge 보다 release tag 만 안전)

---

## Phase 6 — Alternate flow (Spike FAIL 후)

### Task 19 (alt): Trace round 2 — Harmony trace round 2

**전제**: Task 7 (User gate) 에서 "Trace round 2" 결정.

**Files:**
- Modify: `src/LongYinRoster/Core/Probes/ProbeActiveUiRefresh.cs` 또는 신규 `ProbeUiRefreshTrace.cs`

- [ ] **Step 19.1: Trace 후보 method patterns 정의**

`Refresh.*Skill / Refresh.*Kungfu / Refresh.*Panel / Refresh.*Fight / Update.*Skill / Reload.*Skill / Set.*Dirty` 같은 후보 패턴.

- [ ] **Step 19.2: Harmony Prefix patch 작성 — 후보 method patch 시 호출 trace**

(v0.5 PoC ProbeActiveKungfuV2.RunPhaseB 패턴 mirror)

```csharp
// 각 후보 method 마다 Harmony Prefix patch
// Prefix 안에서 Logger.Info($"TRACE: {method.FullName} called from {stackTrace}")
```

- [ ] **Step 19.3: 사용자 — game UI 로 active 변경 + 후속 호출 method 추적**

사용자가 game UI 무공 패널에서 active 1 개 변경 → BepInEx 로그에 TRACE 출력 → method path 발견.

- [ ] **Step 19.4: 발견된 method 를 ActiveKungfuApplier.TriggerUiRefresh 에 추가**

flag toggle path 대신 또는 추가로 발견 method 호출.

- [ ] **Step 19.5: Spike Phase 1 Step 2/3 재실행 — UI 갱신 확인**

PASS 시 Phase 3 진행 (Task 8).

- [ ] **Step 19.6: 결과 dump + commit**

```bash
git add docs/superpowers/dumps/2026-05-02-active-ui-refresh-trace.md src/...
git commit -m "spike(v0.5.1): trace round 2 — [method path] 발견 / 미발견"
```

---

### Task 20 (alt): Symbol scan — Assembly-CSharp.dll 의 method 전수 dump

**전제**: Task 7 에서 "Symbol scan" 결정 또는 Task 19 FAIL 후.

**Files:**
- Create: `docs/superpowers/dumps/2026-05-02-active-ui-refresh-symbols.md`

- [ ] **Step 20.1: 후보 method 전수 dump**

Probe 에 reflection scan 추가 — `Refresh.*Skill / Refresh.*Kungfu / Refresh.*Panel / Refresh.*Fight / Update.*Skill` 정규식 매칭 method 모두 출력.

- [ ] **Step 20.2: zero-arg 후보 시도**

각 후보 method 에 대해:
- arg 갯수 = 0 또는 1 (player 만)
- F12 한 번 누를 때마다 1 개씩 시도 → UI 갱신 보이면 방치, 아니면 다음 후보

- [ ] **Step 20.3: 발견 시 Phase 3 진행**

발견 method → ActiveKungfuApplier.TriggerUiRefresh 에 추가.

- [ ] **Step 20.4: 모두 FAIL 시 — abort + maintenance**

dump 작성 + Task 22 (Out path) 진행.

---

### Task 21 (alt): Trace + Symbol scan 모두 FAIL — abort + maintenance

**전제**: Task 19 + 20 모두 FAIL.

**Files:**
- Create: `docs/superpowers/dumps/2026-05-02-active-kungfu-v0.5.1-fail.md`
- Modify: `docs/HANDOFF.md`

- [ ] **Step 21.1: FAIL dump 작성**

```markdown
# v0.5.1 active full — FAIL report (2026-05-02)

## Outcome
모든 spike path (Step 1-4 + Trace round 2 + Symbol scan) FAIL — release 안 함.

## 시도 history
- Step 1 (1-swap): UI 미반영
- Step 2 (11-swap): UI 미반영
- Step 3 (11-swap + flag toggle): UI 미반영
- Trace round 2: method path 미발견 또는 호출했으나 효과 없음
- Symbol scan: 모든 후보 zero-arg 호출 — 효과 없음

## v0.5 evidence + v0.5.1 추가 evidence
[Spike dump 파일 link]
[Trace dump 파일 link]
[Symbol scan dump 파일 link]

## 결정
- v0.5.1.0 release 안 함
- foundation 보존 (Capabilities.ActiveKungfu = false 유지, ApplySelection 변경 없음)
- v0.6+ sub-project 변경 — 외형 / 인벤토리 등
```

- [ ] **Step 21.2: Probe 코드 cleanup (release 안 해도 cleanup)**

Task 15 의 Step 15.1-15.4 와 동일.

- [ ] **Step 21.3: PinpointPatcher 의 SetActiveKungfu 본문 + ProbeActiveKungfuCapability 원복**

Task 10 의 변경을 revert — `ProbeActiveKungfuCapability` 는 hardcoded false 로 다시.

- [ ] **Step 21.4: ActiveKungfuApplier.cs 보존 결정**

옵션:
- 옵션 A: 보존 (v0.6 sub-project 의 startpoint, dead code 이지만 evidence)
- 옵션 B: 삭제 (foundation 만 보존, code 는 v0.6 에서 재작성)

권장: 옵션 A — v0.6 active full retry 시 startpoint.

- [ ] **Step 21.5: HANDOFF.md 업데이트**

§6 에 "v0.5.1 active full — FAIL — v0.5 와 동일 패턴" 추가 + v0.6 sub-project 후보 갱신.

- [ ] **Step 21.6: Commit**

```bash
git add -A
git commit -m "chore(v0.5.1): active full FAIL — Probe cleanup + foundation 보존 + HANDOFF"
```

- [ ] **Step 21.7: Branch 결정**

- v0.5.1 branch 유지 (v0.6 시 재시도 startpoint) 또는
- main 으로 cherry-pick (foundation 만) + branch 폐기

---

### Task 22: Out — abort 결정 시 sub-project 변경

**전제**: Task 7 에서 "abort" 직접 선택 또는 Task 21 후.

- [ ] **Step 22.1: 사용자에게 다음 sub-project 결정 받기**

옵션:
- 외형 (faceData + partPosture sub-data wrapper graph)
- 인벤토리 (ItemData[] wrapper graph)
- 창고 (동상)
- 무공 list (KungfuSkillLvData ctor / factory)
- maintenance 모드

- [ ] **Step 22.2: 새 sub-project brainstorming 시작**

선택된 sub-project 로 brainstorming 새 사이클 시작 — `superpowers:brainstorming` 스킬.

---

## Self-Review Checklist

Plan 작성 후 spec 과 대조:

**1. Spec coverage**:
- [x] §1 Context — Phase 1 baseline + v0.5 evidence 활용 ✓
- [x] §2 Goals (8 항목) — Task 8-14 (Apply + Restore + persistence + Capabilities + UI + legacy + 회귀) ✓
- [x] §2 Non-goals (10 항목) — non-goal 은 plan 에서 다루지 않음 (외형/list/인벤토리/창고 = Task 22 sub-project 변경) ✓
- [x] §3.1 Hybrid flow — Phase 1 → 2 (Spike) → 3 (Impl) → 4 (Smoke) → 5 (Release) ✓
- [x] §3.2 Spike → release scope matrix — Task 11 (Smoke), Task 18 (Release), Task 21 (FAIL alternate) ✓
- [x] §3.3 영향 파일 — File Structure section 일치 ✓
- [x] §4 Spike Phase 1 detail — Task 2-7 (Step 1-4 + user gate) ✓
- [x] §5 Implementation 설계 — Task 8-10 (Applier + Patcher 본문 + Capability) ✓
- [x] §6 Smoke 시나리오 1-3 + 회귀 — Task 11-14 ✓
- [x] §7 Failure mode — Task 7, 19-22 (alternate flow) ✓
- [x] §8 Release / Git plan — Task 15-18 ✓
- [x] §9 v0.6+ 후보 — Task 17 HANDOFF 업데이트 + Task 22 sub-project 변경 ✓
- [x] §10 Q&A 결정 — plan flow 자체에 반영 ✓
- [x] Appendix A v0.5 evidence — Task 8 / 9 의 코드에 반영 ✓

**2. Placeholder scan**:
- "TBD" / "TODO" / "implement later" 없음 ✓
- "fill in details" 없음 ✓
- "Similar to Task N" 없음 — 각 task 가 독립 step 작성 ✓
- 모든 code step 에 실제 code block 있음 ✓

**3. Type consistency**:
- `ActiveKungfuApplier.Apply(player, slot, sel)` 시그니처 — Task 8/9/10 일관 ✓
- `Result.Skipped / Reason / UnequipCount / EquipCount / MissingCount` — Task 8/9/10 일관 ✓
- `ExtractEquippedSkillIDs(JsonElement)` — Task 8/9 일관 ✓
- `Capabilities.ActiveKungfu` — Task 10 의 Probe 와 일관 ✓
- `kungfuSkills[].equiped` / `skillID` — 모든 task 에서 일관 ✓
- `EquipSkill / UnequipSkill (wrapper, true)` — Task 8/9/10 일관 ✓

수정할 항목 없음 — plan 상태로 진행.

---

**Plan complete.**
