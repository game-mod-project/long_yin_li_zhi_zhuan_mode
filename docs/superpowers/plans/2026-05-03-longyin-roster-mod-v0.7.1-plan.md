# v0.7.1 컨테이너 UX 개선 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v0.7.0.1 smoke 도중 사용자가 보고한 3 UX 이슈 (destination 모호 / capacity 정보 부재 / 가드 부재) 를 한 release 로 마무리.

**Architecture:** Spec §4 의 3 task 를 phase 로 분해. Phase 1 (Capacity reflection spike) → Phase 2 (Capacity helper, spike 결과에 따라 path fork) → Phase 3 (KoreanStrings) → Phase 4 (ContainerOps signature) → Phase 5 (ContainerOpsHelper 분리) → Phase 6 (ContainerPanel UI) → Phase 7 (ModWindow wiring) → Phase 8 (단위 테스트) → Phase 9 (인게임 smoke) → Phase 10 (release). 게임 사실 (인벤 over-cap 허용 / 창고 hard cap) 을 위해 destination 별 분기.

**Tech Stack:** BepInEx 6 IL2CPP, Unity IMGUI, .NET 6 SDK, xUnit + Shouldly, HarmonyLib, System.Text.Json. Baseline: v0.7.0.1 main HEAD (`0d25e14`).

---

## ⚠ Amendment (2026-05-03) — spike 결과 따른 capacity → maxWeight 변경

Phase 1.2 인게임 spike 결과 (`docs/superpowers/dumps/2026-05-03-v0.7.1-capacity-spike.md`):
- ItemListData 의 capacity 후보는 `maxWeight` (Single/float) 단 하나
- **갯수 기반 capacity 자체가 게임에 없음 — 무게 기반 (kg)**
- 사용자 결정 (B): 라벨에 갯수+무게 둘 다 표시, 가드는 무게 기반

**Spec 갱신**: §3.4 / §4.2 / §4.3 / §6 / §7.2 / §9 무게 기반으로 재정의. 본 plan 의 아래 phase 들은 spec 변경에 따라 **int / capacity → float / maxWeight** 일관 치환:

| Phase | 변경 사항 |
|---|---|
| 2.1 | ConfigEntry<int> → <float>, default 171/217 → 964f/300f, 이름 InventoryCapacity → InventoryMaxWeight, AcceptableValueRange<float>(100, 10000) / (10, 50000). **이미 commit `358991a` 됐음 → fix-up commit 으로 변경** |
| 2.2 | CAPACITY_NAMES = `{ "maxWeight" }`, GetCapacity → **GetMaxWeight (float, float fallback)** |
| 3.1 | ToastInvOk = `"인벤토리로 {0}개 처리"`, ToastInvOvercap = `"인벤토리로 {0}개 처리 ({1:F1}/{2:F1} kg 초과 — 이동속도 저하)"`, ToastStoFull = `"창고 무게 한계 — 처리 불가"`, ToastStoPartial = `"창고로 {0}개 처리 ({1}개는 무게 초과로 컨테이너에 남김)"` |
| 4.1 | OverCap (int) → **OverCapWeight (float)** |
| 4.2 | maxCapacity (int) → **maxWeight (float)** + 누적 weight 계산 (entry.weight 합산해서 currentWeight + sumTried > maxWeight 체크) |
| 5.1 | capacity (int) → **maxWeight (float)**, Result.OverCapWeight (float) |
| 6.1 | _inventoryCapacity / _storageCapacity (int, default 171/217) → **_inventoryMaxWeight / _storageMaxWeight (float, default 964f/300f)** |
| 6.3 | FormatCount(label, cur, max, allowOvercap) → **FormatCount(label, countN, currentWeight, maxWeight, allowOvercap)**. 반환: `"{label} ({countN}개, {curW:F1} / {maxW:F1} kg)"` + over-cap 마커 |
| 6.4 | DrawLeftColumn — currentWeight = `_inventoryRows.Sum(r => r.Weight)` (또는 LINQ 없이 foreach) |
| 7.1 | InventoryCapacity → InventoryMaxWeight, GetCapacity → GetMaxWeight, capacity → maxWeight, OverCap → OverCapWeight 일관 변경 |
| 8.3 | FormatCount tests — `(label, countN, curW, maxW, allowOvercap)` signature 맞춰 4 케이스 update |

다른 phase (1.1, 6.2, 6.5, 8.1, 8.2, 9.1, 9.2, 10) 는 영향 없음.

각 implementer prompt 시 controller 가 위 변경 반영된 정확한 코드를 직접 전달 — implementer 는 본 plan 의 옛 코드 블록 대신 prompt 의 코드를 따름.

---

## File Structure

**Create:**
- `src/LongYinRoster/Core/Probes/ProbeItemListCapacity.cs` — Phase 1 spike, capacity property dump
- `src/LongYinRoster/Core/ItemListReflector.cs` — Phase 2 capacity helper (PASS path: reflection / FAIL path: config 값 반환). 같은 파일·같은 signature, 내부 분기.
- `src/LongYinRoster.Tests/ContainerPanelFormatTests.cs` — FormatCount 단위 테스트
- `dist/release-notes-v0.7.1.md` — release notes

**Modify:**
- `src/LongYinRoster/Containers/ContainerOps.cs:79-84,108-172` — GameMoveResult.OverCap 추가, AddItemsJsonToGame signature 변경
- `src/LongYinRoster/Containers/ContainerOpsHelper.cs:17-22,47-76` — Result.OverCap, ContainerToInventory/Storage 분리, 기존 ContainerToGame 제거
- `src/LongYinRoster/UI/ContainerPanel.cs:62-67,77-79,159-182,184-274` — callback 4개 추가, SetInventory/StorageRows(capacity), DrawLeftColumn / DrawRightColumn UI 변경, FormatCount helper
- `src/LongYinRoster/UI/ModWindow.cs:118-129,165-168,218-226` — 4 callback wiring, RefreshAllContainerRows capacity 전달, DoContainerToGame 분리
- `src/LongYinRoster/Plugin.cs:17` — VERSION 0.7.0.1 → 0.7.1
- `src/LongYinRoster/Util/KoreanStrings.cs:끝` — 라벨·toast 상수 추가
- `src/LongYinRoster/Config.cs:23-50` — InventoryCapacity / StorageCapacity ConfigEntry (FAIL path 만 사용, 항상 bind)
- `src/LongYinRoster.Tests/ContainerOpsTests.cs:끝` — AddItemsJsonToGame allowOvercap 분기 (가능 범위)
- `src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs:46-56` — ContainerToGame → ContainerToInventory/Storage rename
- `docs/HANDOFF.md` — §1 v0.7.1 entry, sub-project 번호 재정렬

**Update:**
- `docs/superpowers/dumps/2026-05-03-v0.7.1-capacity-spike.md` (신규) — Phase 1 결과 dump
- `docs/superpowers/dumps/2026-05-03-v0.7.1-smoke-results.md` (신규) — Phase 9 smoke 결과

---

## Phase 1: Capacity Reflection Spike

목표: ItemListData 의 capacity property 가 IL2CPP wrapper 에 노출되는지 확인. 결과에 따라 Phase 2 path 선택.

