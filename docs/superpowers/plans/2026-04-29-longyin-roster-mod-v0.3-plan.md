# LongYin Roster Mod v0.3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply (slot → game) 와 Restore (slot 0 → game) 흐름을 PinpointPatcher 패턴으로 구현. JSON deserialize 대신 game-self method (`SetX`/`ChangeX`/`AddX`/`RefreshX`) 호출로 필드 단위 정밀 복사.

**Architecture:** 4-layer (UI / Orchestration / Patch / Discovery). 7-step pipeline (`SetSimpleFields → RebuildKungfuSkills → RebuildItemList → RebuildSelfStorage → RebuildHeroTagData → RefreshSelfState → RefreshExternalManagers`). 첫 task = `[F12]` 임시 핸들러로 HeroData method dump → spec 매트릭스 보강 round → 본 구현. Apply 실패 시 슬롯 0 자동백업으로 자동복원. game-state escape hatch (`GameDataController.Save/Load`) 거부. `dotnet test` 18 → 24 (PinpointPatcher framework 6 신규).

**Tech Stack:** C# 11 / .NET 6 / BepInEx 6 IL2CPP-CoreCLR / Il2CppInterop / Newtonsoft.Json (IL2CPP-bound, Serialize 단방향만) / System.Text.Json (slot JSON 파싱) / Harmony / xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md`
**선행 spec:** `docs/superpowers/specs/2026-04-27-longyin-roster-mod-design.md` (v0.1 base, 변경 없음)
**선행 plan:** `docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md` (v0.1~v0.2)

---

## Pre-flight

### Task 0: 현재 빌드/테스트 상태 검증

**Files:** (read-only)

- [ ] **Step 1: 게임 닫혔는지 확인**

Run:
```bash
tasklist | grep -i LongYinLiZhiZhuan
```
Expected: 빈 출력 (게임 process 없음). 출력 있으면 게임 종료 후 재시도.

- [ ] **Step 2: 현재 18 unit tests pass 확인**

Run:
```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Passed: 18, Failed: 0`. 18개 미만이거나 실패 있으면 v0.2 마지막 commit (`8c89fe4`) 으로 reset 필요 — 사용자 지시 받기.

- [ ] **Step 3: Release 빌드 성공 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. dll 이 `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 에 배포됐는지 확인.

- [ ] **Step 4: 작업 브랜치 생성**

Run:
```bash
git checkout -b v0.3
git status
```
Expected: `On branch v0.3` + `nothing to commit, working tree clean`.

---

## Phase 1 — Discovery

### Task 1: HeroDataDump 임시 도구 + [F12] 핸들러 + Config kill switch

**Files:**
- Create: `src/LongYinRoster/Core/HeroDataDump.cs`
- Modify: `src/LongYinRoster/Config.cs` (lines 18~19, 41~44)
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (Update 메서드 추가)

- [ ] **Step 1: Config.cs 수정 — `AllowApplyToGame` 신규 + `RunPinpointPatchOnApply` 제거**

`src/LongYinRoster/Config.cs` 의 line 18~19 (필드 선언) 를 다음으로 교체:
```csharp
    public static ConfigEntry<bool>    AutoBackupBeforeApply   = null!;
    public static ConfigEntry<bool>    AllowApplyToGame        = null!;
```

`src/LongYinRoster/Config.cs` 의 line 41~44 (Bind 호출) 를 다음으로 교체:
```csharp
        AutoBackupBeforeApply = cfg.Bind("Behavior", "AutoBackupBeforeApply", true,
                                         "덮어쓰기 직전 슬롯 0에 자동 저장 (실패 시 자동복원의 source)");
        AllowApplyToGame      = cfg.Bind("Behavior", "AllowApplyToGame",      true,
                                         "Apply 자체 kill switch. dump phase 에서 false 권장");
```

- [ ] **Step 2: HeroDataDump.cs 생성**

`src/LongYinRoster/Core/HeroDataDump.cs`:
```csharp
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// 임시 진단 도구 — v0.3 release 전 제거. plan Task 21 에서 파일과 [F12] 핸들러 모두 삭제.
///
/// 게임 안에서 reflection 으로 HeroData 의 모든 method/property/field 를 BepInEx 로그에
/// dump 하고, Hero 관련 Refresh API 를 가진 매니저 후보를 enumerate. 그 결과로
/// docs/HeroData-methods.md 를 작성하고 spec §7.2 매트릭스를 보강한다.
/// </summary>
public static class HeroDataDump
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void DumpToLog()
    {
        Logger.Info("============================== HeroDataDump.start ==============================");
        var player = HeroLocator.GetPlayer();
        if (player == null)
        {
            Logger.Warn("HeroDataDump: player null. Game 진입 후 다시 [F12] 누르세요.");
            return;
        }
        var heroType = player.GetType();
        Logger.Info($"HeroDataDump: heroType = {heroType.AssemblyQualifiedName}");

        DumpHeroSelf(heroType);
        DumpManagerCandidates(heroType);
        Logger.Info("============================== HeroDataDump.end ================================");
    }

    private static void DumpHeroSelf(Type heroType)
    {
        foreach (var m in heroType.GetMethods(F).OrderBy(m => m.Name))
        {
            var pars = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
            Logger.Info($"HeroDataDump.self.method: {m.ReturnType.Name} {m.Name}({pars})");
        }
        foreach (var p in heroType.GetProperties(F).OrderBy(p => p.Name))
            Logger.Info($"HeroDataDump.self.prop: {p.PropertyType.Name} {p.Name} {{ get={p.CanRead}, set={p.CanWrite} }}");
        foreach (var f in heroType.GetFields(F).OrderBy(f => f.Name))
            Logger.Info($"HeroDataDump.self.field: {f.FieldType.Name} {f.Name}");
    }

    private static void DumpManagerCandidates(Type heroType)
    {
        var rx = new Regex("^(Refresh|Update|OnHero|Rebuild)");
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        foreach (var t in SafeGetTypes(asm))
        {
            if (t == null) continue;
            if (!t.Name.EndsWith("Manager") && !t.Name.EndsWith("Controller")) continue;
            if (t.Namespace != null && t.Namespace.StartsWith("LongYinRoster")) continue;
            foreach (var m in t.GetMethods(F))
            {
                if (!rx.IsMatch(m.Name)) continue;
                var pars = m.GetParameters();
                bool acceptsHero = pars.Any(p =>
                    p.ParameterType == heroType ||
                    p.ParameterType.Name == "HeroData");
                if (!acceptsHero) continue;
                var sig = string.Join(",", pars.Select(p => p.ParameterType.Name));
                Logger.Info($"HeroDataDump.mgr: {t.FullName}.{m.Name}({sig})");
            }
        }
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
    }
}
```

- [ ] **Step 3: ModWindow.cs 의 Update 메서드에 [F12] 핸들러 추가**

`src/LongYinRoster/UI/ModWindow.cs` 의 `Update` 메서드 (line 298~307) 를 다음으로 교체:
```csharp
    private void Update()
    {
        if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

        // [F12] HeroDataDump trigger — v0.3 plan Task 1 임시 핸들러. plan Task 21 에서 제거.
        if (Input.GetKeyDown(KeyCode.F12)) Core.HeroDataDump.DumpToLog();

        if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
            Time.timeScale = 0f;
    }
```

- [ ] **Step 4: 빌드**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: 18 unit tests pass 확인 (regression 안 났는지)**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Passed: 18, Failed: 0`.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Core/HeroDataDump.cs src/LongYinRoster/Config.cs src/LongYinRoster/UI/ModWindow.cs
git commit -m "$(cat <<'EOF'
feat(core): HeroDataDump temp tool + [F12] handler + AllowApplyToGame config

v0.3 plan Task 1: HeroData / Hero-related manager method dump 를 위한 임시 진단
도구. plan Task 21 에서 파일과 [F12] 핸들러 모두 제거된다. AllowApplyToGame
config 도 추가 (kill switch — dump phase 에서 false 로 두면 안전).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: 게임 안 dump 실행 + spec 매트릭스 보강 round

**Files:**
- Create: `docs/HeroData-methods.md` (dump 산출물)
- Modify: `docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md` (§7.2 보강)

- [ ] **Step 1: BepInEx 로그 클리어 + 게임 시작**

```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

게임 실행 + 캐릭터 진입 (heroID==0 의 player 가 활성). 사용자가 직접 게임 시작.

- [ ] **Step 2: [F12] 누르고 dump 출력**

게임 안에서 `[F12]` 1회 누름. BepInEx 로그에 `HeroDataDump.start` ~ `HeroDataDump.end` 출력 확인.

Run:
```bash
grep -n "HeroDataDump" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | head -50
```
Expected: `HeroDataDump.start`, `HeroDataDump.self.method:` 다수, `HeroDataDump.mgr:` 다수, `HeroDataDump.end`.

- [ ] **Step 3: smoke A1/A2/A3 검증**

| 항목 | 확인 |
|---|---|
| A1 | `HeroDataDump.start` ~ `end` 사이 method list 출력됨 |
| A2 | `HeroDataDump.self.method:` 출력에 `RefreshMaxAttriAndSkill`, `GetMaxAttri`, `GetMaxFightSkill`, `GetMaxLivingSkill`, `GetMaxFavor`, `GetFinalTravelSpeed` 중 1개+ 발견 |
| A3 | `HeroDataDump.mgr:` 라인 1개+ 출력 (`RefreshHero` / `UpdateHero` / `OnHeroChanged` / `RebuildHero` 같은 method 시그니처) |

3개 모두 통과 안 하면 Task 1 의 dump 코드 점검 (HeroLocator 가 player 못 찾았으면 log 의 `HeroDataDump: player null` 메시지). 통과 시 다음 step.

- [ ] **Step 4: docs/HeroData-methods.md 작성**

로그의 `HeroDataDump.*` 라인을 `docs/HeroData-methods.md` 에 정리:
```markdown
# HeroData / Hero-related Manager Method Dump

