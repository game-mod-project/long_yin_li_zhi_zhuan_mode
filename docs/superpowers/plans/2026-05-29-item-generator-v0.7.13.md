# 아이템 생성기 (Item Generator) v0.7.13 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임의 아이템 generator 메서드를 reflection 으로 직접 호출해, 사용자가 7 카테고리의 구체 아이템을 선택·레벨/등급 지정·생성하여 인벤토리에 추가하는 전용 패널(F11 신규 항목)을 추가한다.

**Architecture:** 신규 4 컴포넌트 — `GameControllerLocator`(GameController.Instance 접근, HeroLocator mirror) / `ItemDbCache`(7 카테고리 DB 열거, SkillNameCache mirror) / `ItemFactory`(generator reflection 호출 + GetItem + FinalizeNewItemWrapper) / `ItemGeneratorPanel`(UI, PlayerEditorPanel 무공 list 패턴 mirror). 순수 로직(매핑/필터/페이징/arg)은 TDD, reflection 호출은 인게임 smoke.

**Tech Stack:** C# / BepInEx 6 IL2CPP / Il2CppInterop reflection / IMGUI / xUnit + Shouldly. baseline v0.7.12.3 (402 tests).

**입력 자산:**
- spec: `docs/superpowers/specs/2026-05-29-longyin-roster-mod-v0.7.13-design.md`
- dump: `docs/superpowers/dumps/2026-05-29-ct-table-portability-analysis.md` (게임 메서드 카탈로그)

---

## 파일 구조

**신규 (Core):**
- `src/LongYinRoster/Core/GameControllerLocator.cs` — GameController.Instance reflection 접근
- `src/LongYinRoster/Core/ItemGenCategory.cs` — 7 카테고리 enum + sub-type 정의 + 한글 라벨
- `src/LongYinRoster/Core/GeneratorSpec.cs` — 카테고리/subType → (게임 메서드명, arg 구성) 매핑 테이블 (순수)
- `src/LongYinRoster/Core/ItemGenEntry.cs` — DB entry POCO + 필터/페이징 순수 helper (`ItemGenFilter`)
- `src/LongYinRoster/Core/ItemDbCache.cs` — 7 카테고리 게임 DB 열거 (SkillNameCache mirror)
- `src/LongYinRoster/Core/ItemFactory.cs` — generator 호출 + GetItem + FinalizeNewItemWrapper

**신규 (UI):**
- `src/LongYinRoster/UI/ItemGeneratorPanel.cs` — 전용 패널 (PlayerEditorPanel 패턴)

**수정 (wiring):**
- `src/LongYinRoster/UI/ModeSelector.cs` — Mode enum + 메뉴 버튼
- `src/LongYinRoster/UI/HotkeyMap.cs` — 핫키 필드 + Bind + Shortcut
- `src/LongYinRoster/Config.cs` — 핫키 + 패널 영속화 ConfigEntry
- `src/LongYinRoster/UI/ModWindow.cs` — 인스턴스 + Awake wiring + Update transition + OnGUI + 영속화
- `src/LongYinRoster/UI/SettingsPanel.cs` — 핫키 rebind 통합
- `src/LongYinRoster/Plugin.cs` — VERSION + Logger
- `docs/HANDOFF.md` — v0.7.13 entry

**신규 (Tests):**
- `src/LongYinRoster.Tests/GeneratorSpecTests.cs`
- `src/LongYinRoster.Tests/ItemGenFilterTests.cs`
- `src/LongYinRoster.Tests/ItemFactoryArgTests.cs`

---

## Task 1: Spike — GameControllerLocator + generator/DB 진단 (인게임 게이트)

**목적:** generator C# 시그니처 + DB 접근자 이름을 추측이 아닌 **실제 게임에서 확정**. 이후 모든 task 의 전제.

**Files:**
- Create: `src/LongYinRoster/Core/GameControllerLocator.cs`
- Create: `src/LongYinRoster/Core/ItemGenDiagnostic.cs` (임시 — Task 8 에서 제거)
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (임시 진단 핫키 [F11+9])

- [ ] **Step 1: GameControllerLocator 작성** (HeroLocator.cs L152-192 mirror)

`src/LongYinRoster/Core/GameControllerLocator.cs`:
```csharp
using System;
using System.Linq;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — GameController.Instance reflection 접근. HeroLocator 패턴 mirror.
/// generator 메서드(GenerateWeapon 등)의 this 객체.
/// </summary>
public static class GameControllerLocator
{
    private const BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    public static object? GetGameController()
    {
        try
        {
            var t = FindTypeByName("GameController");
            if (t == null) { Logger.WarnOnce("GCLoc", "GameControllerLocator: GameController type 미발견"); return null; }
            var inst = ReadStaticMember(t, "Instance");
            if (inst == null) { Logger.WarnOnce("GCLoc", "GameControllerLocator: GameController.Instance null"); return null; }
            return inst;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("GCLoc", $"GameControllerLocator threw: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static object? ReadStaticMember(Type t, string name)
    {
        var p = t.GetProperty(name, StaticFlags);
        if (p != null) return p.GetValue(null);
        var f = t.GetField(name, StaticFlags);
        if (f != null) return f.GetValue(null);
        foreach (var alt in new[] { "instance", "_instance", "s_Instance", "s_instance" })
        {
            var pa = t.GetProperty(alt, StaticFlags);
            if (pa != null) return pa.GetValue(null);
            var fa = t.GetField(alt, StaticFlags);
            if (fa != null) return fa.GetValue(null);
        }
        return null;
    }

    private static Type? FindTypeByName(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.Name == name
                && (t.Namespace == null || !t.Namespace.StartsWith("LongYinRoster", StringComparison.Ordinal)));
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
    }
}
```

- [ ] **Step 2: 진단 클래스 작성** (게임 메서드/DB 시그니처 dump)

`src/LongYinRoster/Core/ItemGenDiagnostic.cs`:
```csharp
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>v0.7.13 임시 spike — generator 시그니처 + DB 접근자 dump. Task 8 에서 제거.</summary>
public static class ItemGenDiagnostic
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Dump()
    {
        var gc = GameControllerLocator.GetGameController();
        if (gc == null) { Logger.Info("[ItemGenDiag] GameController null"); return; }
        Logger.Info($"[ItemGenDiag] GameController type = {gc.GetType().FullName}");

        // 1. Generate* 메서드 시그니처
        var sb = new StringBuilder();
        foreach (var m in gc.GetType().GetMethods(F)
                     .Where(m => m.Name.StartsWith("Generate", StringComparison.Ordinal))
                     .OrderBy(m => m.Name))
        {
            var ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            sb.Append($"\n  {m.ReturnType.Name} {m.Name}({ps})");
        }
        Logger.Info($"[ItemGenDiag] GameController Generate* methods:{sb}");

        // 2. GameDataController DB property 후보
        var gdcType = Type.GetType("GameDataController, Assembly-CSharp");
        var gdcInst = gdcType?.GetProperty("Instance",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null);
        if (gdcInst != null)
        {
            var dbs = gdcInst.GetType().GetProperties(F)
                .Where(p => p.Name.IndexOf("DataBase", StringComparison.OrdinalIgnoreCase) >= 0
                         || p.Name.IndexOf("DB", StringComparison.Ordinal) >= 0)
                .Select(p => p.Name);
            Logger.Info($"[ItemGenDiag] GDC DB props: {string.Join(", ", dbs)}");
        }
    }
}
```

