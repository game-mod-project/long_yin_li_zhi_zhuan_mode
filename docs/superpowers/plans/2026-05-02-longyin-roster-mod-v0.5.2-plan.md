# LongYin Roster Mod v0.5.2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ApplySelection 의 새 카테고리 `KungfuList` 를 활성화 — slot 의 무공 list 를 player 에 완전 교체 (clear + add all). v0.5.1 active 의 N7 한계 (다른 캐릭터의 active set 적용 불가) 자동 해소.

**Architecture:** v0.5.1 의 algorithm 통찰 (game 자체 패턴 mirror) 을 무공 list manipulation 에 적용. game-self method (`LoseAllKungfu` / `LearnKungfu` 류) 발견 후 clear + add all. 새 파일 `Core/KungfuListApplier.cs` 에 책임 분리. PinpointPatcher 의 `RebuildKungfuSkills` step (현재 `SkipKungfuSkills` stub) 본문 교체. step 순서 변경 — `RebuildKungfuSkills` 를 `SetActiveKungfu` 직전에 배치 (list 정확화 후 active 매칭).

**Tech Stack:** BepInEx 6.0.0-dev (IL2CPP, .NET 6) / HarmonyLib / Il2CppInterop / System.Text.Json / xUnit + Shouldly.

**선행 spec:** [`2026-05-02-longyin-roster-mod-v0.5.2-design.md`](../specs/2026-05-02-longyin-roster-mod-v0.5.2-design.md)

**작업 흐름**: Phase 1 (foundation + branch) → Phase 2 (Spike Phase 1 — Probe + method dump + clear/add 검증) → Phase 3 (Impl — KungfuListApplier + PinpointPatcher 본문 교체 + Capabilities/ApplySelection/UI 확장) → Phase 4 (Smoke 시나리오 1-3 + 회귀) → Phase 5 (Release — Probe cleanup + VERSION + dist + tag). Spike FAIL 시 Phase 6 alternate (wrapper ctor 재도전 / abort).

---

## File Structure

### 신규 파일

| 경로 | 책임 | 조건부? | Lifetime |
|---|---|---|---|
| `src/LongYinRoster/Core/Probes/ProbeKungfuList.cs` | Spike Phase 1 — method dump + clear/add 시도 | 항상 (PoC) | release 전 cleanup |
| `src/LongYinRoster/Core/Probes/ProbeRunner.cs` | F12 trigger → Spike Mode 분기 | 항상 (PoC) | release 전 cleanup |
| `src/LongYinRoster/Core/KungfuListApplier.cs` | clear + add all algorithm + UI refresh trigger | ✓ Spike PASS | 영구 |
| `src/LongYinRoster.Tests/KungfuListApplierTests.cs` | slot JSON parse + selection gate unit tests (5 tests) | ✓ Spike PASS | 영구 |
| `docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md` | Spike Phase 1 결과 + decision | 항상 | 영구 |
| `docs/superpowers/dumps/2026-05-XX-v0.5.2-smoke.md` | Smoke 시나리오 결과 (release 시) | ✓ release | 영구 |
| `dist/LongYinRoster_v0.5.2.zip` | release artifact | ✓ release | 영구 |
| `dist/LongYinRoster_v0.5.2/` | release 폴더 구조 | ✓ release | 영구 |

### 수정 파일

| 경로 | 변경 | 조건부? |
|---|---|---|
| `src/LongYinRoster/UI/ModWindow.cs` | F12 / F10 핫키 handler 추가 (Spike) | 항상 (PoC, release 전 cleanup) |
| `src/LongYinRoster/Core/PinpointPatcher.cs` | `SkipKungfuSkills` stub → `RebuildKungfuSkills` 본문 (KungfuListApplier 호출), step 순서 변경 (RebuildKungfuSkills 를 SetActiveKungfu 직전), `ProbeKungfuListCapability` 신규 | ✓ Spike PASS |
| `src/LongYinRoster/Core/Capabilities.cs` | `KungfuList` flag 추가 | 항상 |
| `src/LongYinRoster/Core/ApplySelection.cs` | `KungfuList` 필드 + JSON 직렬화 + V03Default + RestoreAll + AnyEnabled | 항상 |
| `src/LongYinRoster/Util/KoreanStrings.cs` | `Cat_KungfuList = "무공 목록"` 추가 | 항상 |
| `src/LongYinRoster/UI/SlotDetailPanel.cs` | 9 → 10 카테고리 grid (4 row × 3 col 또는 3 row × 4 col) | ✓ Spike PASS |
| `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` | KungfuListApplier.cs link | ✓ Spike PASS |
| `src/LongYinRoster.Tests/CapabilitiesTests.cs` | KungfuList round-trip | 항상 |
| `src/LongYinRoster.Tests/ApplySelectionTests.cs` | KungfuList JSON round-trip + V03Default + RestoreAll | 항상 |
| `src/LongYinRoster/Plugin.cs` | VERSION `0.5.1` → `0.5.2` | ✓ release |
| `README.md` | v0.5.2 highlights + 10 카테고리 표 + Releases entry | ✓ release |
| `docs/HANDOFF.md` | §1 main baseline = v0.5.2, §2 git history, §6 v0.6 후보 갱신 | 항상 |

---

## Phase 1 — Foundation (항상 실행)

### Task 1: Branch + baseline 검증

**Files:**
- Read: `git status`, `git log`

- [ ] **Step 1.1: 작업 위치 + 현재 baseline 확인**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git status
git log --oneline -5
```

Expected:
- working tree clean
- HEAD = `01c16a5 docs: v0.5.2 spec` (main 의 latest)

- [ ] **Step 1.2: v0.5.2 branch 생성 + checkout**

```bash
git checkout -b v0.5.2
git branch --show-current
```

Expected: `v0.5.2`.

- [ ] **Step 1.3: 게임 닫기 + v0.5.1 baseline build**

```bash
tasklist | grep -i LongYinLiZhiZhuan
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 게임 프로세스 없음 (또는 사용자에게 닫으라 요청). Build SUCCEEDED, dll deploy.

- [ ] **Step 1.4: v0.5.1 baseline tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: **50/50 PASS** (v0.5.1 baseline).

---

### Task 2: Capabilities.KungfuList flag 추가

**Files:**
- Modify: `src/LongYinRoster/Core/Capabilities.cs`
- Modify: `src/LongYinRoster.Tests/CapabilitiesTests.cs` (있으면 / 없으면 신규)

- [ ] **Step 2.1: Failing test — CapabilitiesTests 에 KungfuList round-trip**

`src/LongYinRoster.Tests/CapabilitiesTests.cs` 가 이미 있으면 `KungfuList` 검증 추가. 없으면 신규 작성. 추가 검증 case:

```csharp
[Fact]
public void AllOff_KungfuList_False()
{
    var c = Capabilities.AllOff();
    c.KungfuList.ShouldBeFalse();
}

[Fact]
public void AllOn_KungfuList_True()
{
    var c = Capabilities.AllOn();
    c.KungfuList.ShouldBeTrue();
}

[Fact]
public void ToString_IncludesKungfuListFlag()
{
    var c = new Capabilities { KungfuList = true };
    c.ToString().ShouldContain("KungfuList=True");
}
```