**Source**: `BepInEx/LogOutput.log`, `[F12]` 핸들러 (v0.3 plan Task 2)
**Date**: 2026-04-29
**Game version**: 1.0.0 f8.2

## HeroData self — methods

(BepInEx 로그의 `HeroDataDump.self.method:` 라인 모두 복사. 한 method 당 한 줄.)

## HeroData self — properties

(`HeroDataDump.self.prop:` 라인 복사.)

## HeroData self — fields

(`HeroDataDump.self.field:` 라인 복사.)

## Hero-related Manager candidates

(`HeroDataDump.mgr:` 라인 복사.)
```

실제 로그 내용을 위 4개 섹션에 채워서 작성.

- [ ] **Step 5: spec §7.2 매트릭스 보강**

`docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md` §7.2 의 표를 dump 결과로 update:

각 행의 "가설" 컬럼을 **🟢 dump-evidenced** / **🟡 dump-evidenced (best-effort)** / **⚪ method 미발견 (v0.4 후보)** / **⛔** 중 하나로 변경.

추가로 §7.2 표 아래에 **"§7.2.1 method 매핑 (dump 후 확정)"** 신규 sub-section 추가:
```markdown
### 7.2.1 method 매핑 (dump 후 확정)

PinpointPatcher 의 각 step 이 호출하는 game-self method 매핑.

**Step 1 SetSimpleFields**:
- `heroName` → (dump 결과의 SetterMethod 또는 Property setter — 없으면 ⚪)
- `fame` → (...)
- `fightScore` → 🟢 derived (RefreshMaxAttriAndSkill 가 재계산)
- `hp`, `maxhp` → (...)
- `baseAttri.*` → (...)
- (각 simple field 마다 한 줄)

**Step 2 RebuildKungfuSkills**:
- Clear: `IL2CppListOps.Clear(player.kungfuSkills)` (raw)
- Add: `(dump 결과의 AddKungfuSkill 시그니처)`

**Step 3~5 Rebuild...** (동일 패턴)

**Step 6 RefreshSelfState** (fatal):
- `RefreshMaxAttriAndSkill()`
- `GetMaxAttri()`
- (dump 결과의 Refresh/Recalc method 들)

**Step 7 RefreshExternalManagers**:
- `XxxManager.Instance.RefreshHero(player)` (dump 의 manager 후보)
```

각 항목을 dump 결과로 채우기. method 명이 일치하지 않으면 ⚪ 또는 v0.4 후보로 분류 + Open Questions 갱신.

- [ ] **Step 6: 사용자 게이트 — 보강된 매트릭스 review**

작업자는 사용자에게 알림:
> "Spec §7.2 + §7.2.1 을 dump 결과로 보강했습니다. `docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md` 검토 후 OK 하시면 Task 3 (foundation classes) 부터 진행합니다. 매트릭스에서 ⚪ 또는 v0.4 분류된 필드가 너무 많으면 spec/plan 재조정."

사용자 OK 받기 전에는 Task 3 진입 금지.

- [ ] **Step 7: Commit (보강된 spec + dump 산출물)**

```bash
git add docs/HeroData-methods.md docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md
git commit -m "$(cat <<'EOF'
docs: HeroData method dump + spec v0.3 support matrix refined per dump

v0.3 plan Task 2: 게임 안 [F12] HeroDataDump 결과로 docs/HeroData-methods.md
작성, spec §7.2 매트릭스를 dump-evidenced 로 갱신, §7.2.1 method 매핑 sub-section
추가. 사용자 승인 후 Task 3~ 진입.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 2 — Foundation classes (TDD, IL2CPP 의존 없음)

### Task 3: ApplyResult POCO + tests

**Files:**
- Create: `src/LongYinRoster/Core/ApplyResult.cs`
- Create: `src/LongYinRoster.Tests/PinpointPatcherTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` (Compile Include 추가)

- [ ] **Step 1: 실패하는 테스트 작성**

`src/LongYinRoster.Tests/PinpointPatcherTests.cs`:
```csharp
using System;
using FluentAssertions;
using LongYinRoster.Core;
using Xunit;

namespace LongYinRoster.Tests;

public class ApplyResultTests
{
    [Fact]
    public void ApplyResult_StartsEmpty()
    {
        var r = new ApplyResult();
        r.AppliedFields.Should().BeEmpty();
        r.SkippedFields.Should().BeEmpty();
        r.WarnedFields.Should().BeEmpty();
        r.StepErrors.Should().BeEmpty();
        r.HasFatalError.Should().BeFalse();
    }

    [Fact]
    public void ApplyResult_TracksAppliedSkippedWarned()
    {
        var r = new ApplyResult();
        r.AppliedFields.Add("heroName");
        r.SkippedFields.Add("portraitID — no setter mapped");
        r.WarnedFields.Add("hp — InvalidCastException");
        r.StepErrors.Add(new InvalidOperationException("step6 throw"));
        r.HasFatalError = true;

        r.AppliedFields.Should().ContainSingle().Which.Should().Be("heroName");
        r.SkippedFields.Should().ContainSingle();
        r.WarnedFields.Should().ContainSingle();
        r.StepErrors.Should().ContainSingle();
        r.HasFatalError.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Compile Include 를 Tests.csproj 에 추가**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 의 `<ItemGroup>` (line 33~61) 안에 추가:
```xml
    <Compile Include="../LongYinRoster/Core/ApplyResult.cs">
      <Link>Core/ApplyResult.cs</Link>
    </Compile>
```

`<Compile Include="../LongYinRoster/Util/PathProvider.cs">` 바로 다음 줄에 추가.

- [ ] **Step 3: 테스트 실행 → fail 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ApplyResultTests"
```
Expected: 컴파일 에러 — `'ApplyResult' could not be found`. Compile Include 가 빈 .cs 를 가리키지만 파일이 아직 없음.

- [ ] **Step 4: ApplyResult.cs 구현**

`src/LongYinRoster/Core/ApplyResult.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace LongYinRoster.Core;

/// <summary>
/// PinpointPatcher.Apply 의 결과 누적. step 단위로 채워지고 상위 (DoApply) 가 토스트
/// 매핑 + 자동복원 결정에 사용.
/// </summary>
public sealed class ApplyResult
{
    public List<string>    AppliedFields { get; } = new();
    public List<string>    SkippedFields { get; } = new();
    public List<string>    WarnedFields  { get; } = new();
    public List<Exception> StepErrors    { get; } = new();
    public bool HasFatalError { get; set; }
}
```

- [ ] **Step 5: 테스트 실행 → pass 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ApplyResultTests"
```
Expected: `Passed: 2, Failed: 0`.

- [ ] **Step 6: 전체 테스트 실행 → regression 없음 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Passed: 20, Failed: 0` (기존 18 + 신규 2).

- [ ] **Step 7: Commit**

```bash
git add src/LongYinRoster/Core/ApplyResult.cs src/LongYinRoster.Tests/PinpointPatcherTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "$(cat <<'EOF'
feat(core): ApplyResult POCO + 2 unit tests

v0.3 plan Task 3: PinpointPatcher.Apply 의 결과 누적 컨테이너. AppliedFields /
SkippedFields / WarnedFields / StepErrors / HasFatalError. IL2CPP 의존 없는 POCO.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: IL2CppListOps reflection helper + tests

**Files:**
- Create: `src/LongYinRoster/Core/IL2CppListOps.cs`
- Modify: `src/LongYinRoster.Tests/PinpointPatcherTests.cs` (3 tests 추가)
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` (Compile Include 추가)

- [ ] **Step 1: 3개 실패하는 테스트 추가**

`src/LongYinRoster.Tests/PinpointPatcherTests.cs` 끝에 추가:
```csharp
public class IL2CppListOpsTests
{
    [Fact]
    public void Count_ReturnsItemCount_ForStandardList()
    {
        var list = new System.Collections.Generic.List<int> { 10, 20, 30 };
        IL2CppListOps.Count(list).Should().Be(3);
    }

    [Fact]
    public void Get_ReturnsItemAt_ForStandardList()
    {
        var list = new System.Collections.Generic.List<string> { "a", "b", "c" };
        IL2CppListOps.Get(list, 1).Should().Be("b");
    }

    [Fact]
    public void Clear_ClearsStandardList()
    {
        var list = new System.Collections.Generic.List<int> { 1, 2, 3 };
        IL2CppListOps.Clear(list);
        list.Count.Should().Be(0);
    }
}
```

(IL2CppListOps 의 reflection 패턴은 표준 .NET `List<T>` 도 같은 모양 — `Count` property + `Item` indexer + `Clear()` method — 이라 .NET list 로 unit test 가능. 진짜 IL2CPP list 검증은 smoke check 단계.)

- [ ] **Step 2: Compile Include 추가**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 의 ApplyResult Include 다음에 추가:
```xml
    <Compile Include="../LongYinRoster/Core/IL2CppListOps.cs">
      <Link>Core/IL2CppListOps.cs</Link>
    </Compile>
```

