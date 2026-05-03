# LongYin Roster Mod v0.7.2 (D-3 컨테이너 검색·정렬) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ContainerPanel 의 인벤·창고·외부 디스크 컨테이너 3-area 모두에 독립 검색 box + 정렬 dropdown (카테고리·이름·등급·품질) 을 추가한다. 양방향 이동 후 view 가 자동 갱신되며, 정렬·검색 상태는 세션 휘발이다.

**Architecture:** Approach α — `ItemRow` 에 4 sort key (`CategoryKey` / `NameRaw` / `GradeOrder` / `QualityOrder`) 를 ContainerRowBuilder 진입 시 1회 reflection 으로 채움. area 별 `SearchSortState` (immutable POCO) + `ContainerView` (raw + state hash 기반 cache). OnGUI 매 frame 은 cache 만 그림. raw 변경 (4-callback) 또는 state 변경 (toolbar 입력) 시 invalidate. IMGUI strip 회피를 위해 `GUILayout.TextField` / `Button(string)` / `Label(string)` default skin only. dropdown 은 4-segmented button.

**Tech Stack:** C# 10, BepInEx 6 IL2CPP, Newtonsoft IL2CPP-bound (Serializer 한 곳만), System.Text.Json (slot/container traversal), Harmony, IMGUI (GUILayout). 테스트: xUnit (LongYinRoster.Tests).

**Spec:** `docs/superpowers/specs/2026-05-03-longyin-roster-mod-v0.7.2-design.md`

---

## File Structure

| 신규/변경 | 파일 | 역할 |
|---|---|---|
| 신규 | `src/LongYinRoster/Containers/SortKey.cs` | enum SortKey { Category, Name, Grade, Quality } |
| 신규 | `src/LongYinRoster/Containers/SearchSortState.cs` | immutable POCO + WithSearch / WithKey / ToggleDirection |
| 신규 | `src/LongYinRoster/Containers/ContainerView.cs` | ApplyView(raw, state) + cache (raw·state hash 비교) |
| 신규 | `src/LongYinRoster/Core/ItemReflector.cs` | item-level reflection helper — GetCategoryKey / GetNameRaw / GetGradeOrder / GetQualityOrder |
| 신규 | `src/LongYinRoster/UI/SearchSortToolbar.cs` | IMGUI 1-line — TextField + 4 segmented + ▲/▼ |
| 변경 | `src/LongYinRoster/UI/ContainerPanel.cs` | ItemRow 확장 + 3-area toolbar + 3-area state + cache 사용 |
| 변경 | `src/LongYinRoster/Containers/ContainerRowBuilder.cs` | 두 path (JSON / IL2CPP) 모두 sort key 4종 채움 |
| 변경 | `src/LongYinRoster/UI/ModWindow.cs` | Set{Inventory,Storage,Container}Rows 호출 후 ContainerPanel 의 raw refresh hook 호출 |
| 변경 | `src/LongYinRoster/Util/KoreanStrings.cs` | 정렬 라벨 + 검색 placeholder + 토스트 메시지 |
| 신규 | `src/LongYinRoster.Tests/SearchSortStateTests.cs` | 3 tests |
| 신규 | `src/LongYinRoster.Tests/ContainerViewTests.cs` | 5 tests |
| 신규 | `src/LongYinRoster.Tests/ItemReflectorTests.cs` | 3 tests |
| 신규 (선택) | `src/LongYinRoster.Tests/ContainerRowBuilderTests.cs` | sort key 채움 검증 (있다면 1~2 tests) |
| 신규 | `docs/superpowers/dumps/2026-05-XX-v0.7.2-grade-quality-spike.md` | spike 산출물 |

기존 패턴 일관성: `Core/ItemListReflector.cs` 와 `Containers/ContainerOps.cs` 의 reflection / IL2CPP 흐름을 그대로 따름.

---

## Task 0: Spike — ItemData 등급/품질 reflection dump

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (임시 [F12] dump handler — Task 끝에서 제거)
- Create: `docs/superpowers/dumps/2026-05-XX-v0.7.2-grade-quality-spike.md`

**Note:** 이 task 만 TDD 가 적용되지 않는다 (인게임 reflection dump). 산출물은 markdown dump 와 후속 task 에서 사용할 필드명·한자 매핑이다.

- [ ] **Step 0.1: ModWindow.cs 에 [F12] dump handler 추가**

`ModWindow.cs` 의 `OnGUI` 또는 별도 Update 진입점 (기존 `[F11]` toggle 옆). 정확 위치는 ModWindow 의 입력 처리 path. 첫 인벤토리 item 의 모든 public/non-public field + property 를 dump:

```csharp
// ModWindow.cs — _spike_v0_7_2_dumped 플래그로 1회만 실행
private bool _spike_v0_7_2_dumped = false;
private void DumpFirstItemFields_v0_7_2()
{
    if (_spike_v0_7_2_dumped) return;
    var ild = GetPlayerItemListData();
    if (ild == null) { Util.Logger.Warn("[v0.7.2 spike] ItemListData null"); return; }
    var allItem = ReadObj(ild, "allItem");
    if (allItem == null) { Util.Logger.Warn("[v0.7.2 spike] allItem null"); return; }
    int n = LongYinRoster.Containers.IL2CppListOps.Count(allItem);
    if (n == 0) { Util.Logger.Warn("[v0.7.2 spike] inventory empty"); return; }
    var item = LongYinRoster.Containers.IL2CppListOps.Get(allItem, 0);
    if (item == null) { Util.Logger.Warn("[v0.7.2 spike] item null"); return; }

    var t = item.GetType();
    Util.Logger.Info($"[v0.7.2 spike] item type = {t.FullName}");
    const System.Reflection.BindingFlags F =
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    foreach (var p in t.GetProperties(F))
    {
        try { Util.Logger.Info($"[v0.7.2 spike] prop {p.PropertyType.Name} {p.Name} = {p.GetValue(item)}"); }
        catch (System.Exception ex) { Util.Logger.Info($"[v0.7.2 spike] prop {p.Name} threw {ex.Message}"); }
    }
    foreach (var f in t.GetFields(F))
    {
        try { Util.Logger.Info($"[v0.7.2 spike] fld  {f.FieldType.Name} {f.Name} = {f.GetValue(item)}"); }
        catch (System.Exception ex) { Util.Logger.Info($"[v0.7.2 spike] fld  {f.Name} threw {ex.Message}"); }
    }
    _spike_v0_7_2_dumped = true;
}

private static object? ReadObj(object obj, string name)
{
    var t = obj.GetType();
    const System.Reflection.BindingFlags F =
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var p = t.GetProperty(name, F); if (p != null) return p.GetValue(obj);
    var f = t.GetField(name, F); if (f != null) return f.GetValue(obj);
    return null;
}
```

`OnGUI` 진입 또는 Update (`UnityEngine.Input.GetKeyDown(KeyCode.F12)` 검사) 에서 위 method 호출. ContainerPanel 이 닫혀 있어도 동작하도록 ModWindow 자체 입력 분기에 둔다.

`GetPlayerItemListData()` 는 ModWindow.cs 안에 v0.7.1 시점 이미 존재. 없으면 같은 클래스에서 reflection path (Plugin → HeroLocator → player.itemListData) 로 추가.

- [ ] **Step 0.2: 빌드**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: build succeeded, dll deployed.

- [ ] **Step 0.3: 게임 실행 + 인게임 [F12]**

게임을 시작하고 게임 진행. 인벤토리에 다양한 등급·품질 아이템이 있는 시점에서 [F12] 1회 누름. 가능하면 다른 등급·품질 가진 캐릭터로 [F12] 추가 1~2회 (리셋 위해 `_spike_v0_7_2_dumped = false` 잠시 토글하는 임시 분기 추가 가능).

- [ ] **Step 0.4: BepInEx/LogOutput.log 분석**

```bash
grep -n "v0.7.2 spike" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" > /tmp/spike-v0.7.2.log
```