### Task 1.1: ProbeItemListCapacity 작성

**Files:**
- Create: `src/LongYinRoster/Core/Probes/ProbeItemListCapacity.cs`

- [ ] **Step 1: ProbeItemListCapacity.cs 작성**

```csharp
using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.7.1 Phase 1 spike — player.itemListData / player.selfStorage 의
/// capacity 후보 property/field enumerate. F12 핸들러로 1회 호출 후 BepInEx
/// 로그 분석. 결정 후 본 파일은 git 에 보존하되 [F12] handler 는 제거 (release 전).
/// </summary>
public static class ProbeItemListCapacity
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly string[] Keywords = new[]
    {
        "capacity", "max", "limit", "size", "volume", "count"
    };

    public static void Run()
    {
        Logger.Info("=== ProbeItemListCapacity.Run ===");
        var p = HeroLocator.GetPlayer();
        if (p == null) { Logger.Warn("player null — 게임 진입 후 시도"); return; }
        DumpOne(p, "itemListData");
        DumpOne(p, "selfStorage");
        Logger.Info("=== ProbeItemListCapacity.Run end ===");
    }

    private static void DumpOne(object player, string fieldName)
    {
        var ild = ReadFieldOrProperty(player, fieldName);
        if (ild == null) { Logger.Warn($"{fieldName} null"); return; }
        var t = ild.GetType();
        Logger.Info($"--- {fieldName} type={t.FullName} ---");

        foreach (var prop in t.GetProperties(F))
        {
            string n = prop.Name.ToLowerInvariant();
            foreach (var kw in Keywords)
            {
                if (n.Contains(kw))
                {
                    object? v = null;
                    try { v = prop.GetValue(ild); } catch (Exception ex) { v = $"<throw {ex.GetType().Name}>"; }
                    Logger.Info($"  prop {prop.PropertyType.Name} {prop.Name} = {v}");
                    break;
                }
            }
        }
        foreach (var fld in t.GetFields(F))
        {
            string n = fld.Name.ToLowerInvariant();
            foreach (var kw in Keywords)
            {
                if (n.Contains(kw))
                {
                    object? v = null;
                    try { v = fld.GetValue(ild); } catch (Exception ex) { v = $"<throw {ex.GetType().Name}>"; }
                    Logger.Info($"  fld  {fld.FieldType.Name} {fld.Name} = {v}");
                    break;
                }
            }
        }
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var prop = t.GetProperty(name, F);
        if (prop != null) return prop.GetValue(obj);
        var fld = t.GetField(name, F);
        if (fld != null) return fld.GetValue(obj);
        return null;
    }
}
```

- [ ] **Step 2: F12 임시 핸들러 추가**

`src/LongYinRoster/UI/ModWindow.cs:665` 의 기존 line:

```csharp
        if (Input.GetKeyDown(KeyCode.F12)) Core.Probes.ProbeRunner.Trigger();
```

을 다음으로 교체 (Shift 와 함께 누르면 capacity probe 실행, 그 외엔 기존 ItemList probe 유지):

```csharp
        if (Input.GetKeyDown(KeyCode.F12))
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                Core.Probes.ProbeItemListCapacity.Run();
            else
                Core.Probes.ProbeRunner.Trigger();
        }
```

- [ ] **Step 3: 빌드**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 4: commit (spike 코드 보존)**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/Core/Probes/ProbeItemListCapacity.cs src/LongYinRoster/UI/ModWindow.cs
git -C "Save/_PlayerExport" commit -m "feat(spike): v0.7.1 ProbeItemListCapacity + Shift+F12 trigger"
```

### Task 1.2: 인게임 spike 실행 + 결과 dump

- [ ] **Step 1: BepInEx 로그 클리어**

```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

- [ ] **Step 2: 게임 실행, 캐릭터 진입 후 Shift+F12 1회**

  사용자에게 요청: "게임 진입 후 Shift+F12 한 번 누른 뒤 게임 종료해 주세요"

- [ ] **Step 3: 결과 grep**

```bash
grep -n "ProbeItemListCapacity" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

Expected: `ProbeItemListCapacity.Run` 시작 + `--- itemListData type=...` + capacity 후보 property/field 들 + `--- selfStorage type=...` + 동상 + `Run end`.

- [ ] **Step 4: dump 보존 + fork 결정 기록**

dump 결과를 파일에 저장:

```bash
grep "ProbeItemListCapacity" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" > "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/docs/superpowers/dumps/2026-05-03-v0.7.1-capacity-spike.md"
```

수동으로 dump file 의 head 에 결정 기록 추가:

```markdown
# v0.7.1 Capacity Spike Result

**일시**: 2026-05-03
**결정**: PASS / FAIL  ← 둘 중 하나 선택
**선택된 path**:
  - PASS path 시: 사용 property/field 이름 (예: `selfMaxCount`)
  - FAIL path 시: Config fallback 사용

(이하 raw dump)
...
```

PASS criterion: capacity 후보 property/field 가 (a) int 형 + (b) 합리적 값 (인벤≈171, 창고≈217 또는 그와 비슷한 범위) 으로 한 개 이상 발견.
FAIL criterion: 후보 0건 또는 모두 무관 (e.g. itemTypeCount, allItem.Count 같은 현재 갯수만).

- [ ] **Step 5: commit dump**

```bash
git -C "Save/_PlayerExport" add docs/superpowers/dumps/2026-05-03-v0.7.1-capacity-spike.md
git -C "Save/_PlayerExport" commit -m "docs(spike): v0.7.1 capacity spike result + path 결정"
```

---

## Phase 2: ItemListReflector Helper

Phase 1 fork 결과에 따라 두 path 중 하나 적용. helper signature 는 양쪽 동일.

### Task 2.1: Config entry 추가 (FAIL path fallback / 항상 bind)

**Files:**
- Modify: `src/LongYinRoster/Config.cs`

- [ ] **Step 1: Config.cs 에 두 entry 추가**

`src/LongYinRoster/Config.cs:21` 의 `LogLevel` 선언 다음, line 23 `Bind` 메서드 직전에 추가:

```csharp
    public static ConfigEntry<int>     InventoryCapacity = null!;
    public static ConfigEntry<int>     StorageCapacity   = null!;
```

`src/LongYinRoster/Config.cs:46` 의 `LogLevel = cfg.Bind(...)` 다음에 추가:

```csharp
        InventoryCapacity = cfg.Bind("Container", "InventoryCapacity", 171,
                                     new ConfigDescription(
                                         "인벤토리 capacity. spike PASS 시 reflection 우선, 미발견 시 본 값 fallback.",
                                         new AcceptableValueRange<int>(1, 1000)));
        StorageCapacity   = cfg.Bind("Container", "StorageCapacity",   217,
                                     new ConfigDescription(
                                         "창고 capacity. 동상.",
                                         new AcceptableValueRange<int>(1, 10000)));
```

- [ ] **Step 2: build 검증**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 3: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/Config.cs
git -C "Save/_PlayerExport" commit -m "feat(config): v0.7.1 InventoryCapacity / StorageCapacity entries"
```

