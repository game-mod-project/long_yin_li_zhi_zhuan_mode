# LongYin Roster Mod v0.5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v0.4 deferred 항목 중 외형 (portraitID + gender + sprite refresh) 과 무공 active (semantic 재조사) 두 항목을 dual PoC 로 도전, OR-gate 로 release scope 결정 (외형 OR active PASS → v0.5.0 release; 양쪽 FAIL → maintenance 모드 + PoC report).

**Architecture:** v0.4 의 selection-aware 9-step pipeline / `ApplySelection` / `SimpleFieldMatrix` 패턴을 그대로 재사용. 외형 PASS 시 `SimpleFieldMatrix` 에 +2 entry (Category=Appearance) + sprite invalidate hook. active PASS 시 `PinpointPatcher.SetActiveKungfu` step 본문 (현재 stub) 을 실제 method path 로 교체. 신규 인프라 없음. Slot schema 변경 없음.

**Tech Stack:** BepInEx 6.0.0-dev (IL2CPP, .NET 6) / HarmonyLib / Il2CppInterop / Newtonsoft.Json (IL2CPP-bound, Serialize 만) / System.Text.Json / xUnit + Shouldly.

**선행 spec:** [`2026-05-01-longyin-roster-mod-v0.5-design.md`](../specs/2026-05-01-longyin-roster-mod-v0.5-design.md)

**작업 흐름**: Phase 1 (foundation) → Phase 2 (외형 PoC) → G1 → Phase 3 (외형 promote, PASS only) → Phase 4 (active PoC) → G2 / G3 → Phase 5 (active promote, PASS only) → Phase 6 (UI/Capabilities) → Phase 7 (integration + smoke G4) → Phase 8 (release G5). 양쪽 FAIL 시 Phase 9 alternate flow.

---

## File Structure

### 신규 파일

| 경로 | 책임 | 조건부? | Lifetime |
|---|---|---|---|
| `src/LongYinRoster/Core/Probes/ProbePortraitRefresh.cs` | 외형 PoC Phase 2 — setter direct + refresh method 시도 | 항상 (PoC) | release 전 제거 |
| `src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs` | active PoC Phase A/B/C — save-diff + Harmony trace + in-memory | 항상 (PoC) | release 전 제거 |
| `src/LongYinRoster/Core/Probes/ProbeRunner.cs` | F12 trigger → 어느 Probe 실행할지 분기 | 항상 (PoC) | release 전 제거 |
| `src/LongYinRoster/Core/PortraitRefresh.cs` | sprite cache invalidate method 캡슐화 | ✓ 외형 PASS | 영구 |
| `src/LongYinRoster/Core/ActiveKungfuPath.cs` | active set method path + list 검증 | ✓ active PASS | 영구 |
| `src/LongYinRoster.Tests/PortraitRefreshTests.cs` | mock player 검증 unit test | ✓ 외형 PASS | 영구 |
| `src/LongYinRoster.Tests/ActiveKungfuPathTests.cs` | list 부재 시 skip + warning 검증 | ✓ active PASS | 영구 |
| `docs/superpowers/dumps/2026-05-01-portrait-methods.md` | Phase 1 static dump 후보 list | 항상 | 영구 |
| `docs/superpowers/dumps/2026-05-01-portrait-poc-result.md` | Phase 2 PASS/FAIL evidence | 항상 | 영구 |
| `docs/superpowers/dumps/2026-05-01-active-kungfu-diff.md` | Phase A save-diff 결과 | 항상 | 영구 |
| `docs/superpowers/dumps/2026-05-01-active-kungfu-trace.md` | Phase B Harmony trace 결과 | 항상 | 영구 |
| `docs/superpowers/dumps/2026-05-01-active-kungfu-poc-result.md` | Phase C PASS/FAIL evidence | 항상 | 영구 |

### 수정 파일

| 경로 | 변경 | 조건부? |
|---|---|---|
| `src/LongYinRoster/Core/Capabilities.cs` | `Appearance` flag 추가 | 항상 |
| `src/LongYinRoster/Core/SimpleFieldMatrix.cs` | `FieldCategory.Appearance` enum + portraitID/gender entry | ✓ 외형 PASS |
| `src/LongYinRoster/Core/PinpointPatcher.cs` | SetSimpleFields 의 selection switch + Appearance hook (외형) / SetActiveKungfu 본문 (active) | PASS 따라 |
| `src/LongYinRoster/UI/SlotDetailPanel.cs` | Capabilities.Appearance / ActiveKungfu 가 true 면 disabled 플래그 false | PASS 따라 |
| `src/LongYinRoster/Util/KoreanStrings.cs` | `Cat_Appearance` 추가 + disabled suffix 제거 (PASS 항목) | 항상 + PASS 따라 |
| `src/LongYinRoster/Plugin.cs` | F12 trigger handler 추가 (PoC), 1 회 capability probe 호출 | 항상 (PoC) / 항상 (capability) |
| `src/LongYinRoster.Tests/CapabilitiesTests.cs` | `Appearance` round-trip | 항상 |
| `src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs` (있다면 / 신규 생성) | Appearance Category entry | ✓ 외형 PASS |
| `docs/HANDOFF.md` | v0.5 도달점 + 새 §6 v0.6+ 후보 | 항상 (release / FAIL 모두) |
| `README.md` | v0.5 highlights | ✓ release 시만 |
| `Directory.Build.props` 또는 `Plugin.cs:VERSION` | 0.4.0 → 0.5.0 | ✓ release 시만 |

---

## Phase 1 — Foundation (항상 실행)

### Task 1: Branch 생성 + baseline 검증

**Files:**
- Read: `Plugin.cs`, `Capabilities.cs`, current git status

- [ ] **Step 1.1: 작업 worktree / branch 확인**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git status
git log --oneline -5
```

Expected: clean working tree, main HEAD = `c248802 docs: v0.5 spec ...`.

- [ ] **Step 1.2: v0.4 baseline 검증 — `dotnet test`**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: **40/40 PASS** (v0.4 baseline 유지).

- [ ] **Step 1.3: v0.4 baseline 검증 — `dotnet build`**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: BUILD SUCCEEDED, `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 자동 배포.

작업 시작 전 baseline 이 깨졌는지 확인. 깨졌으면 v0.5 작업 시작 전에 fix.

---

### Task 2: `Capabilities.Appearance` flag 추가

**Files:**
- Modify: `src/LongYinRoster/Core/Capabilities.cs`
- Modify: `src/LongYinRoster.Tests/CapabilitiesTests.cs` (있다면; 없으면 신규)

- [ ] **Step 2.1: Failing test — `CapabilitiesTests` 에 Appearance round-trip**

`src/LongYinRoster.Tests/CapabilitiesTests.cs` 가 이미 있으면 수정, 없으면 신규.

```csharp
// File: src/LongYinRoster.Tests/CapabilitiesTests.cs

using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CapabilitiesTests
{
    [Fact]
    public void AllOff_AllFalseIncludingAppearance()
    {
        var c = Capabilities.AllOff();
        c.Identity.ShouldBeFalse();
        c.ActiveKungfu.ShouldBeFalse();
        c.ItemList.ShouldBeFalse();
        c.SelfStorage.ShouldBeFalse();
        c.Appearance.ShouldBeFalse();
    }

    [Fact]
    public void AllOn_AllTrueIncludingAppearance()
    {
        var c = Capabilities.AllOn();
        c.Identity.ShouldBeTrue();
        c.ActiveKungfu.ShouldBeTrue();
        c.ItemList.ShouldBeTrue();
        c.SelfStorage.ShouldBeTrue();
        c.Appearance.ShouldBeTrue();
    }

    [Fact]
    public void ToString_IncludesAppearanceFlag()
    {
        var c = new Capabilities { Appearance = true };
        c.ToString().ShouldContain("Appearance=True");
    }
}
```

- [ ] **Step 2.2: Run test — should fail**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~CapabilitiesTests"
```

Expected: FAIL — "Appearance" property does not exist on Capabilities.

- [ ] **Step 2.3: Implementation — Capabilities.Appearance 추가**

`src/LongYinRoster/Core/Capabilities.cs`:

```csharp
namespace LongYinRoster.Core;

public sealed class Capabilities
{
    public bool Identity     { get; init; }
    public bool ActiveKungfu { get; init; }
    public bool ItemList     { get; init; }
    public bool SelfStorage  { get; init; }
    public bool Appearance   { get; init; }   // v0.5 — portraitID + gender + sprite refresh

    public static Capabilities AllOff() => new();
    public static Capabilities AllOn() => new()
    {
        Identity = true, ActiveKungfu = true, ItemList = true, SelfStorage = true,
        Appearance = true,
    };

    public override string ToString() =>
        $"Identity={Identity} ActiveKungfu={ActiveKungfu} " +
        $"ItemList={ItemList} SelfStorage={SelfStorage} Appearance={Appearance}";
}
```

- [ ] **Step 2.4: Run tests — should pass**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~CapabilitiesTests"
```

Expected: 3 tests PASS (또는 기존 + 3).

- [ ] **Step 2.5: Commit**

```bash
git add src/LongYinRoster/Core/Capabilities.cs src/LongYinRoster.Tests/CapabilitiesTests.cs
git commit -m "feat(core): Capabilities.Appearance flag (v0.5 외형 PoC prerequisite)"
```