dump 줄에서 다음을 찾는다:
1. **type / category**: `Type` / `SubType` 외에 별도 string/enum 이 있는가? 없다면 spec §4.2 의 `CategoryKey` 를 `$"{Type}.{SubType}"` 같은 합성 string 으로 결정
2. **name (한자)**: `name` field 그대로 사용
3. **등급**: `grade` / `level` / `lv` / `tier` / `rank` / `dengji` 같은 후보 이름 + 값이 0~5 range int 인지 / 한자 string 인지 / enum (Il2CppEnum) 인지
4. **품질**: `quality` / `purity` / `pin` / `pinji` / `pinzhi` 같은 후보 이름 + 값 형태

- [ ] **Step 0.5: spike 산출물 markdown 작성**

`docs/superpowers/dumps/2026-05-03-v0.7.2-grade-quality-spike.md` (날짜는 spike 실행 일자):

```markdown
# v0.7.2 — ItemData 등급·품질 spike 결과

**일시**: 2026-05-XX
**dump 경로**: BepInEx/LogOutput.log "v0.7.2 spike" 줄

## 발견된 필드

| 의미 | 필드명 | 타입 | 값 form | 매핑 |
|---|---|---|---|---|
| 카테고리 | (예: `Type` + `SubType` 조합) | int + int | 0..N | `$"{Type}.{SubType}"` 합성 |
| 이름 | `name` | string | 한자 raw | 그대로 |
| 등급 | (예: `grade`) | int | 0..5 | 0=열악(劣), 1=보통(普), 2=우수(优), 3=정량(精), 4=완벽(完美), 5=절세(绝世) |
| 품질 | (예: `quality`) | int | 0..5 | 0=잔품(残), 1=하품(下), 2=중품(中), 3=상품(上), 4=진품(珍), 5=극품(极) |

## 미발견 필드 (있을 경우)

(spike 결과 못 찾으면 fallback: dropdown disabled. v0.7.4 D-1 시점에 재시도.)

## 발견 시 ItemReflector 의 access path

- `GetGradeOrder(item)`: `t.GetProperty("grade", BF) ?? t.GetField("grade", BF)` → int 캐스팅
- `GetQualityOrder(item)`: 동상 `quality`
- string enum 인 경우: 값을 `GradeMap` / `QualityMap` 로 lookup
- 미발견 시: -1 반환
```

dump 가 enum (`Il2CppSystem.Enum`) 으로 노출되면 ToString() 결과 한자를 GradeMap / QualityMap dictionary 로 매핑.

- [ ] **Step 0.6: dump handler 제거**

ModWindow.cs 에서 `DumpFirstItemFields_v0_7_2`, `_spike_v0_7_2_dumped`, F12 분기 모두 제거. ReadObj 이 ModWindow 에 신규 추가됐다면 그것도 제거.

- [ ] **Step 0.7: Commit**

```bash
git add docs/superpowers/dumps/2026-05-03-v0.7.2-grade-quality-spike.md src/LongYinRoster/UI/ModWindow.cs
git commit -m "docs(spike): v0.7.2 ItemData grade/quality reflection dump"
```

---

## Task 1: SortKey enum + SearchSortState POCO (TDD)

**Files:**
- Create: `src/LongYinRoster/Containers/SortKey.cs`
- Create: `src/LongYinRoster/Containers/SearchSortState.cs`
- Test: `src/LongYinRoster.Tests/SearchSortStateTests.cs`

- [ ] **Step 1.1: 실패 테스트 작성**

`src/LongYinRoster.Tests/SearchSortStateTests.cs`:

```csharp
using Xunit;
using LongYinRoster.Containers;

namespace LongYinRoster.Tests;

public class SearchSortStateTests
{
    [Fact]
    public void Default_state_has_empty_search_category_key_and_ascending()
    {
        var s = SearchSortState.Default;
        Assert.Equal("", s.Search);
        Assert.Equal(SortKey.Category, s.Key);
        Assert.True(s.Ascending);
    }

    [Fact]
    public void WithSearch_returns_new_instance_with_updated_text_only()
    {
        var s = SearchSortState.Default;
        var s2 = s.WithSearch("檢");
        Assert.Equal("", s.Search);     // 원본 불변
        Assert.Equal("檢", s2.Search);
        Assert.Equal(s.Key, s2.Key);
        Assert.Equal(s.Ascending, s2.Ascending);
    }

    [Fact]
    public void WithKey_and_ToggleDirection_compose_correctly()
    {
        var s = SearchSortState.Default
            .WithKey(SortKey.Grade)
            .ToggleDirection();
        Assert.Equal(SortKey.Grade, s.Key);
        Assert.False(s.Ascending);
    }
}
```

- [ ] **Step 1.2: 테스트 실행 — 실패 확인**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SearchSortStateTests"
```

Expected: 컴파일 에러 (`SortKey` / `SearchSortState` not defined).

- [ ] **Step 1.3: SortKey enum 작성**

`src/LongYinRoster/Containers/SortKey.cs`:

```csharp
namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — ContainerPanel 정렬 키. spec §3.3 (사용자 확정 4 키).
/// </summary>
public enum SortKey
{
    Category = 0,
    Name     = 1,
    Grade    = 2,
    Quality  = 3,
}
```

- [ ] **Step 1.4: SearchSortState POCO 작성**

`src/LongYinRoster/Containers/SearchSortState.cs`:

```csharp
namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — 검색·정렬 상태. immutable. cache invalidate 의 source-of-truth.
/// 세션 휘발 (저장 안 함). v0.7.6 영속화 시 직렬화 합류.
/// </summary>
public sealed class SearchSortState
{
    public static readonly SearchSortState Default = new("", SortKey.Category, true);

    public string  Search    { get; }
    public SortKey Key       { get; }
    public bool    Ascending { get; }

    public SearchSortState(string search, SortKey key, bool ascending)
    {
        Search    = search ?? "";
        Key       = key;
        Ascending = ascending;
    }

    public SearchSortState WithSearch(string text)  => new(text ?? "", Key, Ascending);
    public SearchSortState WithKey(SortKey k)       => new(Search, k, Ascending);
    public SearchSortState ToggleDirection()        => new(Search, Key, !Ascending);

    public override int GetHashCode()
        => System.HashCode.Combine(Search, Key, Ascending);

    public override bool Equals(object? obj)
        => obj is SearchSortState s
            && s.Search == Search && s.Key == Key && s.Ascending == Ascending;
}
```

- [ ] **Step 1.5: 테스트 실행 — 통과 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~SearchSortStateTests"
```

Expected: 3/3 PASS.

- [ ] **Step 1.6: Commit**

```bash
git add src/LongYinRoster/Containers/SortKey.cs src/LongYinRoster/Containers/SearchSortState.cs src/LongYinRoster.Tests/SearchSortStateTests.cs
git commit -m "feat(containers): SortKey + SearchSortState POCO (v0.7.2 D-3)"
```

---

## Task 2: ItemReflector — 등급·품질 access (TDD)

**Files:**
- Create: `src/LongYinRoster/Core/ItemReflector.cs`
- Test: `src/LongYinRoster.Tests/ItemReflectorTests.cs`

- [ ] **Step 2.1: 실패 테스트 작성**

`src/LongYinRoster.Tests/ItemReflectorTests.cs`:

```csharp
using Xunit;
using LongYinRoster.Core;

namespace LongYinRoster.Tests;

public class ItemReflectorTests
{
    private sealed class FakeItem
    {
        public int grade;
        public int quality;
        public string name = "";
    }

    [Fact]
    public void GetGradeOrder_reads_int_grade_field_when_present()
    {
        var item = new FakeItem { grade = 3, quality = 5 };
        Assert.Equal(3, ItemReflector.GetGradeOrder(item));
    }

    [Fact]
    public void GetQualityOrder_reads_int_quality_field_when_present()
    {
        var item = new FakeItem { grade = 0, quality = 4 };
        Assert.Equal(4, ItemReflector.GetQualityOrder(item));
    }

    [Fact]
    public void Returns_negative_one_for_missing_or_null()
    {
        var item = new object();   // grade/quality 없음
        Assert.Equal(-1, ItemReflector.GetGradeOrder(item));
        Assert.Equal(-1, ItemReflector.GetQualityOrder(item));
        Assert.Equal(-1, ItemReflector.GetGradeOrder(null));
        Assert.Equal(-1, ItemReflector.GetQualityOrder(null));
    }
}
```

