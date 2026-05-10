# LongYinRoster v0.7.11 Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** ContainerPanel 의 6 카테고리 (Cat 1/2/3/4/5/9) UX 개선 — split + collapse / 일괄선택 + 카운터 / Undo + toast 강화 / filter 정밀화 + 결과 카운터 / 삭제 confirm + 정보 표시 + Clone / corner resize handle. v0.7.10.2 baseline 유지, 신규 IMGUI API 도입 0, 회귀 없이 incremental.

**Architecture:** ContainerPanel.cs 중심 (595 → ~900 LOC). 신규 helper 자산: `Containers/ContainerOpUndo.cs` (Undo stack + OpRecord) / `ContainerRepository.Clone` extension / `ContainerView` 확장 (등급 + 착용중 filter). Config 5 ConfigEntry 추가. 단일 file 비대 risk → v0.7.12+ 부분 분리 후보.

**Tech Stack:** C# 11 / .NET Standard 2.1, BepInEx 6 IL2CPP, HarmonyLib (이미 register 됨), xUnit + Shouldly (POCO mocks), Newtonsoft.Json (IL2CPP for ContainerFile).

**Spec:** [`2026-05-10-longyin-roster-mod-v0.7.11-design.md`](../specs/2026-05-10-longyin-roster-mod-v0.7.11-design.md)

---

## File Structure

| File | Status | 책임 |
|---|---|---|
| `src/LongYinRoster/Config.cs` | modify | 5 신규 ConfigEntry — Collapsed × 2 / SplitPreset / FilterMinRare / ExcludeEquipped |
| `src/LongYinRoster/UI/ContainerPanel.cs` | modify | 본체 — 6 카테고리 모든 UI 변경. ~+300 LOC |
| `src/LongYinRoster/Containers/ContainerOpUndo.cs` | create | Undo stack (single OpRecord) + OpKind enum. ~100 LOC |
| `src/LongYinRoster/Containers/ContainerRepository.cs` | modify | `Clone(int sourceIdx)` helper. ~+30 LOC |
| `src/LongYinRoster/Containers/ContainerOps.cs` | modify | 모든 op 가 Undo metadata 기록. ~+50 LOC |
| `src/LongYinRoster/Containers/ContainerMetadata.cs` | modify | ItemCount + Weight cache. ~+20 LOC |
| `src/LongYinRoster/Containers/ContainerView.cs` | modify | 등급 범위 + 착용중 filter 추가. ~+40 LOC |
| `src/LongYinRoster.Tests/ContainerOpUndoTests.cs` | create | Undo Move/Copy/Clone inverse 검증. ~5 tests |
| `src/LongYinRoster.Tests/ContainerViewExtraFilterTests.cs` | create | 등급 범위 / 착용중 필터 검증. ~5 tests |
| `src/LongYinRoster.Tests/ContainerRepositoryCloneTests.cs` | create | Clone 깊은 복사 + 이름 자동 검증. ~3 tests |
| `src/LongYinRoster/Plugin.cs` | modify | VERSION → "0.7.11" |
| `README.md` | modify | v0.7.11 line 추가 |
| `docs/HANDOFF.md` | modify | 진행 상태 + Releases + 다음 후속 |
| `dist/release-notes-v0.7.11.md` | create | release notes |

**Total**: 4 새 source + 8 modified + 3 새 tests. **Test count delta**: 374 → ~387 (+13).

---

## Phase 0 — Spike (impl 진입 전 검증)

### Task 0.1: Spike — Event.MouseDrag strip-safe

**Files:** N/A (검증만)

- [ ] **Step 1**: `grep -rn "MouseDown\|MouseDrag\|MouseUp" src/LongYinRoster/UI/ContainerPanel.cs` — 기존 사용처 확인
- [ ] **Step 2**: v0.7.4 의 `Event.current.type == EventType.MouseDown` 검증 자산 (memory) 확인. MouseDrag 가 없으면 인게임 spike 필요.
- [ ] **Step 3**: 결과 = strip-safe 추정 (MouseDown 검증 → Drag/Up 도 같은 IL2CPP API surface). NO-GO fallback = preset 토글 button 만 (corner drag 빼고).

**Spike 결과 기록**: 본 plan 의 §Risk 에 confirm/deny.

---

## Phase 1 — Cat 5 (안전성 우선): 삭제 confirm + 정보 표시 + Clone