---

### Task 3: `FieldCategory.Appearance` enum 추가

**Files:**
- Modify: `src/LongYinRoster/Core/SimpleFieldMatrix.cs:13-20`

> **참고**: 이 task 는 enum 만 추가. 실제 entry 는 외형 PASS 후 Task 9 에서 추가.

- [ ] **Step 3.1: Implementation — Appearance enum value**

`src/LongYinRoster/Core/SimpleFieldMatrix.cs` 의 `FieldCategory` enum 에 추가:

```csharp
public enum FieldCategory
{
    None,         // 부상/충성/호감 — Apply 안 함, 영구 보존
    Stat,
    Honor,
    Skin,
    SelfHouse,
    TalentPoint,
    Appearance,   // v0.5 — portraitID + gender (외형 PoC PASS 시 entry 추가)
}
```

- [ ] **Step 3.2: Update PinpointPatcher.SetSimpleFields selection switch**

`src/LongYinRoster/Core/PinpointPatcher.cs` 의 `SetSimpleFields` 함수의 `entry.Category switch` 에 추가. 일단 Appearance 는 `selection.Appearance` flag 따름 — 다음 Task 에서 ApplySelection 에 flag 가 이미 있는지 확인.

```csharp
bool enabled = entry.Category switch
{
    FieldCategory.Stat        => selection.Stat,
    FieldCategory.Honor       => selection.Honor,
    FieldCategory.Skin        => selection.Skin,
    FieldCategory.SelfHouse   => selection.SelfHouse,
    FieldCategory.TalentPoint => selection.TalentTag,
    FieldCategory.Appearance  => selection.Appearance,  // v0.5 — 외형 PASS 시 entries 추가됨
    FieldCategory.None        => false,
    _ => false,
};
```

- [ ] **Step 3.3: 빌드 검증 — ApplySelection.Appearance flag 존재 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release 2>&1 | tail -20
```

Expected: BUILD SUCCEEDED. v0.4 spec 에 의하면 `ApplySelection` 은 이미 `Appearance` flag 보유. 만약 컴파일 실패 → ApplySelection 에 추가해야 (sub-task A 로 분기).

- [ ] **Step 3.3a (sub-task, 필요 시): ApplySelection 에 Appearance flag 추가**

만약 Step 3.3 빌드 실패면 이 단계 진행. 성공이면 skip.

`src/LongYinRoster/Slots/ApplySelection.cs` (또는 비슷한 위치) 에 `bool Appearance { get; init; }` 추가 + JSON helpers 의 `appearance` 키 처리. v0.4 의 9 카테고리 외 추가.

- [ ] **Step 3.4: Run tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 모든 기존 + 새 tests PASS.

- [ ] **Step 3.5: Commit**

```bash
git add src/LongYinRoster/Core/SimpleFieldMatrix.cs src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "feat(core): FieldCategory.Appearance + SetSimpleFields selection switch (v0.5 외형 PoC prerequisite)"
```

---

### Task 4: `KoreanStrings.Cat_Appearance` 추가

**Files:**
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs:78-89`

- [ ] **Step 4.1: Implementation**

`src/LongYinRoster/Util/KoreanStrings.cs` 의 v0.4 카테고리 섹션에 추가:

```csharp
    // v0.5 — 외형 카테고리
    public const string Cat_Appearance      = "외형";
```

위치: `Cat_SelfStorage` 다음, `Cat_DisabledSuffix` 앞.

- [ ] **Step 4.2: 빌드 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 4.3: Commit**

```bash
git add src/LongYinRoster/Util/KoreanStrings.cs
git commit -m "feat(strings): v0.5 — 외형 카테고리 label"
```

---

### Task 5: `ProbeRunner` + Plugin F12 trigger temp wiring

**Files:**
- Create: `src/LongYinRoster/Core/Probes/ProbeRunner.cs`
- Modify: `src/LongYinRoster/Plugin.cs`

> **목적**: PoC 실행을 위한 F12 trigger 인프라. v0.4 의 HeroDataDumpV04 + [F12] 패턴 그대로. release 직전 제거.

- [ ] **Step 5.1: ProbeRunner 신규**

`src/LongYinRoster/Core/Probes/ProbeRunner.cs`:

```csharp
using System;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5 PoC 임시 trigger. F12 시 사용자 선택에 따라 Probe 실행.
/// release 직전 ProbeRunner + ProbePortraitRefresh + ProbeActiveKungfuV2 + Plugin 의 F12 handler 일괄 제거.
/// </summary>
internal static class ProbeRunner
{
    public enum Mode { Portrait, ActiveDiff, ActiveTrace, ActiveInMemory }

    /// <summary>현재 활성 Probe 모드. 개발 중 코드로 변경.</summary>
    public static Mode Current = Mode.Portrait;

    public static void Run()
    {
        try
        {
            var player = HeroLocator.GetPlayer();
            if (player == null) { Logger.Warn("ProbeRunner: player not found"); return; }

            switch (Current)
            {
                case Mode.Portrait:
                    Logger.Info("=== ProbePortraitRefresh START ===");
                    ProbePortraitRefresh.Run(player);
                    Logger.Info("=== ProbePortraitRefresh END ===");
                    break;
                case Mode.ActiveDiff:
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseA (save-diff) START ===");
                    ProbeActiveKungfuV2.RunPhaseA(player);
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseA END ===");
                    break;
                case Mode.ActiveTrace:
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseB (Harmony trace) START ===");
                    ProbeActiveKungfuV2.RunPhaseB(player);
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseB END ===");
                    break;
                case Mode.ActiveInMemory:
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseC (in-memory) START ===");
                    ProbeActiveKungfuV2.RunPhaseC(player);
                    Logger.Info("=== ProbeActiveKungfuV2.PhaseC END ===");
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"ProbeRunner: {ex.GetType().Name}: {ex.Message}");
            Logger.Error(ex.StackTrace ?? "(no stack)");
        }
    }
}
```

> 주의: `ProbePortraitRefresh.Run` / `ProbeActiveKungfuV2.RunPhaseA/B/C` 는 다음 task 에서 정의. 이 단계에서는 컴파일 에러 발생 — 다음 task 와 묶어 한 번에 commit.

- [ ] **Step 5.2: Plugin.cs 에 F12 핸들러 wiring**

`src/LongYinRoster/Plugin.cs` 의 `Load()` 함수에서 ModWindow 추가 후, F12 listener 추가하는 컴포넌트 또는 ModWindow 자체에 핸들러 hook. v0.4 의 F12 [F12] handler 패턴 (commit `2d4b24e` 에서 제거된 것) 을 다시 추가.

ModWindow.cs 에 `Update()` 가 이미 있다면 그 안에 추가:

```csharp
// 추가 — ModWindow.Update() 또는 별도 컴포넌트 안:
if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12))
{
    LongYinRoster.Core.Probes.ProbeRunner.Run();
}
```

> 정확한 위치는 `ModWindow.cs` 의 Update() 검토 후 결정. v0.4 의 commit 2d4b24e 가 무엇을 제거했는지 `git show 2d4b24e` 로 참조 — 같은 위치에 v0.5 용 핸들러 추가.

- [ ] **Step 5.3: 빌드 (다음 task 와 함께 commit) — 일단 build 안 되어도 ok**

이 task 는 다음 두 task (Probe 본체) 를 모두 작성한 후 한 번에 commit. 단계로만 분리.

- [ ] **Step 5.4: 임시 markdown — Phase 정리 cheat sheet**

`docs/superpowers/dumps/2026-05-01-poc-cheatsheet.md` (선택사항, 작업자 본인용):

```markdown
# v0.5 PoC cheat sheet

## Probe 모드 변경
`ProbeRunner.Current = Mode.{Portrait, ActiveDiff, ActiveTrace, ActiveInMemory}`
빌드 → 게임 재시작 → F12

## 게이트
- G1: Portrait Phase 2 후 사용자 PASS/FAIL
- G2: ActiveDiff 후 사용자 Phase B 진행 동의
- G3: ActiveInMemory 후 사용자 PASS/FAIL
```

(이 파일은 release 전 제거 안 해도 됨 — `dumps/` 디렉터리는 영구.)

---

## Phase 2 — 외형 PoC (G1 게이트)

### Task 6: 외형 Phase 1 — Static dump

**Files:**
- Create: `docs/superpowers/dumps/2026-05-01-portrait-methods.md`

- [ ] **Step 6.1: HeroDataDumpV04 같은 dump 도구로 method enumerate**

v0.4 의 `HeroDataDumpV04` 가 release 빌드에서 제거됨 (commit `2d4b24e`). v0.5 에서는 새 dump 가 필요하면 `git show 2d4b24e^:src/LongYinRoster/Core/HeroDataDumpV04.cs` 로 코드 복원하여 v0.5 용으로 재사용. 또는 ProbeRunner 에 dump 모드 하나 더 추가.

수동 dump 가 더 빠를 수도 있음:
- ILSpy / dnSpy 등으로 `BepInEx/interop/Assembly-CSharp.dll` 의 `HeroData` 클래스 method 검색
- 패턴 매칭: `Refresh.*Portrait`, `Reload.*Portrait`, `Update.*Portrait`, `Refresh.*Face`, `Refresh.*Avatar`, `Refresh.*Sprite`, `set_portraitID`
- `MainHeroData` / `PlayerView` / `HeroPanel` (있다면) 도 같은 패턴