- [ ] **Step 3: 테스트 fail 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~IL2CppListOpsTests"
```
Expected: 컴파일 에러 — `'IL2CppListOps' could not be found`.

- [ ] **Step 4: IL2CppListOps.cs 구현**

`src/LongYinRoster/Core/IL2CppListOps.cs`:
```csharp
using System;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// Il2CppSystem.Collections.Generic.List&lt;T&gt; 가 .NET IEnumerable 을 구현하지 않아
/// foreach 가 안 되는 환경 대응. reflection 으로 Count property, Item indexer (또는
/// get_Item(int) method), Clear method 를 호출. 표준 .NET List&lt;T&gt; 도 동일 모양이므로
/// 단위 테스트 가능 (실제 IL2CPP list 는 smoke check 로 검증).
/// </summary>
public static class IL2CppListOps
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static int Count(object il2List)
    {
        if (il2List == null) throw new ArgumentNullException(nameof(il2List));
        var prop = il2List.GetType().GetProperty("Count", F)
            ?? throw new InvalidOperationException(
                $"IL2CppListOps.Count: type {il2List.GetType().FullName} has no Count property");
        return Convert.ToInt32(prop.GetValue(il2List));
    }

    public static object? Get(object il2List, int index)
    {
        if (il2List == null) throw new ArgumentNullException(nameof(il2List));
        var t = il2List.GetType();
        var itemProp = t.GetProperty("Item", F);
        if (itemProp != null) return itemProp.GetValue(il2List, new object[] { index });
        var getItem = t.GetMethod("get_Item", F, null, new[] { typeof(int) }, null);
        if (getItem != null) return getItem.Invoke(il2List, new object[] { index });
        throw new InvalidOperationException(
            $"IL2CppListOps.Get: type {t.FullName} has no Item indexer / get_Item(int)");
    }

    public static void Clear(object il2List)
    {
        if (il2List == null) throw new ArgumentNullException(nameof(il2List));
        var t = il2List.GetType();
        var clear = t.GetMethod("Clear", F, null, Type.EmptyTypes, null)
            ?? t.GetMethod("clear", F, null, Type.EmptyTypes, null);
        if (clear == null)
            throw new InvalidOperationException(
                $"IL2CppListOps.Clear: type {t.FullName} has no Clear() method");
        clear.Invoke(il2List, null);
    }
}
```

- [ ] **Step 5: 테스트 pass 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~IL2CppListOpsTests"
```
Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 6: 전체 테스트 → 23 pass**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Passed: 23, Failed: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/LongYinRoster/Core/IL2CppListOps.cs src/LongYinRoster.Tests/PinpointPatcherTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "$(cat <<'EOF'
feat(core): IL2CppListOps reflection helpers + 3 unit tests

v0.3 plan Task 4: Il2CppSystem.Collections.Generic.List<T> 가 .NET IEnumerable
을 구현 안 해서 foreach 불가. reflection 으로 Count / Item indexer / Clear 호출.
표준 .NET List<T> 도 같은 모양이라 unit test 가능 (실제 IL2CPP 는 smoke).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: SimpleFieldMatrix static readonly + schema test

**Files:**
- Create: `src/LongYinRoster/Core/SimpleFieldMatrix.cs`
- Modify: `src/LongYinRoster.Tests/PinpointPatcherTests.cs` (1 test 추가)
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` (Compile Include 추가)

- [ ] **Step 1: 실패하는 테스트 추가**

`src/LongYinRoster.Tests/PinpointPatcherTests.cs` 끝에 추가:
```csharp
public class SimpleFieldMatrixTests
{
    [Fact]
    public void Schema_FrozenShape()
    {
        SimpleFieldMatrix.Entries.Should().NotBeNull();
        SimpleFieldMatrix.Entries.Should().NotBeEmpty();
        foreach (var e in SimpleFieldMatrix.Entries)
        {
            e.Name.Should().NotBeNullOrWhiteSpace();
            e.JsonPath.Should().NotBeNullOrWhiteSpace();
            e.PropertyName.Should().NotBeNullOrWhiteSpace();
            e.Type.Should().NotBeNull();
        }
    }
}
```

- [ ] **Step 2: Compile Include 추가**

```xml
    <Compile Include="../LongYinRoster/Core/SimpleFieldMatrix.cs">
      <Link>Core/SimpleFieldMatrix.cs</Link>
    </Compile>
```

- [ ] **Step 3: 테스트 fail 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SimpleFieldMatrixTests"
```
Expected: 컴파일 에러 — `'SimpleFieldMatrix' could not be found`.

- [ ] **Step 4: SimpleFieldMatrix.cs 구현**

`src/LongYinRoster/Core/SimpleFieldMatrix.cs`:
```csharp
using System.Collections.Generic;

namespace LongYinRoster.Core;

/// <summary>
/// PinpointPatcher.SetSimpleFields 가 처리할 simple-value scalar 매트릭스.
///
/// 본 entry list 는 spec §7.2.1 의 "Step 1 SetSimpleFields" mapping 과 1:1.
/// dump 결과 (plan Task 2) 로 채워졌다. 새 필드 추가 / 제거 시 spec §7.2.1 도 동기화.
///
/// SetterStyle:
///   Direct  : InvokeSetter(player, method, newValue)
///   Delta   : InvokeSetter(player, method, newValue - currentValue)
///   None    : 직접 set 경로 없음 — RefreshSelfState 가 derived 로 재계산 기대
/// </summary>
public enum SetterStyle { Direct, Delta, None }

public sealed record SimpleFieldEntry(
    string      Name,
    string      JsonPath,
    string      PropertyName,
    System.Type Type,
    string?     SetterMethod,
    SetterStyle SetterStyle);

public static class SimpleFieldMatrix
{
    /// <summary>
    /// dump 결과로 확정된 매트릭스. plan Task 2 직후 채워진다. 빈 list 면 schema test
    /// 실패 — Task 2 가 완료 안 됐다는 신호.
    /// </summary>
    public static readonly IReadOnlyList<SimpleFieldEntry> Entries = new[]
    {
        // (plan Task 2 의 spec §7.2.1 매트릭스로 entry 채우기)
        // 예시:
        // new SimpleFieldEntry("이름",     "heroName",        "heroName",     typeof(string), "SetHeroName",        SetterStyle.Direct),
        // new SimpleFieldEntry("성",       "nickname",        "nickname",     typeof(string), null,                 SetterStyle.None),
        // new SimpleFieldEntry("명예",     "fame",            "fame",         typeof(int),    "ChangeFame",         SetterStyle.Delta),
        // new SimpleFieldEntry("hp",       "hp",              "hp",           typeof(int),    "ChangeHp",           SetterStyle.Delta),
        // ...
    };
}
```

**중요**: 위 array 는 spec §7.2.1 (Task 2 산출물) 의 "Step 1 SetSimpleFields" mapping 으로 채운다. 한 줄도 빠지면 안 됨. 매트릭스가 비어 있으면 schema test 가 fail (`Entries.Should().NotBeEmpty()`).

- [ ] **Step 5: 테스트 pass 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SimpleFieldMatrixTests"
```
Expected: `Passed: 1, Failed: 0`. fail 시 매트릭스가 비어 있거나 entry 의 빈 string — Task 2 의 spec §7.2.1 다시 확인.

- [ ] **Step 6: 전체 테스트 → 24 pass**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Passed: 24, Failed: 0`.

- [ ] **Step 7: Commit**

```bash
git add src/LongYinRoster/Core/SimpleFieldMatrix.cs src/LongYinRoster.Tests/PinpointPatcherTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "$(cat <<'EOF'
feat(core): SimpleFieldMatrix from dump + schema test

v0.3 plan Task 5: PinpointPatcher.SetSimpleFields 의 매트릭스. spec §7.2.1 의
mapping 과 1:1. schema test 가 매트릭스가 비거나 entry shape 가 깨진 경우 즉시
회귀 감지.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 3 — PinpointPatcher Pipeline (smoke for IL2CPP-bound steps)

### Task 6: PinpointPatcher.Apply 골격 + TryStep 게이팅

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs` (전체 재작성, no-op 폐기)

- [ ] **Step 1: PinpointPatcher.cs 전체 재작성**

`src/LongYinRoster/Core/PinpointPatcher.cs`:
```csharp
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
```

- [ ] **Step 2: 빌드**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: 24 unit tests pass 확인**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Passed: 24, Failed: 0`.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "$(cat <<'EOF'
feat(core): PinpointPatcher.Apply skeleton + TryStep gating

v0.3 plan Task 6: 7-step pipeline 골격 + fatal 게이팅. step body 는 NotImplemented
로 시작 — Task 7~13 에서 각 step 의 실제 구현. v0.1 의 RefreshAfterApply no-op
는 폐기 (Apply 자체로 격상).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: SetSimpleFields 구현 + smoke B1

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs` (SetSimpleFields body)

- [ ] **Step 1: SetSimpleFields body 채우기**

`src/LongYinRoster/Core/PinpointPatcher.cs` 의 `SetSimpleFields` 를 다음으로 교체:
```csharp
    private const System.Reflection.BindingFlags F =
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
        System.Reflection.BindingFlags.Instance;

    private static void SetSimpleFields(JsonElement slot, object player, ApplyResult res)
    {
        foreach (var entry in SimpleFieldMatrix.Entries)
        {
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
                var methodArgs = entry.SetterStyle switch
                {
                    SetterStyle.Direct => new[] { newValue },
                    SetterStyle.Delta  => new object[] { Subtract(newValue, currentValue, entry.Type) },
                    _ => throw new InvalidOperationException("unreachable")
                };
                InvokeMethod(player, entry.SetterMethod!, methodArgs);
                res.AppliedFields.Add(entry.Name);
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"{entry.Name} — {ex.GetType().Name}: {ex.Message}");
            }
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

    private static void InvokeMethod(object obj, string methodName, object?[] args)
    {
        var t = obj.GetType();
        var m = t.GetMethod(methodName, F)
            ?? throw new MissingMethodException(t.FullName, methodName);
        m.Invoke(obj, args);
    }

    private static object Subtract(object? newValue, object? currentValue, Type type)
    {
        if (type == typeof(int))    return ((int)newValue!)    - ((int?)currentValue ?? 0);
        if (type == typeof(long))   return ((long)newValue!)   - ((long?)currentValue ?? 0L);
        if (type == typeof(float))  return ((float)newValue!)  - ((float?)currentValue ?? 0f);
        if (type == typeof(double)) return ((double)newValue!) - ((double?)currentValue ?? 0d);
        throw new InvalidOperationException($"Delta not supported for type {type.Name}");
    }
```

(`F` 상수가 이미 클래스 안에 있으면 중복 선언 안 함.)

- [ ] **Step 2: 빌드 + 24 tests pass**

Run:
```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Build succeeded. 0 Error(s)` + `Passed: 24, Failed: 0`.

- [ ] **Step 3: 임시 [F11+S] 핸들러로 SetSimpleFields 단독 호출 (smoke B1 도구)**