### Task 2.2: ItemListReflector 작성 (Phase 1 결과 반영)

**Files:**
- Create: `src/LongYinRoster/Core/ItemListReflector.cs`

- [ ] **Step 1: ItemListReflector.cs 작성 — PASS path 선택 시**

dump 에서 발견된 property/field 이름을 `CAPACITY_PROP_NAME` 에 박는다 (예시: `selfMaxCount`). 미발견 시 config fallback.

```csharp
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// ItemListData (인벤/창고 wrapper) 의 capacity 추출 helper.
/// Phase 1 spike 결과에 따라 PASS path 의 property/field 이름이 결정된다.
/// 발견 못 한 destination 은 Config fallback 으로 자동 회귀.
/// </summary>
public static class ItemListReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Phase 1 spike 결과로 채움. 미발견 시 빈 string array 유지 → 항상 fallback.
    // 예: new[] { "selfMaxCount", "maxItemCount" }
    private static readonly string[] CAPACITY_NAMES = new string[] { /* spike 결과 채움 */ };

    /// <summary>
    /// reflection 으로 itemList wrapper 의 capacity 시도. 미발견 시 fallbackValue 반환.
    /// </summary>
    public static int GetCapacity(object? itemList, int fallbackValue)
    {
        if (itemList == null) return fallbackValue;
        var t = itemList.GetType();
        foreach (var name in CAPACITY_NAMES)
        {
            var prop = t.GetProperty(name, F);
            if (prop != null && prop.PropertyType == typeof(int))
            {
                try { return (int)prop.GetValue(itemList)!; }
                catch (System.Exception ex) { Logger.Warn($"ItemListReflector.GetCapacity prop {name}: {ex.Message}"); }
            }
            var fld = t.GetField(name, F);
            if (fld != null && fld.FieldType == typeof(int))
            {
                try { return (int)fld.GetValue(itemList)!; }
                catch (System.Exception ex) { Logger.Warn($"ItemListReflector.GetCapacity fld {name}: {ex.Message}"); }
            }
        }
        return fallbackValue;
    }
}
```

**FAIL path 분기**: spike FAIL 일 시 `CAPACITY_NAMES = new string[] { }` 그대로 유지 → reflection skip → 항상 fallbackValue 반환. 즉 코드는 동일, 문자열 배열만 비움.

- [ ] **Step 2: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 3: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/Core/ItemListReflector.cs
git -C "Save/_PlayerExport" commit -m "feat(core): v0.7.1 ItemListReflector — capacity helper (spike 결과 반영)"
```

---

## Phase 3: KoreanStrings — 라벨·toast 상수 추가

### Task 3.1: KoreanStrings 추가

**Files:**
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs`

- [ ] **Step 1: KoreanStrings.cs 끝에 추가**

`src/LongYinRoster/Util/KoreanStrings.cs:103` (마지막 `}` 직전) 에 다음 추가:

```csharp

    // v0.7.1 — 컨테이너 UX 개선
    public const string Lbl_Inventory          = "인벤토리";
    public const string Lbl_Storage            = "창고";
    public const string Lbl_Container          = "컨테이너";
    public const string Lbl_OvercapMarker      = " ⚠ 초과";

    public const string BtnInvMove             = "← 인벤으로 이동";
    public const string BtnInvCopy             = "← 인벤으로 복사";
    public const string BtnStoMove             = "← 창고로 이동";
    public const string BtnStoCopy             = "← 창고로 복사";

    public const string ToastInvOk             = "인벤토리로 {0}개 처리";
    public const string ToastInvOvercap        = "인벤토리로 {0}개 처리 ({1}/{2} 초과 — 이동속도 저하)";
    public const string ToastStoOk             = "창고로 {0}개 처리";
    public const string ToastStoPartial        = "창고로 {0}개 처리 ({1}개는 컨테이너에 남김)";
    public const string ToastStoFull           = "창고 가득 참 — 처리 불가";
```

- [ ] **Step 2: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 3: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/Util/KoreanStrings.cs
git -C "Save/_PlayerExport" commit -m "feat(strings): v0.7.1 컨테이너 UX 라벨/toast 상수"
```

---

## Phase 4: ContainerOps signature 변경

### Task 4.1: GameMoveResult 에 OverCap 추가

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerOps.cs:79-84`

- [ ] **Step 1: 현재 GameMoveResult 확인**

```bash
sed -n '79,84p' "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/Containers/ContainerOps.cs"
```

Expected:
```
    public sealed class GameMoveResult
    {
        public int Succeeded { get; set; }
        public int Failed    { get; set; }
        public string? Reason { get; set; }
    }
```

- [ ] **Step 2: OverCap 필드 추가**

위 block 을 다음으로 교체:

```csharp
    public sealed class GameMoveResult
    {
        public int Succeeded { get; set; }
        public int Failed    { get; set; }
        public int OverCap   { get; set; }   // v0.7.1 — 인벤 over-cap 시 capacity 초과 갯수 (allowOvercap=true 분기)
        public string? Reason { get; set; }
    }
```

- [ ] **Step 3: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

### Task 4.2: AddItemsJsonToGame signature 확장

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerOps.cs:108-172`

- [ ] **Step 1: 현재 method 확인**

```bash
sed -n '108,172p' "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/Containers/ContainerOps.cs"
```

Expected: `public static GameMoveResult AddItemsJsonToGame(object player, string itemsJson, int maxCapacity)` 시작.

- [ ] **Step 2: signature + 본체 교체**

위 method 전체를 다음으로 교체:

```csharp
    /// <summary>
    /// JSON array 의 각 entry 를 ItemData wrapper 로 deep-copy 후 player.{targetField}.allItem 에 GetItem 호출.
    /// allowOvercap=true (인벤): capacity 가드 skip, 모든 entry 시도. capacity 초과분은 OverCap 필드에 보고.
    /// allowOvercap=false (창고): 현 로직 유지. available = max(0, maxCapacity - cur), 초과분 Failed.
    /// </summary>
    public static GameMoveResult AddItemsJsonToGame(object player, string itemsJson, int maxCapacity, bool allowOvercap, string targetField)
    {
        var res = new GameMoveResult();
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array) { res.Reason = "itemsJson 이 array 아님"; return res; }

            var ild = ReadFieldOrProperty(player, targetField);
            var allItem = ild != null ? ReadFieldOrProperty(ild, "allItem") : null;
            if (allItem == null) { res.Reason = $"player.{targetField}.allItem null"; return res; }
            int curN = IL2CppListOps.Count(allItem);

            Type? wrapperType = null;
            for (int k = 0; k < curN && wrapperType == null; k++)
            {
                var s = IL2CppListOps.Get(allItem, k);
                if (s != null) wrapperType = s.GetType();
            }
            if (wrapperType == null) { res.Reason = "wrapperType 미발견 (인벤토리/창고 비어있음)"; return res; }

            ConstructorInfo? ctor = null;
            Type? itemTypeEnum = null;
            foreach (var c in wrapperType.GetConstructors(F))
            {
                var ps = c.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType.IsEnum && ps[0].ParameterType.Name == "ItemType")
                {
                    ctor = c; itemTypeEnum = ps[0].ParameterType; break;
                }
            }
            if (ctor == null) { res.Reason = "ItemType ctor 미발견"; return res; }

            int requested = arr.GetArrayLength();
            int available = allowOvercap ? requested : Math.Max(0, maxCapacity - curN);

            for (int i = 0; i < requested; i++)
            {
                if (!allowOvercap && res.Succeeded >= available) { res.Failed++; continue; }
                var entry = arr[i];
                try
                {
                    int type = entry.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.Number ? tEl.GetInt32() : 0;
                    var wrapper = ctor.Invoke(new object[] { Enum.ToObject(itemTypeEnum!, type) });
                    ItemListApplier.ApplyJsonToObject(entry, wrapper, depth: 0);
                    InvokeMethod(player, "GetItem", new object[] { wrapper, false });
                    res.Succeeded++;
                }
                catch (Exception ex)
                {
                    res.Failed++;
                    Logger.Warn($"ContainerOps.AddItemsJsonToGame entry[{i}]: {ex.GetType().Name}: {ex.Message}");
                }
            }

            // allowOvercap=true 시 maxCapacity 초과분을 OverCap 으로 보고 (사용자 안내용)
            if (allowOvercap)
            {
                int finalCount = curN + res.Succeeded;
                res.OverCap = Math.Max(0, finalCount - maxCapacity);
            }
        }
        catch (Exception ex)
        {
            res.Reason = $"AddItemsJsonToGame threw: {ex.Message}";
        }
        return res;
    }