- [ ] **Step 3: 임시 진단 핫키 추가** — `ModWindow.cs` Update() 의 핫키 블록(약 L915 부근, PlayerEditorShortcut 체크 다음)에 추가:
```csharp
// v0.7.13 TEMP spike — F11+9 로 진단 dump (Task 8 에서 제거)
if (UnityEngine.Input.GetKey(HotkeyMap.MainKey) && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Alpha9))
    Core.ItemGenDiagnostic.Dump();
```

- [ ] **Step 4: 빌드**

Run: `cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport" && DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: `Build succeeded. 0 Error(s)` (게임 종료 상태에서 DLL 배포)

- [ ] **Step 5: 인게임 실행 — 사용자 게이트**

사용자: 게임 실행 → 세이브 로드 → F11+9 → `BepInEx/LogOutput.log` 의 `[ItemGenDiag]` 줄 확인.
기록 대상 (dump 문서에 append):
- GameController 의 `Generate*` 메서드 정확한 시그니처 (param 타입/순서/개수)
- GDC 의 DB property 이름 (무기/갑옷/투구/신발/장신구/단약/음식/재료/말/보물 각각)
- 비급: `ItemData.SetBookData` 존재 여부 + `GenerateBook` 시그니처

- [ ] **Step 6: findings 기록 + 커밋**

`docs/superpowers/dumps/2026-05-29-ct-table-portability-analysis.md` 에 "## 7. v0.7.13 spike findings (인게임 확정)" 섹션 추가 — 확정된 시그니처/DB 이름 표.
```bash
git add docs/superpowers/dumps/2026-05-29-ct-table-portability-analysis.md src/LongYinRoster/Core/GameControllerLocator.cs src/LongYinRoster/Core/ItemGenDiagnostic.cs src/LongYinRoster/UI/ModWindow.cs
git commit -m "spike(v0.7.13): GameControllerLocator + generator/DB 시그니처 인게임 확정"
```

**게이트:** 확정된 시그니처가 spec §5 후보와 다르면, Task 2/4 의 GeneratorSpec arg 구성을 확정값으로 조정. 미해결 카테고리는 disable 플래그로 표시.

---

## Task 2: ItemGenCategory + GeneratorSpec (순수 매핑, TDD)

**Files:**
- Create: `src/LongYinRoster/Core/ItemGenCategory.cs`
- Create: `src/LongYinRoster/Core/GeneratorSpec.cs`
- Test: `src/LongYinRoster.Tests/GeneratorSpecTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`src/LongYinRoster.Tests/GeneratorSpecTests.cs`:
```csharp
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class GeneratorSpecTests
{
    [Fact]
    public void AllCategories_HaveSpec()
    {
        foreach (ItemGenCategory cat in System.Enum.GetValues(typeof(ItemGenCategory)))
        {
            var spec = GeneratorSpec.For(cat);
            spec.ShouldNotBeNull();
            spec.MethodName.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Weapon_MapsToGenerateWeapon()
    {
        GeneratorSpec.For(ItemGenCategory.Equipment).MethodName.ShouldBe("GenerateWeapon");
    }

    [Fact]
    public void Horse_RequiresTameRateFixup()
    {
        GeneratorSpec.For(ItemGenCategory.Horse).TameRateFixup.ShouldBeTrue();
    }

    [Fact]
    public void EquipmentSubType_SelectsCorrectMethod()
    {
        // subType 0~4 = 무기/갑옷/투구/신발/장신구
        GeneratorSpec.ForEquipmentSubType(0).MethodName.ShouldBe("GenerateWeapon");
        GeneratorSpec.ForEquipmentSubType(1).MethodName.ShouldBe("GenerateArmor");
        GeneratorSpec.ForEquipmentSubType(4).MethodName.ShouldBe("GenerateDecoration");
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport" && DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter GeneratorSpecTests`
Expected: FAIL (ItemGenCategory / GeneratorSpec 미정의 — 컴파일 에러)

- [ ] **Step 3: ItemGenCategory 작성**

`src/LongYinRoster/Core/ItemGenCategory.cs`:
```csharp
namespace LongYinRoster.Core;

/// <summary>v0.7.13 — 아이템 생성 카테고리 (게임 ItemType 매핑). CategoryGlyph.For 와 일관.</summary>
public enum ItemGenCategory
{
    Equipment = 0,  // 장비 (subType 0~4: 무기/갑옷/투구/신발/장신구, 5: 마구)
    Medicine  = 1,  // 단약
    Food      = 2,  // 음식
    Book      = 3,  // 비급
    Treasure  = 4,  // 보물
    Material  = 5,  // 재료
    Horse     = 6,  // 말
}

public static class ItemGenCategoryNames
{
    public static string Korean(ItemGenCategory c) => c switch
    {
        ItemGenCategory.Equipment => "장비",
        ItemGenCategory.Medicine  => "단약",
        ItemGenCategory.Food      => "음식",
        ItemGenCategory.Book      => "비급",
        ItemGenCategory.Treasure  => "보물",
        ItemGenCategory.Material  => "재료",
        ItemGenCategory.Horse     => "말",
        _ => "기타",
    };
}
```

- [ ] **Step 4: GeneratorSpec 작성** (Task 1 spike 확정 시그니처 반영)