- [ ] **Step 2.2: 테스트 실행 — 실패 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ItemReflectorTests"
```

Expected: 컴파일 에러 (`ItemReflector` not defined).

- [ ] **Step 2.3: ItemReflector 작성**

`src/LongYinRoster/Core/ItemReflector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.2 — 개별 ItemData reflection helper.
/// Task 0 spike (docs/superpowers/dumps/2026-05-XX-v0.7.2-grade-quality-spike.md) 결과를
/// 후보 array 에 반영한다. spike 가 enum 한자 string 으로 노출하면 GradeMap / QualityMap 사용.
///
/// 기존 ItemListReflector 는 list-level (maxWeight) 전용이라 분리.
/// </summary>
public static class ItemReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Task 0 spike 결과 채움. 미발견 시 빈 array → -1 반환.
    private static readonly string[] GRADE_NAMES   = new[] { "grade", "level", "lv", "tier", "rank" };
    private static readonly string[] QUALITY_NAMES = new[] { "quality", "purity", "pin", "pinji", "pinzhi" };

    // Task 0 spike 결과 한자 string 으로 노출되면 enable. int 노출이면 dictionary lookup skip.
    private static readonly Dictionary<string, int> GradeMap = new()
    {
        ["劣"]    = 0, ["普"] = 1, ["优"] = 2, ["精"] = 3, ["完美"] = 4, ["绝世"] = 5,
        ["열악"] = 0, ["보통"] = 1, ["우수"] = 2, ["정량"] = 3, ["완벽"] = 4, ["절세"] = 5,
    };
    private static readonly Dictionary<string, int> QualityMap = new()
    {
        ["残"]    = 0, ["下"] = 1, ["中"] = 2, ["上"] = 3, ["珍"] = 4, ["极"] = 5,
        ["잔품"] = 0, ["하품"] = 1, ["중품"] = 2, ["상품"] = 3, ["진품"] = 4, ["극품"] = 5,
    };

    public static int GetGradeOrder(object? item)   => Read(item, GRADE_NAMES, GradeMap);
    public static int GetQualityOrder(object? item) => Read(item, QUALITY_NAMES, QualityMap);

    public static string GetCategoryKey(object? item)
    {
        if (item == null) return "";
        // ContainerRowBuilder 가 이미 Type / SubType 을 채우므로 그것을 합성. 직접 reflection 도 가능.
        int t  = ReadInt(item, "type");
        int st = ReadInt(item, "subType");
        return $"{t:D3}.{st:D3}";
    }

    public static string GetNameRaw(object? item)
    {
        if (item == null) return "";
        var v = ReadObj(item, "name");
        return v as string ?? "";
    }

    // ------- internals -------

    private static int Read(object? item, string[] names, Dictionary<string, int> map)
    {
        if (item == null) return -1;
        var t = item.GetType();
        foreach (var name in names)
        {
            var raw = ReadFieldOrProperty(t, item, name);
            if (raw == null) continue;
            // int / byte / short / long → int 캐스팅
            if (raw is System.IConvertible)
            {
                try
                {
                    int n = System.Convert.ToInt32(raw);
                    if (raw is string s) { return map.TryGetValue(s, out var ord) ? ord : -1; }
                    return n;
                }
                catch (System.Exception ex) { Logger.Warn($"ItemReflector.Read int cast {name}: {ex.Message}"); }
            }
            // string (한자 enum)
            if (raw is string str)
            {
                return map.TryGetValue(str, out var ord) ? ord : -1;
            }
            // Il2CppSystem.Enum 또는 .NET enum 의 ToString
            var s2 = raw.ToString() ?? "";
            if (map.TryGetValue(s2, out var ord2)) return ord2;
        }
        return -1;
    }

    private static object? ReadFieldOrProperty(Type t, object obj, string name)
    {
        try
        {
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj);
            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj);
        }
        catch (System.Exception ex) { Logger.Warn($"ItemReflector read {name}: {ex.Message}"); }
        return null;
    }

    private static object? ReadObj(object obj, string name) => ReadFieldOrProperty(obj.GetType(), obj, name);

    private static int ReadInt(object obj, string name)
    {
        var v = ReadObj(obj, name);
        if (v == null) return 0;
        try { return System.Convert.ToInt32(v); } catch { return 0; }
    }
}
```

- [ ] **Step 2.4: 테스트 실행 — 통과 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ItemReflectorTests"
```

Expected: 3/3 PASS.

- [ ] **Step 2.5: Commit**

```bash
git add src/LongYinRoster/Core/ItemReflector.cs src/LongYinRoster.Tests/ItemReflectorTests.cs
git commit -m "feat(core): ItemReflector — grade/quality/name/category reflection helper (v0.7.2 D-3)"
```

---

## Task 3: ItemRow 확장 (sort key 4 신규 필드)

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:16-25` (ItemRow 정의)

**Note:** ItemRow 가 init-only POCO 라 신규 필드 추가는 기존 호출자 (ContainerRowBuilder) 두 path 모두에서 채워야 통합 task (Task 4) 에서 처리. 여기서는 모델만 확장. 빌드 가능 상태 유지.

- [ ] **Step 3.1: ItemRow 확장**

`src/LongYinRoster/UI/ContainerPanel.cs` 의 ItemRow 정의를 다음으로 교체 (line 16~25 부근):

```csharp
    public sealed class ItemRow
    {
        public int     Index     { get; init; }
        public string  Name      { get; init; } = "";
        public int     Type      { get; init; }
        public int     SubType   { get; init; }
        public int     EnhanceLv { get; init; }
        public float   Weight    { get; init; }
        public bool    Equipped  { get; init; }

        // v0.7.2 D-3 sort keys (ContainerRowBuilder 가 채움). 미발견 시 -1 / "" — 정렬 끝/앞으로 밀림.
        public string  CategoryKey  { get; init; } = "";
        public string  NameRaw      { get; init; } = "";
        public int     GradeOrder   { get; init; } = -1;
        public int     QualityOrder { get; init; } = -1;
    }
```

NameRaw 는 한자 raw — 검색·정렬 매칭용. `Name` 은 표시 (현재는 같지만 v0.7.5 한글화 시점에 분리됨).

- [ ] **Step 3.2: 빌드 — 기존 코드 영향 없는지 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 빌드 성공. 기존 ItemRow 사용처 (ContainerRowBuilder, ContainerPanel) 는 init-only 필드 추가에 영향 없음 (default value 사용).

- [ ] **Step 3.3: 기존 테스트 회귀 없음 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 기존 117 PASS (Tasks 1+2 의 6 신규 포함, 기존 111).

- [ ] **Step 3.4: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): ItemRow — 4 sort keys (CategoryKey/NameRaw/GradeOrder/QualityOrder) for v0.7.2 D-3"
```

---

## Task 4: ContainerRowBuilder — sort key 채움 (JSON + IL2CPP path)

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerRowBuilder.cs`

**Note:** TDD 적용 — JSON path 는 unit test 가능 (기존 파턴). IL2CPP path 는 reflection 만 검증 가능 (실제 game item 은 인게임 smoke). 이번 task 는 JSON path 만 unit test, IL2CPP path 는 mirror 확장.

- [ ] **Step 4.1: 실패 테스트 작성**

테스트 파일이 없으므로 신규 생성 — `src/LongYinRoster.Tests/ContainerRowBuilderTests.cs`:

```csharp
using Xunit;
using LongYinRoster.Containers;

namespace LongYinRoster.Tests;