```

- [ ] **Step 3: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build` 가 ContainerOpsHelper.cs 의 옛 호출 site 때문에 FAIL 예상 (다음 Phase 에서 fix). 컴파일러 에러 메시지가 `AddItemsJsonToGame` argument count mismatch 가리키면 정상.

- [ ] **Step 4: commit (다음 phase 까지 빌드 깨진 상태)**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/Containers/ContainerOps.cs
git -C "Save/_PlayerExport" commit -m "feat(containers): v0.7.1 AddItemsJsonToGame allowOvercap+targetField, OverCap 필드"
```

---

## Phase 5: ContainerOpsHelper 분리

### Task 5.1: Result 에 OverCap 추가, ContainerToGame 제거, ContainerToInventory/Storage 신설

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerOpsHelper.cs:17-22,47-76`

- [ ] **Step 1: Result class 에 OverCap 추가**

`src/LongYinRoster/Containers/ContainerOpsHelper.cs:17-22` 의 Result class 를 다음으로 교체:

```csharp
    public sealed class Result
    {
        public int    Succeeded { get; set; }
        public int    Failed    { get; set; }
        public int    OverCap   { get; set; }   // v0.7.1 — 인벤 over-cap 발생 갯수
        public string Reason    { get; set; } = "";
    }
```

- [ ] **Step 2: ContainerToGame 메서드 제거 + ContainerToInventory/Storage 추가**

`src/LongYinRoster/Containers/ContainerOpsHelper.cs:47-76` 의 `ContainerToGame` 전체를 다음 두 method 로 교체:

```csharp
    /// <summary>
    /// v0.7.1: 컨테이너 → player.itemListData (인벤토리). over-cap 허용 (allowOvercap=true).
    /// </summary>
    public Result ContainerToInventory(object player, HashSet<int> indices, bool removeFromContainer, int capacity)
    {
        return ContainerToTarget(player, indices, removeFromContainer, capacity,
                                  allowOvercap: true, targetField: "itemListData");
    }

    /// <summary>
    /// v0.7.1: 컨테이너 → player.selfStorage (창고). hard cap (allowOvercap=false).
    /// </summary>
    public Result ContainerToStorage(object player, HashSet<int> indices, bool removeFromContainer, int capacity)
    {
        return ContainerToTarget(player, indices, removeFromContainer, capacity,
                                  allowOvercap: false, targetField: "selfStorage");
    }

    private Result ContainerToTarget(object player, HashSet<int> indices, bool removeFromContainer,
                                      int capacity, bool allowOvercap, string targetField)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string existing  = _repo.LoadItemsJson(CurrentContainerIndex);
            string extracted = ContainerOps.ExtractItemsByIndex(existing, indices);
            var gr = ContainerOps.AddItemsJsonToGame(player, extracted, capacity, allowOvercap, targetField);
            res.Succeeded = gr.Succeeded;
            res.Failed    = gr.Failed;
            res.OverCap   = gr.OverCap;
            if (removeFromContainer && gr.Succeeded > 0)
            {
                var sortedIndices = new List<int>(indices);
                sortedIndices.Sort();
                var toRemove = new HashSet<int>();
                for (int k = 0; k < gr.Succeeded && k < sortedIndices.Count; k++) toRemove.Add(sortedIndices[k]);
                string remaining = ContainerOps.RemoveItemsByIndex(existing, toRemove);
                _repo.SaveItemsJson(CurrentContainerIndex, remaining);
            }
            res.Reason = gr.Reason ?? "";
        }
        catch (System.Exception ex)
        {
            res.Reason = $"ContainerToTarget({targetField}) threw: {ex.Message}";
            Logger.Warn(res.Reason);
        }
        return res;
    }
```

- [ ] **Step 3: build (ModWindow.cs 의 ContainerToGame 호출 site 때문에 FAIL 예상)**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `error CS1061: 'ContainerOpsHelper' does not contain a definition for 'ContainerToGame'`. Phase 7 에서 fix.

- [ ] **Step 4: commit (빌드 여전히 깨짐)**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/Containers/ContainerOpsHelper.cs
git -C "Save/_PlayerExport" commit -m "feat(containers): v0.7.1 ContainerToInventory/Storage 분리, Result.OverCap"
```

---

## Phase 6: ContainerPanel UI 변경

### Task 6.1: SetInventoryRows / SetStorageRows signature — capacity 인자 추가

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:38-43,77-79`

- [ ] **Step 1: capacity 저장 field 추가**

`src/LongYinRoster/UI/ContainerPanel.cs:38-43` 의 row field 들 다음에 capacity field 추가:

```csharp
    private List<ItemRow> _inventoryRows = new();
    private List<ItemRow> _storageRows   = new();
    private List<ItemRow> _containerRows = new();
    private HashSet<int>  _inventoryChecks = new();
    private HashSet<int>  _storageChecks   = new();
    private HashSet<int>  _containerChecks = new();
    private int           _inventoryCapacity = 171;   // v0.7.1
    private int           _storageCapacity   = 217;   // v0.7.1
```

- [ ] **Step 2: SetInventoryRows / SetStorageRows signature 변경**

`src/LongYinRoster/UI/ContainerPanel.cs:77-79` 를 다음으로 교체:

```csharp
    public void SetInventoryRows(List<ItemRow> rows, int capacity = 171) { _inventoryRows = rows; _inventoryChecks.Clear(); _inventoryCapacity = capacity; }
    public void SetStorageRows  (List<ItemRow> rows, int capacity = 217) { _storageRows   = rows; _storageChecks.Clear();   _storageCapacity   = capacity; }
    public void SetContainerRows(List<ItemRow> rows) { _containerRows = rows; _containerChecks.Clear(); }
```