`src/LongYinRoster/Core/GeneratorSpec.cs`:
```csharp
namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — 카테고리/subType → 게임 generator 메서드명 + 호출 메타.
/// 시그니처는 Task 1 spike 로 확정. arg 구성은 ItemFactory.BuildArgs 가 담당.
/// </summary>
public sealed class GeneratorSpec
{
    public string MethodName { get; init; } = "";
    public bool TameRateFixup { get; init; }   // 말 = 생성 후 tameRate=1.0
    public bool IsBook { get; init; }          // 비급 = SetBookData 경로 (빈 ItemData type=3)

    public static GeneratorSpec For(ItemGenCategory cat) => cat switch
    {
        ItemGenCategory.Equipment => ForEquipmentSubType(0),
        ItemGenCategory.Medicine  => new GeneratorSpec { MethodName = "GenerateMedData" },
        ItemGenCategory.Food      => new GeneratorSpec { MethodName = "GenerateFoodData" },
        ItemGenCategory.Book      => new GeneratorSpec { MethodName = "GenerateBook", IsBook = true },
        ItemGenCategory.Treasure  => new GeneratorSpec { MethodName = "GenerateTreasure" },
        ItemGenCategory.Material  => new GeneratorSpec { MethodName = "GenerateMaterial" },
        ItemGenCategory.Horse     => new GeneratorSpec { MethodName = "GenerateHorseData", TameRateFixup = true },
        _ => new GeneratorSpec { MethodName = "" },
    };

    public static GeneratorSpec ForEquipmentSubType(int subType) => subType switch
    {
        0 => new GeneratorSpec { MethodName = "GenerateWeapon" },
        1 => new GeneratorSpec { MethodName = "GenerateArmor" },
        2 => new GeneratorSpec { MethodName = "GenerateHelmet" },
        3 => new GeneratorSpec { MethodName = "GenerateShoes" },
        4 => new GeneratorSpec { MethodName = "GenerateDecoration" },
        5 => new GeneratorSpec { MethodName = "GenerateHorseArmorData" },
        _ => new GeneratorSpec { MethodName = "GenerateWeapon" },
    };
}
```

