# v0.7.4 — Item 상세 panel (D-1) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ContainerPanel 의 cell 클릭 시 별도 `ItemDetailPanel` window 가 선택 item 의 sub-data wrapper 필드 (curated 한글 라벨 + raw fields) 를 표시한다. View-only — 수정 불가.

**Architecture:** v0.7.3 의 24×24 cell 을 invisible Button 으로 변경 → 글로벌 1 focus `(ContainerArea, int)?` 갱신. 신규 `ItemDetailReflector` 가 sub-data wrapper 별 curated (장비/비급/단약 우선) + raw fields 추출. 신규 `ItemDetailPanel` window 가 ContainerPanel 패턴 (DialogStyle + GUI.Window + drag + position persist) 모방. ContainerPanel toolbar 에 ⓘ 토글 추가. 신규 IMGUI primitive 미도입 — v0.7.3 검증된 strip-safe 패턴만.

**Tech Stack:** C# / .NET (BepInEx 6 IL2CPP), UnityEngine.GUI/GUILayout (default skin overload only — strip-safe), xUnit + Shouldly 단위 테스트.

**Spec:** [docs/superpowers/specs/2026-05-03-longyin-roster-mod-v0.7.4-design.md](../specs/2026-05-03-longyin-roster-mod-v0.7.4-design.md)

---

## Task 0: Spike — sub-data wrapper 필드 inventory (manual)

spike 결과 없이는 Task 3 (curated reflector) 가 정확한 필드명을 모름. 사용자 게임 협조 필요.

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs` — temporary [F12] handler add (release 직전 Task 9 에서 제거)
- Create: `docs/superpowers/dumps/2026-05-XX-v0.7.4-subdata-spike.md` — replace `XX` with actual day-of-month

- [ ] **Step 1: Add F12 handler to Plugin.cs**

`src/LongYinRoster/Plugin.cs` 의 `Update()` 또는 적절한 keypress handler 위치 찾기 (F11 handler 옆). 그 안에 추가:

```csharp
if (Input.GetKeyDown(KeyCode.F12))
{
    DumpSubDataFields();
}
```

신규 method 추가 (Plugin class 안):

```csharp
private void DumpSubDataFields()
{
    try
    {
        var hero = HeroLocator.GetHeroData();
        if (hero == null) { Util.Logger.Info("[v0.7.4 spike] hero null"); return; }

        var inv = HeroLocator.ReadObj(hero, "itemListData");
        if (inv == null) { Util.Logger.Info("[v0.7.4 spike] inv null"); return; }

        var allItem = HeroLocator.ReadObj(inv, "allItem");
        if (allItem == null) { Util.Logger.Info("[v0.7.4 spike] allItem null"); return; }

        int n = LongYinRoster.Containers.IL2CppListOps.Count(allItem);
        Util.Logger.Info($"[v0.7.4 spike] allItem count = {n}");

        // 각 카테고리 1+ item 의 sub-data wrapper 필드 dump
        var seenTypes = new System.Collections.Generic.HashSet<int>();
        var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        for (int i = 0; i < n && seenTypes.Count < 6; i++)
        {
            var item = LongYinRoster.Containers.IL2CppListOps.Get(allItem, i);
            if (item == null) continue;
            int type = System.Convert.ToInt32(HeroLocator.ReadObj(item, "type") ?? 0);
            if (seenTypes.Contains(type)) continue;
            seenTypes.Add(type);

            string name = (HeroLocator.ReadObj(item, "name") as string) ?? "?";
            Util.Logger.Info($"[v0.7.4 spike] === item idx={i} type={type} name={name} ===");

            // ItemData top-level fields (already known from v0.7.2 spike)
            DumpFields("[item]", item, bf);

            // sub-data wrappers
            string[] wrappers = { "equipmentData", "medFoodData", "bookData", "treasureData", "materialData", "horseData" };
            foreach (var wrapperName in wrappers)
            {
                var wrapper = HeroLocator.ReadObj(item, wrapperName);
                if (wrapper == null) continue;
                Util.Logger.Info($"[v0.7.4 spike]   [{wrapperName}] non-null");
                DumpFields($"  [{wrapperName}]", wrapper, bf);
            }
        }
        Util.Logger.Info($"[v0.7.4 spike] done — {seenTypes.Count}/6 categories sampled");
    }
    catch (System.Exception ex)
    {
        Util.Logger.Warn($"[v0.7.4 spike] threw: {ex.GetType().Name}: {ex.Message}");
    }
}

private void DumpFields(string prefix, object obj, System.Reflection.BindingFlags bf)
{
    var t = obj.GetType();
    foreach (var f in t.GetFields(bf))
    {
        try
        {
            var v = f.GetValue(obj);
            string s = v == null ? "null" : v.ToString();
            if (s != null && s.Length > 80) s = s.Substring(0, 80) + "...";
            Util.Logger.Info($"[v0.7.4 spike] {prefix}   {f.FieldType.Name} {f.Name} = {s}");
        }
        catch { /* skip unreadable */ }
    }
    foreach (var p in t.GetProperties(bf))
    {
        try
        {
            var v = p.GetValue(obj);
            string s = v == null ? "null" : v.ToString();
            if (s != null && s.Length > 80) s = s.Substring(0, 80) + "...";
            Util.Logger.Info($"[v0.7.4 spike] {prefix}   {p.PropertyType.Name} {p.Name} = {s}");
        }
        catch { /* skip unreadable */ }
    }
}
```

If `HeroLocator.ReadObj` is `private`, temporarily make it `internal` for this task or replicate the reflection inline.

- [ ] **Step 2: Build Release + verify game closed**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
tasklist | grep -i LongYinLiZhiZhuan
```
Expected: 출력 없음. 게임 실행중이면 사용자에게 종료 요청.

```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: Build succeeded, 0 errors. dll 자동 deploy.

- [ ] **Step 3: Clear log + 사용자에게 게임 실행 요청**

```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

사용자 메시지:
> "게임 실행 → 캐릭터 로드 → F12 1번 누르고 인벤 dump 받으세요. 인벤에 6 카테고리 (장비/단약/음식/비급/보물/재료/말) 다 있으면 좋고, 없으면 외부 디스크 컨테이너로 옮긴 sample 도 됩니다. dump 끝나면 알려주세요."

- [ ] **Step 4: Read dump from log + write spike dump file**

```bash
grep "v0.7.4 spike" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

결과를 `docs/superpowers/dumps/2026-05-XX-v0.7.4-subdata-spike.md` 에 정리 (실제 날짜 사용):

```markdown
# v0.7.4 D-1 — sub-data wrapper 필드 inventory spike

**일시**: 2026-05-XX
**dump 경로**: BepInEx/LogOutput.log 의 `[v0.7.4 spike]` 줄
**샘플**: 인벤토리 첫 N item (각 카테고리 1+)

## 카테고리별 sub-data wrapper 필드

### type=0 장비 (`equipmentData`)
| 필드명 | 타입 | sample 값 | 한글 라벨 후보 |
|---|---|---|---|
| enhanceLv | Int32 | 3 | 강화 lv |
| equiped | Boolean | True | 착용중 |
| ... (dump 결과 채움) | | | |

### type=2 단약/음식 (`medFoodData`)
...

### type=3 비급 (`bookData`)
...

### type=4 보물 (`treasureData`)
...

### type=5 재료 (`materialData`)
...

### type=6 말 (`horseData`)
...

## ItemReflector 갱신 후보

(spike 결과 기반 ItemDetailReflector 의 카테고리별 helper 매핑 결정)

## 한계 / 검증 필요

- 샘플 부족한 카테고리: ...
- IL2CPP unbox 실패 필드: ...
```

- [ ] **Step 5: Commit spike + dump**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Plugin.cs docs/superpowers/dumps/
git commit -m "spike: v0.7.4 D-1 sub-data wrapper 필드 inventory"
```

[F12] handler 는 release 직전 Task 9 에서 제거.

---

