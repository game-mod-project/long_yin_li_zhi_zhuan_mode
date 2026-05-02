# LongYin Roster Mod v0.5.3 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ApplySelection 의 `ItemList` 카테고리 활성화 — slot 의 `itemListData.allItem` 을 player 에 완전 교체 (clear + add all). v0.4 PoC A4 의 `ItemDataFactory.IsAvailable=false` stub 해제.

**Architecture:** v0.5.2 의 algorithm 통찰 (game 자체 패턴 mirror — wrapper ctor + game-self method + 2-pass retry) 을 인벤토리 manipulation 에 적용. game-self method (`LoseAllItem` clear + `GetItem(wrapper)` add) 는 이미 존재 — `ItemData` ctor 발견 후 v0.5.2 KungfuListApplier 와 거의 동일한 알고리즘 적용. 새 파일 `Core/ItemListApplier.cs` 에 책임 분리. ItemDataFactory 폐기 (ctor 직접 호출).

**Tech Stack:** BepInEx 6.0.0-dev (IL2CPP, .NET 6) / HarmonyLib / Il2CppInterop / System.Text.Json / xUnit + Shouldly.

**선행 spec:** [`2026-05-02-longyin-roster-mod-v0.5.3-design.md`](../specs/2026-05-02-longyin-roster-mod-v0.5.3-design.md)

**작업 흐름**: Phase 1 (foundation + branch) → Phase 2 (Spike — ItemData type dump + ctor 발견) → Phase 3 (Impl — ItemListApplier + PinpointPatcher 본문 교체 + ItemDataFactory 폐기) → Phase 4 (Smoke 시나리오 1-3 + 회귀) → Phase 5 (Release).

---

## File Structure

### 신규 파일

| 경로 | 책임 | 조건부? | Lifetime |
|---|---|---|---|
| `src/LongYinRoster/Core/Probes/ProbeItemList.cs` | Spike Phase 1 — ItemData type dump + ctor 시도 | 항상 (PoC) | release 전 cleanup |
| `src/LongYinRoster/Core/Probes/ProbeRunner.cs` | F12 trigger | 항상 (PoC) | release 전 cleanup |
| `src/LongYinRoster/Core/ItemListApplier.cs` | clear + add all algorithm + 2-pass retry | ✓ Spike PASS | 영구 |
| `src/LongYinRoster.Tests/ItemListApplierTests.cs` | slot JSON parse + selection gate (5 tests) | ✓ Spike PASS | 영구 |
| `docs/superpowers/dumps/2026-05-XX-item-list-spike.md` | Spike 결과 | 항상 | 영구 |
| `docs/superpowers/dumps/2026-05-XX-v0.5.3-smoke.md` | Smoke 결과 | ✓ release | 영구 |
| `dist/LongYinRoster_v0.5.3.zip` | release artifact | ✓ release | 영구 |

### 수정 파일

| 경로 | 변경 | 조건부? |
|---|---|---|
| `src/LongYinRoster/UI/ModWindow.cs` | F12 + 1-3 hotkey 추가 | 항상 (PoC, release 전 cleanup) |
| `src/LongYinRoster/Core/PinpointPatcher.cs` | `RebuildItemList` 본문 (현재 v0.4 ItemDataFactory 의존 stub) → ItemListApplier 호출, `ProbeItemListCapability` 정확화 | ✓ Spike PASS |
| `src/LongYinRoster/Core/ItemDataFactory.cs` | 폐기 (delete) | ✓ Spike PASS |
| `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` | ItemListApplier link 추가 + ItemDataFactory link 제거 | ✓ Spike PASS |
| `src/LongYinRoster/Plugin.cs` | VERSION `0.5.2` → `0.5.3` | ✓ release |
| `README.md` | v0.5.3 highlights + Releases entry | ✓ release |
| `docs/HANDOFF.md` | §1 main baseline + Releases | 항상 |

### 변경 없는 파일 (확인만)

| 경로 | 이유 |
|---|---|
| `src/LongYinRoster/Core/Capabilities.cs` | `ItemList` flag 이미 존재 (v0.4) |
| `src/LongYinRoster/Core/ApplySelection.cs` | `ItemList` field 이미 존재 (v0.4) |
| `src/LongYinRoster/Slots/SlotFile.cs` | `itemList` 직렬화 이미 존재 (v0.4) |
| `src/LongYinRoster/Util/KoreanStrings.cs` | `Cat_ItemList` 이미 존재 |
| `src/LongYinRoster/UI/SlotDetailPanel.cs` | `itemList` 체크박스 이미 존재 (v0.4) |