- [ ] **Step 5: 테스트 통과 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter GeneratorSpecTests`
Expected: PASS (4 tests)

- [ ] **Step 6: 커밋**
```bash
git add src/LongYinRoster/Core/ItemGenCategory.cs src/LongYinRoster/Core/GeneratorSpec.cs src/LongYinRoster.Tests/GeneratorSpecTests.cs
git commit -m "feat(v0.7.13): ItemGenCategory + GeneratorSpec 매핑 테이블 (TDD)"
```

---

## Task 3: ItemGenEntry + ItemGenFilter (순수 필터/페이징, TDD)

**Files:**
- Create: `src/LongYinRoster/Core/ItemGenEntry.cs`
- Test: `src/LongYinRoster.Tests/ItemGenFilterTests.cs`

- [ ] **Step 1: 실패 테스트 작성**

`src/LongYinRoster.Tests/ItemGenFilterTests.cs`:
```csharp
using System.Collections.Generic;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemGenFilterTests
{
    private static List<ItemGenEntry> Sample() => new()
    {
        new ItemGenEntry { Id = 1, NameRaw = "长矛", NameKr = "장창", Category = ItemGenCategory.Equipment, SubType = 0 },
        new ItemGenEntry { Id = 2, NameRaw = "重甲", NameKr = "중갑", Category = ItemGenCategory.Equipment, SubType = 1 },
        new ItemGenEntry { Id = 3, NameRaw = "金创药", NameKr = "금창약", Category = ItemGenCategory.Medicine, SubType = 0 },
    };

    [Fact]
    public void FilterByCategory_ReturnsOnlyMatching()
    {
        var r = ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, secondary: -1, search: "");
        r.Count.ShouldBe(2);
    }

    [Fact]
    public void FilterBySecondary_NarrowsToSubType()
    {
        var r = ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, secondary: 1, search: "");
        r.Count.ShouldBe(1);
        r[0].Id.ShouldBe(2);
    }

    [Fact]
    public void Search_MatchesKoreanOrRaw()
    {
        ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, -1, "장창").Count.ShouldBe(1);
        ItemGenFilter.Apply(Sample(), ItemGenCategory.Equipment, -1, "重甲").Count.ShouldBe(1);
    }

    [Fact]
    public void Page_ReturnsSliceAndTotalPages()
    {
        var big = new List<ItemGenEntry>();
        for (int i = 0; i < 25; i++) big.Add(new ItemGenEntry { Id = i, NameKr = $"x{i}", Category = ItemGenCategory.Material });
        var (slice, totalPages) = ItemGenFilter.Page(big, page: 1, pageSize: 10);
        slice.Count.ShouldBe(10);
        totalPages.ShouldBe(3);
        slice[0].Id.ShouldBe(10);
    }

    [Fact]
    public void Page_ClampsOutOfRange()
    {
        var (slice, totalPages) = ItemGenFilter.Page(Sample(), page: 99, pageSize: 10);
        totalPages.ShouldBe(1);
        slice.Count.ShouldBe(3);
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter ItemGenFilterTests`
Expected: FAIL (ItemGenEntry / ItemGenFilter 미정의)

- [ ] **Step 3: ItemGenEntry + ItemGenFilter 작성**

`src/LongYinRoster/Core/ItemGenEntry.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace LongYinRoster.Core;

/// <summary>v0.7.13 — 생성 가능 아이템 DB entry (1회 열거 캐시 단위).</summary>
public sealed class ItemGenEntry
{
    public int Id { get; init; }                  // skillID(비급) 또는 DB index/itemID
    public string NameRaw { get; init; } = "";
    public string? NameKr { get; init; }
    public ItemGenCategory Category { get; init; }
    public int SubType { get; init; }
    public int RareLvDefault { get; init; }

    public string Display => string.IsNullOrEmpty(NameKr) ? NameRaw : NameKr!;
}

/// <summary>v0.7.13 — 순수 필터/페이징 (ItemGeneratorPanel 이 사용, 테스트 가능).</summary>
public static class ItemGenFilter
{
    public static List<ItemGenEntry> Apply(
        IEnumerable<ItemGenEntry> entries, ItemGenCategory category, int secondary, string? search)
    {
        IEnumerable<ItemGenEntry> q = entries.Where(e => e.Category == category);
        if (secondary >= 0) q = q.Where(e => e.SubType == secondary);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            q = q.Where(e => (e.NameKr != null && e.NameKr.Contains(s, StringComparison.OrdinalIgnoreCase))
                          || e.NameRaw.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        return q.ToList();
    }

    public static (List<ItemGenEntry> Slice, int TotalPages) Page(
        IReadOnlyList<ItemGenEntry> filtered, int page, int pageSize)
    {
        int total = (filtered.Count + pageSize - 1) / pageSize;
        if (total == 0) total = 1;
        if (page >= total) page = total - 1;
        if (page < 0) page = 0;
        int start = page * pageSize;
        int end = Math.Min(start + pageSize, filtered.Count);
        var slice = new List<ItemGenEntry>();
        for (int i = start; i < end; i++) slice.Add(filtered[i]);
        return (slice, total);
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter ItemGenFilterTests`
Expected: PASS (5 tests)

- [ ] **Step 5: 커밋**
```bash
git add src/LongYinRoster/Core/ItemGenEntry.cs src/LongYinRoster.Tests/ItemGenFilterTests.cs
git commit -m "feat(v0.7.13): ItemGenEntry + ItemGenFilter 순수 필터/페이징 (TDD)"
```

---

## Task 4: ItemDbCache (게임 DB 열거, SkillNameCache mirror)

**Files:**
- Create: `src/LongYinRoster/Core/ItemDbCache.cs`

**비고:** reflection 열거는 게임 타입 의존 → 테스트는 test-mode(게임 부재) 에서 빈 list 반환 + no-throw 만 검증. 실제 열거는 Task 8 smoke. 비급은 `SkillNameCache` 위임.

- [ ] **Step 1: ItemDbCache 작성** (SkillNameCache.cs L148-249 mirror, Task 1 확정 DB 이름 반영)

`src/LongYinRoster/Core/ItemDbCache.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — 7 카테고리 생성 가능 아이템 DB 열거 (lazy, thread-safe). SkillNameCache mirror.
/// 비급은 SkillNameCache 위임. DB property 이름은 Task 1 spike 확정값.
/// </summary>
public static class ItemDbCache
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static List<ItemGenEntry>? _cache;
    private static readonly object _lock = new();

    // Task 1 spike 확정 DB property 이름으로 교체. (category, subType, dbProperty)
    private static readonly (ItemGenCategory Cat, int SubType, string DbProp)[] DbMap =
    {
        (ItemGenCategory.Equipment, 0, "weaponDataBase"),
        (ItemGenCategory.Equipment, 1, "armorDataBase"),
        (ItemGenCategory.Equipment, 2, "helmetDataBase"),
        (ItemGenCategory.Equipment, 3, "shoesDataBase"),
        (ItemGenCategory.Equipment, 4, "decorationDataBase"),
        (ItemGenCategory.Medicine,  0, "medDataBase"),
        (ItemGenCategory.Food,      0, "foodDataBase"),
        (ItemGenCategory.Material,  0, "materialDataBase"),
        (ItemGenCategory.Treasure,  0, "treasureDataBase"),
        (ItemGenCategory.Horse,     0, "horseDataBase"),
    };

    public static IReadOnlyList<ItemGenEntry> All()
    {
        EnsureBuilt();
        return _cache!;
    }

    public static void ResetForTests()
    {
        lock (_lock) { _cache = null; }
    }

    private static void EnsureBuilt()
    {
        if (_cache != null) return;
        lock (_lock)
        {
            if (_cache != null) return;
            _cache = BuildFromGame();
        }
    }

    private static List<ItemGenEntry> BuildFromGame()
    {
        var list = new List<ItemGenEntry>();
        try
        {
            var gdcType = Type.GetType("GameDataController, Assembly-CSharp");
            if (gdcType == null) { Logger.WarnOnce("ItemDbCache", "GameDataController 미발견"); return list; }
            var gdc = gdcType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null);
            if (gdc == null) { Logger.WarnOnce("ItemDbCache", "GDC.Instance null"); return list; }

            foreach (var (cat, sub, dbProp) in DbMap)
            {
                try { EnumerateDb(gdc, cat, sub, dbProp, list); }
                catch (Exception ex) { Logger.WarnOnce("ItemDbCache", $"{dbProp}: {ex.GetType().Name}: {ex.Message}"); }
            }

            // 비급 = SkillNameCache 위임 (134+ entry, type 0~8 → SubType)
            foreach (var (id, label) in SkillNameCache.AllOrdered())
            {
                list.Add(new ItemGenEntry
                {
                    Id = id, NameRaw = label, NameKr = label,
                    Category = ItemGenCategory.Book, SubType = SkillNameCache.GetType(id),
                });
            }
            Logger.Info($"ItemDbCache: built {list.Count} entries");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("ItemDbCache", $"BuildFromGame: {ex.GetType().Name}: {ex.Message}");
        }
        return list;
    }

    private static void EnumerateDb(object gdc, ItemGenCategory cat, int sub, string dbProp, List<ItemGenEntry> list)
    {
        var db = gdc.GetType().GetProperty(dbProp, F)?.GetValue(gdc);
        if (db == null) { Logger.WarnOnce("ItemDbCache", $"{dbProp} null"); return; }
        int n = IL2CppListOps.Count(db);
        for (int i = 0; i < n; i++)
        {
            var entry = IL2CppListOps.Get(db, i);
            if (entry == null) continue;
            string raw = ReadStr(entry, "name");
            if (string.IsNullOrEmpty(raw)) raw = ReadStr(entry, "itemName");
            int rare = ReadInt(entry, "rareLv");
            list.Add(new ItemGenEntry
            {
                Id = i, NameRaw = raw,
                NameKr = string.IsNullOrEmpty(raw) ? null : HangulDict.Translate(raw),
                Category = cat, SubType = sub, RareLvDefault = rare,
            });
        }
    }

    private static int ReadInt(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return Convert.ToInt32(p.GetValue(obj));
            var f = t.GetField(name, F);
            if (f != null) return Convert.ToInt32(f.GetValue(obj));
        }
        catch { }
        return 0;
    }

    private static string ReadStr(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null) return p.GetValue(obj)?.ToString() ?? "";
            var f = t.GetField(name, F);
            if (f != null) return f.GetValue(obj)?.ToString() ?? "";
        }
        catch { }
        return "";
    }
}
```

- [ ] **Step 2: 빌드 확인** (테스트 게임 부재라 컴파일만)

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: `Build succeeded. 0 Error(s)`

주: `SkillNameCache.GetType(int)` / `AllOrdered()` 존재 확인됨 (조사 보고). 없으면 SkillNameCache 의 해당 accessor 시그니처에 맞춰 호출 조정.

- [ ] **Step 3: 커밋**
```bash
git add src/LongYinRoster/Core/ItemDbCache.cs
git commit -m "feat(v0.7.13): ItemDbCache 7 카테고리 DB 열거 (SkillNameCache mirror)"
```

---

## Task 5: ItemFactory (generator 호출 + GetItem)

**Files:**
- Create: `src/LongYinRoster/Core/ItemFactory.cs`
- Test: `src/LongYinRoster.Tests/ItemFactoryArgTests.cs`

**비고:** 실제 generator 호출은 게임 의존 → smoke. `BuildArgs`(arg 배열 구성)는 순수 → TDD.

- [ ] **Step 1: 실패 테스트 작성** (BuildArgs 순수 로직)

`src/LongYinRoster.Tests/ItemFactoryArgTests.cs`:
```csharp
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemFactoryArgTests
{
    [Fact]
    public void EquipmentArgs_LevelRarityShape()
    {
        // 장비: (itemLv, 0, 0, bossLv, player) — player 는 호출 시 주입, BuildArgs 는 player 제외 prefix
        var args = ItemFactory.BuildArgs(ItemGenCategory.Equipment, subType: 0, id: 0, lv: 5, rare: 5);
        args.Length.ShouldBeGreaterThanOrEqualTo(2);
        args[0].ShouldBe(5);   // itemLv
    }

    [Fact]
    public void BookArgs_SkillIdAndRare()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Book, subType: 0, id: 287, lv: 0, rare: 5);
        args[0].ShouldBe(287); // skillID
        args[1].ShouldBe(5);   // rareLv
    }

    [Fact]
    public void HorseArgs_IdAndBossLv()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Horse, subType: 0, id: 3, lv: 5, rare: 5);
        args[0].ShouldBe(3);   // id
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter ItemFactoryArgTests`
Expected: FAIL (ItemFactory 미정의)

- [ ] **Step 3: ItemFactory 작성** (Task 1 확정 시그니처로 BuildArgs 조정)

`src/LongYinRoster/Core/ItemFactory.cs`:
```csharp
using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — 게임 generator reflection 호출 → ItemData → GetItem 인벤 추가.
/// BuildArgs 는 순수(테스트), Generate 는 게임 의존(smoke).
/// arg 구성은 Task 1 spike 확정 시그니처로 조정.
/// </summary>
public static class ItemFactory
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public sealed class Result
    {
        public bool Ok { get; set; }
        public int Created { get; set; }
        public string? Reason { get; set; }
        public string? ItemName { get; set; }
    }

    /// <summary>generator 메서드 인자 배열 (this/player 제외). spike 확정값 반영.</summary>
    public static object[] BuildArgs(ItemGenCategory cat, int subType, int id, int lv, int rare)
    {
        // "레벨" = itemLv = bossLv 동일 적용 (spec §5 단순화)
        return cat switch
        {
            // 장비: (itemLv, 0, 0, bossLv) + player(호출부 append)
            ItemGenCategory.Equipment => new object[] { lv, 0, 0, lv },
            ItemGenCategory.Book      => new object[] { id, rare },           // SetBookData(skillID, rareLv)
            ItemGenCategory.Medicine  => new object[] { id, lv },
            ItemGenCategory.Food      => new object[] { id, lv },
            ItemGenCategory.Material  => new object[] { subType, lv, lv },
            ItemGenCategory.Horse     => new object[] { id, lv },
            ItemGenCategory.Treasure  => new object[] { id, rare, lv },
            _ => new object[] { lv },
        };
    }

    public static Result Generate(object? player, ItemGenCategory cat, int subType, int id, int lv, int rare, int qty)
    {
        var res = new Result();
        if (player == null) { res.Reason = "player null (게임 로드 후 시도)"; return res; }
        var gc = GameControllerLocator.GetGameController();
        if (gc == null) { res.Reason = "GameController null"; return res; }

        var spec = cat == ItemGenCategory.Equipment ? GeneratorSpec.ForEquipmentSubType(subType) : GeneratorSpec.For(cat);
        if (string.IsNullOrEmpty(spec.MethodName)) { res.Reason = $"generator 미정의: {cat}"; return res; }

        int created = 0;
        string? lastName = null;
        for (int q = 0; q < Math.Max(1, qty); q++)
        {
            try
            {
                object? item = InvokeGenerator(gc, player, spec, cat, subType, id, lv, rare);
                if (item == null) { res.Reason = $"{spec.MethodName} 반환 null"; break; }

                if (spec.TameRateFixup) TrySetHorseTame(item);
                ItemListApplier.FinalizeNewItemWrapper(item);
                AddToInventory(player, item);
                lastName = TryGetName(item);
                created++;
            }
            catch (Exception ex)
            {
                Logger.WarnOnce("ItemFactory", $"Generate {spec.MethodName}: {ex.GetType().Name}: {ex.Message}");
                res.Reason = $"{spec.MethodName}: {ex.Message}";
                break;
            }
        }
        res.Created = created;
        res.Ok = created > 0;
        res.ItemName = lastName;
        return res;
    }

    private static object? InvokeGenerator(object gc, object player, GeneratorSpec spec,
        ItemGenCategory cat, int subType, int id, int lv, int rare)
    {
        var args = BuildArgs(cat, subType, id, lv, rare);
        // 장비 generator 는 마지막 param 이 player — append 후 InvokeMethodReturning 이 best overload 매칭
        object[] callArgs = cat == ItemGenCategory.Equipment ? Append(args, player) : args;
        return InvokeMethodReturning(gc, spec.MethodName, callArgs);
    }

    private static object[] Append(object[] arr, object tail)
    {
        var r = new object[arr.Length + 1];
        Array.Copy(arr, r, arr.Length);
        r[arr.Length] = tail;
        return r;
    }

    private static void AddToInventory(object player, object item)
    {
        // HeroData.GetItem(ItemData, bool) — ItemListApplier 와 동일 경로
        var ild = player.GetType().GetProperty("itemListData", F)?.GetValue(player)
                  ?? player.GetType().GetField("itemListData", F)?.GetValue(player);
        // GetItem 은 player(HeroData) 메서드
        InvokeMethodVoid(player, "GetItem", new object[] { item, false });
    }

    private static void TrySetHorseTame(object item)
    {
        var hd = item.GetType().GetProperty("horseData", F)?.GetValue(item)
                 ?? item.GetType().GetField("horseData", F)?.GetValue(item);
        if (hd == null) return;
        var p = hd.GetType().GetProperty("tameRate", F);
        if (p != null && p.CanWrite) { p.SetValue(hd, 1.0f); return; }
        var f = hd.GetType().GetField("tameRate", F);
        if (f != null) f.SetValue(hd, 1.0f);
    }

    private static string? TryGetName(object item)
    {
        try
        {
            var p = item.GetType().GetProperty("name", F);
            string raw = p?.GetValue(item)?.ToString() ?? "";
            return string.IsNullOrEmpty(raw) ? null : HangulDict.Translate(raw);
        }
        catch { return null; }
    }

    // ItemListApplier.InvokeMethod 와 동일 매칭 로직 — 반환값 버전
    private static object? InvokeMethodReturning(object obj, string methodName, object[] args)
    {
        var best = FindMethod(obj.GetType(), methodName, args);
        if (best == null) throw new MissingMethodException(obj.GetType().FullName, methodName);
        return best.Invoke(obj, FillArgs(best, args));
    }

    private static void InvokeMethodVoid(object obj, string methodName, object[] args)
    {
        var best = FindMethod(obj.GetType(), methodName, args);
        if (best == null) throw new MissingMethodException(obj.GetType().FullName, methodName);
        best.Invoke(obj, FillArgs(best, args));
    }

    private static MethodInfo? FindMethod(Type t, string methodName, object[] args)
    {
        MethodInfo? best = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != methodName) continue;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) continue;
            bool ok = true;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
            }
            if (!ok) continue;
            if (best == null || ps.Length < best.GetParameters().Length) best = m;
        }
        return best;
    }

    private static object?[] FillArgs(MethodInfo m, object[] args)
    {
        var ps = m.GetParameters();
        var full = new object?[ps.Length];
        for (int i = 0; i < ps.Length; i++)
            full[i] = i < args.Length ? args[i]
                : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
        return full;
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter ItemFactoryArgTests`
Expected: PASS (3 tests)

- [ ] **Step 5: 커밋**
```bash
git add src/LongYinRoster/Core/ItemFactory.cs src/LongYinRoster.Tests/ItemFactoryArgTests.cs
git commit -m "feat(v0.7.13): ItemFactory generator 호출 + GetItem (BuildArgs TDD)"
```

---

## Task 6: ItemGeneratorPanel (UI)

**Files:**
- Create: `src/LongYinRoster/UI/ItemGeneratorPanel.cs`

**비고:** UI 렌더링은 smoke. PlayerEditorPanel 무공 list 패턴 + ItemGenFilter(Task 3) 재사용. strip-safe IMGUI 만 (GUILayout.Button/Label/TextField/Space 2-arg, GUI.color, GUI.Window). FlexibleSpace/GUIStyle 금지.

- [ ] **Step 1: ItemGeneratorPanel 작성**

`src/LongYinRoster/UI/ItemGeneratorPanel.cs`:
```csharp
using System;
using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>v0.7.13 — 아이템 생성 패널 (F11 신규 항목). PlayerEditorPanel 무공 list 패턴 mirror.</summary>
public sealed class ItemGeneratorPanel
{
    public bool Visible { get; set; } = false;
    public Rect WindowRect => _rect;
    public Func<object?>? GetPlayer;

    private Rect _rect = new(300, 150, 620, 560);
    private const int WindowID = 0x4C593738;   // "LY78"
    private const int PAGE_SIZE = 10;

    private ItemGenCategory _category = ItemGenCategory.Equipment;
    private int _secondary = -1;
    private int _page = 0;
    private string _search = "";
    private int _selectedId = -1;
    private string _lvBuf = "5";
    private string _rareBuf = "5";
    private string _qtyBuf = "1";
    private Vector2 _scroll = Vector2.zero;

    public void Init(float x, float y, float w, float h)
    {
        _rect = new Rect(x, y, Math.Max(w, 620f), Math.Max(h, 560f));
    }

    public void OnGUI()
    {
        if (!Visible) return;
        try { _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, ""); }
        catch (Exception ex) { Logger.WarnOnce("ItemGenPanel", $"OnGUI: {ex.GetType().Name}: {ex.Message}"); }
    }

    private void Draw(int id)
    {
        try
        {
            GUI.enabled = true;
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "아이템 생성");
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X")) { Visible = false; return; }

            GUILayout.Space(DialogStyle.HeaderHeight);
            var player = GetPlayer?.Invoke();
            if (player == null) { GUILayout.Label("  게임 로드 후 사용 가능"); GUI.DragWindow(new Rect(0, 0, _rect.width, DialogStyle.HeaderHeight)); return; }

            DrawCategoryTabs();
            DrawSecondaryTabs();
            DrawSearch();
            DrawList();
            DrawGenerateBar(player);

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex) { Logger.WarnOnce("ItemGenPanel", $"Draw: {ex.GetType().Name}: {ex.Message}"); }
    }

    private void DrawCategoryTabs()
    {
        GUILayout.BeginHorizontal();
        foreach (ItemGenCategory c in Enum.GetValues(typeof(ItemGenCategory)))
        {
            var prev = GUI.color;
            if (_category == c) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemGenCategoryNames.Korean(c), GUILayout.Width(55)))
            { _category = c; _secondary = -1; _page = 0; _selectedId = -1; }
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSecondaryTabs()
    {
        if (_category != ItemGenCategory.Equipment) return;
        string[] labels = { "무기", "갑옷", "투구", "신발", "장신구", "마구" };
        GUILayout.BeginHorizontal();
        var p0 = GUI.color; if (_secondary == -1) GUI.color = Color.cyan;
        if (GUILayout.Button("전체", GUILayout.Width(45))) { _secondary = -1; _page = 0; }
        GUI.color = p0;
        for (int i = 0; i < labels.Length; i++)
        {
            var prev = GUI.color; if (_secondary == i) GUI.color = Color.cyan;
            if (GUILayout.Button(labels[i], GUILayout.Width(45))) { _secondary = i; _page = 0; }
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSearch()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("검색:", GUILayout.Width(40));
        _search = GUILayout.TextField(_search ?? "", GUILayout.Width(_rect.width - 80));
        GUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        var filtered = ItemGenFilter.Apply(ItemDbCache.All(), _category, _secondary, _search);
        var (slice, totalPages) = ItemGenFilter.Page(filtered, _page, PAGE_SIZE);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30)) && _page > 0) _page--;
        GUILayout.Label($"  {_page + 1} / {totalPages} ({filtered.Count}개)", GUILayout.Width(160));
        if (GUILayout.Button("▶", GUILayout.Width(30)) && _page < totalPages - 1) _page++;
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
        foreach (var e in slice)
        {
            GUILayout.BeginHorizontal();
            var prev = GUI.color;
            if (_selectedId == e.Id) GUI.color = Color.cyan;
            string mark = _selectedId == e.Id ? "▶ " : "  ";
            if (GUILayout.Button($"{mark}{e.Display}", GUILayout.Width(_rect.width - 60))) _selectedId = e.Id;
            GUI.color = prev;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawGenerateBar(object player)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("레벨:", GUILayout.Width(40));
        _lvBuf = GUILayout.TextField(_lvBuf, GUILayout.Width(40));
        GUILayout.Label("등급:", GUILayout.Width(40));
        _rareBuf = GUILayout.TextField(_rareBuf, GUILayout.Width(40));
        GUILayout.Label("수량:", GUILayout.Width(40));
        _qtyBuf = GUILayout.TextField(_qtyBuf, GUILayout.Width(40));
        if (GUILayout.Button("생성", GUILayout.Width(80))) DoGenerate(player);
        GUILayout.EndHorizontal();
    }

    private void DoGenerate(object player)
    {
        if (_selectedId < 0) { ToastService.Push("아이템을 선택하세요", ToastKind.Error); return; }
        int lv = ParseInt(_lvBuf, 5), rare = ParseInt(_rareBuf, 5), qty = ParseInt(_qtyBuf, 1);
        int subType = _category == ItemGenCategory.Equipment ? Math.Max(0, _secondary) : 0;
        var res = ItemFactory.Generate(player, _category, subType, _selectedId, lv, rare, qty);
        if (res.Ok)
            ToastService.Push($"✓ {res.ItemName ?? "아이템"} ×{res.Created} 생성됨", ToastKind.Success);
        else
            ToastService.Push($"✘ 생성 실패: {res.Reason}", ToastKind.Error);
    }

    private static int ParseInt(string s, int def)
        => int.TryParse(s, out var v) ? v : def;
}
```

- [ ] **Step 2: 빌드 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: `Build succeeded. 0 Error(s)`

주: `DialogStyle.FillBackground/DrawHeader/HeaderHeight` 시그니처는 기존 패널(PlayerEditorPanel) 사용처 확인 후 일치시킬 것. `SkillNameCache.GetType` 가 secondary 탭에 필요하면 비급 탭도 추가(현재 장비만 secondary).

- [ ] **Step 3: 커밋**
```bash
git add src/LongYinRoster/UI/ItemGeneratorPanel.cs
git commit -m "feat(v0.7.13): ItemGeneratorPanel UI (PlayerEditorPanel 패턴)"
```

---

## Task 7: Wiring (메뉴 + 핫키 + Config + ModWindow + Settings)

**Files:**
- Modify: `src/LongYinRoster/UI/ModeSelector.cs`
- Modify: `src/LongYinRoster/UI/HotkeyMap.cs`
- Modify: `src/LongYinRoster/Config.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`
- Modify: `src/LongYinRoster/UI/SettingsPanel.cs`

**비고:** 정확한 라인은 구현 시 각 파일 Read 로 재확인 (조사 보고 기준). 패턴은 v0.7.8 PlayerEditor 추가와 동일.

- [ ] **Step 1: ModeSelector.cs** — `Mode` enum 에 `ItemGen` 추가, 메뉴 버튼 추가, 윈도우 높이 +버튼 조정:
```csharp
public enum Mode { None, Character, Container, Settings, Player, ItemGen }
// 메뉴 버튼 블록 (플레이어 편집 버튼 다음):
if (GUILayout.Button("아이템 생성 (F11+5)", GUILayout.Height(36))) SetMode(Mode.ItemGen);
```

- [ ] **Step 2: HotkeyMap.cs** — 필드/Bind/Shortcut 추가:
```csharp
public static KeyCode ItemGenModeKey       = KeyCode.Alpha5;
public static KeyCode ItemGenModeKeyNumpad = KeyCode.Keypad5;
// Bind() 내:
ItemGenModeKey = Config.HotkeyItemGenMode.Value;
ItemGenModeKeyNumpad = NumpadFor(ItemGenModeKey);
// 신규 메서드:
public static bool ItemGenShortcut()
    => Input.GetKey(MainKey) &&
       (Input.GetKeyDown(ItemGenModeKey) ||
        (ItemGenModeKeyNumpad != KeyCode.None && Input.GetKeyDown(ItemGenModeKeyNumpad)));
