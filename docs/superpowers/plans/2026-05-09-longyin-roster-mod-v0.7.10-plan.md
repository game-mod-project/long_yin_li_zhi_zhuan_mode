# LongYinRoster v0.7.10 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 1 = 천부 max 보유수 lock (`HeroData.GetMaxTagNum` Postfix override, Player heroID=0 only, ConfigEntry 영속). Phase 2 = 속성 6 / 무학 9 / 기예 9 의 수치 (base) + 자질값 (max) inline editor — PlayerEditorPanel secondary tab `[기본 / 속성]`, [저장] gated apply, cheat `ChangeAttri/FightSkill/LivingSkill` 패턴 100% mirror.

**Architecture:** Phase 1 = 신규 `Core/GetMaxTagNumPatch.cs` (Harmony Postfix, RestKeepHeroTagPatch 와 동일 manual register 패턴) + Config 2 entries + PlayerEditorPanel 천부 섹션 헤더에 `[☐ Lock max] [ 999 ]` 토글 추가 (즉시 적용, ConfigEntry 자동 영속). Phase 2 = 신규 `Core/HeroAttriReflector` (3 axis × base/max/heroBuff read) + `Core/CharacterAttriEditor` (cheat ChangeAttri/FightSkill/LivingSkill mirror + sanitize) + `Util/AttriLabels` (24 hardcoded 한글) + `UI/AttriTabPanel` (3-column inline TextField + 일괄 button + [저장]/[되돌리기]) + PlayerEditorPanel 헤더에 `[기본][속성]` secondary tab 추가. v0.7.6 자산 (dirty + [저장] gated, ConfigEntry 즉시 영속) + v0.7.7/v0.7.8 자산 (RefreshMaxAttriAndSkill helper) 100% 재사용.

**Tech Stack:** C# 11 / .NET Standard 2.1, BepInEx 6 IL2CPP, HarmonyLib (manual register, NOT attribute-based — generic game type), Newtonsoft.Json (IL2CPP), xUnit + Shouldly (tests with POCO mocks).

**Spec:** [`2026-05-09-longyin-roster-mod-v0.7.10-design.md`](../specs/2026-05-09-longyin-roster-mod-v0.7.10-design.md)

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `src/LongYinRoster/Config.cs` | modify | Add `LockMaxTagNum` (bool) + `LockedMaxTagNumValue` (int) ConfigEntry |
| `src/LongYinRoster/Plugin.cs` | modify | Register `GetMaxTagNumPatch` via manual Harmony patch (RestKeepHeroTagPatch mirror) |
| `src/LongYinRoster/Core/GetMaxTagNumPatch.cs` | create | Phase 1 — `HeroData.GetMaxTagNum` Postfix override (cheat 100% mirror) |
| `src/LongYinRoster/Core/PlayerEditApplier.cs` | modify | Expose `TryInvokeRefreshMaxAttriAndSkill` → `public static RefreshMaxAttriAndSkill(player)` for Phase 2 reuse |
| `src/LongYinRoster/Util/AttriLabels.cs` | create | Hardcoded 24 한글 labels (속성 6 / 무학 9 / 기예 9). HangulDict optional fallback. |
| `src/LongYinRoster/Core/HeroAttriReflector.cs` | create | Reflection read of `baseAttri[i]` / `maxAttri[i]` / `baseFightSkill[i]` / `maxFightSkill[i]` / `baseLivingSkill[i]` / `maxLivingSkill[i]` / `heroBuff[idx]` |
| `src/LongYinRoster/Core/CharacterAttriEditor.cs` | create | Phase 2 — `ChangeAttri/FightSkill/LivingSkill(hero, idx, val)` (cheat CharacterFeature.cs:1341-1383 mirror) + clamp [0, 999999] + sanitize batch |
| `src/LongYinRoster/UI/AttriTabPanel.cs` | create | Phase 2 — secondary tab 의 3-column row drawing + buffer + 일괄 button + [저장]/[되돌리기] |
| `src/LongYinRoster/UI/PlayerEditorPanel.cs` | modify | Header 에 `[기본][속성]` secondary tab + 천부 섹션 헤더에 `[☐ Lock max] [ 999 ]` |
| `src/LongYinRoster.Tests/GetMaxTagNumPatchTests.cs` | create | 5 tests — toggle off / on / heroID mismatch / value=0 / null player |
| `src/LongYinRoster.Tests/HeroAttriReflectorTests.cs` | create | 6 tests — POCO mock + index 6/9/9 verify |
| `src/LongYinRoster.Tests/CharacterAttriEditorTests.cs` | create | 10 tests — clamp / dirty / 일괄 / sanitize 1회 / 비숫자 무시 |
| `src/LongYinRoster.Tests/AttriTabPanelBufferTests.cs` | create | 4 tests — buffer load / dirty 추적 / 일괄 button / [되돌리기] |
| `README.md` | modify | v0.7.10 기능 추가 |
| `dist/release-notes-v0.7.10.md` | create | release notes |

**Total**: 6 new source files / 4 modified source files / 4 new test files / 2 modified docs.
**Test count delta**: 327 → ~352 (+25). Approximate.

---

## Test Conventions

- xUnit (`[Fact]`) + Shouldly (`.ShouldBe(...)`)
- POCO mock (private nested classes with public fields mirroring game property camelCase)
- Test class names = `{TargetClass}Tests`
- IL2CPP runtime-only paths (RefreshMaxAttriAndSkill, Harmony patch result) → 인게임 smoke

---

## Phase 1 — LockedMax (천부 max 보유수)

### Task 1: Config — Add LockMaxTagNum + LockedMaxTagNumValue

**Files:**
- Modify: `src/LongYinRoster/Config.cs:39-58` (after v0.7.8 PlayerEditorPanel section, before ContainerPanel section)

- [ ] **Step 1: Write the failing test**

Create `src/LongYinRoster.Tests/GetMaxTagNumPatchTests.cs` with stub (will be filled in Task 4).

```csharp
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class GetMaxTagNumPatchTests
{
    [Fact]
    public void Postfix_LockOff_DoesNotModifyResult()
    {
        // Stub — fill after GetMaxTagNumPatch exists.
        true.ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it passes (sanity)**

Run: `dotnet test --filter "FullyQualifiedName~GetMaxTagNumPatchTests" -v normal`
Expected: PASS (stub).

- [ ] **Step 3: Modify Config.cs — declare ConfigEntry fields**

Add field declarations after line 45 (after `PlayerEditorPanelOpen`):

```csharp
    // v0.7.10 Phase 1 — Lock 천부 max 보유수 (cheat GameplayPatch.GetMaxTagNum mirror)
    public static ConfigEntry<bool>    LockMaxTagNum         = null!;
    public static ConfigEntry<int>     LockedMaxTagNumValue  = null!;
```

- [ ] **Step 4: Modify Config.cs — Bind() body**

Add after line 115 (after `PlayerEditorPanelOpen` bind), before `ContainerPanelX`:

```csharp
        // v0.7.10 Phase 1 — Lock 천부 max 보유수
        LockMaxTagNum         = cfg.Bind("Hero", "LockMaxTagNum",         false,
                                         "천부 max 보유수 lock — true 시 GetMaxTagNum() Postfix 가 LockedMaxTagNumValue 로 override (Player heroID=0 only)");
        LockedMaxTagNumValue  = cfg.Bind("Hero", "LockedMaxTagNumValue",  999,
                                         new ConfigDescription(
                                             "LockMaxTagNum=true 시 적용할 천부 max 값 (1~999999)",
                                             new AcceptableValueRange<int>(1, 999999)));
```

- [ ] **Step 5: Build to verify Config compiles**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
Expected: PASS, 0 warnings new.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Config.cs src/LongYinRoster.Tests/GetMaxTagNumPatchTests.cs
git commit -m "feat(config): v0.7.10 Phase 1 — LockMaxTagNum + LockedMaxTagNumValue ConfigEntry"
```

---

### Task 2: Core — GetMaxTagNumPatch.cs (Harmony Postfix)

**Files:**
- Create: `src/LongYinRoster/Core/GetMaxTagNumPatch.cs`

- [ ] **Step 1: Write the test (fail-then-pass)**

Replace stub in `src/LongYinRoster.Tests/GetMaxTagNumPatchTests.cs` with concrete tests using a static-mock approach (since Postfix is `internal static` and depends on Plugin.Config + HeroLocator):

```csharp
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.10 Phase 1 — GetMaxTagNumPatch Postfix 동작 검증.
/// IL2CPP HeroData 인자는 mock 으로 대체 — Postfix 의 분기 로직만 검증.
/// 실제 Harmony attach + game runtime 호출은 인게임 smoke.
/// </summary>
public class GetMaxTagNumPatchTests
{
    private sealed class FakeHero { public int heroID; }

    [Fact]
    public void Apply_LockOff_ReturnsOriginal()
    {
        int result = 30;
        GetMaxTagNumPatch.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: false, lockedValue: 999, playerHeroID: 0, ref result);
        result.ShouldBe(30);
    }

    [Fact]
    public void Apply_LockOnPlayerMatch_OverridesResult()
    {
        int result = 30;
        GetMaxTagNumPatch.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: true, lockedValue: 999, playerHeroID: 0, ref result);
        result.ShouldBe(999);
    }

    [Fact]
    public void Apply_LockOnHeroIDMismatch_DoesNotOverride()
    {
        int result = 30;
        GetMaxTagNumPatch.ApplyOverride(new FakeHero { heroID = 5 },
            isLocked: true, lockedValue: 999, playerHeroID: 0, ref result);
        result.ShouldBe(30);
    }

    [Fact]
    public void Apply_LockOnValueZero_DoesNotOverride()
    {
        int result = 30;
        GetMaxTagNumPatch.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: true, lockedValue: 0, playerHeroID: 0, ref result);
        result.ShouldBe(30);
    }

    [Fact]
    public void Apply_LockOnPlayerNull_DoesNotOverride()
    {
        int result = 30;
        // playerHeroID=-1 simulates "player null" (HeroLocator.GetPlayer() returned null).
        GetMaxTagNumPatch.ApplyOverride(new FakeHero { heroID = 0 },
            isLocked: true, lockedValue: 999, playerHeroID: -1, ref result);
        result.ShouldBe(30);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~GetMaxTagNumPatchTests" -v normal`