public class ContainerRowBuilderTests
{
    [Fact]
    public void FromJsonArray_fills_sort_keys_for_v0_7_2()
    {
        string json = """
        [
          {"name":"补血弹","type":2,"subType":1,"weight":1.5,"grade":3,"quality":4},
          {"name":"无名刀","type":1,"subType":0,"weight":3.2}
        ]
        """;

        var rows = ContainerRowBuilder.FromJsonArray(json);
        Assert.Equal(2, rows.Count);

        Assert.Equal("002.001", rows[0].CategoryKey);
        Assert.Equal("补血弹", rows[0].NameRaw);
        Assert.Equal(3, rows[0].GradeOrder);
        Assert.Equal(4, rows[0].QualityOrder);

        Assert.Equal("001.000", rows[1].CategoryKey);
        Assert.Equal("无名刀", rows[1].NameRaw);
        Assert.Equal(-1, rows[1].GradeOrder);   // grade 필드 없음
        Assert.Equal(-1, rows[1].QualityOrder); // quality 필드 없음
    }
}
```

- [ ] **Step 4.2: 테스트 실행 — 실패 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerRowBuilderTests"
```

Expected: 4 assertion FAIL (sort key 4 모두 default 값).

- [ ] **Step 4.3: ContainerRowBuilder 의 JSON path 갱신**

`src/LongYinRoster/Containers/ContainerRowBuilder.cs` 의 `FromJsonArray` 안에서 `new ContainerPanel.ItemRow { ... }` 부분을 다음으로 교체:

```csharp
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                int type    = RI(e, "type");
                int subType = RI(e, "subType");
                string name = R(e, "name", "");
                int grade   = e.TryGetProperty("grade", out var gv)   && gv.ValueKind == JsonValueKind.Number ? gv.GetInt32() : -1;
                int quality = e.TryGetProperty("quality", out var qv) && qv.ValueKind == JsonValueKind.Number ? qv.GetInt32() : -1;
                list.Add(new ContainerPanel.ItemRow
                {
                    Index        = i++,
                    Name         = name,
                    Type         = type,
                    SubType      = subType,
                    EnhanceLv    = ReadEnhance(e),
                    Weight       = RF(e, "weight"),
                    Equipped     = false,
                    CategoryKey  = $"{type:D3}.{subType:D3}",
                    NameRaw      = name,
                    GradeOrder   = grade,
                    QualityOrder = quality,
                });
            }
```

- [ ] **Step 4.4: ContainerRowBuilder 의 IL2CPP path 갱신**

같은 파일의 `FromGameAllItem` 안 `list.Add(new ContainerPanel.ItemRow { ... })` 부분 교체:

```csharp
            list.Add(new ContainerPanel.ItemRow
            {
                Index        = i,
                Name         = name,
                Type         = type,
                SubType      = subType,
                EnhanceLv    = enh,
                Weight       = weight,
                Equipped     = equipped,
                CategoryKey  = $"{type:D3}.{subType:D3}",
                NameRaw      = name,
                GradeOrder   = LongYinRoster.Core.ItemReflector.GetGradeOrder(item),
                QualityOrder = LongYinRoster.Core.ItemReflector.GetQualityOrder(item),
            });
```

(파일 상단 `using` 절은 기존대로 — `LongYinRoster.Core` 가 없다면 추가:)

```csharp
using LongYinRoster.Core;
```

추가하면 `LongYinRoster.Core.ItemReflector` 를 `ItemReflector.GetGradeOrder` 로 단축 호출 가능 (선택).

- [ ] **Step 4.5: 테스트 실행 — 통과 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerRowBuilderTests"
```

Expected: 1/1 PASS.

- [ ] **Step 4.6: 전체 회귀 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 118 PASS (111 baseline + 7 = 3 SearchSortState + 3 ItemReflector + 1 ContainerRowBuilder).

- [ ] **Step 4.7: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerRowBuilder.cs src/LongYinRoster.Tests/ContainerRowBuilderTests.cs
git commit -m "feat(containers): ContainerRowBuilder — fill 4 sort keys on both JSON/IL2CPP paths (v0.7.2 D-3)"
```

---

## Task 5: ContainerView (filter + sort + cache) (TDD)

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerView.cs`
- Test: `src/LongYinRoster.Tests/ContainerViewTests.cs`

- [ ] **Step 5.1: 실패 테스트 작성**

`src/LongYinRoster.Tests/ContainerViewTests.cs`:

```csharp
using System.Collections.Generic;
using Xunit;
using LongYinRoster.Containers;
using LongYinRoster.UI;

namespace LongYinRoster.Tests;

public class ContainerViewTests
{
    private static ContainerPanel.ItemRow Row(int idx, string name, string cat, int g, int q)
        => new ContainerPanel.ItemRow {
            Index = idx, Name = name, NameRaw = name, CategoryKey = cat,
            GradeOrder = g, QualityOrder = q,
        };

    [Fact]
    public void Search_filters_substring_case_insensitive_on_NameRaw()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "Sword", "001.000", 1, 1), Row(1, "Shield", "001.001", 2, 2), Row(2, "potion", "002.000", 0, 0) };
        var view = new ContainerView();
        var s = SearchSortState.Default.WithSearch("S");
        var result = view.ApplyView(raw, s);
        Assert.Equal(2, result.Count);   // Sword + Shield
    }

    [Fact]
    public void Sort_by_Category_then_Index_ascending()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(2, "B", "002.000", 0, 0), Row(0, "C", "001.001", 0, 0), Row(1, "A", "001.000", 0, 0) };
        var view = new ContainerView();
        var result = view.ApplyView(raw, SearchSortState.Default);   // Category asc default
        Assert.Equal(new[] { 1, 0, 2 }, result.ConvertAll(r => r.Index).ToArray());
    }

    [Fact]
    public void Sort_by_Grade_descending_via_ToggleDirection()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "A", "X", 1, 0), Row(1, "B", "X", 5, 0), Row(2, "C", "X", 3, 0) };
        var view = new ContainerView();
        var s = SearchSortState.Default.WithKey(SortKey.Grade).ToggleDirection();
        var result = view.ApplyView(raw, s);
        Assert.Equal(new[] { 1, 2, 0 }, result.ConvertAll(r => r.Index).ToArray());
    }

    [Fact]
    public void Reflection_failed_rows_with_negative_one_grade_sink_to_end_in_asc()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "A", "X", -1, 0), Row(1, "B", "X", 0, 0), Row(2, "C", "X", 5, 0) };
        var view = new ContainerView();
        var s = SearchSortState.Default.WithKey(SortKey.Grade);   // asc
        var result = view.ApplyView(raw, s);
        // -1 가 가장 작아서 asc 시 맨 앞에 옴
        Assert.Equal(new[] { 0, 1, 2 }, result.ConvertAll(r => r.Index).ToArray());
    }

    [Fact]
    public void Cache_returns_same_array_when_raw_and_state_unchanged()
    {
        var raw = new List<ContainerPanel.ItemRow> { Row(0, "A", "X", 1, 1), Row(1, "B", "Y", 2, 2) };
        var view = new ContainerView();
        var s = SearchSortState.Default;
        var first  = view.ApplyView(raw, s);
        var second = view.ApplyView(raw, s);
        Assert.Same(first, second);   // cache hit — 같은 인스턴스
    }
}
```

- [ ] **Step 5.2: 테스트 실행 — 실패 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerViewTests"
```

Expected: 컴파일 에러 (`ContainerView` not defined).

- [ ] **Step 5.3: ContainerView 작성**