- [ ] **Step 2.2: Run tests — should fail (KungfuList property 없음)**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~CapabilitiesTests"
```

Expected: FAIL — `Capabilities.KungfuList` not found (CS1061).

- [ ] **Step 2.3: Capabilities.cs 에 KungfuList 추가**

`src/LongYinRoster/Core/Capabilities.cs` 수정:

```csharp
public sealed class Capabilities
{
    public bool Identity     { get; init; }
    public bool ActiveKungfu { get; init; }
    public bool ItemList     { get; init; }
    public bool SelfStorage  { get; init; }
    public bool Appearance   { get; init; }
    public bool KungfuList   { get; init; }   // v0.5.2

    public static Capabilities AllOff() => new();
    public static Capabilities AllOn() => new()
    {
        Identity = true, ActiveKungfu = true, ItemList = true, SelfStorage = true,
        Appearance = true, KungfuList = true,
    };

    public override string ToString() =>
        $"Identity={Identity} ActiveKungfu={ActiveKungfu} " +
        $"ItemList={ItemList} SelfStorage={SelfStorage} " +
        $"Appearance={Appearance} KungfuList={KungfuList}";
}
```

- [ ] **Step 2.4: Run tests — should pass**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~CapabilitiesTests"
```

Expected: PASS.

- [ ] **Step 2.5: Commit**

```bash
git add src/LongYinRoster/Core/Capabilities.cs src/LongYinRoster.Tests/CapabilitiesTests.cs
git commit -m "feat(capabilities): KungfuList flag — v0.5.2 prerequisite"
```

---

### Task 3: ApplySelection.KungfuList 필드 추가

**Files:**
- Modify: `src/LongYinRoster/Core/ApplySelection.cs`
- Modify: `src/LongYinRoster.Tests/ApplySelectionTests.cs`

- [ ] **Step 3.1: Failing test — ApplySelectionTests 에 KungfuList JSON round-trip**

`src/LongYinRoster.Tests/ApplySelectionTests.cs` 에 추가:

```csharp
[Fact]
public void V03Default_KungfuList_False()
{
    var s = ApplySelection.V03Default();
    s.KungfuList.ShouldBeFalse();
}

[Fact]
public void RestoreAll_KungfuList_True()
{
    var s = ApplySelection.RestoreAll();
    s.KungfuList.ShouldBeTrue();
}

[Fact]
public void ToJson_FromJson_KungfuList_RoundTrip()
{
    var s = new ApplySelection { KungfuList = true };
    var json = ApplySelection.ToJson(s);
    var s2 = ApplySelection.FromJson(json);
    s2.KungfuList.ShouldBeTrue();
}
```

- [ ] **Step 3.2: Run tests — should fail**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ApplySelectionTests"
```

Expected: FAIL — `ApplySelection.KungfuList` not found.

- [ ] **Step 3.3: ApplySelection.cs 수정**

`src/LongYinRoster/Core/ApplySelection.cs` 의 모든 위치에 `KungfuList` 추가:

```csharp
public sealed class ApplySelection
{
    public bool Stat        { get; set; } = true;
    public bool Honor       { get; set; } = true;
    public bool TalentTag   { get; set; } = true;
    public bool Skin        { get; set; } = true;
    public bool SelfHouse   { get; set; } = false;
    public bool Identity    { get; set; } = false;
    public bool ActiveKungfu{ get; set; } = false;
    public bool ItemList    { get; set; } = false;
    public bool SelfStorage { get; set; } = false;
    public bool Appearance  { get; set; } = false;
    public bool KungfuList  { get; set; } = false;   // v0.5.2

    public static ApplySelection V03Default() => new();

    public static ApplySelection RestoreAll() => new()
    {
        Stat = true, Honor = true, TalentTag = true, Skin = true,
        SelfHouse = true, Identity = true, ActiveKungfu = true,
        ItemList = true, SelfStorage = true, Appearance = true,
        KungfuList = true,
    };

    public bool AnyEnabled() =>
        Stat || Honor || TalentTag || Skin || SelfHouse ||
        Identity || ActiveKungfu || ItemList || SelfStorage || Appearance ||
        KungfuList;

    public static string ToJson(ApplySelection s)
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteBoolean("stat",         s.Stat);
            w.WriteBoolean("honor",        s.Honor);
            w.WriteBoolean("talentTag",    s.TalentTag);
            w.WriteBoolean("skin",         s.Skin);
            w.WriteBoolean("selfHouse",    s.SelfHouse);
            w.WriteBoolean("identity",     s.Identity);
            w.WriteBoolean("activeKungfu", s.ActiveKungfu);
            w.WriteBoolean("itemList",     s.ItemList);
            w.WriteBoolean("selfStorage",  s.SelfStorage);
            w.WriteBoolean("appearance",   s.Appearance);
            w.WriteBoolean("kungfuList",   s.KungfuList);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static ApplySelection FromJson(string json)
    {
        var s = V03Default();
        if (string.IsNullOrWhiteSpace(json)) return s;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return s;

        bool Read(string key, bool def) =>
            root.TryGetProperty(key, out var v) ? v.ValueKind == JsonValueKind.True : def;

        s.Stat         = Read("stat",         s.Stat);
        s.Honor        = Read("honor",        s.Honor);
        s.TalentTag    = Read("talentTag",    s.TalentTag);
        s.Skin         = Read("skin",         s.Skin);
        s.SelfHouse    = Read("selfHouse",    s.SelfHouse);
        s.Identity     = Read("identity",     s.Identity);
        s.ActiveKungfu = Read("activeKungfu", s.ActiveKungfu);
        s.ItemList     = Read("itemList",     s.ItemList);
        s.SelfStorage  = Read("selfStorage",  s.SelfStorage);
        s.Appearance   = Read("appearance",   s.Appearance);
        s.KungfuList   = Read("kungfuList",   s.KungfuList);
        return s;
    }

    public static ApplySelection FromJsonElement(JsonElement el)
    {
        var s = V03Default();
        if (el.ValueKind != JsonValueKind.Object) return s;

        bool Read(string key, bool def) =>
            el.TryGetProperty(key, out var v) ? v.ValueKind == JsonValueKind.True : def;

        s.Stat         = Read("stat",         s.Stat);
        s.Honor        = Read("honor",        s.Honor);
        s.TalentTag    = Read("talentTag",    s.TalentTag);
        s.Skin         = Read("skin",         s.Skin);
        s.SelfHouse    = Read("selfHouse",    s.SelfHouse);
        s.Identity     = Read("identity",     s.Identity);
        s.ActiveKungfu = Read("activeKungfu", s.ActiveKungfu);
        s.ItemList     = Read("itemList",     s.ItemList);
        s.SelfStorage  = Read("selfStorage",  s.SelfStorage);
        s.Appearance   = Read("appearance",   s.Appearance);
        s.KungfuList   = Read("kungfuList",   s.KungfuList);
        return s;
    }
}
```

- [ ] **Step 3.4: Run tests — should pass**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ApplySelectionTests"
```