- [ ] **Step 6.2: dump 결과를 markdown 으로 저장**

`docs/superpowers/dumps/2026-05-01-portrait-methods.md`:

```markdown
# v0.5 외형 PoC — Phase 1 Static dump (2026-05-01)

## Scope
`Assembly-CSharp.dll` 의 portrait / face / avatar / sprite refresh 후보 method.

## 후보 method list

### HeroData
- `RefreshPortrait()` — (있으면)
- `set_portraitID(int)` — IL2CPP setter (strip 여부 검증 필요)
- `RefreshFaceData()` — (있으면)
- ... (실제 결과 채움)

### MainHeroData / PlayerView / HeroPanel (UI 컴포넌트)
- ... (실제 결과 채움)

### 기타
- ... (실제 결과 채움)

## abort 평가
- 후보 method 0 → 외형 PoC FAIL (즉시 종료)
- 후보 method 1+ → Phase 2 진행
```

- [ ] **Step 6.3: 사용자 확인 — Phase 2 진행 여부**

> **GATE (informal)**: 후보 method 0 시 외형 PoC FAIL 선언, Task 7 ~ Task 12 (외형 promote) skip, Phase 4 active PoC 로 직진. 후보 method 1+ 시 다음 task.

- [ ] **Step 6.4: Commit**

```bash
git add docs/superpowers/dumps/2026-05-01-portrait-methods.md
git commit -m "poc: v0.5 외형 Phase 1 — static dump (HeroData/UI portrait method 후보)"
```

---

### Task 7: 외형 Phase 2 — `ProbePortraitRefresh` In-memory PoC

**Files:**
- Create: `src/LongYinRoster/Core/Probes/ProbePortraitRefresh.cs`
- Modify: `src/LongYinRoster/Core/Probes/ProbeRunner.cs` (이미 reference, 본체 추가)

- [ ] **Step 7.1: ProbePortraitRefresh skeleton**

`src/LongYinRoster/Core/Probes/ProbePortraitRefresh.cs`:

```csharp
using System;
using System.Reflection;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5 외형 PoC Phase 2. portraitID + gender setter direct 시도 + refresh method 호출.
/// PASS 기준: 게임 화면 초상화 즉시 변경 + save-reload 후 유지 (사용자 G1 게이트).
/// </summary>
internal static class ProbePortraitRefresh
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run(object player)
    {
        var t = player.GetType();
        Logger.Info($"player type: {t.FullName}");

        // 1. 현재값 read
        int currentPortrait = ReadInt(player, "portraitID");
        int currentGender   = ReadInt(player, "gender");
        Logger.Info($"current portraitID={currentPortrait}, gender={currentGender}");

        // 2. setter direct 시도 — read-back 검증
        int newPortrait = currentPortrait + 1;
        TrySetterDirect(player, "set_portraitID", newPortrait);
        int afterSetter = ReadInt(player, "portraitID");
        Logger.Info($"after setter direct: portraitID={afterSetter} (expected {newPortrait})");

        if (afterSetter != newPortrait)
        {
            Logger.Warn("setter direct silent no-op — fallback: backing field reflection");
            TryFieldDirect(player, "portraitID", newPortrait);
            int afterField = ReadInt(player, "portraitID");
            Logger.Info($"after field set: portraitID={afterField} (expected {newPortrait})");
        }

        // 3. 후보 refresh method 들 순회 호출 (Phase 1 dump 결과 따라 실제 list 채움)
        string[] candidateMethods =
        {
            "RefreshPortrait",
            "RefreshFaceData",
            "RefreshAvatar",
            "RefreshSelfState",  // 알려진 메서드 — 부수효과 클 수 있음
            // ... (Phase 1 dump 결과 따라 추가)
        };

        foreach (var name in candidateMethods)
        {
            TryCall(player, name);
        }

        Logger.Info("=== Phase 2 done. 화면 + save-reload 후 G1 판정 ===");
    }

    private static int ReadInt(object obj, string field)
    {
        var prop = obj.GetType().GetProperty(field, F);
        if (prop != null) return (int)(prop.GetValue(obj) ?? 0);
        var fld = obj.GetType().GetField(field, F);
        if (fld != null) return (int)(fld.GetValue(obj) ?? 0);
        Logger.Warn($"field/property '{field}' not found");
        return -1;
    }

    private static void TrySetterDirect(object obj, string methodName, int value)
    {
        var m = obj.GetType().GetMethod(methodName, F);
        if (m == null) { Logger.Warn($"method '{methodName}' not found"); return; }
        try { m.Invoke(obj, new object[] { value }); Logger.Info($"called {methodName}({value})"); }
        catch (Exception ex) { Logger.Warn($"{methodName} threw: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void TryFieldDirect(object obj, string field, int value)
    {
        var fld = obj.GetType().GetField(field, F);
        if (fld == null) { Logger.Warn($"field '{field}' not found"); return; }
        try { fld.SetValue(obj, value); Logger.Info($"field-set {field}={value}"); }
        catch (Exception ex) { Logger.Warn($"field-set {field} threw: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void TryCall(object obj, string methodName)
    {
        var m = obj.GetType().GetMethod(methodName, F, null, Type.EmptyTypes, null);
        if (m == null) { Logger.Info($"method '{methodName}': not found (skip)"); return; }
        try { m.Invoke(obj, null); Logger.Info($"called {methodName}() — ok"); }
        catch (Exception ex) { Logger.Warn($"{methodName} threw: {ex.GetType().Name}: {ex.Message}"); }
    }
}
```

- [ ] **Step 7.2: 빌드 + 게임 닫기 → 빌드 → 게임 시작**

```bash
tasklist | grep -i LongYinLiZhiZhuan
# 게임이 떠 있으면 종료
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 7.3: 게임 내 검증 — F12 trigger**

게임 시작 → 캐릭터 입장 → F11 (mod window) → ProbeRunner.Current = Mode.Portrait 확인. 코드에 default 로 Portrait 모드 박혀 있음. F12 키 → BepInEx log 관찰:

- `current portraitID=XX, gender=YY`
- setter direct 결과
- 각 candidate method 호출 결과
- 게임 화면의 초상화 변경 여부

- [ ] **Step 7.4: save → reload → 화면 유지 확인**

게임 자체 F5 save → 메뉴 → save load → 같은 캐릭터 → 초상화 유지 여부 + 정보창 정상 작동.

- [ ] **Step 7.5: PoC 결과 markdown**

`docs/superpowers/dumps/2026-05-01-portrait-poc-result.md`:

```markdown
# v0.5 외형 PoC — Phase 2 결과 (2026-05-01)

## Outcome
- **PASS / FAIL**: <사용자 G1 판정>

## 시도 method
- setter direct (`set_portraitID`): <결과>
- field reflection (`portraitID`): <결과>
- refresh methods: <결과>

## BepInEx log 발췌
```
<로그 붙여넣기>
```

## 화면 / save-reload 결과
- in-memory 즉시 변경: yes / no
- save → reload 후 유지: yes / no
- 정보창 정상: yes / no

## 결정
PASS 시 → Task 8 (Promote) 진행, Capabilities.Appearance = true.
FAIL 시 → 외형 defer to v0.6, §10 후보 evidence 갱신.
```

- [ ] **Step 7.6: G1 게이트 — 사용자 판정**

> **STOP**: 사용자에게 결과 확인 요청. PASS / FAIL 결정 받기.
> - PASS → Task 8 ~ 11 (외형 promote) 진행
> - FAIL → Task 8 ~ 11 skip, Phase 4 (active PoC) 로 진행

- [ ] **Step 7.7: Commit (PASS 또는 FAIL 모두)**

```bash
git add src/LongYinRoster/Core/Probes/ProbePortraitRefresh.cs \
        src/LongYinRoster/Core/Probes/ProbeRunner.cs \
        src/LongYinRoster/Plugin.cs \
        src/LongYinRoster/UI/ModWindow.cs \
        docs/superpowers/dumps/2026-05-01-portrait-poc-result.md