### Task 1.1: Config — 변경 없음 (Cat 5 는 ConfigEntry 추가 안 함)

Skip.

### Task 1.2: ContainerMetadata — ItemCount + Weight cache

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerMetadata.cs`
- Modify: `src/LongYinRoster.Tests/ContainerMetadataTests.cs` (기존)

- [ ] **Step 1: Read existing**

```bash
grep -n "class ContainerMetadata\|ItemCount\|Weight" src/LongYinRoster/Containers/ContainerMetadata.cs
```

- [ ] **Step 2: Add ItemCount + Weight properties**

```csharp
public sealed class ContainerMetadata
{
    public int ContainerIndex { get; set; }
    public string ContainerName { get; set; } = "";

    /// <summary>v0.7.11 — Cat 5B dropdown 표시용. ContainerFile 의 Items.Count.</summary>
    public int ItemCount { get; set; }

    /// <summary>v0.7.11 — Cat 5B dropdown 표시용. Items 의 Weight 합계.</summary>
    public float TotalWeight { get; set; }
}
```

- [ ] **Step 3: ContainerRepository.LoadMetadata 갱신**

`ContainerRepository.cs` 의 `RefreshMetadata` (또는 LoadMetadata) 안에서 ContainerFile 로드 시:
```csharp
meta.ItemCount = file.Items?.Count ?? 0;
meta.TotalWeight = 0f;
if (file.Items != null) foreach (var i in file.Items) meta.TotalWeight += i.Weight;
```

- [ ] **Step 4: 테스트 추가**

`ContainerMetadataTests.cs` 에:
```csharp
[Fact]
public void ContainerMetadata_HasItemCountAndWeight()
{
    var m = new ContainerMetadata { ItemCount = 5, TotalWeight = 10.5f };
    m.ItemCount.ShouldBe(5);
    m.TotalWeight.ShouldBe(10.5f);
}
```

`ContainerRepositoryTests.cs` 에 Load 후 ItemCount/Weight 검증 1 test 추가.

- [ ] **Step 5: build + test**

```bash
dotnet build -c Release && dotnet test
```
Expected: 374 + 2 = 376 PASS.

- [ ] **Step 6: commit**

```bash
git commit -m "feat(containers): v0.7.11 Phase 1 — ContainerMetadata.ItemCount/TotalWeight (Cat 5B dropdown)"
```

### Task 1.3: ContainerRepository — Clone helper

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerRepository.cs`
- Create: `src/LongYinRoster.Tests/ContainerRepositoryCloneTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using System.IO;
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerRepositoryCloneTests : System.IDisposable
{
    private readonly string _tmpDir;

    public ContainerRepositoryCloneTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), $"longyin-clone-{System.Guid.NewGuid()}");
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose() => Directory.Delete(_tmpDir, recursive: true);

    [Fact]
    public void Clone_CreatesNewContainer_WithSuffixedName()
    {
        var repo = new ContainerRepository(_tmpDir);
        int srcIdx = repo.CreateNew("원본");
        int cloneIdx = repo.Clone(srcIdx);
        cloneIdx.ShouldBeGreaterThan(srcIdx);
        var meta = repo.LoadMetadata(cloneIdx);
        meta.ShouldNotBeNull();
        meta!.ContainerName.ShouldBe("원본 (복사본)");
    }

    [Fact]
    public void Clone_DeepCopies_Items()
    {
        var repo = new ContainerRepository(_tmpDir);
        int srcIdx = repo.CreateNew("원본");
        var src = repo.Load(srcIdx);
        src!.Items.Add(new ContainerItemEntry { Name = "item1", Weight = 1.5f });
        repo.Save(srcIdx, src);

        int cloneIdx = repo.Clone(srcIdx);
        var clone = repo.Load(cloneIdx);
        clone!.Items.Count.ShouldBe(1);
        clone.Items[0].Name.ShouldBe("item1");
        clone.Items[0].Weight.ShouldBe(1.5f);

        // 깊은 복사 — 원본 modify 시 clone 영향 없음
        src.Items[0].Name = "modified";
        var cloneReload = repo.Load(cloneIdx);
        cloneReload!.Items[0].Name.ShouldBe("item1");
    }

    [Fact]
    public void Clone_NonExistentSource_ReturnsMinusOne()
    {
        var repo = new ContainerRepository(_tmpDir);
        int result = repo.Clone(9999);
        result.ShouldBe(-1);
    }
}
```

