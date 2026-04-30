# LongYin Roster Mod v0.4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v0.3 의 PinpointPatcher 7-step pipeline 을 9-step + selection-aware 로 확장하여 정체성 / 인벤토리 / 창고 / 무공 active 활성화 + SlotDetailPanel 체크박스 UI 인프라 구축. 부상/충성/호감 backup 은 영구 보존으로 회귀.

**Architecture:** `ApplySelection` POCO (9 boolean) + `Capabilities` Probe (Plugin 시작 시 1 회 cache) + `SimpleFieldMatrix.FieldCategory` enum + slot JSON `_meta.applySelection` schema 추가 + SlotDetailPanel 인라인 3x3 체크박스 grid (toggle 시 즉시 disk write).

**Tech Stack:** BepInEx 6 IL2CPP-CoreCLR, Il2CppInterop, .NET 6 (System.Text.Json), Harmony 2, IMGUI default skin (GUIStyle 인자 receiving overload 회피 — IL2CPP strip), xUnit (테스트).

**Spec**: [docs/superpowers/specs/2026-04-30-longyin-roster-mod-v0.4-design.md](../specs/2026-04-30-longyin-roster-mod-v0.4-design.md)

---

## File Structure

### New files
- `src/LongYinRoster/Core/ApplySelection.cs` — 9 boolean POCO, V03Default / RestoreAll / JSON 직렬화 helper
- `src/LongYinRoster/Core/Capabilities.cs` — 4 boolean POCO (Identity / ActiveKungfu / ItemList / SelfStorage). Probe 결과 cache
- `src/LongYinRoster/Core/IdentityFieldMatrix.cs` — 9 정체성 필드 매핑 (PoC 결과 path 명시)
- `src/LongYinRoster/Core/HeroDataDumpV04.cs` — 임시 PoC 진단 helper (release 전 제거)
- `src/LongYinRoster.Tests/ApplySelectionTests.cs` — JSON round-trip / V03Default / RestoreAll / MissingFieldFallback

### Modify
- `src/LongYinRoster/Core/PinpointPatcher.cs` — `Apply` 시그니처에 `ApplySelection` 인자 추가. `Probe` static 추가. 신규 step 4 개 (SetIdentityFields / SetActiveKungfu / RebuildItemList / RebuildSelfStorage)
- `src/LongYinRoster/Core/SimpleFieldMatrix.cs` — `FieldCategory` enum 추가, `SimpleFieldEntry` record 에 Category property 추가, 17 entry 매핑 (활성 무공 entry 제거, 부상/충성/호감 5 entry → Category=None)
- `src/LongYinRoster/Slots/SlotPayload.cs` — `SlotPayloadMeta` record 에 `ApplySelection` field 추가
- `src/LongYinRoster/Slots/SlotFile.cs` — `_meta.applySelection` JSON 직렬화/파싱
- `src/LongYinRoster/Slots/SlotRepository.cs` — `UpdateApplySelection(int slot, ApplySelection)` 메서드 추가
- `src/LongYinRoster/UI/SlotDetailPanel.cs` — 체크박스 grid 9개 + `Capabilities` 인자 추가 + Toggle 콜백
- `src/LongYinRoster/UI/ModWindow.cs` — `Capabilities` cache + `DoApply` 가 selection 전달 + `RequestRestore` 가 RestoreAll 사용
- `src/LongYinRoster/Util/KoreanStrings.cs` — 9 카테고리 한국어 label + disabled suffix + ApplySectionHeader
- `src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs` — Category 매핑 검증 + 17 entry count
- `src/LongYinRoster.Tests/SlotFileTests.cs` — applySelection round-trip
- `README.md` / `docs/HANDOFF.md` — release 시 갱신

### Remove (release 전)
- `src/LongYinRoster/Core/HeroDataDumpV04.cs`
- `Plugin.cs` 의 임시 `[F12]` PoC 핸들러 (있다면)

---

## Phase A — R&D PoC

PoC tasks 는 in-game 검증이라 표준 TDD 와 다르다. 패턴:
1. 임시 진단 helper 추가 (HeroDataDumpV04.cs) + `[F12]` 핸들러
2. Release build → 게임 실행 → 핫키로 PoC 시도
3. BepInEx 로그 / 정보창 / save→reload 로 결과 검증
4. 결과를 spec §6 / Capabilities 에 반영
5. 다음 PoC Task 진행

각 PoC Task 는 별도 commit. PoC 끝난 후 Task 14 에서 임시 코드 제거.

---

### Task A1: 임시 진단 helper 스켈레톤 + [F12] 핸들러

**Files:**
- Create: `src/LongYinRoster/Core/HeroDataDumpV04.cs`
- Modify: `src/LongYinRoster/Plugin.cs` (Update 메서드에 `[F12]` 핸들러 추가)

- [ ] **Step 1: HeroDataDumpV04 skeleton 작성**

```csharp
// src/LongYinRoster/Core/HeroDataDumpV04.cs
using System;
using System.Reflection;
using System.Text;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.4 PoC 임시 진단 helper. Release 전 Task 14 에서 제거.
/// [F12] 핸들러가 mode 별로 다른 PoC 분기 호출.
///
/// PoC mode:
///   1. Identity        — heroName setter / backing field / Harmony 검증
///   2. ActiveKungfu    — kungfuSkills wrapper 찾기 + SetNowActiveSkill 호출
///   3. ItemData        — IntPtr ctor / static factory / GetItem hijack 후보
///   4. ItemListClear   — LoseAllItem 부수효과 검증
/// </summary>
public static class HeroDataDumpV04
{
    public enum Mode { Identity, ActiveKungfu, ItemData, ItemListClear }

    public static void Run(Mode mode)
    {
        var player = HeroLocator.GetPlayer();
        if (player == null) { Logger.Warn("HeroDataDumpV04: no player"); return; }

        try
        {
            switch (mode)
            {
                case Mode.Identity:        ProbeIdentity(player); break;
                case Mode.ActiveKungfu:    ProbeActiveKungfu(player); break;
                case Mode.ItemData:        ProbeItemData(player); break;
                case Mode.ItemListClear:   ProbeItemListClear(player); break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"HeroDataDumpV04({mode}) threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ProbeIdentity(object player) => Logger.Info("ProbeIdentity: TBD A2");
    private static void ProbeActiveKungfu(object player) => Logger.Info("ProbeActiveKungfu: TBD A3");
    private static void ProbeItemData(object player) => Logger.Info("ProbeItemData: TBD A4");
    private static void ProbeItemListClear(object player) => Logger.Info("ProbeItemListClear: TBD A4");
}
```

- [ ] **Step 2: Plugin.cs Update 에 [F12] 핸들러 추가**

`Plugin.cs` 의 Update 메서드 끝에 추가:

```csharp
// v0.4 임시 PoC 진단 — Task 14 에서 제거
if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12))
{
    var mode = HeroDataDumpV04.Mode.Identity;          // 매번 PoC mode 바꿔서 빌드
    LongYinRoster.Core.HeroDataDumpV04.Run(mode);
}
```

(만약 Plugin 에 Update 가 없으면 `MonoBehaviour` 가 아닐 수 있음 — `BepInEx5Plugin` / `BepInEx6Plugin` 패턴 따라 핸들러 위치 결정. Plan 작성 시점 Plugin.cs 의 구조 확인)

- [ ] **Step 3: 빌드 + 게임 안 [F12] 한 번 → BepInEx 로그에 "ProbeIdentity: TBD A2" 확인**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: build success → 게임 실행 → F11 + 캐릭터 안에서 F12 → 로그에 `ProbeIdentity: TBD A2` 출력.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Core/HeroDataDumpV04.cs src/LongYinRoster/Plugin.cs
git commit -m "feat(poc): v0.4 HeroDataDumpV04 skeleton + [F12] handler"
```

---

### Task A2: PoC — 정체성 (Identity) 우회 path 검증

**Files:**
- Modify: `src/LongYinRoster/Core/HeroDataDumpV04.cs` (ProbeIdentity 본문 채움)

- [ ] **Step 1: ProbeIdentity 본문 — 시도 A (setter 직접) + 시도 B (backing field) 동시 실행**

```csharp
private static void ProbeIdentity(object player)
{
    var t = player.GetType();
    string original = (string)(t.GetProperty("heroName")?.GetValue(player) ?? "");
    Logger.Info($"ProbeIdentity: original heroName={original}");

    // 시도 A — setter 직접
    string testA = original + "_A";
    try
    {
        t.GetProperty("heroName")?.SetValue(player, testA);
        string after = (string)(t.GetProperty("heroName")?.GetValue(player) ?? "");
        Logger.Info($"ProbeIdentity A (setter): set='{testA}' got='{after}' " +
                    $"{(after == testA ? "PASS" : "FAIL silent no-op")}");
    }
    catch (Exception ex) { Logger.Warn($"ProbeIdentity A threw: {ex.Message}"); }

    // 시도 B — backing field
    var bf = t.GetField("<heroName>k__BackingField",
                         BindingFlags.NonPublic | BindingFlags.Instance)
          ?? t.GetField("_heroName",
                         BindingFlags.NonPublic | BindingFlags.Instance);
    if (bf != null)
    {
        string testB = original + "_B";
        try
        {
            bf.SetValue(player, testB);
            string after = (string)(t.GetProperty("heroName")?.GetValue(player) ?? "");
            Logger.Info($"ProbeIdentity B (backing field {bf.Name}): set='{testB}' got='{after}' " +
                        $"{(after == testB ? "PASS" : "FAIL")}");
        }
        catch (Exception ex) { Logger.Warn($"ProbeIdentity B threw: {ex.Message}"); }
    }
    else
    {
        Logger.Warn("ProbeIdentity B: backing field not found via standard names");
        // enumerate fields 모두 dump
        foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance |
                                       BindingFlags.Public | BindingFlags.FlattenHierarchy))
            if (f.Name.ToLowerInvariant().Contains("name"))
                Logger.Info($"  field candidate: {f.Name} ({f.FieldType.Name})");
    }

    // 원래 값 복구 (게임 상태 오염 방지)
    try { t.GetProperty("heroName")?.SetValue(player, original); } catch { }
}
```

- [ ] **Step 2: 빌드 + 게임 안 F12 한 번 → 로그 분석**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

게임 실행 → F11 → 모드 창에서 캐릭터 안 → F12 → BepInEx/LogOutput.log 확인:

```
grep -A 2 "ProbeIdentity" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

판정:
- A=PASS → setter 직접 사용 가능 → IdentityFieldMatrix 의 path = "Setter"
- A=FAIL + B=PASS → backing field 사용 → path = "BackingField" + 필드 이름 명시
- A/B 모두 FAIL → Task A2-extra: Harmony postfix 시도

- [ ] **Step 3: spec §6.1 update — PoC 결과 명시**

`docs/superpowers/specs/2026-04-30-longyin-roster-mod-v0.4-design.md` §6.1 끝에 추가:

```markdown
**PoC 결과 (2026-04-30)**:
- 시도 A: [PASS/FAIL]
- 시도 B: [PASS/FAIL] (backing field 이름: _____)
- Capabilities.Identity = [true/false]
- IdentityFieldMatrix path 결정: [Setter / BackingField / Harmony]
```

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Core/HeroDataDumpV04.cs docs/superpowers/specs/2026-04-30-longyin-roster-mod-v0.4-design.md
git commit -m "poc: v0.4 Identity path validated [PASS or partial result]"
```

---

### Task A3: PoC — 무공 active 우회 검증

**Files:**
- Modify: `src/LongYinRoster/Core/HeroDataDumpV04.cs` (ProbeActiveKungfu 본문)

- [ ] **Step 1: ProbeActiveKungfu 본문**

```csharp
private static void ProbeActiveKungfu(object player)
{
    var t = player.GetType();

    // 1) 현재 nowActiveSkill 읽기
    int currentID = (int)(t.GetProperty("nowActiveSkill")?.GetValue(player) ?? -1);
    Logger.Info($"ProbeActiveKungfu: current nowActiveSkill={currentID}");

    // 2) kungfuSkills list 의 entry 들 enumerate
    var ksList = t.GetProperty("kungfuSkills")?.GetValue(player);
    if (ksList == null) { Logger.Warn("kungfuSkills null"); return; }
    int n = IL2CppListOps.Count(ksList);
    Logger.Info($"  kungfuSkills count = {n}");

    object? testWrapper = null;
    int testID = -1;
    for (int i = 0; i < n; i++)
    {
        var entry = IL2CppListOps.Get(ksList, i);
        if (entry == null) continue;
        var idProp = entry.GetType().GetProperty("skillID")
                  ?? entry.GetType().GetProperty("ID")
                  ?? entry.GetType().GetProperty("id");
        var lvProp = entry.GetType().GetProperty("lv");
        int id = idProp != null ? (int)idProp.GetValue(entry)! : -1;
        int lv = lvProp != null ? (int)lvProp.GetValue(entry)! : -1;
        Logger.Info($"  [{i}] type={entry.GetType().Name} skillID={id} lv={lv}");

        // 첫 entry 를 test 후보 (currentID 아닌 것)
        if (testWrapper == null && id != currentID && id > 0) { testWrapper = entry; testID = id; }
    }

    if (testWrapper == null) { Logger.Warn("no test wrapper candidate"); return; }
    Logger.Info($"  test candidate: skillID={testID}");

    // 3) SetNowActiveSkill 호출
    var m = t.GetMethod("SetNowActiveSkill",
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    if (m == null) { Logger.Warn("SetNowActiveSkill missing"); return; }
    try
    {
        m.Invoke(player, new[] { testWrapper });
        int after = (int)(t.GetProperty("nowActiveSkill")?.GetValue(player) ?? -1);
        Logger.Info($"ProbeActiveKungfu: SetNowActiveSkill done — nowActiveSkill={after} " +
                    $"{(after == testID ? "PASS" : "FAIL — value not changed")}");
    }
    catch (Exception ex)
    {
        Logger.Warn($"SetNowActiveSkill threw: {ex.GetType().Name}: {ex.Message}");
    }

    // 원복
    if (currentID > 0)
    {
        // 원래 wrapper 다시 찾아서 복원
        for (int i = 0; i < n; i++)
        {
            var entry = IL2CppListOps.Get(ksList, i);
            if (entry == null) continue;
            int id = (int)(entry.GetType().GetProperty("skillID")?.GetValue(entry) ?? -1);
            if (id == currentID) { try { m.Invoke(player, new[] { entry }); } catch { } break; }
        }
    }
}
```

- [ ] **Step 2: 빌드 + Plugin.cs 의 mode = ActiveKungfu 로 바꾸고 빌드**

```csharp
var mode = HeroDataDumpV04.Mode.ActiveKungfu;
```

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

게임 실행 → F11 → F12.

- [ ] **Step 3: 로그 분석**

```
grep "ProbeActiveKungfu" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

판정:
- "PASS" → Capabilities.ActiveKungfu = true. KungfuSkillLvData 의 ID property 이름 (skillID / ID / id) 명시
- "FAIL value not changed" → setter 가 silent no-op (Identity 시도 A 와 같은 함정) → 추가 R&D 필요
- 호출 throw → method 시그니처 다름 → 추가 dump

- [ ] **Step 4: spec §6.2 update + commit**

spec §6.2 끝에 PoC 결과 추가, commit.

```bash
git add src/LongYinRoster/Core/HeroDataDumpV04.cs src/LongYinRoster/Plugin.cs docs/superpowers/specs/2026-04-30-longyin-roster-mod-v0.4-design.md
git commit -m "poc: v0.4 ActiveKungfu wrapper-based SetNowActiveSkill validated"
```

---

### Task A4: PoC — 인벤토리/창고 (ItemData wrapper) 우회 검증

**Files:**
- Modify: `src/LongYinRoster/Core/HeroDataDumpV04.cs` (ProbeItemData / ProbeItemListClear 본문)

- [ ] **Step 1: ProbeItemData 본문 — IntPtr ctor + static factory enumerate**

```csharp
private static void ProbeItemData(object player)
{
    // 1) ItemData type 찾기
    Type? itemDataType = null;
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        try
        {
            foreach (var t in asm.GetTypes())
                if (t.Name == "ItemData" && t.Namespace != "LongYinRoster.Core")
                { itemDataType = t; break; }
        }
        catch { }
        if (itemDataType != null) break;
    }
    if (itemDataType == null) { Logger.Warn("ItemData type not found"); return; }
    Logger.Info($"ItemData type: {itemDataType.FullName}");

    // 2) ctors enumerate
    foreach (var ctor in itemDataType.GetConstructors(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        var ps = ctor.GetParameters();
        var sig = string.Join(",", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"));
        Logger.Info($"  ctor: ({sig})");
    }

    // 3) 시도 A: IntPtr ctor 직접 호출
    var ipCtor = itemDataType.GetConstructor(new[] { typeof(IntPtr) });
    if (ipCtor != null)
    {
        Logger.Info("  IntPtr ctor exists — Il2CppInterop wrapper 패턴");
        // valid IntPtr 가 필요 — game 의 기존 ItemData 의 .Pointer 사용
        // 또는 IL2CPP-side 의 il2cpp_object_new 호출
        // 여기서는 타입 존재만 확인
    }

    // 4) static factory candidates
    foreach (var m in itemDataType.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
    {
        if (m.ReturnType == itemDataType || m.ReturnType.Name == "ItemData")
            Logger.Info($"  static factory candidate: {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})");
    }

    // 5) game 안의 기존 ItemData 확인 (player.itemListData.allItem[0])
    var t = player.GetType();
    var itemListData = t.GetProperty("itemListData")?.GetValue(player);
    var allItem = itemListData?.GetType().GetProperty("allItem")?.GetValue(itemListData);
    if (allItem != null)
    {
        int n = IL2CppListOps.Count(allItem);
        Logger.Info($"  player.itemListData.allItem count = {n}");
        if (n > 0)
        {
            var first = IL2CppListOps.Get(allItem, 0);
            Logger.Info($"    [0] type={first?.GetType().FullName}");
            // first 의 properties enumerate
            if (first != null)
                foreach (var p in first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Logger.Info($"      .{p.Name} = {p.GetValue(first)}");
        }
    }
}

private static void ProbeItemListClear(object player)
{
    var t = player.GetType();
    var preCount = IL2CppListOps.Count(
        t.GetProperty("itemListData")?.GetValue(player)?
            .GetType().GetProperty("allItem")?
            .GetValue(t.GetProperty("itemListData")?.GetValue(player)) ?? new object());
    Logger.Info($"ProbeItemListClear: pre LoseAllItem count={preCount}");

    var m = t.GetMethod("LoseAllItem", BindingFlags.Public | BindingFlags.Instance);
    if (m == null) { Logger.Warn("LoseAllItem missing"); return; }

    // 주의 — destructive! 자동백업 후에만 실행 권장. 아래는 진단 only — 게임 종료 후 다시 로드 권장
    Logger.Warn("ProbeItemListClear: NOT calling LoseAllItem (destructive). Rebuild build with explicit flag to actually clear.");
}
```

- [ ] **Step 2: 빌드 + mode=ItemData 로 빌드 + 게임 안 F12**

```csharp
var mode = HeroDataDumpV04.Mode.ItemData;
```

- [ ] **Step 3: 로그 분석 + 결정**

```
grep -A 30 "ItemData type" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

판정:
- IntPtr ctor 존재 + game 의 기존 ItemData 의 .Pointer 추출 가능 → 시도 가능 (`ItemData(existingPtr)` 로 wrap, 단 새 객체 생성은 별도)
- static factory 발견 (예: `ItemData.Create(int id, int count)`) → 가장 깔끔
- 둘 다 fail → 시도 C: Harmony patch on 게임 method (예: 상점 구매 path)

이 task 의 결과로 v0.4 의 인벤토리/창고 활성화 가능 여부 결정 — Capabilities.ItemList / SelfStorage 의 boolean 확정.

- [ ] **Step 4: spec §6.3 update + commit**

```bash
git add src/LongYinRoster/Core/HeroDataDumpV04.cs src/LongYinRoster/Plugin.cs docs/superpowers/specs/2026-04-30-longyin-roster-mod-v0.4-design.md
git commit -m "poc: v0.4 ItemData wrapper enumeration — [PASS/FAIL/PARTIAL]"
```

---

## Phase B — Framework

PoC 결과로 Capabilities boolean 들 확정 후 framework 구축. 모든 task 가 unit test 우선 (TDD).

---

### Task B5: ApplySelection POCO + JSON helper

**Files:**
- Create: `src/LongYinRoster/Core/ApplySelection.cs`
- Create: `src/LongYinRoster.Tests/ApplySelectionTests.cs`

- [ ] **Step 1: failing test — JSON round-trip**

```csharp
// src/LongYinRoster.Tests/ApplySelectionTests.cs
using LongYinRoster.Core;
using Xunit;

namespace LongYinRoster.Tests;

public class ApplySelectionTests
{
    [Fact]
    public void V03Default_HasFourCategoriesOn()
    {
        var sel = ApplySelection.V03Default();
        Assert.True(sel.Stat);
        Assert.True(sel.Honor);
        Assert.True(sel.TalentTag);
        Assert.True(sel.Skin);
        Assert.False(sel.SelfHouse);
        Assert.False(sel.Identity);
        Assert.False(sel.ActiveKungfu);
        Assert.False(sel.ItemList);
        Assert.False(sel.SelfStorage);
    }

    [Fact]
    public void RestoreAll_HasAllNineOn()
    {
        var sel = ApplySelection.RestoreAll();
        Assert.True(sel.Stat);
        Assert.True(sel.Honor);
        Assert.True(sel.TalentTag);
        Assert.True(sel.Skin);
        Assert.True(sel.SelfHouse);
        Assert.True(sel.Identity);
        Assert.True(sel.ActiveKungfu);
        Assert.True(sel.ItemList);
        Assert.True(sel.SelfStorage);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllNine()
    {
        var orig = new ApplySelection
        {
            Stat = true, Honor = false, TalentTag = true, Skin = false,
            SelfHouse = true, Identity = false, ActiveKungfu = true,
            ItemList = false, SelfStorage = true,
        };
        string json = ApplySelection.ToJson(orig);
        var parsed = ApplySelection.FromJson(json);

        Assert.Equal(orig.Stat,         parsed.Stat);
        Assert.Equal(orig.Honor,        parsed.Honor);
        Assert.Equal(orig.TalentTag,    parsed.TalentTag);
        Assert.Equal(orig.Skin,         parsed.Skin);
        Assert.Equal(orig.SelfHouse,    parsed.SelfHouse);
        Assert.Equal(orig.Identity,     parsed.Identity);
        Assert.Equal(orig.ActiveKungfu, parsed.ActiveKungfu);
        Assert.Equal(orig.ItemList,     parsed.ItemList);
        Assert.Equal(orig.SelfStorage,  parsed.SelfStorage);
    }

    [Fact]
    public void FromJson_MissingFields_FallsBackToV03Default()
    {
        // v0.2 / v0.3 슬롯 호환 — applySelection 자체가 없거나 partial
        var partial = ApplySelection.FromJson("{}");
        Assert.True(partial.Stat);
        Assert.True(partial.Honor);
        Assert.False(partial.Identity);
    }
}
```

- [ ] **Step 2: 테스트 실행 → fail**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ApplySelectionTests"
```
Expected: 4 fails (`ApplySelection` undefined).

- [ ] **Step 3: ApplySelection 구현**

```csharp
// src/LongYinRoster/Core/ApplySelection.cs
using System.Text;
using System.Text.Json;

namespace LongYinRoster.Core;

/// <summary>
/// 9-카테고리 selection. 슬롯 JSON 의 _meta.applySelection 으로 영속.
/// V03Default = v0.3 호환 (스탯/명예/천부/스킨 on, 신규 5 off).
/// RestoreAll = 9 카테고리 모두 on (slot 0 자동백업 복원 시).
/// </summary>
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

    public static ApplySelection V03Default() => new();

    public static ApplySelection RestoreAll() => new()
    {
        Stat = true, Honor = true, TalentTag = true, Skin = true,
        SelfHouse = true, Identity = true, ActiveKungfu = true,
        ItemList = true, SelfStorage = true,
    };

    public bool AnyEnabled() =>
        Stat || Honor || TalentTag || Skin || SelfHouse ||
        Identity || ActiveKungfu || ItemList || SelfStorage;

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
        return s;
    }
}
```

- [ ] **Step 4: 테스트 재실행 → 4 PASS**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ApplySelectionTests"
```
Expected: 4 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Core/ApplySelection.cs src/LongYinRoster.Tests/ApplySelectionTests.cs
git commit -m "feat(core): v0.4 ApplySelection POCO + JSON helpers + 4 tests"
```

---

### Task B6: Capabilities POCO

**Files:**
- Create: `src/LongYinRoster/Core/Capabilities.cs`

- [ ] **Step 1: Capabilities 작성**

```csharp
// src/LongYinRoster/Core/Capabilities.cs
namespace LongYinRoster.Core;

/// <summary>
/// PinpointPatcher.Probe 결과 cache. Plugin 시작 시 1 회 결정 후 ModWindow 에 cache.
/// SlotDetailPanel 의 disabled 체크박스 결정에 사용.
///
/// v0.3 검증된 카테고리 (Stat / Honor / TalentTag / Skin / SelfHouse) 는
/// Capabilities 검사 안 함 — 항상 true 가정.
/// </summary>
public sealed class Capabilities
{
    public bool Identity     { get; init; }
    public bool ActiveKungfu { get; init; }
    public bool ItemList     { get; init; }
    public bool SelfStorage  { get; init; }

    public static Capabilities AllOff() => new();
    public static Capabilities AllOn() => new()
    {
        Identity = true, ActiveKungfu = true, ItemList = true, SelfStorage = true,
    };

    public override string ToString() =>
        $"Identity={Identity} ActiveKungfu={ActiveKungfu} " +
        $"ItemList={ItemList} SelfStorage={SelfStorage}";
}
```

- [ ] **Step 2: 빌드 확인** (테스트는 Task B7 와 함께 작성)

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: build success.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Core/Capabilities.cs
git commit -m "feat(core): v0.4 Capabilities POCO"
```

---

### Task B7: SimpleFieldMatrix Category enum + 17 entry 정정

**Files:**
- Modify: `src/LongYinRoster/Core/SimpleFieldMatrix.cs`
- Modify: `src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs` (테스트 추가, 기존 schema 테스트 변경)

- [ ] **Step 1: failing test — Category 매핑**

```csharp
// src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs
// (기존 SimpleFieldMatrix schema test 가 있으면 그 옆에 추가. 없으면 신규 file)
using LongYinRoster.Core;
using System.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class SimpleFieldMatrixTests
{
    [Fact]
    public void Entries_HasSeventeenAfterV04Refactor()
    {
        // v0.3 의 18 entry → v0.4 에서 활성 무공 (nowActiveSkill) entry 제거 → 17
        Assert.Equal(17, SimpleFieldMatrix.Entries.Count);
    }

    [Fact]
    public void Entries_HasNoActiveKungfuEntry()
    {
        Assert.DoesNotContain(SimpleFieldMatrix.Entries,
            e => e.PropertyName == "nowActiveSkill");
    }

    [Fact]
    public void Entries_InjuryAndLoyalAndFavor_AreCategoryNone()
    {
        var noneNames = new[] { "externalInjury", "internalInjury", "poisonInjury", "loyal", "favor" };
        foreach (var name in noneNames)
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(e => e.PropertyName == name);
            Assert.NotNull(e);
            Assert.Equal(FieldCategory.None, e!.Category);
        }
    }

    [Fact]
    public void Entries_HpManaPower_AreCategoryStat()
    {
        foreach (var name in new[] { "hp", "mana", "power" })
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == name);
            Assert.NotNull(e);
            Assert.Equal(FieldCategory.Stat, e!.Category);
        }
    }

    [Fact]
    public void Entries_FameAndBadFame_AreCategoryHonor()
    {
        foreach (var name in new[] { "fame", "badFame" })
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == name);
            Assert.NotNull(e);
            Assert.Equal(FieldCategory.Honor, e!.Category);
        }
    }

    [Fact]
    public void Entries_SkinID_IsCategorySkin()
    {
        var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == "skinID");
        Assert.NotNull(e);
        Assert.Equal(FieldCategory.Skin, e!.Category);
    }

    [Fact]
    public void Entries_SelfHouse_IsCategorySelfHouse()
    {
        var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == "selfHouseTotalAdd");
        Assert.NotNull(e);
        Assert.Equal(FieldCategory.SelfHouse, e!.Category);
    }

    [Fact]
    public void Entries_HeroTagPoint_IsCategoryTalentPoint()
    {
        var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == "heroTagPoint");
        Assert.NotNull(e);
        Assert.Equal(FieldCategory.TalentPoint, e!.Category);
    }

    [Fact]
    public void Entries_BaseStatLists_AreCategoryStat()
    {
        foreach (var name in new[] { "baseAttri", "baseFightSkill", "baseLivingSkill", "expLivingSkill" })
        {
            var e = SimpleFieldMatrix.Entries.FirstOrDefault(x => x.PropertyName == name);
            Assert.NotNull(e);
            Assert.Equal(FieldCategory.Stat, e!.Category);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 → fail**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SimpleFieldMatrixTests"
```
Expected: 9 fails (`FieldCategory` undefined, `Category` property missing).

- [ ] **Step 3: SimpleFieldMatrix 변경 — Category enum + record 확장 + 17 entry**

```csharp
// src/LongYinRoster/Core/SimpleFieldMatrix.cs
using System.Collections.Generic;

namespace LongYinRoster.Core;

public enum SetterStyle { Direct, Delta, None }

/// <summary>
/// v0.4 — entry 의 selection 분류. None 은 "영구 보존" (부상/충성/호감).
/// Stat / Honor / Skin / SelfHouse 는 ApplySelection 의 동명 flag 따라 selection.
/// TalentPoint 는 ApplySelection.TalentTag 와 묶여 selection (heroTagPoint 가 천부 카테고리 안).
/// </summary>
public enum FieldCategory
{
    None,         // 부상/충성/호감 — Apply 안 함, 영구 보존
    Stat,         // hp/mana/power + base stat lists
    Honor,        // fame/badFame
    Skin,         // skinID
    SelfHouse,    // selfHouseTotalAdd
    TalentPoint,  // heroTagPoint
}

public sealed record SimpleFieldEntry(
    string         Name,
    string         JsonPath,
    string         PropertyName,
    System.Type    Type,
    string?        SetterMethod,
    SetterStyle    SetterStyle,
    FieldCategory  Category);

public static class SimpleFieldMatrix
{
    /// <summary>
    /// v0.4: 17 entry. v0.3 18 entry 에서 활성 무공 (nowActiveSkill) 제거 — 별도 step 으로 이관.
    /// 부상/충성/호감 5 entry 의 Category=None — Apply 안 함 (v0.3 backup 폐기, 영구 보존 정책).
    /// </summary>
    public static readonly IReadOnlyList<SimpleFieldEntry> Entries = new[]
    {
        new SimpleFieldEntry("명예",            "fame",                "fame",                typeof(float), "ChangeFame",                 SetterStyle.Delta,  FieldCategory.Honor),
        new SimpleFieldEntry("악명",            "badFame",             "badFame",             typeof(float), "ChangeBadFame",              SetterStyle.Delta,  FieldCategory.Honor),
        new SimpleFieldEntry("HP",              "hp",                  "hp",                  typeof(float), "ChangeHp",                   SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("Mana",            "mana",                "mana",                typeof(float), "ChangeMana",                 SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("Power",           "power",               "power",               typeof(float), "ChangePower",                SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("외상",            "externalInjury",      "externalInjury",      typeof(float), "ChangeExternalInjury",       SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("내상",            "internalInjury",      "internalInjury",      typeof(float), "ChangeInternalInjury",       SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("중독",            "poisonInjury",        "poisonInjury",        typeof(float), "ChangePoisonInjury",         SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("충성",            "loyal",               "loyal",               typeof(float), "ChangeLoyal",                SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("호감",            "favor",               "favor",               typeof(float), "SetFavor",                   SetterStyle.Direct, FieldCategory.None),
        new SimpleFieldEntry("자기집 add",      "selfHouseTotalAdd",   "selfHouseTotalAdd",   typeof(float), "ChangeSelfHouseTotalAdd",    SetterStyle.Delta,  FieldCategory.SelfHouse),
        new SimpleFieldEntry("천부 포인트",     "heroTagPoint",        "heroTagPoint",        typeof(float), "ChangeTagPoint",             SetterStyle.Delta,  FieldCategory.TalentPoint),
        new SimpleFieldEntry("스킨",            "skinID",              "skinID",              typeof(int),   "SetSkin",                    SetterStyle.Direct, FieldCategory.Skin),
        new SimpleFieldEntry("baseAttri[i]",    "baseAttri",           "baseAttri",           typeof(float), "ChangeAttri",                SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("baseFightSkill[i]","baseFightSkill",     "baseFightSkill",      typeof(float), "ChangeFightSkill",           SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("baseLivingSkill[i]","baseLivingSkill",   "baseLivingSkill",     typeof(float), "ChangeLivingSkill",          SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("expLivingSkill[i]","expLivingSkill",     "expLivingSkill",      typeof(float), "ChangeLivingSkillExp",       SetterStyle.Delta,  FieldCategory.Stat),
    };
}
```

- [ ] **Step 4: 테스트 재실행 → 9 PASS**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SimpleFieldMatrixTests"
```
Expected: 9 PASS.

- [ ] **Step 5: 전체 회귀 테스트 — 기존 테스트 깨졌나 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 모든 기존 테스트 + 신규 9 = ~34 PASS.

만약 기존 PinpointPatcher 가 활성 무공 entry 의존이면 (entry.PropertyName == "nowActiveSkill" 분기 등) 빌드 오류 발생 — 그 분기 제거 필요. 하지만 PinpointPatcher 의 ApplySkinSpecialCase / ApplyListIndexedSpecialCase 만 special-case 처리하니 nowActiveSkill 은 default path 였음. entry 가 빠지면 default path 가 호출 안 됨 — OK.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Core/SimpleFieldMatrix.cs src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs
git commit -m "feat(core): SimpleFieldMatrix Category enum + v0.4 17 entry"
```

---

### Task B8: SlotPayloadMeta + SlotFile + ApplySelection 직렬화

**Files:**
- Modify: `src/LongYinRoster/Slots/SlotPayload.cs` (record 에 ApplySelection field)
- Modify: `src/LongYinRoster/Slots/SlotFile.cs` (write/parse 에 applySelection 추가)
- Modify: `src/LongYinRoster.Tests/SlotFileTests.cs`

- [ ] **Step 1: failing test — slot 파일에 applySelection 저장 + 로드**

`src/LongYinRoster.Tests/SlotFileTests.cs` 에 추가:

```csharp
[Fact]
public void Write_ThenRead_PreservesApplySelection()
{
    var path = Path.Combine(_tempDir, "slot_05.json");
    var sel = new ApplySelection
    {
        Stat = false, Honor = true, TalentTag = false, Skin = false,
        SelfHouse = true, Identity = true, ActiveKungfu = false,
        ItemList = false, SelfStorage = true,
    };
    var meta = new SlotPayloadMeta(
        SchemaVersion: SlotFile.CurrentSchemaVersion,
        ModVersion: "0.4.0",
        SlotIndex: 5,
        UserLabel: "test",
        UserComment: "",
        CaptureSource: "live",
        CaptureSourceDetail: "",
        CapturedAt: DateTime.UtcNow,
        GameSaveVersion: "1.0.0 f8.2",
        GameSaveDetail: "",
        Summary: new SlotMetadata("h", "n", false, 20, 1, 0f, 0, 0, 0, 0, 0L, 0),
        ApplySelection: sel);
    SlotFile.Write(path, new SlotPayload { Meta = meta, Player = "{}" });

    var loaded = SlotFile.Read(path);
    Assert.False(loaded.Meta.ApplySelection.Stat);
    Assert.True (loaded.Meta.ApplySelection.Honor);
    Assert.True (loaded.Meta.ApplySelection.SelfHouse);
    Assert.True (loaded.Meta.ApplySelection.Identity);
    Assert.False(loaded.Meta.ApplySelection.ActiveKungfu);
    Assert.True (loaded.Meta.ApplySelection.SelfStorage);
}

[Fact]
public void Read_LegacySlotWithoutApplySelection_FallsBackToV03Default()
{
    var path = Path.Combine(_tempDir, "slot_legacy.json");
    // v0.2/v0.3 형식 — applySelection field 없음
    File.WriteAllText(path,
        "{\n  \"_meta\": { \"schemaVersion\": 1, \"slotIndex\": 1, \"userLabel\":\"x\", \"userComment\":\"\"," +
        " \"captureSource\":\"live\", \"captureSourceDetail\":\"\", \"capturedAt\":\"2026-01-01T00:00:00Z\"," +
        " \"gameSaveVersion\":\"\", \"gameSaveDetail\":\"\", \"modVersion\":\"\", " +
        " \"summary\":{ \"heroName\":\"h\", \"heroNickName\":\"n\", \"isFemale\":false, \"age\":20," +
        " \"generation\":1, \"fightScore\":0, \"kungfuCount\":0, \"kungfuMaxLvCount\":0," +
        " \"itemCount\":0, \"storageCount\":0, \"money\":0, \"talentCount\":0 } },\n" +
        " \"player\": {} }");

    var loaded = SlotFile.Read(path);
    // V03Default: 4 카테고리 on
    Assert.True(loaded.Meta.ApplySelection.Stat);
    Assert.True(loaded.Meta.ApplySelection.Honor);
    Assert.True(loaded.Meta.ApplySelection.TalentTag);
    Assert.True(loaded.Meta.ApplySelection.Skin);
    Assert.False(loaded.Meta.ApplySelection.Identity);
    Assert.False(loaded.Meta.ApplySelection.ItemList);
}
```

- [ ] **Step 2: 테스트 실행 → fail**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SlotFileTests"
```
Expected: 2 fails (`ApplySelection` field missing on SlotPayloadMeta).

- [ ] **Step 3: SlotPayload 의 record 에 ApplySelection 추가**

```csharp
// src/LongYinRoster/Slots/SlotPayload.cs
using System;
using LongYinRoster.Core;

namespace LongYinRoster.Slots;

public sealed class SlotPayload
{
    public SlotPayloadMeta Meta   { get; init; } = default!;
    public string          Player { get; init; } = "";
}

public sealed record SlotPayloadMeta(
    int      SchemaVersion,
    string   ModVersion,
    int      SlotIndex,
    string   UserLabel,
    string   UserComment,
    string   CaptureSource,
    string   CaptureSourceDetail,
    DateTime CapturedAt,
    string   GameSaveVersion,
    string   GameSaveDetail,
    SlotMetadata Summary,
    ApplySelection ApplySelection)
{
    // 기존 호출자가 ApplySelection 인자 없이 생성하지 못해 빌드 깨질 것 — Task B11 의 ModWindow update 때 한꺼번에 정정.
    // 임시 호환 ctor 는 추가 안 함 (call-site 모두 update 가 더 안전).
}
```

- [ ] **Step 4: SlotFile 의 SerializeMeta / ParseMeta 에 applySelection 추가**

```csharp
// src/LongYinRoster/Slots/SlotFile.cs SerializeMeta 안 (Summary 닫은 후, EndObject 전):
using LongYinRoster.Core;
// ...
// w.WriteEndObject();   // summary 끝
w.WriteStartObject("applySelection");
w.WriteBoolean("stat",         m.ApplySelection.Stat);
w.WriteBoolean("honor",        m.ApplySelection.Honor);
w.WriteBoolean("talentTag",    m.ApplySelection.TalentTag);
w.WriteBoolean("skin",         m.ApplySelection.Skin);
w.WriteBoolean("selfHouse",    m.ApplySelection.SelfHouse);
w.WriteBoolean("identity",     m.ApplySelection.Identity);
w.WriteBoolean("activeKungfu", m.ApplySelection.ActiveKungfu);
w.WriteBoolean("itemList",     m.ApplySelection.ItemList);
w.WriteBoolean("selfStorage",  m.ApplySelection.SelfStorage);
w.WriteEndObject();
// w.WriteEndObject();  // meta 끝
```

ParseMeta 의 끝부분:

```csharp
// src/LongYinRoster/Slots/SlotFile.cs ParseMeta 안:
ApplySelection sel;
if (m.TryGetProperty("applySelection", out var selEl))
    sel = ApplySelection.FromJsonElement(selEl);
else
    sel = ApplySelection.V03Default();

return new SlotPayloadMeta(
    SchemaVersion:       GetInt(m, "schemaVersion", 1),
    // ...
    Summary:             summary,
    ApplySelection:      sel);
```

- [ ] **Step 5: 테스트 재실행 → 2 PASS + 기존 SlotFile 테스트 PASS**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SlotFileTests"
```
Expected: 5 PASS (기존 3 + 신규 2). 만약 기존 테스트가 SlotPayloadMeta 생성 시 ApplySelection 안 넘기면 build 실패 — fixture 정정.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/Slots/SlotPayload.cs src/LongYinRoster/Slots/SlotFile.cs src/LongYinRoster.Tests/SlotFileTests.cs
git commit -m "feat(slots): _meta.applySelection schema + read/write + legacy fallback test"
```

---

### Task B9: SlotRepository.UpdateApplySelection

**Files:**
- Modify: `src/LongYinRoster/Slots/SlotRepository.cs`

- [ ] **Step 1: SlotRepositoryTests 에 failing test 추가**

`src/LongYinRoster.Tests/SlotRepositoryTests.cs` 에:

```csharp
[Fact]
public void UpdateApplySelection_PersistsToFile()
{
    var sel = new ApplySelection { Identity = true, ItemList = true };
    _repo.Write(1, MakePayload(slot: 1, label: "x"));   // 기존 helper 가정
    _repo.UpdateApplySelection(1, sel);
    _repo.Reload();
    var loaded = _repo.All[1].Meta!.ApplySelection;
    Assert.True (loaded.Identity);
    Assert.True (loaded.ItemList);
    Assert.False(loaded.SelfStorage);
}
```

- [ ] **Step 2: 실행 → fail (`UpdateApplySelection` undefined)**

- [ ] **Step 3: SlotRepository 에 메서드 추가**

```csharp
// src/LongYinRoster/Slots/SlotRepository.cs
public void UpdateApplySelection(int index, LongYinRoster.Core.ApplySelection sel) =>
    UpdateMeta(index, m => m with { ApplySelection = sel });
```

- [ ] **Step 4: 실행 → PASS**

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Slots/SlotRepository.cs src/LongYinRoster.Tests/SlotRepositoryTests.cs
git commit -m "feat(slots): SlotRepository.UpdateApplySelection — toggle 즉시 저장 path"
```

---

### Task B10: PinpointPatcher.Probe + Apply 시그니처 변경

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`

이 task 는 큰 변경 — Apply 시그니처 자체가 바뀌어 모든 호출자 (ModWindow.DoApply / AttemptAutoRestore) 영향. 임시로 호환 wrapper 두지 말고 한꺼번에 update — Task B11 (ModWindow) 와 같이 작업.

- [ ] **Step 1: Apply 시그니처 변경 + selection-aware SetSimpleFields**

```csharp
// src/LongYinRoster/Core/PinpointPatcher.cs
// 시그니처:
public static ApplyResult Apply(string slotPlayerJson, object currentPlayer, ApplySelection selection)
{
    if (slotPlayerJson == null) throw new ArgumentNullException(nameof(slotPlayerJson));
    if (currentPlayer == null) throw new ArgumentNullException(nameof(currentPlayer));
    if (selection == null) throw new ArgumentNullException(nameof(selection));

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
```

`SetSimpleFields` 의 selection filter 추가:

```csharp
private static void SetSimpleFields(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    foreach (var entry in SimpleFieldMatrix.Entries)
    {
        bool enabled = entry.Category switch
        {
            FieldCategory.Stat        => selection.Stat,
            FieldCategory.Honor       => selection.Honor,
            FieldCategory.Skin        => selection.Skin,
            FieldCategory.SelfHouse   => selection.SelfHouse,
            FieldCategory.TalentPoint => selection.TalentTag,
            FieldCategory.None        => false,
            _ => false,
        };
        if (!enabled)
        {
            res.SkippedFields.Add($"{entry.Name} (selection off)");
            continue;
        }

        // (기존 로직 그대로 — ApplySkinSpecialCase / ApplyListIndexedSpecialCase / Regular path)
        // ...
    }
}
```

- [ ] **Step 2: SetIdentityFields 신규 step**

(PoC 결과에 따라 path 결정 후 본문 채움. 아래는 시도 A 가 PASS 한 경우의 default 구현 — 시도 B 였으면 backing field SetValue 사용)

```csharp
private static void SetIdentityFields(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.Identity) { res.SkippedFields.Add("identity (selection off)"); return; }
    // capability gate
    if (!Probe().Identity)  { res.SkippedFields.Add("identity (PoC failed)"); return; }

    foreach (var ifEntry in IdentityFieldMatrix.Entries)
    {
        if (!TryReadJsonValue(slot, ifEntry.JsonPath, ifEntry.Type, out var newVal))
        {
            res.SkippedFields.Add($"identity:{ifEntry.Name} — not in slot JSON");
            continue;
        }
        try
        {
            // PoC 결과 path 에 따라 분기 — 아래는 시도 A (setter 직접) 의 경우
            if (ifEntry.Path == IdentityPath.Setter)
            {
                var p = player.GetType().GetProperty(ifEntry.PropertyName, F);
                p?.SetValue(player, newVal);
            }
            else if (ifEntry.Path == IdentityPath.BackingField)
            {
                var f = player.GetType().GetField(ifEntry.BackingFieldName!,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                f?.SetValue(player, newVal);
            }
            res.AppliedFields.Add($"identity:{ifEntry.Name}");
        }
        catch (Exception ex)
        {
            res.WarnedFields.Add($"identity:{ifEntry.Name} — {ex.GetType().Name}: {ex.Message}");
        }
    }
}
```

(IdentityFieldMatrix 는 Task B12 에서 구현 — placeholder 로 일단 비워둘 수 있지만 best 는 같이 작업)

- [ ] **Step 3: SetActiveKungfu 신규 step**

```csharp
private static void SetActiveKungfu(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.ActiveKungfu) { res.SkippedFields.Add("activeKungfu (selection off)"); return; }
    if (!Probe().ActiveKungfu)   { res.SkippedFields.Add("activeKungfu (PoC failed)"); return; }

    if (!slot.TryGetProperty("nowActiveSkill", out var idEl) ||
        idEl.ValueKind != JsonValueKind.Number)
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
```

- [ ] **Step 4: RebuildItemList / RebuildSelfStorage 활성화 + Probe**

```csharp
private static void RebuildItemList(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.ItemList) { res.SkippedFields.Add("itemList (selection off)"); return; }
    if (!Probe().ItemList)   { res.SkippedFields.Add("itemList (PoC failed — v0.5+ 후보)"); return; }

    // 1) Clear via game-self LoseAllItem
    try { InvokeMethod(player, "LoseAllItem", System.Array.Empty<object>()); }
    catch (Exception ex) { res.WarnedFields.Add($"itemList clear — {ex.GetType().Name}: {ex.Message}"); }

    // 2) Add each from slot
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
            // PoC 결과 path — IntPtr ctor 또는 static factory 또는 Harmony hijack
            var itemData = ItemDataFactory.Create(id, count);   // PoC Task A4 결과로 구현
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

private static void RebuildSelfStorage(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.SelfStorage) { res.SkippedFields.Add("selfStorage (selection off)"); return; }
    if (!Probe().SelfStorage)   { res.SkippedFields.Add("selfStorage (PoC failed — v0.5+ 후보)"); return; }

    // 1) Clear via raw IL2CppListOps (selfStorage 에는 LoseAllItem 동등 method 없음 — Q5)
    var storage = ReadFieldOrProperty(player, "selfStorage");
    var allItem = storage != null ? ReadFieldOrProperty(storage, "allItem") : null;
    if (allItem != null)
    {
        try { IL2CppListOps.Clear(allItem); }
        catch (Exception ex) { res.WarnedFields.Add($"selfStorage clear — {ex.GetType().Name}: {ex.Message}"); }
    }

    // 2) Add each from slot
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
            // selfStorage.AddItem(itemData)? 또는 raw IL2CppListOps.Add
            // PoC A4 의 결과로 Game-self method 발견 시 그것 호출 — 없으면 raw add
            IL2CppListOps.Add(allItem!, itemData);
            added++;
        }
        catch (Exception ex)
        {
            res.WarnedFields.Add($"selfStorage[{i}] id={id} — {ex.GetType().Name}: {ex.Message}");
        }
    }
    res.AppliedFields.Add($"selfStorage ({added}/{arr.GetArrayLength()})");
}
```

(`ItemDataFactory` 는 Task A4 PoC 결과 반영하여 별도 file 또는 PinpointPatcher private helper 로 구현 — 본 task 에는 placeholder 로 두고 Task B12 와 같이 작업)

- [ ] **Step 5: SkipKungfuSkills (v0.5+ 후보 — 기존 RebuildKungfuSkills 의 ⚪ 메시지 그대로)**

```csharp
private static void SkipKungfuSkills(ApplyResult res)
{
    res.SkippedFields.Add("kungfuSkills — collection rebuild deferred to v0.5+");
}
```

- [ ] **Step 6: RebuildHeroTagData 에 selection.TalentTag 검사 추가**

기존 RebuildHeroTagData 의 entry 점에 prepend:

```csharp
private static void RebuildHeroTagData(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.TalentTag) { res.SkippedFields.Add("heroTagData (selection off)"); return; }
    // (기존 본문 그대로)
}
```

- [ ] **Step 7: Probe 메서드 추가**

```csharp
private static Capabilities? _capCache;

public static Capabilities Probe()
{
    if (_capCache != null) return _capCache;
    var p = HeroLocator.GetPlayer();
    if (p == null) return _capCache = Capabilities.AllOff();

    // 가벼운 reflection-only checks — game state 안 건드림
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

private static bool ProbeIdentityCapability(object p)
{
    // Task A2 결과 — PASS 였으면 setter 존재 + non-trivial test 시도, FAIL 이면 backing field 검사
    var prop = p.GetType().GetProperty("heroName", F);
    if (prop == null || !prop.CanWrite) return false;
    // setter 가 silent no-op 인지 확인 — set 시도 후 read-back 비교 (값 복구 포함)
    string original = (string)prop.GetValue(p)!;
    string test = original + "_probe";
    try
    {
        prop.SetValue(p, test);
        bool ok = ((string)prop.GetValue(p)!) == test;
        prop.SetValue(p, original);   // 복구
        return ok;
    }
    catch { return false; }
}

private static bool ProbeActiveKungfuCapability(object p)
{
    return p.GetType().GetMethod("SetNowActiveSkill", F) != null
        && p.GetType().GetProperty("kungfuSkills", F) != null;
}

private static bool ProbeItemListCapability(object p)
{
    // ItemDataFactory.IsAvailable 가 PoC A4 결과 반영 — 둘 다 구현되면 true
    return ItemDataFactory.IsAvailable
        && p.GetType().GetMethod("LoseAllItem", F) != null
        && p.GetType().GetMethod("GetItem", F) != null;
}
```

- [ ] **Step 8: 빌드 — 모든 호출자 업데이트 (ModWindow Task B11 에서 같이)**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: 빌드 실패 — `ModWindow.DoApply` 가 selection 안 넘김. 이건 Task B11 에서 같이 fix.

- [ ] **Step 9: Commit (Task B11 와 묶어서 — single coherent commit 더 안전)**

→ Task B11 와 같은 commit 에 묶음. 본 task 끝에서는 commit 안 함, B11 완료 후 같이.

---

### Task B11: ModWindow Probe + Capabilities cache + selection-aware DoApply

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: Capabilities cache + Awake 에 Probe**

```csharp
// src/LongYinRoster/UI/ModWindow.cs
public Core.Capabilities Capabilities { get; private set; } = Core.Capabilities.AllOff();

private void Awake()
{
    _instance = this;
    // ... 기존 ...

    // v0.4 — Plugin 시작 시 1 회 capability probe (게임 안 진입 후 첫 ModWindow 활성화 시)
    // Probe 는 player 필요 — Awake 가 아니라 첫 OnGUI 에서 lazy 호출 더 안전
    Logger.Info($"ModWindow Awake (slots dir: {slotDir})");
}

private bool _capabilitiesProbed = false;

private void OnGUI()
{
    ToastService.Draw();
    if (!_visible) return;

    // lazy Probe — 첫 OnGUI 시점 (player 살아있음 가정)
    if (!_capabilitiesProbed)
    {
        Capabilities = Core.PinpointPatcher.Probe();
        _capabilitiesProbed = true;
    }

    // ... 기존 ...
}
```

- [ ] **Step 2: SlotDetailPanel.Draw 호출 시 Capabilities 전달 (B12 후 wiring)**

(B12 의 SlotDetailPanel 시그니처 변경 후 여기 update)

- [ ] **Step 3: DoApply 에 selection 인자 전달**

```csharp
private void DoApply(int slot, bool doAutoBackup)
{
    var player = Core.HeroLocator.GetPlayer();
    if (player == null) { /* ... */ return; }
    if (!Config.AllowApplyToGame.Value) { /* ... */ return; }

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
                    Summary: nowSummary,
                    ApplySelection: Core.ApplySelection.RestoreAll()),  // 자동백업은 항상 모두 — Restore 시 RestoreAll 보장
                Player = nowJson,
            };
            Repo.WriteAutoBackup(payload);
        }
        catch (Exception ex) { /* ... */ return; }
    }

    // 2. 슬롯 데이터 read + strip
    SlotPayload loaded;
    string stripped;
    Core.ApplySelection selection;
    try
    {
        loaded   = SlotFile.Read(Repo.PathFor(slot));
        stripped = Core.PortabilityFilter.StripForApply(loaded.Player);
        // slot 0 (Restore) 면 RestoreAll, 그 외엔 슬롯 메타 의 selection
        selection = (slot == 0) ? Core.ApplySelection.RestoreAll() : loaded.Meta.ApplySelection;
    }
    catch (Exception ex) { /* ... */ return; }

    // 3. PinpointPatcher 호출 — selection 추가
    Core.ApplyResult res;
    try { res = Core.PinpointPatcher.Apply(stripped, player, selection); }
    catch (Exception ex)
    {
        Logger.Error($"PinpointPatcher.Apply top-level throw: {ex}");
        if (doAutoBackup) AttemptAutoRestore(player);
        ToastService.Push(string.Format(
            doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                         : KoreanStrings.ToastErrApplyNoBackup, ex.Message), ToastKind.Error);
        return;
    }

    // 4. fatal — autorestore (RestoreAll)
    if (res.HasFatalError)
    {
        string firstErr = res.StepErrors.Count > 0 ? res.StepErrors[0].Message : "fatal step";
        if (doAutoBackup) AttemptAutoRestore(player);
        ToastService.Push(string.Format(
            doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                         : KoreanStrings.ToastErrApplyNoBackup, firstErr), ToastKind.Error);
        return;
    }

    // 5. 성공
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
        var res = Core.PinpointPatcher.Apply(stripped, player, Core.ApplySelection.RestoreAll());
        if (res.HasFatalError)
            Logger.Error("Auto-restore also failed — game state may be inconsistent");
        else
            Logger.Info($"Auto-restore OK applied={res.AppliedFields.Count}");
    }
    catch (Exception ex) { Logger.Error($"Auto-restore threw: {ex}"); }
}
```

- [ ] **Step 4: DoCapture 에 ApplySelection.V03Default() 추가**

```csharp
var payload = new SlotPayload
{
    Meta = new SlotPayloadMeta(
        SchemaVersion: SlotFile.CurrentSchemaVersion,
        ModVersion: Plugin.VERSION,
        SlotIndex: slot,
        UserLabel: label,
        UserComment: "",
        CaptureSource: "live",
        CaptureSourceDetail: "",
        CapturedAt: DateTime.Now,
        GameSaveVersion: "1.0.0 f8.2",
        GameSaveDetail: "",
        Summary: summary,
        ApplySelection: Core.ApplySelection.V03Default()),  // 신규
    Player = json,
};
```

`DoImportFromFile` 에도 동일하게 ApplySelection 인자 추가 (V03Default).

- [ ] **Step 5: RequestRestore — selection 무관 명시**

기존:
```csharp
onConfirm: () => DoApply(slot: 0, doAutoBackup: false));
```

(DoApply 가 slot=0 일 때 RestoreAll() 사용하도록 step 3 에서 분기 했으니 추가 변경 없음)

- [ ] **Step 6: 빌드 + 회귀 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 빌드 success + 모든 테스트 PASS.

- [ ] **Step 7: Commit (Task B10 + B11 묶어서)**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs src/LongYinRoster/UI/ModWindow.cs
git commit -m "feat(core): PinpointPatcher selection-aware Apply + 4 신규 step + Probe + ModWindow wiring"
```

---

### Task B12: IdentityFieldMatrix + ItemDataFactory (PoC 결과 반영)

**Files:**
- Create: `src/LongYinRoster/Core/IdentityFieldMatrix.cs`
- Create: `src/LongYinRoster/Core/ItemDataFactory.cs`

이 task 는 PoC 결과 (Task A2 / A4) 가 결정한 path 를 코드로 굳힘.

- [ ] **Step 1: IdentityFieldMatrix 작성**

```csharp
// src/LongYinRoster/Core/IdentityFieldMatrix.cs
using System.Collections.Generic;

namespace LongYinRoster.Core;

public enum IdentityPath { Setter, BackingField, Harmony }

public sealed record IdentityFieldEntry(
    string         Name,
    string         JsonPath,
    string         PropertyName,
    System.Type    Type,
    IdentityPath   Path,
    string?        BackingFieldName);

/// <summary>
/// 9 정체성 필드 매핑. PoC Task A2 결과로 Path 결정:
///   - Setter:      property setter 직접 호출 (Newtonsoft Populate 함정 통과)
///   - BackingField: <heroName>k__BackingField 직접 set (setter no-op 일 때 fallback)
///   - Harmony:     이 path 는 v0.4 에서 미사용 — v0.5+ 후보
/// </summary>
public static class IdentityFieldMatrix
{
    public static readonly IReadOnlyList<IdentityFieldEntry> Entries = new[]
    {
        // PoC 결과 — 시도 A=PASS 가정. 만약 실제 PoC 가 다르면 path / BackingFieldName 정정
        new IdentityFieldEntry("이름",       "heroName",       "heroName",       typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("별명",       "heroNickName",   "heroNickName",   typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("성씨",       "heroFamilyName", "heroFamilyName", typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("설정명",     "settingName",    "settingName",    typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("성별",       "isFemale",       "isFemale",       typeof(bool),   IdentityPath.Setter, null),
        new IdentityFieldEntry("나이",       "age",            "age",            typeof(int),    IdentityPath.Setter, null),
        new IdentityFieldEntry("천성",       "nature",         "nature",         typeof(int),    IdentityPath.Setter, null),
        new IdentityFieldEntry("재능",       "talent",         "talent",         typeof(int),    IdentityPath.Setter, null),
        new IdentityFieldEntry("세대",       "generation",     "generation",     typeof(int),    IdentityPath.Setter, null),
    };
}
```

- [ ] **Step 2: ItemDataFactory 작성 — PoC 결과로 path 결정**

```csharp
// src/LongYinRoster/Core/ItemDataFactory.cs
using System;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// PoC Task A4 결과로 ItemData 생성. 3 path 후보:
///   1. IntPtr ctor (Il2CppInterop wrapper 표준)
///   2. Static factory (e.g., ItemData.Create(int id, int count))
///   3. Harmony hijack (game 의 어느 method 가 ItemData 생성하는 path 를 가로챔)
///
/// IsAvailable 이 false 면 Capabilities.ItemList = false → UI disabled.
/// </summary>
public static class ItemDataFactory
{
    private static Func<int, int, object>? _create;
    private static bool _initialized = false;

    public static bool IsAvailable
    {
        get { EnsureInit(); return _create != null; }
    }

    public static object Create(int itemID, int count)
    {
        EnsureInit();
        if (_create == null)
            throw new InvalidOperationException("ItemDataFactory unavailable — PoC failed");
        return _create(itemID, count);
    }

    private static void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;

        // PoC A4 결과로 어느 path 가 살아있는지 결정 — 아래는 시도 2 (static factory) 가정
        // 실제 PoC 결과로 다른 path 일 수 있음. partial path 도 OK — 하나만 살아있으면 IsAvailable=true
        var itemDataType = FindGameType("ItemData");
        if (itemDataType == null) return;

        // 시도 1 — IntPtr ctor (단, valid IntPtr 가 없으므로 단독 사용 어려움. game 의 기존 ItemData 의 ptr 복사 등 필요)
        // 시도 2 — static factory (PoC 에서 발견된 method 이름 사용)
        var factory = itemDataType.GetMethod("Create",
            BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(int), typeof(int) }, null);
        if (factory != null)
        {
            _create = (id, count) => factory.Invoke(null, new object[] { id, count })!;
            return;
        }

        // 시도 3 — Harmony hijack (별도 helper method 호출)
        // ... PoC 결과 기반
    }

    private static Type? FindGameType(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            try
            {
                foreach (var t in asm.GetTypes())
                    if (t.Name == typeName && (t.Namespace == null || !t.Namespace.StartsWith("LongYinRoster")))
                        return t;
            }
            catch { }
        return null;
    }
}
```

- [ ] **Step 3: 빌드 + 회귀 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 빌드 success + 테스트 모두 PASS (~36 tests).

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Core/IdentityFieldMatrix.cs src/LongYinRoster/Core/ItemDataFactory.cs
git commit -m "feat(core): IdentityFieldMatrix + ItemDataFactory — PoC 결과 코드화"
```

---

## Phase C — UI

---

### Task C13: KoreanStrings 신규 추가

**Files:**
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs`

- [ ] **Step 1: 9 카테고리 label + disabled suffix 추가**

KoreanStrings 의 마지막 (v0.3 신규 끝) 다음에 추가:

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
public const string Cat_DisabledSuffix  = " (v0.5+ 후보)";
public const string ApplySectionHeader  = "─── Apply 항목 ───";
```

- [ ] **Step 2: 빌드 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: build success.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Util/KoreanStrings.cs
git commit -m "feat(strings): v0.4 — 9 카테고리 label + disabled suffix"
```

---

### Task C14: SlotDetailPanel 체크박스 grid + Capabilities + Toggle 콜백

**Files:**
- Modify: `src/LongYinRoster/UI/SlotDetailPanel.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (호출자 update)

- [ ] **Step 1: SlotDetailPanel 시그니처 변경 + grid 그리기**

```csharp
// src/LongYinRoster/UI/SlotDetailPanel.cs
using System;
using LongYinRoster.Core;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class SlotDetailPanel
{
    public Action<int>? OnApplyRequested;
    public Action<int>? OnDeleteRequested;
    public Action<int>? OnRenameRequested;
    public Action<int>? OnCommentRequested;
    public Action<int>? OnRestoreRequested;
    public Action<int, ApplySelection>? OnApplySelectionChanged;

    public void Draw(SlotEntry entry, bool inGame, Capabilities cap)
    {
        GUILayout.BeginVertical();

        if (entry.IsEmpty)
        {
            GUILayout.Label(inGame ? KoreanStrings.EmptyStateNoSlots : KoreanStrings.EmptyStateNoGame);
            GUILayout.EndVertical();
            return;
        }

        var m = entry.Meta!;
        var s = m.Summary;

        GUILayout.Label($"슬롯 {entry.Index:D2} · {s.HeroName} ({s.HeroNickName})");
        GUILayout.Space(4);
        Row("캡처",        m.CapturedAt.ToString("yyyy-MM-dd HH:mm"));
        Row("출처",        m.CaptureSource == "live" ? "라이브" : $"파일 {m.CaptureSourceDetail}");
        Row("세이브 시점", m.GameSaveDetail);
        Row("전투력",      s.FightScore.ToString("N0"));
        Row("무공",        $"{s.KungfuCount} (Lv10 {s.KungfuMaxLvCount})");
        Row("인벤토리",    $"{s.ItemCount} / 창고 {s.StorageCount}");
        Row("금전",        $"{s.Money:N0}냥");
        Row("천부",        $"{s.TalentCount}개");
        if (!string.IsNullOrEmpty(m.UserComment))
            Row("메모", m.UserComment);

        GUILayout.Space(8);

        if (entry.Index == 0)
        {
            // Restore — 체크박스 노출 안 함
            GUI.enabled = inGame;
            if (GUILayout.Button(KoreanStrings.RestoreBtn))
                OnRestoreRequested?.Invoke(entry.Index);
            GUI.enabled = true;
        }
        else
        {
            // 체크박스 grid (3 컬럼 x 3 행)
            DrawApplySelectionGrid(entry.Index, m.ApplySelection, cap);

            GUILayout.Space(6);

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

        GUILayout.EndVertical();
    }

    private void DrawApplySelectionGrid(int slotIndex, ApplySelection sel, Capabilities cap)
    {
        GUILayout.Label(KoreanStrings.ApplySectionHeader);
        // 3 컬럼 x 3 행 (9 카테고리)
        bool changed = false;

        GUILayout.BeginHorizontal();
        changed |= ToggleCell(KoreanStrings.Cat_Stat,         ref sel.Stat,         enabled: true);
        changed |= ToggleCell(KoreanStrings.Cat_Honor,        ref sel.Honor,        enabled: true);
        changed |= ToggleCell(KoreanStrings.Cat_TalentTag,    ref sel.TalentTag,    enabled: true);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        changed |= ToggleCell(KoreanStrings.Cat_Skin,         ref sel.Skin,         enabled: true);
        changed |= ToggleCell(KoreanStrings.Cat_SelfHouse,    ref sel.SelfHouse,    enabled: true);
        changed |= ToggleCell(KoreanStrings.Cat_Identity,     ref sel.Identity,     enabled: cap.Identity);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        changed |= ToggleCell(KoreanStrings.Cat_ActiveKungfu, ref sel.ActiveKungfu, enabled: cap.ActiveKungfu);
        changed |= ToggleCell(KoreanStrings.Cat_ItemList,     ref sel.ItemList,     enabled: cap.ItemList);
        changed |= ToggleCell(KoreanStrings.Cat_SelfStorage,  ref sel.SelfStorage,  enabled: cap.SelfStorage);
        GUILayout.EndHorizontal();

        if (changed)
            OnApplySelectionChanged?.Invoke(slotIndex, sel);
    }

    private static bool ToggleCell(string label, ref bool state, bool enabled)
    {
        // GUIStyle 인자 받는 overload 회피 (IL2CPP strip).
        // IMGUI Toggle: state 변화 감지 위해 before/after 비교
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && enabled;
        bool before = enabled ? state : false;       // disabled 면 강제 false 표시
        string lbl  = enabled ? label : (label + KoreanStrings.Cat_DisabledSuffix);
        bool after  = GUILayout.Toggle(before, lbl, GUILayout.Width(140));
        GUI.enabled = wasEnabled;
        if (!enabled) return false;          // disabled 면 어떤 클릭도 무시
        if (after == state) return false;
        state = after;
        return true;
    }

    private static void Row(string k, string v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(k, GUILayout.Width(80));
        GUILayout.Label(v);
        GUILayout.EndHorizontal();
    }
}
```

**중요 — `ApplySelection` 의 boolean property 가 ref 로 못 넘어감 (auto-property):**
- `ref sel.Stat` 는 backing field 직접 접근 안 됨 — 수정 필요. 대안: `ApplySelection` 의 property 를 field 로 바꾸거나, `Toggle` 의 cell 패턴을 `bool current = sel.Stat; ...; if (changed) sel.Stat = current;` 로 변경.

Step 1 의 ToggleCell 호출을 다음과 같이 수정:

```csharp
changed |= ToggleCell(KoreanStrings.Cat_Stat, sel.Stat, true, v => sel.Stat = v);
// ...
private static bool ToggleCell(string label, bool state, bool enabled, Action<bool> setter)
{
    bool wasEnabled = GUI.enabled;
    GUI.enabled = wasEnabled && enabled;
    string lbl = enabled ? label : (label + KoreanStrings.Cat_DisabledSuffix);
    bool before = enabled ? state : false;
    bool after = GUILayout.Toggle(before, lbl, GUILayout.Width(140));
    GUI.enabled = wasEnabled;
    if (!enabled || after == state) return false;
    setter(after);
    return true;
}
```

- [ ] **Step 2: ModWindow 가 SlotDetailPanel.Draw 호출 시 Capabilities 전달**

```csharp
// src/LongYinRoster/UI/ModWindow.cs DrawWindow 안:
private void DrawWindow(int id)
{
    DialogStyle.FillBackground(_rect.width, _rect.height);
    GUILayout.BeginHorizontal();
    _list.Draw(Repo, 240f);
    GUILayout.Space(8);
    _detail.Draw(Repo.All[_list.Selected], inGame: Core.HeroLocator.IsInGame(),
                 cap: Capabilities);   // ← v0.4 신규
    GUILayout.EndHorizontal();
    GUI.DragWindow(new Rect(0, 0, 10000, 24));
}
```

- [ ] **Step 3: ModWindow 가 OnApplySelectionChanged 콜백 wire — toggle 즉시 저장**

```csharp
// src/LongYinRoster/UI/ModWindow.cs Awake 안 (panel callbacks 묶음):
_detail.OnApplySelectionChanged = (slotIndex, sel) =>
{
    try
    {
        Repo.UpdateApplySelection(slotIndex, sel);
        // Reload 안 함 — selection 만 변경된 거라 file mtime 외 다른 변화 없음
    }
    catch (Exception ex)
    {
        Logger.Error($"UpdateApplySelection(slot={slotIndex}) failed: {ex}");
    }
};
```

- [ ] **Step 4: ModWindow 의 _rect height 약간 증가 (60~80px)**

```csharp
// 기본 height — Config.WindowH.Value 의 default 변경
// v0.3: 480, v0.4: 560
// Config.cs 의 WindowH default:
// (기존)
// public static ConfigEntry<float> WindowH = ...; // default 480
// →
// default 560
```

`Config.cs` 확인 후 height default 값만 update.

- [ ] **Step 5: 빌드 + 회귀 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: build success + 모든 테스트 PASS.

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/UI/SlotDetailPanel.cs src/LongYinRoster/UI/ModWindow.cs src/LongYinRoster/Config.cs
git commit -m "feat(ui): v0.4 SlotDetailPanel 9-카테고리 체크박스 grid + 즉시 저장 wiring"
```

---

## Phase D — Smoke + Release

---

### Task D15: Smoke checklist 작성 + 게임 안 검증

**Files:**
- Create: `docs/superpowers/specs/2026-04-30-v0.4-smoke.md` (smoke 결과 기록)

- [ ] **Step 1: smoke checklist 문서 작성**

`docs/superpowers/specs/2026-04-30-v0.4-smoke.md`:

```markdown
# v0.4 Smoke Checklist

각 항목 게임 안 검증 결과 기록.

## A. 체크박스 default 검증
- [ ] 신규 슬롯 캡처 (`[+]`) → SlotDetailPanel 의 체크박스 상태 = V03Default
  - on: 스탯 / 명예 / 천부 / 스킨 (4 개)
  - off: 자기집 add / 정체성 / 무공 active / 인벤토리 / 창고 (5 개)

## B. Toggle 시 즉시 저장
- [ ] 임의 슬롯의 정체성 체크 → 모드 창 닫고 다시 열기 → 정체성 그대로 체크됨
- [ ] 슬롯 file 의 _meta.applySelection.identity = true 직접 확인:
  ```python
  python -c "import json; d=json.load(open('Slots/slot_03.json', encoding='utf-8-sig')); print(d['_meta']['applySelection'])"
  ```

## C. v0.3 호환 Apply
- [ ] V03Default selection 그대로 → Apply → 스탯/명예/천부/스킨 변경. 부상/충성/호감 변경 안 됨 (Apply 안 함, 영구 보존)
- [ ] save → reload 후 정보창 정상 (G1/G2/G3 회귀 게이트)

## D. 부분 Apply — 스탯만
- [ ] 슬롯의 스탯 만 체크 → 다른 8 카테고리 off → Apply
- [ ] 결과: HP/Mana/Power/baseAttri 변경. 정체성/인벤토리 그대로 (game 상태 유지)

## E. 정체성 PoC
- [ ] 슬롯의 정체성 on → Apply → heroName 등 변화
- [ ] save → reload → 정보창 표시 정상 (Capabilities.Identity = true 시)
- [ ] Capabilities.Identity = false 면 disabled checkbox + "(v0.5+ 후보)"

## F. 인벤토리 / 창고 PoC
- [ ] 슬롯의 인벤토리 on → Apply
- [ ] 결과: 새 인벤토리 entry. item 사용 가능 (장비/소비)
- [ ] save → reload 후에도 살아있음
- [ ] Capabilities.ItemList = false 면 disabled checkbox

## G. 무공 active PoC
- [ ] 슬롯의 무공 active on + Apply → 활성 무공 표시 변화
- [ ] player 미보유 skill ID 면 warn 토스트 (kungfuSkills v0.5+ 후보 안내)

## H. Restore 항상 모두 적용
- [ ] slot 0 의 detail panel — 체크박스 노출 안 됨, [↶ 복원] 버튼만
- [ ] 임의 슬롯 Apply → 자동백업 후 → [↶ 복원] → 9 카테고리 모두 복원 (체크박스 무관)

## I. PoC 실패 disabled UI
- [ ] PoC 실패 카테고리는 회색 체크박스 + "(v0.5+ 후보)" suffix
- [ ] 클릭해도 토글 안 됨 (GUI.enabled = false)

## J. v0.2 / v0.3 슬롯 호환
- [ ] v0.3 슬롯 (applySelection 없음) 로드 → V03Default 자동 적용
- [ ] file 안 건드림 — slot_XX.json 의 mtime 변경 없음
- [ ] 사용자가 toggle 하는 시점에만 file 갱신 (첫 sync)
```

- [ ] **Step 2: 게임 실행 + 각 항목 검증**

게임 안에서 각 항목 직접 시도. 실패 항목 발견 시 해당 task (B7~C14) 로 돌아가 fix.

- [ ] **Step 3: Commit (smoke 결과 기록)**

```bash
git add docs/superpowers/specs/2026-04-30-v0.4-smoke.md
git commit -m "docs: v0.4 smoke checklist 통과 기록"
```

---

### Task D16: 임시 PoC 코드 제거

**Files:**
- Delete: `src/LongYinRoster/Core/HeroDataDumpV04.cs`
- Modify: `src/LongYinRoster/Plugin.cs` ([F12] 핸들러 제거)

- [ ] **Step 1: HeroDataDumpV04.cs 삭제**

```bash
rm "src/LongYinRoster/Core/HeroDataDumpV04.cs"
```

- [ ] **Step 2: Plugin.cs 의 [F12] 핸들러 제거**

`Plugin.cs` 의 Update 안 추가했던 부분:
```csharp
// v0.4 임시 PoC 진단 — Task 14 에서 제거
if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12)) { ... }
```
삭제.

- [ ] **Step 3: 빌드 + 테스트 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: build success + 모든 테스트 PASS.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Core/HeroDataDumpV04.cs src/LongYinRoster/Plugin.cs
git commit -m "chore(release): remove HeroDataDumpV04 + [F12] handler"
```

---

### Task D17: README / HANDOFF / spec §12 update

**Files:**
- Modify: `README.md`
- Modify: `docs/HANDOFF.md`
- Modify: `docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md` (§12 cross-reference)

- [ ] **Step 1: README.md update**

v0.4 기능 추가:
- 정체성 / 무공 active / 인벤토리 / 창고 활성화 (PoC 결과 명시)
- 9-카테고리 체크박스 UI
- 부상/충성/호감 backup 폐기 (영구 보존)
- v0.2/v0.3 슬롯 호환 (V03Default 자동 적용)

- [ ] **Step 2: HANDOFF.md update**

§5 검증 완료 list 에 v0.4 항목 추가, §6 다음 세션은 v0.5+ (무공 list / 외형) 또는 maintenance.

§2 깃 히스토리 갱신 (v0.4 의 commit 추가).

- [ ] **Step 3: v0.3 spec 의 §12 (Out of Scope) 갱신 — v0.4 에서 활성화된 항목 표시**

`2026-04-29-longyin-roster-mod-v0.3-design.md` 의 §12 list 에서:
- "§7.2 매트릭스의 ⚪ 항목 활성화" — 정체성 / 인벤토리 / 창고 / 무공 active 는 **v0.4 활성화** 로 표시
- "필드 단위 selective Apply (cherry-pick) — SlotDetailPanel 의 체크박스 매트릭스. v0.5+." — **v0.4 활성화 (카테고리 단위)** 로 표시

- [ ] **Step 4: Commit**

```bash
git add README.md docs/HANDOFF.md docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md
git commit -m "docs: v0.4 README / HANDOFF / v0.3 spec §12 cross-reference"
```

---

### Task D18: VERSION bump + dist zip + git tag + GitHub release

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs` (`VERSION` 상수)
- Create: `dist/LongYinRoster_v0.4.0.zip`

- [ ] **Step 1: Plugin.cs 의 VERSION 갱신**

```csharp
public const string VERSION = "0.4.0";
```

(또는 `Directory.Build.props` / `csproj` 에 있는 위치 확인 후 update)

- [ ] **Step 2: Release build**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

- [ ] **Step 3: dist 폴더 구성**

```bash
mkdir -p dist/LongYinRoster_v0.4.0/BepInEx/plugins/LongYinRoster
cp src/LongYinRoster/bin/Release/net6.0/LongYinRoster.dll dist/LongYinRoster_v0.4.0/BepInEx/plugins/LongYinRoster/
cp README.md dist/LongYinRoster_v0.4.0/
```

- [ ] **Step 4: zip**

PowerShell:
```powershell
Compress-Archive -Path "dist/LongYinRoster_v0.4.0/*" -DestinationPath "dist/LongYinRoster_v0.4.0.zip" -Force
```

또는 bash + zip:
```bash
cd dist && zip -r LongYinRoster_v0.4.0.zip LongYinRoster_v0.4.0/ && cd ..
```

- [ ] **Step 5: VERSION commit**

```bash
git add src/LongYinRoster/Plugin.cs
git commit -m "chore(release): v0.4.0 — VERSION bump"
```

- [ ] **Step 6: git tag + push**

```bash
git tag v0.4.0
git push origin main
git push origin v0.4.0
```

- [ ] **Step 7: GitHub release**

```bash
gh release create v0.4.0 dist/LongYinRoster_v0.4.0.zip \
    --title "v0.4.0 — 정체성 / 인벤토리 / 창고 + 9-카테고리 체크박스" \
    --notes-file - <<'EOF'
## v0.4.0 — 2026-04-30

### 신규
- **정체성 활성화** — heroName / nickname / age 등 9 필드 (PoC 결과 ${IDENTITY_PATH})
- **인벤토리 / 창고 활성화** — itemListData / selfStorage entry-level rebuild
- **무공 active 활성화** — nowActiveSkill 적용 (player 보유 skill 한정)
- **9-카테고리 체크박스 UI** — SlotDetailPanel 인라인 grid. 슬롯별 selection 저장 (`_meta.applySelection`)

### 변경
- **부상 / 충성 / 호감 backup 폐기** — v0.3 자동 적용 → v0.4 영구 보존 (force/relations 와 같은 정책)
- ModWindow height 480 → 560 (체크박스 grid 공간)

### 호환성
- v0.2 / v0.3 슬롯: `_meta.applySelection` 없음 → 로드 시 V03Default (스탯/명예/천부/스킨 on, 신규 5 off) 자동 적용. file 안 건드림

### v0.5+ 후보
- 무공 list (kungfuSkills) — KungfuSkillLvData wrapper ctor R&D 필요
- 외형 (faceData / portraitID) — sprite invalidation
- Apply preview / selection 프리셋 / 부상-충성-호감 옵션화

### 테스트
- ~36 unit tests all pass
- Smoke checklist (A~J) all pass
EOF
```

(사용자 게이트 — `gh release create` 는 사용자가 명시 승인 후 실행. 본 task 는 명령어 준비만)

- [ ] **Step 8: 게임-load verify**

새로 zip 푼 dll 로 게임 실행 → F11 → 모든 카테고리 체크박스 정상 → Apply / Restore 동작 확인.

- [ ] **Step 9: HANDOFF 의 §2 깃 히스토리에 v0.4.0 tag 추가 + commit**

```bash
git add docs/HANDOFF.md
git commit -m "docs(handoff): v0.4.0 release tag"
git push origin main
```

---

## Self-Review Checklist (작성자 — plan 완성 후 1 회)

이 checklist 는 plan 작성 직후 작성자가 한 번 돌리는 검증. issue 발견 시 plan 본문에 inline 정정.

### 1. Spec coverage
- [ ] §1 Context — Phase A/B 가 PinpointPatcher selection-aware 변환 cover
- [ ] §2 Goals 8 항목 모두 task 매핑:
  - Goal 1 (정체성) — Task A2 + B12 (IdentityFieldMatrix)
  - Goal 2 (인벤토리/창고) — Task A4 + B12 (ItemDataFactory) + B10 (RebuildItemList/SelfStorage)
  - Goal 3 (무공 active) — Task A3 + B10 (SetActiveKungfu)
  - Goal 4 (체크박스 UI) — Task C13 + C14
  - Goal 5 (ApplySelection 슬롯별 저장) — Task B5 + B8 + B9 + C14
  - Goal 6 (Capability 자동 감지) — Task B6 + B10 (Probe) + B11 (cache)
  - Goal 7 (v0.2/v0.3 슬롯 호환) — Task B5 + B8 (legacy fallback test)
  - Goal 8 (Restore 항상 모두) — Task B11 (slot==0 RestoreAll 분기)
- [ ] §4 ApplySelection 모델 — Task B5
- [ ] §5 PinpointPatcher 9-step — Task B7 + B10
- [ ] §6 R&D PoC — Task A2/A3/A4 + B12
- [ ] §7 UI — Task C13/C14
- [ ] §8 Migration — Task B5 + B8 (FromJsonElement / V03Default fallback)
- [ ] §9 Testing — Task B5 (ApplySelectionTests) + B7 (SimpleFieldMatrixTests) + B8 (SlotFileTests) + D15 (smoke)
- [ ] §10 Out of Scope — Task D17 (spec §12 cross-reference)

### 2. Placeholder scan
- "TBD" — Task A1 의 ProbeIdentity 본문 placeholder ("TBD A2") — 의도적 (skeleton). Task A2 에서 채움 ✓
- "PoC 결과에 따라" — Task B12 의 IdentityFieldMatrix / ItemDataFactory 본문에서 PoC 결과 분기 — 명시적 deferred, 실제 PoC 결과로 정정 필요. fix: Task B12 의 step 1 의 코드는 "시도 A 가 PASS 가정" path 만 명시하고, PoC 결과 다른 경우 정정 안내. **이는 placeholder 라기보다는 R&D contingency** — 명시적이라 OK.
- "기존 호출자가 ApplySelection 인자 없이..." 같은 임시 호환 ctor 안 둠 — 명시적 결정 ✓

### 3. Type consistency
- `ApplySelection` 9 boolean — 모든 task 에서 같은 이름 (Stat/Honor/TalentTag/Skin/SelfHouse/Identity/ActiveKungfu/ItemList/SelfStorage) ✓
- `FieldCategory` enum — Task B7 정의, Task B10 SetSimpleFields filter 에서 일관 사용 ✓
- `Capabilities` 4 boolean (Identity/ActiveKungfu/ItemList/SelfStorage) — Task B6/B10/B11/C14 일관 ✓
- `PinpointPatcher.Apply` 시그니처 (json, player, selection) — Task B10/B11 일관 ✓
- `SlotDetailPanel.Draw(entry, inGame, cap)` — Task C14/B11 일관 ✓
- `OnApplySelectionChanged` 콜백 시그니처 (`Action<int, ApplySelection>`) — Task C14 정의, Task B11 wire — 일관 ✓
- `IdentityFieldMatrix.Entries` — Task B10/B12 일관. `IdentityPath` enum (Setter/BackingField/Harmony) — B12 정의, B10 SetIdentityFields 분기에서 일관 ✓
- `ItemDataFactory.Create(int id, int count)` — Task B10 RebuildItemList/SelfStorage 호출, Task B12 정의 — 일관 ✓
- `SlotPayloadMeta` record — Task B8 에 ApplySelection field 추가, Task B11 의 DoCapture/DoApply 자동백업 두 호출자 모두 update — 일관 ✓

### 4. Ambiguity check
- "ModWindow height 480 → 560" — Config.cs 의 default 값 위치 확인 후 update (Step C14-4 명시) ✓
- "ProbeIdentityCapability" 가 setter 에 test 값 set + 복구하는 destructive 동작 — 첫 OnGUI 시점이라 player 살아있음. but 사용자 visible 변화 가능 (한 frame 동안 heroName 이 "X_probe" 로 표시될 수 있음). 한국어 설명 추가 필요? — minor, 본 plan 에는 명시하지 않음. release notes 또는 README 에 caveat 추가 가능
- Restore 시 selection — Task B11 의 step 3 에서 `selection = (slot == 0) ? RestoreAll : meta.ApplySelection` 분기 명시 ✓
- 자동백업의 ApplySelection — Task B11 step 1 에서 `RestoreAll()` 명시 (자동백업이 항상 9 카테고리 모두 저장 — 이후 복원 시 항상 모두 복원) ✓

---

## Plan complete

저장 경로: `docs/superpowers/plans/2026-04-30-longyin-roster-mod-v0.4-plan.md`

---

## Execution Handoff

**두 가지 옵션**:

**1. Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration. PoC tasks (A1~A4) 는 게임 안 검증이라 사용자 직접 게이트 — subagent 가 빌드만 하고 사용자가 게임 실행 + 결과 보고.

**2. Inline Execution** — current session 에서 batch execution + checkpoints for review.

어느 approach 로 진행하시겠습니까?