`src/LongYinRoster/Containers/ContainerView.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using LongYinRoster.UI;

namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — raw row list + SearchSortState → filtered/sorted view list (cached).
/// raw reference 또는 SearchSortState 가 변하면 재계산. 같으면 캐시 인스턴스 반환.
/// IL2CPP IMGUI 매 frame OnGUI 부담 ↓ 가 핵심 목적.
/// </summary>
public sealed class ContainerView
{
    private object?                     _lastRawRef;   // reference identity
    private SearchSortState?            _lastState;
    private List<ContainerPanel.ItemRow>? _cached;

    public List<ContainerPanel.ItemRow> ApplyView(List<ContainerPanel.ItemRow> raw, SearchSortState state)
    {
        if (raw == null) raw = new List<ContainerPanel.ItemRow>();
        if (state == null) state = SearchSortState.Default;

        if (object.ReferenceEquals(raw, _lastRawRef) && state.Equals(_lastState) && _cached != null)
            return _cached;

        IEnumerable<ContainerPanel.ItemRow> q = raw;

        if (!string.IsNullOrEmpty(state.Search))
        {
            string needle = state.Search;
            q = q.Where(r => (r.NameRaw ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        q = state.Key switch
        {
            SortKey.Category => q.OrderBy(r => r.CategoryKey ?? "").ThenBy(r => r.Index),
            SortKey.Name     => q.OrderBy(r => r.NameRaw ?? "").ThenBy(r => r.Index),
            SortKey.Grade    => q.OrderBy(r => r.GradeOrder).ThenBy(r => r.Index),
            SortKey.Quality  => q.OrderBy(r => r.QualityOrder).ThenBy(r => r.Index),
            _                => q.OrderBy(r => r.Index),
        };

        var result = q.ToList();
        if (!state.Ascending) result.Reverse();

        _lastRawRef = raw;
        _lastState  = state;
        _cached     = result;
        return result;
    }

    public void Invalidate()
    {
        _lastRawRef = null;
        _lastState  = null;
        _cached     = null;
    }
}
```

- [ ] **Step 5.4: 테스트 실행 — 통과 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerViewTests"
```

Expected: 5/5 PASS.

- [ ] **Step 5.5: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerView.cs src/LongYinRoster.Tests/ContainerViewTests.cs
git commit -m "feat(containers): ContainerView — filter+sort+cache (v0.7.2 D-3)"
```

---

## Task 6: SearchSortToolbar IMGUI 컴포넌트

**Files:**
- Create: `src/LongYinRoster/UI/SearchSortToolbar.cs`

**Note:** IMGUI 컴포넌트는 unit test 어려움 (Unity 의존). 컴파일 검증 + 인게임 smoke 로 검증. 다만 logic-only 부분 (state mutation 트리거) 은 SearchSortState mutator 가 이미 검증돼 있으므로 toolbar 는 입력 → state mutator 호출만 담당.

- [ ] **Step 6.1: SearchSortToolbar 작성**

`src/LongYinRoster/UI/SearchSortToolbar.cs`:

```csharp
using LongYinRoster.Containers;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.2 — IMGUI 1줄 검색·정렬 toolbar.
/// [TextField (~140)] [카테고리][이름][등급][품질] [▲ 또는 ▼]
/// 호스트 (ContainerPanel) 가 state 보유 + 변경 시 ContainerView.Invalidate.
///
/// IL2CPP strip 회피: GUILayout.TextField, GUILayout.Button(string), GUILayout.Label(string)
/// default skin 만 사용. GUIStyle 받는 overload 금지.
/// </summary>
public static class SearchSortToolbar
{
    /// <summary>
    /// state 를 in-place 가능 위치 — 입력 변경 시 새 SearchSortState 반환.
    /// 같은 frame 에서 반환값을 host state 에 할당.
    /// </summary>
    public static SearchSortState Draw(SearchSortState current, bool gradeQualityEnabled = true)
    {
        var result = current;
        GUILayout.BeginHorizontal();

        // 검색 box (폭 140)
        string newText = GUILayout.TextField(current.Search ?? "", GUILayout.Width(140));
        if (!ReferenceEquals(newText, current.Search) && newText != current.Search)
            result = result.WithSearch(newText);

        GUILayout.Space(4);

        // 정렬 키 4 segmented
        result = DrawKeyButton(result, SortKey.Category, "카테고리", 60);
        result = DrawKeyButton(result, SortKey.Name,     "이름",     50);
        result = DrawKeyButton(result, SortKey.Grade,    "등급",     50, gradeQualityEnabled);
        result = DrawKeyButton(result, SortKey.Quality,  "품질",     50, gradeQualityEnabled);

        GUILayout.Space(4);

        // 방향 토글
        string arrow = result.Ascending ? "▲" : "▼";
        if (GUILayout.Button(arrow, GUILayout.Width(28)))
            result = result.ToggleDirection();

        GUILayout.EndHorizontal();
        return result;
    }

    private static SearchSortState DrawKeyButton(SearchSortState s, SortKey key, string label, int width, bool enabled = true)
    {
        bool active = s.Key == key;
        var prevColor = GUI.color;
        var prevEnabled = GUI.enabled;
        if (!enabled) { GUI.enabled = false; }
        else if (active) { GUI.color = Color.cyan; }
        if (GUILayout.Button(label, GUILayout.Width(width)) && enabled)
            s = s.WithKey(key);
        GUI.color = prevColor;
        GUI.enabled = prevEnabled;
        return s;
    }
}
```

- [ ] **Step 6.2: 빌드 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 빌드 성공.

- [ ] **Step 6.3: 전체 테스트 회귀 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 123 PASS (111 + 12 신규 task 1+2+4+5).

- [ ] **Step 6.4: Commit**

```bash
git add src/LongYinRoster/UI/SearchSortToolbar.cs
git commit -m "feat(ui): SearchSortToolbar IMGUI — search box + 4 segmented + direction toggle (v0.7.2 D-3)"
```

---

## Task 7: ContainerPanel 통합 — 3-area state + cache + invalidate

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs`

**Note:** 가장 큰 변경 task. ContainerPanel 에 state 3개 + view 3개 추가, Set{Inv,Sto,Container}Rows 가 invalidate, DrawLeftColumn / DrawRightColumn 이 toolbar 그리고 view 사용. DrawItemList 가 raw 대신 view 받음.

- [ ] **Step 7.1: state + view 필드 추가**

`src/LongYinRoster/UI/ContainerPanel.cs` 의 private 필드 영역 (line 39~46 부근, `_inventoryRows` 직후):

```csharp
    // ... 기존 필드 ...
    private List<ItemRow> _inventoryRows = new();
    private List<ItemRow> _storageRows   = new();
    private List<ItemRow> _containerRows = new();
    // ... 기존 코드 ...

    // v0.7.2 D-3 — area 별 search/sort state + cached view
    private SearchSortState _invState   = SearchSortState.Default;
    private SearchSortState _stoState   = SearchSortState.Default;
    private SearchSortState _conState   = SearchSortState.Default;
    private readonly ContainerView _invView = new();
    private readonly ContainerView _stoView = new();
    private readonly ContainerView _conView = new();

    // v0.7.2 — Task 0 spike 결과로 enable/disable. 미발견 시 dropdown grade/quality 비활성 + 토스트 1회.
    private bool _gradeQualityEnabled = true;
    private bool _gradeQualityToastShown = false;
```

`using LongYinRoster.Containers;` 가 이미 file 상단에 있음 (line 3). `SearchSortState` / `ContainerView` 는 `LongYinRoster.Containers` namespace.

- [ ] **Step 7.2: SetXxxRows 가 cache invalidate**

기존 `SetInventoryRows` / `SetStorageRows` / `SetContainerRows` (line 82~84) 를 다음으로 교체:

```csharp
    public void SetInventoryRows(List<ItemRow> rows, float maxWeight = 964f)
    {
        _inventoryRows = rows;
        _inventoryChecks.Clear();
        _inventoryMaxWeight = maxWeight;
        _invView.Invalidate();
    }
    public void SetStorageRows(List<ItemRow> rows, float maxWeight = 300f)
    {
        _storageRows = rows;
        _storageChecks.Clear();
        _storageMaxWeight = maxWeight;
        _stoView.Invalidate();
    }
    public void SetContainerRows(List<ItemRow> rows)
    {
        _containerRows = rows;
        _containerChecks.Clear();
        _conView.Invalidate();
    }