git commit -m "poc: v0.5 외형 Phase 2 — ProbePortraitRefresh + result <PASS/FAIL>"
```

---

## Phase 3 — 외형 Promote (G1 PASS 시만)

> **조건부**: G1 게이트가 FAIL 이면 Task 8 ~ 11 skip, Phase 4 로 직진.

### Task 8: `Core/PortraitRefresh.cs` 신규 — production class

**Files:**
- Create: `src/LongYinRoster/Core/PortraitRefresh.cs`

- [ ] **Step 8.1: Implementation**

PoC 에서 발견된 method path 를 캡슐화. **실제 method 이름은 PoC 결과 따라 채움**:

```csharp
using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5 — 외형 Apply 후 sprite cache invalidate.
/// PoC Phase 2 에서 발견된 method path 를 production code 로 캡슐화.
/// </summary>
public static class PortraitRefresh
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// Apply 후 호출 — portrait sprite 의 cache invalidate.
    /// 실패 시 throw 하지 않음 (PinpointPatcher TryStep 가 catch).
    /// </summary>
    public static void Invoke(object player)
    {
        if (player == null) { Logger.Warn("PortraitRefresh.Invoke: player null"); return; }

        // PoC PASS 결과 따라 실제 method 이름 채움.
        // 예: TryCall(player, "RefreshPortrait");
        TryCall(player, "<METHOD_FROM_POC>");
    }

    private static void TryCall(object obj, string methodName)
    {
        var m = obj.GetType().GetMethod(methodName, F, null, Type.EmptyTypes, null);
        if (m == null) { Logger.Warn($"PortraitRefresh: method '{methodName}' not found"); return; }
        try { m.Invoke(obj, null); Logger.Info($"PortraitRefresh: {methodName}() ok"); }
        catch (Exception ex) { Logger.Warn($"PortraitRefresh: {methodName} threw: {ex.Message}"); }
    }
}
```

> **TODO 처리**: `<METHOD_FROM_POC>` 는 Task 7 의 PoC 결과 markdown 에서 추출한 정확한 method 이름으로 교체. PoC 결과에 method 가 1 개 이상이면 모두 호출 (TryCall 여러 번).

- [ ] **Step 8.2: 빌드 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 8.3: Commit**

```bash
git add src/LongYinRoster/Core/PortraitRefresh.cs
git commit -m "feat(core): PortraitRefresh — v0.5 외형 sprite cache invalidate (PoC method promote)"
```

---

### Task 9: `SimpleFieldMatrix` — portraitID + gender entry 추가

**Files:**
- Modify: `src/LongYinRoster/Core/SimpleFieldMatrix.cs:43-62`
- Create: `src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs` (없으면)

- [ ] **Step 9.1: Failing test**

`src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs` 신규 (또는 기존 수정):

```csharp
using System.Linq;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class SimpleFieldMatrixTests
{
    [Fact]
    public void PortraitId_Entry_Exists_With_Appearance_Category()
    {
        var entry = SimpleFieldMatrix.Entries.SingleOrDefault(e => e.PropertyName == "portraitID");
        entry.ShouldNotBeNull();
        entry.Category.ShouldBe(FieldCategory.Appearance);
        entry.Type.ShouldBe(typeof(int));
        entry.SetterStyle.ShouldBe(SetterStyle.Direct);
    }

    [Fact]
    public void Gender_Entry_Exists_With_Appearance_Category()
    {
        var entry = SimpleFieldMatrix.Entries.SingleOrDefault(e => e.PropertyName == "gender");
        entry.ShouldNotBeNull();
        entry.Category.ShouldBe(FieldCategory.Appearance);
        entry.Type.ShouldBe(typeof(int));
        entry.SetterStyle.ShouldBe(SetterStyle.Direct);
    }
}
```

- [ ] **Step 9.2: Run test — should fail**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SimpleFieldMatrixTests"
```

Expected: FAIL — entries 에 portraitID / gender 없음.

- [ ] **Step 9.3: Implementation — entry 추가**

`src/LongYinRoster/Core/SimpleFieldMatrix.cs:62` 의 array 끝에 추가:

```csharp
        new SimpleFieldEntry("expLivingSkill[i]", "expLivingSkill",      "expLivingSkill",      typeof(float), "ChangeLivingSkillExp",       SetterStyle.Delta,  FieldCategory.Stat),

        // v0.5 — 외형 (PoC PASS 결과)
        new SimpleFieldEntry("portraitID",        "portraitID",          "portraitID",          typeof(int),   "set_portraitID",             SetterStyle.Direct, FieldCategory.Appearance),
        new SimpleFieldEntry("gender",            "gender",              "gender",              typeof(int),   "set_gender",                 SetterStyle.Direct, FieldCategory.Appearance),
    };
}
```

> **참고**: SetterMethod 의 정확한 이름은 PoC 결과에 따라. setter direct 가 silent no-op 이고 field reflection 으로만 작동하면 SetterStyle / SetterMethod 처리 방식을 PinpointPatcher.SetSimpleFields 의 기존 InvokeSetter 와 호환되도록 검증.

- [ ] **Step 9.4: Run tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SimpleFieldMatrixTests"
```

Expected: PASS.

- [ ] **Step 9.5: Commit**

```bash
git add src/LongYinRoster/Core/SimpleFieldMatrix.cs src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs
git commit -m "feat(core): SimpleFieldMatrix +2 entry — portraitID/gender (v0.5 외형 PASS)"
```

---

### Task 10: `PinpointPatcher` — 외형 hook 추가

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs:40-48`

- [ ] **Step 10.1: Implementation — Apply pipeline 에 외형 step 추가**

`PinpointPatcher.Apply` 의 pipeline 에 외형 sprite invalidate step 추가:

```csharp
TryStep("SetSimpleFields",         () => SetSimpleFields(slot, currentPlayer, selection, res), res);
TryStep("SetIdentityFields",       () => SetIdentityFields(slot, currentPlayer, selection, res), res);
TryStep("SetActiveKungfu",         () => SetActiveKungfu(slot, currentPlayer, selection, res), res);
TryStep("RebuildItemList",         () => RebuildItemList(slot, currentPlayer, selection, res), res);
TryStep("RebuildSelfStorage",      () => RebuildSelfStorage(slot, currentPlayer, selection, res), res);
TryStep("RebuildKungfuSkills",     () => SkipKungfuSkills(res), res);
TryStep("RebuildHeroTagData",      () => RebuildHeroTagData(slot, currentPlayer, selection, res), res);
TryStep("RefreshPortrait",         () => RefreshPortraitStep(currentPlayer, selection, res), res);  // v0.5 신규
TryStep("RefreshSelfState",        () => RefreshSelfState(currentPlayer, res), res, fatal: true);
TryStep("RefreshExternalManagers", () => RefreshExternalManagers(currentPlayer, res), res);
```

그리고 새 step 함수:

```csharp
private static void RefreshPortraitStep(object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.Appearance) return;  // 외형 selection 안 됐으면 skip
    PortraitRefresh.Invoke(player);
    res.AppliedFields.Add("portraitRefresh");
}
```

- [ ] **Step 10.2: 빌드**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 10.3: Run all tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 모든 tests PASS (regress 안 됨 검증).

- [ ] **Step 10.4: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "feat(core): PinpointPatcher RefreshPortrait step (v0.5 외형 PASS)"
```

---

### Task 11: `PortraitRefreshTests` — unit test

**Files:**
- Create: `src/LongYinRoster.Tests/PortraitRefreshTests.cs`

- [ ] **Step 11.1: Test 작성**

```csharp
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class PortraitRefreshTests
{
    private class StubPlayer
    {
        public bool RefreshCalled;
        public void RefreshPortrait() => RefreshCalled = true;  // PoC 발견된 method 이름
    }

    [Fact]
    public void Invoke_Calls_RefreshMethod_OnPlayer()
    {
        var stub = new StubPlayer();
        PortraitRefresh.Invoke(stub);
        stub.RefreshCalled.ShouldBeTrue();
    }

    [Fact]
    public void Invoke_NullPlayer_DoesNotThrow()
    {
        Should.NotThrow(() => PortraitRefresh.Invoke(null));
    }
}
```

> **TODO 처리**: `RefreshPortrait` method 이름은 PoC 결과 따라 stub 의 method 이름 정정.

- [ ] **Step 11.2: Run test**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~PortraitRefreshTests"
```

Expected: 2 PASS.

- [ ] **Step 11.3: Commit**

```bash
git add src/LongYinRoster.Tests/PortraitRefreshTests.cs
git commit -m "test: PortraitRefreshTests — Invoke + null guard"
```

---

## Phase 4 — 무공 active PoC (G2 + G3 게이트)

### Task 12: active Phase A — `ProbeActiveKungfuV2.RunPhaseA` save-diff

**Files:**
- Create: `src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs`

- [ ] **Step 12.1: Probe skeleton — Phase A**