Expected: FAIL (CS0103 — `ApplyOverride` not defined).

- [ ] **Step 3: Implement GetMaxTagNumPatch.cs**

Create `src/LongYinRoster/Core/GetMaxTagNumPatch.cs`:

```csharp
using System;
using System.Reflection;
using HarmonyLib;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 1 — `HeroData.GetMaxTagNum()` Harmony Postfix.
///
/// cheat LongYinCheat.Patches.GameplayPatch.GetMaxTagNumPostfix (line 84-100) 의 100% mirror.
/// Plugin.Config.LockMaxTagNum=true + LockedMaxTagNumValue&gt;0 + __instance.heroID == player.heroID
/// 일 때 __result 를 LockedMaxTagNumValue 로 override.
///
/// Player 만 적용 — NPC 의 GetMaxTagNum 호출에는 무간섭 (mental model 분리,
/// brainstorm Q2=A 결정).
///
/// 등록 = manual via Plugin.cs Harmony.Patch (RestKeepHeroTagPatch mirror — generic
/// game type 은 attribute 기반 patch 불가).
/// </summary>
public static class GetMaxTagNumPatch
{
    /// <summary>Plugin.cs 에서 manual register 호출.</summary>
    public static void Register(Harmony harmony)
    {
        try
        {
            var heroDataType = AccessTools.TypeByName("HeroData");
            if (heroDataType == null)
            {
                Logger.Warn("GetMaxTagNumPatch: HeroData type not found — skip");
                return;
            }
            var m = AccessTools.Method(heroDataType, "GetMaxTagNum");
            if (m == null)
            {
                Logger.Warn("GetMaxTagNumPatch: HeroData.GetMaxTagNum not found — skip");
                return;
            }
            var postfix = new HarmonyMethod(typeof(GetMaxTagNumPatch).GetMethod(
                nameof(Postfix),
                BindingFlags.Static | BindingFlags.Public));
            harmony.Patch(m, postfix: postfix);
            Logger.Info("GetMaxTagNumPatch: HeroData.GetMaxTagNum patched");
        }
        catch (Exception ex)
        {
            Logger.Warn($"GetMaxTagNumPatch.Register threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Harmony Postfix — `__instance` = caller HeroData, `__result` = original return.</summary>
    public static void Postfix(object __instance, ref int __result)
    {
        try
        {
            bool isLocked  = Config.LockMaxTagNum.Value;
            int  lockedVal = Config.LockedMaxTagNumValue.Value;
            var  player    = HeroLocator.GetPlayer();
            int  pid       = player == null ? -1 : ReadHeroID(player);
            int  hid       = ReadHeroID(__instance);
            ApplyOverride(__instance, isLocked, lockedVal, pid, ref __result, hid);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("GetMaxTagNumPatch", $"Postfix threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Test-friendly extracted core. heroID 가 missing 시 -2 default.</summary>
    public static void ApplyOverride(object instance, bool isLocked, int lockedValue,
                                     int playerHeroID, ref int result, int instanceHeroID = -2)
    {
        if (!isLocked) return;
        if (lockedValue <= 0) return;
        if (playerHeroID < 0) return;          // player null
        if (instanceHeroID == -2) instanceHeroID = ReadHeroID(instance);
        if (instanceHeroID != playerHeroID) return;
        result = lockedValue;
    }

    private static int ReadHeroID(object instance)
    {
        if (instance == null) return -1;
        try
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var t = instance.GetType();
            var f = t.GetField("heroID", F);
            if (f != null) { var v = f.GetValue(instance); if (v is int i) return i; }
            var p = t.GetProperty("heroID", F);
            if (p != null) { var v = p.GetValue(instance); if (v is int i) return i; }
        }
        catch { }
        return -1;
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~GetMaxTagNumPatchTests" -v normal`
Expected: 5 tests PASS.

- [ ] **Step 5: Build**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Core/GetMaxTagNumPatch.cs src/LongYinRoster.Tests/GetMaxTagNumPatchTests.cs
git commit -m "feat(core): v0.7.10 Phase 1 — GetMaxTagNumPatch (cheat GameplayPatch mirror)"
```

---

### Task 3: Plugin — Register GetMaxTagNumPatch

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs:28-29` (after RestKeepHeroTagPatch.Register)

- [ ] **Step 1: Modify Plugin.cs**

Add after line 28 (`Core.RestKeepHeroTagPatch.Register(harmony);`):

```csharp
        Core.GetMaxTagNumPatch.Register(harmony);
```

Update VERSION constant on line 17:

```csharp
    public const string VERSION = "0.7.10";
```

Add to Logger.Info bottom of Load() (after v0.7.8 line):

```csharp
        Logger.Info("[v0.7.10] GetMaxTagNumPatch registered + AttriTabPanel ready ([기본]/[속성] tab)");
```

- [ ] **Step 2: Build**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
Expected: PASS.

- [ ] **Step 3: Run all tests (regression)**

Run: `dotnet test`
Expected: 327 + 5 = 332 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Plugin.cs
git commit -m "feat(plugin): v0.7.10 Phase 1 — Plugin.Load register GetMaxTagNumPatch + VERSION 0.7.10"
```

---

### Task 4: PlayerEditorPanel — 천부 섹션 헤더 lock 토글

**Files:**
- Modify: `src/LongYinRoster/UI/PlayerEditorPanel.cs` — `DrawHeroTagDataSection` (around line 379) + add `_lockValueBuffer` private field

- [ ] **Step 1: Read current DrawHeroTagDataSection signature + content**

Run: `grep -n "DrawHeroTagDataSection\|_lockValueBuffer\|_sectionOpen" src/LongYinRoster/UI/PlayerEditorPanel.cs | head -30`
Note current header line where `천부 (N/M)` is rendered.

- [ ] **Step 2: Add `_lockValueBuffer` private field**

Find the private field declarations near top of the class (before constructor). Add:

```csharp
    /// <summary>v0.7.10 Phase 1 — TextField buffer for LockedMaxTagNumValue input.</summary>
    private string _lockValueBuffer = "";
```

- [ ] **Step 3: Modify DrawHeroTagDataSection — add lock row**

Inside `DrawHeroTagDataSection(object player)`, find the `천부` 헤더 row (search for `"천부"`). After the existing header label row but BEFORE the existing tag list/add rows, insert:

```csharp
        // v0.7.10 Phase 1 — Lock max 보유수 토글 + 값
        GUILayout.BeginHorizontal();
        GUILayout.Space(8);
        bool prevLock = Config.LockMaxTagNum.Value;
        bool newLock  = GUILayout.Toggle(prevLock, "Lock max", GUILayout.Width(80));
        if (newLock != prevLock) Config.LockMaxTagNum.Value = newLock;

        // sync buffer with persisted ConfigEntry on first draw / external change
        string persistedStr = Config.LockedMaxTagNumValue.Value.ToString();
        if (string.IsNullOrEmpty(_lockValueBuffer) || (!_isUserEditingLock && _lockValueBuffer != persistedStr))
            _lockValueBuffer = persistedStr;

        string newBuf = GUILayout.TextField(_lockValueBuffer, 6, GUILayout.Width(64));
        if (newBuf != _lockValueBuffer)
        {
            _lockValueBuffer = newBuf;
            _isUserEditingLock = true;
            if (int.TryParse(newBuf, out int v) && v >= 1 && v <= 999999)
                Config.LockedMaxTagNumValue.Value = v;
        }
        GUILayout.Label("(1~999999)", GUILayout.Width(80));
        GUILayout.FlexibleSpace();  // strip-safe? — replace with GUILayout.Space(...)
        GUILayout.EndHorizontal();
```

**WAIT — strip-safe 검증**: HANDOFF §4.3 + memory 에 `GUILayout.FlexibleSpace()` ❌ strip 됨. 위 코드에서 `GUILayout.FlexibleSpace()` 를 `GUILayout.Space(8)` (또는 적절한 픽셀값) 으로 교체.

```csharp
        // 위 FlexibleSpace 자리:
        GUILayout.Space(8);  // strip-safe alternative
        GUILayout.EndHorizontal();
```

Add the editing-flag field near `_lockValueBuffer`:

```csharp
    private bool _isUserEditingLock;
```

- [ ] **Step 4: Build**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
Expected: PASS.

- [ ] **Step 5: Run all tests (regression — v0.7.8 PlayerEditorPanel 회귀 검증)**

Run: `dotnet test`
Expected: 332 PASS (no new tests for UI here — IMGUI 인게임 smoke).

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/UI/PlayerEditorPanel.cs
git commit -m "feat(ui): v0.7.10 Phase 1 — PlayerEditorPanel 천부 섹션 헤더 [Lock max] 토글 + TextField"
```

---

### Task 5: Phase 1 — 인게임 smoke

**Files:**
- Create: `docs/superpowers/dumps/2026-05-09-v0.7.10-phase1-smoke.md` (placeholder, fill during smoke run)