Expected: PASS.

- [ ] **Step 3.5: 전체 tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 50 + 새 추가된 N tests = 53+ PASS, 회귀 없음.

- [ ] **Step 3.6: Commit**

```bash
git add src/LongYinRoster/Core/ApplySelection.cs src/LongYinRoster.Tests/ApplySelectionTests.cs
git commit -m "feat(slots): ApplySelection.KungfuList field — JSON round-trip + default off"
```

---

### Task 4: KoreanStrings.Cat_KungfuList 추가

**Files:**
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs`

- [ ] **Step 4.1: Cat_KungfuList 추가**

`src/LongYinRoster/Util/KoreanStrings.cs` 의 `// v0.4 — 체크박스 카테고리` 섹션 끝에 추가:

```csharp
// v0.4 — 체크박스 카테고리
public const string Cat_Stat            = "스탯";
public const string Cat_Honor           = "명예";
public const string Cat_TalentTag       = "천부";
public const string Cat_Skin            = "스킨";
public const string Cat_SelfHouse       = "자기집 add";
public const string Cat_Identity        = "정체성";
public const string Cat_ActiveKungfu    = "무공 active";
public const string Cat_ItemList        = "인벤토리";
public const string Cat_SelfStorage     = "창고";
// v0.5 — 외형 카테고리
public const string Cat_Appearance      = "외형";
// v0.5.2 — 무공 list 카테고리
public const string Cat_KungfuList      = "무공 목록";
public const string Cat_DisabledSuffix  = " (v0.5+ 후보)";
public const string ApplySectionHeader  = "─── Apply 항목 ───";
```

(기존 `Cat_DisabledSuffix` / `ApplySectionHeader` 위에 `Cat_KungfuList` 1 줄 삽입)

- [ ] **Step 4.2: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

- [ ] **Step 4.3: Commit**

```bash
git add src/LongYinRoster/Util/KoreanStrings.cs
git commit -m "feat(strings): v0.5.2 — Cat_KungfuList = 무공 목록"
```

---

## Phase 2 — Spike Phase 1 (User-driven, with gate)

### Task 5: Probes 디렉터리 + ProbeKungfuList.cs 작성

**Files:**
- Create: `src/LongYinRoster/Core/Probes/ProbeKungfuList.cs`
- Create: `src/LongYinRoster/Core/Probes/ProbeRunner.cs`

- [ ] **Step 5.1: Probes 디렉터리 생성**

```bash
mkdir -p "src/LongYinRoster/Core/Probes"
```

- [ ] **Step 5.2: ProbeKungfuList.cs 작성**

```csharp
// File: src/LongYinRoster/Core/Probes/ProbeKungfuList.cs

using System;
using System.Reflection;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5.2 Spike Phase 1 — 무공 list game-self method discovery.
///
/// 5 modes:
///   Step1 = method dump (Lose/Learn/Add/Clear*Kungfu* 시그니처)
///   Step2 = clear 후보 시도
///   Step3 = add 후보 시도
///   Step4 = 통합 (clear + add all)
///   Step5 = persistence baseline (현재 list count + first 10 entries 출력)
///
/// release 전 cleanup.
/// </summary>
public static class ProbeKungfuList
{
    public enum Mode { Step1, Step2, Step3, Step4, Step5 }

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
            case Mode.Step1: RunStep1(player); break;
            case Mode.Step2: RunStep2(player, ksList); break;
            case Mode.Step3: RunStep3(player, ksList); break;
            case Mode.Step4: RunStep4(player, ksList); break;
            case Mode.Step5: RunStep5(player, ksList, n); break;
        }
    }

    private static void RunStep1(object player)
    {
        // Method dump — Lose/Learn/Add/Clear/Remove/Get/Drop * Kungfu*
        var t = player.GetType();
        var pattern = new System.Text.RegularExpressions.Regex(
            @"^(Lose|Learn|Add|Clear|Remove|Get|Drop)(All)?(Kungfu|Skill)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        Logger.Info("=== Spike Step1 — method dump ===");
        foreach (var m in t.GetMethods(F))
        {
            if (!pattern.IsMatch(m.Name)) continue;
            var ps = m.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"method: {m.ReturnType.Name} {m.Name}({sig})");
        }
        Logger.Info("=== Spike Step1 end ===");
    }

    private static void RunStep2(object player, object ksList)
    {
        // 후보 clear method 시도 (사용자가 dump 결과 보고 spec impl 시 정확한 method 결정)
        // 여기는 일반 후보들 시도 — 정확한 method 발견 시 사용자가 update
        string[] candidates = { "LoseAllKungfu", "ClearAllKungfu", "RemoveAllKungfu" };
        foreach (var name in candidates)
        {
            var m = player.GetType().GetMethod(name, F, null, Type.EmptyTypes, null);
            if (m == null) { Logger.Info($"Spike Step2: {name}() not found"); continue; }
            int beforeCount = IL2CppListOps.Count(ksList);
            try { m.Invoke(player, null); }
            catch (Exception ex) { Logger.Warn($"Spike Step2 {name}: {ex.GetType().Name}: {ex.Message}"); continue; }
            int afterCount = IL2CppListOps.Count(ksList);
            Logger.Info($"Spike Step2: {name}() — count {beforeCount} → {afterCount}");
            return;  // 첫 작동 후 종료 — 결과 사용자 확인
        }
        Logger.Warn("Spike Step2: 모든 후보 not found");
    }

    private static void RunStep3(object player, object ksList)
    {
        // 후보 add method 시도 — (int skillID, int lv) 시그니처 가정
        // SkillID 100 (가정 ID, dump 결과 따라 변경) lv 1 으로 시도
        int testSkillID = 100;
        int testLv = 1;
        string[] candidates = { "LearnKungfu", "AddKungfuSkill", "GetKungfu" };
        foreach (var name in candidates)
        {
            // signature (int, int) 매칭 method 찾기
            var m = player.GetType().GetMethod(name, F, null, new[] { typeof(int), typeof(int) }, null);
            if (m == null) { Logger.Info($"Spike Step3: {name}(int, int) not found"); continue; }
            int beforeCount = IL2CppListOps.Count(ksList);
            try { m.Invoke(player, new object[] { testSkillID, testLv }); }
            catch (Exception ex) { Logger.Warn($"Spike Step3 {name}: {ex.GetType().Name}: {ex.Message}"); continue; }
            int afterCount = IL2CppListOps.Count(ksList);
            Logger.Info($"Spike Step3: {name}({testSkillID}, {testLv}) — count {beforeCount} → {afterCount}");
            return;
        }
        Logger.Warn("Spike Step3: 모든 (int, int) 후보 not found");
    }

    private static void RunStep4(object player, object ksList)
    {
        // 통합 시나리오는 Spike Phase 1 의 Step 1-3 결과 분석 후 implementation 으로 이동
        // 여기는 placeholder
        Logger.Info("Spike Step4: 통합 시나리오는 Step 1-3 분석 후 implementation 단계에서 검증");
    }

    private static void RunStep5(object player, object ksList, int n)
    {
        // persistence baseline — 현재 list 의 first 10 entries 출력
        Logger.Info($"Spike Step5: kungfuSkills count={n}");
        int dumpN = System.Math.Min(n, 10);
        for (int i = 0; i < dumpN; i++)
        {
            var w = IL2CppListOps.Get(ksList, i);
            if (w == null) continue;
            int sid = (int)(ReadField(w, "skillID") ?? -1);
            int lv = (int)(ReadField(w, "lv") ?? -1);
            float fe = (float)(ReadField(w, "fightExp") ?? -1f);
            bool eq = (bool)(ReadField(w, "equiped") ?? false);
            Logger.Info($"Spike Step5: [{i}] skillID={sid} lv={lv} fightExp={fe} equiped={eq}");
        }
        Logger.Info("Spike Step5: 게임 메뉴 → save → 종료 → 재시작 → load → 위 list 와 일치하는지 사용자 확인");
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
}
```