- [ ] **Step 3: build (ModWindow 호출 site 와 default 인자 호환되므로 통과 예상)**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `error CS1061: 'ContainerOpsHelper' does not contain ContainerToGame` 만 남음 (Phase 5 와 동일).

### Task 6.2: 신규 callback 2개 추가

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:62-69`

- [ ] **Step 1: callback delegate 추가**

`src/LongYinRoster/UI/ContainerPanel.cs:67` 의 `OnContainerToInventoryCopy` 다음에 추가:

```csharp
    public Action<HashSet<int>>? OnContainerToInventoryMove;
    public Action<HashSet<int>>? OnContainerToInventoryCopy;
    public Action<HashSet<int>>? OnContainerToStorageMove;     // v0.7.1
    public Action<HashSet<int>>? OnContainerToStorageCopy;     // v0.7.1
    public Action<HashSet<int>>? OnContainerDelete;
```

(기존 `OnContainerDelete` line 은 그대로 유지.)

- [ ] **Step 2: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 여전히 ContainerToGame missing error 만 남음.

### Task 6.3: FormatCount helper 추가

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:289-296`

- [ ] **Step 1: BuildLabel static method 다음에 FormatCount 추가**

`src/LongYinRoster/UI/ContainerPanel.cs:296` 의 `}` (BuildLabel 닫는 괄호) 직후에 추가:

```csharp

    /// <summary>
    /// "라벨 (cur / max 개)" + 인벤 over-cap 시 마커 ⚠ 초과.
    /// </summary>
    private static string FormatCount(string label, int cur, int max, bool allowOvercap)
    {
        string s = $"{label} ({cur} / {max} 개)";
        if (allowOvercap && cur > max) s += KoreanStrings.Lbl_OvercapMarker;
        return s;
    }
```

- [ ] **Step 2: KoreanStrings using 확인**

`src/LongYinRoster/UI/ContainerPanel.cs:5` 의 using block 에 다음이 없으면 추가:

```csharp
using LongYinRoster.Util;
```

(기존 using 들 확인 — `LongYinRoster.Containers` 와 `UnityEngine` 외에 `LongYinRoster.Util` 도 필요.)

- [ ] **Step 3: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: ContainerToGame missing error 만 남음.

### Task 6.4: DrawLeftColumn 라벨 교체 (FormatCount 사용)

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:159-182`

- [ ] **Step 1: DrawLeftColumn 본체 교체**

`src/LongYinRoster/UI/ContainerPanel.cs:159-182` 의 method 본체를 다음으로 교체:

```csharp
    private void DrawLeftColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));
        _leftColumnScroll = GUILayout.BeginScrollView(_leftColumnScroll, GUILayout.Height(640));

        GUILayout.Label(FormatCount(KoreanStrings.Lbl_Inventory, _inventoryRows.Count, _inventoryCapacity, allowOvercap: true));
        DrawItemList(_inventoryRows, _inventoryChecks, ref _invScroll, 220);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnInventoryToContainerMove?.Invoke(new HashSet<int>(_inventoryChecks));
        if (GUILayout.Button("→ 복사")) OnInventoryToContainerCopy?.Invoke(new HashSet<int>(_inventoryChecks));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label(FormatCount(KoreanStrings.Lbl_Storage, _storageRows.Count, _storageCapacity, allowOvercap: false));
        DrawItemList(_storageRows, _storageChecks, ref _stoScroll, 220);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnStorageToContainerMove?.Invoke(new HashSet<int>(_storageChecks));
        if (GUILayout.Button("→ 복사")) OnStorageToContainerCopy?.Invoke(new HashSet<int>(_storageChecks));
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }
```

- [ ] **Step 2: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: ContainerToGame missing error 만 남음.

### Task 6.5: DrawRightColumn 우측 4-callback 버튼 layout

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:265-271`

- [ ] **Step 1: 우측 컨테이너 list 아래 버튼 row 교체**

`src/LongYinRoster/UI/ContainerPanel.cs:265-271` 의 다음 5 줄:

```csharp
        GUILayout.Label($"컨테이너 ({_containerRows.Count}개)");
        DrawItemList(_containerRows, _containerChecks, ref _conScroll, 420);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("← 이동")) OnContainerToInventoryMove?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button("← 복사")) OnContainerToInventoryCopy?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button("☓ 삭제")) OnContainerDelete?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();
```

을 다음으로 교체:

```csharp
        GUILayout.Label($"{KoreanStrings.Lbl_Container} ({_containerRows.Count}개)");
        DrawItemList(_containerRows, _containerChecks, ref _conScroll, 360);

        // v0.7.1: destination 별 4 버튼 (좌측 column mirror) + 삭제
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(KoreanStrings.BtnInvMove)) OnContainerToInventoryMove?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button(KoreanStrings.BtnInvCopy)) OnContainerToInventoryCopy?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(KoreanStrings.BtnStoMove)) OnContainerToStorageMove?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button(KoreanStrings.BtnStoCopy)) OnContainerToStorageCopy?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("☓ 삭제")) OnContainerDelete?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();
```

- [ ] **Step 2: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 여전히 ContainerToGame missing error 만 (ModWindow.cs).

- [ ] **Step 3: commit (Phase 4·5·6 누적, ModWindow Phase 7 에서 fix 예정)**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/UI/ContainerPanel.cs
git -C "Save/_PlayerExport" commit -m "feat(ui): v0.7.1 ContainerPanel — capacity 라벨 + 대칭 mirror 4-callback layout"
```

---

## Phase 7: ModWindow wiring 변경

### Task 7.1: DoContainerToGame 분리 + 4 callback wiring

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs:118-129,165-168,218-226`

- [ ] **Step 1: callback wiring 4-method 로 교체**

`src/LongYinRoster/UI/ModWindow.cs:122-123` 의 두 줄:

```csharp
        _containerPanel.OnContainerToInventoryMove = checks => DoContainerToGame(checks, removeFromContainer: true);
        _containerPanel.OnContainerToInventoryCopy = checks => DoContainerToGame(checks, removeFromContainer: false);
```

을 다음으로 교체 (4 callback):

```csharp
        _containerPanel.OnContainerToInventoryMove = checks => DoContainerToInventory(checks, removeFromContainer: true);
        _containerPanel.OnContainerToInventoryCopy = checks => DoContainerToInventory(checks, removeFromContainer: false);
        _containerPanel.OnContainerToStorageMove   = checks => DoContainerToStorage  (checks, removeFromContainer: true);
        _containerPanel.OnContainerToStorageCopy   = checks => DoContainerToStorage  (checks, removeFromContainer: false);
```

- [ ] **Step 2: RefreshAllContainerRows 에 capacity 인자 전달**

`src/LongYinRoster/UI/ModWindow.cs:163-170` 의 method 본체를 다음으로 교체:

```csharp
    private void RefreshAllContainerRows()
    {
        var inv = GetPlayerInventoryList();
        var sto = GetPlayerStorageList();
        var ild = inv != null ? GetPlayerItemListData() : null;     // capacity helper 용
        var ssd = sto != null ? GetPlayerSelfStorage()  : null;
        int invCap = Core.ItemListReflector.GetCapacity(ild, ModCfg.InventoryCapacity.Value);
        int stoCap = Core.ItemListReflector.GetCapacity(ssd, ModCfg.StorageCapacity.Value);
        _containerPanel.SetInventoryRows(inv != null ? ContainerRowBuilder.FromGameAllItem(inv) : new List<ContainerPanel.ItemRow>(), invCap);
        _containerPanel.SetStorageRows  (sto != null ? ContainerRowBuilder.FromGameAllItem(sto) : new List<ContainerPanel.ItemRow>(), stoCap);
        RefreshContainerRows();
    }

    private object? GetPlayerItemListData()
    {
        var p = Core.HeroLocator.GetPlayer();
        return p != null ? ReadFieldOrProperty(p, "itemListData") : null;
    }

    private object? GetPlayerSelfStorage()
    {
        var p = Core.HeroLocator.GetPlayer();
        return p != null ? ReadFieldOrProperty(p, "selfStorage") : null;
    }
```

- [ ] **Step 3: DoContainerToGame 분리**

`src/LongYinRoster/UI/ModWindow.cs:218-226` 의 `DoContainerToGame` method 전체를 다음 두 method 로 교체:

```csharp
    private void DoContainerToInventory(HashSet<int> checks, bool removeFromContainer)
    {
        if (_containerOps == null) return;
        var p = Core.HeroLocator.GetPlayer();
        if (p == null) { _containerPanel.Toast(KoreanStrings.ToastContainerNeedGameEnter); return; }
        int cap = Core.ItemListReflector.GetCapacity(GetPlayerItemListData(), ModCfg.InventoryCapacity.Value);
        var r = _containerOps.ContainerToInventory(p, checks, removeFromContainer, cap);
        if (!string.IsNullOrEmpty(r.Reason) && r.Succeeded == 0)
        {
            _containerPanel.Toast(r.Reason);
        }
        else if (r.OverCap > 0)
        {
            // 인벤 over-cap 발생 — 현재 finalCount = cur + Succeeded, OverCap = finalCount - max
            int curAfter = (GetPlayerInventoryList() is { } l ? Core.IL2CppListOps.Count(l) : cap + r.OverCap);
            _containerPanel.Toast(string.Format(KoreanStrings.ToastInvOvercap, r.Succeeded, curAfter, cap));
        }
        else
        {
            _containerPanel.Toast(string.Format(KoreanStrings.ToastInvOk, r.Succeeded));
        }
        RefreshAllContainerRows();
    }

    private void DoContainerToStorage(HashSet<int> checks, bool removeFromContainer)
    {
        if (_containerOps == null) return;
        var p = Core.HeroLocator.GetPlayer();
        if (p == null) { _containerPanel.Toast(KoreanStrings.ToastContainerNeedGameEnter); return; }
        int cap = Core.ItemListReflector.GetCapacity(GetPlayerSelfStorage(), ModCfg.StorageCapacity.Value);
        var r = _containerOps.ContainerToStorage(p, checks, removeFromContainer, cap);
        if (!string.IsNullOrEmpty(r.Reason) && r.Succeeded == 0 && r.Failed == 0)
        {
            _containerPanel.Toast(r.Reason);
        }
        else if (r.Succeeded == 0 && r.Failed > 0)
        {
            _containerPanel.Toast(KoreanStrings.ToastStoFull);
        }
        else if (r.Failed > 0)
        {
            _containerPanel.Toast(string.Format(KoreanStrings.ToastStoPartial, r.Succeeded, r.Failed));
        }
        else
        {
            _containerPanel.Toast(string.Format(KoreanStrings.ToastStoOk, r.Succeeded));
        }
        RefreshAllContainerRows();
    }
```

- [ ] **Step 4: build (이번엔 통과해야 함)**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`. (warnings 0 또는 기존 warnings 만)

- [ ] **Step 5: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/UI/ModWindow.cs
git -C "Save/_PlayerExport" commit -m "feat(ui): v0.7.1 ModWindow — DoContainerToInventory/Storage 분리 + capacity wiring"
```

---

## Phase 8: 단위 테스트

### Task 8.1: ContainerOpsHelperGuardTests 의 ContainerToGame → ContainerToInventory rename

**Files:**
- Modify: `src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs:46-56`

- [ ] **Step 1: ContainerToGame 호출 site 두 곳 rename**

`src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs:53` 의:

```csharp
        var result = helper.ContainerToGame(player: new object(), indices: new HashSet<int> { 0 }, removeFromContainer: false);
```

을 다음으로 교체 (두 destination 모두 같은 가드 동작이라 ContainerToInventory 만 검증해도 충분):

```csharp
        var result = helper.ContainerToInventory(player: new object(), indices: new HashSet<int> { 0 }, removeFromContainer: false, capacity: 171);
```

- [ ] **Step 2: 테스트 실행**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerOpsHelperGuardTests"
```

Expected: 6/6 PASS.

- [ ] **Step 3: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs
git -C "Save/_PlayerExport" commit -m "test(containers): v0.7.1 ContainerToGame → ContainerToInventory rename"
```

### Task 8.2: ContainerOpsHelper destination 분리 동작 추가 테스트

**Files:**
- Modify: `src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs:끝`

- [ ] **Step 1: 새 test 두 개 추가**

`src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs` 의 마지막 `}` (class 닫는 괄호) 직전에 추가:

```csharp

    [Fact]
    public void ContainerToInventory_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);

        var result = helper.ContainerToInventory(player: new object(), indices: new HashSet<int> { 0 }, removeFromContainer: false, capacity: 171);

        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void ContainerToStorage_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);

        var result = helper.ContainerToStorage(player: new object(), indices: new HashSet<int> { 0 }, removeFromContainer: false, capacity: 217);

        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void ContainerToInventory_EmptyChecks_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        helper.CurrentContainerIndex = repo.CreateNew("test");

        var result = helper.ContainerToInventory(player: new object(), indices: new HashSet<int>(), removeFromContainer: false, capacity: 171);

        Assert.Equal("선택된 항목 없음", result.Reason);
    }
```

- [ ] **Step 2: 테스트 실행**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerOpsHelperGuardTests"
```

Expected: 9/9 PASS.

- [ ] **Step 3: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs
git -C "Save/_PlayerExport" commit -m "test(containers): v0.7.1 ContainerToInventory/Storage 가드 테스트"
```

### Task 8.3: ContainerPanelFormatTests 신규

**Files:**
- Create: `src/LongYinRoster.Tests/ContainerPanelFormatTests.cs`

이 테스트는 ContainerPanel 의 private static `FormatCount` 를 직접 호출 못하므로 reflection 으로 우회하거나, FormatCount 를 internal helper class 로 분리하는 두 옵션이 있다. **단순 우회 (internal 노출) 선택** — `[InternalsVisibleTo("LongYinRoster.Tests")]` 가 이미 있으면 internal 로 충분.

- [ ] **Step 1: ContainerPanel 의 FormatCount 를 internal 로 변경**