Apply UI 가 Phase 4 에 가서야 와이어링되므로, SetSimpleFields 단독 검증을 위해 임시 keyboard 핸들러 추가. `src/LongYinRoster/UI/ModWindow.cs` 의 `Update` 의 `[F12]` 핸들러 다음에 추가:
```csharp
        // [F11 + S] 임시 — SetSimpleFields 단독 smoke. plan Task 18 에서 제거.
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.S))
        {
            try
            {
                var player = Core.HeroLocator.GetPlayer();
                if (player == null) { Logger.Warn("smoke S: player null"); return; }
                if (!Repo.All[1].IsEmpty)
                {
                    var slot1 = Slots.SlotFile.Read(Repo.PathFor(1));
                    var stripped = Core.PortabilityFilter.StripForApply(slot1.Player);
                    using var doc = System.Text.Json.JsonDocument.Parse(stripped);
                    var res = new Core.ApplyResult();
                    typeof(Core.PinpointPatcher).GetMethod("SetSimpleFields",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .Invoke(null, new object[] { doc.RootElement, player, res });
                    Logger.Info($"smoke S: applied={res.AppliedFields.Count} skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
                }
            }
            catch (Exception ex) { Logger.Error($"smoke S: {ex}"); }
        }
```

- [ ] **Step 4: Smoke B1 — 게임 안 검증**

준비: 게임 시작 → 캐릭터 진입 → 슬롯 1 에 캡처 (`[+]`).
변경: 캐릭터의 simple-value 를 의도적으로 변경 (예: 게임 안 디버그 도구 또는 다른 mod 로 fame 증가, hp 감소).
검증:
```
1. BepInEx 로그 클리어
2. F11+S 누름
3. grep "smoke S" "...LogOutput.log"
4. Expected: "smoke S: applied=X skipped=Y warned=Z" — applied 가 spec §7.2.1 의 Step 1 매트릭스 entry 수와 비슷
5. 게임 캐릭터 정보창 확인 → fame / hp 등이 슬롯 1 캡처 시점 값으로 복귀
```

통과 시 Task 8 진입. 실패 시 (`HasFatalError`, 모든 entry warn 등) `docs/superpowers/specs/2026-04-29-v0.3-smoke.md` 에 결과 기록 + 사용자에게 매트릭스 재검토 요청.

- [ ] **Step 5: smoke 결과 docs/superpowers/specs/2026-04-29-v0.3-smoke.md 에 기록**

파일 신규 또는 추가:
```markdown
# v0.3 Smoke Checklist

## Phase A — Dump (Task 2)
- [x] A1 ... A2 ... A3

## Phase B — PinpointPatcher 단계
- [x] B1 SetSimpleFields — applied=N, fightScore N → N, fame N → N (date)
- [ ] B2 ...
```

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
feat(core): SetSimpleFields step 1 + smoke B1 verified

v0.3 plan Task 7: PinpointPatcher Step 1. SimpleFieldMatrix 기반 game-self method
호출 (Direct / Delta). 임시 [F11+S] 핸들러로 단독 smoke check 통과 (Task 18 에서
제거). docs/.../v0.3-smoke.md 에 결과 기록.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: RebuildKungfuSkills + smoke B2

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (임시 [F11+K] 핸들러)

- [ ] **Step 1: RebuildKungfuSkills body 채우기**

`PinpointPatcher.cs` 의 `RebuildKungfuSkills` 를 다음으로 교체. 매트릭스에서 확정된 Add method 시그니처를 spec §7.2.1 의 "Step 2 RebuildKungfuSkills" 에서 가져온다 (예: `AddKungfuSkill(int id, int lv, int exp, bool equipped)`).
```csharp
    private static void RebuildKungfuSkills(JsonElement slot, object player, ApplyResult res)
    {
        var il2List = ReadFieldOrProperty(player, "kungfuSkills");
        if (il2List == null) { res.SkippedFields.Add("kungfuSkills — list field missing"); return; }
        IL2CppListOps.Clear(il2List);

        if (!slot.TryGetProperty("kungfuSkills", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add("kungfuSkills — not in slot JSON");
            return;
        }

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            int  id       = entry.GetProperty("id").GetInt32();
            int  lv       = entry.TryGetProperty("lv", out var lvEl)         ? lvEl.GetInt32()   : 0;
            int  exp      = entry.TryGetProperty("exp", out var expEl)       ? expEl.GetInt32()  : 0;
            bool equipped = entry.TryGetProperty("equiped", out var eqEl)    ? eqEl.GetBoolean() : false;
            try
            {
                // spec §7.2.1 의 "Step 2 RebuildKungfuSkills" Add 시그니처. dump 결과로 다르면 교체.
                InvokeMethod(player, "AddKungfuSkill", new object[] { id, lv, exp, equipped });
                res.AppliedFields.Add($"kungfuSkill[{id}]");
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"kungfuSkill[{id}] — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
```

**중요**: `"AddKungfuSkill"` method 명 + 인자 list 가 spec §7.2.1 매트릭스와 다르면 spec 따라 교체. spec §7.2.1 가 "AddKungfuSkill(id, lv, exp, equipped)" 가 아니면 위 InvokeMethod 줄을 spec 의 시그니처대로 수정.

- [ ] **Step 2: 빌드 + 24 tests pass**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 둘 다 OK.

- [ ] **Step 3: 임시 [F11+K] 핸들러 추가**

`ModWindow.cs` 의 `Update` 안 [F11+S] 다음에 추가:
```csharp
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.K))
        {
            try
            {
                var player = Core.HeroLocator.GetPlayer();
                if (player == null) { Logger.Warn("smoke K: player null"); return; }
                if (!Repo.All[1].IsEmpty)
                {
                    var slot1 = Slots.SlotFile.Read(Repo.PathFor(1));
                    var stripped = Core.PortabilityFilter.StripForApply(slot1.Player);
                    using var doc = System.Text.Json.JsonDocument.Parse(stripped);
                    var res = new Core.ApplyResult();
                    typeof(Core.PinpointPatcher).GetMethod("RebuildKungfuSkills",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .Invoke(null, new object[] { doc.RootElement, player, res });
                    Logger.Info($"smoke K: applied={res.AppliedFields.Count} warned={res.WarnedFields.Count}");
                }
            }
            catch (Exception ex) { Logger.Error($"smoke K: {ex}"); }
        }
```

- [ ] **Step 4: Smoke B2**

준비: 슬롯 1 에 무공 list 가 있는 캡처 존재. 캐릭터의 무공을 의도적으로 변경 (학습/삭제).
검증: `[F11+K]` → 로그에 `smoke K: applied=N` (N = 슬롯의 무공 수). 캐릭터 무공창 확인 → 슬롯 1 시점 무공으로 복귀.
실패 시: `WarnedFields` 에 어떤 method missing 인지 확인 → spec §7.2.1 의 시그니처 확인 → 매트릭스/코드 수정 후 재시도.

- [ ] **Step 5: smoke-md 갱신**

`docs/superpowers/specs/2026-04-29-v0.3-smoke.md` 에 B2 항목 갱신.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
feat(core): RebuildKungfuSkills step 2 + smoke B2 verified

v0.3 plan Task 8: PinpointPatcher Step 2. Clear=raw + Add=AddKungfuSkill (spec
§7.2.1). 임시 [F11+K] 핸들러로 단독 smoke 통과 (Task 18 에서 제거).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: RebuildItemList + smoke B3

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: RebuildItemList body 채우기**

`PinpointPatcher.cs` 의 `RebuildItemList`:
```csharp
    private static void RebuildItemList(JsonElement slot, object player, ApplyResult res)
    {
        var itemListData = ReadFieldOrProperty(player, "itemListData");
        if (itemListData == null) { res.SkippedFields.Add("itemListData — field missing"); return; }
        var allItem = ReadFieldOrProperty(itemListData, "allItem");
        if (allItem == null) { res.SkippedFields.Add("itemListData.allItem — field missing"); return; }
        IL2CppListOps.Clear(allItem);

        if (!slot.TryGetProperty("itemListData", out var ild) ||
            !ild.TryGetProperty("allItem", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add("itemListData.allItem — not in slot JSON");
            return;
        }

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            int id    = entry.GetProperty("id").GetInt32();
            int count = entry.TryGetProperty("count", out var cEl) ? cEl.GetInt32() : 1;
            try
            {
                // spec §7.2.1 의 "Step 3 RebuildItemList" Add 시그니처.
                InvokeMethod(player, "AddItemToList", new object[] { id, count });
                res.AppliedFields.Add($"item[{id}]x{count}");
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"item[{id}] — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
```

**중요**: `"AddItemToList"` 시그니처를 spec §7.2.1 매트릭스 따라 교체. 시그니처가 다르면 InvokeMethod 라인 + 인자 추출 부분도 교체.

- [ ] **Step 2: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 3: 임시 [F11+I] 핸들러 추가**

`ModWindow.cs` 의 [F11+K] 다음에 추가 (구조는 [F11+K] 와 동일, method 이름 `RebuildItemList` 만 다름):
```csharp
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.I))
        {
            try
            {
                var player = Core.HeroLocator.GetPlayer();
                if (player == null) { Logger.Warn("smoke I: player null"); return; }
                if (!Repo.All[1].IsEmpty)
                {
                    var slot1 = Slots.SlotFile.Read(Repo.PathFor(1));
                    var stripped = Core.PortabilityFilter.StripForApply(slot1.Player);
                    using var doc = System.Text.Json.JsonDocument.Parse(stripped);
                    var res = new Core.ApplyResult();
                    typeof(Core.PinpointPatcher).GetMethod("RebuildItemList",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .Invoke(null, new object[] { doc.RootElement, player, res });
                    Logger.Info($"smoke I: applied={res.AppliedFields.Count} warned={res.WarnedFields.Count}");
                }
            }
            catch (Exception ex) { Logger.Error($"smoke I: {ex}"); }
        }
```

- [ ] **Step 4: Smoke B3**

준비: 슬롯 1 에 인벤토리 데이터 있음. 캐릭터 인벤토리 변경 (아이템 추가/삭제).
검증: [F11+I] → 로그 `smoke I: applied=N`. 인벤토리창 확인.