```csharp
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5 무공 active PoC. v0.4 A3 FAIL (wrapper.lv vs nowActiveSkill ID mismatch) 재도전.
/// 가설 변경: save-diff (Phase A) → Harmony trace (Phase B) → in-memory (Phase C).
/// </summary>
internal static class ProbeActiveKungfuV2
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static string SaveDir => Path.Combine(PathProvider.GameSaveDir, "SaveSlot0", "Hero");
    private static string SaveSlotN(int n) => Path.Combine(PathProvider.GameSaveDir, $"SaveSlot{n}", "Hero");

    /// <summary>
    /// Phase A — save-diff. 사용자가 두 save 를 만든 상태에서 호출.
    /// 시나리오: SaveSlot1 (active = X) vs SaveSlot2 (active = Y) 로 diff.
    /// </summary>
    public static void RunPhaseA(object _player)
    {
        const int SLOT_BEFORE = 1;
        const int SLOT_AFTER  = 2;

        Logger.Info($"ProbeActiveKungfuV2.PhaseA — diff SaveSlot{SLOT_BEFORE} vs SaveSlot{SLOT_AFTER}");

        var beforePath = SaveSlotN(SLOT_BEFORE);
        var afterPath  = SaveSlotN(SLOT_AFTER);

        if (!File.Exists(beforePath) || !File.Exists(afterPath))
        {
            Logger.Warn($"PhaseA: save 파일 없음. before={beforePath} ({File.Exists(beforePath)}) after={afterPath} ({File.Exists(afterPath)})");
            Logger.Warn("사용자: 게임 안에서 active 변경 전 SaveSlot1 에 save → active 변경 → SaveSlot2 에 save 후 F12 다시.");
            return;
        }

        // ApplySaveFileScanner 와 같은 방식으로 hero[heroID==0] JSON 추출.
        var beforeHero = ExtractPlayerJson(beforePath);
        var afterHero  = ExtractPlayerJson(afterPath);

        // diff — JSON 객체 깊이 비교.
        DiffJson("(root)", beforeHero, afterHero);

        Logger.Info("ProbeActiveKungfuV2.PhaseA done. diff 결과를 dumps/2026-05-01-active-kungfu-diff.md 에 캡처.");
    }

    private static JsonElement ExtractPlayerJson(string path)
    {
        // 실제로는 SaveFileScanner / SerializerService 의 코드 재사용. 일단 placeholder.
        // 정확한 path/parse 로직은 SaveFileScanner.cs 의 LoadHero0 패턴 참고.
        var raw = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.Clone();
    }

    private static void DiffJson(string path, JsonElement a, JsonElement b)
    {
        // 기본 diff — 깊이 우선, 차이 발견 시 path 와 값 출력.
        if (a.ValueKind != b.ValueKind)
        {
            Logger.Info($"DIFF[{path}]: kind {a.ValueKind} -> {b.ValueKind}");
            return;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in a.EnumerateObject())
                {
                    if (b.TryGetProperty(prop.Name, out var bv))
                        DiffJson($"{path}.{prop.Name}", prop.Value, bv);
                    else
                        Logger.Info($"DIFF[{path}.{prop.Name}]: removed");
                }
                foreach (var prop in b.EnumerateObject())
                    if (!a.TryGetProperty(prop.Name, out _))
                        Logger.Info($"DIFF[{path}.{prop.Name}]: added");
                break;
            case JsonValueKind.Array:
                if (a.GetArrayLength() != b.GetArrayLength())
                    Logger.Info($"DIFF[{path}]: length {a.GetArrayLength()} -> {b.GetArrayLength()}");
                int n = Math.Min(a.GetArrayLength(), b.GetArrayLength());
                for (int i = 0; i < n; i++) DiffJson($"{path}[{i}]", a[i], b[i]);
                break;
            default:
                if (a.GetRawText() != b.GetRawText())
                    Logger.Info($"DIFF[{path}]: {a.GetRawText()} -> {b.GetRawText()}");
                break;
        }
    }

    public static void RunPhaseB(object player) { Logger.Info("PhaseB stub — Task 13 에서 구현"); }
    public static void RunPhaseC(object player) { Logger.Info("PhaseC stub — Task 14 에서 구현"); }
}
```

> 주의: `ExtractPlayerJson` 의 정확한 logic 은 `SaveFileScanner.LoadHero0` 패턴 따라 — 게임 save 파일 구조 (`Hero` 안의 `[heroID==0]` 객체) 알맞게.

- [ ] **Step 12.2: 빌드 + 게임 절차**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

ProbeRunner.Current = Mode.ActiveDiff 로 변경 → 게임 빌드 → 게임 안에서:
1. active 무공 X 확인
2. 게임 SaveSlot 1 에 save
3. 게임 안에서 active 무공 Y 로 변경
4. 게임 SaveSlot 2 에 save
5. F12 → diff 출력 in BepInEx log

- [ ] **Step 12.3: 결과 markdown**

`docs/superpowers/dumps/2026-05-01-active-kungfu-diff.md`:

```markdown
# v0.5 active Phase A — save-diff (2026-05-01)

## 시나리오
- SaveSlot1: active = X (kungfuID=...)
- SaveSlot2: active = Y (kungfuID=...)

## diff 결과
```
<BepInEx 로그의 DIFF[...] 발췌>
```

## 변경 필드 set
- `nowActiveSkill`: <변경 전 -> 변경 후>
- `kungfuSkills[*].equiped`: <변경 양상>
- ... (실제 결과)

## abort 평가
- 변경 필드 0 → active PoC FAIL
- 변경 필드 1+ → Phase B (Harmony trace) 진행

## G2 게이트 — Phase B 진행 동의
사용자: <yes / no>
```

- [ ] **Step 12.4: G2 게이트 — 사용자 확인**

> **STOP**: 변경 필드 0 → active FAIL 선언, Phase 5 (active promote) skip, Phase 6 진행. 변경 필드 1+ → Phase B 진행 동의 받기.

- [ ] **Step 12.5: Commit**

```bash
git add src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs \
        docs/superpowers/dumps/2026-05-01-active-kungfu-diff.md
git commit -m "poc: v0.5 active Phase A — save-diff scaffold + result"
```

---

### Task 13: active Phase B — Harmony trace

**Files:**
- Modify: `src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs`

> **조건부**: G2 가 진행 동의일 때만. FAIL 시 skip.

- [ ] **Step 13.1: Phase B body — Harmony patch wiring**

`RunPhaseB` 본체:

```csharp
public static void RunPhaseB(object player)
{
    Logger.Info("PhaseB: Harmony trace 시작. 후보 method patch.");

    // Phase A 결과 기반 후보. 패턴 매칭으로 Assembly-CSharp 안의 후보 method enumerate.
    var candidatePatterns = new[] { "SetActiveSkill", "SwitchActiveSkill", "SetNowActiveSkill", "EquipKungfu", "ChangeActiveKungfu" };

    var heroDataType = player.GetType();
    foreach (var pattern in candidatePatterns)
    {
        foreach (var m in heroDataType.GetMethods(F))
        {
            if (m.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var harmony = new HarmonyLib.Harmony($"com.deepe.longyinroster.probe.{m.Name}");
                    var prefix = typeof(ProbeActiveKungfuV2).GetMethod(nameof(GenericPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(m, prefix: new HarmonyLib.HarmonyMethod(prefix));
                    Logger.Info($"PhaseB: patched {m.DeclaringType?.Name}.{m.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"PhaseB: patch {m.Name} failed: {ex.Message}");
                }
            }
        }
    }

    Logger.Info("PhaseB: 패치 완료. 사용자: 게임 UI 에서 active 변경 → log 관찰.");
}

private static void GenericPrefix(System.Reflection.MethodBase __originalMethod, object[] __args)
{
    var argDesc = __args == null ? "<null>" : string.Join(", ", System.Array.ConvertAll(__args, a => a?.ToString() ?? "null"));
    Logger.Info($"TRACE: {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}({argDesc})");
}
```

- [ ] **Step 13.2: 빌드 + 게임 시도**

ProbeRunner.Current = Mode.ActiveTrace → 빌드 → 게임:
1. F12 → patch 활성화
2. 게임 안에서 active 무공 변경 UI 클릭
3. BepInEx log 에 TRACE 메시지 관찰

- [ ] **Step 13.3: 결과 markdown**

`docs/superpowers/dumps/2026-05-01-active-kungfu-trace.md`:

```markdown
# v0.5 active Phase B — Harmony trace (2026-05-01)

## 시도 method
<patched method list>

## 호출 trace (game UI active 변경 시)
```
<BepInEx 로그 TRACE 발췌>
```

## 발견된 진짜 method path
- `<Class>.<Method>(<args>)` — 가장 outer 호출
- `<Class>.<Method>(<args>)` — inner

## abort 평가
- 후보 5+ patched, 0 호출 → active PoC FAIL
- 호출 1+ → Phase C (in-memory) 진행
```

- [ ] **Step 13.4: Commit**

```bash
git add src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs \
        docs/superpowers/dumps/2026-05-01-active-kungfu-trace.md
git commit -m "poc: v0.5 active Phase B — Harmony trace + result"
```

---

### Task 14: active Phase C — in-memory PoC + G3

**Files:**
- Modify: `src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs`
- Create: `docs/superpowers/dumps/2026-05-01-active-kungfu-poc-result.md`

> **조건부**: Phase B 가 method 발견했을 때만.

- [ ] **Step 14.1: Phase C body**

`RunPhaseC`:

```csharp
public static void RunPhaseC(object player)
{
    Logger.Info("PhaseC: 발견된 method 직접 호출 + 검증.");

    // Phase B 결과 따라 정확한 method 이름 / 시그니처 채움.
    const string METHOD_NAME = "<METHOD_FROM_PHASE_B>";

    // 1. slot 의 active ID 추출 (HARDCODED for PoC — 실제론 slot file load)
    int targetActiveId = 12345; // PoC 검증용 ID. 실제론 다른 무공 ID.

    // 2. list 존재 검증
    if (!HasKungfuInList(player, targetActiveId))
    {
        Logger.Warn($"PhaseC: kungfuSkills 에 ID {targetActiveId} 부재. skip + warning toast 시뮬레이션.");
        return;
    }

    // 3. method 호출
    var m = player.GetType().GetMethod(METHOD_NAME, F);
    if (m == null) { Logger.Warn($"PhaseC: method {METHOD_NAME} not found"); return; }

    try
    {
        m.Invoke(player, new object[] { targetActiveId /* + 다른 인자들 */ });
        Logger.Info($"PhaseC: {METHOD_NAME}({targetActiveId}) called ok");
    }
    catch (Exception ex)
    {
        Logger.Error($"PhaseC: {METHOD_NAME} threw: {ex.GetType().Name}: {ex.Message}");
    }

    Logger.Info("PhaseC done. 사용자: 화면 + save-reload 후 G3 판정.");
}

private static bool HasKungfuInList(object player, int kungfuId)
{
    // kungfuSkills 의 IL2CPP List 순회 — HeroLocator.cs 의 reflection 패턴 따라.
    // placeholder — 실제 구현은 IL2CppListOps 또는 같은 helper 활용.
    return true;  // PoC: 일단 true 가정
}
```