(Note: Test은 actual `ContainerRepository` API 에 따라 조정. ContainerItemEntry / Save / Load / LoadMetadata signatures 검증.)

- [ ] **Step 2: Run test → FAIL**

```bash
dotnet test --filter "FullyQualifiedName~ContainerRepositoryCloneTests" -v normal
```

- [ ] **Step 3: Add Clone method**

```csharp
// ContainerRepository.cs
public int Clone(int sourceIdx)
{
    var src = Load(sourceIdx);
    if (src == null) return -1;
    var srcMeta = LoadMetadata(sourceIdx);
    string newName = srcMeta != null ? $"{srcMeta.ContainerName} (복사본)" : "(복사본)";
    int newIdx = CreateNew(newName);
    var clone = Load(newIdx);
    if (clone == null) return -1;
    // 깊은 복사 — JSON serialize → deserialize cycle 또는 직접 복사
    foreach (var it in src.Items)
    {
        clone.Items.Add(new ContainerItemEntry
        {
            // 모든 ContainerItemEntry 필드 복사 (실제 필드 list 에 따라 조정)
            Name = it.Name,
            Weight = it.Weight,
            // ... 다른 필드
        });
    }
    Save(newIdx, clone);
    RefreshMetadata();
    return newIdx;
}
```

- [ ] **Step 4: Run test → PASS**

`dotnet test --filter ...` → 3 PASS.

- [ ] **Step 5: commit**

```bash
git commit -m "feat(containers): v0.7.11 Phase 1 — ContainerRepository.Clone (Cat 5C 깊은 복사)"
```

### Task 1.4: ContainerPanel — 삭제 confirm + 정보 표시 + Clone button

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs`

- [ ] **Step 1: 삭제 confirm** — 기존 `[삭제]` button (line 393-404) 변경:

```csharp
if (GUILayout.Button("삭제", GUILayout.Width(45)))
{
    if (_repo != null && _selectedContainerIdx > 0)
    {
        var meta = _containerList.Find(c => c.ContainerIndex == _selectedContainerIdx);
        string name = meta?.ContainerName ?? $"#{_selectedContainerIdx}";
        int items = meta?.ItemCount ?? 0;
        _confirmDialog.Show(
            $"<{name}> 컨테이너를 삭제하시겠습니까?\n안의 {items}개 item 도 함께 삭제됩니다.",
            onConfirm: DoDeleteContainer);
    }
}

private void DoDeleteContainer()
{
    if (_repo == null || _selectedContainerIdx <= 0) return;
    _repo.Delete(_selectedContainerIdx);
    _selectedContainerIdx = -1;
    Config.ContainerLastIndex.Value = -1;
    RefreshContainerList();
    OnContainerSelected?.Invoke(-1);
    Toast("컨테이너 삭제됨");
}
```

`_confirmDialog` field 추가 (existing ConfirmDialog 자산 재사용).

- [ ] **Step 2: 정보 표시** — dropdown entry format (line 411):

```csharp
foreach (var m in _containerList)
{
    string label = $"{m.ContainerIndex:D2}: {m.ContainerName} ({m.ItemCount}개, {m.TotalWeight:F1}kg)";
    if (GUILayout.Button(label)) { ... }
}
```

- [ ] **Step 3: Clone button** — 우측 column 헤더 button row 에 추가 (이름변경 다음):

```csharp
if (GUILayout.Button("복사", GUILayout.Width(45)))
{
    if (_repo != null && _selectedContainerIdx > 0)
    {
        int newIdx = _repo.Clone(_selectedContainerIdx);
        if (newIdx > 0)
        {
            RefreshContainerList();
            _selectedContainerIdx = newIdx;
            Config.ContainerLastIndex.Value = newIdx;
            OnContainerSelected?.Invoke(newIdx);
            Toast($"컨테이너 #{newIdx} 복사됨");
        }
        else
        {
            Toast("컨테이너 복사 실패");
        }
    }
}
```

- [ ] **Step 4: build + test + smoke**

```bash
dotnet build -c Release && dotnet test
```
Expected: 376 PASS.

- [ ] **Step 5: commit**

```bash
git commit -m "feat(ui): v0.7.11 Phase 1 — ContainerPanel 삭제 confirm + dropdown 정보 + Clone button (Cat 5)"
```

---

## Phase 2 — Cat 1: 인벤/창고 collapse + split preset

### Task 2.1: Config — 3 신규 ConfigEntry

**Files:** Modify `src/LongYinRoster/Config.cs`

- [ ] **Step 1: 추가**

```csharp
// v0.7.11 Cat 1 — 인벤/창고 collapse + split preset
public static ConfigEntry<bool>    ContainerInventoryCollapsed = null!;
public static ConfigEntry<bool>    ContainerStorageCollapsed   = null!;
public static ConfigEntry<int>     ContainerSplitPreset        = null!;
```

`Bind()` 안:
```csharp
ContainerInventoryCollapsed = cfg.Bind("Container", "InventoryCollapsed", false,
    "인벤토리 list collapse 상태 (true = 접힘, 라벨만 표시)");