- [ ] **Step 5: smoke-md 갱신 + Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
feat(core): RebuildItemList step 3 + smoke B3 verified

v0.3 plan Task 9: PinpointPatcher Step 3 (인벤토리). Clear+AddItemToList. 임시
[F11+I] smoke 통과.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 10: RebuildSelfStorage + smoke B4

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: RebuildSelfStorage body 채우기**

`PinpointPatcher.cs` 의 `RebuildSelfStorage`. itemListData 와 동일 패턴이지만 `selfStorage.allItem` 사용 + spec §7.2.1 의 Add method (예: `AddToStorage(id, count)`):
```csharp
    private static void RebuildSelfStorage(JsonElement slot, object player, ApplyResult res)
    {
        var selfStorage = ReadFieldOrProperty(player, "selfStorage");
        if (selfStorage == null) { res.SkippedFields.Add("selfStorage — field missing"); return; }
        var allItem = ReadFieldOrProperty(selfStorage, "allItem");
        if (allItem == null) { res.SkippedFields.Add("selfStorage.allItem — field missing"); return; }
        IL2CppListOps.Clear(allItem);

        if (!slot.TryGetProperty("selfStorage", out var ss) ||
            !ss.TryGetProperty("allItem", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add("selfStorage.allItem — not in slot JSON");
            return;
        }

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            int id    = entry.GetProperty("id").GetInt32();
            int count = entry.TryGetProperty("count", out var cEl) ? cEl.GetInt32() : 1;
            try
            {
                // spec §7.2.1 의 "Step 4 RebuildSelfStorage" Add 시그니처.
                InvokeMethod(player, "AddToStorage", new object[] { id, count });
                res.AppliedFields.Add($"storage[{id}]x{count}");
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"storage[{id}] — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
```

**중요**: `"AddToStorage"` 가 spec §7.2.1 의 시그니처와 다르면 교체. method 자체 없으면 (⚪) selfStorage step 을 미지원으로 처리 — 매트릭스에서 v0.4 후보로 분류.

- [ ] **Step 2: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 3: 임시 [F11+G] 핸들러 추가**

(`G` = 창고/storage 머릿글자) `ModWindow.cs` 에 [F11+I] 다음에 추가, 동일 구조, method 이름 `RebuildSelfStorage`, 로그 prefix `smoke G`.

- [ ] **Step 4: Smoke B4**

창고 변경 후 [F11+G] → 로그 + 게임 창고창 확인.

- [ ] **Step 5: smoke-md 갱신 + Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
feat(core): RebuildSelfStorage step 4 + smoke B4 verified

v0.3 plan Task 10: PinpointPatcher Step 4 (창고). 임시 [F11+G] smoke 통과.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 11: RebuildHeroTagData + smoke B5

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: RebuildHeroTagData body 채우기**

천부(heroTagData) 의 schema 는 dump 산출물 + spec §7.2.1 의 "Step 5" 가 명시. 일반적 패턴:
```csharp
    private static void RebuildHeroTagData(JsonElement slot, object player, ApplyResult res)
    {
        // heroTagPoint (남은 천부 포인트) 단순 set — spec §7.2.1 매핑
        if (slot.TryGetProperty("heroTagPoint", out var ptEl) && ptEl.ValueKind == JsonValueKind.Number)
        {
            try
            {
                var current = ReadFieldOrProperty(player, "heroTagPoint");
                int newPt = ptEl.GetInt32();
                int curPt = current is int ci ? ci : 0;
                // spec §7.2.1: ChangeHeroTagPoint(delta) 또는 SetHeroTagPoint(value)
                InvokeMethod(player, "ChangeHeroTagPoint", new object[] { newPt - curPt });
                res.AppliedFields.Add($"heroTagPoint {curPt}→{newPt}");
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"heroTagPoint — {ex.GetType().Name}: {ex.Message}");
            }
        }

        // heroTagData (천부 list) — 일반적 schema: { allTag: [{id, lv}] }
        var heroTagData = ReadFieldOrProperty(player, "heroTagData");
        if (heroTagData == null) { res.SkippedFields.Add("heroTagData — field missing"); return; }
        var allTag = ReadFieldOrProperty(heroTagData, "allTag");
        if (allTag == null) { res.SkippedFields.Add("heroTagData.allTag — field missing"); return; }
        IL2CppListOps.Clear(allTag);

        if (!slot.TryGetProperty("heroTagData", out var htd) ||
            !htd.TryGetProperty("allTag", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            res.SkippedFields.Add("heroTagData.allTag — not in slot JSON");
            return;
        }

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            int id = entry.GetProperty("id").GetInt32();
            int lv = entry.TryGetProperty("lv", out var lvEl) ? lvEl.GetInt32() : 1;
            try
            {
                // spec §7.2.1 의 "Step 5 RebuildHeroTagData" Add 시그니처.
                InvokeMethod(player, "AddHeroTag", new object[] { id, lv });
                res.AppliedFields.Add($"heroTag[{id}]Lv{lv}");
            }
            catch (Exception ex)
            {
                res.WarnedFields.Add($"heroTag[{id}] — {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
```

**중요**: spec §7.2.1 의 Step 5 매핑이 다른 method 명/schema 면 교체. heroTagData schema 도 dump 후 확인.

- [ ] **Step 2: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 3: 임시 [F11+T] 핸들러 추가**

(`T` = talent) [F11+G] 다음에 추가, method 이름 `RebuildHeroTagData`, 로그 prefix `smoke T`.

- [ ] **Step 4: Smoke B5**

천부 변경 → [F11+T] → 로그 + 천부창 확인.

- [ ] **Step 5: smoke-md 갱신 + Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
feat(core): RebuildHeroTagData step 5 + smoke B5 verified

v0.3 plan Task 11: PinpointPatcher Step 5 (천부). heroTagPoint + heroTagData.allTag.
임시 [F11+T] smoke 통과.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 12: RefreshSelfState (fatal) + smoke B6

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: RefreshSelfState body 채우기**

`PinpointPatcher.cs` 의 `RefreshSelfState`. spec §7.2.1 "Step 6" 의 Refresh method list 사용. 한 method 씩 try/catch — 일부 missing 은 warn, step 자체 throw 만 fatal.
```csharp
    private static void RefreshSelfState(object player, ApplyResult res)
    {
        // spec §7.2.1 "Step 6 RefreshSelfState" 매핑. dump 결과로 method 이름 다르면 교체.
        TryInvokeNoArg(player, "RefreshMaxAttriAndSkill", res);
        TryInvokeNoArg(player, "GetMaxAttri",             res);
        TryInvokeNoArg(player, "GetMaxFightSkill",        res);
        TryInvokeNoArg(player, "GetMaxLivingSkill",       res);
        TryInvokeNoArg(player, "GetMaxFavor",             res);
        TryInvokeNoArg(player, "GetFinalTravelSpeed",     res);
        // (spec §7.2.1 에 더 있으면 모두 추가)
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
```

`Type` 이 namespace 충돌하면 `using System;` 위쪽에 이미 있으므로 OK. `F` 상수도 이미 존재.

- [ ] **Step 2: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 3: 임시 [F11+R] 핸들러 추가 — 단독 + 누적 smoke**

`ModWindow.cs` 에 [F11+T] 다음에 추가:
```csharp
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.R))
        {
            try
            {
                var player = Core.HeroLocator.GetPlayer();
                if (player == null) { Logger.Warn("smoke R: player null"); return; }
                var res = new Core.ApplyResult();
                typeof(Core.PinpointPatcher).GetMethod("RefreshSelfState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .Invoke(null, new object[] { player, res });
                Logger.Info($"smoke R: applied={res.AppliedFields.Count} skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
            }
            catch (Exception ex) { Logger.Error($"smoke R: {ex}"); }
        }
```

- [ ] **Step 4: Smoke B6**

상황 1 — 단독: 캐릭터 진입 후 [F11+R] → 로그 `smoke R: applied=N skipped=M`. applied > 0 (적어도 1개 method 정상 호출).

상황 2 — 누적 (step 1~5 실행 후 stat stale 검증): [F11+S] → [F11+K] → [F11+I] → [F11+G] → [F11+T] → [F11+R]. 마지막 [F11+R] 후 캐릭터 정보창의 fightScore / maxhp / 무공 max 값 등이 game-internal 계산값과 일치 (UI 재진입 없이도 갱신).

- [ ] **Step 5: smoke-md 갱신 + Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
feat(core): RefreshSelfState step 6 (fatal) + smoke B6 verified

v0.3 plan Task 12: PinpointPatcher Step 6. fatal=true 게이팅 — RefreshXxx 자체
throw 시 자동복원 트리거. spec §7.2.1 의 method 들 호출. 임시 [F11+R] 단독 +
B1~B5 누적 후 stat 갱신 smoke 통과.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 13: RefreshExternalManagers + smoke B7

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: RefreshExternalManagers body 채우기**

`PinpointPatcher.cs` 의 `RefreshExternalManagers`. spec §7.2.1 "Step 7" 의 manager list 사용:
```csharp
    private static void RefreshExternalManagers(object player, ApplyResult res)
    {
        // spec §7.2.1 "Step 7 RefreshExternalManagers" 매핑. 각 manager 의 Instance 가
        // null 이면 skip, RefreshHero(player) signature mismatch 면 warn.
        TryInvokeManager("HeroIconManager",   "RefreshHero", player, res);
        TryInvokeManager("HeroPanelController","UpdateHero", player, res);
        // (spec §7.2.1 에 더 있으면 모두 추가)
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
        const BindingFlags SF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
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
```

`using System.Reflection;` 가 이미 PinpointPatcher 안에 없으면 추가.

- [ ] **Step 2: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 3: 임시 [F11+E] 핸들러 추가**

(`E` = external) [F11+R] 다음에 추가, method 이름 `RefreshExternalManagers`, 로그 prefix `smoke E`.

- [ ] **Step 4: Smoke B7**

B1~B6 누적 실행 후 [F11+E] → 캐릭터 포트레이트 / 마을 패널의 영웅 아이콘 / 미니맵 영웅 표시 등 시각적으로 갱신.