## Task 1: ContainerArea enum (신규 모듈)

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerArea.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` — add `<Compile Include="../LongYinRoster/Containers/ContainerArea.cs">` per Task 1 csproj pattern from v0.7.3

이 enum 자체는 단위 테스트 가치 적음 (3-value enum) — Test 만 csproj include 만.

- [ ] **Step 1: Create enum file**

`src/LongYinRoster/Containers/ContainerArea.cs`:

```csharp
namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.4 D-1 — ContainerPanel 의 3 area 식별자.
/// `ContainerPanel._focus = (Area, Index)?` 글로벌 focus tuple 의 첫 번째 component.
/// </summary>
public enum ContainerArea
{
    Inventory,
    Storage,
    Container,
}
```

- [ ] **Step 2: Add csproj entry**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 의 Containers-grouped Compile entries (search for `<Link>Containers/`). 알파벳 순으로 추가:

```xml
<Compile Include="../LongYinRoster/Containers/ContainerArea.cs">
  <Link>Containers/ContainerArea.cs</Link>
</Compile>
```

- [ ] **Step 3: Verify build + tests still pass**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: Build OK + 170/170 PASS (no new tests yet).

- [ ] **Step 4: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Containers/ContainerArea.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(containers): v0.7.4 D-1 — ContainerArea enum"
```

---

## Task 2: ItemDetailReflector.GetRawFields (TDD)

raw fields enumeration + IL2CPP wrapper meta 필터 + 비활성 sub-data wrapper 제외. 이 부분은 spike 와 무관 — 단순 reflection 로직.

**Files:**
- Create: `src/LongYinRoster/Core/ItemDetailReflector.cs` (raw method only — Task 3 에서 curated 추가)
- Test: `src/LongYinRoster.Tests/ItemDetailReflectorRawTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`

- [ ] **Step 1: Write failing tests**

Create `src/LongYinRoster.Tests/ItemDetailReflectorRawTests.cs`:

```csharp
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemDetailReflectorRawTests
{
    private sealed class FakeEquipmentData
    {
        public int enhanceLv = 3;
        public bool equiped = true;
        // IL2CPP wrapper meta — should be filtered
        public System.IntPtr Pointer = (System.IntPtr)123;
        public string ObjectClass = "stub";
    }

    private sealed class FakeBookData
    {
        public int learnLv = 5;
        public int maxLearnLv = 10;
    }

    private sealed class FakeItem
    {
        public string name = "多情飞刀";
        public int type = 0;
        public int subType = 0;
        public int itemLv = 5;
        public int rareLv = 5;
        public float weight = 2.5f;
        public FakeEquipmentData equipmentData = new();
        public FakeBookData bookData = null!;   // type=0 inactive book
        // IL2CPP wrapper meta on item itself
        public System.IntPtr Pointer = (System.IntPtr)456;
    }

    [Fact]
    public void GetRawFields_NullItem_ReturnsEmpty()
    {
        ItemDetailReflector.GetRawFields(null).ShouldBeEmpty();
    }

    [Fact]
    public void GetRawFields_DumpsItemFields()
    {
        var item = new FakeItem();
        var raw = ItemDetailReflector.GetRawFields(item);
        raw.ShouldContain(x => x.FieldName == "name" && x.Value == "多情飞刀");
        raw.ShouldContain(x => x.FieldName == "type" && x.Value == "0");
        raw.ShouldContain(x => x.FieldName == "weight" && x.Value == "2.5");
    }

    [Fact]
    public void GetRawFields_FiltersIL2CppMeta()
    {
        var item = new FakeItem();
        var raw = ItemDetailReflector.GetRawFields(item);
        raw.ShouldNotContain(x => x.FieldName == "Pointer");
        raw.ShouldNotContain(x => x.FieldName == "ObjectClass");
    }

    [Fact]
    public void GetRawFields_DumpsActiveSubDataOnly()
    {
        var item = new FakeItem();   // type=0 → equipmentData active, bookData null
        var raw = ItemDetailReflector.GetRawFields(item);
        raw.ShouldContain(x => x.FieldName == "[equipmentData] enhanceLv" && x.Value == "3");
        raw.ShouldContain(x => x.FieldName == "[equipmentData] equiped" && x.Value == "True");
        raw.ShouldNotContain(x => x.FieldName.StartsWith("[bookData]"));
    }
}
```

- [ ] **Step 2: Add csproj entry + run failing test**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 의 Core-grouped Compile entries 에 추가:

```xml
<Compile Include="../LongYinRoster/Core/ItemDetailReflector.cs">
  <Link>Core/ItemDetailReflector.cs</Link>
</Compile>
```

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ItemDetailReflectorRawTests
```
Expected: 컴파일 실패 (`ItemDetailReflector` 미존재).

- [ ] **Step 3: Implement minimal raw method**

Create `src/LongYinRoster/Core/ItemDetailReflector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.4 D-1 — ItemData + sub-data wrapper reflection helper.
/// `GetCuratedFields` 는 카테고리별 한글 라벨 매핑 (장비/비급/단약 우선 — Task 3).
/// `GetRawFields` 는 모든 reflection 필드 dump + IL2CPP wrapper meta 필터.
/// 본 module 은 UI 와 무관 — 단위 테스트 가능.
/// </summary>
public static class ItemDetailReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly HashSet<string> WRAPPER_META = new()
    {
        "ObjectClass", "Pointer", "WasCollected", "isWrapped", "pooledPtr",
    };

    private static readonly string[] SUBDATA_WRAPPERS =
    {
        "equipmentData", "medFoodData", "bookData", "treasureData", "materialData", "horseData",
    };

    /// <summary>
    /// item + 활성화 sub-data wrapper 의 모든 reflection 필드 dump.
    /// IL2CPP wrapper meta 필터, 비활성 wrapper 제외.
    /// </summary>
    public static List<(string FieldName, string Value)> GetRawFields(object? item)
    {
        var result = new List<(string, string)>();
        if (item == null) return result;

        DumpFields(result, "", item);
        foreach (var wrapperName in SUBDATA_WRAPPERS)
        {
            var wrapper = ReadFieldOrProperty(item.GetType(), item, wrapperName);
            if (wrapper == null) continue;   // inactive
            DumpFields(result, $"[{wrapperName}] ", wrapper);
        }
        return result;
    }

    private static void DumpFields(List<(string, string)> result, string prefix, object obj)
    {
        var t = obj.GetType();
        foreach (var f in t.GetFields(F))
        {
            if (WRAPPER_META.Contains(f.Name)) continue;
            if (Array.IndexOf(SUBDATA_WRAPPERS, f.Name) >= 0) continue;   // wrapper itself dumped via prefix path
            string val;
            try { val = f.GetValue(obj)?.ToString() ?? "null"; }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            result.Add(($"{prefix}{f.Name}", val));
        }
        foreach (var p in t.GetProperties(F))
        {
            if (WRAPPER_META.Contains(p.Name)) continue;
            if (p.GetIndexParameters().Length > 0) continue;   // skip indexers
            string val;
            try { val = p.GetValue(obj)?.ToString() ?? "null"; }
            catch (Exception ex) { val = $"<unreadable: {ex.GetType().Name}>"; }
            result.Add(($"{prefix}{p.Name}", val));
        }
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
        catch { /* swallow */ }
        return null;
    }
}
```

- [ ] **Step 4: Run tests + verify 4/4 PASS**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ItemDetailReflectorRawTests
```
Expected: 4/4 PASS.

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 174/174 PASS (170 + 4 raw).