- [ ] **Step 5.3: ProbeRunner.cs 작성 (v0.5.1 패턴 mirror)**

```csharp
// File: src/LongYinRoster/Core/Probes/ProbeRunner.cs

using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

public static class ProbeRunner
{
    public static ProbeKungfuList.Mode Mode { get; set; } = ProbeKungfuList.Mode.Step1;

    public static void Trigger()
    {
        Logger.Info($"=== ProbeRunner: F12 → KungfuList / {Mode} ===");
        ProbeKungfuList.Run(Mode);
        Logger.Info("=== ProbeRunner: end ===");
    }

    public static void CycleMode()
    {
        var cur = Mode;
        Mode = (ProbeKungfuList.Mode)(((int)cur + 1) % 5);
        Logger.Info($"ProbeRunner.Mode = {Mode}");
    }
}
```

- [ ] **Step 5.4: ModWindow.cs 의 Update 에 F12 / F10 핫키 추가**

`src/LongYinRoster/UI/ModWindow.cs` 의 `Update` method 안에 추가 (기존 F11 toggle 옆):

```csharp
private void Update()
{
    if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

    // v0.5.2 Spike — F12 trigger, F10 mode cycle (release 전 cleanup)
    if (Input.GetKeyDown(KeyCode.F12)) Core.Probes.ProbeRunner.Trigger();
    if (Input.GetKeyDown(KeyCode.F10)) Core.Probes.ProbeRunner.CycleMode();

    if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
        Time.timeScale = 0f;
}
```

- [ ] **Step 5.5: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

- [ ] **Step 5.6: Commit**

```bash
git add src/LongYinRoster/Core/Probes/ src/LongYinRoster/UI/ModWindow.cs
git commit -m "spike(v0.5.2): ProbeKungfuList + F12 trigger — 5 mode"
```

---

### Task 6: Spike Step 1 — Method dump 실행 (사용자)

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md` (이 task 에서 신규)

- [ ] **Step 6.1: 사용자 안내 — 게임 시작 + F12 (Mode = Step1)**

사용자에게:
1. 게임 시작 + 캐릭터 load
2. mod F11 끔 (또는 그대로)
3. F12 누름 → BepInEx 로그에 method dump 출력

- [ ] **Step 6.2: BepInEx 로그 확인**

```bash
grep -n "Spike Step1\|method:" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -50
```

Expected: HeroData 의 `Lose|Learn|Add|Clear|Remove|Get|Drop` * `Kungfu|Skill` method 시그니처 list 출력. 후보들 (예시):
- `Void LoseAllKungfu()`
- `Void LearnKungfu(Int32 skillID, Int32 lv)`
- 또는 다른 시그니처

- [ ] **Step 6.3: dump 파일 작성**

`docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md` (XX = 실행 일자):

```markdown
# v0.5.2 Spike — 무공 list method discovery (2026-05-XX)

## Step 1 — Method dump

**실행**: F12 → Mode = Step1

**결과**:
[BepInEx 로그의 method list 복사]

**clear method 후보**: [발견된 후보들]
**add method 후보**: [발견된 후보들]

**판정**: [PASS — 후보 발견 / FAIL — 후보 0 — user gate]
```

---

### Task 7: Spike Step 2 — Clear method 시도

**Files:**
- Update: `docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md`

- [ ] **Step 7.1: 게임 보호 — 현재 캐릭터 SaveSlot save (Spike FAIL 시 reload 위해)**

사용자 안내: 현재 캐릭터 진도를 잃지 않도록 game save 한 번.

- [ ] **Step 7.2: F10 → Mode = Step2 → F12**

사용자 안내:
1. F10 한 번 누름 (`ProbeRunner.Mode = Step2` 확인)
2. F12 → Step 2 실행

```bash
grep -n "Spike Step2" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -10
```

Expected: 후보 method 호출 + count 변화 로그. 예: `Spike Step2: LoseAllKungfu() — count 130 → 0`.

- [ ] **Step 7.3: 게임 무공 패널 확인**

사용자 보고: 무공 패널이 비워졌는지 (count = 0) 또는 일부 보존된 무공 (영구 무공 등) 만 남았는지 확인.

- [ ] **Step 7.4: dump 파일 update**

```markdown
## Step 2 — Clear 시도

**method**: [실제 작동한 method name]
**결과**: count 130 → N (N = remaining)
**UI**: [무공 패널이 비워졌는지]

**판정**: [PASS / FAIL]
```

- [ ] **Step 7.5: SaveSlot reload — 현재 캐릭터 baseline 복원**

사용자 안내: 메인 → 위에 save 한 SaveSlot reload → 무공 list 복원. 그 후 Step 3 진행.

---

### Task 8: Spike Step 3 — Add method 시도

**Files:**
- Update: `docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md`

- [ ] **Step 8.1: F10 → Mode = Step3 → F12**

사용자 안내:
1. F10 → Mode = Step3
2. F12 → Step 3 실행

테스트 skillID 는 `ProbeKungfuList.RunStep3` 의 hardcoded 100. 실제 게임의 skillID 와 매칭 안 될 수도. dump 파일에서 결과 확인.

```bash
grep -n "Spike Step3" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -10
```

Expected: 후보 method 호출 + count 변화. 예: `Spike Step3: LearnKungfu(100, 1) — count 130 → 131`.

- [ ] **Step 8.2: 게임 무공 패널 확인**

새 무공 추가됐는지 사용자 확인.

- [ ] **Step 8.3: dump 파일 update**

```markdown
## Step 3 — Add 시도

**method**: [실제 작동한 method name + 시그니처]
**결과**: count 변화 + UI 새 무공 추가
**판정**: [PASS / FAIL]
```

- [ ] **Step 8.4: SaveSlot reload — baseline 복원**

---

### Task 9: Spike Step 5 — Persistence baseline

**Files:**
- Update: `docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md`

- [ ] **Step 9.1: F10 → Mode = Step5 → F12 (pre-save baseline)**

```bash
grep -n "Spike Step5" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -20
```

Expected: 현재 list 의 first 10 entries 출력 (skillID, lv, fightExp, equiped).

- [ ] **Step 9.2: 사용자 — game save → 종료 → 재시작 → load**

- [ ] **Step 9.3: F10 → Mode = Step5 → F12 (post-reload)**

```bash
grep -n "Spike Step5" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -20
```

Expected: pre-save 와 동일한 first 10 entries 출력.

- [ ] **Step 9.4: dump 파일 update**

```markdown
## Step 5 — Persistence baseline