- [ ] **Step 5: smoke-md 갱신 + Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
feat(core): RefreshExternalManagers step 7 + smoke B7 verified

v0.3 plan Task 13: PinpointPatcher Step 7 (마지막). spec §7.2.1 의 Hero-related
manager Refresh API 호출. 임시 [F11+E] smoke 통과 — 포트레이트 / 영웅 아이콘
시각 갱신 확인.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 4 — Orchestration

### Task 14: Config 변경 — RunPinpointPatchOnApply 제거 (이미 Task 1 에서)

**Files:** (no-op — Task 1 에서 이미 적용)

- [ ] **Step 1: 확인만**

Run:
```bash
grep -n "RunPinpointPatchOnApply\|AllowApplyToGame" src/LongYinRoster/Config.cs
```
Expected: `AllowApplyToGame` 만 보임, `RunPinpointPatchOnApply` 없음. Task 1 에서 정상 처리됐다면 OK.

이 task 는 plan 의 명시적 단계로 두지만 실제 코드 변경은 Task 1 에서 끝남. step 1 만 verify 후 progress.

---

### Task 15: KoreanStrings 신규 토스트/dialog 문자열

**Files:**
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs`

- [ ] **Step 1: KoreanStrings 신규/변경 추가**

`src/LongYinRoster/Util/KoreanStrings.cs` 의 `ToastErrApply` (line 55) 다음에 추가:
```csharp
    // v0.3 신규
    public const string ToastApplyOk                 = "✓ 슬롯 {0} 적용됨 ({1}개 필드, {2}개 미지원)";
    public const string ToastApplyDisabled           = "✘ Apply 가 설정에서 비활성됨";
    public const string ToastErrSlotRead             = "✘ 슬롯 {0} 읽기/파싱 실패: {1}";
    public const string ToastErrApplyAutoRestored    = "✘ 적용 실패: {0}. 자동복원 시도됨 (로그 확인)";
    public const string ToastErrApplyNoBackup        = "✘ 적용 실패: {0}. 자동백업 비활성 — 수동 복구";
    public const string ToastErrEmptySlot            = "✘ 슬롯이 비어 있습니다";
    public const string ToastErrNoBackup             = "✘ 자동백업이 없습니다";
    public const string ConfirmTitleRestore          = "↶ 자동백업 복원 확인";
    public const string ConfirmRestoreMain           = "Apply 직전 상태로 되돌립니다.\n현재 캐릭터 본질이 슬롯 0 의 자동백업으로 교체됩니다.";
    public const string Restore                      = "복원";
```

- [ ] **Step 2: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: OK.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Util/KoreanStrings.cs
git commit -m "$(cat <<'EOF'
feat(util): KoreanStrings v0.3 신규 토스트/dialog 문자열

v0.3 plan Task 15: ToastApplyOk / ToastApplyDisabled / ToastErrSlotRead /
ToastErrApplyAutoRestored / ToastErrApplyNoBackup / ToastErrEmptySlot /
ToastErrNoBackup / ConfirmTitleRestore / ConfirmRestoreMain / Restore.
spec §8.3 매핑.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 16: ModWindow.RequestApply / DoApply 구현

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: ModWindow.cs 의 Awake 에 와이어링 추가**

`ModWindow.cs` 의 Awake 메서드의 wire panel callbacks 부분 (line 65~73) 을 다음으로 교체:
```csharp
        // wire panel callbacks
        _list.OnSaveCurrentRequested    = RequestCapture;
        _list.OnImportFromFileRequested = RequestImportFromFile;
        _detail.OnRenameRequested       = RequestRename;
        _detail.OnCommentRequested      = RequestComment;
        _detail.OnDeleteRequested       = RequestDelete;
        _detail.OnApplyRequested        = RequestApply;
        _detail.OnRestoreRequested      = RequestRestore;
```

- [ ] **Step 2: ModWindow.cs 의 RequestDelete 다음에 RequestApply / DoApply / AttemptAutoRestore 추가**

`ModWindow.cs` 의 `RequestDelete` 메서드 (line 207~232) 끝의 `}` 다음 (`/// <summary> [F] 파일에서 핸들러` 직전) 에 추가:
```csharp
    // ---------------------------------------------------------------- v0.3 Apply / Restore

    private void RequestApply(int slot)
    {
        if (slot < 1 || slot >= Repo.All.Count)
        {
            ToastService.Push(KoreanStrings.ToastErrEmptySlot, ToastKind.Error);
            return;
        }
        var entry = Repo.All[slot];
        if (entry.IsEmpty || entry.Meta == null)
        {
            ToastService.Push(KoreanStrings.ToastErrEmptySlot, ToastKind.Error);
            return;
        }

        var label = entry.Meta.UserLabel;
        var body  = string.Format(KoreanStrings.ConfirmApplyMain, $"슬롯 {slot} · {label}")
                  + "\n" + KoreanStrings.ConfirmApplyPolicy;
        _confirm.Show(
            title: KoreanStrings.ConfirmTitleApply,
            body:  body,
            confirmLabel: KoreanStrings.Apply,
            onConfirm: () => DoApply(slot, Config.AutoBackupBeforeApply.Value));
    }

    private void DoApply(int slot, bool doAutoBackup)
    {
        var player = Core.HeroLocator.GetPlayer();
        if (player == null)
        {
            ToastService.Push(KoreanStrings.ToastErrNoPlayer, ToastKind.Error);
            return;
        }
        if (!Config.AllowApplyToGame.Value)
        {
            ToastService.Push(KoreanStrings.ToastApplyDisabled, ToastKind.Error);
            return;
        }

        // 1. 자동백업 (slot 0)
        if (doAutoBackup)
        {
            try
            {
                var nowJson    = Core.SerializerService.Serialize(player);
                var nowSummary = SlotMetadata.FromPlayerJson(nowJson);
                var backupLabel = $"{nowSummary.HeroName} {nowSummary.HeroNickName} (Apply 직전 자동백업)";
                var payload = new SlotPayload
                {
                    Meta = new SlotPayloadMeta(
                        SchemaVersion: SlotFile.CurrentSchemaVersion,
                        ModVersion: Plugin.VERSION,
                        SlotIndex: 0,
                        UserLabel: backupLabel,
                        UserComment: $"slot {slot} 적용 직전",
                        CaptureSource: "auto",
                        CaptureSourceDetail: $"pre-apply-from-slot-{slot}",
                        CapturedAt: DateTime.Now,
                        GameSaveVersion: "1.0.0 f8.2",
                        GameSaveDetail: "",
                        Summary: nowSummary),
                    Player = nowJson,
                };
                Repo.WriteAutoBackup(payload);
            }
            catch (Exception ex)
            {
                ToastService.Push(string.Format(KoreanStrings.ToastErrAutoBackup), ToastKind.Error);
                Logger.Error($"DoApply auto-backup failed: {ex}");
                return;
            }
        }

        // 2. 슬롯 데이터 read + strip
        SlotPayload loaded;
        string stripped;
        try
        {
            loaded   = SlotFile.Read(Repo.PathFor(slot));
            stripped = Core.PortabilityFilter.StripForApply(loaded.Player);
        }
        catch (Exception ex)
        {
            ToastService.Push(string.Format(KoreanStrings.ToastErrSlotRead, slot, ex.Message), ToastKind.Error);
            Logger.Error($"DoApply slot read failed (slot={slot}): {ex}");
            return;
        }

        // 3. PinpointPatcher 호출
        Core.ApplyResult res;
        try
        {
            res = Core.PinpointPatcher.Apply(stripped, player);
        }
        catch (Exception ex)
        {
            Logger.Error($"PinpointPatcher.Apply top-level throw: {ex}");
            if (doAutoBackup) AttemptAutoRestore(player);
            ToastService.Push(string.Format(
                doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                             : KoreanStrings.ToastErrApplyNoBackup, ex.Message), ToastKind.Error);
            return;
        }

        // 4. fatal 결과 처리
        if (res.HasFatalError)
        {
            string firstErr = res.StepErrors.Count > 0 ? res.StepErrors[0].Message : "fatal step";
            if (doAutoBackup) AttemptAutoRestore(player);
            ToastService.Push(string.Format(
                doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                             : KoreanStrings.ToastErrApplyNoBackup, firstErr), ToastKind.Error);
            return;
        }

        // 5. 성공 (warn/skip 포함)
        Repo.Reload();
        ToastService.Push(string.Format(KoreanStrings.ToastApplyOk,
                                        slot, res.AppliedFields.Count, res.SkippedFields.Count),
                          ToastKind.Success);
        Logger.Info($"Apply OK slot={slot} applied={res.AppliedFields.Count} " +
                    $"skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
    }

    private void AttemptAutoRestore(object player)
    {
        try
        {
            var slot0 = SlotFile.Read(Repo.PathFor(0));
            var stripped = Core.PortabilityFilter.StripForApply(slot0.Player);
            var res = Core.PinpointPatcher.Apply(stripped, player);
            if (res.HasFatalError)
                Logger.Error("Auto-restore also failed — game state may be inconsistent");
            else
                Logger.Info($"Auto-restore OK applied={res.AppliedFields.Count}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Auto-restore threw: {ex}");
        }
    }
```

`using LongYinRoster.Slots;` 등 using 은 ModWindow 가 이미 가지고 있음 (`Capture` 흐름이 동일 namespace 사용). 추가 using 불필요.

- [ ] **Step 3: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: OK.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/UI/ModWindow.cs
git commit -m "$(cat <<'EOF'
feat(ui): ModWindow.RequestApply / DoApply / AttemptAutoRestore wired

v0.3 plan Task 16: spec §5.1 의 Apply orchestration. ConfirmDialog → 자동백업
(slot 0) → SlotFile.Read → StripForApply → PinpointPatcher.Apply → 자동복원
fallback → 토스트. _detail.OnApplyRequested 와이어링.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 17: ModWindow.RequestRestore

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: RequestRestore 추가**