- [ ] **Step 5: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Core/ItemDetailReflector.cs src/LongYinRoster.Tests/ItemDetailReflectorRawTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(core): v0.7.4 D-1 — ItemDetailReflector.GetRawFields (raw dump + IL2CPP meta filter)"
```

---

## Task 3: ItemDetailReflector.GetCuratedFields (TDD)

장비 / 비급 / 단약 3 카테고리만 우선 cover. 미지원 카테고리는 빈 list. spike 결과 (Task 0) 기반 정확한 필드명.

**Files:**
- Modify: `src/LongYinRoster/Core/ItemDetailReflector.cs` — `GetCuratedFields` + 3 private helper 추가
- Modify: `src/LongYinRoster.Tests/ItemDetailReflectorRawTests.cs` 와 같은 file 에 추가 OR 신규 파일 — 신규 `ItemDetailReflectorCuratedTests.cs` 권장

**Note**: 본 task 의 정확한 필드명은 Task 0 spike 결과 의존. 아래 코드는 spike 가설 (장비 = enhanceLv/equiped/속성, 비급 = learnLv/maxLearnLv, 단약 = effectValue/duration). spike 후 실제 필드명으로 정정 가능 — 테스트 fake item 의 필드명도 같이 갱신.

- [ ] **Step 1: Write failing tests**

Create `src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs`:

```csharp
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemDetailReflectorCuratedTests
{
    // ===== Equipment (type=0) =====
    private sealed class FakeEquipmentData { public int enhanceLv = 3; public bool equiped = true; }
    private sealed class FakeEquipmentItem
    {
        public string name = "多情飞刀";
        public int type = 0;
        public int subType = 0;
        public int itemLv = 5;
        public int rareLv = 5;
        public float weight = 2.5f;
        public int value = 32000;
        public FakeEquipmentData equipmentData = new();
    }

    [Fact]
    public void GetCuratedFields_Equipment_ReturnsLabeledFields()
    {
        var item = new FakeEquipmentItem();
        var curated = ItemDetailReflector.GetCuratedFields(item);
        curated.ShouldNotBeEmpty();
        curated.ShouldContain(x => x.Label == "강화" && x.Value == "+3");
        curated.ShouldContain(x => x.Label == "착용중" && x.Value == "예");
        curated.ShouldContain(x => x.Label == "무게" && x.Value == "2.5 kg");
    }

    // ===== Book (type=3) =====
    private sealed class FakeBookData { public int learnLv = 5; public int maxLearnLv = 10; }
    private sealed class FakeBookItem
    {
        public string name = "九阳神功";
        public int type = 3;
        public int subType = 0;
        public int itemLv = 4;
        public int rareLv = 4;
        public float weight = 1.0f;
        public int value = 50000;
        public FakeBookData bookData = new();
    }

    [Fact]
    public void GetCuratedFields_Book_ReturnsLabeledFields()
    {
        var item = new FakeBookItem();
        var curated = ItemDetailReflector.GetCuratedFields(item);
        curated.ShouldNotBeEmpty();
        curated.ShouldContain(x => x.Label == "학습 lv" && x.Value == "5 / 10");
    }

    // ===== Med/Food (type=2) =====
    private sealed class FakeMedFoodData { public int effectValue = 100; }
    private sealed class FakeMedFoodItem
    {
        public string name = "九转还魂丹";
        public int type = 2;
        public int subType = 0;
        public int itemLv = 5;
        public int rareLv = 5;
        public float weight = 0.5f;
        public int value = 8000;
        public FakeMedFoodData medFoodData = new();
    }

    [Fact]
    public void GetCuratedFields_MedFood_ReturnsLabeledFields()
    {
        var item = new FakeMedFoodItem();
        var curated = ItemDetailReflector.GetCuratedFields(item);
        curated.ShouldNotBeEmpty();
        curated.ShouldContain(x => x.Label == "효과");
    }

    // ===== Unsupported categories =====
    private sealed class FakeTreasureItem { public int type = 4; }
    private sealed class FakeMaterialItem { public int type = 5; }
    private sealed class FakeHorseItem { public int type = 6; }

    [Fact]
    public void GetCuratedFields_Treasure_ReturnsEmpty()
    {
        ItemDetailReflector.GetCuratedFields(new FakeTreasureItem()).ShouldBeEmpty();
    }

    [Fact]
    public void GetCuratedFields_Material_ReturnsEmpty()
    {
        ItemDetailReflector.GetCuratedFields(new FakeMaterialItem()).ShouldBeEmpty();
    }

    [Fact]
    public void GetCuratedFields_NullItem_ReturnsEmpty()
    {
        ItemDetailReflector.GetCuratedFields(null).ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run failing tests**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ItemDetailReflectorCuratedTests
```
Expected: 컴파일 실패 (`GetCuratedFields` 미존재).

- [ ] **Step 3: Add GetCuratedFields + 3 helpers**

Edit `src/LongYinRoster/Core/ItemDetailReflector.cs` — `GetRawFields` 메서드 위에 추가:

```csharp
    /// <summary>
    /// 카테고리별 의미 있는 필드 → (한글 라벨, 표시 값) tuple list.
    /// 우선 cover: type=0 장비 / type=2 단약·음식 / type=3 비급.
    /// 미지원 카테고리 (treasure/material/horse) 는 빈 list — caller 가 raw fallback.
    /// </summary>
    public static List<(string Label, string Value)> GetCuratedFields(object? item)
    {
        if (item == null) return new();
        int type = ReadInt(item, "type");
        return type switch
        {
            0 => GetEquipmentDetails(item),
            2 => GetMedFoodDetails(item),
            3 => GetBookDetails(item),
            _ => new(),   // type=4/5/6 + unknown
        };
    }

    private static List<(string, string)> GetEquipmentDetails(object item)
    {
        var result = new List<(string, string)>();
        var ed = ReadFieldOrProperty(item.GetType(), item, "equipmentData");
        int enh = ed != null ? ReadInt(ed, "enhanceLv") : 0;
        bool equipped = ed != null && ReadBool(ed, "equiped");
        if (enh > 0) result.Add(("강화", $"+{enh}"));
        result.Add(("착용중", equipped ? "예" : "아니오"));
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        // 추가 필드는 spike 결과로 확장
        return result;
    }

    private static List<(string, string)> GetBookDetails(object item)
    {
        var result = new List<(string, string)>();
        var bd = ReadFieldOrProperty(item.GetType(), item, "bookData");
        if (bd != null)
        {
            int lv = ReadInt(bd, "learnLv");
            int max = ReadInt(bd, "maxLearnLv");
            result.Add(("학습 lv", $"{lv} / {max}"));
        }
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    private static List<(string, string)> GetMedFoodDetails(object item)
    {
        var result = new List<(string, string)>();
        var mf = ReadFieldOrProperty(item.GetType(), item, "medFoodData");
        if (mf != null)
        {
            int eff = ReadInt(mf, "effectValue");
            result.Add(("효과", eff.ToString()));
        }
        result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
        result.Add(("가격", ReadInt(item, "value").ToString()));
        return result;
    }

    private static int ReadInt(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj.GetType(), obj, name);
        if (v == null) return 0;
        try { return Convert.ToInt32(v); } catch { return 0; }
    }
    private static float ReadFloat(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj.GetType(), obj, name);
        if (v == null) return 0f;
        try { return Convert.ToSingle(v); } catch { return 0f; }
    }
    private static bool ReadBool(object obj, string name)
    {
        var v = ReadFieldOrProperty(obj.GetType(), obj, name);
        return v is bool b && b;
    }
```

- [ ] **Step 4: Add csproj entry for new test file**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 에는 같은 디렉토리의 모든 `.cs` 가 자동으로 컴파일됨 (test 파일은 직접 directory) — csproj 변경 불필요. 단지 `ItemDetailReflector.cs` 의 csproj 항목은 Task 2 에서 이미 추가됨.

- [ ] **Step 5: Run tests**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ItemDetailReflectorCuratedTests
```
Expected: 6/6 PASS.

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 180/180 PASS (170 + 4 raw + 6 curated).

- [ ] **Step 6: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Core/ItemDetailReflector.cs src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs
git commit -m "feat(core): v0.7.4 D-1 — ItemDetailReflector.GetCuratedFields (장비/비급/단약 우선)"
```

---

## Task 4: ItemCellRenderer.DrawAtRect overload

cell 렌더링을 인자 rect 에 overlay 하는 overload. Task 5 에서 ContainerPanel.DrawItemList 가 Button 의 GetLastRect 결과를 인자로 넘겨줄 때 사용.

**Files:**
- Modify: `src/LongYinRoster/UI/ItemCellRenderer.cs`

`Draw(ContainerPanel.ItemRow r, int size)` 의 변형 — `GUILayoutUtility.GetRect` 호출 없이 인자 rect 받음.

- [ ] **Step 1: Add DrawAtRect method**

`src/LongYinRoster/UI/ItemCellRenderer.cs` 의 `Draw` 메서드 아래에 추가:

```csharp
    /// <summary>
    /// v0.7.4 D-1 — 인자 rect 에 cell overlay.
    /// 호출자가 이미 rect 영역을 layout 으로 잡아둔 경우 사용 (예: ContainerPanel
    /// DrawItemList 의 invisible Button 이 자리 잡고 GetLastRect 로 rect 받은 후 overlay).
    /// `Draw(r, size)` 와 동일 기능 — layout 자리 잡기 단계만 생략.
    /// </summary>
    public static void DrawAtRect(ContainerPanel.ItemRow r, Rect rect)
    {
        var prevColor = GUI.color;

        // 배경 — GradeColor (alpha 0.6)
        GUI.color = GradeBackground(r.GradeOrder);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        // 중앙 카테고리 한자
        GUI.Label(rect, CategoryGlyph.For(r.Type, r.SubType));

        // 우상단 품질 마름모
        if (r.QualityOrder >= 0)
        {
            GUI.color = QualityColor(r.QualityOrder);
            GUI.DrawTexture(new Rect(rect.xMax - 9, rect.yMin + 1, 8, 8), Texture2D.whiteTexture);
            GUI.color = prevColor;
        }

        // 우하단 강화
        var badge = BadgeText(r.EnhanceLv);
        if (!string.IsNullOrEmpty(badge))
            GUI.Label(new Rect(rect.xMax - 18, rect.yMax - 14, 18, 14), badge);

        // 좌하단 착용중
        var marker = EquippedMarker(r.Equipped);
        if (!string.IsNullOrEmpty(marker))
            GUI.Label(new Rect(rect.xMin + 1, rect.yMax - 14, 14, 14), marker);
    }
```

- [ ] **Step 2: Build + run all tests**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: Build OK + 180/180 PASS (no new tests).

- [ ] **Step 3: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/UI/ItemCellRenderer.cs
git commit -m "feat(ui): v0.7.4 D-1 — ItemCellRenderer.DrawAtRect overload"
```

---

## Task 5: ContainerPanel — focus state + cell Button + ⓘ 토글

복합 task — `_focus` 신규 필드 + cell 을 invisible Button 으로 변경 + Set*Rows 에 raw item paired source 추가 + GetFocusedRawItem helper + toolbar 에 ⓘ 토글.

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs`
- Test: `src/LongYinRoster.Tests/ContainerPanelFocusTests.cs`

`_itemDetailPanel` reference 는 Plugin.cs 가 wire-up (Task 7). ContainerPanel 자체는 panel 의 Visible 토글 callback 으로 추상화.

- [ ] **Step 1: Write failing focus tests**

Create `src/LongYinRoster.Tests/ContainerPanelFocusTests.cs`:

```csharp
using LongYinRoster.Containers;
using LongYinRoster.UI;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerPanelFocusTests
{
    private static ContainerPanel.ItemRow Row(int idx) => new ContainerPanel.ItemRow
    {
        Index = idx, Name = $"item{idx}", Type = 0, SubType = 0, EnhanceLv = 0, Weight = 1f, Equipped = false,
    };

    [Fact]
    public void GetFocusedRawItem_NoFocus_ReturnsNull()
    {
        var panel = new ContainerPanel();
        panel.GetFocusedRawItem().ShouldBeNull();
    }

    [Fact]
    public void GetFocusedRawItem_OOB_ClearsAndReturnsNull()
    {
        var panel = new ContainerPanel();
        var rows = new List<ContainerPanel.ItemRow> { Row(0), Row(1) };
        var raw = new List<object> { "item0", "item1" };
        panel.SetInventoryRows(rows, raw);
        panel.SetFocus(ContainerArea.Inventory, 5);   // OOB
        panel.GetFocusedRawItem().ShouldBeNull();
        panel.HasFocus.ShouldBeFalse();   // auto-clear
    }
}
```

- [ ] **Step 2: Run failing test**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ContainerPanelFocusTests
```
Expected: 컴파일 실패 — `SetInventoryRows(rows, raw)` 시그니처 부재 + `SetFocus`/`HasFocus`/`GetFocusedRawItem` method 부재.

- [ ] **Step 3: Modify ContainerPanel — add focus state + raw item paired sources**

`src/LongYinRoster/UI/ContainerPanel.cs` 의 state 필드 영역 (line 33~70 부근) 에 추가:

```csharp
    // v0.7.4 D-1 — 글로벌 1 focus + raw item paired sources
    private (ContainerArea Area, int Index)? _focus;
    private List<object> _inventoryRawItems = new();
    private List<object> _storageRawItems   = new();
    private List<object> _containerRawItems = new();

    // ItemDetailPanel toggle callback — Plugin.cs 가 wire-up.
    public Action? OnToggleItemDetailPanel;
    public Func<bool>? IsItemDetailPanelVisible;

    public bool HasFocus => _focus.HasValue;
    public (ContainerArea Area, int Index)? Focus => _focus;
    public void SetFocus(ContainerArea area, int index) => _focus = (area, index);
    public void ClearFocus() => _focus = null;

    public object? GetFocusedRawItem()
    {
        if (_focus is not (var area, var idx)) return null;
        var raw = area switch
        {
            ContainerArea.Inventory => _inventoryRawItems,
            ContainerArea.Storage   => _storageRawItems,
            ContainerArea.Container => _containerRawItems,
            _ => null,
        };
        if (raw == null || idx < 0 || idx >= raw.Count) { _focus = null; return null; }
        return raw[idx];
    }
```

`SetInventoryRows`/`SetStorageRows`/`SetContainerRows` 시그니처 변경 + `_focus` invalidation:

기존 `SetInventoryRows`:
```csharp
public void SetInventoryRows(List<ItemRow> rows, float maxWeight = 964f)
{
    _inventoryRows = rows;
    _inventoryChecks.Clear();
    _inventoryMaxWeight = maxWeight;
    _invView.Invalidate();
}
```

신규:
```csharp
public void SetInventoryRows(List<ItemRow> rows, List<object> rawItems, float maxWeight = 964f)
{
    _inventoryRows = rows;
    _inventoryRawItems = rawItems;
    _inventoryChecks.Clear();
    _inventoryMaxWeight = maxWeight;
    _invView.Invalidate();
    if (_focus is (ContainerArea.Inventory, var idx) && (idx < 0 || idx >= rawItems.Count)) _focus = null;
}
```

`SetStorageRows` / `SetContainerRows` 도 같은 패턴 (`_storageRawItems` / `_containerRawItems`, area 변경, maxWeight 또는 없음).

- [ ] **Step 4: Modify DrawItemList — cell click + focus visual**

기존 `DrawItemList` (v0.7.3 fallback 후 단계):
```csharp
private void DrawItemList(List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
{
    scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
    var prevColor = GUI.color;
    foreach (var r in rows)
    {
        if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;

        GUILayout.BeginHorizontal();
        ItemCellRenderer.Draw(r, size: 24);
        GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder);
        bool was = checks.Contains(r.Index);
        bool now = GUILayout.Toggle(was, BuildLabel(r));
        GUI.color = prevColor;
        GUILayout.EndHorizontal();
        if (now && !was) checks.Add(r.Index);
        if (!now && was) checks.Remove(r.Index);
    }
    GUILayout.EndScrollView();
}
```

`DrawItemList` 의 시그니처에 `area` 인자 추가:
```csharp
private void DrawItemList(ContainerArea area, List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
{
    scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
    var prevColor = GUI.color;
    foreach (var r in rows)
    {
        if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;

        GUILayout.BeginHorizontal();

        // cell = invisible Button + overlay (v0.7.4 D-1)
        bool cellClicked = GUILayout.Button("", GUILayout.Width(24), GUILayout.Height(24));
        var cellRect = GUILayoutUtility.GetLastRect();
        ItemCellRenderer.DrawAtRect(r, cellRect);
        if (_focus is (var fa, var fi) && fa == area && fi == r.Index)
            DrawFocusOutline(cellRect);
        if (cellClicked) _focus = (area, r.Index);

        GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder);
        bool was = checks.Contains(r.Index);
        bool now = GUILayout.Toggle(was, BuildLabel(r));
        GUI.color = prevColor;
        GUILayout.EndHorizontal();
        if (now && !was) checks.Add(r.Index);
        if (!now && was) checks.Remove(r.Index);
    }
    GUILayout.EndScrollView();
}

private static void DrawFocusOutline(Rect rect)
{
    var prev = GUI.color;
    GUI.color = Color.cyan;
    GUI.DrawTexture(new Rect(rect.xMin,     rect.yMin,     rect.width, 1), Texture2D.whiteTexture);
    GUI.DrawTexture(new Rect(rect.xMin,     rect.yMax - 1, rect.width, 1), Texture2D.whiteTexture);
    GUI.DrawTexture(new Rect(rect.xMin,     rect.yMin,     1,          rect.height), Texture2D.whiteTexture);
    GUI.DrawTexture(new Rect(rect.xMax - 1, rect.yMin,     1,          rect.height), Texture2D.whiteTexture);
    GUI.color = prev;
}
```

`DrawLeftColumn` / `DrawRightColumn` 의 호출 site 도 인자 추가:
- `DrawItemList(invView, _inventoryChecks, ref _invScroll, 220);` → `DrawItemList(ContainerArea.Inventory, invView, _inventoryChecks, ref _invScroll, 220);`
- `DrawItemList(stoView, _storageChecks, ref _stoScroll, 220);` → `DrawItemList(ContainerArea.Storage, stoView, _storageChecks, ref _stoScroll, 220);`
- `DrawItemList(conView, _containerChecks, ref _conScroll, 500);` → `DrawItemList(ContainerArea.Container, conView, _containerChecks, ref _conScroll, 500);`

- [ ] **Step 5: Add ⓘ 토글 to toolbar**

`DrawGlobalToolbar` 메서드 수정 — `SearchSortToolbar.Draw` 호출 후에 ⓘ 추가:

```csharp
private void DrawGlobalToolbar()
{
    GUILayout.BeginHorizontal();
    var newState = SearchSortToolbar.Draw(_globalState, _gradeQualityEnabled);
    if (!newState.Equals(_globalState))
    {
        _globalState = newState;
        _invView.Invalidate();
        _stoView.Invalidate();
        _conView.Invalidate();
    }

    GUILayout.Space(4);
    bool detailVisible = IsItemDetailPanelVisible?.Invoke() ?? false;
    var prevColor = GUI.color;
    if (detailVisible) GUI.color = Color.cyan;
    if (GUILayout.Button("ⓘ 상세", GUILayout.Width(60)))
        OnToggleItemDetailPanel?.Invoke();
    GUI.color = prevColor;

    GUILayout.EndHorizontal();
}
```

`SearchSortToolbar.Draw` 가 자체 BeginHorizontal/EndHorizontal 을 가진 경우 그대로 두고 ⓘ 만 별도 BeginHorizontal — 또는 `SearchSortToolbar.Draw` 가 horizontal 안 묶으면 `DrawGlobalToolbar` 가 묶음. 기존 `SearchSortToolbar.cs` 확인:

```bash
grep -n "BeginHorizontal\|EndHorizontal" src/LongYinRoster/UI/SearchSortToolbar.cs
```

`SearchSortToolbar.Draw` 가 자체 BeginHorizontal 으로 한 줄로 그리면, `DrawGlobalToolbar` 는 `SearchSortToolbar.Draw` 후 `GUILayout.Button("ⓘ 상세")` 를 별도 줄에 그리거나 — 또는 `SearchSortToolbar.Draw` 시그니처에 `extraButton: Action?` 인자 추가해 한 줄에 합치기. **추천: 별도 줄** (toolbar 가 482px 이미 차지, ⓘ 60px 추가하면 542px — 공간은 OK 지만 BeginHorizontal 분리가 코드 안정).

위 코드 수정: SearchSortToolbar 가 자체 BeginHorizontal 갖는다면 `DrawGlobalToolbar` 는 한 줄로 묶는 BeginHorizontal 제거 + 두 줄로:
```csharp
private void DrawGlobalToolbar()
{
    var newState = SearchSortToolbar.Draw(_globalState, _gradeQualityEnabled);
    if (!newState.Equals(_globalState)) { ... }

    // ⓘ 상세 — 두 번째 줄
    GUILayout.BeginHorizontal();
    bool detailVisible = IsItemDetailPanelVisible?.Invoke() ?? false;
    var prevColor = GUI.color;
    if (detailVisible) GUI.color = Color.cyan;
    if (GUILayout.Button("ⓘ 상세", GUILayout.Width(60)))
        OnToggleItemDetailPanel?.Invoke();
    GUI.color = prevColor;
    GUILayout.EndHorizontal();
}
```

(2-row toolbar — 작은 공간 부담 OK)

- [ ] **Step 6: Run tests**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ContainerPanelFocusTests
```
Expected: 2/2 PASS.

```
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 182/182 PASS.

기존 `ContainerPanelFormatTests` 등이 `SetInventoryRows` 호출 시 시그니처 변경되어 컴파일 오류 가능 — 그 호출 site 도 `rawItems` 추가 (test 에서는 `new List<object>()` 빈 list 사용 가능 — focus tests 외에는 raw items 사용 안 함).

기존 test 파일 검색:
```bash
grep -rn "SetInventoryRows\|SetStorageRows\|SetContainerRows" src/LongYinRoster.Tests/
```

각 호출 site 에 `new List<object>()` 빈 list 추가:
```csharp
panel.SetInventoryRows(rows, new List<object>(), maxWeight: 964f);
```

- [ ] **Step 7: Build verify**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: Build OK (Plugin.cs 의 호출 site 는 Task 7 에서 갱신 — 본 Task 5 시점엔 Plugin.cs 의 ContainerPanel.SetXxxRows 호출이 컴파일 오류 발생할 수 있음). 임시 fix: Plugin.cs 호출 site 에 `new List<object>()` 추가하거나 — Task 7 까지 Plugin.cs 빌드 무시하고 ContainerPanel only 빌드. 가장 단순: Task 5 에서 Plugin.cs 호출 site 도 임시로 `new List<object>()` 빈 list 추가, Task 7 에서 진짜 raw items 채움.

Plugin.cs 임시 수정:
```bash
grep -n "SetInventoryRows\|SetStorageRows\|SetContainerRows" src/LongYinRoster/Plugin.cs
```

각 호출 site 에 `new List<object>()` 인자 추가 (Task 7 에서 진짜 source 로 교체).

- [ ] **Step 8: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/UI/ContainerPanel.cs src/LongYinRoster.Tests/ContainerPanelFocusTests.cs src/LongYinRoster.Tests/ src/LongYinRoster/Plugin.cs
git commit -m "feat(ui): v0.7.4 D-1 — ContainerPanel focus state + cell Button + ⓘ 토글"
```

---

## Task 6: ItemDetailPanel — 신규 IMGUI window

ContainerPanel 패턴 모방 신규 panel. unit test 없음 (IMGUI runtime 필요) — Task 8 smoke 시각 검증.

**Files:**
- Create: `src/LongYinRoster/UI/ItemDetailPanel.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` — `<Compile Include>` 추가
- Modify: `src/LongYinRoster.Tests/UnityStubs.cs` — 필요시 추가 stub (대부분 v0.7.3 에서 이미 검증된 IMGUI 만 사용 — 추가 stub 불필요할 가능성 높음)

- [ ] **Step 1: Create ItemDetailPanel skeleton**

`src/LongYinRoster/UI/ItemDetailPanel.cs`:

```csharp
using System;
using System.Collections.Generic;
using LongYinRoster.Containers;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.4 D-1 — focus 된 item 의 상세 정보 표시 non-modal window.
/// ContainerPanel 의 ⓘ 토글로 Visible 제어. F11 (ContainerPanel close) 시 sync 닫힘.
/// 매 frame focus item reflection — Apply / 이동·복사 후 stale 자동 회피.
/// </summary>
public sealed class ItemDetailPanel
{
    public bool Visible { get; set; } = false;
    private Rect _rect = new Rect(820, 100, 380, 500);
    private const int WindowID = 0x4C593734;   // "LY74"
    private bool _rawExpanded = false;
    private Vector2 _scroll = Vector2.zero;
    private ContainerPanel? _hostPanel;

    public void Init(ContainerPanel host, float defaultX, float defaultY, float defaultWidth, float defaultHeight)
    {
        _hostPanel = host;
        _rect = new Rect(defaultX, defaultY, defaultWidth, defaultHeight);
    }

    public Rect WindowRect => _rect;

    public void OnGUI()
    {
        if (!Visible) return;
        try
        {
            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"ItemDetailPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "Item 상세");
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
                Visible = false;
            GUILayout.Space(DialogStyle.HeaderHeight);

            var raw = _hostPanel?.GetFocusedRawItem();
            if (raw == null) DrawEmpty();
            else DrawDetails(raw);

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"ItemDetailPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void DrawEmpty()
    {
        GUILayout.Space(60);
        GUILayout.BeginHorizontal();
        GUILayout.Space(30);
        GUILayout.Label("item 의 cell 을 클릭하세요");
        GUILayout.EndHorizontal();
    }

    private void DrawDetails(object raw)
    {
        // 1. header — focused item 의 이름 + 등급/품질 색상
        string name = ItemReflector.GetNameRaw(raw);
        int grade = ItemReflector.GetGradeOrder(raw);
        var prevColor = GUI.color;
        GUI.color = ItemCellRenderer.GradeColor(grade);
        GUILayout.Label($"  {name}");
        GUI.color = prevColor;
        GUILayout.Space(4);

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 110));

        // 2. curated 섹션
        var curated = ItemDetailReflector.GetCuratedFields(raw);
        if (curated.Count > 0)
        {
            GUILayout.Label("== 정보 ==");
            foreach (var (label, value) in curated)
                GUILayout.Label($"  {label}: {value}");
            GUILayout.Space(8);
        }

        // 3. raw fields (접이식)
        var rawFields = ItemDetailReflector.GetRawFields(raw);
        var arrow = _rawExpanded ? "▼" : "▶";
        if (GUILayout.Button($"{arrow} Raw fields ({rawFields.Count})"))
            _rawExpanded = !_rawExpanded;
        if (_rawExpanded)
        {
            foreach (var (fname, value) in rawFields)
                GUILayout.Label($"  {fname}: {value}");
        }

        GUILayout.EndScrollView();
    }
}
```

- [ ] **Step 2: Add csproj entry**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 의 UI-grouped Compile entries 에 추가 (알파벳 순):

```xml
<Compile Include="../LongYinRoster/UI/ItemDetailPanel.cs">
  <Link>UI/ItemDetailPanel.cs</Link>
</Compile>
```

- [ ] **Step 3: Build + run tests**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: Build OK + 182/182 PASS. 추가 UnityStubs 필요시 알림 — 보통 v0.7.3 에서 이미 검증된 stubs 충분.

`GUI.Window` / `GUI.WindowFunction` / `GUI.DragWindow` 가 stub 에 있는지 확인:
```bash
grep -n "Window\|WindowFunction\|DragWindow" src/LongYinRoster.Tests/UnityStubs.cs
```

이미 ContainerPanel 도 사용 중이므로 stub 에 있음 (v0.7.0 부터 있었음).

- [ ] **Step 4: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/UI/ItemDetailPanel.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(ui): v0.7.4 D-1 — ItemDetailPanel non-modal window"
```

---

## Task 7: Plugin.cs + Config.cs wire-up

ItemDetailPanel 인스턴스 + ContainerPanel callback 연결 + Set*Rows raw items source + F11 sync close + Config 영속화.

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`
- Modify: `src/LongYinRoster/Config.cs`

- [ ] **Step 1: Add Config entries**

`src/LongYinRoster/Config.cs` 의 `Bind` method 에 추가 (다른 `ConfigEntry` Bind 패턴 따라):

```csharp
public ConfigEntry<float> ItemDetailPanelX { get; private set; } = null!;
public ConfigEntry<float> ItemDetailPanelY { get; private set; } = null!;
public ConfigEntry<float> ItemDetailPanelWidth  { get; private set; } = null!;
public ConfigEntry<float> ItemDetailPanelHeight { get; private set; } = null!;
public ConfigEntry<bool>  ItemDetailPanelOpen   { get; private set; } = null!;
```

`Bind` (또는 ctor) 안에 binding:

```csharp
ItemDetailPanelX = configFile.Bind("UI", "ItemDetailPanelX", 970f, "item 상세 panel X 좌표");
ItemDetailPanelY = configFile.Bind("UI", "ItemDetailPanelY", 100f, "item 상세 panel Y 좌표");
ItemDetailPanelWidth  = configFile.Bind("UI", "ItemDetailPanelWidth",  380f, "item 상세 panel 폭");
ItemDetailPanelHeight = configFile.Bind("UI", "ItemDetailPanelHeight", 500f, "item 상세 panel 높이");
ItemDetailPanelOpen   = configFile.Bind("UI", "ItemDetailPanelOpen",   false, "item 상세 panel 디폴트 열림");
```

- [ ] **Step 2: Modify Plugin.cs — instantiate + wire up**

`src/LongYinRoster/Plugin.cs` 의 ContainerPanel 인스턴스 옆에 추가:

```csharp
private ItemDetailPanel _itemDetailPanel = null!;
```

Init / Awake 의 ContainerPanel Init 옆에:

```csharp
_itemDetailPanel = new ItemDetailPanel();
_itemDetailPanel.Init(
    _containerPanel,
    Config.ItemDetailPanelX.Value,
    Config.ItemDetailPanelY.Value,
    Config.ItemDetailPanelWidth.Value,
    Config.ItemDetailPanelHeight.Value);
_itemDetailPanel.Visible = Config.ItemDetailPanelOpen.Value;

// ContainerPanel callback wiring
_containerPanel.OnToggleItemDetailPanel = () => _itemDetailPanel.Visible = !_itemDetailPanel.Visible;
_containerPanel.IsItemDetailPanelVisible = () => _itemDetailPanel.Visible;
```

`OnGUI` (MonoBehaviour) 에 1줄 추가 — ContainerPanel.OnGUI 호출 옆에:

```csharp
_itemDetailPanel.OnGUI();
```

F11 keydown handler (`Input.GetKeyDown(KeyCode.F11)`) 안에:

```csharp
if (Input.GetKeyDown(KeyCode.F11))
{
    _containerPanel.Visible = !_containerPanel.Visible;
    if (!_containerPanel.Visible) _itemDetailPanel.Visible = false;   // sync close
}
```

(F11 handler 가 있다면 — 없으면 ModeSelector 또는 다른 entry 에서 ContainerPanel toggle 위치에 sync close 추가)

OnDestroy / OnApplicationQuit 또는 quit handler 에 position 영속화:

```csharp
Config.ItemDetailPanelX.Value = _itemDetailPanel.WindowRect.x;
Config.ItemDetailPanelY.Value = _itemDetailPanel.WindowRect.y;
Config.ItemDetailPanelWidth.Value  = _itemDetailPanel.WindowRect.width;
Config.ItemDetailPanelHeight.Value = _itemDetailPanel.WindowRect.height;
Config.ItemDetailPanelOpen.Value   = _itemDetailPanel.Visible;
```

(기존 ContainerPanel 의 position 영속 패턴 그대로 따라가기. ContainerPanel 의 position persist 코드 옆에 동일 패턴 추가)

- [ ] **Step 3: Set*Rows 호출 site 갱신 — 진짜 raw item source**

`Plugin.cs` 의 ContainerPanel `Set*Rows` 호출 site (Task 5 에서 임시로 `new List<object>()` 추가한 곳) 를 진짜 raw item source 로 교체.

`ContainerOps.ReadInventoryRows()` 와 비슷한 helper 가 raw item list 도 같이 반환하도록 변경 — 또는 별도 helper `ContainerOps.ReadInventoryRawItems()` 추가:

```bash
grep -n "ReadInventoryRows\|ReadStorageRows\|ReadContainerRows" src/LongYinRoster/Containers/ContainerOps.cs
```

기존 ContainerOps 가 row builder + raw IL2CPP list 양쪽 다 reach 가능하므로, 시그니처 확장:

```csharp
public static (List<ContainerPanel.ItemRow> Rows, List<object> RawItems) ReadInventoryRowsAndRaw(...)
{
    // ... 기존 row builder 로직 + paired raw item list 동시 채움
}
```

또는 더 단순 — 기존 row builder 가 IL2CppListOps.Get 으로 한 번씩 raw item 받으니까, builder 내부 변경:
- `ContainerRowBuilder.FromGameAllItem` 가 `(rows, rawItems)` 반환하도록 변경
- 또는 raw item 도 `ItemRow` 의 `init-only` 필드로 직접 들고 있게 변경

**가장 단순한 path**: `ContainerRowBuilder.FromGameAllItem(allItem)` 시그니처를 `(List<ItemRow>, List<object>)` tuple 반환으로 변경. JSON path (`FromJsonArray`) 도 같은 tuple 반환 (JSON path 의 raw item 은 `JsonElement.Clone()` 또는 byte[] — 하지만 JSON path 는 보통 외부 디스크 컨테이너 read 시 사용). plan 단계에서 JSON path raw item 의 의미 확인:

```bash
grep -rn "ContainerRowBuilder\.FromJsonArray\|ContainerRowBuilder\.FromGameAllItem" src/LongYinRoster/
```

ContainerOps 에서만 호출되므로 거기 변경 분리.

**구체 변경**:
```csharp
// ContainerRowBuilder.cs — 시그니처 변경
public static (List<ContainerPanel.ItemRow> Rows, List<object> RawItems) FromGameAllItem(object il2List) {
    var rows = new List<ContainerPanel.ItemRow>();
    var raws = new List<object>();
    int n = IL2CppListOps.Count(il2List);
    for (int i = 0; i < n; i++) {
        var item = IL2CppListOps.Get(il2List, i);
        if (item == null) { raws.Add(null!); continue; }   // null placeholder for OOB safety
        rows.Add(new ContainerPanel.ItemRow { /* ... */ });
        raws.Add(item);
    }
    return (rows, raws);
}

// FromJsonArray 도 같은 패턴 — raws 에는 JsonElement 또는 deserialize 된 뭔가 (단, JSON path 의 reflection 은 ItemReflector 가 string-based 라 raw 가 ItemData 객체일 필요 — 또는 JSON path 의 ItemDetailReflector 는 별도 처리 필요)
```

**Plan 단계 결정**: JSON 컨테이너 (외부 디스크) 의 raw item 은 `JsonElement` 그대로 넘기고, `ItemDetailReflector` 가 두 path (IL2CPP `ItemData` 와 `JsonElement`) 모두 처리할 수 있는지 검토 필요. 첫 release 단순 path = **JSON 컨테이너의 raw item 도 IL2CPP 객체로 deserialize 후 reflection** — 이는 v0.5.5 에서 이미 처리한 패턴. 이미 `ItemListApplier` 가 JSON → IL2CPP 변환 가지고 있으니 재사용. 단순화: Container 의 row 도 IL2CPP 객체로 변환된 raw item 을 paired source 로 보유.

또는 더 단순: **Container area 의 raw item 은 일단 JSON path 라 reflection 안되니, ItemDetailPanel 의 Container area focus 시 빈 상태 표시**. 첫 release 단순 path — 후속 sub-project 또는 v0.7.4.x patch 에서 해결.

**plan 결정** (단순화): 
- 인벤·창고: ContainerOps 가 IL2CPP raw item 을 paired list 로 같이 반환. ItemDetailPanel 은 IL2CPP `ItemData` 객체에 직접 reflection
- 컨테이너: JSON `ItemData` 는 deserialize 안 됨 → `_containerRawItems` 는 빈 list 또는 JsonElement[] — ItemDetailReflector 가 JsonElement path 도 처리? **첫 release 단순화**: Container area focus 시 ItemDetailPanel 이 "외부 디스크 item — 상세 미지원" 표시. Curated/raw 빈 list. 후속 patch 에서 JsonElement path 또는 JSON→IL2CPP 변환 추가

→ 본 plan 에서는 인벤·창고만 raw paired source 채우고, Container 는 `_containerRawItems` 를 빈 list 그대로. ItemDetailReflector 는 null-safe 라 빈 fallback 자동 처리.

```csharp
// Plugin.cs — 인벤·창고만 raw source 채움
var (invRows, invRaws) = ContainerOps.ReadInventoryRowsAndRaw(...);
_containerPanel.SetInventoryRows(invRows, invRaws);

var (stoRows, stoRaws) = ContainerOps.ReadStorageRowsAndRaw(...);
_containerPanel.SetStorageRows(stoRows, stoRaws);

// 컨테이너는 raw 빈 list 채움 — JsonElement path 는 v0.7.4.x patch 또는 후속
var conRows = ContainerOps.ReadContainerRows(...);   // 기존 시그니처 유지
_containerPanel.SetContainerRows(conRows, new List<object>());
```

`ContainerOps.ReadInventoryRowsAndRaw` / `ReadStorageRowsAndRaw` 는 기존 `ReadInventoryRows` / `ReadStorageRows` 의 시그니처 변형. 기존 method 는 deprecate 또는 wrapper.

- [ ] **Step 4: Build + run tests**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: Build OK + 182/182 PASS.

- [ ] **Step 5: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Plugin.cs src/LongYinRoster/Config.cs src/LongYinRoster/Containers/ContainerOps.cs src/LongYinRoster/Containers/ContainerRowBuilder.cs
git commit -m "feat(plugin): v0.7.4 D-1 — ItemDetailPanel wiring + Config + raw item paired source (인벤·창고)"
```

---

## Task 8: 인게임 smoke 6/6

unit test 불가능 항목들 (cell click 동작, ItemDetailPanel 시각, 카테고리별 curated 표시, F11 sync, position persist, IL2CPP strip 회귀 미발생) 사용자 수동 시각 확인.

**Files**: 코드 수정 없음 — smoke dump 만 작성.

- [ ] **Step 1: 게임 닫기 + 빌드 + 로그 클리어**

```bash
tasklist | grep -i LongYinLiZhiZhuan
```
출력 있으면 사용자에게 종료 요청.

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

- [ ] **Step 2: 사용자에게 게임 실행 요청**

> "게임 실행 → 캐릭터 로드 → F11 → 컨테이너 관리 진입. 인벤에서 cell 클릭 → 외곽선 cyan 표시 + ⓘ 상세 버튼 클릭 → ItemDetailPanel 열림 → 장비/비급/단약 sample 각각 클릭하면서 6 시나리오 확인 부탁드립니다."

**6 시나리오**:
1. cell 클릭 시 focus 갱신 + 외곽선 cyan (3-area 모두 시각 확인)
2. ⓘ 버튼으로 ItemDetailPanel 열기/닫기 (active 시 cyan)
3. 장비/비급/단약 클릭 시 curated 섹션 한글 라벨 + 값 정상
4. Raw section ▶ 클릭 → ▼ 펼침, 모든 reflection 필드 표시 + IL2CPP meta 필터
5. → 이동·복사 후 focus 자동 해제 (item OOB), 다른 item 클릭 시 panel 갱신
6. F11 닫기 시 ItemDetailPanel 도 같이 닫힘. 재오픈 시 position persist (PlayerPrefs 또는 BepInEx config)

- [ ] **Step 3: 로그 확인 + dump 작성**

```bash
grep -n "ContainerPanel\|ItemDetailPanel\|threw\|Method unstripping" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | head -30
```
Expected: `threw exception` 0건. `Method unstripping failed` 0건.

회귀 발견 시 spec §6 fallback 적용 (예: Button + GetLastRect strip → GetRect + Event.current).

`docs/superpowers/dumps/2026-05-XX-v0.7.4-smoke-results.md` (실제 날짜):

```markdown
# v0.7.4 D-1 — 인게임 smoke 결과

**일시**: 2026-05-XX
**baseline**: v0.7.4 build (Task 7 commit)

| # | 시나리오 | 결과 | 비고 |
|---|---|---|---|
| 1 | cell 클릭 focus + cyan outline | ✅/❌ | |
| 2 | ⓘ 상세 토글 | ✅/❌ | |
| 3 | curated 3 카테고리 (장비/비급/단약) | ✅/❌ | |
| 4 | raw fields 펼침/접힘 + meta 필터 | ✅/❌ | |
| 5 | 이동·복사 후 focus 자동 해제 | ✅/❌ | |
| 6 | F11 sync close + position persist | ✅/❌ | |

**총 결과**: X/6 PASS

**발견 회귀 / Fallback 적용**: ...
```

- [ ] **Step 4: Commit smoke dump**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add docs/superpowers/dumps/
git commit -m "docs(smoke): v0.7.4 D-1 인게임 smoke 6/6"
```

---

## Task 9: VERSION + HANDOFF + README + spike handler 제거 + release

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs` — F12 spike handler 제거 + VERSION 0.7.3 → 0.7.4
- Modify: `docs/HANDOFF.md`
- Modify: `README.md`

- [ ] **Step 1: F12 spike handler 제거**

`src/LongYinRoster/Plugin.cs` 의 Task 0 에서 추가한 [F12] handler (`if (Input.GetKeyDown(KeyCode.F12)) DumpSubDataFields();` 와 `DumpSubDataFields` / `DumpFields` private methods) 모두 제거.

- [ ] **Step 2: VERSION bump**

`src/LongYinRoster/Plugin.cs` 의 VERSION 상수:
- Old: `public const string VERSION = "0.7.3";`
- New: `public const string VERSION = "0.7.4";`

- [ ] **Step 3: HANDOFF.md update**

다음 sections 갱신:
- §1 progress line: 0.7.3 → 0.7.4 + brief D-1 description (view-only Item 상세 panel)
- §1 Releases list: append v0.7.4 entry
- §6.B v0.7.4 항목 ✅ 마킹 + scope (view-only, hybrid curated+raw)
- §6.B 추가 후보: v0.7.4.x patch (나머지 3 카테고리 curated), v0.7.7 후보 (Item editor)
- §7 컨텍스트 압축본 regenerate

- [ ] **Step 4: README.md update**

컨테이너 관리 sub-section 끝 (v0.7.3 placeholder cell section 다음) 에 추가:

```markdown
### v0.7.4 — Item 상세 panel

ContainerPanel 의 cell 을 클릭하면 단일 focus 됩니다 (외곽선 cyan). ⓘ 상세 버튼으로 별도 ItemDetailPanel window 가 열려서 선택 item 의 상세 정보를 표시합니다:

- **Curated 섹션**: 카테고리별 의미 있는 정보 한글 라벨 (장비 = 강화/착용/속성, 비급 = 학습 lv, 단약 = 효과 등). v0.7.4 첫 release 우선 cover = 장비/비급/단약. 나머지 (음식/보물/재료/말) 는 후속 patch.
- **Raw fields 섹션** (접이식): 모든 reflection 필드 dump (IL2CPP meta 필터). game patch 후 새 필드도 즉시 표시.

ItemDetailPanel 은 view-only — item 수정 기능은 후속 sub-project (v0.7.7 후보 — Item editor) 에서 별도 release.

ContainerPanel 닫힘 (F11) 시 ItemDetailPanel 도 같이 닫힙니다. position 은 영속.
```

Releases 표 v0.7.4 row 추가.

- [ ] **Step 5: Final build + tests**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: Build OK + 182/182 PASS (Task 0 의 spike handler 제거 후 컴파일 OK).

- [ ] **Step 6: Commit + tag**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Plugin.cs docs/HANDOFF.md README.md
git commit -m "chore(release): v0.7.4 — Item 상세 panel (D-1, view-only)"
git tag v0.7.4
```

(권한 거부 시 PowerShell heredoc fallback — Task 2 implementer 가 v0.7.3 에서 사용한 패턴)

- [ ] **Step 7: Release 패키징 + push (사용자 수동)**

dist zip + GitHub release + git push 는 사용자 컨트롤 (v0.7.0~v0.7.3 패턴 동일). 자동 안 함.

---

## Self-Review (작성 후 점검)

### Spec coverage
- §2.1 view-only: 모든 task 가 read-only — setter 호출 0 ✓
- §2.2 cell click → focus: Task 5 ✓
- §2.3 별도 non-modal window: Task 6 + Task 7 wire-up ✓
- §2.4 Hybrid curated + raw: Task 2 (raw) + Task 3 (curated 3) ✓
- §2.5 비범위: Item editor / multi-window / hover 비구현 — 모든 task 그 한계 안 ✓
- §3 sub-project mapping: Task 9 HANDOFF §6.B 갱신 ✓
- §4.1 Approach: Task 1~7 통합 ✓
- §4.2 데이터 모델: Task 1 (enum) + Task 5 (focus + raw paired) ✓
- §4.3 신규/변경 모듈: Task 1~7 모두 ✓
- §4.4 spike: Task 0 ✓
- §4.5 ItemDetailReflector: Task 2 + Task 3 ✓
- §4.6 ContainerPanel 변경: Task 5 ✓
- §4.7 ItemDetailPanel: Task 6 ✓
- §4.8 Config 신규: Task 7 ✓
- §4.9 Plugin.cs 변경: Task 7 ✓
- §4.10 IL2CPP / 성능: 모든 task 의 IMGUI 패턴이 v0.7.3 검증된 것만 ✓
- §6 위험: Task 8 smoke 가 Button + GetLastRect strip 검증, fallback 명시
- §7 tests: Task 2 (4) + Task 3 (6) + Task 5 (2) = 12 신규 ✓
- §8 완료 기준: Task 8 + Task 9 ✓
- §9 release contract: Task 9 ✓

### Placeholder scan
- "spike 결과로 정확한 필드명 확정" — Task 0 의 spike 가 dump 의 정확한 결과로 갱신할 수 있도록 명시
- Task 3 의 fake test 필드명 (`enhanceLv`/`learnLv`/`effectValue`) 은 가설 — Task 0 spike 후 정정 가능하도록 명시
- "2026-05-XX" 날짜 — impl 시 실제 날짜 사용 (qualified placeholder)
- Task 5 Step 5 의 SearchSortToolbar BeginHorizontal 분기 — `grep` 으로 확인 후 결정 (executable instruction 명시)

추상적 placeholder 없음.

### Type consistency
- `(ContainerArea Area, int Index)?` tuple — Task 1 enum 정의, Task 5 ContainerPanel 사용, Task 6 ItemDetailPanel 간접 사용 (GetFocusedRawItem) ✓
- `ItemDetailReflector.GetCuratedFields(object?) → List<(string Label, string Value)>` — Task 3 정의, Task 6 사용 ✓
- `ItemDetailReflector.GetRawFields(object?) → List<(string FieldName, string Value)>` — Task 2 정의, Task 6 사용 ✓
- `ItemCellRenderer.DrawAtRect(ItemRow, Rect)` — Task 4 정의, Task 5 사용 ✓
- `ContainerPanel.GetFocusedRawItem() → object?` — Task 5 정의, Task 6 사용 ✓
- `ContainerPanel.SetInventoryRows(rows, rawItems, maxWeight)` — Task 5 변경, Task 7 호출 site ✓
- `OnToggleItemDetailPanel` / `IsItemDetailPanelVisible` callbacks — Task 5 정의, Task 7 wire-up ✓

issue 없음.