```
또한 `MainKeyPressedAlone()` 의 "다른 모드키 동시 안 눌림" 조건에 ItemGen 키 포함.

- [ ] **Step 3: Config.cs** — 핫키 + 패널 영속화 ConfigEntry:
```csharp
public static ConfigEntry<KeyCode> HotkeyItemGenMode = null!;
public static ConfigEntry<float> ItemGenPanelX = null!, ItemGenPanelY = null!, ItemGenPanelW = null!, ItemGenPanelH = null!;
public static ConfigEntry<bool>  ItemGenPanelOpen = null!;
// Bind():
HotkeyItemGenMode = cfg.Bind("Hotkey", "ItemGenMode", KeyCode.Alpha5, "아이템 생성 단축키 (F11+이 키)");
ItemGenPanelX = cfg.Bind("UI", "ItemGenPanelX", 300f, "아이템 생성 panel X");
ItemGenPanelY = cfg.Bind("UI", "ItemGenPanelY", 150f, "아이템 생성 panel Y");
ItemGenPanelW = cfg.Bind("UI", "ItemGenPanelW", 620f, "아이템 생성 panel 폭");
ItemGenPanelH = cfg.Bind("UI", "ItemGenPanelH", 560f, "아이템 생성 panel 높이");
ItemGenPanelOpen = cfg.Bind("UI", "ItemGenPanelOpen", false, "아이템 생성 panel 디폴트 표시");
```

- [ ] **Step 4: ModWindow.cs** — 인스턴스 + Awake wiring + Update transition + OnGUI + 영속화 + ShouldBlockMouse:
```csharp
// 필드 (패널 인스턴스 블록):
private readonly ItemGeneratorPanel _itemGenPanel = new();
private bool _lastSeenItemGenMode_unused; // 기존 _lastSeenMode transition 패턴 사용
// Awake (PlayerEditor Init 다음):
_itemGenPanel.Init(Config.ItemGenPanelX.Value, Config.ItemGenPanelY.Value, Config.ItemGenPanelW.Value, Config.ItemGenPanelH.Value);
_itemGenPanel.Visible = Config.ItemGenPanelOpen.Value;
_itemGenPanel.GetPlayer = Core.HeroLocator.GetPlayer;
// Update 핫키 (PlayerEditorShortcut 다음):
if (HotkeyMap.ItemGenShortcut()) _modeSelector.SetMode(ModeSelector.Mode.ItemGen);
// Update transition handler (Mode.Player 분기 다음):
if (_modeSelector.CurrentMode == ModeSelector.Mode.ItemGen) {
    _itemGenPanel.Visible = true;
    if (_visible) Toggle();
    _containerPanel.Visible = false; _settingsPanel.Visible = false; _playerEditorPanel.Visible = false;
}
// OnGUI (PlayerEditorPanel.OnGUI 다음):
_itemGenPanel.OnGUI();
// 영속화 (PlayerEditor 영속화 다음):
Config.ItemGenPanelX.Value = _itemGenPanel.WindowRect.x;
Config.ItemGenPanelY.Value = _itemGenPanel.WindowRect.y;
Config.ItemGenPanelW.Value = _itemGenPanel.WindowRect.width;
Config.ItemGenPanelH.Value = _itemGenPanel.WindowRect.height;
Config.ItemGenPanelOpen.Value = _itemGenPanel.Visible;
// ShouldBlockMouse: _itemGenPanel.Visible && _itemGenPanel.WindowRect.Contains(mouse) 포함
```

- [ ] **Step 5: SettingsPanel.cs** — 핫키 rebind 통합 (조사 보고 §5 의 10단계: Default 상수 / Buffer / HydrateFromValues param / Hydrate / DoSave / RecomputeConflict keys+labels / DrawHotkeyRow / CaptureSlot enum / switch case / SetBuffer 메서드).

- [ ] **Step 6: 빌드 + 전체 테스트**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test`
Expected: `Build succeeded. 0 Error(s)` + `Passed! Failed: 0` (Task 2/3/5 신규 테스트 포함, ~414+)