`ModWindow.cs` 의 `AttemptAutoRestore` 다음에 추가:
```csharp
    private void RequestRestore(int _slotArg)
    {
        // SlotDetailPanel 의 OnRestoreRequested 가 entry.Index 를 넘기지만 Restore 는 항상 슬롯 0.
        if (Repo.All[0].IsEmpty)
        {
            ToastService.Push(KoreanStrings.ToastErrNoBackup, ToastKind.Error);
            return;
        }
        var label = Repo.All[0].Meta?.UserLabel ?? KoreanStrings.SlotAutoBackup;
        _confirm.Show(
            title: KoreanStrings.ConfirmTitleRestore,
            body:  KoreanStrings.ConfirmRestoreMain + $"\n원본: {label}",
            confirmLabel: KoreanStrings.Restore,
            onConfirm: () => DoApply(slot: 0, doAutoBackup: false));
    }
```

(`SlotDetailPanel.OnRestoreRequested` 가 `Action<int>` 라 인자 받지만 사용 안 함 — `_slotArg` 무시.)

**`DoApply(slot: 0, ...)` 가 작동하려면 SlotRepository.PathFor(0) 가 정상 file path 반환해야 하고, SlotFile.Read 가 슬롯 0 파일 읽을 수 있어야 함. SlotRepository.Write(0, ...) 만 throw 하지 Read 는 정상**. spec §5.2 가정 일치.

- [ ] **Step 2: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/UI/ModWindow.cs
git commit -m "$(cat <<'EOF'
feat(ui): ModWindow.RequestRestore — slot 0 → game

v0.3 plan Task 17: spec §5.2 — Restore 는 DoApply(slot:0, doAutoBackup:false)
호출로 Apply 와 동일 코드 path 재사용. _detail.OnRestoreRequested 와이어링.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 18: SlotDetailPanel 의 Apply / Restore 버튼 활성 + 임시 smoke 핸들러 제거

**Files:**
- Modify: `src/LongYinRoster/UI/SlotDetailPanel.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (임시 [F11+S/K/I/G/T/R/E] 핸들러 제거)

- [ ] **Step 1: SlotDetailPanel.cs 의 Apply / Restore 버튼 활성**

`src/LongYinRoster/UI/SlotDetailPanel.cs` 의 line 48~67 (`if (entry.Index == 0) ... else ...`) 를 다음으로 교체:
```csharp
        if (entry.Index == 0)
        {
            // Restore (slot 0 → game)
            GUI.enabled = inGame;
            if (GUILayout.Button(KoreanStrings.RestoreBtn))
                OnRestoreRequested?.Invoke(entry.Index);
            GUI.enabled = true;
        }
        else
        {
            // Apply (slot → game)
            GUI.enabled = inGame;
            if (GUILayout.Button(KoreanStrings.ApplyBtn))
                OnApplyRequested?.Invoke(entry.Index);
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(KoreanStrings.RenameBtn))  OnRenameRequested ?.Invoke(entry.Index);
            if (GUILayout.Button(KoreanStrings.CommentBtn)) OnCommentRequested?.Invoke(entry.Index);
            if (GUILayout.Button(KoreanStrings.DeleteBtn))  OnDeleteRequested ?.Invoke(entry.Index);
            GUILayout.EndHorizontal();
        }
```

(`(v0.x 예정)` 라벨 제거. `inGame` 이 false 이면 버튼 disable — `HeroLocator.IsInGame()` 결과.)

- [ ] **Step 2: ModWindow.cs 의 [F11+S/K/I/G/T/R/E] 임시 핸들러 모두 제거**

`ModWindow.cs` 의 `Update` 메서드 안의 `// [F11 + S] 임시 ...` 부터 시작하는 블록들을 모두 삭제. `[F12]` 핸들러는 유지 (Task 21 에서 별도 제거).

`Update` 메서드는 다음 형태로 정리:
```csharp
    private void Update()
    {
        if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

        // [F12] HeroDataDump trigger — v0.3 plan Task 1 임시 핸들러. plan Task 21 에서 제거.
        if (Input.GetKeyDown(KeyCode.F12)) Core.HeroDataDump.DumpToLog();

        if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
            Time.timeScale = 0f;
    }
```

- [ ] **Step 3: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/UI/SlotDetailPanel.cs src/LongYinRoster/UI/ModWindow.cs
git commit -m "$(cat <<'EOF'
feat(ui): activate Apply/Restore buttons; remove temp smoke handlers

v0.3 plan Task 18: SlotDetailPanel 의 Apply / Restore 버튼 정상 활성, (v0.x 예정)
라벨 제거. ModWindow 의 [F11+S/K/I/G/T/R/E] 임시 smoke 핸들러 모두 제거 ([F12]
HeroDataDump 만 잔존, Task 21 에서 마지막 제거). 이제 UI 클릭으로 전체 7-step
파이프라인이 호출되는 형태.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 5 — Integration smoke

### Task 19: smoke C1 + C2 (큰 변경 + Apply + save→reload)

**Files:**
- Modify: `docs/superpowers/specs/2026-04-29-v0.3-smoke.md`

- [ ] **Step 1: BepInEx 로그 클리어 + 게임 시작**

```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

- [ ] **Step 2: Smoke C1 — 슬롯 1 캡처 → 큰 변경 → Apply → 종합 일치**

순서:
1. 게임 캐릭터 진입
2. 모드 창 열기 (F11) → 슬롯 1 에 캡처 ([+])
3. 모드 창 닫기 (F11)
4. 게임 안에서 큰 변경 (예: 다른 캐릭터 영입 / 무공 학습 / 아이템 다수 획득 / 천부 사용)
5. 모드 창 열기 → 슬롯 1 의 ▼ 버튼 → ConfirmDialog → 덮어쓰기 → 토스트 "✓ 슬롯 1 적용됨 (N개 필드, M개 미지원)"
6. 캐릭터 정보창 / 무공창 / 인벤토리창 / 천부창 모두 슬롯 1 시점 상태로 복귀

기준: applied 카운트가 spec §7.2.1 매트릭스 기대 수에 가까움 (10+ 필드). skipped 가 매트릭스의 ⚪ 항목 수와 일치.

- [ ] **Step 3: Smoke C2 — save → reload → 정보창 정상**

순서:
1. C1 직후 (Apply 됨 상태)
2. 게임 메뉴 → 저장 (게임 자체 SaveSlot 0 또는 1)
3. 메인 메뉴 → 다시 그 슬롯 로드
4. 캐릭터 정보창 열기 (게임 안 단축키 또는 마을 패널 클릭)
5. **정보창이 정상 열림** (NRE / blank 없음)

이 단계가 **v0.2 시도 2 의 정확한 실패점**. 통과 못 하면 spec §1.2 의 reference link 문제가 v0.3 에서도 잔존하는 것 — Phase 3 의 어느 step (가능성 높음: Step 2 또는 3 의 collection rebuild 의 reference link) 가 broken. Logger 의 Apply 결과 라인 + game 자체 BepInEx 로그의 NRE stack trace 확인.

- [ ] **Step 4: smoke-md 갱신**

`docs/superpowers/specs/2026-04-29-v0.3-smoke.md` 에 C1, C2 결과 + 관찰 (applied/skipped 카운트, 시각적 확인 항목, NRE 없음) 기록.

- [ ] **Step 5: Commit (smoke 결과만)**

```bash
git add docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
docs: smoke C1 + C2 verified — Apply integration + save/reload

v0.3 plan Task 19: 슬롯 1 캡처 → 큰 변경 → Apply → 종합 일치 (C1). save → reload
→ 정보창 정상 (C2 — v0.2 시도 2 의 실패점 통과). 결과 기록.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 20: smoke C3~C7 + D1~D3

**Files:**
- Modify: `docs/superpowers/specs/2026-04-29-v0.3-smoke.md`

- [ ] **Step 1: C3 — 보존 필드 검증**

순서: Apply 직전에 force/location/relations 메모 → Apply → 같은 값 유지 확인. 토스트가 spec §"보존 정책" 그대로 동작.

- [ ] **Step 2: C4 — Restore (slot 0)**

C1 직후 (자동백업 슬롯 0 존재). 슬롯 0 선택 → ↶ 복원 → ConfirmDialog → 복원 → 토스트. 캐릭터 정보가 C1 직전 (Apply 직전) 상태로 복귀.

- [ ] **Step 3: C5 — 자동복원 트리거 (의도적 throw)**

(이 step 은 일시 코드 변경 필요)
1. `PinpointPatcher.cs` 의 `RefreshSelfState` 첫 줄에 `throw new InvalidOperationException("smoke C5 forced");` 추가 (임시)
2. 빌드
3. 게임 안 슬롯 1 ▼ → ConfirmDialog → 덮어쓰기
4. 토스트 `ToastErrApplyAutoRestored` 확인 + 게임 캐릭터 = Apply 직전 상태 (자동복원 동작)
5. 임시 throw 라인 제거 + 빌드 + 다시 게임 → 정상 Apply 동작 확인
6. **임시 throw 라인이 코드에 남아 있지 않은지 grep 으로 verify**

```bash
grep -n "smoke C5 forced" src/LongYinRoster/Core/PinpointPatcher.cs
```
Expected: 빈 출력.

- [ ] **Step 4: C6 — Config kill switch**

BepInEx 의 `BepInEx/config/com.deepe.longyinroster.cfg` 에서 `AllowApplyToGame = false` 변경 → 게임 재시작 → 슬롯 1 ▼ → 토스트 `ToastApplyDisabled` + game state 변경 0. 다시 `true` 로 변경.

- [ ] **Step 5: C7 — 미지원 필드 토스트**

C1 (정상 Apply) 의 토스트가 "✓ 슬롯 1 적용됨 (N개 필드, **M개 미지원**)" 형식 + BepInEx 로그에 `Apply OK ... skipped=M` + 이전 단계 dump 매트릭스의 ⚪ 항목과 M 일치.

- [ ] **Step 6: D1 — 빈 슬롯 Apply 시도**