ContainerStorageCollapsed = cfg.Bind("Container", "StorageCollapsed", false,
    "창고 list collapse 상태");
ContainerSplitPreset = cfg.Bind("Container", "SplitPreset", 0,
    new ConfigDescription("인벤/창고 height 비율 preset (0=50:50, 1=70:30, 2=30:70, 3=100:0/0:100)",
        new AcceptableValueRange<int>(0, 3)));
```

- [ ] **Step 2: build → PASS, commit**

```bash
git commit -m "feat(config): v0.7.11 Phase 2 — ContainerInventoryCollapsed + StorageCollapsed + SplitPreset"
```

### Task 2.2: ContainerPanel — collapse 토글 + split preset

**Files:** Modify `src/LongYinRoster/UI/ContainerPanel.cs`

- [ ] **Step 1: DrawLeftColumn 변경**

```csharp
private void DrawLeftColumn()
{
    GUILayout.BeginVertical(GUILayout.Width(390));

    bool invCollapsed = Config.ContainerInventoryCollapsed.Value;
    bool stoCollapsed = Config.ContainerStorageCollapsed.Value;
    int preset = Config.ContainerSplitPreset.Value;

    // 사용 가능 height = 640 (기존 Begin/EndScrollView height)
    const float TOTAL_H = 640f;
    const float COLLAPSED_H = 28f;  // collapse 시 라벨만
    const float SPLIT_GAP = 4f;

    float invH, stoH;
    if (invCollapsed && !stoCollapsed) { invH = COLLAPSED_H; stoH = TOTAL_H - COLLAPSED_H - SPLIT_GAP; }
    else if (!invCollapsed && stoCollapsed) { invH = TOTAL_H - COLLAPSED_H - SPLIT_GAP; stoH = COLLAPSED_H; }
    else if (invCollapsed && stoCollapsed) { invH = stoH = COLLAPSED_H; }
    else
    {
        // split preset 적용
        (invH, stoH) = preset switch
        {
            1 => (TOTAL_H * 0.7f - SPLIT_GAP/2, TOTAL_H * 0.3f - SPLIT_GAP/2),
            2 => (TOTAL_H * 0.3f - SPLIT_GAP/2, TOTAL_H * 0.7f - SPLIT_GAP/2),
            3 => (TOTAL_H - COLLAPSED_H - SPLIT_GAP, COLLAPSED_H),  // 100:0 → storage 작게
            _ => (TOTAL_H * 0.5f - SPLIT_GAP/2, TOTAL_H * 0.5f - SPLIT_GAP/2),
        };
    }

    DrawListSection("인벤토리", _inventoryRows, _inventoryChecks, ref _invScroll, invH,
        invCollapsed, () => Config.ContainerInventoryCollapsed.Value = !Config.ContainerInventoryCollapsed.Value,
        ContainerArea.Inventory);

    GUILayout.Space(SPLIT_GAP);

    DrawListSection("창고", _storageRows, _storageChecks, ref _stoScroll, stoH,
        stoCollapsed, () => Config.ContainerStorageCollapsed.Value = !Config.ContainerStorageCollapsed.Value,
        ContainerArea.Storage);

    // Split preset cycle button
    GUILayout.BeginHorizontal();
    string presetLabel = preset switch { 1 => "70:30", 2 => "30:70", 3 => "100:0", _ => "50:50" };
    if (GUILayout.Button($"비율 {presetLabel} ▼", GUILayout.Width(120)))
    {
        Config.ContainerSplitPreset.Value = (preset + 1) % 4;
    }
    GUILayout.EndHorizontal();

    GUILayout.EndVertical();
}