- [ ] **Step 7: 커밋**
```bash
git add src/LongYinRoster/UI/ModeSelector.cs src/LongYinRoster/UI/HotkeyMap.cs src/LongYinRoster/Config.cs src/LongYinRoster/UI/ModWindow.cs src/LongYinRoster/UI/SettingsPanel.cs
git commit -m "feat(v0.7.13): F11 메뉴 항목 + 핫키 + Config + Settings rebind wiring"
```

---

## Task 8: 진단 제거 + 인게임 smoke + release

**Files:**
- Delete: `src/LongYinRoster/Core/ItemGenDiagnostic.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (임시 F11+9 핫키 제거)
- Modify: `src/LongYinRoster/Plugin.cs` (VERSION + Logger)
- Modify: `docs/HANDOFF.md`

- [ ] **Step 1: 임시 진단 제거**

`ItemGenDiagnostic.cs` 삭제 + `ModWindow.cs` 의 F11+9 진단 핫키 블록(Task 1 Step 3) 제거.

- [ ] **Step 2: VERSION bump + Logger**

`Plugin.cs`: `VERSION = "0.7.13"` + Logger 줄 추가:
```csharp
Logger.Info("[v0.7.13] 아이템 생성기 (F11+5) — 7 카테고리 generator 직접 호출 + GetItem");
```

- [ ] **Step 3: 빌드 + 전체 테스트**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release && DOTNET_CLI_UI_LANGUAGE=en dotnet test`
Expected: `Build succeeded. 0 Error(s)` + `Passed! Failed: 0`