---

## Phase 1 — Foundation

### Task 1: Branch + baseline

**Files:**
- Read: `git status`, `git log`

- [ ] **Step 1.1: 작업 위치 + baseline 확인**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git status
git log --oneline -3
```

Expected: working tree clean, HEAD = `ffc801f docs: v0.5.3 spec` 또는 그 후의 main.

- [ ] **Step 1.2: v0.5.3 branch 생성**

```bash
git checkout -b v0.5.3
git branch --show-current
```

Expected: `v0.5.3`.

- [ ] **Step 1.3: 게임 닫기 + baseline build**

```bash
tasklist | grep -i LongYinLiZhiZhuan
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 게임 종료, Build SUCCEEDED.

- [ ] **Step 1.4: baseline tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: **57/57 PASS** (v0.5.2 baseline).

---

## Phase 2 — Spike Phase 1

### Task 2: ProbeItemList.cs + ProbeRunner.cs + F12/1-3 hotkey

**Files:**
- Create: `src/LongYinRoster/Core/Probes/ProbeItemList.cs`
- Create: `src/LongYinRoster/Core/Probes/ProbeRunner.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 2.1: Probes 디렉터리 생성**

```bash
mkdir -p "src/LongYinRoster/Core/Probes"
```

- [ ] **Step 2.2: ProbeItemList.cs 작성**

```csharp
// File: src/LongYinRoster/Core/Probes/ProbeItemList.cs

using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5.3 Spike Phase 1 — 인벤토리 game-self method + ItemData ctor discovery.
///
/// 3 modes:
///   Step1 = HeroData method dump (Lose|Add|Get|Remove*Item* 시그니처)
///   Step2 = ItemData wrapper type ctor + static method dump
///   Step3 = persistence baseline (현재 itemListData.allItem 의 first 10 entries)
/// </summary>
public static class ProbeItemList
{
    public enum Mode { Step1, Step2, Step3 }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run(Mode mode)
    {
        var player = HeroLocator.GetPlayer();
        if (player == null) { Logger.Warn("Spike: player null"); return; }

        switch (mode)
        {
            case Mode.Step1: RunStep1(player); break;
            case Mode.Step2: RunStep2(player); break;
            case Mode.Step3: RunStep3(player); break;
        }
    }

    private static void RunStep1(object player)
    {
        var t = player.GetType();
        var pattern = new System.Text.RegularExpressions.Regex(
            @"^(Lose|Add|Get|Remove|Drop)(All)?Item",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        Logger.Info("=== Spike Step1 — HeroData *Item* method dump ===");
        foreach (var m in t.GetMethods(F))
        {
            if (!pattern.IsMatch(m.Name)) continue;
            var ps = m.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"method: {m.ReturnType.Name} {m.Name}({sig})");
        }
        Logger.Info("=== Spike Step1 end ===");
    }

    private static void RunStep2(object player)
    {
        // ItemData wrapper type 찾기 — itemListData.allItem 의 첫 element
        var ild = ReadField(player, "itemListData");
        if (ild == null) { Logger.Warn("Spike Step2: itemListData null"); return; }
        var allItem = ReadField(ild, "allItem");
        if (allItem == null) { Logger.Warn("Spike Step2: allItem null"); return; }

        int count = IL2CppListOps.Count(allItem);
        Logger.Info($"Spike Step2: itemListData.allItem count={count}");
        if (count == 0) { Logger.Warn("Spike Step2: allItem 비어있음 — wrapper type 알 수 없음"); return; }

        var sample = IL2CppListOps.Get(allItem, 0);
        if (sample == null) { Logger.Warn("Spike Step2: sample null"); return; }
        var wrapperType = sample.GetType();
        Logger.Info($"=== Spike Step2 — ItemData ({wrapperType.FullName}) dump ===");

        // Constructors
        Logger.Info("--- Constructors ---");
        foreach (var ctor in wrapperType.GetConstructors(F | BindingFlags.Static))
        {
            var ps = ctor.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"ctor: ({sig})");
        }