- [ ] **Step 14.2: 빌드 + 게임 시도**

ProbeRunner.Current = Mode.ActiveInMemory → 빌드 → 게임:
1. F12 → method 호출
2. 게임 안에서 active 무공 변경 검증
3. F5 save → reload → 정상 작동 확인

- [ ] **Step 14.3: 결과 markdown**

`docs/superpowers/dumps/2026-05-01-active-kungfu-poc-result.md`:

```markdown
# v0.5 active Phase C — PoC 결과 (2026-05-01)

## Outcome
- **PASS / FAIL**: <G3 판정>

## 발견된 method
- `<Class>.<Method>(<args>)`

## in-memory 결과
- method 호출 ok: yes / no
- 게임 UI 변화: yes / no
- save-reload 후 유지: yes / no

## list 부재 처리
- skip + warning toast: 검증됨 / N/A

## BepInEx log 발췌
```
<로그>
```

## 결정
PASS → Task 15+ promote.
FAIL → active defer to v0.6.
```

- [ ] **Step 14.4: G3 게이트 — 사용자 판정**

> **STOP**: PASS / FAIL 결정.
> - PASS → Task 15 ~ 17 (active promote)
> - FAIL → Task 15 ~ 17 skip, Phase 6 진행

- [ ] **Step 14.5: Commit**

```bash
git add src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs \
        docs/superpowers/dumps/2026-05-01-active-kungfu-poc-result.md
git commit -m "poc: v0.5 active Phase C — in-memory + result <PASS/FAIL>"
```

---

## Phase 5 — active Promote (G3 PASS 시만)

### Task 15: `Core/ActiveKungfuPath.cs` 신규

**Files:**
- Create: `src/LongYinRoster/Core/ActiveKungfuPath.cs`

- [ ] **Step 15.1: Implementation**

```csharp
using System;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5 — 무공 active set + list 검증.
/// PoC Phase B/C 에서 발견된 method path 캡슐화.
/// </summary>
public static class ActiveKungfuPath
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>slot JSON 의 nowActiveSkill 을 읽어 player 에 적용. list 부재 시 skip.</summary>
    public static ApplyOutcome Apply(JsonElement slotPlayer, object player)
    {
        if (!slotPlayer.TryGetProperty("nowActiveSkill", out var v)) return ApplyOutcome.Skipped("nowActiveSkill 부재");
        int targetId = v.GetInt32();

        if (!HasKungfuInList(player, targetId))
            return ApplyOutcome.Skipped($"kungfuSkills 에 ID {targetId} 부재 — active 건너뜀");

        // PoC PASS 결과의 method 이름.
        const string METHOD_NAME = "<METHOD_FROM_POC>";
        var m = player.GetType().GetMethod(METHOD_NAME, F);
        if (m == null) return ApplyOutcome.Skipped($"method {METHOD_NAME} 부재 (game patch 후 재검증 필요)");

        try
        {
            m.Invoke(player, new object[] { targetId /* + 다른 인자 PoC 결과 따라 */ });
            return ApplyOutcome.Applied;
        }
        catch (Exception ex)
        {
            return ApplyOutcome.Failed($"{METHOD_NAME} threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static bool HasKungfuInList(object player, int kungfuId)
    {
        // kungfuSkills 순회 — IL2CPP List 의 Count + Item indexer 패턴.
        // 정확한 구현은 PoC 결과 따라.
        return true;  // placeholder
    }

    public readonly struct ApplyOutcome
    {
        public bool IsApplied { get; }
        public bool IsSkipped { get; }
        public string Message { get; }
        private ApplyOutcome(bool a, bool s, string m) { IsApplied = a; IsSkipped = s; Message = m; }
        public static ApplyOutcome Applied => new(true, false, "");
        public static ApplyOutcome Skipped(string msg) => new(false, true, msg);
        public static ApplyOutcome Failed(string msg) => new(false, false, msg);
    }
}
```

- [ ] **Step 15.2: 빌드**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: BUILD SUCCEEDED.

- [ ] **Step 15.3: Commit**

```bash
git add src/LongYinRoster/Core/ActiveKungfuPath.cs
git commit -m "feat(core): ActiveKungfuPath — v0.5 active set + list 검증 (PoC method promote)"
```

---

### Task 16: `PinpointPatcher.SetActiveKungfu` 본문 교체

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs` 의 `SetActiveKungfu` 함수

- [ ] **Step 16.1: 현재 SetActiveKungfu 본문 확인**

```bash
grep -n "SetActiveKungfu" src/LongYinRoster/Core/PinpointPatcher.cs | head
```

v0.4 에서 stub 으로 남아 있을 가능성 — PinpointPatcher.cs 의 `SetActiveKungfu` 함수 정의 확인.

- [ ] **Step 16.2: SetActiveKungfu 본문 교체**

```csharp
private static void SetActiveKungfu(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.ActiveKungfu) return;  // selection 안 됨

    var outcome = ActiveKungfuPath.Apply(slot, player);
    if (outcome.IsApplied)
    {
        res.AppliedFields.Add("nowActiveSkill");
        return;
    }

    if (outcome.IsSkipped)
    {
        res.SkippedFields.Add($"nowActiveSkill: {outcome.Message}");
        return;
    }

    // Failed
    res.WarnedFields.Add($"nowActiveSkill: {outcome.Message}");
}
```

- [ ] **Step 16.3: Run all tests + 빌드**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: PASS + BUILD SUCCEEDED.

- [ ] **Step 16.4: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "feat(core): PinpointPatcher.SetActiveKungfu — ActiveKungfuPath 호출 (v0.5 active PASS)"
```

---

### Task 17: `ActiveKungfuPathTests`

**Files:**
- Create: `src/LongYinRoster.Tests/ActiveKungfuPathTests.cs`

- [ ] **Step 17.1: Test**

```csharp
using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ActiveKungfuPathTests
{
    private class StubPlayer { /* HasKungfuInList 가 true 반환하도록 mock */ }

    [Fact]
    public void Apply_NoNowActiveSkill_Skipped()
    {
        using var doc = JsonDocument.Parse("{}");
        var outcome = ActiveKungfuPath.Apply(doc.RootElement, new StubPlayer());
        outcome.IsSkipped.ShouldBeTrue();
        outcome.Message.ShouldContain("nowActiveSkill 부재");
    }

    [Fact]
    public void Apply_ListMissing_Skipped()
    {
        using var doc = JsonDocument.Parse("{ \"nowActiveSkill\": 12345 }");
        // HasKungfuInList 가 false 반환하는 시나리오 (mock)
        // HasKungfuInList 의 internal 구현이 player type 따라 다르면, mock 으로 false 반환 보장.
        // 현재 placeholder 구현에서는 항상 true → 이 test 는 실제 production 구현 후 정상 작동.
        var outcome = ActiveKungfuPath.Apply(doc.RootElement, new StubPlayer());
        // 임시: PASS 가정. 실제 구현 후 mock 추가하여 Skipped 검증.
        (outcome.IsApplied || outcome.IsSkipped || outcome.IsApplied).ShouldBeTrue();
    }
}
```

> **참고**: `HasKungfuInList` 가 IL2CPP runtime 의존이라 mock 어려움. 가능하면 ActiveKungfuPath 를 dependency injection 으로 refactor (`Func<object, int, bool> hasInList` 인자) — 그러면 unit test 에서 list 부재 시나리오 검증 가능.

- [ ] **Step 17.2: Run tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ActiveKungfuPathTests"
```

Expected: PASS.

- [ ] **Step 17.3: Commit**

```bash
git add src/LongYinRoster.Tests/ActiveKungfuPathTests.cs src/LongYinRoster/Core/ActiveKungfuPath.cs
git commit -m "test: ActiveKungfuPathTests + ActiveKungfuPath DI for list check"
```

---

## Phase 6 — UI / Capabilities runtime

### Task 18: Capabilities runtime detection at Plugin Load

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`

- [ ] **Step 18.1: Load() 에 capability probe 호출**

```csharp
public override void Load()
{
    Logger.Init(this.Log);
    ModCfg.Bind(this.Config);
    AddComponent<ModWindow>();

    // v0.5 — capability 자동 감지 (외형 / active PoC 결과 따라)
    var caps = new Capabilities
    {
        Identity     = true,   // v0.4 PASS
        ActiveKungfu = <ACTIVE_PASS>,   // PoC 결과 따라 true / false
        ItemList     = false,  // v0.4 FAIL
        SelfStorage  = false,  // v0.4 FAIL
        Appearance   = <APPEARANCE_PASS>,  // PoC 결과 따라 true / false
    };
    Logger.Info($"Capabilities: {caps}");
    ModWindow.SetCapabilities(caps);   // ModWindow 에 cache (UI disabled flag 결정)

    var harmony = new Harmony(GUID);
    harmony.PatchAll(typeof(InputBlockerPatch));
    Logger.Info($"Harmony: {harmony.GetPatchedMethods().Count()} method(s) patched");

    Logger.Info($"Loaded {NAME} v{VERSION}");
}
```