```

- [ ] **Step 7.3: ContainerPanel.Visible 가 false → true 진입 시 state 초기화 메서드 추가**

영속성: 세션 휘발 (spec §3.5) — Visible 토글 시 default 복원하는 별도 method 는 도입하지 않는다. 사용자가 F11 닫고 재오픈해도 state 가 유지되는 것이 자연스러우면 그대로. spec 의 "F11 닫고 재오픈 시 toolbar 상태 초기화" 는 게임 reload 시점 reset 으로 해석 (Plugin 인스턴스가 새로 생성됨 = state default). 명시적 reset method 는 v0.7.6 영속화 시점에 도입 결정.

→ 이 step 은 no-op. 다음 step 으로.

- [ ] **Step 7.4: DrawLeftColumn 갱신 (인벤·창고 toolbar + view 사용)**

기존 `DrawLeftColumn` (line 164~192) 을 다음으로 교체:

```csharp
    private void DrawLeftColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));
        _leftColumnScroll = GUILayout.BeginScrollView(_leftColumnScroll, GUILayout.Height(640));

        // 인벤
        var invView = _invView.ApplyView(_inventoryRows, _invState);
        float invWeight = 0f;
        foreach (var r in _inventoryRows) invWeight += r.Weight;   // 라벨은 raw 기준 (전체 무게)
        GUILayout.Label(FormatCount(KoreanStrings.Lbl_Inventory, _inventoryRows.Count, invWeight, _inventoryMaxWeight, allowOvercap: true));
        var newInvState = SearchSortToolbar.Draw(_invState, _gradeQualityEnabled);
        if (!newInvState.Equals(_invState)) { _invState = newInvState; _invView.Invalidate(); }
        DrawItemList(invView, _inventoryChecks, ref _invScroll, 200);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnInventoryToContainerMove?.Invoke(new HashSet<int>(_inventoryChecks));
        if (GUILayout.Button("→ 복사")) OnInventoryToContainerCopy?.Invoke(new HashSet<int>(_inventoryChecks));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // 창고
        var stoView = _stoView.ApplyView(_storageRows, _stoState);
        float stoWeight = 0f;
        foreach (var r in _storageRows) stoWeight += r.Weight;
        GUILayout.Label(FormatCount(KoreanStrings.Lbl_Storage, _storageRows.Count, stoWeight, _storageMaxWeight, allowOvercap: false));
        var newStoState = SearchSortToolbar.Draw(_stoState, _gradeQualityEnabled);
        if (!newStoState.Equals(_stoState)) { _stoState = newStoState; _stoView.Invalidate(); }
        DrawItemList(stoView, _storageChecks, ref _stoScroll, 200);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnStorageToContainerMove?.Invoke(new HashSet<int>(_storageChecks));
        if (GUILayout.Button("→ 복사")) OnStorageToContainerCopy?.Invoke(new HashSet<int>(_storageChecks));
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }
```

list height 가 220 → 200 으로 축소 (toolbar 가 ~28px 차지).

- [ ] **Step 7.5: DrawRightColumn 갱신 (컨테이너 toolbar + view 사용)**

기존 `DrawRightColumn` 의 컨테이너 list 표시 부분 (line 275~276) 을 다음으로 교체:

```csharp
        var conView = _conView.ApplyView(_containerRows, _conState);
        GUILayout.Label($"{KoreanStrings.Lbl_Container} ({_containerRows.Count}개)");
        var newConState = SearchSortToolbar.Draw(_conState, _gradeQualityEnabled);
        if (!newConState.Equals(_conState)) { _conState = newConState; _conView.Invalidate(); }
        DrawItemList(conView, _containerChecks, ref _conScroll, 340);
```

list height 360 → 340 (toolbar 가 ~28px 차지).

- [ ] **Step 7.6: DrawItemList 가 view 를 받도록 인자 명시**

기존 `DrawItemList` (line 294~306) 는 그대로. caller 가 view 를 넘기므로 함수 시그니처 그대로 유효 (`List<ItemRow>` 받음). 단, 카테고리 탭 filter (`ItemCategoryFilter.Matches`) 가 이미 그 안에 있다 — search/sort 필터 후 카테고리 필터까지 적용. 순서: view (search+sort) → DrawItemList (category) → 표시. 의도된 조합.

→ 변경 불필요. step skip.

- [ ] **Step 7.7: 빌드 + 회귀 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 빌드 성공. 123 tests PASS (기존 회귀 없음).

- [ ] **Step 7.8: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): ContainerPanel — 3-area search/sort toolbar + cached view (v0.7.2 D-3)"
```

---

## Task 8: ModWindow / Plugin wiring — raw 변경 시 invalidate 보장

**Files:**
- Verify: `src/LongYinRoster/UI/ModWindow.cs` (Set{Inv,Sto,Container}Rows 호출처)

**Note:** Set 메소드가 이미 invalidate 호출 (Task 7.2) — 이 task 는 검증만. ModWindow 가 4-callback (이동·복사) 처리 후 raw 를 다시 채우면 자동 invalidate.

- [ ] **Step 8.1: ModWindow 의 callback handler 확인**

`src/LongYinRoster/UI/ModWindow.cs` 의 `OnContainerToInventoryMove` / `OnInventoryToContainerCopy` 등 callback 처리 후 `_containerPanel.SetInventoryRows(...)` 또는 `SetContainerRows(...)` 호출 path 가 있는지 확인:

```bash
grep -n "SetInventoryRows\|SetStorageRows\|SetContainerRows" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/UI/ModWindow.cs"
```

Expected: 4-callback 처리 후 source / destination 둘 다 SetXxxRows 호출하는 라인이 있음 (v0.7.0 wired). 없거나 한쪽만 있다면 다음 step.

- [ ] **Step 8.2: 누락된 invalidate path 보강 (필요시)**

만약 ModWindow 가 source 만 갱신하고 destination 안 갱신하면 (예: 컨테이너 → 인벤으로 이동 후 인벤 raw 만 갱신, 컨테이너 raw 미갱신), 양쪽 모두 SetXxxRows 호출 추가.

```csharp
// 예 (path 가 다르면 해당 callback 위치 따라 수정)
private void OnContainerToInventoryMove(HashSet<int> indices)
{
    // ... 기존 ContainerOps 처리 ...
    RefreshInventoryRows();   // 기존
    RefreshContainerRows();   // 신규 (또는 기존)
}
```

기존 v0.7.0 / v0.7.1 시점에 양방향 mirror 모두 raw refresh 호출이 명시돼있으면 변경 불요.

- [ ] **Step 8.3: 빌드 + 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 123 PASS.

- [ ] **Step 8.4: Commit (변경 있을 때만)**

```bash
# (변경 없으면 commit skip)
git add src/LongYinRoster/UI/ModWindow.cs
git commit -m "fix(ui): ensure raw refresh on both source and destination after 4-callback (v0.7.2 D-3)"
```

---

## Task 9: KoreanStrings 보강 + 인게임 smoke

**Files:**
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs` (정렬 dropdown / 검색 placeholder / 토스트)
- Test: 인게임 6/6 smoke

- [ ] **Step 9.1: KoreanStrings 추가**

`src/LongYinRoster/Util/KoreanStrings.cs` 의 컨테이너 섹션 (Lbl_Inventory 직후, line 105 부근) 에 추가:

```csharp
    // v0.7.2 D-3 검색·정렬
    public const string Tip_GradeQualityUnavailable = "등급/품질 reflection 미발견 — 정렬 불가 (spike 재시도 권장)";
    public const string Tip_SearchPlaceholder       = "이름 검색…";