- [ ] **Step 4: 인게임 smoke — 사용자 게이트**

사용자: 게임 실행 → 세이브 로드 → F11+5 → 각 카테고리 1종씩 생성 시도:
| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 장비 무기 1종 선택 → 레벨5 등급5 → 생성 | 인벤에 무기 추가 + 토스트 |
| 2 | 비급 1종 → 생성 | 인벤에 무공책 추가 |
| 3 | 단약/음식/재료/보물 각 1종 | 각 인벤 추가 |
| 4 | 말 1종 → 생성 | 인벤 추가 + tameRate 100% |
| 5 | 수량 5 → 생성 | 5개 추가 |
| 6 | 생성 후 세이브 → 로드 | 생성 아이템 유지 (FinalizeNewItemWrapper 효과) |
| 7 | 카테고리 전환 시 secondary/선택 reset | UX 정상 |

실패 카테고리 발견 시: BepInEx 로그의 `[ItemFactory]` WarnOnce 로 메서드/시그니처 재확인 → `BuildArgs`/`GeneratorSpec` 조정 → 재빌드.

- [ ] **Step 5: HANDOFF + smoke 결과 기록**

`docs/superpowers/dumps/2026-05-29-v0.7.13-smoke-results.md` 작성 + `docs/HANDOFF.md` 에 v0.7.13 entry + baseline 갱신.