`src/LongYinRoster/UI/ContainerPanel.cs` 의 `FormatCount` (Task 6.3 에서 추가한 method) 의 `private static` 을 `internal static` 으로 변경:

```csharp
    internal static string FormatCount(string label, int cur, int max, bool allowOvercap)
```

- [ ] **Step 2: AssemblyInfo 또는 csproj 에 InternalsVisibleTo 확인**

```bash
grep -n "InternalsVisibleTo" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/LongYinRoster.csproj"
```

없으면 csproj 의 `<PropertyGroup>` 안에 (또는 별도 ItemGroup):

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="LongYinRoster.Tests" />
  </ItemGroup>
```

- [ ] **Step 3: 테스트 파일 작성**

`src/LongYinRoster.Tests/ContainerPanelFormatTests.cs`:

```csharp
using LongYinRoster.UI;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerPanelFormatTests
{
    [Fact]
    public void FormatCount_NormalInventory_NoMarker()
    {
        string s = ContainerPanel.FormatCount("인벤토리", 45, 171, allowOvercap: true);
        Assert.Equal("인벤토리 (45 / 171 개)", s);
    }

    [Fact]
    public void FormatCount_OvercapInventory_AppendsMarker()
    {
        string s = ContainerPanel.FormatCount("인벤토리", 175, 171, allowOvercap: true);
        Assert.Equal("인벤토리 (175 / 171 개) ⚠ 초과", s);
    }

    [Fact]
    public void FormatCount_StorageNoMarkerEvenIfNumericallyOver()
    {
        // allowOvercap=false 면 cur > max 라도 마커 미부착 (창고 hard cap → 정상 시나리오에서 발생 안 함)
        string s = ContainerPanel.FormatCount("창고", 220, 217, allowOvercap: false);
        Assert.Equal("창고 (220 / 217 개)", s);
    }

    [Fact]
    public void FormatCount_AtCapacity_NoMarker()
    {
        string s = ContainerPanel.FormatCount("인벤토리", 171, 171, allowOvercap: true);
        Assert.Equal("인벤토리 (171 / 171 개)", s);
    }
}
```

- [ ] **Step 4: 테스트 실행**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerPanelFormatTests"
```

Expected: 4/4 PASS.

- [ ] **Step 5: 전체 테스트 실행 — 회귀 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 기존 45 + Phase 8 신규 (3 + 4) = 52/52 PASS.

- [ ] **Step 6: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster.Tests/ContainerPanelFormatTests.cs src/LongYinRoster/UI/ContainerPanel.cs src/LongYinRoster/LongYinRoster.csproj
git -C "Save/_PlayerExport" commit -m "test(ui): v0.7.1 ContainerPanelFormatTests + InternalsVisibleTo"
```

---

## Phase 9: 인게임 smoke

### Task 9.1: F12 capacity probe 핸들러 cleanup

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs:665-672`

- [ ] **Step 1: Shift+F12 분기 제거 (Phase 1 의 임시 핸들러 원위치)**

`src/LongYinRoster/UI/ModWindow.cs:665-672` 의 if-else 블록을 다음 한 줄로 회귀:

```csharp
        if (Input.GetKeyDown(KeyCode.F12)) Core.Probes.ProbeRunner.Trigger();
```

- [ ] **Step 2: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 3: commit**

```bash
git -C "Save/_PlayerExport" add src/LongYinRoster/UI/ModWindow.cs
git -C "Save/_PlayerExport" commit -m "chore(ui): v0.7.1 release 전 Shift+F12 capacity probe handler 제거"
```

### Task 9.2: 인게임 smoke 6항목 사용자 검증

- [ ] **Step 1: 게임 종료 + DLL 재배포 확인**

```bash
ls -la "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/LongYinRoster.dll"
```

Expected: 최신 build 시각 (방금 build 한 시각).

- [ ] **Step 2: BepInEx 로그 클리어**

```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

- [ ] **Step 3: 사용자 검증 6 항목 (인게임)**

사용자에게 다음 항목 인게임 확인 요청:

1. **인벤토리 라벨**: ContainerPanel 좌상단에 `"인벤토리 (N / 171 개)"` 류 표시
2. **인벤토리 over-cap 마커**: 인벤 가득 찬 상태에서 컨테이너→인벤 [이동/복사] 후 라벨이 `"인벤토리 (175 / 171 개) ⚠ 초과"` 형태로 변함
3. **창고 라벨**: 좌측 column 창고 측에 `"창고 (N / 217 개)"` 표시
4. **컨테이너→인벤 4-button**: `← 인벤으로 이동` `← 인벤으로 복사` 동작. over-cap 시 toast `"인벤토리로 N개 처리 (X/171 초과 — 이동속도 저하)"` 표시
5. **컨테이너→창고 4-button**: `← 창고로 이동` `← 창고로 복사` 동작. 가득 찬 상태에서 클릭 시 toast `"창고 가득 참 — 처리 불가"`. partial 시 `"창고로 N개 처리 (K개는 컨테이너에 남김)"`
6. **회귀**: v0.7.0.1 의 컨테이너 신규/이름변경/삭제 + 인벤/창고→컨테이너 [→이동/복사] 동작 정상 (사라지거나 NRE 없음)

- [ ] **Step 4: smoke 결과 기록**

`docs/superpowers/dumps/2026-05-03-v0.7.1-smoke-results.md` 작성:

```markdown
# v0.7.1 Smoke Results

**일시**: 2026-05-03
**Build**: commit (Phase 9.1 commit hash)
**검증자**: 사용자

## 6 항목 결과
1. 인벤토리 라벨 (N/MAX): PASS / FAIL — 관찰값:
2. 인벤 over-cap 마커: PASS / FAIL — 관찰값:
3. 창고 라벨 (N/MAX): PASS / FAIL — 관찰값:
4. 컨테이너→인벤 4-button + over-cap toast: PASS / FAIL — 관찰값:
5. 컨테이너→창고 4-button + 가득 거절 / partial toast: PASS / FAIL — 관찰값:
6. 회귀 (v0.7.0.1 컨테이너 동작): PASS / FAIL — 관찰값:

## 결론
모두 PASS 시 → release 진행. 일부 FAIL 시 → 해당 phase 로 회귀해 fix.
```

- [ ] **Step 5: smoke commit**

```bash
git -C "Save/_PlayerExport" add docs/superpowers/dumps/2026-05-03-v0.7.1-smoke-results.md
git -C "Save/_PlayerExport" commit -m "docs(smoke): v0.7.1 인게임 검증 결과"
```

---

## Phase 10: Release

### Task 10.1: VERSION bump

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs:17`

- [ ] **Step 1: VERSION 0.7.0.1 → 0.7.1**

`src/LongYinRoster/Plugin.cs:17` 의:

```csharp
    public const string VERSION = "0.7.0.1";
```

을 다음으로 교체:

```csharp
    public const string VERSION = "0.7.1";
```