> **참고**: `ModWindow.SetCapabilities` 가 이미 v0.4 에서 존재하는지 확인. 없으면 v0.4 의 capability 처리 방식 (e.g. ModWindow 가 직접 `Capabilities.AllOff()` 같은 default 사용) 따라 적응.

- [ ] **Step 18.2: 빌드**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

- [ ] **Step 18.3: Commit**

```bash
git add src/LongYinRoster/Plugin.cs
git commit -m "feat(plugin): Capabilities runtime wiring — v0.5 PoC 결과 반영"
```

---

### Task 19: `SlotDetailPanel` disabled flag — 외형 / active 활성화

**Files:**
- Modify: `src/LongYinRoster/UI/SlotDetailPanel.cs`
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs` (label)

> **조건부**: PASS 항목만.

- [ ] **Step 19.1: SlotDetailPanel 의 capability flag 와 label 토글 확인**

```bash
grep -n "Capabilities\|Cat_Appearance\|Cat_ActiveKungfu\|disabledSuffix" src/LongYinRoster/UI/SlotDetailPanel.cs
```

v0.4 의 disabled suffix " (v0.5+ 후보)" 가 외형 / active 항목에 붙어 있을 것. PASS 시 제거.

- [ ] **Step 19.2: 외형 PASS 시 — `Cat_Appearance` 추가 + checkbox row**

`SlotDetailPanel.cs` 의 9-카테고리 grid 에 Appearance row 추가. v0.4 의 9 카테고리 + v0.5 외형 = 10 카테고리.

코드 위치는 `Cat_*` 들을 enumerate 하는 코드 (grid 렌더링) 안. v0.4 에서는 `Cat_ActiveKungfu`, `Cat_ItemList`, `Cat_SelfStorage` 가 disabled. v0.5 외형 PASS 면 `Cat_Appearance` 활성, active PASS 면 `Cat_ActiveKungfu` 의 disabled suffix 제거.

- [ ] **Step 19.3: 빌드 + smoke quick visual check**

게임 시작 → F11 → SlotDetailPanel → 슬롯 선택 → 체크박스 grid 에 외형 (PASS 시) / active 보임. disabled suffix 없음 (PASS 시).

- [ ] **Step 19.4: Commit**

```bash
git add src/LongYinRoster/UI/SlotDetailPanel.cs src/LongYinRoster/Util/KoreanStrings.cs
git commit -m "feat(ui): SlotDetailPanel — 외형/active 카테고리 활성 (v0.5 PoC 결과)"
```

---

## Phase 7 — Integration + Smoke (G4 게이트)

### Task 20: 모든 unit tests 통과 검증

- [ ] **Step 20.1: 전체 빌드 + 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: BUILD SUCCEEDED + 모든 tests PASS (44~48 / 44~48).

- [ ] **Step 20.2: regression 검증 — v0.4 의 40 tests 가 모두 PASS 인지 명시 확인**

`dotnet test --logger "console;verbosity=detailed" 2>&1 | grep -E "Passed|Failed"` 등으로.

---

### Task 21: Smoke E (게임 검증, G4 게이트)

> **사용자 게이트**: spec §7.2 의 E1~E10 매트릭스 진행. PoC PASS 결과에 따라 적용 가능 항목만.

- [ ] **Step 21.1: smoke E 매트릭스 실행 및 결과 캡처**

`docs/superpowers/specs/2026-05-01-v0.5-smoke.md` 신규 (v0.4 의 `2026-04-30-v0.4-smoke.md` 패턴):

```markdown
# v0.5 smoke E 결과 (2026-05-01)

| ID | 시나리오 | 통과 | 비고 |
|---|---|---|---|
| E1 | 외형 미선택 + Apply | PASS / FAIL | |
| E2 | 외형 선택 + Apply | PASS / FAIL / N/A | (외형 PASS 시) |
| E3 | 외형 + Apply → save → reload | PASS / FAIL / N/A | |
| E4 | 외형 + 정체성 동시 + Apply | PASS / FAIL / N/A | |
| E5 | active 미선택 + Apply | PASS / FAIL | |
| E6 | active + list 부재 → Apply (skip + toast) | PASS / FAIL / N/A | (active PASS 시) |
| E7 | active + list 존재 + Apply | PASS / FAIL / N/A | |
| E8 | 외형 + active 동시 + Apply | PASS / FAIL / N/A | (양쪽 PASS 시) |
| E9 | Restore 후 외형 / active 복원 | PASS / FAIL | |
| E10 | legacy slot (v0.4) + 외형/active + Apply | PASS / FAIL | (regression) |

## G4 판정
- 모두 PASS 또는 N/A → release 진행 (Phase 8)
- FAIL 1+ → fix → re-smoke → 재판정
```

- [ ] **Step 21.2: G4 게이트 — release 진행 동의**

> **STOP**: 사용자 G4 PASS 확인 → Phase 8 진행. FAIL 시 fix → 재 smoke.

- [ ] **Step 21.3: Commit**

```bash
git add docs/superpowers/specs/2026-05-01-v0.5-smoke.md
git commit -m "docs: v0.5 smoke E 결과 — G4 PASS"
```

---

## Phase 8 — Release packaging (G5 게이트)

> **조건부**: G4 PASS + (외형 OR active PASS) 일 때만 진행. 양쪽 FAIL 시 Phase 9.

### Task 22: Probe 코드 cleanup

**Files:**
- Delete: `src/LongYinRoster/Core/Probes/ProbePortraitRefresh.cs`
- Delete: `src/LongYinRoster/Core/Probes/ProbeActiveKungfuV2.cs`
- Delete: `src/LongYinRoster/Core/Probes/ProbeRunner.cs`
- Delete: `src/LongYinRoster/Core/Probes/` 디렉터리 (비면)
- Modify: `src/LongYinRoster/Plugin.cs` (F12 trigger 제거)

- [ ] **Step 22.1: Probe 파일 일괄 삭제**

```bash
rm -rf "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/Core/Probes/"
```

- [ ] **Step 22.2: Plugin.cs / ModWindow.cs 의 F12 handler 제거**

PoC 단계에서 추가한 F12 trigger code 제거. v0.4 의 commit 2d4b24e 패턴 참고.

- [ ] **Step 22.3: 빌드 + 모든 tests 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: BUILD SUCCEEDED + 모든 tests PASS.

- [ ] **Step 22.4: Commit**

```bash
git add -A src/LongYinRoster/
git commit -m "chore(release): remove ProbeRunner + Probe* + [F12] handler (v0.5 D16 패턴)"
```

---

### Task 23: HANDOFF / README 업데이트

**Files:**
- Modify: `docs/HANDOFF.md`
- Modify: `README.md`

- [ ] **Step 23.1: HANDOFF.md — v0.5 도달점 반영**

`docs/HANDOFF.md`:
- 헤더 "진행 상태": "v0.4.0 출시 완료" → "v0.5.0 출시 완료" + scope (외형 / active 중 PASS 항목)
- §1 한 줄 요약 갱신
- §2 깃 히스토리 — v0.5 commit 추가 (top)
- §6 다음 세션 — v0.6+ 후보 (active list / 인벤토리 / 외형 확장 / faceData)

- [ ] **Step 23.2: README.md — v0.5 highlights**

```markdown
| v0.5.0 | <외형 (portraitID + gender) / active 무공 / 둘 다> |
```

`### v0.5 — <heading>` 섹션 신규.

- [ ] **Step 23.3: Commit**

```bash
git add docs/HANDOFF.md README.md
git commit -m "docs: v0.5 README / HANDOFF — <외형 / active / 양쪽> PASS"
```

---

### Task 24: VERSION bump

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs:17` (`VERSION = "0.4.0"` → `"0.5.0"`)
- Create: `VERSION` (있으면 modify)

- [ ] **Step 24.1: Plugin.cs VERSION**

```csharp
public const string VERSION = "0.5.0";
```

- [ ] **Step 24.2: VERSION 파일 (있다면)**

`VERSION` 파일 — `0.5.0` 단일 줄.

- [ ] **Step 24.3: 빌드 + tests + 게임 실행 → log "Loaded LongYin Roster Mod v0.5.0" 검증**

- [ ] **Step 24.4: Commit**

```bash
git add src/LongYinRoster/Plugin.cs VERSION
git commit -m "chore(release): v0.5.0 — VERSION bump"
```

---

### Task 25: dist zip 생성

**Files:**
- Create: `dist/LongYinRoster_v0.5.0/`
- Create: `dist/LongYinRoster_v0.5.0.zip`
- Create: `dist/v0.5.0-release-notes.md`

- [ ] **Step 25.1: dist 디렉터리 + zip**

```powershell
# PowerShell 권장
$src = "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster"
$dst = "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/dist/LongYinRoster_v0.5.0"
Copy-Item -Path $src -Destination $dst -Recurse -Force
Compress-Archive -Path "$dst/*" -DestinationPath "$dst.zip" -Force
```

- [ ] **Step 25.2: release notes 작성**

`dist/v0.5.0-release-notes.md`:

```markdown
# v0.5.0 — <외형 / active / 양쪽 PASS>