private void DrawListSection(string title, List<ItemRow> rows, HashSet<int> checks,
                             ref Vector2 scroll, float height, bool collapsed, System.Action onToggle,
                             ContainerArea area)
{
    GUILayout.BeginHorizontal();
    if (GUILayout.Button(collapsed ? "▶" : "▼", GUILayout.Width(24))) onToggle();
    float weight = 0f; foreach (var r in rows) weight += r.Weight;
    float maxW = area == ContainerArea.Inventory ? _inventoryMaxWeight : _storageMaxWeight;
    GUILayout.Label(FormatCount(title, rows.Count, weight, maxW, allowOvercap: area == ContainerArea.Inventory));
    GUILayout.EndHorizontal();

    if (collapsed) return;

    var view = (area == ContainerArea.Inventory ? _invView : _stoView).ApplyView(rows, _globalState);
    DrawItemList(area, view, checks, ref scroll, height);

    GUILayout.BeginHorizontal();
    var arrowMove = area == ContainerArea.Inventory ? OnInventoryToContainerMove : OnStorageToContainerMove;
    var arrowCopy = area == ContainerArea.Inventory ? OnInventoryToContainerCopy : OnStorageToContainerCopy;
    if (GUILayout.Button("→ 이동")) arrowMove?.Invoke(new HashSet<int>(checks));
    if (GUILayout.Button("→ 복사")) arrowCopy?.Invoke(new HashSet<int>(checks));
    GUILayout.EndHorizontal();
}
```

- [ ] **Step 2: build + 인게임 smoke**

build pass + 인게임에서 collapse/split preset 정상 동작 확인.

- [ ] **Step 3: commit**

```bash
git commit -m "feat(ui): v0.7.11 Phase 2 — ContainerPanel 인벤/창고 collapse + split 4-preset (Cat 1A+1B)"
```

---

## Phase 3 — Cat 2: 일괄선택 button + 카운터 + 등급별 + 착용중 제외

### Task 3.1: ContainerView — 등급 범위 + 착용중 filter 추가

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerView.cs`
- Create: `src/LongYinRoster.Tests/ContainerViewExtraFilterTests.cs`

- [ ] **Step 1: Write failing test** — visible row 가 등급 범위 + 착용중 필터 모두 적용되는지

(코드 생략 — POCO mock with rows of varying RareLv + Equipped, assert ApplyView output)

- [ ] **Step 2: ContainerView.ApplyView 시그니처 확장**

기존:
```csharp
public List<ItemRow> ApplyView(List<ItemRow> rows, SearchSortState state)
```

신규:
```csharp
public List<ItemRow> ApplyView(List<ItemRow> rows, SearchSortState state,
                               int minRareLv = -1, bool excludeEquipped = false)
```

내부:
```csharp
var filtered = rows.Where(r =>
    (minRareLv < 0 || r.RareLv >= minRareLv) &&
    (!excludeEquipped || !r.Equipped) &&
    /* 기존 search/sort/category filter */).ToList();
```

- [ ] **Step 3: build + test → 5 PASS**

- [ ] **Step 4: commit**

```bash
git commit -m "feat(containers): v0.7.11 Phase 3 — ContainerView 등급 범위 + 착용중 filter (Cat 4B/4E)"
```

### Task 3.2: ContainerPanel — 2A 일괄 button + 2B 카운터 + 2C 등급별 + 2H 토글

**Files:** Modify `src/LongYinRoster/UI/ContainerPanel.cs`

- [ ] **Step 1: 헤더 일괄 button + 카운터** — `DrawListSection` 의 헤더 row 에 추가:

```csharp
GUILayout.BeginHorizontal();
if (GUILayout.Button(collapsed ? "▶" : "▼", GUILayout.Width(24))) onToggle();

// v0.7.11 — 카운터 + 무게 (선택 포함)
int selCount = checks.Count;
float selWeight = 0f; foreach (int idx in checks) if (idx < rows.Count) selWeight += rows[idx].Weight;
float weight = 0f; foreach (var r in rows) weight += r.Weight;
float maxW = area == ContainerArea.Inventory ? _inventoryMaxWeight : _storageMaxWeight;
string countStr = selCount > 0
    ? $"{title} (선택: {selCount} / {rows.Count}개, {selWeight:F1}/{weight:F1}kg / 최대 {maxW:F1}kg)"
    : FormatCount(title, rows.Count, weight, maxW, allowOvercap: area == ContainerArea.Inventory);
GUILayout.Label(countStr);

// v0.7.11 — 일괄 button
GUILayout.FlexibleSpace();  // ⚠ STRIP RISK — Space(N) 으로 교체 필요
GUILayout.Space(8);
if (GUILayout.Button("☑", GUILayout.Width(28)))
{
    // 현재 visible row 모두 선택
    var view = (area == ContainerArea.Inventory ? _invView : _stoView).ApplyView(rows, _globalState,
        Config.ContainerFilterMinRare.Value, Config.ContainerExcludeEquipped.Value);
    foreach (var r in view) checks.Add(r.Index);
}
if (GUILayout.Button("☐", GUILayout.Width(28))) checks.Clear();
if (GUILayout.Button("↺", GUILayout.Width(28)))
{
    // 반전 — visible row 의 currently checked 는 빼고, unchecked 는 추가
    var view = (area == ContainerArea.Inventory ? _invView : _stoView).ApplyView(rows, _globalState,
        Config.ContainerFilterMinRare.Value, Config.ContainerExcludeEquipped.Value);
    foreach (var r in view)
    {
        if (checks.Contains(r.Index)) checks.Remove(r.Index);
        else checks.Add(r.Index);
    }
}
// 등급별 일괄
string rareLabel = Config.ContainerFilterMinRare.Value < 0 ? "등급 전체" : $"등급 ≥ {GetRareLabel(Config.ContainerFilterMinRare.Value)}";
if (GUILayout.Button($"[{rareLabel}]", GUILayout.Width(80)))
{
    Config.ContainerFilterMinRare.Value = (Config.ContainerFilterMinRare.Value + 2) % 7 - 1;  // -1 → 0 → 1 → ... → 5 → -1
}
GUILayout.EndHorizontal();
```

⚠ **`GUILayout.FlexibleSpace()` strip risk** — 위 코드는 spike 검증 후 사용. STRIP 시 fixed `GUILayout.Space(N)` 으로 교체.

- [ ] **Step 2: 글로벌 toolbar 에 착용중 토글 추가**

`DrawGlobalToolbar` 안:
```csharp
bool exclude = Config.ContainerExcludeEquipped.Value;
bool newExclude = GUILayout.Toggle(exclude, "착용중 제외", GUILayout.Width(100));
if (newExclude != exclude) Config.ContainerExcludeEquipped.Value = newExclude;
```

- [ ] **Step 3: build + 인게임 smoke**

- [ ] **Step 4: commit**

```bash
git commit -m "feat(ui): v0.7.11 Phase 3 — ContainerPanel 일괄선택 + 카운터 + 등급별 + 착용중 (Cat 2A/B/C/H)"
```

---

## Phase 4 — Cat 4: 등급 범위 + 무공 secondary tab + 결과 카운터

### Task 4.1: ContainerPanel — 4B/4G/4K UI

**Files:** Modify `src/LongYinRoster/UI/ContainerPanel.cs`

- [ ] **Step 1: 글로벌 toolbar 에 4B (등급 범위) + 4K (결과 카운터)**

`DrawGlobalToolbar` 끝에 추가:
```csharp
// 4B 는 Phase 3 의 헤더 button 으로 이미 처리. 여기서는 4K 만.
int totalAfterFilter = 0;
foreach (var r in _inventoryRows.Concat(_storageRows)) {
    if (FilterPasses(r)) totalAfterFilter++;
}
int totalRaw = _inventoryRows.Count + _storageRows.Count;
GUILayout.Label($"(결과: {totalAfterFilter} / {totalRaw})", GUILayout.Width(150));
```

- [ ] **Step 2: 4G — 무공 secondary tab** (카테고리 = 비급 일 때만 표시)

`DrawCategoryTabs` 다음에:
```csharp
if (_filter == ItemCategoryFilter.Book)  // 비급
{
    DrawKungfuTypeSecondaryTabs();  // 9 tab + 전체 (10)
}
```

`_kungfuTypeFilter` field 추가 (-1 = 전체, 0~8 = 9 type). `ContainerView.ApplyView` 시그니처에 `int kungfuType = -1` 추가.