**Pre-save first 10**: [list]
**Post-reload first 10**: [list]
**판정**: [PASS / FAIL]
```

---

### Task 10: User gate — Spike 결과 결정

**Files:**
- Update: `docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md`

- [ ] **Step 10.1: Spike 결과 종합**

dump 파일에 모든 step 결과 정리. 판정:
- All PASS → Phase 3 (Implementation) 진행
- 일부 FAIL → user gate 결정

- [ ] **Step 10.2: User decision (FAIL 시)**

옵션:
- **Wrapper ctor 재도전** (B approach) — `KungfuSkillLvData` IL2CPP wrapper ctor / factory 발견 + IL2CppListOps.Add. v0.4 PoC A1 FAIL 의 재시도
- **abort + 다른 sub-project** — 외형 / 인벤토리 / 창고 등으로 변경

- [ ] **Step 10.3: dump + commit**

```bash
git add docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md
git commit -m "spike(v0.5.2): method discovery 결과 — [PASS path / FAIL → user gate decision]"
```

---

## Phase 3 — Implementation (Spike PASS 후)

### Task 11: KungfuListApplierTests.cs 작성 (Failing tests)

**Files:**
- Create: `src/LongYinRoster.Tests/KungfuListApplierTests.cs`

- [ ] **Step 11.1: Test 파일 신규 작성**

```csharp
// File: src/LongYinRoster.Tests/KungfuListApplierTests.cs

using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class KungfuListApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void ExtractKungfuList_ReturnsAllEntries()
    {
        var slot = ParseSlot(@"{
          ""kungfuSkills"": [
            {""skillID"": 100, ""lv"": 1, ""fightExp"": 0, ""bookExp"": 0, ""equiped"": false},
            {""skillID"": 200, ""lv"": 5, ""fightExp"": 100, ""bookExp"": 50, ""equiped"": true}
          ]
        }");
        var list = KungfuListApplier.ExtractKungfuList(slot);
        list.Count.ShouldBe(2);
        list[0].SkillID.ShouldBe(100);
        list[1].SkillID.ShouldBe(200);
        list[1].Lv.ShouldBe(5);
    }

    [Fact]
    public void ExtractKungfuList_HandlesEmptyList()
    {
        var slot = ParseSlot(@"{ ""kungfuSkills"": [] }");
        var list = KungfuListApplier.ExtractKungfuList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractKungfuList_MissingKungfuSkills_ReturnsEmpty()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var list = KungfuListApplier.ExtractKungfuList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RespectsApplySelection_SkipsWhenFalse()
    {
        var slot = ParseSlot(@"{ ""kungfuSkills"": [{""skillID"": 100, ""lv"": 1}] }");
        var sel = new ApplySelection { KungfuList = false };
        var result = KungfuListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }

    [Fact]
    public void Apply_HandlesMissingPlayer_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""kungfuSkills"": [{""skillID"": 100, ""lv"": 1}] }");
        var sel = new ApplySelection { KungfuList = true };
        var result = KungfuListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("player null");
    }
}
```

- [ ] **Step 11.2: Run tests — should fail (KungfuListApplier 없음)**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~KungfuListApplierTests"
```

Expected: FAIL — `KungfuListApplier` 또는 `ExtractKungfuList` not found.

---

### Task 12: KungfuListApplier.cs 작성

**Files:**
- Create: `src/LongYinRoster/Core/KungfuListApplier.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` (link 추가)

- [ ] **Step 12.1: KungfuListApplier.cs 신규 작성**

Spike Phase 1 에서 발견된 method name 을 hardcoded 로 사용 (예: `LoseAllKungfu` clear / `LearnKungfu(int, int)` add — 실제는 Spike 결과 따라 변경):

```csharp
// File: src/LongYinRoster/Core/KungfuListApplier.cs

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5.2 — 무공 list Replace (clear + add all).
///
/// v0.5.1 Phase B Harmony trace 의 game 자체 패턴 mirror — game-self method 발견 후 wrapper
/// ctor 우회. clear method (예: LoseAllKungfu) 호출 후 slot list 의 each entry 마다 add
/// method (예: LearnKungfu(skillID, lv)) 호출.
///
/// v0.4 PoC A1 의 KungfuSkillLvData wrapper ctor IL2CPP 한계 회피.
/// </summary>
public static class KungfuListApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int RemovedCount { get; set; }
        public int AddedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public sealed record KungfuEntry(int SkillID, int Lv, float FightExp, float BookExp);

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Spike Phase 1 결과로 확정된 method names
    // 실제 method name 은 Step 6.2 의 dump 결과 보고 결정. 여기는 placeholder.
    private const string ClearMethodName = "LoseAllKungfu";
    private const string AddMethodName   = "LearnKungfu";

    public static IReadOnlyList<KungfuEntry> ExtractKungfuList(JsonElement slot)
    {
        var list = new List<KungfuEntry>();
        if (!slot.TryGetProperty("kungfuSkills", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("skillID", out var idEl) || idEl.ValueKind != JsonValueKind.Number) continue;
            int skillID = idEl.GetInt32();
            int lv = entry.TryGetProperty("lv", out var lvEl) && lvEl.ValueKind == JsonValueKind.Number ? lvEl.GetInt32() : 1;
            float fe = entry.TryGetProperty("fightExp", out var feEl) && feEl.ValueKind == JsonValueKind.Number ? feEl.GetSingle() : 0f;
            float be = entry.TryGetProperty("bookExp", out var beEl) && beEl.ValueKind == JsonValueKind.Number ? beEl.GetSingle() : 0f;
            list.Add(new KungfuEntry(skillID, lv, fe, be));
        }
        return list;
    }

    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.KungfuList)
        {
            res.Skipped = true;
            res.Reason = "kungfuList (selection off)";
            return res;
        }

        var list = ExtractKungfuList(slot);
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

        // Clear phase
        int beforeCount = IL2CppListOps.Count(ksList);
        try
        {
            InvokeMethod(player, ClearMethodName, Array.Empty<object>());
            int afterCount = IL2CppListOps.Count(ksList);
            res.RemovedCount = beforeCount - afterCount;
            Logger.Info($"KungfuList clear ({ClearMethodName}): {beforeCount} → {afterCount}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"KungfuList clear: {ex.GetType().Name}: {ex.Message}");
            res.Skipped = true;
            res.Reason = $"clear failed: {ex.Message}";
            return res;
        }

        // Add phase
        foreach (var entry in list)
        {
            try
            {
                InvokeMethod(player, AddMethodName, new object[] { entry.SkillID, entry.Lv });
                res.AddedCount++;
            }
            catch (Exception ex)
            {
                res.FailedCount++;
                Logger.Warn($"KungfuList add skillID={entry.SkillID}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Logger.Info($"KungfuList Apply done — removed={res.RemovedCount} added={res.AddedCount} failed={res.FailedCount}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
    {
        return Apply(player, backup, new ApplySelection { KungfuList = true });
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

- [ ] **Step 12.2: Tests csproj 에 KungfuListApplier.cs link 추가**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 의 `<ItemGroup>` 안 (다른 link 들 사이에) 추가:

```xml
<Compile Include="../LongYinRoster/Core/KungfuListApplier.cs">
  <Link>Core/KungfuListApplier.cs</Link>