- [ ] **Step 2: build**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`. BepInEx 로그에 `Loaded LongYin Roster Mod v0.7.1` 출력될 것.

### Task 10.2: HANDOFF.md 업데이트

**Files:**
- Modify: `docs/HANDOFF.md`

- [ ] **Step 1: §1 헤더 갱신**

`docs/HANDOFF.md` 의 line 4 (`**진행 상태**`) 를 다음으로 교체:

```markdown
**진행 상태**: **v0.7.1 release** — 컨테이너 UX 1차 (대칭 mirror 4-callback / capacity 표시 (N/MAX) / destination 별 가드 (인벤 over-cap 허용+마커, 창고 hard cap+거절 toast)).
```

- [ ] **Step 2: Releases list 에 v0.7.1 추가**

`docs/HANDOFF.md` 의 v0.7.0.1 line 다음에 추가:

```markdown
- [v0.7.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.1) — 컨테이너 UX 1차: destination 명시 (대칭 mirror 4-callback) / capacity 표시 (N/MAX + 인벤 over-cap ⚠ 초과 마커) / 가드 (인벤 over-cap 허용+속도페널티 안내, 창고 hard cap+거절 toast)
```

- [ ] **Step 3: §1 sub-project 번호 재정렬**

`docs/HANDOFF.md` 의 §1 sub-project 7 줄 (v0.7.1~v0.7.5 list) 를 다음으로 교체:

```markdown
- v0.7.2: D-1 컨테이너 UX 2차 — Item 상세 panel (선택 item reflection 표시)
- v0.7.3: D-2 컨테이너 UX 3차 — 아이콘 그리드 (sprite reference + IMGUI grid)
- v0.7.4: D-3 컨테이너 UX 4차 — 검색·정렬
- v0.7.5: 설정 panel — hotkey 변경 / 컨테이너 정원 / 창 크기 조정
- v0.7.6: Apply 부분 미리보기 — 선택한 카테고리 적용 시 전후 비교
- v0.7.7: Slot diff preview — Apply 전 어떤 필드가 바뀔지 미리보기 (스탯/장비/무공 차이 시각화)
- v0.7.8: NPC 지원 — 캐릭터 선택 + apply target 확장 (heroID=0 외 다른 캐릭터)
```

- [ ] **Step 4: Known Limitations 섹션 갱신**

`docs/HANDOFF.md` 의 `## v0.7.0.1 Known Limitations` 헤더를 `## v0.7.1 Known Limitations` 로 변경. 컨테이너 관련 limitation 5번째 항목 (v0.7.1 sub-project) 을 다음으로 변경:

```markdown
- **컨테이너 Item 상세 / 아이콘 그리드 / 검색·정렬** 은 v0.7.2 / v0.7.3 / v0.7.4 (D-1/2/3) sub-project 에서 처리.
```

- [ ] **Step 5: commit**

```bash
git -C "Save/_PlayerExport" add docs/HANDOFF.md src/LongYinRoster/Plugin.cs
git -C "Save/_PlayerExport" commit -m "chore(release): v0.7.1 — VERSION bump + HANDOFF 업데이트 (sub-project 재정렬)"
```

### Task 10.3: dist 패키징 + GitHub release

- [ ] **Step 1: dist 디렉토리 준비**

```bash
mkdir -p "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/dist/LongYinRoster_v0.7.1"
cp "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/LongYinRoster.dll" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/dist/LongYinRoster_v0.7.1/"
cp "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/README.md" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/dist/LongYinRoster_v0.7.1/"
```

Expected: 두 파일 복사됨.

- [ ] **Step 2: zip 패키징 (PowerShell)**

```powershell
Compress-Archive -Path "E:\Games\龙胤立志传.v1.0.0f8.2\LongYinLiZhiZhuan\Save\_PlayerExport\dist\LongYinRoster_v0.7.1\*" -DestinationPath "E:\Games\龙胤立志传.v1.0.0f8.2\LongYinLiZhiZhuan\Save\_PlayerExport\dist\LongYinRoster_v0.7.1.zip" -Force
```

- [ ] **Step 3: release notes 작성**

`dist/release-notes-v0.7.1.md`:

```markdown
# v0.7.1 — 컨테이너 UX 1차

## 변경
- **destination 명시**: 컨테이너→게임 버튼이 `← 인벤으로 이동/복사` `← 창고로 이동/복사` 4개로 분리 (좌측 column mirror)
- **capacity 표시**: 인벤/창고 라벨에 `(N / MAX 개)` 분수 표기. 인벤 over-cap 시 ⚠ 초과 마커
- **가드**:
  - 인벤: over-cap 허용 (속도 페널티). 발생 시 toast `"인벤토리로 N개 처리 (X/MAX 초과 — 이동속도 저하)"`
  - 창고: hard cap. 가득 시 toast `"창고 가득 참 — 처리 불가"`. partial 시 `"창고로 N개 처리 (K개는 컨테이너에 남김)"`
- **capacity source**: spike 결과에 따라 reflection 우선, 미발견 시 BepInEx Config (`Container.InventoryCapacity`, `Container.StorageCapacity`) 로 fallback

## 호환
- v0.7.0 / v0.7.0.1 슬롯·컨테이너 파일 무손실
- 게임 v1.0.0 f8.2 검증

## 다음
v0.7.2 (D-1 Item 상세) → v0.7.3 (D-2 아이콘 그리드) → v0.7.4 (D-3 검색·정렬)
```

- [ ] **Step 4: GitHub release create**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git tag v0.7.1
git push origin v0.7.1
gh release create v0.7.1 dist/LongYinRoster_v0.7.1.zip \
  --title "v0.7.1 — 컨테이너 UX 1차 (destination + capacity + 가드)" \
  --notes-file dist/release-notes-v0.7.1.md
```

Expected: release URL 출력.

- [ ] **Step 5: dist 변경 commit**

```bash
git -C "Save/_PlayerExport" add dist/release-notes-v0.7.1.md
git -C "Save/_PlayerExport" commit -m "chore(release): v0.7.1 release notes"
git -C "Save/_PlayerExport" push origin main
```

---

## Self-Review 체크 결과

스펙 §4·§5·§6·§7·§8 의 모든 결정사항이 plan task 로 매핑됨:
- **Task A 대칭 mirror UI** → Task 6.5 (DrawRightColumn) + Task 7.1 (ModWindow callback wiring)
- **Task B capacity 표시 (B-2)** → Task 1.1~1.2 spike, 2.1~2.2 helper, 3.1 KoreanStrings, 6.1 SetRows signature, 6.3 FormatCount, 6.4 DrawLeftColumn, 7.1 RefreshAllContainerRows
- **Task C destination 별 가드** → Task 4.2 AddItemsJsonToGame allowOvercap, 5.1 ContainerToInventory/Storage 분리, 7.1 toast 분기
- **신규 callback** → Task 6.2
- **Result.OverCap** → Task 4.1 + 5.1
- **단위 테스트** → Phase 8
- **인게임 smoke 6항목** → Task 9.2
- **VERSION bump + HANDOFF + release** → Phase 10

---

Plan complete and saved to [`docs/superpowers/plans/2026-05-03-longyin-roster-mod-v0.7.1-plan.md`](Save/_PlayerExport/docs/superpowers/plans/2026-05-03-longyin-roster-mod-v0.7.1-plan.md).

Two execution options:

**1. Subagent-Driven (recommended)** — Fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