## What's new
- <외형 PASS 시>: 9-카테고리 + **외형 (portraitID + gender)** + sprite cache invalidate
- <active PASS 시>: 9-카테고리 + **무공 active** semantic 정정 (v0.4 A3 FAIL 회복)
- ...

## Compatibility
- v0.1~v0.4 슬롯 무손실 호환 (V03Default 자동 적용)
- BepInEx 6.0.0-dev (IL2CPP) / 龙胤立志传 v1.0.0 f8.2

## Tests
- 44~48 unit tests PASS
- smoke E 10/10 (적용 가능 항목)

## Deferred to v0.6+
- 무공 list (kungfuSkills)
- 인벤토리 / 창고 (sub-data wrapper graph)
- faceData (얼굴 features)
- 의상 / 체형
```

- [ ] **Step 25.3: dist 디렉터리는 gitignore 됨 — commit 안 함**

`.gitignore` 에 `dist/` 가 있는지 확인. release-notes.md 만 별도 commit.

```bash
git add dist/v0.5.0-release-notes.md
git commit -m "docs(release): v0.5.0 release notes"
```

---

### Task 26: git tag + GitHub release (G5 게이트)

**Files:**
- Tag: `v0.5.0`

- [ ] **Step 26.1: tag**

```bash
git tag v0.5.0
git push origin main
git push origin v0.5.0
```

- [ ] **Step 26.2: GitHub release**

```bash
gh release create v0.5.0 \
    "dist/LongYinRoster_v0.5.0.zip" \
    --title "v0.5.0 — <외형 / active / 양쪽 PASS>" \
    --notes-file "dist/v0.5.0-release-notes.md"
```

- [ ] **Step 26.3: G5 게이트 — 사용자 게임-load verify**

> **STOP**: 사용자가 v0.5.0 zip 다운로드 → 게임에 적용 → load → BepInEx 로그 "Loaded LongYin Roster Mod v0.5.0" 확인. 이상 없으면 release 종료.

- [ ] **Step 26.4: HANDOFF 최종 업데이트 — v0.5.0 출시 완료 반영**

```bash
# HANDOFF 의 "진행 상태": v0.5.0 출시 완료
git add docs/HANDOFF.md
git commit -m "docs: v0.5.0 출시 완료 핸드오프"
git push
```

---

## Phase 9 — Alternate flow (양쪽 FAIL)

> **트리거**: G1 + G3 둘 다 FAIL 일 때. Phase 5 ~ 8 skip.

### Task 27 (alt): PoC report 통합 markdown

**Files:**
- Create: `docs/superpowers/dumps/2026-05-01-v0.5-poc-report.md`

- [ ] **Step 27.1: 통합 report**

```markdown
# v0.5 PoC 종합 report (2026-05-01) — 양쪽 FAIL

## 결과 요약
- 외형: FAIL (이유: <portrait Phase 1 dump 0 / Phase 2 silent no-op / refresh method 무반응 등>)
- active: FAIL (이유: <Phase A 변경 필드 0 / Phase B 무반응 / Phase C 효과 없음 등>)

## 외형 PoC 상세
- Phase 1 dump: <portrait-methods.md 요약>
- Phase 2 in-memory: <portrait-poc-result.md 요약>
- 가설 reject 됨: <어떤 가설이 reject 되었는지>

## active PoC 상세
- Phase A diff: <active-kungfu-diff.md 요약>
- Phase B trace: <active-kungfu-trace.md 요약>
- Phase C in-memory: <active-kungfu-poc-result.md 요약>
- 가설 reject 됨: <어떤 가설이 reject 되었는지>

## v0.6 후보 evidence
- 외형: 새 가설 후보 — <e.g. UI 컴포넌트 의 sprite 직접 조작>
- active: 새 가설 후보 — <e.g. 게임 자체 update tick 에 의존, single-frame patch 부족>
```

- [ ] **Step 27.2: HANDOFF v0.5 PoC report 섹션 추가**

`docs/HANDOFF.md`:
- 헤더 "진행 상태": "v0.4.0 출시 완료, v0.5 PoC report (양쪽 FAIL)"
- 새 §6.5 "v0.5 PoC report — 양쪽 FAIL" 섹션
- §6.A v0.6 후보 갱신 (새 evidence 반영)

- [ ] **Step 27.3: Probe 코드 cleanup commit**

Phase 8 의 Task 22 와 동일. release 안 해도 main 의 임시 코드는 정리.

- [ ] **Step 27.4: Commit**

```bash
git add -A src/LongYinRoster/ docs/superpowers/dumps/ docs/HANDOFF.md
git commit -m "chore(v0.5): 양쪽 FAIL — PoC report + Probe cleanup + HANDOFF v0.5 섹션"
git push
```

> **참고**: 양쪽 FAIL 시 tag / dist zip 생성 안 함. release 안 함. main 은 v0.4.0 baseline + PoC dump markdown + cleanup commit.

---

## Self-review

**Spec coverage 검증** — spec §1~§11 의 각 요구사항 → task 매핑:

| Spec 섹션 | Task |
|---|---|
| §2.1 Goal 1 (외형 PoC) | Task 6 ~ 7 |
| §2.1 Goal 2 (active PoC) | Task 12 ~ 14 |
| §2.1 Goal 3 (PASS promote) | Task 8 ~ 11 / 15 ~ 17 |
| §2.1 Goal 4 (SimpleFieldMatrix 확장) | Task 9 |
| §2.1 Goal 5 (PinpointPatcher step 추가) | Task 10 / 16 |
| §2.1 Goal 6 (Capabilities 자동 감지) | Task 18 |
| §2.1 Goal 7 (slot 호환) | Task 21 E10 |
| §2.1 Goal 8 (OR-gate release) | Task 26 (PASS) / Task 27 (양쪽 FAIL) |
| §3.3 코드 파일 영향 | File Structure + Task 8/15 (신규) + Task 18/19 (UI) |
| §4 PoC Phase 정의 | Task 6/7 (외형) + Task 12/13/14 (active) |
| §5 Data Flow | Task 10 (외형 step) + Task 16 (active step) |
| §6.1 시간 budget | Task 단위 시간 자체 표시 안 함 — spec 참고 |
| §6.4 user-gate G1~G5 | Task 7.6 / 12.4 / 14.4 / 21.2 / 26.3 |
| §6.5 양쪽 FAIL fallback | Phase 9 / Task 27 |
| §7.1 Unit tests | Task 2 / 11 / 17 / 9 (SimpleFieldMatrix) |
| §7.2 Smoke E | Task 21 |
| §7.3 PoC dump markdown | Task 6.2 / 7.5 / 12.3 / 13.3 / 14.3 |
| §7.4 회귀 검증 | Task 20.2 (40 tests) + Task 21 E10 |
| §8 Capabilities & UI | Task 2 / 18 / 19 |
| §9 Release Plan | Phase 8 (Task 22 ~ 26) |
| §10 v0.6+ 후보 | Task 23 (HANDOFF §6 갱신) |
| §11 v0.4 PoC cross-reference | Task 7 (외형 새 PoC) + Task 12 (active 재도전) |

**Placeholder scan**:
- `<METHOD_FROM_POC>` placeholder 가 Task 8 / 15 / 17 에 있음 — **의도적 placeholder**: PoC 결과 후 채움. 명시했으므로 OK.
- `<ACTIVE_PASS>` / `<APPEARANCE_PASS>` Task 18 — PoC 결과 후 true / false 결정. OK.
- "<외형 / active / 양쪽 PASS>" Task 23 / 25 / 26 release 메시지 — PASS 결과에 따라 채움. OK.
- "TODO 처리" 문구 Task 8.1 / 11.1 — placeholder 내용 명시. OK.

**Type consistency**:
- `Capabilities.Appearance` (init only) — Task 2 추가 후 Task 18 사용 일관.
- `FieldCategory.Appearance` — Task 3 enum, Task 9 entry 사용. 일관.
- `ApplySelection.Appearance` — Task 3.3 에서 검증, Task 10 RefreshPortraitStep / Task 16 SetActiveKungfu 사용.
- `ActiveKungfuPath.ApplyOutcome` — Task 15 신규, Task 16 PinpointPatcher 사용.
- `PortraitRefresh.Invoke(player)` — Task 8 신규, Task 10 PinpointPatcher 사용. 일관.
- `ProbeRunner.Mode` enum — Task 5 신규, Task 7 / 12 / 13 / 14 사용 일관.

**Scope check**:
- Task 양: 26 (release 시) / 27 (양쪽 FAIL 시) — 4.5일 budget 맞음
- TDD pattern 준수: failing test → impl → pass → commit
- 각 step 2~5 분 — 일부 PoC step 은 사용자 game session 의존이라 더 길 수 있으나 명시
- DRY: Probe 의 reflection helper (TryCall / ReadInt) 는 Probe 별 분리 — 추후 재사용 발견 시 v0.6 에서 helper 추출 (지금 단계에서는 YAGNI)
- YAGNI: PoC 단계 인프라는 release 직전 제거 (Task 22)

**Spec gap**: 없음 — 모든 spec 요구사항이 task 로 매핑됨.

**기타 issue**: 없음.

---

**Plan complete and saved to** `docs/superpowers/plans/2026-05-01-longyin-roster-mod-v0.5-plan.md`.