- [ ] **Step 6: release 커밋 + tag + dist + push + gh release** (v0.7.12.3 패턴 동일)
```bash
git add -A && git commit -m "chore(release): v0.7.13 아이템 생성기 — 7 카테고리 generator 직접 호출"
git tag -a v0.7.13 -m "v0.7.13 — 아이템 생성기 (F11+5)"
# dist zip + push + gh release (사용자 확인 후)
```

---

## Self-Review

**Spec coverage:**
- §3 아키텍처 (4 컴포넌트) → Task 1(Locator) / 4(DbCache) / 5(Factory) / 6(Panel) ✓
- §5 생성 엔진 (8 generator) → Task 2(Spec) + 5(Factory BuildArgs) ✓
- §6 DB 열거 → Task 4 ✓
- §7 에러 처리 (resolve 실패 disable / null toast / 무게 / WarnOnce) → Task 5/6 (WarnOnce, player null toast) ✓ ; 무게 경고는 Task 6 DoGenerate 에 ItemListReflector.GetMaxWeight 경고 추가 필요 → **보강:** Task 6 DoGenerate 에 생성 후 `ItemListReflector.GetMaxWeight` 초과 시 ⚠ toast 추가.
- §8 테스트 → Task 2/3/5 (GeneratorSpec/ItemGenFilter/ItemFactoryArg) ✓
- §9 구현 단계 (spike-first) → Task 1 게이트 ✓

**Placeholder scan:** generator 시그니처는 "Task 1 spike 확정값 반영" 으로 명시 — 추측이 아닌 게이트 의존. ItemDbCache 의 DB property 이름(weaponDataBase 등)도 Task 1 확정 대상으로 명시. 코드 블록은 모두 완전.

**Type consistency:** `ItemGenEntry`(Id/NameRaw/NameKr/Category/SubType/RareLvDefault/Display) — Task 3 정의, Task 4/6 사용 일치. `GeneratorSpec`(MethodName/TameRateFixup/IsBook) — Task 2 정의, Task 5 사용 일치. `ItemFactory.Generate/BuildArgs/Result` — Task 5 정의, Task 6 사용 일치. `ItemGenCategory` — Task 2 정의, 전 task 일치.

**보강 적용:** Task 6 `DoGenerate` 에 생성 성공 후 무게 초과 경고 추가 (spec §7):
```csharp
// res.Ok 분기 내, success toast 다음:
try {
    var ild = player.GetType().GetProperty("itemListData",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(player);
    if (ild != null) {
        float max = ItemListReflector.GetMaxWeight(ild, 964f);
        float cur = 0f;
        var wp = ild.GetType().GetProperty("weight", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (wp != null) cur = Convert.ToSingle(wp.GetValue(ild));
        if (cur > max) ToastService.Push($"⚠ 인벤 무게 초과 ({cur:F0}/{max:F0}kg) — 속도 페널티", ToastKind.Info);
    }
} catch { }
```
(`ItemListReflector.GetMaxWeight(itemList, fallback)` 는 v0.7.1 자산 — HANDOFF §6.A 확인.)