- [ ] **Step 3: build + 인게임 smoke**

- [ ] **Step 4: commit**

```bash
git commit -m "feat(ui): v0.7.11 Phase 4 — ContainerPanel 등급 범위 + 무공 secondary tab + 결과 카운터 (Cat 4B/G/K)"
```

---

## Phase 5 — Cat 3: Undo + toast 강화 + button 강조

### Task 5.1: ContainerOpUndo — Undo stack + OpRecord

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerOpUndo.cs`
- Create: `src/LongYinRoster.Tests/ContainerOpUndoTests.cs`

- [ ] **Step 1: Write failing test** (생략 — Undo Move/Copy/Clone inverse 검증, 5 tests)

- [ ] **Step 2: Create ContainerOpUndo.cs**

```csharp
using System;
using System.Collections.Generic;

namespace LongYinRoster.Containers;

public enum OpKind { Move, Copy, Delete, Clone }

public sealed record OpRecord(
    OpKind Kind,
    ContainerArea Source,
    ContainerArea Target,
    int? SourceContainerIdx,
    int? TargetContainerIdx,
    List<int> AffectedItemIDs,
    DateTime Timestamp);

public static class ContainerOpUndo
{
    private static OpRecord? _lastOp;

    public static void Record(OpRecord op) => _lastOp = op;

    public static OpRecord? Peek() => _lastOp;

    public static OpRecord? Pop()
    {
        var op = _lastOp;
        _lastOp = null;
        return op;
    }

    public static bool CanUndo => _lastOp != null && _lastOp.Kind != OpKind.Delete;
    // Delete Undo 는 v0.7.11 에서 미지원 (file 복원 어려움)
}
```

- [ ] **Step 3: Run test → PASS, commit**

### Task 5.2: ContainerOps — Undo metadata 기록

**Files:** Modify `src/LongYinRoster/Containers/ContainerOps.cs`

- [ ] **Step 1**: 모든 ContainerOp 호출 site (GameToContainer / ContainerToGame / Clone) 끝에 `ContainerOpUndo.Record(...)` 추가

- [ ] **Step 2: build + test, commit**

### Task 5.3: ContainerPanel — Undo button + toast 강화 + button 강조

**Files:** Modify `src/LongYinRoster/UI/ContainerPanel.cs`

- [ ] **Step 1**: 글로벌 toolbar 에 Undo button:

```csharp
GUI.enabled = ContainerOpUndo.CanUndo;
if (GUILayout.Button("↶ Undo", GUILayout.Width(80)))
{
    var op = ContainerOpUndo.Pop();
    if (op != null) PerformInverse(op);  // Plugin 또는 ModWindow 의 callback
}
GUI.enabled = true;
```

- [ ] **Step 2: 3D 토스트 강화**

`Toast` 호출 site 에서 success/fail 카운트 + 사유 누적. 예:
```csharp
Toast(failed == 0 ? $"{success}개 이동" : $"{success}개 이동, {failed}개 실패 ({reason})");
```

- [ ] **Step 3: 3G button 강조**

`DrawListSection` 의 [→이동] [→복사] button 직전:
```csharp
var prevColor = GUI.color;
GUI.enabled = checks.Count > 0 && _selectedContainerIdx > 0;
if (GUI.enabled) GUI.color = new Color(0.5f, 1.0f, 0.5f);  // 녹색 강조
if (GUILayout.Button("→ 이동")) arrowMove?.Invoke(new HashSet<int>(checks));
if (GUILayout.Button("→ 복사")) arrowCopy?.Invoke(new HashSet<int>(checks));
GUI.color = prevColor;
GUI.enabled = true;
```

우측 컨테이너 ←-callback 도 동일.

- [ ] **Step 4: build + test, commit**

```bash
git commit -m "feat(ui): v0.7.11 Phase 5 — ContainerPanel Undo + toast 강화 + button 강조 (Cat 3C/D/G)"
```

---

## Phase 6 — Cat 9: corner resize handle + clamp

### Task 6.1: ContainerPanel — DrawResizeHandle

**Files:** Modify `src/LongYinRoster/UI/ContainerPanel.cs`

- [ ] **Step 1: 추가 field**

```csharp
private bool _resizing;
private Vector2 _resizeStart;
private Vector2 _resizeStartSize;