        // Static methods
        Logger.Info("--- Static methods ---");
        foreach (var m in wrapperType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var ps = m.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"static: {m.ReturnType.Name} {m.Name}({sig})");
        }
        Logger.Info("=== Spike Step2 end ===");
    }

    private static void RunStep3(object player)
    {
        var ild = ReadField(player, "itemListData");
        if (ild == null) { Logger.Warn("Spike Step3: itemListData null"); return; }
        var allItem = ReadField(ild, "allItem");
        if (allItem == null) { Logger.Warn("Spike Step3: allItem null"); return; }

        int count = IL2CppListOps.Count(allItem);
        Logger.Info($"Spike Step3: itemListData.allItem count={count}");
        int dumpN = System.Math.Min(count, 10);
        for (int i = 0; i < dumpN; i++)
        {
            var w = IL2CppListOps.Get(allItem, i);
            if (w == null) continue;
            int id = (int)(ReadField(w, "itemID") ?? -1);
            int cnt = (int)(ReadField(w, "itemCount") ?? -1);
            Logger.Info($"Spike Step3: [{i}] itemID={id} itemCount={cnt}");
        }
        Logger.Info("Spike Step3: save → reload → 위 list 와 일치하는지 사용자 확인");
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

- [ ] **Step 2.3: ProbeRunner.cs 작성**

```csharp
// File: src/LongYinRoster/Core/Probes/ProbeRunner.cs

using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

public static class ProbeRunner
{
    public static ProbeItemList.Mode Mode { get; set; } = ProbeItemList.Mode.Step1;

    public static void Trigger()
    {
        Logger.Info($"=== ProbeRunner: F12 → ItemList / {Mode} ===");
        ProbeItemList.Run(Mode);
        Logger.Info("=== ProbeRunner: end ===");
    }

    public static void SetMode(ProbeItemList.Mode m)
    {
        Mode = m;
        Logger.Info($"ProbeRunner.Mode = {m}");
    }
}
```

- [ ] **Step 2.4: ModWindow.Update 에 F12 + 1-3 hotkey 추가**

`src/LongYinRoster/UI/ModWindow.cs` 의 `Update` method 안에 추가:

```csharp
private void Update()
{
    if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

    // v0.5.3 Spike — F12 trigger, mod 창 visible 동안 1-3 으로 Mode 직접 설정 (release 전 cleanup)
    if (Input.GetKeyDown(KeyCode.F12)) Core.Probes.ProbeRunner.Trigger();
    if (_visible)
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            Core.Probes.ProbeRunner.SetMode(Core.Probes.ProbeItemList.Mode.Step1);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            Core.Probes.ProbeRunner.SetMode(Core.Probes.ProbeItemList.Mode.Step2);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            Core.Probes.ProbeRunner.SetMode(Core.Probes.ProbeItemList.Mode.Step3);
    }

    if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
        Time.timeScale = 0f;
}
```

- [ ] **Step 2.5: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED.

- [ ] **Step 2.6: Commit**

```bash
git add src/LongYinRoster/Core/Probes/ src/LongYinRoster/UI/ModWindow.cs
git commit -m "spike(v0.5.3): ProbeItemList + F12 trigger — 3 mode (method dump / ctor dump / persistence)"
```

---

### Task 3: Spike 실행 (사용자 in-game)

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-item-list-spike.md`

- [ ] **Step 3.1: 게임 시작 + Step 1 (HeroData method dump)**

사용자 안내:
1. 게임 시작 → 캐릭터 load (다양한 item 보유)
2. mod F11 → 열림 → 1 (Step 1) → F11 끔
3. F12 → method dump

Expected log: `Lose|Add|Get|Remove*Item*` 시그니처 list. 가능: `LoseAllItem()`, `GetItem(ItemData wrapper, ...)` 등.

- [ ] **Step 3.2: Step 2 (ItemData ctor dump)**

```
F11 → 2 → F11 끔 → F12
```

Expected log: ItemData 의 ctor list. v0.5.2 패턴 가정:
- `ctor: ()` parameterless
- `ctor: (Int32 _itemID)` 또는 `ctor: (Int32 itemID, Int32 itemCount)`
- `ctor: (IntPtr pointer)` — IL2CPP wrapper

- [ ] **Step 3.3: Step 3 (persistence baseline)**

```
F11 → 3 → F11 끔 → F12
```

Expected log: 첫 10 item entry (itemID, itemCount).

- [ ] **Step 3.4: dump 파일 작성**

`docs/superpowers/dumps/2026-05-XX-item-list-spike.md` 에 결과 기록 (XX = 실행 일자):

```markdown
# v0.5.3 Spike — 인벤토리 method + ItemData ctor discovery

## Step 1 — HeroData method dump
[로그 복사]

**clear method 후보**: [TBD]
**add method 후보**: [TBD]

## Step 2 — ItemData ctor dump
[로그 복사]

**ctor 후보**: [TBD]

## Step 3 — Persistence baseline
[로그 복사]

## 종합 판정
[PASS / FAIL]
```

- [ ] **Step 3.5: User gate**

PASS (ctor 발견) → Phase 3 진행
FAIL (ctor 발견 안됨) → User gate (abort + 외형 sub-project 변경 또는 wrapper graph 더 탐색)

- [ ] **Step 3.6: Commit dump**

```bash
git add docs/superpowers/dumps/2026-05-XX-item-list-spike.md
git commit -m "spike(v0.5.3): ItemData ctor 발견 결과 — [PASS path 또는 FAIL]"
```

---

## Phase 3 — Implementation (Spike PASS 후)

### Task 4: ItemListApplierTests.cs (failing tests)

**Files:**
- Create: `src/LongYinRoster.Tests/ItemListApplierTests.cs`

- [ ] **Step 4.1: Test 파일 작성**

```csharp
// File: src/LongYinRoster.Tests/ItemListApplierTests.cs

using System.Text.Json;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemListApplierTests
{
    private static JsonElement ParseSlot(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement;
    }

    [Fact]
    public void ExtractItemList_ReturnsAllEntries()
    {
        var slot = ParseSlot(@"{
          ""itemListData"": {
            ""allItem"": [
              {""itemID"": 100, ""itemCount"": 1},
              {""itemID"": 200, ""itemCount"": 5}
            ]
          }
        }");
        var list = ItemListApplier.ExtractItemList(slot);
        list.Count.ShouldBe(2);
        list[0].ItemID.ShouldBe(100);
        list[1].ItemID.ShouldBe(200);
        list[1].ItemCount.ShouldBe(5);
    }

    [Fact]
    public void ExtractItemList_HandlesEmptyList()
    {
        var slot = ParseSlot(@"{ ""itemListData"": { ""allItem"": [] } }");
        var list = ItemListApplier.ExtractItemList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractItemList_MissingItemListData_ReturnsEmpty()
    {
        var slot = ParseSlot(@"{ ""heroName"": ""test"" }");
        var list = ItemListApplier.ExtractItemList(slot);
        list.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_RespectsApplySelection_SkipsWhenFalse()
    {
        var slot = ParseSlot(@"{ ""itemListData"": { ""allItem"": [{""itemID"": 100, ""itemCount"": 1}] } }");
        var sel = new ApplySelection { ItemList = false };
        var result = ItemListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("selection off");
    }

    [Fact]
    public void Apply_HandlesMissingPlayer_SkipsWithReason()
    {
        var slot = ParseSlot(@"{ ""itemListData"": { ""allItem"": [{""itemID"": 100, ""itemCount"": 1}] } }");
        var sel = new ApplySelection { ItemList = true };
        var result = ItemListApplier.Apply(player: null, slot, sel);
        result.Skipped.ShouldBeTrue();
        result.Reason!.ShouldContain("player null");
    }
}
```

- [ ] **Step 4.2: Run tests — should fail**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ItemListApplierTests"
```

Expected: FAIL — `ItemListApplier` not found (CS0103).

---

### Task 5: ItemListApplier.cs 작성

**Files:**
- Create: `src/LongYinRoster/Core/ItemListApplier.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`

- [ ] **Step 5.1: ItemListApplier.cs 작성** (Spike 결과의 method names 사용 — 아래는 가정)

```csharp
// File: src/LongYinRoster/Core/ItemListApplier.cs

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5.3 — 인벤토리 list Replace (clear + add all).
///
/// Spike Phase 1 결과로 확정:
///   - Clear: HeroData.LoseAllItem() — parameterless (이미 v0.4 stub 가정)
///   - Wrapper ctor: ItemData(int _itemID) — Spike Step 2 발견 (v0.5.2 패턴 mirror)
///   - Property setter: itemCount reflection set
///   - Add: HeroData.GetItem(ItemData wrapper, ...) — 이미 v0.4 stub 가정
///   - 2-pass retry — game-internal silent fail 회피 (v0.5.2 패턴)
///
/// v0.4 PoC A4 의 ItemDataFactory.IsAvailable=false stub 해제. ItemDataFactory 폐기.
/// </summary>
public static class ItemListApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int RemovedCount { get; set; }
        public int AddedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public sealed record ItemEntry(int ItemID, int ItemCount);

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string ClearMethodName = "LoseAllItem";
    private const string AddMethodName   = "GetItem";

    public static IReadOnlyList<ItemEntry> ExtractItemList(JsonElement slot)
    {
        var list = new List<ItemEntry>();
        if (!slot.TryGetProperty("itemListData", out var ild) || ild.ValueKind != JsonValueKind.Object)
            return list;
        if (!ild.TryGetProperty("allItem", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("itemID", out var idEl) || idEl.ValueKind != JsonValueKind.Number) continue;
            int itemID = idEl.GetInt32();
            int count = entry.TryGetProperty("itemCount", out var cEl) && cEl.ValueKind == JsonValueKind.Number ? cEl.GetInt32() : 1;
            list.Add(new ItemEntry(itemID, count));
        }
        return list;
    }

    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.ItemList)
        {
            res.Skipped = true;
            res.Reason = "itemList (selection off)";
            return res;
        }

        var list = ExtractItemList(slot);
        if (player == null)
        {
            res.Skipped = true;
            res.Reason = "player null (test mode)";
            return res;
        }

        var ild = ReadFieldOrProperty(player, "itemListData");
        if (ild == null)
        {
            res.Skipped = true;
            res.Reason = "itemListData null";
            return res;
        }
        var allItem = ReadFieldOrProperty(ild, "allItem");
        if (allItem == null)
        {
            res.Skipped = true;
            res.Reason = "itemListData.allItem null";
            return res;
        }

        // Wrapper type 발견 — 첫 element 의 type
        Type? wrapperType = null;
        if (IL2CppListOps.Count(allItem) > 0)
        {
            var sample = IL2CppListOps.Get(allItem, 0);
            if (sample != null) wrapperType = sample.GetType();
        }
        if (wrapperType == null)
        {
            res.Skipped = true;
            res.Reason = "wrapperType null (allItem empty before clear)";
            return res;
        }

        // Wrapper ctor (int _itemID)
        var wrapperCtor = wrapperType.GetConstructor(F, null, new[] { typeof(int) }, null);
        if (wrapperCtor == null)
        {
            res.Skipped = true;
            res.Reason = $"wrapper ctor (int) not found on {wrapperType.FullName}";
            return res;
        }

        // Clear phase
        int beforeCount = IL2CppListOps.Count(allItem);
        try
        {
            InvokeMethod(player, ClearMethodName, Array.Empty<object>());
            int afterCount = IL2CppListOps.Count(allItem);
            res.RemovedCount = beforeCount - afterCount;
            Logger.Info($"ItemList clear ({ClearMethodName}): {beforeCount} → {afterCount}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"ItemList clear: {ex.GetType().Name}: {ex.Message}");
            res.Skipped = true;
            res.Reason = $"clear failed: {ex.Message}";
            return res;
        }

        // Add phase — 2-pass retry (v0.5.2 패턴)
        for (int pass = 0; pass < 2; pass++)
        {
            int beforePass = IL2CppListOps.Count(allItem);
            foreach (var entry in list)
            {
                try
                {
                    var wrapper = wrapperCtor.Invoke(new object[] { entry.ItemID });
                    TrySetMember(wrapper, "itemCount", entry.ItemCount);
                    InvokeMethod(player, AddMethodName, new object[] { wrapper, false, false });
                }
                catch (Exception ex)
                {
                    if (pass == 0)
                        Logger.Warn($"ItemList add pass={pass} itemID={entry.ItemID}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            int afterPass = IL2CppListOps.Count(allItem);
            Logger.Info($"ItemList add pass={pass}: count {beforePass} → {afterPass} (target={list.Count})");
            if (afterPass >= list.Count) break;
        }

        int finalCount = IL2CppListOps.Count(allItem);
        res.AddedCount = finalCount;
        res.FailedCount = System.Math.Max(0, list.Count - finalCount);

        Logger.Info($"ItemList Apply done — removed={res.RemovedCount} added={res.AddedCount} failed={res.FailedCount}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
    {
        return Apply(player, backup, new ApplySelection { ItemList = true });
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

    private static void TrySetMember(object obj, string name, object value)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null && p.CanWrite)
        {
            try { p.SetValue(obj, value); } catch { }
            return;
        }
        var f = t.GetField(name, F);
        if (f != null)
        {
            try { f.SetValue(obj, value); } catch { }
        }
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

- [ ] **Step 5.2: Tests csproj 에 ItemListApplier link 추가**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 에 `<ItemGroup>` 안에 추가 (KungfuListApplier link 옆):

```xml
<Compile Include="../LongYinRoster/Core/ItemListApplier.cs">
  <Link>Core/ItemListApplier.cs</Link>
</Compile>
```

- [ ] **Step 5.3: Run tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ItemListApplierTests"
```

Expected: 5/5 PASS.

- [ ] **Step 5.4: 전체 tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 62/62 PASS.

- [ ] **Step 5.5: Commit**

```bash
git add src/LongYinRoster/Core/ItemListApplier.cs src/LongYinRoster.Tests/ItemListApplierTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(core): ItemListApplier — LoseAllItem clear + ctor(int) wrapper + GetItem add 2-pass + 5 tests"
```

---

### Task 6: PinpointPatcher.RebuildItemList 본문 교체 + Probe 정확화

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`

- [ ] **Step 6.1: RebuildItemList 본문 교체**

`src/LongYinRoster/Core/PinpointPatcher.cs` 의 `RebuildItemList` (현재 v0.4 stub 형태) 를 다음으로 교체:

```csharp
private static void RebuildItemList(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.ItemList) { res.SkippedFields.Add("itemList (selection off)"); return; }
    if (!Probe().ItemList)   { res.SkippedFields.Add("itemList (capability off)"); return; }

    var r = ItemListApplier.Apply(player, slot, selection);
    if (r.Skipped) { res.SkippedFields.Add($"itemList — {r.Reason}"); return; }
    res.AppliedFields.Add($"itemList (removed={r.RemovedCount} added={r.AddedCount} failed={r.FailedCount})");
    if (r.FailedCount > 0)
        res.WarnedFields.Add($"itemList — {r.FailedCount} entries failed");
}
```

- [ ] **Step 6.2: ProbeItemListCapability 정확화 — ItemDataFactory 의존 제거**

기존:
```csharp
private static bool ProbeItemListCapability(object p)
{
    return ItemDataFactory.IsAvailable
        && p.GetType().GetMethod("LoseAllItem", F) != null
        && p.GetType().GetMethod("GetItem", F) != null;
}
```

v0.5.3 교체:
```csharp
private static bool ProbeItemListCapability(object p)
{
    // v0.5.3 — ItemDataFactory 폐기. method 존재 검사만으로 capability 결정.
    // ItemData ctor 검사는 ItemListApplier.Apply 시 lazy.
    return p.GetType().GetMethod("LoseAllItem", F, null, Type.EmptyTypes, null) != null
        && p.GetType().GetMethod("GetItem", F) != null;
}
```

- [ ] **Step 6.3: Build + tests 회귀**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: BUILD SUCCEEDED, 62/62 PASS.

- [ ] **Step 6.4: Commit**

```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "feat(core): PinpointPatcher.RebuildItemList 본문 교체 + ProbeItemListCapability ItemDataFactory 의존 제거"
```

---

### Task 7: ItemDataFactory 폐기

**Files:**
- Delete: `src/LongYinRoster/Core/ItemDataFactory.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`

- [ ] **Step 7.1: ItemDataFactory.cs 삭제**

```bash
git rm src/LongYinRoster/Core/ItemDataFactory.cs
```

- [ ] **Step 7.2: Tests csproj 에서 ItemDataFactory link 제거**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 에서 다음 줄 제거:

```xml
<Compile Include="../LongYinRoster/Core/ItemDataFactory.cs">
  <Link>Core/ItemDataFactory.cs</Link>
</Compile>
```

- [ ] **Step 7.3: PinpointPatcher.cs 에서 ItemDataFactory 참조 제거**

기존 `RebuildItemList` 의 v0.4 stub 코드에 `ItemDataFactory.Create` 호출이 있다면 (Step 6.1 에서 이미 교체됨) — 추가 정리 없음.

- [ ] **Step 7.4: Build + tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: BUILD SUCCEEDED, 62/62 PASS.

- [ ] **Step 7.5: Commit**

```bash
git add -A
git commit -m "chore(core): ItemDataFactory 폐기 — KungfuListApplier 와 일관, ctor 직접 호출"
```

---

## Phase 4 — Smoke 시나리오 (in-game 검증)

### Task 8: Smoke 시나리오 1 — 다른 캐릭터 인벤토리 Apply

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-v0.5.3-smoke.md`

- [ ] **Step 8.1: Pre — 다른 캐릭터 인벤토리 capture**

사용자 안내:
1. 다른 캐릭터 (다양한 장비/소모품) → game load → mod slot 1 capture
2. 현재 캐릭터 (다른 인벤토리) load
3. mod F11 → slot 1 → ✓ 인벤토리 → ▼ Apply

- [ ] **Step 8.2: BepInEx 로그 확인**

```bash
grep -n "ItemList Apply done\|ItemList clear" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -10
```

Expected:
- `ItemList clear (LoseAllItem): N → 0`
- `ItemList add pass=0: count 0 → M`
- `ItemList Apply done — removed=N added=M failed=K`

- [ ] **Step 8.3: 인벤토리 패널 확인** — slot 1 의 item set 으로 변경

- [ ] **Step 8.4: save → reload persistence**

게임 save → 종료 → 재시작 → load → 인벤토리 유지 확인

- [ ] **Step 8.5: Smoke dump 작성**

```markdown
# v0.5.3 Smoke 결과

## 시나리오 1 — 다른 캐릭터 인벤토리 Apply
- 결과: [PASS / FAIL]
- save → reload: [PASS / FAIL]
```

---

### Task 9: Smoke 시나리오 2 — Self-Apply

- [ ] **Step 9.1: 현재 캐릭터 capture → mod slot 1**
- [ ] **Step 9.2: 게임에서 일부 item 사용/구입 → 인벤토리 변경**
- [ ] **Step 9.3: mod slot 1 → ✓ ItemList → ▼ Apply (자동백업 → slot 0)**
- [ ] **Step 9.4: 인벤토리 = slot 1 시점 확인**
- [ ] **Step 9.5: save → reload 검증**
- [ ] **Step 9.6: Smoke dump update**

---

### Task 10: Smoke 시나리오 3 — Restore + 회귀

- [ ] **Step 10.1: 시나리오 2 직후 → mod slot 0 → ↶ 복원**
- [ ] **Step 10.2: 인벤토리 = 변경된 시점 확인**
- [ ] **Step 10.3: 회귀 — v0.5.2 KungfuList / active / 정체성 / 천부 / 스탯 등 동작 유지**
- [ ] **Step 10.4: legacy 슬롯 (v0.1~v0.5.2) 호환 확인**
- [ ] **Step 10.5: Smoke dump 종합 판정 + commit**

```bash
git add docs/superpowers/dumps/2026-05-XX-v0.5.3-smoke.md
git commit -m "docs: v0.5.3 smoke 결과 — 시나리오 1/2/3 + 회귀 [PASS]"
```

---

## Phase 5 — Release

### Task 11: Probe 코드 cleanup (D16 패턴)

**Files:**
- Delete: `src/LongYinRoster/Core/Probes/`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 11.1: Probes 디렉토리 삭제**

```bash
git rm -r src/LongYinRoster/Core/Probes/
```

- [ ] **Step 11.2: ModWindow.Update 의 F12 + 1-3 hotkey 제거**

```csharp
private void Update()
{
    if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

    if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
        Time.timeScale = 0f;
}
```

- [ ] **Step 11.3: Build + tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: BUILD SUCCEEDED, 62/62 PASS.

- [ ] **Step 11.4: Commit**

```bash
git add -A
git commit -m "chore(release): remove Probe code + F12/1-3 hotkey (D16 패턴)"
```

---

### Task 12: VERSION + README + HANDOFF

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`
- Modify: `README.md`
- Modify: `docs/HANDOFF.md`

- [ ] **Step 12.1: VERSION 0.5.2 → 0.5.3**

`src/LongYinRoster/Plugin.cs:17`:
```csharp
public const string VERSION = "0.5.3";
```

- [ ] **Step 12.2: README — v0.5.3 highlights + Releases**

`README.md` 에 v0.5.2 섹션 다음 추가:

```markdown
### v0.5.3 — 인벤토리 활성화

- **인벤토리 카테고리 활성화** — slot 의 itemListData.allItem 을 player 에 완전 교체 (clear + add all)
- **알고리즘**: v0.5.2 패턴 mirror — `LoseAllItem` clear + `ItemData(itemID)` ctor + `GetItem(wrapper)` add (2-pass retry)
- **v0.4 PoC A4 의 ItemDataFactory.IsAvailable=false stub 해제**
- **ItemDataFactory 폐기** — KungfuListApplier 와 일관 (ctor 직접 호출)
- **현재 미지원**: ItemData sub-data (강화도/옵션 등) 보존 — itemID + count 만 복원
```

Releases 표:
```
| v0.5.3 | 인벤토리 활성화 (clear + add all + 2-pass retry) |
```

- [ ] **Step 12.3: HANDOFF — §1 main baseline + Releases**

`docs/HANDOFF.md`:
- §1 한 줄 요약 → main baseline = v0.5.3
- Releases list → v0.5.3 entry 추가

- [ ] **Step 12.4: Build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: SUCCEEDED. 게임 실행 시 BepInEx 로그에 `Loaded LongYin Roster Mod v0.5.3`.

- [ ] **Step 12.5: Commit**

```bash
git add src/LongYinRoster/Plugin.cs README.md docs/HANDOFF.md
git commit -m "chore(release): v0.5.3 — VERSION bump + README/HANDOFF update"
```

---

### Task 13: dist + tag + main merge + push + GitHub release

**Files:**
- Create: `dist/LongYinRoster_v0.5.3/`, `dist/LongYinRoster_v0.5.3.zip`
- Tag: `v0.5.3`

- [ ] **Step 13.1: dist 디렉토리 생성**

```bash
mkdir -p "dist/LongYinRoster_v0.5.3/BepInEx/plugins/LongYinRoster"
cp "src/LongYinRoster/bin/Release/LongYinRoster.dll" "dist/LongYinRoster_v0.5.3/BepInEx/plugins/LongYinRoster/"
cp README.md "dist/LongYinRoster_v0.5.3/"
```

- [ ] **Step 13.2: zip 생성 (PowerShell)**

```powershell
Set-Location "E:\Games\龙胤立志传.v1.0.0f8.2\LongYinLiZhiZhuan\Save\_PlayerExport"
Compress-Archive -Path "dist\LongYinRoster_v0.5.3\*" -DestinationPath "dist\LongYinRoster_v0.5.3.zip" -Force
```

- [ ] **Step 13.3: tag 생성**

```bash
git tag -a v0.5.3 -m "v0.5.3 — 인벤토리 활성화 (clear + add all + 2-pass retry)"
```

- [ ] **Step 13.4: main merge**

```bash
git checkout main
git merge --no-ff v0.5.3 -m "Merge v0.5.3 — 인벤토리 활성화"
```

- [ ] **Step 13.5: push origin main + tag**

```bash
git push origin main
git push origin refs/tags/v0.5.3
```

- [ ] **Step 13.6: GitHub release**

```bash
gh release create v0.5.3 dist/LongYinRoster_v0.5.3.zip \
  --repo game-mod-project/long_yin_li_zhi_zhuan_mode \
  --title "v0.5.3 — 인벤토리 활성화" \
  --notes "v0.5.2 패턴 mirror — LoseAllItem clear + ItemData(itemID) ctor + GetItem(wrapper) add 2-pass retry. v0.4 PoC A4 의 ItemDataFactory stub 해제. ItemDataFactory 폐기."
```

---

## Self-Review Checklist

**1. Spec coverage**:
- [x] §1 Context — Phase 1 baseline + v0.5.2 통찰 활용 ✓
- [x] §2 Goals (7) — Task 4-12 ✓
- [x] §2 Non-goals — 창고/외형/sub-data 제외 ✓
- [x] §3 Architecture — Hybrid spike + spec + impl ✓
- [x] §4 Spike Phase 1 — Task 2-3 ✓
- [x] §5 Implementation — Task 4-7 ✓
- [x] §6 Smoke — Task 8-10 ✓
- [x] §7 Failure mode — Task 3 user gate, Step 5/6 build verify ✓
- [x] §8 Release — Task 11-13 ✓
- [x] §9 v0.6+ 후보 — Task 12.3 HANDOFF 갱신 ✓
- [x] Appendix A ItemDataFactory 폐기 — Task 7 ✓

**2. Placeholder scan**:
- "TBD" 는 Spike 결과 미정 항목만 (의도된 placeholder) ✓
- "Similar to" 없음 ✓
- 모든 step 에 실제 code/명령 ✓

**3. Type consistency**:
- `ItemListApplier.Apply / Restore / ExtractItemList / Result / ItemEntry` — Task 4/5/6 일관 ✓
- `ItemEntry(int ItemID, int ItemCount)` — record 일관 ✓
- `Capabilities.ItemList / ApplySelection.ItemList` — 이미 v0.4 부터 존재, 변경 없음 ✓
- `ClearMethodName="LoseAllItem"` / `AddMethodName="GetItem"` — 일관 ✓

수정 항목 없음.

---

**Plan complete.**