</Compile>
```

- [ ] **Step 12.3: Run tests — should pass**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~KungfuListApplierTests"
```

Expected: 5/5 PASS.

- [ ] **Step 12.4: 전체 tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 56+ PASS (50 baseline + 5 KungfuListApplier + 1+ Capabilities + 3 ApplySelection 등).

- [ ] **Step 12.5: Commit**

```bash
git add src/LongYinRoster/Core/KungfuListApplier.cs src/LongYinRoster.Tests/KungfuListApplierTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(core): KungfuListApplier — clear + add all + 5 tests"
```

---

### Task 13: PinpointPatcher.RebuildKungfuSkills 본문 교체 + step 순서

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`

- [ ] **Step 13.1: RebuildKungfuSkills 본문 교체**

기존 `SkipKungfuSkills` (line ~424-427) 를 제거하고 `RebuildKungfuSkills` 추가:

```csharp
// 기존
private static void SkipKungfuSkills(ApplyResult res)
{
    res.SkippedFields.Add("kungfuSkills — collection rebuild deferred to v0.5+");
}

// v0.5.2 교체
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
```

- [ ] **Step 13.2: Step 순서 변경 — RebuildKungfuSkills 를 SetActiveKungfu 직전으로**

`Apply` method 의 TryStep 순서 (line ~40-48) 변경:

```csharp
// v0.5.1
TryStep("SetSimpleFields",         () => SetSimpleFields(slot, currentPlayer, selection, res), res);
TryStep("SetIdentityFields",       () => SetIdentityFields(slot, currentPlayer, selection, res), res);
TryStep("SetActiveKungfu",         () => SetActiveKungfu(slot, currentPlayer, selection, res), res);
TryStep("RebuildItemList",         () => RebuildItemList(slot, currentPlayer, selection, res), res);
TryStep("RebuildSelfStorage",      () => RebuildSelfStorage(slot, currentPlayer, selection, res), res);
TryStep("RebuildKungfuSkills",     () => SkipKungfuSkills(res), res);
TryStep("RebuildHeroTagData",      () => RebuildHeroTagData(slot, currentPlayer, selection, res), res);
TryStep("RefreshSelfState",        () => RefreshSelfState(currentPlayer, res), res, fatal: true);
TryStep("RefreshExternalManagers", () => RefreshExternalManagers(currentPlayer, res), res);

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
```

- [ ] **Step 13.3: ProbeKungfuListCapability 추가**

`Probe()` method 와 `ProbeIdentityCapability` / `ProbeActiveKungfuCapability` / `ProbeItemListCapability` 옆에 추가:

```csharp
public static Capabilities Probe()
{
    if (_capCache != null) return _capCache;
    var p = HeroLocator.GetPlayer();
    if (p == null) return _capCache = Capabilities.AllOff();

    bool identity     = ProbeIdentityCapability(p);
    bool activeKungfu = ProbeActiveKungfuCapability(p);
    bool itemList     = ProbeItemListCapability(p);
    bool selfStorage  = itemList;
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

private static bool ProbeKungfuListCapability(object p)
{
    // v0.5.2 — Spike PASS 후 method names 확정.
    // ClearMethodName / AddMethodName 둘 다 존재해야 capability ok.
    return p.GetType().GetMethod("LoseAllKungfu", F, null, Type.EmptyTypes, null) != null
        && p.GetType().GetMethod("LearnKungfu", F, null, new[] { typeof(int), typeof(int) }, null) != null;
}
```

(method names 는 Spike Phase 1 결과 따라 변경)

- [ ] **Step 13.4: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

- [ ] **Step 13.5: 전체 tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 56+ PASS, 회귀 없음.

- [ ] **Step 13.6: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "feat(core): PinpointPatcher.RebuildKungfuSkills 본문 + step 순서 변경 (RebuildKungfuSkills → SetActiveKungfu)"
```

---

### Task 14: SlotDetailPanel 10 카테고리 grid 확장

**Files:**
- Modify: `src/LongYinRoster/UI/SlotDetailPanel.cs`

- [ ] **Step 14.1: DrawApplySelectionGrid 의 Row 추가**

기존 9 카테고리 (3 row × 3 col) → 10 카테고리. Option a: 4 row × 3 col (row 4 에 KungfuList 만, 빈 칸 2):

```csharp
private void DrawApplySelectionGrid(int slotIndex, ApplySelection sel, Capabilities cap)
{
    GUILayout.Label(KoreanStrings.ApplySectionHeader);
    bool changed = false;

    // Row 1: 스탯 / 명예 / 천부 (v0.3 검증 — 항상 enabled)
    GUILayout.BeginHorizontal();
    changed |= ToggleCell(KoreanStrings.Cat_Stat,         sel.Stat,         enabled: true,            v => sel.Stat = v);
    changed |= ToggleCell(KoreanStrings.Cat_Honor,        sel.Honor,        enabled: true,            v => sel.Honor = v);
    changed |= ToggleCell(KoreanStrings.Cat_TalentTag,    sel.TalentTag,    enabled: true,            v => sel.TalentTag = v);
    GUILayout.EndHorizontal();

    // Row 2: 스킨 / 자기집 add / 정체성
    GUILayout.BeginHorizontal();
    changed |= ToggleCell(KoreanStrings.Cat_Skin,         sel.Skin,         enabled: true,            v => sel.Skin = v);
    changed |= ToggleCell(KoreanStrings.Cat_SelfHouse,    sel.SelfHouse,    enabled: true,            v => sel.SelfHouse = v);
    changed |= ToggleCell(KoreanStrings.Cat_Identity,     sel.Identity,     enabled: cap.Identity,    v => sel.Identity = v);
    GUILayout.EndHorizontal();

    // Row 3: 무공 active / 인벤토리 / 창고
    GUILayout.BeginHorizontal();
    changed |= ToggleCell(KoreanStrings.Cat_ActiveKungfu, sel.ActiveKungfu, enabled: cap.ActiveKungfu, v => sel.ActiveKungfu = v);
    changed |= ToggleCell(KoreanStrings.Cat_ItemList,     sel.ItemList,     enabled: cap.ItemList,     v => sel.ItemList = v);
    changed |= ToggleCell(KoreanStrings.Cat_SelfStorage,  sel.SelfStorage,  enabled: cap.SelfStorage,  v => sel.SelfStorage = v);
    GUILayout.EndHorizontal();

    // Row 4: 무공 목록 (v0.5.2 신규)
    GUILayout.BeginHorizontal();
    changed |= ToggleCell(KoreanStrings.Cat_KungfuList,   sel.KungfuList,   enabled: cap.KungfuList,   v => sel.KungfuList = v);
    GUILayout.EndHorizontal();

    if (changed)
        OnApplySelectionChanged?.Invoke(slotIndex, sel);
}
```

- [ ] **Step 14.2: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

- [ ] **Step 14.3: 전체 tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 56+ PASS.

- [ ] **Step 14.4: Commit**

```bash
git add src/LongYinRoster/UI/SlotDetailPanel.cs
git commit -m "feat(ui): SlotDetailPanel — 10 카테고리 grid (row 4 에 무공 목록 추가)"
```

---

## Phase 4 — Smoke 시나리오 (in-game 검증)

### Task 15: Smoke 시나리오 1 — 다른 캐릭터 무공 set Apply

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-v0.5.2-smoke.md`

- [ ] **Step 15.1: Pre — 다른 캐릭터 SaveSlot capture**

사용자 안내:
1. 강력한 무공 set 있는 다른 캐릭터의 SaveSlot N → game load
2. mod F11 → slot 1 [+] capture (그 캐릭터의 무공 set 저장)
3. 현재 캐릭터의 SaveSlot M load (다른 무공 list)

- [ ] **Step 15.2: Apply slot 1 (KungfuList + ActiveKungfu)**

사용자 안내:
1. mod F11 → slot 1 → ✓ KungfuList + ✓ ActiveKungfu → ▼ 덮어쓰기
2. confirm → toast
3. F11 끔 → 무공 패널 → list = slot 1 의 set 으로 변경 + active 도 매칭

- [ ] **Step 15.3: BepInEx 로그 확인**

```bash
grep -n "KungfuList Apply done\|ActiveKungfu Apply done" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -10
```

Expected:
- `KungfuList Apply done — removed=N added=M failed=K`
- `ActiveKungfu Apply done — unequip=X equip=Y missing=0` (missing=0 — list 가 정확해서)

- [ ] **Step 15.4: Save → reload persistence**

게임 save → 종료 → 재시작 → load → 무공 list + active 정확히 유지 사용자 확인.

- [ ] **Step 15.5: Smoke dump 파일 작성**

```markdown
# v0.5.2 smoke 결과 (2026-05-XX)

## 시나리오 1 — 다른 캐릭터 무공 set Apply

- Pre: slot 1 = NPC X 의 무공 set (count N)
- Pre: 현재 캐릭터 SaveSlot M load (다른 list)
- Apply slot 1 (KungfuList + ActiveKungfu)
- 결과: list = N, active 매칭 (missing=0)
- save → reload: 유지
- 판정: [PASS / FAIL]
```

---

### Task 16: Smoke 시나리오 2 — Self-Apply (lv 복원)

**Files:**
- Update: `docs/superpowers/dumps/2026-05-XX-v0.5.2-smoke.md`

- [ ] **Step 16.1: Pre — 자기 자신 capture**

사용자 안내:
1. SaveSlot N (현재 캐릭터) load → mod slot 1 capture
2. 게임에서 무공 일부 변경 (새 무공 학습 또는 기존 무공 lv up)

- [ ] **Step 16.2: Apply slot 1 (자동백업 → slot 0)**

사용자 안내:
1. mod F11 → slot 1 → ✓ KungfuList → ▼ Apply
2. F11 끔 → list 가 slot 1 시점의 진도 (lv / fightExp) 로 복원

- [ ] **Step 16.3: 검증**

- list count = slot 1 시점 count
- 변경했던 무공의 lv 가 slot 1 시점으로 복원

- [ ] **Step 16.4: Save → reload 확인**

- [ ] **Step 16.5: Smoke dump update**

---

### Task 17: Smoke 시나리오 3 — Restore (slot 0 자동백업)

**Files:**
- Update: `docs/superpowers/dumps/2026-05-XX-v0.5.2-smoke.md`

- [ ] **Step 17.1: 시나리오 2 의 step 2 직후 (slot 0 = 변경된 list)**

- [ ] **Step 17.2: mod slot 0 → ↶ 복원**

- [ ] **Step 17.3: list 가 변경된 시점 (자동백업) 으로 복귀 확인**

- [ ] **Step 17.4: Save → reload**

- [ ] **Step 17.5: Smoke dump update**

---

### Task 18: 회귀 시나리오

**Files:**
- Update: `docs/superpowers/dumps/2026-05-XX-v0.5.2-smoke.md`

- [ ] **Step 18.1: v0.5.1 active 회귀 — KungfuList off + ActiveKungfu on**

- [ ] **Step 18.2: 천부 / 정체성 / 스탯 등 회귀**

- [ ] **Step 18.3: 외형 / 인벤토리 / 창고 disabled 표시 유지**

- [ ] **Step 18.4: legacy 슬롯 (v0.3/v0.4/v0.5.1) 호환**

- [ ] **Step 18.5: Smoke dump update + commit**

```bash
git add docs/superpowers/dumps/2026-05-XX-v0.5.2-smoke.md
git commit -m "docs: v0.5.2 smoke 결과 — 시나리오 1/2/3 + 회귀 [PASS]"
```

---

## Phase 5 — Release (Smoke PASS 후)

### Task 19: Probe 코드 cleanup

**Files:**
- Delete: `src/LongYinRoster/Core/Probes/` (전체)
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (F12 / F10 핫키 제거)

- [ ] **Step 19.1: Probes 디렉토리 삭제**

```bash
git rm -r src/LongYinRoster/Core/Probes/
```

- [ ] **Step 19.2: ModWindow.Update 의 F12 / F10 핫키 제거**

```csharp
private void Update()
{
    if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

    if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
        Time.timeScale = 0f;
}
```

- [ ] **Step 19.3: Build + tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: BUILD SUCCEEDED, 56+ PASS.

- [ ] **Step 19.4: Commit**

```bash
git add -A
git commit -m "chore(release): remove Probe code + F12/F10 handlers (D16 패턴)"
```

---

### Task 20: VERSION + README + HANDOFF

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`
- Modify: `README.md`
- Modify: `docs/HANDOFF.md`

- [ ] **Step 20.1: VERSION 0.5.1 → 0.5.2**

`src/LongYinRoster/Plugin.cs:17`:
```csharp
public const string VERSION = "0.5.2";
```

- [ ] **Step 20.2: README — v0.5.2 highlights + 10 카테고리 + Releases**

`README.md` 의 v0.5.1 섹션 다음에 v0.5.2 섹션 추가:

```markdown
### v0.5.2 — 무공 list 활성화

- **무공 list 카테고리 활성화** — slot 의 무공 list 를 player 에 완전 교체 (clear + add all)
- **lv / fightExp / bookExp 보존** — 각 무공의 학습 진도까지 slot 값으로 복원
- **v0.5.1 active 의 N7 자동 해소** — 다른 캐릭터의 active set Apply 가능 (missing=0)
- **알고리즘**: v0.5.1 의 game 자체 패턴 mirror — `LoseAllKungfu` clear + `LearnKungfu(skillID, lv)` add all
- **step 순서**: `RebuildKungfuSkills` 가 `SetActiveKungfu` 직전 — list 정확화 후 active 매칭
- **새 카테고리 default off** — 사용자 명시 토글 필요 (보수적, v0.4 active 패턴 mirror)
```

Releases 표:
```
| v0.5.2 | 무공 list 활성화 (clear + add all + lv 복원) |
```

- [ ] **Step 20.3: HANDOFF — §1, §2, §6**

`docs/HANDOFF.md`:
- §1 한 줄 요약 → main baseline = v0.5.2
- §1 Releases list → v0.5.2 entry
- §2 git history → v0.5.2 commits
- §6 v0.6 후보 갱신 (외형 / 인벤토리 / 창고 / UI cache 일반화)

- [ ] **Step 20.4: Build 검증 — 새 VERSION 로딩**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED. 게임 실행 시 BepInEx 로그에 `Loaded LongYin Roster Mod v0.5.2`.

- [ ] **Step 20.5: Commit**

```bash
git add src/LongYinRoster/Plugin.cs README.md docs/HANDOFF.md
git commit -m "chore(release): v0.5.2 — VERSION bump + README/HANDOFF update"
```

---

### Task 21: dist + tag

**Files:**
- Create: `dist/LongYinRoster_v0.5.2/`, `dist/LongYinRoster_v0.5.2.zip`
- Tag: `v0.5.2`

- [ ] **Step 21.1: dist 디렉토리 생성**

```bash
mkdir -p "dist/LongYinRoster_v0.5.2/BepInEx/plugins/LongYinRoster"
cp "src/LongYinRoster/bin/Release/LongYinRoster.dll" "dist/LongYinRoster_v0.5.2/BepInEx/plugins/LongYinRoster/"
cp README.md "dist/LongYinRoster_v0.5.2/"
```

- [ ] **Step 21.2: zip 생성 (PowerShell)**

```powershell
Set-Location "E:\Games\龙胤立志传.v1.0.0f8.2\LongYinLiZhiZhuan\Save\_PlayerExport"
Compress-Archive -Path "dist\LongYinRoster_v0.5.2\*" -DestinationPath "dist\LongYinRoster_v0.5.2.zip" -Force
```

- [ ] **Step 21.3: tag 생성**

```bash
git tag -a v0.5.2 -m "v0.5.2 — 무공 list 활성화 (clear + add all + lv 복원)"
git tag --list "v0.5*"
```

Expected: `v0.5.1` + `v0.5.2`.

- [ ] **Step 21.4: main merge + push (사용자 결정 후)**

```bash
git checkout main
git merge --no-ff v0.5.2 -m "Merge v0.5.2 — 무공 list 활성화"
git push origin main
git push origin refs/tags/v0.5.2
```

- [ ] **Step 21.5: GitHub release**

```bash
gh release create v0.5.2 dist/LongYinRoster_v0.5.2.zip \
  --repo game-mod-project/long_yin_li_zhi_zhuan_mode \
  --title "v0.5.2 — 무공 list 활성화" \
  --notes-file <release-notes.md>
```

---

## Phase 6 — Alternate flow (Spike FAIL 후)

### Task 22 (alt): Wrapper ctor 재도전

**전제**: Task 10 (User gate) 에서 "wrapper ctor 재도전" 결정.

**Files:**
- Modify: `src/LongYinRoster/Core/Probes/ProbeKungfuList.cs` (new mode 추가)

- [ ] **Step 22.1: KungfuSkillLvData ctor reflection scan**

`KungfuSkillLvData` 의 ctor / factory 발견 시도. 후보:
- `KungfuSkillLvData()` parameterless ctor
- `KungfuSkillLvData(int skillID, int lv)` 유사 시그니처
- `KungfuSkillLvData.Create(...)` static factory
- IL2CPP `IntPtr` ctor (Il2CppInterop 패턴)

- [ ] **Step 22.2: ctor 호출 + IL2CppListOps.Add 시도**

ctor 작동 시 wrapper 인스턴스 생성 → ksList 에 IL2CppListOps.Add 로 직접 추가.

- [ ] **Step 22.3: Spike Step 4 통합 시도**

ctor + IL2CppListOps.Clear + IL2CppListOps.Add all 로 list 교체.

- [ ] **Step 22.4: 결과 dump + decision**

PASS → KungfuListApplier 의 ClearMethodName / AddMethodName 대신 ctor + IL2CppListOps 사용. impl 진행.
FAIL → Task 23 (abort).

---

### Task 23 (alt): Abort + maintenance

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-v0.5.2-fail.md`

- [ ] **Step 23.1: FAIL dump 작성**

- [ ] **Step 23.2: Probe 코드 cleanup (release 안 해도)**

- [ ] **Step 23.3: PinpointPatcher 의 RebuildKungfuSkills 원복 (SkipKungfuSkills stub)**

- [ ] **Step 23.4: HANDOFF 갱신 — v0.5.2 abort**

- [ ] **Step 23.5: sub-project 변경 결정**

---

## Self-Review Checklist

**1. Spec coverage**:
- [x] §1 Context — Phase 1 baseline + Spike + impl 단계 ✓
- [x] §2 Goals (8 항목) — Task 11-18 (Apply + Restore + persistence + Capabilities + UI + legacy + 회귀) ✓
- [x] §2 Non-goals — Task 22-23 alternate flow ✓
- [x] §3.1 Hybrid flow — Phase 1 → 2 (Spike) → 3 (Impl) → 4 (Smoke) → 5 (Release) ✓
- [x] §3.3 영향 파일 — File Structure section 일치 ✓
- [x] §4 Spike Phase 1 detail — Task 5-10 ✓
- [x] §5 Implementation 설계 — Task 11-14 (Applier + Patcher + Capabilities + ApplySelection + UI) ✓
- [x] §6 Smoke 시나리오 1-3 + 회귀 — Task 15-18 ✓
- [x] §7 Failure mode — Task 10, 22-23 alternate ✓
- [x] §8 Release / Git plan — Task 19-21 ✓
- [x] §9 v0.6+ 후보 — Task 20 의 HANDOFF 갱신 ✓
- [x] Appendix A wrapper shape — Task 12 의 KungfuEntry record 에 반영 ✓

**2. Placeholder scan**:
- "TBD" / "TODO" 없음 ✓
- 일자 placeholder (`2026-05-XX`) 는 Spike 실행 시점 미정 — 의도된 placeholder ✓
- method names (`LoseAllKungfu` / `LearnKungfu`) 는 Spike 결과 따라 변경됨 — 코드 주석으로 명시 ✓

**3. Type consistency**:
- `KungfuListApplier.Apply(player, slot, sel)` — Task 11/12/13 일관 ✓
- `Result.Skipped / Reason / RemovedCount / AddedCount / FailedCount` — 일관 ✓
- `ExtractKungfuList(JsonElement)` → `IReadOnlyList<KungfuEntry>` — Task 11/12 일관 ✓
- `KungfuEntry(int SkillID, int Lv, float FightExp, float BookExp)` — record 일관 ✓
- `Capabilities.KungfuList` — Task 2/13 일관 ✓
- `ApplySelection.KungfuList` — Task 3/12/13/14 일관 ✓
- `KoreanStrings.Cat_KungfuList` — Task 4/14 일관 ✓

수정 항목 없음 — plan 그대로 진행.

---

**Plan complete.**