- [ ] **Step 1: Build deploy**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
(Build 자동으로 `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 배포 — Directory.Build.props 의 GameDir env-var 또는 자동 path)

- [ ] **Step 2: Game launch + 인게임 smoke**

게임 실행 → 적당한 save load → F11 → 플레이어 편집 (F11+4) → 천부 섹션 헤더 시각 검증:
1. `[☐ Lock max] [ 999 ] (1~999999)` 표시 확인
2. 체크 ON → 값 999 → ConfigEntry write 확인 (`BepInEx/config/com.deepe.longyinroster.cfg` 에 `LockMaxTagNum = true` / `LockedMaxTagNumValue = 999`)
3. 게임 안에서 천부 추가 → 999 까지 추가 가능 검증 (인게임 method 의존 — 정확한 테스트는 999 까지 stack 가능 vs 기존 limit 30 비교)
4. uncheck → 다음 천부 추가 시 원래 limit 복귀
5. heroID 0 외 NPC 의 천부 시스템 무영향 검증 (간접 — NPC 의 GetMaxTagNum 호출이 정상 limit 반환 가정)

- [ ] **Step 3: Log capture**

```bash
# 게임 종료 후
cp "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" \
   docs/superpowers/dumps/2026-05-09-v0.7.10-phase1-LogOutput.log
```

- [ ] **Step 4: Smoke 결과 기록**

Edit `docs/superpowers/dumps/2026-05-09-v0.7.10-phase1-smoke.md`:

```markdown
# v0.7.10 Phase 1 인게임 smoke

**일시**: 2026-05-09
**baseline**: v0.7.8 + Phase 1 commits

## 검증 항목

| # | 항목 | 결과 |
|---|---|---|
| 1 | 천부 섹션 헤더 [Lock max] 토글 + TextField 표시 | PASS / FAIL |
| 2 | 체크 ON → ConfigEntry 즉시 write | PASS / FAIL |
| 3 | 게임 안 천부 추가 시 999 limit 적용 | PASS / FAIL |
| 4 | uncheck → 원래 limit 복귀 | PASS / FAIL |
| 5 | NPC 천부 무영향 (heroID 분기) | PASS / FAIL |
| 6 | LogOutput 에 `Method unstripping failed` 0건 | PASS / FAIL |
| 7 | LogOutput 에 `GetMaxTagNumPatch: HeroData.GetMaxTagNum patched` 1건 | PASS / FAIL |
| 8 | v0.7.8 PlayerEditorPanel 6 섹션 회귀 (Resource/SpeAddData/천부 추가/무공/Breakthrough) | PASS / FAIL |

## 결론
TBD (smoke 후 채움)
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/dumps/2026-05-09-v0.7.10-phase1-smoke.md docs/superpowers/dumps/2026-05-09-v0.7.10-phase1-LogOutput.log
git commit -m "docs(smoke): v0.7.10 Phase 1 인게임 smoke — Lock max 토글 + ConfigEntry"
```

---

## Phase 2 — 속성·무학·기예 editor

### Task 6: Util — AttriLabels.cs (24 한글 라벨)

**Files:**
- Create: `src/LongYinRoster/Util/AttriLabels.cs`

- [ ] **Step 1: Write the test**

Create `src/LongYinRoster.Tests/AttriLabelsTests.cs`:

```csharp
using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class AttriLabelsTests
{
    [Fact]
    public void Attri_Returns6Labels()
    {
        AttriLabels.Attri.Length.ShouldBe(6);
        AttriLabels.Attri[0].ShouldBe("근력");
        AttriLabels.Attri[5].ShouldBe("경맥");
    }

    [Fact]
    public void FightSkill_Returns9Labels()
    {
        AttriLabels.FightSkill.Length.ShouldBe(9);
        AttriLabels.FightSkill[0].ShouldBe("내공");
        AttriLabels.FightSkill[8].ShouldBe("사술");
    }

    [Fact]
    public void LivingSkill_Returns9Labels()
    {
        AttriLabels.LivingSkill.Length.ShouldBe(9);
        AttriLabels.LivingSkill[0].ShouldBe("의술");
        AttriLabels.LivingSkill[8].ShouldBe("요리");
    }

    [Theory]
    [InlineData(AttriAxis.Attri, 0, "근력")]
    [InlineData(AttriAxis.Attri, 3, "의지")]
    [InlineData(AttriAxis.FightSkill, 4, "검법")]
    [InlineData(AttriAxis.LivingSkill, 6, "단조")]
    public void For_ReturnsLabelByAxisIndex(AttriAxis axis, int idx, string expected)
        => AttriLabels.For(axis, idx).ShouldBe(expected);

    [Theory]
    [InlineData(AttriAxis.Attri, 6)]
    [InlineData(AttriAxis.Attri, -1)]
    [InlineData(AttriAxis.FightSkill, 9)]
    public void For_OutOfRange_ReturnsBracketedIndex(AttriAxis axis, int idx)
        => AttriLabels.For(axis, idx).ShouldStartWith("[");
}
```

- [ ] **Step 2: Run test (fail)**

Run: `dotnet test --filter "FullyQualifiedName~AttriLabelsTests" -v normal`
Expected: FAIL — `AttriLabels` / `AttriAxis` not defined.

- [ ] **Step 3: Implement AttriLabels.cs**

Create `src/LongYinRoster/Util/AttriLabels.cs`:

```csharp
namespace LongYinRoster.Util;

/// <summary>v0.7.10 Phase 2 — 속성/무학/기예 axis enum.</summary>
public enum AttriAxis
{
    Attri,        // 속성 6 (baseAttri)
    FightSkill,   // 무학 9 (baseFightSkill)
    LivingSkill,  // 기예 9 (baseLivingSkill)
}

/// <summary>
/// v0.7.10 Phase 2 — 24 hardcoded 한글 라벨.
///
/// 게임 LTLocalization / HangulDict 가 라벨을 반환하지 못 하는 경우의 fallback.
/// PlayerEditorPanel 의 [속성] secondary tab 에서 row 라벨로 사용.
/// </summary>
public static class AttriLabels
{
    public static readonly string[] Attri = new[]
    {
        "근력", "민첩", "지력", "의지", "체질", "경맥",
    };

    public static readonly string[] FightSkill = new[]
    {
        "내공", "경공", "절기", "권장", "검법", "도법", "장병", "기문", "사술",
    };

    public static readonly string[] LivingSkill = new[]
    {
        "의술", "독술", "학식", "언변", "채벌", "목식", "단조", "제약", "요리",
    };

    public static string For(AttriAxis axis, int idx)
    {
        var arr = axis switch
        {
            AttriAxis.Attri => Attri,
            AttriAxis.FightSkill => FightSkill,
            AttriAxis.LivingSkill => LivingSkill,
            _ => null,
        };
        if (arr == null) return $"[axis={axis}]";
        if (idx < 0 || idx >= arr.Length) return $"[idx={idx}]";
        return arr[idx];
    }

    public static int Count(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => Attri.Length,
        AttriAxis.FightSkill => FightSkill.Length,
        AttriAxis.LivingSkill => LivingSkill.Length,
        _ => 0,
    };
}
```

- [ ] **Step 4: Run tests (pass)**

Run: `dotnet test --filter "FullyQualifiedName~AttriLabelsTests" -v normal`
Expected: 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Util/AttriLabels.cs src/LongYinRoster.Tests/AttriLabelsTests.cs
git commit -m "feat(util): v0.7.10 Phase 2 — AttriLabels 24 한글 (속성 6 / 무학 9 / 기예 9) + AttriAxis enum"
```

---

### Task 7: Core — HeroAttriReflector.cs

**Files:**
- Create: `src/LongYinRoster/Core/HeroAttriReflector.cs`

- [ ] **Step 1: Write the test**

Create `src/LongYinRoster.Tests/HeroAttriReflectorTests.cs`:

```csharp
#pragma warning disable CS0649

using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class HeroAttriReflectorTests
{
    private sealed class FakeHero
    {
        public List<float> baseAttri        = new() { 199f, 165f, 196f, 183f, 210f, 186f };
        public List<float> maxAttri         = new() { 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseFightSkill   = new() { 155f, 155f, 155f, 162f, 168f, 153f, 315f, 155f, 160f };
        public List<float> maxFightSkill    = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseLivingSkill  = new() { 455f, 255f, 999f, 999f, 702f, 915f, 999f, 250f, 250f };
        public List<float> maxLivingSkill   = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
    }

    [Fact]
    public void GetCount_Attri_Returns6()
        => HeroAttriReflector.GetCount(new FakeHero(), AttriAxis.Attri).ShouldBe(6);

    [Fact]
    public void GetCount_FightSkill_Returns9()
        => HeroAttriReflector.GetCount(new FakeHero(), AttriAxis.FightSkill).ShouldBe(9);

    [Fact]
    public void GetCount_LivingSkill_Returns9()
        => HeroAttriReflector.GetCount(new FakeHero(), AttriAxis.LivingSkill).ShouldBe(9);

    [Theory]
    [InlineData(AttriAxis.Attri, 0, 199f, 999f)]
    [InlineData(AttriAxis.Attri, 3, 183f, 999f)]
    [InlineData(AttriAxis.FightSkill, 6, 315f, 999f)]
    [InlineData(AttriAxis.LivingSkill, 5, 915f, 999f)]
    public void GetEntry_ReturnsBaseAndMax(AttriAxis axis, int idx, float baseExp, float maxExp)
    {
        var (b, m) = HeroAttriReflector.GetEntry(new FakeHero(), axis, idx);
        b.ShouldBe(baseExp);
        m.ShouldBe(maxExp);
    }

    [Fact]
    public void GetEntry_NullHero_ReturnsZeros()
    {
        var (b, m) = HeroAttriReflector.GetEntry(null!, AttriAxis.Attri, 0);
        b.ShouldBe(0f);
        m.ShouldBe(0f);
    }

    [Fact]
    public void GetEntry_OutOfRange_ReturnsZeros()
    {
        var (b, m) = HeroAttriReflector.GetEntry(new FakeHero(), AttriAxis.Attri, 99);
        b.ShouldBe(0f);
        m.ShouldBe(0f);
    }
}
```

- [ ] **Step 2: Run test (fail)**

Run: `dotnet test --filter "FullyQualifiedName~HeroAttriReflectorTests" -v normal`
Expected: FAIL — `HeroAttriReflector` not defined.

- [ ] **Step 3: Implement HeroAttriReflector.cs**

Create `src/LongYinRoster/Core/HeroAttriReflector.cs`:

```csharp
using System;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 2 — `HeroData.baseAttri[i]` / `maxAttri[i]` / `baseFightSkill[i]` /
/// `maxFightSkill[i]` / `baseLivingSkill[i]` / `maxLivingSkill[i]` reflection read.
///
/// cheat CharacterFeature.cs:1341-1383 (`ChangeAttri` / `ChangeFightSkill` / `ChangeLivingSkill`)
/// 의 read path 와 동일 — `hero.{base|max}Xxx` field/property → indexed list.
///
/// IL2CPP `Il2CppSystem.Collections.Generic.List&lt;float&gt;` 는 .NET IEnumerable 미구현 →
/// `Count` property + `get_Item(int)` indexer 사용.
///
/// heroBuff 는 별도 — `HeroSpeAddDataReflector` 의 idx lookup 으로 fallback. axis idx 와
/// SpeAddType idx 매칭 미확인 (Risk §7.6) — heroBuff 표시 못 하면 0 반환.
/// </summary>
public static class HeroAttriReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static int GetCount(object hero, AttriAxis axis)
    {
        if (hero == null) return 0;
        try
        {
            var list = ReadFieldOrProperty(hero, BaseFieldName(axis));
            if (list == null) return 0;
            var countProp = list.GetType().GetProperty("Count", F);
            if (countProp == null) return 0;
            return Convert.ToInt32(countProp.GetValue(list));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroAttriReflector", $"GetCount({axis}): {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    public static (float Base, float Max) GetEntry(object hero, AttriAxis axis, int idx)
    {
        if (hero == null) return (0f, 0f);
        try
        {
            var baseList = ReadFieldOrProperty(hero, BaseFieldName(axis));
            var maxList  = ReadFieldOrProperty(hero, MaxFieldName(axis));
            float b = ReadIndexedFloat(baseList, idx);
            float m = ReadIndexedFloat(maxList, idx);
            return (b, m);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroAttriReflector", $"GetEntry({axis},{idx}): {ex.GetType().Name}: {ex.Message}");
            return (0f, 0f);
        }
    }

    /// <summary>heroBuff[axis idx] 시도 — HeroSpeAddData 가 axis idx 와 매칭 안 되면 0.</summary>
    public static float GetBuff(object hero, AttriAxis axis, int idx)
    {
        if (hero == null) return 0f;
        try
        {
            var heroBuff = ReadFieldOrProperty(hero, "heroBuff");
            if (heroBuff == null) return 0f;
            var getM = heroBuff.GetType().GetMethod("Get", F, null, new[] { typeof(int) }, null);
            if (getM == null) return 0f;
            int speIdx = AxisToSpeAddIdx(axis, idx);
            if (speIdx < 0) return 0f;
            var v = getM.Invoke(heroBuff, new object[] { speIdx });
            return v is float f ? f : 0f;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroAttriReflector", $"GetBuff({axis},{idx}): {ex.GetType().Name}: {ex.Message}");
            return 0f;
        }
    }

    private static string BaseFieldName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "baseAttri",
        AttriAxis.FightSkill => "baseFightSkill",
        AttriAxis.LivingSkill => "baseLivingSkill",
        _ => "",
    };

    private static string MaxFieldName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "maxAttri",
        AttriAxis.FightSkill => "maxFightSkill",
        AttriAxis.LivingSkill => "maxLivingSkill",
        _ => "",
    };

    /// <summary>
    /// axis idx → HeroSpeAddData type idx 변환. cheat CharacterFeature.cs SpeAddTypeNames
    /// 매핑 미확인 → 현재 axis idx 그대로 반환 (속성 0~5 = SpeAddType 0~5 추정). spike 검증 필요.
    /// 매칭 안 되면 GetBuff 가 비매칭 type 의 buff 반환 → 사용자에게 노이즈. spike 결과 따라
    /// switch 확장 또는 -1 반환 (buff 표시 안 함).
    /// </summary>
    private static int AxisToSpeAddIdx(AttriAxis axis, int idx)
    {
        // 추정 매핑 — 인게임 spike 후 정확화 (Spec §6 Spike #5).
        // 속성 0=근력 → SpeAddType 0?
        // 무학 0=내공 → SpeAddType ?
        // 기예 0=의술 → SpeAddType ?
        // 일단 axis 별 base offset + idx, 미검증 시 -1.
        return axis switch
        {
            AttriAxis.Attri => idx,                  // 추정 — spike 결과로 정확화
            AttriAxis.FightSkill => -1,              // 미확정 → buff 0 표시
            AttriAxis.LivingSkill => -1,             // 미확정 → buff 0 표시
            _ => -1,
        };
    }

    private static float ReadIndexedFloat(object list, int idx)
    {
        if (list == null) return 0f;
        var t = list.GetType();
        var countProp = t.GetProperty("Count", F);
        if (countProp == null) return 0f;
        int n = Convert.ToInt32(countProp.GetValue(list));
        if (idx < 0 || idx >= n) return 0f;
        var indexer = t.GetProperty("Item", F) ?? null;
        if (indexer != null)
        {
            var v = indexer.GetValue(list, new object[] { idx });
            return v is float f ? f : Convert.ToSingle(v);
        }
        var getItem = t.GetMethod("get_Item", F, null, new[] { typeof(int) }, null);
        if (getItem != null)
        {
            var v = getItem.Invoke(list, new object[] { idx });
            return v is float f ? f : Convert.ToSingle(v);
        }
        return 0f;
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
}
```

- [ ] **Step 4: Run tests (pass)**

Run: `dotnet test --filter "FullyQualifiedName~HeroAttriReflectorTests" -v normal`
Expected: 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Core/HeroAttriReflector.cs src/LongYinRoster.Tests/HeroAttriReflectorTests.cs
git commit -m "feat(core): v0.7.10 Phase 2 — HeroAttriReflector (baseAttri/maxAttri × 3 axis + heroBuff lookup)"
```

---

### Task 8: Core — Refactor PlayerEditApplier.TryInvokeRefreshMaxAttriAndSkill → public

**Files:**
- Modify: `src/LongYinRoster/Core/PlayerEditApplier.cs:255` (private → public + rename to `RefreshMaxAttriAndSkill`)

- [ ] **Step 1: Read current method signature**

Run: `grep -n "TryInvokeRefreshMaxAttriAndSkill" src/LongYinRoster/Core/PlayerEditApplier.cs`
Expected: 3 occurrences (1 declaration + 2 call sites at lines ~129, ~172).

- [ ] **Step 2: Modify PlayerEditApplier.cs — change private → public + add comment**

Change line 255 from:
```csharp
    private static bool TryInvokeRefreshMaxAttriAndSkill(object player)
```
To:
```csharp
    /// <summary>v0.7.10 Phase 2: 다른 editor (CharacterAttriEditor) 와 공유. v0.7.7 검증된 helper.</summary>
    public static bool RefreshMaxAttriAndSkill(object player)
```

Update internal call sites — find:
```csharp
        bool refreshed = TryInvokeRefreshMaxAttriAndSkill(player);
```
And:
```csharp
        if (any) TryInvokeRefreshMaxAttriAndSkill(player);
```

Replace `TryInvokeRefreshMaxAttriAndSkill` → `RefreshMaxAttriAndSkill` (2 sites).

- [ ] **Step 3: Build + run tests**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj && dotnet test`
Expected: PASS, all 332 + 11 (Tasks 6+7) = 343 tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Core/PlayerEditApplier.cs
git commit -m "refactor(core): v0.7.10 — PlayerEditApplier.RefreshMaxAttriAndSkill public (Phase 2 reuse)"
```

---

### Task 9: Core — CharacterAttriEditor.cs

**Files:**
- Create: `src/LongYinRoster/Core/CharacterAttriEditor.cs`

- [ ] **Step 1: Write the test**

Create `src/LongYinRoster.Tests/CharacterAttriEditorTests.cs`:

```csharp
#pragma warning disable CS0649

using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CharacterAttriEditorTests
{
    /// <summary>cheat ChangeAttri 패턴 mirror — game-self method 가 있는 mock.</summary>
    private sealed class FakeHeroWithMethods
    {
        public List<float> baseAttri       = new() { 199f, 165f, 196f, 183f, 210f, 186f };
        public List<float> maxAttri        = new() { 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseFightSkill  = new() { 155f, 155f, 155f, 162f, 168f, 153f, 315f, 155f, 160f };
        public List<float> maxFightSkill   = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseLivingSkill = new() { 455f, 255f, 999f, 999f, 702f, 915f, 999f, 250f, 250f };
        public List<float> maxLivingSkill  = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };

        public int ChangeAttriCalls;
        public int ChangeFightSkillCalls;
        public int ChangeLivingSkillCalls;

        public void ChangeAttri(int idx, float delta, bool a, bool b)
        {
            ChangeAttriCalls++;
            baseAttri[idx] += delta;
        }

        public void ChangeFightSkill(int idx, float delta, bool a, bool b)
        {
            ChangeFightSkillCalls++;
            baseFightSkill[idx] += delta;
        }

        public void ChangeLivingSkill(int idx, float delta, bool a, bool b)
        {
            ChangeLivingSkillCalls++;
            baseLivingSkill[idx] += delta;
        }
    }

    [Fact]
    public void Change_Attri_AppliesViaGameMethod()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.Attri, 0, 999f);
        ok.ShouldBeTrue();
        hero.baseAttri[0].ShouldBe(999f);
        hero.ChangeAttriCalls.ShouldBe(1);
    }

    [Fact]
    public void Change_FightSkill_AppliesViaGameMethod()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.FightSkill, 4, 500f);
        ok.ShouldBeTrue();
        hero.baseFightSkill[4].ShouldBe(500f);
        hero.ChangeFightSkillCalls.ShouldBe(1);
    }

    [Fact]
    public void Change_LivingSkill_AppliesViaGameMethod()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.LivingSkill, 8, 1500f);
        ok.ShouldBeTrue();
        hero.baseLivingSkill[8].ShouldBe(1500f);
        hero.ChangeLivingSkillCalls.ShouldBe(1);
    }

    [Fact]
    public void Change_ValueAboveMax_BumpsMaxFirst()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.Change(hero, AttriAxis.Attri, 0, 1500f);
        ok.ShouldBeTrue();
        hero.maxAttri[0].ShouldBe(1500f);
        hero.baseAttri[0].ShouldBe(1500f);
    }

    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(1000000f, 999999f)]
    public void Change_ClampsToValidRange(float input, float expected)
    {
        var hero = new FakeHeroWithMethods();
        CharacterAttriEditor.Change(hero, AttriAxis.Attri, 0, input);
        hero.baseAttri[0].ShouldBe(expected);
    }

    [Fact]
    public void TryParse_NonNumeric_ReturnsFalse()
        => CharacterAttriEditor.TryParseInput("abc", out _).ShouldBeFalse();

    [Fact]
    public void TryParse_Numeric_ReturnsTrueWithClampedValue()
    {
        CharacterAttriEditor.TryParseInput("12345", out float v).ShouldBeTrue();
        v.ShouldBe(12345f);
    }

    [Fact]
    public void TryParse_Empty_ReturnsFalse()
        => CharacterAttriEditor.TryParseInput("", out _).ShouldBeFalse();

    [Fact]
    public void ChangeMax_BumpsMaxOnly_DoesNotChangeBase()
    {
        var hero = new FakeHeroWithMethods();
        bool ok = CharacterAttriEditor.ChangeMax(hero, AttriAxis.Attri, 0, 5000f);
        ok.ShouldBeTrue();
        hero.maxAttri[0].ShouldBe(5000f);
        hero.baseAttri[0].ShouldBe(199f);  // unchanged
    }
}
```

- [ ] **Step 2: Run test (fail)**

Run: `dotnet test --filter "FullyQualifiedName~CharacterAttriEditorTests" -v normal`
Expected: FAIL — `CharacterAttriEditor` not defined.

- [ ] **Step 3: Implement CharacterAttriEditor.cs**

Create `src/LongYinRoster/Core/CharacterAttriEditor.cs`:

```csharp
using System;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.10 Phase 2 — `HeroData.ChangeAttri/FightSkill/LivingSkill(int idx, float delta, bool, bool)`
/// game-self method 호출 wrapper.
///
/// cheat CharacterFeature.cs:1341-1383 의 100% mirror:
/// <code>
///   if (hero.maxXxx[idx] &lt; value) hero.maxXxx[idx] = value;
///   hero.ChangeXxx(idx, value - hero.baseXxx[idx], false, false);
///   // fallback (game-self method 없을 때): hero.baseXxx[idx] = value;
/// </code>
///
/// Clamp [0, 999999] (cheat AddTalent 정렬). Sanitize (RefreshMaxAttriAndSkill) 는
/// AttriTabPanel 의 [저장] 클릭 시 1회 — 본 Editor 는 호출 안 함.
/// </summary>
public static class CharacterAttriEditor
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const float MIN = 0f;
    private const float MAX = 999999f;

    /// <summary>base 값 변경 — value 가 max 초과 시 max 도 같이 bump (cheat 패턴).</summary>
    public static bool Change(object hero, AttriAxis axis, int idx, float value)
    {
        if (hero == null) return false;
        value = Clamp(value);
        try
        {
            // 1. max bump if needed
            float curMax = HeroAttriReflector.GetEntry(hero, axis, idx).Max;
            if (curMax < value)
            {
                if (!ChangeMax(hero, axis, idx, value)) return false;
            }

            // 2. game-self method 우선
            float curBase = HeroAttriReflector.GetEntry(hero, axis, idx).Base;
            float delta = value - curBase;
            if (Math.Abs(delta) < 0.001f) return true;  // no-op

            string methodName = ChangeMethodName(axis);
            var m = hero.GetType().GetMethod(methodName, F, null,
                new[] { typeof(int), typeof(float), typeof(bool), typeof(bool) }, null);
            if (m != null)
            {
                m.Invoke(hero, new object[] { idx, delta, false, false });
                return true;
            }

            // 3. fallback — baseXxx[idx] 직접 set
            return SetIndexed(hero, BaseFieldName(axis), idx, value);
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("CharacterAttriEditor", $"Change({axis},{idx},{value}): {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>max 값 변경 — base 는 건드리지 않음.</summary>
    public static bool ChangeMax(object hero, AttriAxis axis, int idx, float value)
    {
        if (hero == null) return false;
        value = Clamp(value);
        return SetIndexed(hero, MaxFieldName(axis), idx, value);
    }

    /// <summary>TextField 입력 parse — 비숫자 / 빈 문자열 → false.</summary>
    public static bool TryParseInput(string input, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!float.TryParse(input, out var v)) return false;
        value = Clamp(v);
        return true;
    }

    private static float Clamp(float v) => Math.Max(MIN, Math.Min(MAX, v));

    private static string BaseFieldName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "baseAttri",
        AttriAxis.FightSkill => "baseFightSkill",
        AttriAxis.LivingSkill => "baseLivingSkill",
        _ => "",
    };

    private static string MaxFieldName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "maxAttri",
        AttriAxis.FightSkill => "maxFightSkill",
        AttriAxis.LivingSkill => "maxLivingSkill",
        _ => "",
    };

    private static string ChangeMethodName(AttriAxis axis) => axis switch
    {
        AttriAxis.Attri => "ChangeAttri",
        AttriAxis.FightSkill => "ChangeFightSkill",
        AttriAxis.LivingSkill => "ChangeLivingSkill",
        _ => "",
    };

    private static bool SetIndexed(object hero, string fieldName, int idx, float value)
    {
        try
        {
            var t = hero.GetType();
            var p = t.GetProperty(fieldName, F);
            object? list = p?.GetValue(hero);
            if (list == null)
            {
                var f = t.GetField(fieldName, F);
                list = f?.GetValue(hero);
            }
            if (list == null) return false;
            var setItem = list.GetType().GetMethod("set_Item", F, null,
                new[] { typeof(int), typeof(float) }, null);
            if (setItem != null)
            {
                setItem.Invoke(list, new object[] { idx, value });
                return true;
            }
            // Some IL2CPP wrappers expose Item indexer property only.
            var indexer = list.GetType().GetProperty("Item", F);
            if (indexer != null && indexer.CanWrite)
            {
                indexer.SetValue(list, value, new object[] { idx });
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("CharacterAttriEditor", $"SetIndexed({fieldName},{idx}): {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
```

- [ ] **Step 4: Run tests (pass)**

Run: `dotnet test --filter "FullyQualifiedName~CharacterAttriEditorTests" -v normal`
Expected: 10 tests PASS.

- [ ] **Step 5: Build + run all tests**

Run: `dotnet build -c Release && dotnet test`
Expected: 343 + 10 = 353 PASS.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Core/CharacterAttriEditor.cs src/LongYinRoster.Tests/CharacterAttriEditorTests.cs
git commit -m "feat(core): v0.7.10 Phase 2 — CharacterAttriEditor (cheat ChangeAttri/FightSkill/LivingSkill mirror + clamp)"
```

---

### Task 10: UI — AttriTabPanel.cs (3-column inline + 일괄 + [저장])

**Files:**
- Create: `src/LongYinRoster/UI/AttriTabPanel.cs`

- [ ] **Step 1: Write the buffer test**

Create `src/LongYinRoster.Tests/AttriTabPanelBufferTests.cs`:

```csharp
#pragma warning disable CS0649

using System.Collections.Generic;
using LongYinRoster.UI;
using LongYinRoster.Util;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class AttriTabPanelBufferTests
{
    private sealed class FakeHero
    {
        public List<float> baseAttri       = new() { 199f, 165f, 196f, 183f, 210f, 186f };
        public List<float> maxAttri        = new() { 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseFightSkill  = new() { 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f, 100f };
        public List<float> maxFightSkill   = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
        public List<float> baseLivingSkill = new() { 200f, 200f, 200f, 200f, 200f, 200f, 200f, 200f, 200f };
        public List<float> maxLivingSkill  = new() { 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f, 999f };
    }

    [Fact]
    public void LoadFromHero_PopulatesBuffer()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());

        buf.Get(AttriAxis.Attri, 0).BaseInput.ShouldBe("199");
        buf.Get(AttriAxis.Attri, 0).MaxInput.ShouldBe("999");
        buf.Get(AttriAxis.FightSkill, 0).BaseInput.ShouldBe("100");
        buf.Get(AttriAxis.LivingSkill, 8).BaseInput.ShouldBe("200");
        buf.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void SetInput_DifferentValue_FlagsDirty()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "999");
        buf.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void SetInput_SameValue_DoesNotFlagDirty()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "199");
        buf.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void BulkSetMax_AppliesToAllRows()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.BulkSetMax(AttriAxis.FightSkill, "9999");

        for (int i = 0; i < 9; i++)
            buf.Get(AttriAxis.FightSkill, i).MaxInput.ShouldBe("9999");
        buf.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void Reset_RestoresOriginals()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "999");
        buf.SetMaxInput(AttriAxis.Attri, 0, "9999");
        buf.IsDirty.ShouldBeTrue();

        buf.Reset();

        buf.Get(AttriAxis.Attri, 0).BaseInput.ShouldBe("199");
        buf.Get(AttriAxis.Attri, 0).MaxInput.ShouldBe("999");
        buf.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void DirtyRows_ReturnsOnlyChanged()
    {
        var buf = new AttriTabBuffer();
        buf.LoadFromHero(new FakeHero());
        buf.SetBaseInput(AttriAxis.Attri, 0, "999");
        buf.SetMaxInput(AttriAxis.FightSkill, 4, "5000");

        var dirty = buf.GetDirtyRows();
        dirty.Count.ShouldBe(2);
    }
}
```

- [ ] **Step 2: Run test (fail)**

Run: `dotnet test --filter "FullyQualifiedName~AttriTabPanelBufferTests" -v normal`
Expected: FAIL — `AttriTabBuffer` not defined.

- [ ] **Step 3: Implement AttriTabPanel.cs (with embedded AttriTabBuffer class)**

Create `src/LongYinRoster/UI/AttriTabPanel.cs`:

```csharp
using System;
using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>v0.7.10 Phase 2 — buffer for [속성] secondary tab (test-friendly extracted).</summary>
public sealed class AttriTabBuffer
{
    public sealed record Row(AttriAxis Axis, int Index, string Label,
                             float OriginalBase, float OriginalMax)
    {
        public string BaseInput { get; set; } = "";
        public string MaxInput  { get; set; } = "";
        public bool   Dirty => BaseInput != OriginalBase.ToString("0")
                            || MaxInput  != OriginalMax.ToString("0");
    }

    private readonly Dictionary<(AttriAxis, int), Row> _rows = new();

    public void LoadFromHero(object hero)
    {
        _rows.Clear();
        AddRowsForAxis(hero, AttriAxis.Attri);
        AddRowsForAxis(hero, AttriAxis.FightSkill);
        AddRowsForAxis(hero, AttriAxis.LivingSkill);
    }

    private void AddRowsForAxis(object hero, AttriAxis axis)
    {
        int n = AttriLabels.Count(axis);
        for (int i = 0; i < n; i++)
        {
            var (b, m) = HeroAttriReflector.GetEntry(hero, axis, i);
            var row = new Row(axis, i, AttriLabels.For(axis, i), b, m)
            {
                BaseInput = b.ToString("0"),
                MaxInput  = m.ToString("0"),
            };
            _rows[(axis, i)] = row;
        }
    }

    public Row Get(AttriAxis axis, int idx) => _rows[(axis, idx)];

    public void SetBaseInput(AttriAxis axis, int idx, string s)
    {
        if (_rows.TryGetValue((axis, idx), out var r)) r.BaseInput = s;
    }

    public void SetMaxInput(AttriAxis axis, int idx, string s)
    {
        if (_rows.TryGetValue((axis, idx), out var r)) r.MaxInput = s;
    }

    public void BulkSetMax(AttriAxis axis, string s)
    {
        int n = AttriLabels.Count(axis);
        for (int i = 0; i < n; i++)
            if (_rows.TryGetValue((axis, i), out var r)) r.MaxInput = s;
    }

    public void Reset()
    {
        foreach (var r in _rows.Values)
        {
            r.BaseInput = r.OriginalBase.ToString("0");
            r.MaxInput  = r.OriginalMax.ToString("0");
        }
    }

    public bool IsDirty
    {
        get { foreach (var r in _rows.Values) if (r.Dirty) return true; return false; }
    }

    public List<Row> GetDirtyRows()
    {
        var list = new List<Row>();
        foreach (var r in _rows.Values) if (r.Dirty) list.Add(r);
        return list;
    }

    public IEnumerable<Row> EnumerateAxis(AttriAxis axis)
    {
        int n = AttriLabels.Count(axis);
        for (int i = 0; i < n; i++)
            if (_rows.TryGetValue((axis, i), out var r)) yield return r;
    }
}

/// <summary>
/// v0.7.10 Phase 2 — PlayerEditorPanel 의 [속성] secondary tab.
///
/// 720 width 분할 = 속성 240 / 무학 240 / 기예 240. row height 24.
/// row 형식 = [라벨 56] [base TextField 56] / [max TextField 56] [+buff 36] [→ effective 36]
/// 일괄 button = column 하단 [TextField] [전체 N].
/// [저장] / [되돌리기] = 탭 footer.
/// </summary>
public sealed class AttriTabPanel
{
    private readonly AttriTabBuffer _buffer = new();
    private object? _loadedFor;

    private string _bulkAttriInput       = "999";
    private string _bulkFightSkillInput  = "999";
    private string _bulkLivingSkillInput = "999";

    public void Draw(object hero)
    {
        if (hero == null)
        {
            GUILayout.Label("플레이어 정보 없음");
            return;
        }
        if (!ReferenceEquals(_loadedFor, hero))
        {
            _buffer.LoadFromHero(hero);
            _loadedFor = hero;
        }

        GUILayout.BeginHorizontal();
        DrawColumn(hero, AttriAxis.Attri, "속성", ref _bulkAttriInput);
        DrawColumn(hero, AttriAxis.FightSkill, "무학", ref _bulkFightSkillInput);
        DrawColumn(hero, AttriAxis.LivingSkill, "기예", ref _bulkLivingSkillInput);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        DrawFooter(hero);
    }

    private void DrawColumn(object hero, AttriAxis axis, string title, ref string bulkInput)
    {
        GUILayout.BeginVertical(GUILayout.Width(240));
        GUILayout.Label(title);
        foreach (var row in _buffer.EnumerateAxis(axis))
        {
            DrawRow(hero, row);
        }
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        bulkInput = GUILayout.TextField(bulkInput, 7, GUILayout.Width(64));
        if (GUILayout.Button($"전체 {title} 자질", GUILayout.Width(120)))
        {
            _buffer.BulkSetMax(axis, bulkInput);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawRow(object hero, AttriTabBuffer.Row row)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(row.Label, GUILayout.Width(48));
        string newBase = GUILayout.TextField(row.BaseInput, 7, GUILayout.Width(48));
        if (newBase != row.BaseInput) _buffer.SetBaseInput(row.Axis, row.Index, newBase);
        GUILayout.Label("/", GUILayout.Width(8));
        string newMax = GUILayout.TextField(row.MaxInput, 7, GUILayout.Width(48));
        if (newMax != row.MaxInput) _buffer.SetMaxInput(row.Axis, row.Index, newMax);

        float buff = HeroAttriReflector.GetBuff(hero, row.Axis, row.Index);
        float effective = (CharacterAttriEditor.TryParseInput(row.BaseInput, out var b) ? b : 0f) + buff;
        GUILayout.Label($"+{buff:0}", GUILayout.Width(40));
        GUILayout.Label($"→ {effective:0}", GUILayout.Width(40));
        GUILayout.EndHorizontal();
    }

    private void DrawFooter(object hero)
    {
        GUILayout.BeginHorizontal();
        GUI.enabled = _buffer.IsDirty;
        if (GUILayout.Button("저장", GUILayout.Width(80)))
        {
            ApplyDirty(hero);
        }
        GUI.enabled = true;

        if (GUILayout.Button("되돌리기", GUILayout.Width(80)))
        {
            _buffer.Reset();
        }
        GUILayout.Space(8);
        GUILayout.EndHorizontal();
    }

    private void ApplyDirty(object hero)
    {
        var dirty = _buffer.GetDirtyRows();
        int success = 0, failed = 0;
        foreach (var r in dirty)
        {
            bool baseOk = true, maxOk = true;
            if (CharacterAttriEditor.TryParseInput(r.BaseInput, out var bv))
            {
                if (Math.Abs(bv - r.OriginalBase) > 0.001f)
                    baseOk = CharacterAttriEditor.Change(hero, r.Axis, r.Index, bv);
            }
            else baseOk = false;

            if (CharacterAttriEditor.TryParseInput(r.MaxInput, out var mv))
            {
                if (Math.Abs(mv - r.OriginalMax) > 0.001f)
                    maxOk = CharacterAttriEditor.ChangeMax(hero, r.Axis, r.Index, mv);
            }
            else maxOk = false;

            if (baseOk && maxOk) success++; else failed++;
        }

        // sanitize 1회 (v0.7.7/v0.7.8 검증된 helper)
        try { PlayerEditApplier.RefreshMaxAttriAndSkill(hero); }
        catch (Exception ex) { Logger.WarnOnce("AttriTabPanel", $"Refresh threw: {ex.Message}"); }

        // re-load buffer from refreshed hero state
        _buffer.LoadFromHero(hero);
        _loadedFor = hero;

        ToastService.Push(failed == 0
            ? $"속성 {success} 항목 적용됨"
            : $"성공 {success} / 실패 {failed}");
    }
}
```

- [ ] **Step 4: Run tests (pass)**

Run: `dotnet test --filter "FullyQualifiedName~AttriTabPanelBufferTests" -v normal`
Expected: 6 tests PASS.

- [ ] **Step 5: Build**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
Expected: PASS. UnityStubs.cs 의 IMGUI signature 매칭 확인 (TextField/Toggle/Button/Label/BeginHorizontal/Space/etc — v0.7.6/v0.7.7 검증된 set 만 사용).

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/UI/AttriTabPanel.cs src/LongYinRoster.Tests/AttriTabPanelBufferTests.cs
git commit -m "feat(ui): v0.7.10 Phase 2 — AttriTabPanel (3-column inline + 일괄 + [저장]/[되돌리기])"
```

---

### Task 11: UI — PlayerEditorPanel secondary tab integration

**Files:**
- Modify: `src/LongYinRoster/UI/PlayerEditorPanel.cs:73-141` (Draw / DrawEmpty / 헤더 영역)

- [ ] **Step 1: Read current Draw method**

Run: `grep -n "private void Draw\|class PlayerEditorPanel" src/LongYinRoster/UI/PlayerEditorPanel.cs | head -5`
확인: line 19 (class), line 73 (Draw), line 142 (DrawEmpty), line 151 (DrawPlayerHeader).

- [ ] **Step 2: Add `Tab` enum + `_activeTab` field + `_attriTabPanel` instance**

Inside the class, near other private fields, add:

```csharp
    /// <summary>v0.7.10 — secondary tab between [기본] (v0.7.8 6 섹션) and [속성] (신규 AttriTabPanel).</summary>
    private enum Tab { Basic, Attri }
    private Tab _activeTab = Tab.Basic;
    private readonly AttriTabPanel _attriTabPanel = new();
```

- [ ] **Step 3: Add secondary tab header in Draw method**

Inside `Draw(int id)` after `DrawPlayerHeader(player);` and BEFORE the `_sectionOpen` 분기, insert:

```csharp
        // v0.7.10 — secondary tab
        GUILayout.BeginHorizontal();
        DrawTabButton("기본", Tab.Basic);
        DrawTabButton("속성", Tab.Attri);
        GUILayout.Space(8);
        GUILayout.EndHorizontal();
        GUILayout.Space(4);

        if (_activeTab == Tab.Attri)
        {
            _attriTabPanel.Draw(player);
            return;  // skip 기본 sections
        }
```

Add private method:

```csharp
    private void DrawTabButton(string label, Tab tab)
    {
        bool active = _activeTab == tab;
        var prevColor = GUI.color;
        if (active) GUI.color = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button(label, GUILayout.Width(80)))
            _activeTab = tab;
        GUI.color = prevColor;
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
Expected: PASS.

- [ ] **Step 5: Run all tests (regression)**

Run: `dotnet test`
Expected: 332 (Phase 1) + 6 (Task 6) + 6 (Task 7) + 10 (Task 9) + 6 (Task 10) = ~360 PASS. v0.7.8 PlayerEditorPanel 회귀 없음.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/UI/PlayerEditorPanel.cs
git commit -m "feat(ui): v0.7.10 Phase 2 — PlayerEditorPanel secondary tab [기본 / 속성] 추가"
```

---

### Task 12: 인게임 smoke (Phase 1 + Phase 2 + 회귀)

**Files:**
- Create: `docs/superpowers/dumps/2026-05-09-v0.7.10-smoke-results.md`

- [ ] **Step 1: Build deploy + game launch**

Run: `dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj`
게임 실행 → save load → F11 → 플레이어 편집

- [ ] **Step 2: Phase 1 smoke (Lock 토글)**

| # | 항목 |
|---|---|
| 1 | 천부 섹션 헤더 = `[☐ Lock max] [ 999 ] (1~999999)` |
| 2 | 체크 ON → ConfigEntry write |
| 3 | 게임 안 천부 추가 시 999 limit |
| 4 | uncheck → 원래 limit |

- [ ] **Step 3: Phase 2 smoke ([속성] 탭)**

| # | 항목 |
|---|---|
| 5 | [기본] / [속성] 탭 버튼 표시 |
| 6 | [속성] 클릭 → 3-column (속성 6 / 무학 9 / 기예 9) row 표시 |
| 7 | 각 row 의 라벨 한글 (근력/민첩/.../의술/.../요리) 정확 |
| 8 | base/max TextField 표시 + +buff + → effective |
| 9 | 근력 base 199 → 999 입력 + [저장] → read-back base=999, max≥999 |
| 10 | 무학 일괄 [전체 9999] → [저장] → 9 row 모두 max=9999 |
| 11 | 기예 일괄 동일 |
| 12 | RefreshMaxAttriAndSkill 후 maxhp/maxpower 변화 (속성 → derived stat) |
| 13 | [되돌리기] → originals 복원 |
| 14 | 비숫자 입력 → 무시 + WarnOnce log |

- [ ] **Step 4: 회귀 검증**

| # | 항목 |
|---|---|
| 15 | [기본] 탭 = v0.7.8 의 6 섹션 모두 작동 (Resource / SpeAddData × 3 / 천부 / 무공 / Breakthrough) |
| 16 | LogOutput 에 `Method unstripping failed` 0건 |
| 17 | LogOutput 에 `GUIClip imbalance` 0건 |
| 18 | LogOutput 에 `GetMaxTagNumPatch: HeroData.GetMaxTagNum patched` 1건 |

- [ ] **Step 5: 기록**

```bash
cp "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" \
   docs/superpowers/dumps/2026-05-09-v0.7.10-LogOutput.log
```

Edit `docs/superpowers/dumps/2026-05-09-v0.7.10-smoke-results.md` (modeled after v0.7.8 smoke):

```markdown
# v0.7.10 인게임 smoke (2026-05-09)

**baseline**: v0.7.8 + Phase 1 + Phase 2 commits (Tasks 1-11)
**plan**: docs/superpowers/plans/2026-05-09-longyin-roster-mod-v0.7.10-plan.md

## 검증 매트릭스

| # | 카테고리 | 항목 | 결과 |
|---|---|---|---|
| 1 | Phase 1 | 천부 섹션 헤더 [Lock max] 토글 + TextField 표시 | TBD |
| 2 | Phase 1 | 체크 ON → ConfigEntry write | TBD |
| 3 | Phase 1 | 천부 추가 시 999 limit | TBD |
| 4 | Phase 1 | uncheck → 원래 limit | TBD |
| 5 | Phase 2 | [기본]/[속성] 탭 버튼 | TBD |
| 6 | Phase 2 | 3-column row 표시 | TBD |
| 7 | Phase 2 | 한글 라벨 정확 | TBD |
| 8 | Phase 2 | base/max input + buff/effective 표시 | TBD |
| 9 | Phase 2 | 근력 base 199→999 [저장] | TBD |
| 10 | Phase 2 | 무학 일괄 [전체 9999] | TBD |
| 11 | Phase 2 | 기예 일괄 [전체 9999] | TBD |
| 12 | Phase 2 | RefreshMaxAttriAndSkill → maxhp 변화 | TBD |
| 13 | Phase 2 | [되돌리기] originals 복원 | TBD |
| 14 | Phase 2 | 비숫자 무시 + WarnOnce | TBD |
| 15 | 회귀 | [기본] 탭 v0.7.8 6 섹션 회귀 | TBD |
| 16 | 회귀 | Method unstripping failed 0건 | TBD |
| 17 | 회귀 | GUIClip imbalance 0건 | TBD |
| 18 | 회귀 | Patch register log 1건 | TBD |

## Sub-data wrapper / heroBuff axis 매칭 결과 (Spike #5 검증)

(인게임에서 +buff 표시값이 정확한지 확인 — 매칭 안 되면 spec §6 Spike #5 의 fallback 적용)

## 결론

(smoke 후 채움 — PASS / 부분 PASS / FAIL + 수정 항목 list)
```

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/dumps/2026-05-09-v0.7.10-smoke-results.md docs/superpowers/dumps/2026-05-09-v0.7.10-LogOutput.log
git commit -m "docs(smoke): v0.7.10 인게임 smoke results — Phase 1 + Phase 2 + 회귀"
```

---

## Release

### Task 13: README + CHANGELOG + release notes

**Files:**
- Modify: `README.md`
- Create: `dist/release-notes-v0.7.10.md`

- [ ] **Step 1: Edit README.md — bump version**

Find the version mention near top + features list. Add v0.7.10 entry to features:

```markdown
- **v0.7.10**: 천부 max 보유수 lock (`[☐ Lock max] [ 999 ]` 토글, Player heroID=0 only) + 속성·무학·기예 editor (PlayerEditorPanel `[기본 / 속성]` secondary tab — 24 row × 2 axis)
```

- [ ] **Step 2: Create dist/release-notes-v0.7.10.md**

```markdown
# LongYin Roster Mod v0.7.10

**일시**: 2026-05-09
**baseline**: v0.7.8 (327/327 tests + 사용자 11 iteration 검증 PASS)

## 새 기능

### Phase 1 — 천부 max 보유수 lock
- PlayerEditorPanel (F11+4) 의 천부 섹션 헤더에 `[☐ Lock max] [ 999 ] (1~999999)` 토글 추가
- 체크 ON 시 `HeroData.GetMaxTagNum()` Postfix 가 LockedMaxTagNumValue 로 override
- Player (heroID=0) only — NPC 무영향
- BepInEx ConfigEntry 자동 영속 (`LockMaxTagNum` / `LockedMaxTagNumValue`)
- cheat LongYinCheat GameplayPatch.GetMaxTagNum 패턴 100% mirror

### Phase 2 — 속성·무학·기예 editor
- PlayerEditorPanel 헤더에 `[기본 / 속성]` secondary tab 추가
- [기본] 탭 = v0.7.8 의 6 섹션 (Resource / SpeAddData × 3 / 천부 / 무공 / Breakthrough) — 회귀 없음
- [속성] 탭 = 신규 — 3 column:
  - **속성 6** — 근력 / 민첩 / 지력 / 의지 / 체질 / 경맥
  - **무학 9** — 내공 / 경공 / 절기 / 권장 / 검법 / 도법 / 장병 / 기문 / 사술
  - **기예 9** — 의술 / 독술 / 학식 / 언변 / 채벌 / 목식 / 단조 / 제약 / 요리
- 각 row = `[라벨] [수치 input] / [자질값 input] +buff → effective`
- 일괄 button × 3 — `[전체 속성 자질]` / `[전체 무학 자질]` / `[전체 기예 자질]` (cheat SetAllAttri/FightSkill/LivingSkill mirror)
- [저장] gated apply — buffer + dirty 추적 → cheat `ChangeAttri/FightSkill/LivingSkill(hero, idx, val)` × N → `RefreshMaxAttriAndSkill()` 1회
- [되돌리기] → originals 복원
- Clamp [0, 999999] (cheat AddTalent 정렬)

## 변경

- `Plugin.VERSION` 0.7.8 → 0.7.10
- `PlayerEditApplier.TryInvokeRefreshMaxAttriAndSkill` (private) → `RefreshMaxAttriAndSkill` (public, Phase 2 reuse)
- BepInEx Plugin.Load 에 `Core.GetMaxTagNumPatch.Register(harmony)` 추가

## 신규 file

- `src/LongYinRoster/Core/GetMaxTagNumPatch.cs` (Phase 1)
- `src/LongYinRoster/Core/HeroAttriReflector.cs` (Phase 2)
- `src/LongYinRoster/Core/CharacterAttriEditor.cs` (Phase 2)
- `src/LongYinRoster/Util/AttriLabels.cs` (Phase 2)
- `src/LongYinRoster/UI/AttriTabPanel.cs` (Phase 2)

## 호환성

- 기존 v0.7.8 사용자 설정 (sort/filter/last/rect/window/hotkey 4) 변경 없음 — append-only
- 게임 patch 없음 (1.0.0f8.2 그대로)

## 미반영 / 후속

- **자질 grade marker** (신/하 등) — derivation rule 미확인. v0.7.10.1 patch 또는 v0.7.11 cycle 에서 추가
- **NPC dropdown** (heroID switch) — v0.7.11 별도 cycle. v0.7.10 자산이 hero 인자 받도록 generalize 후 dropdown 추가
- **Resource stat lock** (hp/power/mana/weight) — Q1 deferred (cheat StatEditor LockedMax 매 frame 패턴, 별도 sub-project)
- **v0.8 진짜 sprite** — G3 Decision DEFER until G4

## Tests

- 327 → ~360 (+33). xUnit + Shouldly + POCO mocks.
- 인게임 smoke = 18 항목 매트릭스 PASS

## 메타

- Spec: `docs/superpowers/specs/2026-05-09-longyin-roster-mod-v0.7.10-design.md`
- Plan: `docs/superpowers/plans/2026-05-09-longyin-roster-mod-v0.7.10-plan.md`
- Smoke: `docs/superpowers/dumps/2026-05-09-v0.7.10-smoke-results.md`
- Roadmap: G3 Decision 2026-05-09 (단일 cycle B+A 결합 + Q4=β NPC 분리)
```

- [ ] **Step 3: Commit**

```bash
git add README.md dist/release-notes-v0.7.10.md
git commit -m "docs(release): v0.7.10 release notes + README features 항목 추가"
```

---

### Task 14: HANDOFF 갱신

**Files:**
- Modify: `docs/HANDOFF.md` (top section "현재 상태" + "다음 sub-project" + Releases list)

- [ ] **Step 1: Read current HANDOFF top**

Run: `head -60 docs/HANDOFF.md`
Note current state strings + Releases list endpoint.

- [ ] **Step 2: Update HANDOFF.md**

Edit top "**진행 상태**" line:
```
**진행 상태**: **v0.7.10 release** — 천부 max 보유수 lock (Phase 1) + 속성·무학·기예 editor (Phase 2). PlayerEditorPanel `[기본 / 속성]` secondary tab — 속성 6 / 무학 9 / 기예 9 (24 row × 2 axis 수치/자질값) inline TextField + 일괄 button × 3 + [저장] gated apply (cheat ChangeAttri/FightSkill/LivingSkill mirror + RefreshMaxAttriAndSkill 1회). 천부 섹션 헤더 [Lock max] 토글 (HeroData.GetMaxTagNum Postfix override, Player heroID=0 only, ConfigEntry 영속). ~360 tests PASS + 인게임 smoke 18/18 PASS.
```

Add to Releases list (after v0.7.8):
```
- [v0.7.10](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.10) — 천부 max lock + 속성·무학·기예 editor. PlayerEditorPanel `[기본 / 속성]` secondary tab. Phase 1 = GetMaxTagNumPatch (cheat 100% mirror). Phase 2 = HeroAttriReflector + CharacterAttriEditor + AttriTabPanel + AttriLabels.
```

Update "**다음 세션 후속 sub-project**":
```
- ✅ ~~v0.7.10 LockedMax + 속성·무학·기예 editor~~ (2026-05-09)
- **v0.7.11 (후보) NPC dropdown** — heroID switch, v0.7.10 모든 자산 generalize. PlayerEditorPanel header 에 SelectorDialog 2단계 탭 (force/문파 + name search) 추가
- **v0.7.10.1 (후보) 자질 grade marker** — derivation rule spike (신/하 등 enum 또는 value threshold)
- **v0.8 (후보) 진짜 sprite** — G4 게이트에서 재평가
- **maintenance** — trigger 시 활성
```

- [ ] **Step 3: Commit**

```bash
git add docs/HANDOFF.md
git commit -m "docs(handoff): v0.7.10 baseline 갱신 + 다음 sub-project 후보 (v0.7.11 NPC / v0.7.10.1 grade marker)"
```

---

### Task 15: VERSION + tag + push

- [ ] **Step 1: Verify VERSION constant**

Run: `grep -n "VERSION" src/LongYinRoster/Plugin.cs`
Expected: `public const string VERSION = "0.7.10";` (set in Task 3 already).

- [ ] **Step 2: Tag**

```bash
git tag v0.7.10
git log --oneline v0.7.8..HEAD
```

- [ ] **Step 3: Push**

```bash
git push origin main
git push origin v0.7.10
```

- [ ] **Step 4: Build release dll for distribution**

```bash
dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj
```

zip 자산 생성 (기존 v0.7.x 패턴 mirror):
```bash
# dist/LongYinRoster-v0.7.10.zip — 자세한 패키징은 prior dist zip 패턴 참조
```

- [ ] **Step 5: GitHub release**

`gh release create v0.7.10 --notes-file dist/release-notes-v0.7.10.md --title "v0.7.10 — 천부 max lock + 속성·무학·기예 editor"`

(또는 dist zip 첨부 시 `dist/LongYinRoster-v0.7.10.zip` 추가)

- [ ] **Step 6: Roadmap meta spec G3 Result append**

Edit `docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md` — `### G3 Decision (2026-05-09)` 섹션 다음에 append:

```markdown
### v0.7.10 Result (2026-05-09)

- Release: [v0.7.10](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.10)
- Spec: [2026-05-09-longyin-roster-mod-v0.7.10-design.md](2026-05-09-longyin-roster-mod-v0.7.10-design.md)
- Plan: [2026-05-09-longyin-roster-mod-v0.7.10-plan.md](../plans/2026-05-09-longyin-roster-mod-v0.7.10-plan.md)
- Smoke: [2026-05-09-v0.7.10-smoke-results.md](../dumps/2026-05-09-v0.7.10-smoke-results.md)
- Tests: 327 → ~360 (+33). 인게임 smoke 18/18 PASS.
- Brainstorm 결과: G3=E (B+A 결합 ③) / Q1=A LockedMax scope=천부 max only / Q2=A Player only / Q3=A 천부 섹션 헤더 / Q4=β 분리 (v0.7.10 + v0.7.11) / Q5=E secondary tab / Q6=B inline + 일괄 / Q7=B [저장] gated / Q8=B 수치/자질값 + buff/effective / Q9=A 즉시 적용
- 신규 자산: GetMaxTagNumPatch / HeroAttriReflector / CharacterAttriEditor / AttriLabels / AttriTabPanel / AttriTabBuffer
- Phase 분리: Phase 1 (LockedMax) commits 1-5 / Phase 2 (속성/무학/기예) commits 6-12 / release commits 13-15

### G4 Gate Pending (v0.7.10 release 직후, 2026-05-09)

평가 대상:
- **v0.7.11 NPC dropdown** — heroID switch + v0.7.10 자산 generalize (★★ 가성비 가장 높음, 자연스러운 후속)
- **v0.7.10.1 자질 grade marker** — derivation rule 또는 별도 field spike
- **v0.8 진짜 sprite** — IL2CPP sprite asset spike
- **v0.7.9 Slot diff preview** — Apply pipeline 변경
- **maintenance** — trigger 시 활성

G4 Decision 은 사용자 명시 선택 시점에 본 spec 에 append.
```

- [ ] **Step 7: Commit roadmap**

```bash
git add docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md
git commit -m "docs(spec): v0.7.10 Result + G4 Gate Pending 메타 roadmap append"
git push origin main
```

---

## Self-Review

(plan 작성 후 self-check)

**1. Spec coverage:**
- Spec §1.2 Phase 매트릭스 → Tasks 1-11 ✓
- Spec §1.3 [속성] tab + 3-column 편집 → Tasks 6-11 ✓
- Spec §1.4 신규 자산 5 file → Tasks 2/6/7/9/10 ✓
- Spec §2.1 사용자 시나리오 → Task 12 smoke ✓
- Spec §3.2 PlayerEditorPanel 변경 (탭 헤더 + 천부 섹션 헤더) → Tasks 4 + 11 ✓
- Spec §4.1 Unit tests ~25 → Tasks 6/7/9/10 (5+6+10+6=27) ✓
- Spec §4.2 인게임 smoke 18 항목 → Task 12 ✓
- Spec §4.3 Strip-safe 검증 → Task 4 노트 + Task 12 회귀 ✓
- Spec §5 영속화 → Tasks 1 (ConfigEntry) ✓
- Spec §6 Spike list — Task 12 #19 (heroBuff axis 매칭 — fallback 명시) + 인게임에서 검증, separate spike commit 안 함 (cheat reference 가 충분히 검증된 패턴이라 risk 낮음). Spike NO-GO 시 Phase 2 의 buff display 만 영향 (수치/자질값 input 은 작동) — fallback 명시 in HeroAttriReflector.AxisToSpeAddIdx.
- Spec §10 작업 순서 → Tasks 1-15 ✓
- Spec §11 의존성 → reuse 명시 (Task 8 RefreshMaxAttriAndSkill public) ✓

**2. Placeholder scan:**
- "TBD" 1 곳 (smoke results template 의 결론 채우기) — 정상 (smoke run 후 채움)
- 다른 TBD/TODO/FIXME 없음 ✓

**3. Type consistency:**
- `AttriAxis` enum (Util) — 모든 사용처 일관 ✓
- `CharacterAttriEditor.Change(hero, axis, idx, val)` 시그니처 — Task 9 정의 + Task 10 사용 일관 ✓
- `HeroAttriReflector.GetEntry(hero, axis, idx) → (Base, Max)` — Task 7 정의 + Task 10 사용 일관 ✓
- `PlayerEditApplier.RefreshMaxAttriAndSkill(player)` (public) — Task 8 정의 + Task 10 호출 일관 ✓
- `Config.LockMaxTagNum` / `Config.LockedMaxTagNumValue` — Task 1 선언 + Task 2/4 사용 일관 ✓
- `GetMaxTagNumPatch.ApplyOverride` 시그니처 — Task 2 정의 + tests 사용 일관 ✓

**Issue 발견 + 수정**:
- 없음. plan 그대로 진행 가능.

---

## 실행 옵션

**Plan complete and saved to `docs/superpowers/plans/2026-05-09-longyin-roster-mod-v0.7.10-plan.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — fresh subagent per task, 두 단계 review 사이클 (Tasks 1-15 분리 dispatch).

**2. Inline Execution** — 본 session 에서 executing-plans 사용해 batch 실행 + checkpoint review.

답: 1 / 2 ?