```

(현재 SearchSortToolbar 가 placeholder 직접 표시 안 하므로 placeholder 는 미사용 — 추후 v0.7.5/v0.7.6 시점 적용. Tip_GradeQualityUnavailable 는 Task 9.3 에서 사용.)

- [ ] **Step 9.2: ContainerPanel 에서 spike 결과 fallback 토스트**

`src/LongYinRoster/UI/ContainerPanel.cs` 의 `Draw` (line 119) 안 try 블록 진입 시점:

```csharp
    private void Draw(int id)
    {
        try
        {
            // v0.7.2 D-3 — grade/quality reflection 미발견 1회 토스트
            if (!_gradeQualityEnabled && !_gradeQualityToastShown)
            {
                ToastService.Push(KoreanStrings.Tip_GradeQualityUnavailable, ToastKind.Info);
                _gradeQualityToastShown = true;
            }

            DialogStyle.FillBackground(_rect.width, _rect.height);
            // ... 기존 ...
```

`_gradeQualityEnabled` 플래그를 외부에서 설정할 수 있도록 setter 추가 (Task 7.1 의 필드 옆):

```csharp
    public void SetGradeQualityEnabled(bool enabled) { _gradeQualityEnabled = enabled; }
```

ModWindow 가 spike 결과 (Task 0 산출물) 로 결정 — ItemRow 의 GradeOrder / QualityOrder 가 모든 row 에서 -1 이면 disable. 단순한 휴리스틱:

```csharp
// ModWindow 의 RefreshInventoryRows 등에서 ContainerPanel 갱신 후
bool anyGradeOk = inventoryRows.Exists(r => r.GradeOrder >= 0) || storageRows.Exists(r => r.GradeOrder >= 0);
_containerPanel.SetGradeQualityEnabled(anyGradeOk);
```

(이는 Task 8 에서 같이 처리 가능. 위치는 RefreshInventoryRows 직후.)

- [ ] **Step 9.3: 빌드**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 빌드 성공.

- [ ] **Step 9.4: 게임 닫혔는지 확인 + dll 배포**

```bash
tasklist | grep -i LongYinLiZhiZhuan
# 결과 없으면 dll 자동 배포됨 (Release build target)
```

- [ ] **Step 9.5: BepInEx 로그 클리어 + 게임 실행**

```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
# 게임 실행 후 F11 → 컨테이너 관리
```

- [ ] **Step 9.6: 인게임 smoke 6/6 시나리오**

각 area (인벤·창고·컨테이너) 에 대해:

1. **검색 substring 매칭**: toolbar 검색 box 에 한자 일부 (예: "刀") 입력 → list 가 그 한자 포함 row 만 표시
2. **정렬 카테고리** (default): Type/SubType 묶음 순서로 정렬 확인
3. **정렬 이름**: 한자 사전식 정렬
4. **정렬 등급**: 0 (열악) → 5 (절세) 또는 -1 (미적용) 순서
5. **정렬 품질**: 동상
6. **방향 ▲/▼ 토글**: 같은 키에서 역순 표시
7. **양방향 이동**: 컨테이너 → 인벤 이동 후 인벤 list 자동 갱신 (정렬·검색 그대로 유지)
8. **F11 닫기/재오픈**: state 보존 (Plugin 인스턴스 유지) — 게임 종료/재실행 시만 default 복귀

`docs/superpowers/dumps/2026-05-XX-v0.7.2-smoke-results.md` 에 결과 기록:

```markdown
# v0.7.2 smoke 결과

| 시나리오 | 인벤 | 창고 | 컨테이너 |
|---|---|---|---|
| 1. 검색 한자 substring | ✓/✗ | ✓/✗ | ✓/✗ |
| 2. 정렬 카테고리 | ... | ... | ... |
| 3. 정렬 이름 | ... | ... | ... |
| 4. 정렬 등급 | ... | ... | ... |
| 5. 정렬 품질 | ... | ... | ... |
| 6. 방향 ▲/▼ | ... | ... | ... |
| 7. 4-callback 후 view 갱신 | ... | ... | ... |
| 8. F11 토글 후 state 보존 | ... | ... | ... |
```

- [ ] **Step 9.7: smoke 실패 시 lookback**

실패 시나리오 별:
- 검색 무동작: SearchSortToolbar.Draw 의 newText 비교 + WithSearch 호출 path 확인. 한자 IME 입력 strip 가능성 → 영문 substring 으로 우회 검증
- 등급/품질 disabled: Task 0 spike 결과 ItemReflector.GRADE_NAMES 후보 array 점검. 한자 enum 노출 시 GradeMap key 갱신
- 4-callback 후 stale: ModWindow callback handler 가 양쪽 SetXxxRows 호출하는지 (Task 8.2)
- toolbar 깨짐 (overflow): list height 200/340 vs 실제 area 높이 mismatch — DialogStyle / WindowRect 760 기준 재계산

수정 후 재 smoke.

- [ ] **Step 9.8: Commit**

```bash
git add src/LongYinRoster/Util/KoreanStrings.cs src/LongYinRoster/UI/ContainerPanel.cs docs/superpowers/dumps/2026-05-03-v0.7.2-smoke-results.md
git commit -m "test: v0.7.2 D-3 인게임 smoke 6/6 + grade/quality fallback toast"
```

---

## Task 10: 색상 row 표시 (선택 — spike 결과에 따라)

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs` (BuildLabel + DrawItemList)

**Note:** spec §4.9 의 후속 자산 — 등급 색상으로 row 텍스트 색상 적용. IL2CPP IMGUI strip-safe 인 `GUI.color` 만 사용 (`GUIStyle` 받지 않음). spike 결과 grade 노출되고 시각적 가치가 명확하면 추가, 아니면 skip 후 v0.7.3 D-2 와 합류.

- [ ] **Step 10.1: 색상 매핑 helper 추가**

`src/LongYinRoster/UI/ContainerPanel.cs` 에 static helper 추가 (BuildLabel 직후):

```csharp
    /// <summary>
    /// v0.7.2 D-3 — 등급 색상 매핑. 0~5: 회색·녹·하늘·보라·오렌지·빨강. 미발견(-1) → 흰색.
    /// IL2CPP IMGUI strip-safe (GUI.color 만 사용, GUIStyle ctor 우회).
    /// </summary>
    private static Color GradeColor(int gradeOrder) => gradeOrder switch
    {
        0 => new Color(0.61f, 0.64f, 0.69f),    // 회색  #9CA3AF
        1 => new Color(0.13f, 0.77f, 0.37f),    // 녹    #22C55E
        2 => new Color(0.22f, 0.74f, 0.97f),    // 하늘 #38BDF8
        3 => new Color(0.66f, 0.33f, 0.97f),    // 보라 #A855F7
        4 => new Color(0.98f, 0.45f, 0.09f),    // 오렌지 #F97316
        5 => new Color(0.94f, 0.27f, 0.27f),    // 빨강 #EF4444
        _ => Color.white,
    };
```

- [ ] **Step 10.2: DrawItemList 에서 row 별 색상 적용**

기존 DrawItemList (line 294~306) 의 toggle 그리기 부분에 색상 적용:

```csharp
    private void DrawItemList(List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
    {
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
        var prevColor = GUI.color;
        foreach (var r in rows)
        {
            if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;
            GUI.color = GradeColor(r.GradeOrder);
            bool was = checks.Contains(r.Index);
            bool now = GUILayout.Toggle(was, BuildLabel(r));
            GUI.color = prevColor;
            if (now && !was) checks.Add(r.Index);
            if (!now && was) checks.Remove(r.Index);
        }
        GUILayout.EndScrollView();
    }
```

(품질 마름모는 sprite 미가용 → v0.7.3 D-2 합류. v0.7.2 는 등급 텍스트 색상만.)

- [ ] **Step 10.3: 빌드 + 인게임 시각 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

게임 실행 → 컨테이너 관리 → row 들이 등급별 색상으로 표시되는지 확인 (회색 ~ 빨강 6단계).

- [ ] **Step 10.4: Commit (구현 시)**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): color row text by grade order (v0.7.2 D-3 — spec §4.9 색상 자산)"
```

---

## Task 11: HANDOFF / README / VERSION + release packaging

**Files:**
- Modify: `docs/HANDOFF.md` — §1 v0.7.2 release 항목 추가 + §6.B 매핑 갱신
- Modify: `README.md` — v0.7.2 검색·정렬 사용법 1단락
- Modify: `src/LongYinRoster/LongYinRoster.csproj` — VERSION 0.7.1 → 0.7.2

- [ ] **Step 11.1: VERSION bump**

`src/LongYinRoster/LongYinRoster.csproj` 의 `<Version>` 0.7.1 → 0.7.2.

```bash
grep -n "0.7.1\|<Version>" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/LongYinRoster.csproj"
```

해당 줄을 0.7.2 로 갱신.

- [ ] **Step 11.2: HANDOFF.md 갱신**

`docs/HANDOFF.md` 의 §1 release 목록에 v0.7.2 항목 추가:

```
- [v0.7.2](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.2) — D-3 컨테이너 검색·정렬: 3-area 독립 toolbar (검색 box + 4 sort key + ▲/▼) + cache. 등급 6단계 텍스트 색상 (Task 10 한 경우).
```

§6.B 의 후속 sub-project 매핑을 다음 순서로 갱신:
- v0.7.3 = D-2 아이콘 그리드
- v0.7.4 = D-1 Item 상세 panel
- v0.7.5 = D-4 Item 한글화

§7 컨텍스트 압축본도 v0.7.2 baseline 으로 갱신.

- [ ] **Step 11.3: README.md 사용법 추가**

README 의 컨테이너 관리 섹션에 1단락:

```markdown
### 검색·정렬 (v0.7.2)

각 list (인벤·창고·컨테이너) 상단 toolbar 에서:
- 검색 box: 한자 일부 입력으로 필터 (한글 검색은 v0.7.5 한글화 후 지원)
- [카테고리]/[이름]/[등급]/[품질] 버튼: 정렬 키 선택 (등급·품질은 reflection 발견 시 활성)
- ▲/▼ 토글: 오름·내림 전환

상태는 세션 휘발 (게임 재실행 시 초기화).
```

- [ ] **Step 11.4: 전체 빌드 + 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 빌드 성공 / **123/123 PASS** (111 baseline + 3 SearchSortState + 3 ItemReflector + 1 ContainerRowBuilder + 5 ContainerView). spec §10 의 122/122 목표는 1 oversubscribed (괜찮음).

- [ ] **Step 11.5: dist 패키지**

기존 v0.7.1 dist 구조 그대로:

```bash
mkdir -p "dist/LongYinRoster_v0.7.2/BepInEx/plugins/LongYinRoster"
cp "BepInEx/plugins/LongYinRoster/LongYinRoster.dll" "dist/LongYinRoster_v0.7.2/BepInEx/plugins/LongYinRoster/"
# 기존 README / 한글모드통팩 파일들 동상
```

PowerShell 으로 zip:

```powershell
Compress-Archive -Path "dist/LongYinRoster_v0.7.2/*" -DestinationPath "dist/LongYinRoster_v0.7.2.zip" -Force
```

- [ ] **Step 11.6: GitHub release**

```bash
gh release create v0.7.2 dist/LongYinRoster_v0.7.2.zip \
  --title "v0.7.2 — D-3 컨테이너 검색·정렬" \
  --notes-file release-notes-v0.7.2.md
```

`release-notes-v0.7.2.md` (수동 작성, 짧게):

```markdown
## v0.7.2 — D-3 컨테이너 검색·정렬

### 추가
- 인벤·창고·컨테이너 3-area 독립 검색 box + 정렬 dropdown (카테고리·이름·등급·품질) + ▲/▼ 방향
- 등급 6단계 텍스트 색상 (열악·회색 → 절세·빨강) — Task 10 적용 시
- ItemReflector — 개별 item reflection helper (Core)

### 알려진 한계
- 한글 검색은 v0.7.5 한글화 시점에 활성
- 등급/품질 reflection 미발견 시 dropdown 비활성 + 토스트 1회 안내
- 정렬·검색 상태는 세션 휘발 (v0.7.6 영속화 옵션 도입 검토)
```

- [ ] **Step 11.7: Commit + push + tag**

```bash
git add docs/HANDOFF.md README.md src/LongYinRoster/LongYinRoster.csproj
git commit -m "chore(release): v0.7.2 — VERSION + HANDOFF + README"
git tag v0.7.2
git push origin main --tags
```

---

## Self-Review

### Spec coverage check

| spec section | task |
|---|---|
| §3.1 검색 + 정렬 둘 다 | Task 1 (state) + Task 5 (view) + Task 6 (toolbar) |
| §3.2 3-area 적용 | Task 7 (state×3 + view×3 + toolbar×3) |
| §3.3 4 정렬 키 | Task 1 (enum) + Task 5 (switch 4-arm) + Task 6 (4 segmented) |
| §3.4 검색 OrdinalIgnoreCase | Task 5.3 ApplyView 의 IndexOf |
| §3.5 세션 휘발 | Task 7.3 (no-op — 명시적 reset 없음) |
| §3.6 비범위 (한글 검색·다중 키·강화 lv) | 명시적으로 미구현 (별도 sub-project) |
| §4.1 Approach α (cache + mirror) | Task 5 (cache) + Task 7 (mirror) |
| §4.2 ItemRow 4 신규 필드 | Task 3 |
| §4.3 SearchSortState | Task 1 |
| §4.4 SearchSortToolbar | Task 6 |
| §4.5 layout (1-row toolbar / list height 축소) | Task 7.4 (200) + Task 7.5 (340) |
| §4.6 데이터 흐름 (raw → filter → sort → view) | Task 5.3 ApplyView pseudocode 매칭 |
| §4.7 cache invalidate trigger | Task 7.2 (SetXxx) + Task 7.4/7.5 (toolbar 변경) + Task 8 (4-callback) |
| §4.8 Reflection spike | Task 0 |
| §4.9 색상 자산 (등급 텍스트 색상) | Task 10 (선택) |
| §4.10 한자→int 매핑 | Task 2.3 ItemReflector 의 GradeMap/QualityMap |
| §4.11 IL2CPP / 성능 가드 | Task 5 (cache) + Task 6 (default skin only) |
| §6.1 unit tests +11 | Task 1 (3) + 2 (3) + 4 (1) + 5 (5) = 12 |
| §6.2 인게임 smoke 6/6 | Task 9.6 (실제 8 시나리오, 6/6 minimum) |
| §7 위험·미지수 | Task 0 fallback / Task 9 IME 한계 |
| §8 완료 기준 | Task 9 (smoke) + Task 11 (release) |
| §9 release 후 contract | Task 11 |

→ 모든 spec 항목 task 매핑 ✓.

### Placeholder scan

- "TBD" / "TODO" / "implement later": 없음
- "Add appropriate error handling" / "handle edge cases": 없음 (각 task 가 명시 코드 보유)
- "Similar to Task N": 없음 (Step 4.4 의 IL2CPP path 도 코드 직접 명시)
- "Write tests for the above" without code: 없음 (모든 test 가 actual code)
- 정의되지 않은 type/method 참조: 검사 — `SearchSortState` (Task 1), `ContainerView` (Task 5), `ItemReflector` (Task 2), `SearchSortToolbar` (Task 6), `ItemRow.{CategoryKey,NameRaw,GradeOrder,QualityOrder}` (Task 3) 모두 task 안에서 정의됨 ✓

### Type consistency

- `SearchSortState.Default` (Task 1.4) ↔ `SearchSortState.Default` 호출 (Task 5, 7) ✓
- `WithSearch` / `WithKey` / `ToggleDirection` 시그니처 일관 ✓
- `SortKey.Category|Name|Grade|Quality` 4-arm switch (Task 5.3) ↔ Toolbar 4 segmented (Task 6.1) ↔ ContainerView 4-arm switch (Task 5.3) ✓
- `ContainerView.ApplyView(raw, state)` ↔ `ContainerView.Invalidate()` (Task 5.3, 7.2) ✓
- `ItemReflector.GetGradeOrder` / `GetQualityOrder` / `GetCategoryKey` / `GetNameRaw` (Task 2.3) ↔ caller (Task 4.4 의 IL2CPP path 는 GradeOrder/QualityOrder 만 사용; CategoryKey/NameRaw 는 inline `$"{type:D3}.{subType:D3}"` 와 `name` raw 그대로 사용 — 의도적, ItemReflector helper 호출 비용 절감)

→ 한 가지 약한 inconsistency: ItemReflector.GetCategoryKey / GetNameRaw 는 Task 2 에서 정의했으나 Task 4 에서 미사용. 이는 fixed string 합성이 더 간결하고 type/subType 이 이미 ContainerRowBuilder 변수로 있어서 helper 호출 불필요. 향후 다른 caller (예: ItemReflectorTests 가 직접 호출) 가 사용 가능하므로 helper 자체는 유지. self-review 결론: **유지 — inconsistency 아님, just unused-but-available helper**.

→ 모든 type/method 일관성 통과 ✓.

수정 사항 없음.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-05-03-longyin-roster-mod-v0.7.2-plan.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