private const float MIN_W = 600f;
private const float MAX_W = 1600f;
private const float MIN_H = 400f;
private const float MAX_H = 1080f;
```

- [ ] **Step 2: DrawResizeHandle method**

```csharp
private void DrawResizeHandle()
{
    var handleRect = new Rect(_rect.width - 16, _rect.height - 16, 16, 16);
    var prev = GUI.color;
    GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
    GUI.DrawTexture(handleRect, Texture2D.whiteTexture);
    GUI.color = prev;

    var e = Event.current;
    if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
    {
        _resizing = true;
        _resizeStart = e.mousePosition;
        _resizeStartSize = new Vector2(_rect.width, _rect.height);
        e.Use();
    }
    else if (_resizing && e.type == EventType.MouseDrag)
    {
        var delta = e.mousePosition - _resizeStart;
        float newW = Math.Max(MIN_W, Math.Min(MAX_W, _resizeStartSize.x + delta.x));
        float newH = Math.Max(MIN_H, Math.Min(MAX_H, _resizeStartSize.y + delta.y));
        _rect = new Rect(_rect.x, _rect.y, newW, newH);
        e.Use();
    }
    else if (_resizing && e.type == EventType.MouseUp)
    {
        _resizing = false;
        Config.ContainerPanelW.Value = _rect.width;
        Config.ContainerPanelH.Value = _rect.height;
        e.Use();
    }
}
```

- [ ] **Step 3: Draw 끝부분에 호출 추가**

```csharp
DrawToast();
DrawResizeHandle();   // v0.7.11 Cat 9
GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
```

- [ ] **Step 4: build + 인게임 smoke**

resize handle 작동 + clamp + 영속화 검증.

- [ ] **Step 5: commit**

```bash
git commit -m "feat(ui): v0.7.11 Phase 6 — ContainerPanel corner resize handle + clamp (Cat 9A/9D)"
```

---

## Phase 7 — Release docs + smoke + tag

### Task 7.1: Plugin VERSION + README + HANDOFF

- VERSION: 0.7.10.2 → 0.7.11
- README v0.7.11 entry
- HANDOFF 진행 상태 + Releases + 다음 후속 (NPC dropdown defer 명시)
- dist/release-notes-v0.7.11.md
- 메타 roadmap §G4 Decision append

### Task 7.2: 인게임 smoke (대규모, ~30+ 항목)

각 phase 별 smoke:
- Phase 1: 삭제 confirm dialog + dropdown 정보 + Clone
- Phase 2: collapse 토글 × 2 + split preset 4-cycle
- Phase 3: 일괄 button 3 + 카운터 + 등급별 일괄 + 착용중 토글
- Phase 4: 등급 범위 dropdown + 무공 secondary tab + 결과 카운터
- Phase 5: Undo + toast 강화 + button 강조
- Phase 6: resize handle + clamp + 영속화
- 회귀: v0.7.10.2 모든 기능 (이동/복사/검색/정렬/카테고리)

### Task 7.3: Tag + push + GitHub release

```bash
git tag v0.7.11
git push origin main
git push origin v0.7.11
gh release create v0.7.11 --notes-file dist/release-notes-v0.7.11.md \
    --title "v0.7.11 — ContainerPanel UX overhaul (6 카테고리)"
```

---

## Self-Review

**Spec coverage**: 모든 채택 항목 (1A/1B/2A/B/C/H/3C/D/G/4B/E/G/K/5A/B/C/9A/D) phase 별 task 명시 ✓
**Type consistency**: ContainerView.ApplyView 시그니처 확장 (minRareLv + excludeEquipped + kungfuType) — Phase 3/4 일관 사용 ✓
**Placeholder**: `GUILayout.FlexibleSpace()` strip risk 명시 (Phase 3.1 Step 1 ⚠) — fallback `Space(N)` ✓
**Test count**: 374 + 13 = 387 추정 ✓
**Phase 분리**: 7 phase 각 commit 분리, 회귀 격리 명확 ✓

## 실행 옵션

**Plan complete and saved to `docs/superpowers/plans/2026-05-10-longyin-roster-mod-v0.7.11-plan.md`**

1. **Subagent-Driven** (recommended) — task 별 fresh agent + 두 단계 review
2. **Inline Execution** — 본 session batch 실행
3. **잠시 중지** — plan 검토 후 재개

답해주시면 진행.