빈 슬롯 (예: 슬롯 5) 선택 — Detail panel 의 EmptyState 표시 → Apply 버튼 자체 안 보임 (또는 disable). UI 클릭 무반응.

- [ ] **Step 7: D2 — 게임 진입 전 Apply**

메인 메뉴 (캐릭터 진입 안 한 상태) → F11 → 슬롯 1 선택 → Detail panel 의 `inGame=false` → Apply 버튼 disable 톤. 클릭 자체 안 됨. (또는 클릭됐는데 토스트 `ToastErrNoPlayer` — 둘 다 OK.)

- [ ] **Step 8: D3 — 자동백업 slot 0 의 직전상태 보존**

C1 시퀀스 후 슬롯 0 선택 → Detail panel 의 캐릭터 이름 / 시간이 C1 시점 (Apply 직전) 의 값.

- [ ] **Step 9: smoke-md 갱신**

C3, C4, C5, C6, C7, D1, D2, D3 모두 [x] + 결과 기록.

- [ ] **Step 10: Commit**

```bash
git add docs/superpowers/specs/2026-04-29-v0.3-smoke.md
git commit -m "$(cat <<'EOF'
docs: smoke C3~C7 + D1~D3 verified

v0.3 plan Task 20: 보존필드 / Restore / 자동복원 throw / config kill switch /
미지원 필드 토스트 / edge case (빈 슬롯, no-game, slot 0 보존) 모두 통과.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 6 — Release

### Task 21: HeroDataDump 제거 + [F12] 핸들러 제거

**Files:**
- Delete: `src/LongYinRoster/Core/HeroDataDump.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (Update 의 [F12] 핸들러 제거)

- [ ] **Step 1: HeroDataDump.cs 삭제**

```bash
rm "src/LongYinRoster/Core/HeroDataDump.cs"
```

- [ ] **Step 2: ModWindow.cs 의 [F12] 핸들러 제거**

`src/LongYinRoster/UI/ModWindow.cs` 의 `Update` 메서드를 다음으로 교체 (Task 1 의 임시 핸들러 라인 2개 제거):
```csharp
    private void Update()
    {
        if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

        if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
            Time.timeScale = 0f;
    }
```

- [ ] **Step 3: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: `Build succeeded.` + `Passed: 24, Failed: 0`. `'HeroDataDump' could not be found` 에러가 없어야 함 — Task 1 후 다른 곳에서 import 했다면 그 import 도 제거.

- [ ] **Step 4: 임시 코드 잔재 확인**

Run:
```bash
grep -rn "HeroDataDump\|smoke C5 forced\|F11 + [SKIGTRE]" src/LongYinRoster/ docs/
```
Expected: 출력 없음 (또는 docs/HeroData-methods.md 의 dump 산출물 참조 정도).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
chore(release): remove HeroDataDump temp tool + [F12] handler

v0.3 plan Task 21: 임시 진단 코드 release zip 에서 제거. docs/HeroData-methods.md
는 영구 reference 로 유지.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 22: Plugin.VERSION bump + README + HANDOFF 갱신

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`
- Modify: `README.md`
- Modify: `docs/HANDOFF.md`

- [ ] **Step 1: Plugin.VERSION bump**

`src/LongYinRoster/Plugin.cs` 의 line 17 을 다음으로 교체:
```csharp
    public const string VERSION = "0.3.0";
```

- [ ] **Step 2: README 갱신 — Apply / Restore 활성**

`README.md` 의 v0.2 기능 섹션 다음에 v0.3 섹션 추가. 기존 README 에 "Apply (v0.2 예정)" / "Restore (v0.2 예정)" 같은 표현이 있으면 "Apply (활성)" 로 변경.

추가할 새 섹션 (README 의 적절한 위치, "기능" 절 끝):
```markdown
### v0.3 — Apply (slot → game) + Restore (slot 0 → game)

- 슬롯의 캐릭터 본질 (이름, 스탯, 무공, 인벤토리, 천부 등) 을 현재 플레이어에 덮어쓰기
- Apply 직전 자동백업 (슬롯 0) — 실패 시 자동복원
- 보존 필드 (force / location / relations) 는 변경 안 됨 — 사회적 위치 유지
- 지원 필드 매트릭스: `docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md` §7.2

**제한사항** (v0.4 후보):
- (spec §12 의 항목 요약 — Apply preview, selective apply 등)
- 매트릭스의 ⚪ 분류 필드는 v0.3 에서 미지원

**사용법**:
1. 모드 창 (F11) → 슬롯 1~20 선택 → `▼ 현재 플레이어로 덮어쓰기`
2. 자동백업 슬롯 0 → `↶ 복원` 으로 Apply 직전 상태로 되돌림
```

- [ ] **Step 3: HANDOFF 갱신 — v0.3 출시 완료**

`docs/HANDOFF.md` 의 다음 부분을 갱신:
- 헤더의 "**진행 상태**" 를 **v0.3.0 출시 완료** 로 변경
- §"Releases" 에 v0.3.0 라인 추가
- §1 한 줄 요약 — v0.3 Apply 흐름 정상 작동 명시
- §2 깃 히스토리 — Task 1~22 의 commit 들을 prepend
- §5 검증된 것 / 검증 안 된 것 — v0.2 의 ❌ Apply 항목들 모두 ✅ 로 이동
- §6 다음 세션 — v0.4 후보 (spec §12) 또는 "v0.3 출시 완료, 다음 세션 없음"

- [ ] **Step 4: 빌드 + 24 tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

- [ ] **Step 5: 게임 한 번 더 띄워서 v0.3.0 로딩 확인**

Run:
```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

게임 시작 → 로그 확인:
```bash
grep -n "Loaded LongYin Roster Mod" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```
Expected: `Loaded LongYin Roster Mod v0.3.0`. 게임 종료.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Plugin.cs README.md docs/HANDOFF.md
git commit -m "$(cat <<'EOF'
docs: bump VERSION to 0.3.0; update README + HANDOFF

v0.3 plan Task 22: Plugin.VERSION 0.3.0, README 의 Apply/Restore 활성 문서화,
HANDOFF 의 진행 상태를 v0.3.0 출시 완료로.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 23: dist 패키징 + tag v0.3.0 + GitHub release

**Files:**
- Create: `dist/LongYinRoster_v0.3.0/` (zip 패키징 산출물)
- Create: `dist/LongYinRoster_v0.3.0.zip`

- [ ] **Step 1: dist 폴더 구성**

PowerShell 로:
```powershell
$dist = "dist/LongYinRoster_v0.3.0"
New-Item -Path $dist -ItemType Directory -Force
New-Item -Path "$dist/BepInEx/plugins/LongYinRoster" -ItemType Directory -Force
Copy-Item "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/LongYinRoster.dll" "$dist/BepInEx/plugins/LongYinRoster/"
Copy-Item "README.md" "$dist/"
```

- [ ] **Step 2: zip 만들기**

```powershell
Compress-Archive -Path "dist/LongYinRoster_v0.3.0/*" -DestinationPath "dist/LongYinRoster_v0.3.0.zip" -Force
```

- [ ] **Step 3: tag + push**

```bash
git tag v0.3.0
git push origin v0.3
git push origin v0.3.0
```

(remote 가 main 만 받는다면 PR 생성 후 merge → tag 후 push.)

- [ ] **Step 4: GitHub release 생성**

Run:
```bash
gh release create v0.3.0 dist/LongYinRoster_v0.3.0.zip \
  --title "v0.3.0 — Apply (slot → game) + Restore" \
  --notes "$(cat <<'EOF'
## Highlights
- Apply (slot → game) 흐름 정상 작동 — JSON deserialize 대신 game-self method 호출
- Restore (slot 0 → game) — Apply 직전 상태로 자동/수동 복귀
- save → reload 후 캐릭터 정보창 정상 (v0.2 시도 2 의 실패점 통과)
- 지원 필드 매트릭스 명시 (docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md §7.2)

## 자세한 변경
- PinpointPatcher 7-step pipeline (SetSimpleFields → RebuildKungfuSkills → RebuildItemList → RebuildSelfStorage → RebuildHeroTagData → RefreshSelfState → RefreshExternalManagers)
- Apply 실패 시 슬롯 0 자동복원 (AutoBackupBeforeApply=true 시)
- AllowApplyToGame config kill switch
- 임시 진단 도구 ([F12] HeroDataDump, [F11+x] step smoke) 는 release zip 에서 제거

## 비지원 (v0.4+ 후보)
- spec §12 + §7.2 의 ⚪ 분류 필드 (dump 후 method 미발견)
- Detail panel 의 "마지막 Apply 결과" 섹션, Apply preview, selective apply

## 설치
Release zip 의 `BepInEx/` 를 게임 루트에 덮어쓰기.

## 문서
- README.md — 사용법
- docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md — 설계
- docs/HANDOFF.md — 작업 상태
- docs/HeroData-methods.md — game binary method reference
EOF
)"
```

- [ ] **Step 5: 최종 verify**

```bash
gh release view v0.3.0
git log --oneline -5
```
Expected: release 페이지 정상 + 최근 5 commit 에 v0.3 plan task 들 표시.

- [ ] **Step 6: Commit (dist 산출물은 .gitignore — commit 안 함)**

dist/ 는 .gitignore 에 있으므로 commit 없음. 이 task 는 release artifacts 만 생성.

---

## 완료 게이트

v0.3.0 release 완료 조건:
- [ ] `dotnet test` → `Passed: 24, Failed: 0`
- [ ] Phase A/B/C/D smoke 모두 [x] (`docs/superpowers/specs/2026-04-29-v0.3-smoke.md`)
- [ ] BepInEx 로그에 unhandled exception 0 (전체 smoke 동안)
- [ ] `grep -rn "HeroDataDump\|smoke C5 forced\|F11 + [SKIGTRE]" src/` → 빈 출력
- [ ] git tag `v0.3.0` push 됨
- [ ] GitHub release 페이지 정상

위 모두 통과하면 v0.3.0 ship 완료. HANDOFF 의 다음 세션 영역 = v0.4 후보 (spec §12) 또는 닫음.
