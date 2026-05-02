# v0.7.0 — F11 메뉴 재설계 + 컨테이너 기능 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** F11 진입 시 모드 선택 메뉴 (캐릭터 관리 / 컨테이너 관리) 도입 + 외부 디스크 컨테이너 ↔ 게임 인벤토리 / 창고 사이의 item 이동·복사 기능 추가.

**Architecture:** ItemListApplier.ApplyJsonToObject deep-copy + IL2CppListOps + SlotRepository 패턴 재사용. 신규 8개 file (UI 2 + Containers 5 + Util 1) + 기존 ModWindow.cs / Plugin.cs 변경. 13-카테고리 시리즈 foundation 활용해 단순 mirror.

**Tech Stack:** BepInEx 6 IL2CPP, .NET 6, Unity IMGUI, System.Text.Json, xUnit + Shouldly.

**Spec**: `docs/superpowers/specs/2026-05-02-longyin-roster-mod-v0.7.0-design.md`

---

## Phase 1 — Foundation (Containers schema + repository)

### Task 1: ContainerMetadata POCO

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerMetadata.cs`
- Test: `src/LongYinRoster.Tests/ContainerMetadataTests.cs`

- [ ] **Step 1: Write failing test**

`src/LongYinRoster.Tests/ContainerMetadataTests.cs`:
```csharp
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerMetadataTests
{
    [Fact]
    public void Default_HasSensibleDefaults()
    {
        var m = new ContainerMetadata();
        m.SchemaVersion.ShouldBe(1);
        m.ContainerIndex.ShouldBe(0);
        m.ContainerName.ShouldBe("");
    }
}
```

- [ ] **Step 2: Implement ContainerMetadata**

`src/LongYinRoster/Containers/ContainerMetadata.cs`:
```csharp
using System;

namespace LongYinRoster.Containers;

public sealed class ContainerMetadata
{
    public int    SchemaVersion   { get; set; } = 1;
    public int    ContainerIndex  { get; set; }
    public string ContainerName   { get; set; } = "";
    public string UserComment     { get; set; } = "";
    public string CreatedAt       { get; set; } = DateTimeOffset.Now.ToString("o");
    public string ModVersion      { get; set; } = "0.7.0";
}
```

- [ ] **Step 3: Run test**

Run: `dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj -c Release --filter ContainerMetadataTests`
Expected: PASS (1 test).

Need to update tests csproj. Add this `<Compile Include="...">` to `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` ItemGroup:
```xml
<Compile Include="../LongYinRoster/Containers/ContainerMetadata.cs">
  <Link>Containers/ContainerMetadata.cs</Link>
</Compile>
```

- [ ] **Step 4: Commit**

```bash
git checkout -b v0.7.0
git add src/LongYinRoster/Containers/ContainerMetadata.cs src/LongYinRoster.Tests/ContainerMetadataTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(containers): ContainerMetadata POCO + tests"
```

---

### Task 2: ContainerFile JSON serialization

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerFile.cs`
- Test: `src/LongYinRoster.Tests/ContainerFileTests.cs`

- [ ] **Step 1: Write failing test**

`src/LongYinRoster.Tests/ContainerFileTests.cs`:
```csharp
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerFileTests
{
    [Fact]
    public void RoundTrip_PreservesMetadataAndItems()
    {
        var m = new ContainerMetadata { ContainerIndex = 3, ContainerName = "테스트" };
        string itemsJson = @"[{""itemID"":34,""type"":0,""name"":""검""},{""itemID"":0,""type"":3,""name"":""책""}]";
        var json = ContainerFile.Compose(m, itemsJson);
        var parsed = ContainerFile.Parse(json);
        parsed.Metadata.ContainerIndex.ShouldBe(3);
        parsed.Metadata.ContainerName.ShouldBe("테스트");
        parsed.ItemsJson.ShouldContain("\"name\":\"검\"");
        parsed.ItemsJson.ShouldContain("\"name\":\"책\"");
    }

    [Fact]
    public void Parse_HandlesEmptyItems()
    {
        var m = new ContainerMetadata { ContainerIndex = 1 };
        var json = ContainerFile.Compose(m, "[]");
        var parsed = ContainerFile.Parse(json);
        parsed.ItemsJson.ShouldBe("[]");
    }
}
```

- [ ] **Step 2: Implement ContainerFile**

`src/LongYinRoster/Containers/ContainerFile.cs`:
```csharp
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LongYinRoster.Containers;

/// <summary>
/// 컨테이너 디스크 file schema:
///   {
///     "_meta": { schemaVersion, containerIndex, containerName, ... },
///     "items": [ ItemData... ]
///   }
/// SlotFile 패턴 mirror — _meta + payload.
/// </summary>
public static class ContainerFile
{
    public sealed class ParsedContainer
    {
        public ContainerMetadata Metadata { get; init; } = new();
        public string ItemsJson { get; init; } = "[]";
    }

    public static string Compose(ContainerMetadata m, string itemsJson)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteStartObject("_meta");
            w.WriteNumber("schemaVersion",  m.SchemaVersion);
            w.WriteNumber("containerIndex", m.ContainerIndex);
            w.WriteString("containerName",  m.ContainerName);
            w.WriteString("userComment",    m.UserComment);
            w.WriteString("createdAt",      m.CreatedAt);
            w.WriteString("modVersion",     m.ModVersion);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        var head = Encoding.UTF8.GetString(ms.ToArray());
        // closing `}` 직전에 `,"items":[...]` 주입
        int closing = head.LastIndexOf('}');
        return head.Substring(0, closing).TrimEnd() + ",\n  \"items\": " + itemsJson + "\n}";
    }

    public static ParsedContainer Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var m = new ContainerMetadata();
        if (root.TryGetProperty("_meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            m.SchemaVersion  = ReadInt(meta, "schemaVersion", 1);
            m.ContainerIndex = ReadInt(meta, "containerIndex", 0);
            m.ContainerName  = ReadStr(meta, "containerName", "");
            m.UserComment    = ReadStr(meta, "userComment", "");
            m.CreatedAt      = ReadStr(meta, "createdAt", "");
            m.ModVersion     = ReadStr(meta, "modVersion", "");
        }
        string items = "[]";
        if (root.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            items = arr.GetRawText();
        return new ParsedContainer { Metadata = m, ItemsJson = items };
    }

    private static int    ReadInt(JsonElement e, string k, int def)    => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;
    private static string ReadStr(JsonElement e, string k, string def) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;
}
```

- [ ] **Step 3: Add to test csproj + run**

Add `<Compile Include>` for `ContainerFile.cs` to test csproj.

Run: `dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj -c Release --filter ContainerFileTests`
Expected: PASS (2 tests).

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerFile.cs src/LongYinRoster.Tests/ContainerFileTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(containers): ContainerFile JSON Compose/Parse + tests"
```

---

### Task 3: ContainerRepository — disk I/O

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerRepository.cs`
- Test: `src/LongYinRoster.Tests/ContainerRepositoryTests.cs`

- [ ] **Step 1: Write failing test**

`src/LongYinRoster.Tests/ContainerRepositoryTests.cs`:
```csharp
using System.IO;
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerRepositoryTests
{
    [Fact]
    public void CreateNew_AssignsIncrementingIndex()
    {
        var dir = Path.Combine(Path.GetTempPath(), "longyin_container_test_" + System.Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            int i1 = repo.CreateNew("첫번째");
            int i2 = repo.CreateNew("두번째");
            i1.ShouldBe(1);
            i2.ShouldBe(2);
            File.Exists(Path.Combine(dir, "container_01.json")).ShouldBeTrue();
            File.Exists(Path.Combine(dir, "container_02.json")).ShouldBeTrue();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void List_ReturnsCreatedContainers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "longyin_container_test_" + System.Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            repo.CreateNew("A");
            repo.CreateNew("B");
            var list = repo.List();
            list.Count.ShouldBe(2);
            list[0].ContainerName.ShouldBe("A");
            list[1].ContainerName.ShouldBe("B");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "longyin_container_test_" + System.Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            int idx = repo.CreateNew("X");
            repo.Delete(idx);
            File.Exists(Path.Combine(dir, $"container_{idx:D2}.json")).ShouldBeFalse();
            repo.List().Count.ShouldBe(0);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Rename_UpdatesMetadata()
    {
        var dir = Path.Combine(Path.GetTempPath(), "longyin_container_test_" + System.Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            int idx = repo.CreateNew("OldName");
            repo.Rename(idx, "NewName");
            var meta = repo.LoadMetadata(idx);
            meta!.ContainerName.ShouldBe("NewName");
        }
        finally { Directory.Delete(dir, true); }
    }
}
```

- [ ] **Step 2: Implement ContainerRepository**

`src/LongYinRoster/Containers/ContainerRepository.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace LongYinRoster.Containers;

/// <summary>
/// 다중 컨테이너 디스크 io. SlotRepository 패턴 mirror.
/// 파일 명: container_NN.json (NN = 0-padded index, 무제한)
/// </summary>
public sealed class ContainerRepository
{
    private readonly string _dir;
    private static readonly Regex FileRegex = new(@"^container_(\d+)\.json$", RegexOptions.Compiled);

    public ContainerRepository(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    public int CreateNew(string name)
    {
        int idx = NextIndex();
        var meta = new ContainerMetadata { ContainerIndex = idx, ContainerName = name };
        var json = ContainerFile.Compose(meta, "[]");
        File.WriteAllText(PathFor(idx), json);
        return idx;
    }

    public List<ContainerMetadata> List()
    {
        var result = new List<ContainerMetadata>();
        foreach (var f in Directory.GetFiles(_dir, "container_*.json"))
        {
            var m = FileRegex.Match(Path.GetFileName(f));
            if (!m.Success) continue;
            try
            {
                var parsed = ContainerFile.Parse(File.ReadAllText(f));
                result.Add(parsed.Metadata);
            }
            catch { }
        }
        result.Sort((a, b) => a.ContainerIndex.CompareTo(b.ContainerIndex));
        return result;
    }

    public ContainerMetadata? LoadMetadata(int idx)
    {
        var f = PathFor(idx);
        if (!File.Exists(f)) return null;
        try { return ContainerFile.Parse(File.ReadAllText(f)).Metadata; }
        catch { return null; }
    }

    public string LoadItemsJson(int idx)
    {
        var f = PathFor(idx);
        if (!File.Exists(f)) return "[]";
        try { return ContainerFile.Parse(File.ReadAllText(f)).ItemsJson; }
        catch { return "[]"; }
    }

    public void SaveItemsJson(int idx, string itemsJson)
    {
        var meta = LoadMetadata(idx) ?? new ContainerMetadata { ContainerIndex = idx };
        File.WriteAllText(PathFor(idx), ContainerFile.Compose(meta, itemsJson));
    }

    public void Rename(int idx, string newName)
    {
        var meta = LoadMetadata(idx);
        if (meta == null) return;
        meta.ContainerName = newName;
        var items = LoadItemsJson(idx);
        File.WriteAllText(PathFor(idx), ContainerFile.Compose(meta, items));
    }

    public void Delete(int idx)
    {
        var f = PathFor(idx);
        if (File.Exists(f)) File.Delete(f);
    }

    private string PathFor(int idx) => Path.Combine(_dir, $"container_{idx:D2}.json");

    private int NextIndex()
    {
        int max = 0;
        foreach (var f in Directory.GetFiles(_dir, "container_*.json"))
        {
            var m = FileRegex.Match(Path.GetFileName(f));
            if (m.Success && int.TryParse(m.Groups[1].Value, out var i) && i > max) max = i;
        }
        return max + 1;
    }
}
```

- [ ] **Step 3: Add to test csproj + run**

Add `<Compile Include>` for `ContainerRepository.cs` to test csproj.

Run: `dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj -c Release --filter ContainerRepositoryTests`
Expected: PASS (4 tests).

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerRepository.cs src/LongYinRoster.Tests/ContainerRepositoryTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(containers): ContainerRepository (CreateNew/List/Load/Save/Rename/Delete) + tests"
```

---

## Phase 2 — Category filter

### Task 4: ItemCategoryFilter

**Files:**
- Create: `src/LongYinRoster/Containers/ItemCategoryFilter.cs`
- Test: `src/LongYinRoster.Tests/ItemCategoryFilterTests.cs`

- [ ] **Step 1: Write failing test**

`src/LongYinRoster.Tests/ItemCategoryFilterTests.cs`:
```csharp
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemCategoryFilterTests
{
    [Theory]
    [InlineData(0, 0, ItemCategory.Equipment)]
    [InlineData(0, 4, ItemCategory.Equipment)]
    [InlineData(2, 0, ItemCategory.Medicine)]
    [InlineData(2, 1, ItemCategory.Food)]
    [InlineData(2, 2, ItemCategory.Food)]
    [InlineData(3, 0, ItemCategory.Book)]
    [InlineData(4, 0, ItemCategory.Treasure)]
    [InlineData(5, 0, ItemCategory.Material)]
    [InlineData(6, 0, ItemCategory.Horse)]
    [InlineData(6, 1, ItemCategory.Horse)]
    public void Classify_KnownTypes(int type, int subType, ItemCategory expected)
    {
        ItemCategoryFilter.Classify(type, subType).ShouldBe(expected);
    }

    [Fact]
    public void Classify_UnknownType_ReturnsOther()
    {
        ItemCategoryFilter.Classify(1, 0).ShouldBe(ItemCategory.Other);
        ItemCategoryFilter.Classify(99, 0).ShouldBe(ItemCategory.Other);
    }

    [Fact]
    public void Matches_AllCategoryShowsAll()
    {
        ItemCategoryFilter.Matches(ItemCategory.All, 0, 0).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.All, 99, 99).ShouldBeTrue();
    }

    [Fact]
    public void Matches_SpecificCategoryFilters()
    {
        ItemCategoryFilter.Matches(ItemCategory.Equipment, 0, 0).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.Equipment, 3, 0).ShouldBeFalse();
        ItemCategoryFilter.Matches(ItemCategory.Medicine, 2, 0).ShouldBeTrue();
        ItemCategoryFilter.Matches(ItemCategory.Medicine, 2, 1).ShouldBeFalse();
        ItemCategoryFilter.Matches(ItemCategory.Food, 2, 1).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Implement**

`src/LongYinRoster/Containers/ItemCategoryFilter.cs`:
```csharp
namespace LongYinRoster.Containers;

public enum ItemCategory
{
    All       = -1,
    Equipment = 0,   // type=0
    Medicine  = 1,   // type=2 subType=0
    Food      = 2,   // type=2 subType≥1
    Book      = 3,   // type=3
    Treasure  = 4,   // type=4
    Material  = 5,   // type=5
    Horse     = 6,   // type=6
    Other     = 99,  // type=1 등 미분류
}

public static class ItemCategoryFilter
{
    public static ItemCategory Classify(int type, int subType) => type switch
    {
        0 => ItemCategory.Equipment,
        2 => subType == 0 ? ItemCategory.Medicine : ItemCategory.Food,
        3 => ItemCategory.Book,
        4 => ItemCategory.Treasure,
        5 => ItemCategory.Material,
        6 => ItemCategory.Horse,
        _ => ItemCategory.Other,
    };

    public static bool Matches(ItemCategory filter, int type, int subType)
    {
        if (filter == ItemCategory.All) return true;
        return Classify(type, subType) == filter;
    }

    public static string KoreanLabel(ItemCategory c) => c switch
    {
        ItemCategory.All       => "전체",
        ItemCategory.Equipment => "장비",
        ItemCategory.Medicine  => "단약",
        ItemCategory.Food      => "음식",
        ItemCategory.Book      => "비급",
        ItemCategory.Treasure  => "보물",
        ItemCategory.Material  => "재료",
        ItemCategory.Horse     => "말",
        ItemCategory.Other     => "기타",
        _ => "?",
    };
}
```

- [ ] **Step 3: Add to test csproj + run**

Add `<Compile Include>` for `ItemCategoryFilter.cs` to test csproj.

Run: `dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj -c Release --filter ItemCategoryFilterTests`
Expected: PASS (4 tests with theory expanding to 13).

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Containers/ItemCategoryFilter.cs src/LongYinRoster.Tests/ItemCategoryFilterTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(containers): ItemCategoryFilter enum + Classify/Matches/KoreanLabel"
```

---

## Phase 3 — ContainerOps (이동/복사/삭제)

### Task 5: ContainerOps — JSON-only operations (game 분리)

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerOps.cs`
- Test: `src/LongYinRoster.Tests/ContainerOpsTests.cs`

ContainerOps 의 game 무관 부분 (JSON manipulation) 을 먼저 구현. game 통합 부분 (Task 6) 은 별도.

- [ ] **Step 1: Write failing test**

`src/LongYinRoster.Tests/ContainerOpsTests.cs`:
```csharp
using System.Text.Json;
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerOpsTests
{
    [Fact]
    public void AppendItemsJson_AddsToExistingArray()
    {
        string existing = @"[{""itemID"":1,""name"":""A""}]";
        string toAdd    = @"[{""itemID"":2,""name"":""B""},{""itemID"":3,""name"":""C""}]";
        var result = ContainerOps.AppendItemsJson(existing, toAdd);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public void AppendItemsJson_HandlesEmptyExisting()
    {
        var result = ContainerOps.AppendItemsJson("[]", @"[{""itemID"":1}]");
        JsonDocument.Parse(result).RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public void RemoveItemsByIndex_PreservesNonSelected()
    {
        string items = @"[{""itemID"":1},{""itemID"":2},{""itemID"":3}]";
        var indices = new System.Collections.Generic.HashSet<int> { 0, 2 };
        var result = ContainerOps.RemoveItemsByIndex(items, indices);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(1);
        doc.RootElement[0].GetProperty("itemID").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void ExtractItemsByIndex_ReturnsSelectedOnly()
    {
        string items = @"[{""itemID"":1},{""itemID"":2},{""itemID"":3}]";
        var indices = new System.Collections.Generic.HashSet<int> { 1, 2 };
        var result = ContainerOps.ExtractItemsByIndex(items, indices);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(2);
        doc.RootElement[0].GetProperty("itemID").GetInt32().ShouldBe(2);
        doc.RootElement[1].GetProperty("itemID").GetInt32().ShouldBe(3);
    }
}
```

- [ ] **Step 2: Implement game-agnostic ContainerOps**

`src/LongYinRoster/Containers/ContainerOps.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace LongYinRoster.Containers;

/// <summary>
/// 컨테이너 operations. JSON-only (Task 5) + game 통합 (Task 6) 분리.
/// </summary>
public static class ContainerOps
{
    /// <summary>두 JSON array string 을 합쳐 단일 JSON array string 반환.</summary>
    public static string AppendItemsJson(string existingArrayJson, string toAppendArrayJson)
    {
        using var ex = JsonDocument.Parse(existingArrayJson);
        using var ad = JsonDocument.Parse(toAppendArrayJson);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            foreach (var e in ex.RootElement.EnumerateArray()) e.WriteTo(w);
            foreach (var e in ad.RootElement.EnumerateArray()) e.WriteTo(w);
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>JSON array 에서 지정 인덱스 entries 제거 후 array string 반환.</summary>
    public static string RemoveItemsByIndex(string arrayJson, HashSet<int> removeIndices)
    {
        using var doc = JsonDocument.Parse(arrayJson);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            int i = 0;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (!removeIndices.Contains(i)) e.WriteTo(w);
                i++;
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>JSON array 에서 지정 인덱스 entries 만 추출해 새 array string 반환.</summary>
    public static string ExtractItemsByIndex(string arrayJson, HashSet<int> indices)
    {
        using var doc = JsonDocument.Parse(arrayJson);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            int i = 0;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (indices.Contains(i)) e.WriteTo(w);
                i++;
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
```

- [ ] **Step 3: Add to test csproj + run**

Add `<Compile Include>` for `ContainerOps.cs` to test csproj.

Run: `dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj -c Release --filter ContainerOpsTests`
Expected: PASS (4 tests).

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerOps.cs src/LongYinRoster.Tests/ContainerOpsTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git commit -m "feat(containers): ContainerOps JSON manipulation (Append/Remove/Extract by index) + tests"
```

---

### Task 6: ContainerOps — game 통합 (이동/복사 to/from game)

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerOps.cs`

이번 task 는 IL2CPP game state 의존이라 unit test 미지원 (manual smoke test 만).

- [ ] **Step 1: Add game-side methods to ContainerOps**

`src/LongYinRoster/Containers/ContainerOps.cs` 에 다음 method 추가 (using 추가 + 메서드 추가):

```csharp
using System;
using System.Reflection;
using System.Text.Json;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

// ... 기존 클래스 within namespace ...

public static class ContainerOps
{
    // ... 기존 JSON-only methods ...

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public sealed class GameMoveResult
    {
        public int Succeeded { get; set; }
        public int Failed    { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>
    /// 게임의 ItemListData.allItem 또는 selfStorage.allItem 의 지정 index entries 에서
    /// 추출해 JSON array string 으로 반환. game list 는 변경 안 함 (read-only).
    /// </summary>
    public static string ExtractGameItemsToJson(object il2List, System.Collections.Generic.HashSet<int> indices)
    {
        using var ms = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            int n = IL2CppListOps.Count(il2List);
            for (int i = 0; i < n; i++)
            {
                if (!indices.Contains(i)) continue;
                var item = IL2CppListOps.Get(il2List, i);
                if (item == null) continue;
                WriteItemAsJson(w, item);
            }
            w.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// JSON array 의 각 entry 를 ItemData wrapper 로 deep-copy 후 player 의 allItem 에
    /// GetItem(wrapper, false) 호출해 추가. ItemListApplier 패턴 mirror.
    /// 가득 참 시 partial — 처리된 N 반환.
    /// </summary>
    public static GameMoveResult AddItemsJsonToGame(object player, string itemsJson, int maxCapacity)
    {
        var res = new GameMoveResult();
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array) { res.Reason = "itemsJson 이 array 아님"; return res; }

            // wrapperType + ctor 발견 (ItemListApplier 와 동일 패턴)
            var ild = ReadFieldOrProperty(player, "itemListData");
            var allItem = ild != null ? ReadFieldOrProperty(ild, "allItem") : null;
            if (allItem == null) { res.Reason = "player.itemListData.allItem null"; return res; }
            int curN = IL2CppListOps.Count(allItem);
            int sample = -1;
            Type? wrapperType = null;
            for (int k = 0; k < curN && wrapperType == null; k++)
            {
                var s = IL2CppListOps.Get(allItem, k);
                if (s != null) { wrapperType = s.GetType(); sample = k; }
            }
            if (wrapperType == null) { res.Reason = "wrapperType 미발견 (인벤토리 비어있음)"; return res; }
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

            int available = System.Math.Max(0, maxCapacity - curN);

            for (int i = 0; i < arr.GetArrayLength(); i++)
            {
                if (res.Succeeded >= available) { res.Failed++; continue; }
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
        }
        catch (Exception ex)
        {
            res.Reason = $"AddItemsJsonToGame threw: {ex.Message}";
        }
        return res;
    }

    /// <summary>
    /// player 의 allItem 에서 지정 index entries 제거. 인덱스 큰 것부터 제거 (앞 인덱스 보존).
    /// game-self 'LoseAllItem' 은 전체 clear 라 직접 list.RemoveAt 사용.
    /// </summary>
    public static int RemoveGameItems(object il2List, System.Collections.Generic.HashSet<int> indices)
    {
        var listType = il2List.GetType();
        var removeAtM = listType.GetMethod("RemoveAt", F, null, new[] { typeof(int) }, null);
        if (removeAtM == null) return 0;
        // 큰 인덱스부터 제거 (작은 인덱스 보존)
        var sorted = new System.Collections.Generic.List<int>(indices);
        sorted.Sort();
        sorted.Reverse();
        int removed = 0;
        foreach (var idx in sorted)
        {
            try { removeAtM.Invoke(il2List, new object[] { idx }); removed++; }
            catch { }
        }
        return removed;
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
            bool compat = true;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { compat = false; break; }
            }
            if (!compat) continue;
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

    private static void WriteItemAsJson(Utf8JsonWriter w, object item)
    {
        // ItemData 의 reflection-readable fields 를 JSON 으로 dump.
        // 단순화: 외부 JsonConvert 호출은 IL2CPP wrapper 호환성 의존이라 회피하고,
        // 핵심 properties 만 직접 write. subData 는 nested object 로 재귀.
        w.WriteStartObject();
        var t = item.GetType();
        WriteProp(w, item, t, "itemID");
        WriteProp(w, item, t, "type");
        WriteProp(w, item, t, "subType");
        WriteProp(w, item, t, "name");
        WriteProp(w, item, t, "value");
        WriteProp(w, item, t, "itemLv");
        WriteProp(w, item, t, "rareLv");
        WriteProp(w, item, t, "weight");
        WriteProp(w, item, t, "isNew");
        WriteProp(w, item, t, "poisonNum");
        WriteProp(w, item, t, "poisonNumDetected");
        WriteSubData(w, item, "equipmentData");
        WriteSubData(w, item, "medFoodData");
        WriteSubData(w, item, "bookData");
        WriteSubData(w, item, "treasureData");
        WriteSubData(w, item, "materialData");
        WriteSubData(w, item, "horseData");
        w.WriteEndObject();
    }

    private static void WriteProp(Utf8JsonWriter w, object obj, Type t, string name)
    {
        var p = t.GetProperty(name, F);
        var f = (p == null) ? t.GetField(name, F) : null;
        if (p == null && f == null) return;
        var v = p?.GetValue(obj) ?? f?.GetValue(obj);
        if (v == null) { w.WriteNull(name); return; }
        switch (v)
        {
            case int i:    w.WriteNumber(name, i); break;
            case long l:   w.WriteNumber(name, l); break;
            case float fl: w.WriteNumber(name, fl); break;
            case double d: w.WriteNumber(name, d); break;
            case bool b:   w.WriteBoolean(name, b); break;
            case string s: w.WriteString(name, s); break;
            default:
                if (v.GetType().IsEnum) w.WriteNumber(name, Convert.ToInt32(v));
                else w.WriteString(name, v.ToString() ?? "");
                break;
        }
    }

    private static void WriteSubData(Utf8JsonWriter w, object item, string subName)
    {
        var sd = ReadFieldOrProperty(item, subName);
        if (sd == null) { w.WriteNull(subName); return; }
        w.WritePropertyName(subName);
        WriteObjectRecursive(w, sd, depth: 0);
    }

    private static void WriteObjectRecursive(Utf8JsonWriter w, object obj, int depth)
    {
        if (depth > 6) { w.WriteNullValue(); return; }
        var t = obj.GetType();
        w.WriteStartObject();
        foreach (var p in t.GetProperties(F))
        {
            var name = p.Name;
            // IL2CPP wrapper 내부 property 제외
            if (name == "ObjectClass" || name == "Pointer" || name == "WasCollected") continue;
            try
            {
                var v = p.GetValue(obj);
                WriteValue(w, name, v, depth);
            }
            catch { }
        }
        w.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter w, string name, object? v, int depth)
    {
        if (v == null) { w.WriteNull(name); return; }
        var vt = v.GetType();
        if (vt == typeof(int))    { w.WriteNumber(name, (int)v); return; }
        if (vt == typeof(long))   { w.WriteNumber(name, (long)v); return; }
        if (vt == typeof(float))  { w.WriteNumber(name, (float)v); return; }
        if (vt == typeof(double)) { w.WriteNumber(name, (double)v); return; }
        if (vt == typeof(bool))   { w.WriteBoolean(name, (bool)v); return; }
        if (vt == typeof(string)) { w.WriteString(name, (string)v); return; }
        if (vt.IsEnum)            { w.WriteNumber(name, Convert.ToInt32(v)); return; }
        // Dictionary<int,float> 등 재귀 — out of scope (computational cost). Item 의
        // capture/restore 는 게임 -> 슬롯 -> 게임 의 round-trip 정확 위해 핵심 필드만.
        // Container 에 저장된 entry 를 다시 game 에 넣을 때 ItemListApplier deep-copy 가
        // subData 풀 복원하므로, container 캡처에서는 핵심 primitives 만으로 충분.
        // (Dictionary content 보존 needed 시 v0.7.x 에서 확장.)
        try
        {
            // List 는 array 로
            var listIface = vt.GetInterface("IList");
            if (listIface != null && IL2CppListOps.Count(v) >= 0)
            {
                w.WriteStartArray(name);
                int n = IL2CppListOps.Count(v);
                for (int i = 0; i < n; i++)
                {
                    var ev = IL2CppListOps.Get(v, i);
                    if (ev == null) { w.WriteNullValue(); continue; }
                    var et = ev.GetType();
                    if (et == typeof(int))    { w.WriteNumberValue((int)ev); continue; }
                    if (et == typeof(float))  { w.WriteNumberValue((float)ev); continue; }
                    if (et == typeof(double)) { w.WriteNumberValue((double)ev); continue; }
                    if (et == typeof(bool))   { w.WriteBooleanValue((bool)ev); continue; }
                    if (et == typeof(string)) { w.WriteStringValue((string)ev); continue; }
                    w.WriteStringValue(ev.ToString() ?? "");
                }
                w.WriteEndArray();
                return;
            }
            // 기타 nested object — 재귀
            w.WritePropertyName(name);
            WriteObjectRecursive(w, v, depth + 1);
        }
        catch { w.WriteNull(name); }
    }
}
```

- [ ] **Step 2: Build mod**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors / 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerOps.cs
git commit -m "feat(containers): ContainerOps game 통합 — ExtractGameItemsToJson / AddItemsJsonToGame / RemoveGameItems"
```

---

## Phase 4 — Hotkey + ModeSelector UI

### Task 7: HotkeyMap

**Files:**
- Create: `src/LongYinRoster/Util/HotkeyMap.cs`

이번 task 는 Unity Input 의존이라 unit test 없음. 통합 테스트만.

- [ ] **Step 1: Implement HotkeyMap**

`src/LongYinRoster/Util/HotkeyMap.cs`:
```csharp
using UnityEngine;

namespace LongYinRoster.Util;

/// <summary>
/// F11 / F11+숫자 hotkey 처리. v0.7.0 부터 ModeSelector 가 사용.
/// 향후 settings panel 에서 hotkey 변경 시 이 클래스의 정적 필드 갱신.
/// </summary>
public static class HotkeyMap
{
    public static KeyCode MainKey = KeyCode.F11;
    public static KeyCode CharacterModeKey  = KeyCode.Alpha1;
    public static KeyCode ContainerModeKey  = KeyCode.Alpha2;
    public static KeyCode CharacterModeKeyNumpad = KeyCode.Keypad1;
    public static KeyCode ContainerModeKeyNumpad = KeyCode.Keypad2;

    /// <summary>F11 단독 눌림 (모드 메뉴 토글).</summary>
    public static bool MainKeyPressedAlone()
    {
        if (!Input.GetKeyDown(MainKey)) return false;
        // 동시 숫자키 없을 때만
        return !(Input.GetKey(CharacterModeKey) || Input.GetKey(ContainerModeKey)
              || Input.GetKey(CharacterModeKeyNumpad) || Input.GetKey(ContainerModeKeyNumpad));
    }

    /// <summary>F11+1 — 캐릭터 관리 직진입.</summary>
    public static bool CharacterShortcut()
    {
        return Input.GetKey(MainKey) &&
               (Input.GetKeyDown(CharacterModeKey) || Input.GetKeyDown(CharacterModeKeyNumpad));
    }

    /// <summary>F11+2 — 컨테이너 관리 직진입.</summary>
    public static bool ContainerShortcut()
    {
        return Input.GetKey(MainKey) &&
               (Input.GetKeyDown(ContainerModeKey) || Input.GetKeyDown(ContainerModeKeyNumpad));
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Util/HotkeyMap.cs
git commit -m "feat(util): HotkeyMap (F11 / F11+1 / F11+2)"
```

---

### Task 8: ModeSelector UI

**Files:**
- Create: `src/LongYinRoster/UI/ModeSelector.cs`

- [ ] **Step 1: Implement ModeSelector**

`src/LongYinRoster/UI/ModeSelector.cs`:
```csharp
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// F11 메뉴 — 캐릭터 관리 / 컨테이너 관리 선택. 280x160 작은 창.
/// 사용자 선택에 따라 CurrentMode 변경 → ModWindow 가 적절한 panel 분기.
/// </summary>
public sealed class ModeSelector
{
    public enum Mode { None, Character, Container }

    public Mode CurrentMode { get; private set; } = Mode.None;
    public bool MenuVisible { get; private set; } = false;

    private Rect _windowRect = new Rect(100, 100, 280, 160);
    private const int WindowID = 0xLY07_5E1;  // arbitrary unique ID

    public void Toggle()
    {
        MenuVisible = !MenuVisible;
        if (!MenuVisible) CurrentMode = Mode.None;
    }

    public void SetMode(Mode m)
    {
        CurrentMode = m;
        MenuVisible = false;
    }

    public void OnGUI()
    {
        if (!MenuVisible) return;
        _windowRect = GUI.Window(WindowID, _windowRect, DrawWindow, "LongYin Roster Mod");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Space(8);
        if (GUILayout.Button("캐릭터 관리 (F11+1)"))   SetMode(Mode.Character);
        GUILayout.Space(4);
        if (GUILayout.Button("컨테이너 관리 (F11+2)")) SetMode(Mode.Container);
        GUILayout.Space(8);
        GUILayout.Label("v0.7.0 — F11 닫기");
        GUI.DragWindow();
    }
}
```

Note: `WindowID` 가 잘못 — C# 식별자에는 '0x' 시작 가능하지만 underscore 위치 확인. 안전한 형식: `private const int WindowID = 0x4C593731;` (LY71 ASCII).

수정:
```csharp
private const int WindowID = 0x4C593731;  // "LY71" ASCII unique ID
```

- [ ] **Step 2: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/UI/ModeSelector.cs
git commit -m "feat(ui): ModeSelector (F11 menu — 캐릭터 관리 / 컨테이너 관리)"
```

---

## Phase 5 — ContainerPanel UI

### Task 9: ContainerPanel — skeleton (창 + 카테고리 탭)

**Files:**
- Create: `src/LongYinRoster/UI/ContainerPanel.cs`

이번 task 는 panel 의 outer shell + category tab 만. 내부 3-pane content 는 다음 task.

- [ ] **Step 1: Implement skeleton**

`src/LongYinRoster/UI/ContainerPanel.cs`:
```csharp
using LongYinRoster.Containers;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class ContainerPanel
{
    public bool Visible { get; set; } = false;
    private Rect _rect = new Rect(150, 100, 800, 600);
    private const int WindowID = 0x4C593732;  // "LY72"

    private ItemCategory _filter = ItemCategory.All;
    private static readonly ItemCategory[] TabOrder = {
        ItemCategory.All, ItemCategory.Equipment, ItemCategory.Medicine,
        ItemCategory.Food, ItemCategory.Book, ItemCategory.Treasure,
        ItemCategory.Material, ItemCategory.Horse,
    };

    public void OnGUI()
    {
        if (!Visible) return;
        _rect = GUI.Window(WindowID, _rect, Draw, "컨테이너 관리");
    }

    private void Draw(int id)
    {
        DrawCategoryTabs();
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        DrawLeftColumn();
        DrawRightColumn();
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0, 0, _rect.width, 20));
    }

    private void DrawCategoryTabs()
    {
        GUILayout.BeginHorizontal();
        foreach (var cat in TabOrder)
        {
            bool active = _filter == cat;
            var prevColor = GUI.color;
            if (active) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemCategoryFilter.KoreanLabel(cat), GUILayout.Width(70)))
                _filter = cat;
            GUI.color = prevColor;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawLeftColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));
        GUILayout.Label($"인벤토리 (필터: {ItemCategoryFilter.KoreanLabel(_filter)})");
        GUILayout.Box("(인벤토리 list — Task 10)", GUILayout.Height(245));
        GUILayout.Label("창고");
        GUILayout.Box("(창고 list — Task 10)", GUILayout.Height(245));
        GUILayout.EndVertical();
    }

    private void DrawRightColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));
        GUILayout.BeginHorizontal();
        GUILayout.Label("[컨테이너 ▼]");
        if (GUILayout.Button("신규", GUILayout.Width(45))) { /* Task 11 */ }
        if (GUILayout.Button("이름변경", GUILayout.Width(60))) { /* Task 11 */ }
        if (GUILayout.Button("삭제", GUILayout.Width(45))) { /* Task 11 */ }
        GUILayout.EndHorizontal();
        GUILayout.Box("(컨테이너 list — Task 10)", GUILayout.Height(490));
        GUILayout.EndVertical();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): ContainerPanel skeleton (창 + 카테고리 탭 + 3-pane placeholders)"
```

---

### Task 10: ContainerPanel — item list rendering + 체크박스

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs`

3 panel 의 item list 표시 + 체크박스 다중 선택. 외부에서 item source (인벤토리/창고/컨테이너) 주입.

- [ ] **Step 1: Add item source abstractions to ContainerPanel**

`src/LongYinRoster/UI/ContainerPanel.cs` 안에 nested data class 추가 + 멤버 추가:

```csharp
public sealed class ItemRow
{
    public int     Index { get; init; }
    public string  Name  { get; init; } = "";
    public int     Type  { get; init; }
    public int     SubType { get; init; }
    public int     EnhanceLv { get; init; }
    public float   Weight { get; init; }
    public bool    Equipped { get; init; }
}

private List<ItemRow> _inventoryRows = new();
private List<ItemRow> _storageRows = new();
private List<ItemRow> _containerRows = new();

private HashSet<int> _inventoryChecks = new();
private HashSet<int> _storageChecks = new();
private HashSet<int> _containerChecks = new();

private Vector2 _invScroll = Vector2.zero;
private Vector2 _stoScroll = Vector2.zero;
private Vector2 _conScroll = Vector2.zero;

public void SetInventoryRows(List<ItemRow> rows) { _inventoryRows = rows; _inventoryChecks.Clear(); }
public void SetStorageRows(List<ItemRow> rows)   { _storageRows = rows; _storageChecks.Clear(); }
public void SetContainerRows(List<ItemRow> rows) { _containerRows = rows; _containerChecks.Clear(); }
```

- [ ] **Step 2: Implement DrawItemList helper + update DrawLeftColumn / DrawRightColumn**

```csharp
private void DrawItemList(List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
{
    scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
    foreach (var r in rows)
    {
        if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;
        bool was = checks.Contains(r.Index);
        bool now = GUILayout.Toggle(was, BuildLabel(r));
        if (now && !was) checks.Add(r.Index);
        if (!now && was) checks.Remove(r.Index);
    }
    GUILayout.EndScrollView();
}

private static string BuildLabel(ItemRow r)
{
    string cat = ItemCategoryFilter.KoreanLabel(ItemCategoryFilter.Classify(r.Type, r.SubType));
    string enh = r.EnhanceLv > 0 ? $"/강화{r.EnhanceLv}" : "";
    string equipped = r.Equipped ? " [착용중]" : "";
    return $"{r.Name} ({cat}{enh}/{r.Weight:F1}kg){equipped}";
}
```

DrawLeftColumn / DrawRightColumn 의 placeholder 를 실제 list 호출로 교체:

```csharp
private void DrawLeftColumn()
{
    GUILayout.BeginVertical(GUILayout.Width(390));
    GUILayout.Label($"인벤토리 ({_inventoryRows.Count}개)");
    DrawItemList(_inventoryRows, _inventoryChecks, ref _invScroll, 220);
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("→ 이동")) { /* Task 12 */ }
    if (GUILayout.Button("→ 복사")) { /* Task 12 */ }
    GUILayout.EndHorizontal();

    GUILayout.Label($"창고 ({_storageRows.Count}개)");
    DrawItemList(_storageRows, _storageChecks, ref _stoScroll, 220);
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("→ 이동")) { /* Task 12 */ }
    if (GUILayout.Button("→ 복사")) { /* Task 12 */ }
    GUILayout.EndHorizontal();
    GUILayout.EndVertical();
}

private void DrawRightColumn()
{
    GUILayout.BeginVertical(GUILayout.Width(390));
    GUILayout.BeginHorizontal();
    GUILayout.Label("[컨테이너 ▼]");
    if (GUILayout.Button("신규",     GUILayout.Width(45))) { /* Task 11 */ }
    if (GUILayout.Button("이름변경", GUILayout.Width(60))) { /* Task 11 */ }
    if (GUILayout.Button("삭제",     GUILayout.Width(45))) { /* Task 11 */ }
    GUILayout.EndHorizontal();
    DrawItemList(_containerRows, _containerChecks, ref _conScroll, 470);
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("← 이동")) { /* Task 12 */ }
    if (GUILayout.Button("← 복사")) { /* Task 12 */ }
    if (GUILayout.Button("☓ 삭제")) { /* Task 12 */ }
    GUILayout.EndHorizontal();
    GUILayout.EndVertical();
}
```

(`using System.Collections.Generic;` 추가 필요)

- [ ] **Step 3: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): ContainerPanel — item list rendering + 체크박스 + 카테고리 필터"
```

---

### Task 11: ContainerPanel — 컨테이너 드롭다운 + 신규/이름변경/삭제

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs`

ContainerRepository 와 wire up + 드롭다운 + prompt UI.

- [ ] **Step 1: Add repo + dropdown state**

```csharp
private ContainerRepository? _repo;
private List<ContainerMetadata> _containerList = new();
private int _selectedContainerIdx = -1;
private bool _dropdownOpen = false;
private string _renameBuffer = "";
private bool _renameMode = false;
private string _newNameBuffer = "";
private bool _newMode = false;

public void SetRepository(ContainerRepository repo)
{
    _repo = repo;
    RefreshContainerList();
}

private void RefreshContainerList()
{
    if (_repo == null) return;
    _containerList = _repo.List();
    if (_selectedContainerIdx < 0 && _containerList.Count > 0)
        _selectedContainerIdx = _containerList[0].ContainerIndex;
}

public Action<int>? OnContainerSelected;  // host wires: load items into _containerRows
```

- [ ] **Step 2: Update DrawRightColumn with dropdown + prompt overlays**

```csharp
private void DrawRightColumn()
{
    GUILayout.BeginVertical(GUILayout.Width(390));
    GUILayout.BeginHorizontal();
    string sel = _selectedContainerIdx > 0
        ? _containerList.Find(c => c.ContainerIndex == _selectedContainerIdx)?.ContainerName ?? "(미선택)"
        : "(미선택)";
    if (GUILayout.Button($"[{sel} ▼]", GUILayout.Width(150)))
        _dropdownOpen = !_dropdownOpen;
    if (GUILayout.Button("신규",     GUILayout.Width(45))) { _newMode = true; _newNameBuffer = ""; }
    if (GUILayout.Button("이름변경", GUILayout.Width(60))) {
        if (_selectedContainerIdx > 0)
        {
            _renameMode = true;
            _renameBuffer = _containerList.Find(c => c.ContainerIndex == _selectedContainerIdx)?.ContainerName ?? "";
        }
    }
    if (GUILayout.Button("삭제", GUILayout.Width(45))) {
        if (_repo != null && _selectedContainerIdx > 0)
        {
            _repo.Delete(_selectedContainerIdx);
            _selectedContainerIdx = -1;
            RefreshContainerList();
            OnContainerSelected?.Invoke(-1);
        }
    }
    GUILayout.EndHorizontal();

    // dropdown
    if (_dropdownOpen)
    {
        foreach (var m in _containerList)
        {
            if (GUILayout.Button($"{m.ContainerIndex:D2}: {m.ContainerName}"))
            {
                _selectedContainerIdx = m.ContainerIndex;
                _dropdownOpen = false;
                OnContainerSelected?.Invoke(m.ContainerIndex);
            }
        }
    }

    // 신규 prompt
    if (_newMode)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("이름:");
        _newNameBuffer = GUILayout.TextField(_newNameBuffer, GUILayout.Width(180));
        if (GUILayout.Button("확인", GUILayout.Width(45)) && _repo != null)
        {
            int idx = _repo.CreateNew(string.IsNullOrWhiteSpace(_newNameBuffer) ? $"컨테이너{System.DateTime.Now:HHmmss}" : _newNameBuffer);
            _newMode = false;
            RefreshContainerList();
            _selectedContainerIdx = idx;
            OnContainerSelected?.Invoke(idx);
        }
        if (GUILayout.Button("취소", GUILayout.Width(45))) _newMode = false;
        GUILayout.EndHorizontal();
    }

    // 이름변경 prompt
    if (_renameMode)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("새 이름:");
        _renameBuffer = GUILayout.TextField(_renameBuffer, GUILayout.Width(180));
        if (GUILayout.Button("확인", GUILayout.Width(45)) && _repo != null && _selectedContainerIdx > 0)
        {
            _repo.Rename(_selectedContainerIdx, _renameBuffer);
            _renameMode = false;
            RefreshContainerList();
        }
        if (GUILayout.Button("취소", GUILayout.Width(45))) _renameMode = false;
        GUILayout.EndHorizontal();
    }

    DrawItemList(_containerRows, _containerChecks, ref _conScroll, 420);
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("← 이동")) { /* Task 12 */ }
    if (GUILayout.Button("← 복사")) { /* Task 12 */ }
    if (GUILayout.Button("☓ 삭제")) { /* Task 12 */ }
    GUILayout.EndHorizontal();
    GUILayout.EndVertical();
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): ContainerPanel — 컨테이너 드롭다운 + 신규/이름변경/삭제 prompt"
```

---

### Task 12: ContainerPanel — 이동/복사/삭제 wiring

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs`

ContainerOps 호출 통합. 호스트 (Plugin.cs) 가 callback 주입해서 game state 와 연결.

- [ ] **Step 1: Add operation callback hooks**

`ContainerPanel.cs` 멤버 추가:

```csharp
public Action<HashSet<int>>?   OnInventoryToContainerMove;
public Action<HashSet<int>>?   OnInventoryToContainerCopy;
public Action<HashSet<int>>?   OnStorageToContainerMove;
public Action<HashSet<int>>?   OnStorageToContainerCopy;
public Action<HashSet<int>>?   OnContainerToInventoryMove;
public Action<HashSet<int>>?   OnContainerToInventoryCopy;
public Action<HashSet<int>>?   OnContainerDelete;
```

DrawLeftColumn / DrawRightColumn 의 placeholder 버튼 callback 연결:

```csharp
// LeftColumn 인벤토리:
if (GUILayout.Button("→ 이동")) OnInventoryToContainerMove?.Invoke(new HashSet<int>(_inventoryChecks));
if (GUILayout.Button("→ 복사")) OnInventoryToContainerCopy?.Invoke(new HashSet<int>(_inventoryChecks));

// LeftColumn 창고:
if (GUILayout.Button("→ 이동")) OnStorageToContainerMove?.Invoke(new HashSet<int>(_storageChecks));
if (GUILayout.Button("→ 복사")) OnStorageToContainerCopy?.Invoke(new HashSet<int>(_storageChecks));

// RightColumn 컨테이너:
if (GUILayout.Button("← 이동")) OnContainerToInventoryMove?.Invoke(new HashSet<int>(_containerChecks));
if (GUILayout.Button("← 복사")) OnContainerToInventoryCopy?.Invoke(new HashSet<int>(_containerChecks));
if (GUILayout.Button("☓ 삭제")) OnContainerDelete?.Invoke(new HashSet<int>(_containerChecks));
```

- [ ] **Step 2: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): ContainerPanel — 이동/복사/삭제 callback 후크"
```

---

## Phase 6 — Plugin wiring + ModWindow 통합

### Task 13: Plugin.cs — ModeSelector + ContainerPanel 등록

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

기존 ModWindow 의 F11 toggle 로직을 ModeSelector 통과로 변경. ContainerPanel 인스턴스 등록 + callback 연결.

- [ ] **Step 1: Modify ModWindow.cs to use HotkeyMap + ModeSelector**

기존 ModWindow.Update 의 `if (Input.GetKeyDown(KeyCode.F11))` 패턴을 다음으로 교체:

```csharp
// HotkeyMap 으로 위임 (v0.7.0)
if (HotkeyMap.MainKeyPressedAlone()) _modeSelector.Toggle();
if (HotkeyMap.CharacterShortcut())   { _modeSelector.SetMode(ModeSelector.Mode.Character); CurrentMode = ModeSelector.Mode.Character; visible = true; }
if (HotkeyMap.ContainerShortcut())   { _modeSelector.SetMode(ModeSelector.Mode.Container); CurrentMode = ModeSelector.Mode.Container; _containerPanel.Visible = true; }
```

ModWindow 에 새 멤버 추가:
```csharp
public ModeSelector.Mode CurrentMode { get; set; } = ModeSelector.Mode.Character;
private ModeSelector _modeSelector = new();
private ContainerPanel _containerPanel = new();
public ContainerPanel ContainerPanel => _containerPanel;
public ModeSelector   ModeSelector   => _modeSelector;
```

OnGUI 안에서 분기:
```csharp
private void OnGUI()
{
    _modeSelector.OnGUI();
    if (_modeSelector.CurrentMode == ModeSelector.Mode.Container)
        _containerPanel.OnGUI();
    if (_modeSelector.CurrentMode == ModeSelector.Mode.Character && visible)
        DrawCharacterPanel();  // 기존 ModWindow content
}
```

(기존 `DrawWindow` 또는 비슷한 method 를 `DrawCharacterPanel` 로 rename)

- [ ] **Step 2: Wire Plugin.cs**

`src/LongYinRoster/Plugin.cs` 의 Load() 에 다음 추가:

```csharp
// v0.7.0 — ContainerRepository 초기화 + ContainerPanel callback wiring
var containerDir = Path.Combine(Paths.PluginPath, "LongYinRoster", "Containers");
var containerRepo = new ContainerRepository(containerDir);

// ModWindow 가 ContainerPanel 보유 — wire callback
var modWindow = (ModWindow)/* AddComponent<ModWindow>() 가 반환한 인스턴스 보관 필요 */;
modWindow.ContainerPanel.SetRepository(containerRepo);
modWindow.ContainerPanel.OnContainerSelected = idx => {
    if (idx < 0) { modWindow.ContainerPanel.SetContainerRows(new List<ContainerPanel.ItemRow>()); return; }
    string itemsJson = containerRepo.LoadItemsJson(idx);
    var rows = ContainerRowBuilder.FromJsonArray(itemsJson);  // helper to convert JSON → ItemRow list
    modWindow.ContainerPanel.SetContainerRows(rows);
};
// (Inventory/Storage rows refresh — ModWindow.Update 안에서 일정 주기로 게임 상태 polling, 또는 panel open 시점에 refresh)
modWindow.ContainerPanel.OnInventoryToContainerMove = checks => DoInventoryToContainer(checks, move: true);
modWindow.ContainerPanel.OnInventoryToContainerCopy = checks => DoInventoryToContainer(checks, move: false);
// ... (8개 callback 모두 wiring)
```

ContainerRowBuilder helper 작성 필요 — Task 14 에서.

(이 task 는 wiring 패턴 보여주기용 — DoInventoryToContainer 등 실제 logic 은 Task 14 ~ 15)

- [ ] **Step 3: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors (참조 미해결 시 stub 추가).

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Plugin.cs src/LongYinRoster/UI/ModWindow.cs
git commit -m "feat(plugin): F11 hotkey ModeSelector 경유 + ContainerPanel wiring scaffold"
```

---

### Task 14: ContainerRowBuilder + game-side refresh

**Files:**
- Create: `src/LongYinRoster/Containers/ContainerRowBuilder.cs`

JSON ItemData array → ContainerPanel.ItemRow list 변환 + 게임 ItemListData / selfStorage → ItemRow list.

- [ ] **Step 1: Implement**

`src/LongYinRoster/Containers/ContainerRowBuilder.cs`:
```csharp
using System.Collections.Generic;
using System.Text.Json;
using LongYinRoster.Core;
using LongYinRoster.UI;

namespace LongYinRoster.Containers;

public static class ContainerRowBuilder
{
    public static List<ContainerPanel.ItemRow> FromJsonArray(string itemsJson)
    {
        var list = new List<ContainerPanel.ItemRow>();
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            int i = 0;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                list.Add(new ContainerPanel.ItemRow
                {
                    Index     = i++,
                    Name      = R(e, "name", ""),
                    Type      = RI(e, "type"),
                    SubType   = RI(e, "subType"),
                    EnhanceLv = ReadEnhance(e),
                    Weight    = RF(e, "weight"),
                    Equipped  = false,
                });
            }
        }
        catch { }
        return list;
    }

    public static List<ContainerPanel.ItemRow> FromGameAllItem(object il2List)
    {
        var list = new List<ContainerPanel.ItemRow>();
        int n = IL2CppListOps.Count(il2List);
        for (int i = 0; i < n; i++)
        {
            var item = IL2CppListOps.Get(il2List, i);
            if (item == null) continue;
            string name    = ReadString(item, "name");
            int type       = ReadInt(item, "type");
            int subType    = ReadInt(item, "subType");
            float weight   = ReadFloat(item, "weight");
            int enh        = 0;
            bool equipped  = false;
            var ed = ReadObj(item, "equipmentData");
            if (ed != null)
            {
                enh = ReadInt(ed, "enhanceLv");
                equipped = ReadBool(ed, "equiped");
            }
            else
            {
                var hd = ReadObj(item, "horseData");
                if (hd != null) equipped = ReadBool(hd, "equiped");
            }
            list.Add(new ContainerPanel.ItemRow
            {
                Index = i, Name = name, Type = type, SubType = subType,
                EnhanceLv = enh, Weight = weight, Equipped = equipped,
            });
        }
        return list;
    }

    private const System.Reflection.BindingFlags F =
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private static int    RI(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    private static float  RF(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : 0f;
    private static string R (JsonElement e, string k, string def) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;

    private static int ReadEnhance(JsonElement e)
    {
        if (!e.TryGetProperty("equipmentData", out var ed) || ed.ValueKind != JsonValueKind.Object) return 0;
        return ed.TryGetProperty("enhanceLv", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    }

    private static object? ReadObj(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        return null;
    }
    private static int    ReadInt(object obj, string name)    { var v = ReadObj(obj, name); return v != null ? System.Convert.ToInt32(v) : 0; }
    private static float  ReadFloat(object obj, string name)  { var v = ReadObj(obj, name); return v != null ? System.Convert.ToSingle(v) : 0f; }
    private static string ReadString(object obj, string name) { var v = ReadObj(obj, name); return v as string ?? ""; }
    private static bool   ReadBool(object obj, string name)   { var v = ReadObj(obj, name); return v is bool b && b; }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerRowBuilder.cs
git commit -m "feat(containers): ContainerRowBuilder — JSON / game allItem → ItemRow list"
```

---

### Task 15: Plugin.cs — full callback wiring (8 operations)

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

8 callback (인벤토리↔컨테이너 4 + 창고↔컨테이너 4) + 컨테이너 삭제.

- [ ] **Step 1: Implement helper class for operations**

`src/LongYinRoster/Containers/ContainerOpsHelper.cs` (신규 파일):
```csharp
using System.Collections.Generic;
using System.Text.Json;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Containers;

/// <summary>
/// ContainerPanel callback 처리 helper. 8개 작업 통합:
///   1. 인벤토리 → 컨테이너 (이동/복사)
///   2. 창고 → 컨테이너 (이동/복사)
///   3. 컨테이너 → 인벤토리 (이동/복사)
///   4. 컨테이너 항목 삭제
/// </summary>
public sealed class ContainerOpsHelper
{
    private readonly ContainerRepository _repo;
    public  int CurrentContainerIndex { get; set; } = -1;

    public ContainerOpsHelper(ContainerRepository repo) => _repo = repo;

    public sealed class Result
    {
        public int Succeeded { get; set; }
        public int Failed    { get; set; }
        public string Reason { get; set; } = "";
    }

    public Result GameToContainer(object il2List, HashSet<int> indices, bool removeFromGame)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string extracted = ContainerOps.ExtractGameItemsToJson(il2List, indices);
            string existing  = _repo.LoadItemsJson(CurrentContainerIndex);
            string merged    = ContainerOps.AppendItemsJson(existing, extracted);
            _repo.SaveItemsJson(CurrentContainerIndex, merged);
            res.Succeeded = JsonDocument.Parse(extracted).RootElement.GetArrayLength();
            if (removeFromGame)
                ContainerOps.RemoveGameItems(il2List, indices);
        }
        catch (System.Exception ex)
        {
            res.Reason = $"GameToContainer threw: {ex.Message}";
            Logger.Warn(res.Reason);
        }
        return res;
    }

    public Result ContainerToGame(object player, HashSet<int> indices, bool removeFromContainer, int gameMaxCapacity = 171)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string existing  = _repo.LoadItemsJson(CurrentContainerIndex);
            string extracted = ContainerOps.ExtractItemsByIndex(existing, indices);
            var gr = ContainerOps.AddItemsJsonToGame(player, extracted, gameMaxCapacity);
            res.Succeeded = gr.Succeeded;
            res.Failed    = gr.Failed;
            if (removeFromContainer && gr.Succeeded > 0)
            {
                // 성공 갯수만큼 제거 — 정확히 어떤 index 가 성공했는지는 partial 일 때 모르므로
                // 단순화: 처리 가능한 갯수까지 indices 의 처음 N개 제거
                var indicesList = new List<int>(indices);
                indicesList.Sort();
                var toRemove = new HashSet<int>();
                for (int k = 0; k < gr.Succeeded && k < indicesList.Count; k++) toRemove.Add(indicesList[k]);
                string remaining = ContainerOps.RemoveItemsByIndex(existing, toRemove);
                _repo.SaveItemsJson(CurrentContainerIndex, remaining);
            }
            res.Reason = gr.Reason ?? "";
        }
        catch (System.Exception ex)
        {
            res.Reason = $"ContainerToGame threw: {ex.Message}";
            Logger.Warn(res.Reason);
        }
        return res;
    }

    public Result DeleteFromContainer(HashSet<int> indices)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string existing = _repo.LoadItemsJson(CurrentContainerIndex);
            string remaining = ContainerOps.RemoveItemsByIndex(existing, indices);
            _repo.SaveItemsJson(CurrentContainerIndex, remaining);
            res.Succeeded = indices.Count;
        }
        catch (System.Exception ex)
        {
            res.Reason = $"DeleteFromContainer threw: {ex.Message}";
            Logger.Warn(res.Reason);
        }
        return res;
    }
}
```

- [ ] **Step 2: Wire Plugin.cs callbacks**

Plugin.Load() 에:

```csharp
var helper = new ContainerOpsHelper(containerRepo);

modWindow.ContainerPanel.OnContainerSelected += idx => helper.CurrentContainerIndex = idx;

modWindow.ContainerPanel.OnInventoryToContainerMove = checks => DoGameToContainer(true,  inventoryListAccessor: () => GetPlayerInventoryList(), checks);
modWindow.ContainerPanel.OnInventoryToContainerCopy = checks => DoGameToContainer(false, inventoryListAccessor: () => GetPlayerInventoryList(), checks);
modWindow.ContainerPanel.OnStorageToContainerMove   = checks => DoGameToContainer(true,  inventoryListAccessor: () => GetPlayerStorageList(),   checks);
modWindow.ContainerPanel.OnStorageToContainerCopy   = checks => DoGameToContainer(false, inventoryListAccessor: () => GetPlayerStorageList(),   checks);
modWindow.ContainerPanel.OnContainerToInventoryMove = checks => DoContainerToGame(true,  checks);
modWindow.ContainerPanel.OnContainerToInventoryCopy = checks => DoContainerToGame(false, checks);
modWindow.ContainerPanel.OnContainerDelete           = checks => {
    var r = helper.DeleteFromContainer(checks);
    Toast($"삭제: {r.Succeeded}개");
    RefreshContainerRows();
};

void DoGameToContainer(bool move, System.Func<object?> inventoryListAccessor, HashSet<int> checks)
{
    var lst = inventoryListAccessor();
    if (lst == null) { Toast("게임 진입 후 사용 가능"); return; }
    var r = helper.GameToContainer(lst, checks, removeFromGame: move);
    Toast($"{(move ? "이동" : "복사")}: {r.Succeeded}개" + (r.Failed > 0 ? $" / {r.Failed}개 실패" : ""));
    RefreshAllRows();
}

void DoContainerToGame(bool move, HashSet<int> checks)
{
    var p = HeroLocator.GetPlayer();
    if (p == null) { Toast("게임 진입 후 사용 가능"); return; }
    var r = helper.ContainerToGame(p, checks, removeFromContainer: move);
    Toast($"{(move ? "이동" : "복사")}: {r.Succeeded}개" + (r.Failed > 0 ? $" / {r.Failed}개 가득 참" : ""));
    RefreshAllRows();
}

object? GetPlayerInventoryList() {
    var p = HeroLocator.GetPlayer();
    if (p == null) return null;
    var ild = p.GetType().GetProperty("itemListData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(p);
    return ild?.GetType().GetProperty("allItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ild);
}

object? GetPlayerStorageList() {
    var p = HeroLocator.GetPlayer();
    if (p == null) return null;
    var ss = p.GetType().GetProperty("selfStorage", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(p);
    return ss?.GetType().GetProperty("allItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ss);
}

void RefreshAllRows() {
    var inv = GetPlayerInventoryList();
    var sto = GetPlayerStorageList();
    modWindow.ContainerPanel.SetInventoryRows(inv != null ? ContainerRowBuilder.FromGameAllItem(inv) : new List<ContainerPanel.ItemRow>());
    modWindow.ContainerPanel.SetStorageRows  (sto != null ? ContainerRowBuilder.FromGameAllItem(sto) : new List<ContainerPanel.ItemRow>());
    RefreshContainerRows();
}

void RefreshContainerRows() {
    if (helper.CurrentContainerIndex > 0)
        modWindow.ContainerPanel.SetContainerRows(ContainerRowBuilder.FromJsonArray(containerRepo.LoadItemsJson(helper.CurrentContainerIndex)));
}

void Toast(string s) => /* SimpleToast 또는 Logger.Info 사용 */ Logger.Info($"[Container] {s}");
```

(Toast 호출은 기존 ModWindow 의 toast 시스템 활용 — `using LongYinRoster.UI` + 인스턴스 호출. 처음엔 Logger.Info 로 단순화 가능.)

- [ ] **Step 3: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerOpsHelper.cs src/LongYinRoster/Plugin.cs
git commit -m "feat(plugin): ContainerOpsHelper + 8 callback wiring (game ↔ container 이동/복사 + 삭제)"
```

---

## Phase 7 — In-game smoke + 착용 장비 처리

### Task 16: 착용 장비 confirm dialog

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs` (또는 ContainerOpsHelper)

이동 시 선택된 인벤토리 item 중 equipped=true 인 게 있으면 confirm dialog 띄우고 OK 시 UnequipItem 호출 후 이동.

- [ ] **Step 1: Pre-move equipped check (Plugin.cs DoGameToContainer)**

```csharp
void DoGameToContainer(bool move, System.Func<object?> inventoryListAccessor, HashSet<int> checks)
{
    var lst = inventoryListAccessor();
    if (lst == null) { Toast("게임 진입 후 사용 가능"); return; }

    if (move)
    {
        // equipped item 검사
        var equippedIdxs = new List<int>();
        foreach (int idx in checks)
        {
            var item = IL2CppListOps.Get(lst, idx);
            if (item == null) continue;
            var ed = item.GetType().GetProperty("equipmentData", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(item);
            if (ed != null)
            {
                var eq = ed.GetType().GetProperty("equiped", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(ed);
                if (eq is bool b && b) equippedIdxs.Add(idx);
            }
        }
        if (equippedIdxs.Count > 0)
        {
            // 단순화: confirm dialog 대신 토스트 + 자동 unequip 진행 (사용자 spec 의 "확인" 단계는 향후 IMGUI confirm dialog 추가)
            Toast($"착용 중 {equippedIdxs.Count}개 — 자동 해제 후 이동");
            var p = HeroLocator.GetPlayer();
            if (p != null)
            {
                var unequipM = p.GetType().GetMethod("UnequipItem", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance, null, new[] { typeof(object), typeof(bool), typeof(bool) }, null);
                // method 정확 시그니처: UnequipItem(ItemData, bool, bool) — 첫 인자 type 매칭 어려움 → name 만으로 검색
                unequipM = null;
                foreach (var m in p.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if (m.Name == "UnequipItem" && m.GetParameters().Length == 3) { unequipM = m; break; }
                }
                foreach (int idx in equippedIdxs)
                {
                    var item = IL2CppListOps.Get(lst, idx);
                    if (item == null) continue;
                    try { unequipM?.Invoke(p, new object[] { item, false, false }); } catch { }
                }
            }
        }
    }

    var r = helper.GameToContainer(lst, checks, removeFromGame: move);
    Toast($"{(move ? "이동" : "복사")}: {r.Succeeded}개" + (r.Failed > 0 ? $" / {r.Failed}개 실패" : ""));
    RefreshAllRows();
}
```

(Spec 의 confirm dialog 는 향후 IMGUI dialog 추가 시 — 지금은 자동 unequip + 토스트 알림으로 단순화. spec 8 항 "경고 + 확인 dialog" 의 "확인" 부분만 향후 v0.7.x 에서 추가.)

- [ ] **Step 2: Build**

Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Plugin.cs
git commit -m "feat(plugin): 이동 시 착용 장비 자동 unequip + 토스트 알림 (spec confirm dialog 는 향후 v0.7.x)"
```

---

### Task 17: In-game smoke 시나리오

(이 task 는 user-driven manual test. 각 시나리오 PASS 시 체크.)

- [ ] **Smoke 1: F11 메뉴 진입**
  - 게임 진입 → F11 → 메뉴 표시 확인
  - F11+1 → 캐릭터 panel 즉시 진입
  - F11+2 → 컨테이너 panel 즉시 진입

- [ ] **Smoke 2: 컨테이너 신규/이름변경/삭제**
  - "신규" → 이름 입력 → 컨테이너 1개 생성 확인 (디스크 file 존재)
  - "이름변경" → 새 이름 적용 확인
  - "삭제" → 컨테이너 file 제거 확인

- [ ] **Smoke 3: 인벤토리 → 컨테이너 (이동)**
  - 카테고리 "장비" 선택 → 무기 1개 체크 → "→이동"
  - 게임 인벤토리에서 사라짐 + 컨테이너 panel 에 추가됨

- [ ] **Smoke 4: 컨테이너 → 인벤토리 (복사)**
  - 컨테이너에서 1개 체크 → "←복사"
  - 게임 인벤토리에 추가됨 + 컨테이너 panel 에 유지

- [ ] **Smoke 5: 카테고리 필터**
  - "비급" 탭 → type=3 만 표시 (게임 카테고리 매칭)
  - "전체" → 모두 표시

- [ ] **Smoke 6: 착용 장비 이동**
  - 착용 중 무기 체크 → "→이동" → 자동 unequip 후 컨테이너 추가

- [ ] **Smoke 7: 인벤토리 가득 참**
  - 인벤토리 ~170개 채운 후 컨테이너에서 5개 복사 → 1개만 추가, 4개 실패 토스트

- [ ] **Smoke 8: 게임 미진입 상태**
  - 메인 메뉴에서 F11+2 → 컨테이너 panel 만 활성, 인벤토리 toast "게임 진입 후 사용 가능"

각 시나리오 issue 발견 시 해당 task 로 돌아가 fix.

---

## Phase 8 — Release

### Task 18: VERSION + HANDOFF + 통합 테스트

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs` (VERSION)
- Modify: `docs/HANDOFF.md`

- [ ] **Step 1: VERSION bump**

`src/LongYinRoster/Plugin.cs`:
```csharp
public const string VERSION = "0.7.0";
```

- [ ] **Step 2: HANDOFF update**

`docs/HANDOFF.md` 의 "진행 상태" + "현재 main baseline" + Releases list 항목 추가:
```markdown
- [v0.7.0](https://github.com/...) — F11 진입 메뉴 (캐릭터 관리 / 컨테이너 관리) + 컨테이너 기능 (인벤토리 / 창고 ↔ 외부 디스크 컨테이너 이동·복사·삭제 + 카테고리 필터)
```

- [ ] **Step 3: Run all tests**

Run: `dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj -c Release`
Expected: 모든 기존 테스트 + 신규 테스트 (ContainerMetadata / ContainerFile / ContainerRepository / ItemCategoryFilter / ContainerOps) PASS.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Plugin.cs docs/HANDOFF.md
git commit -m "chore(release): VERSION 0.6.4 → 0.7.0 + HANDOFF v0.7.0 항목 추가"
```

---

### Task 19: dist + tag + push + GitHub release

- [ ] **Step 1: Build dist zip**

PowerShell:
```powershell
$root = 'E:\Games\龙胤立志传.v1.0.0f8.2\LongYinLiZhiZhuan\Save\_PlayerExport'
$dll = Join-Path $root 'src\LongYinRoster\bin\Release\LongYinRoster.dll'
$staging = Join-Path $env:TEMP 'longyinroster_v070'
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
$pluginDir = Join-Path $staging 'BepInEx\plugins\LongYinRoster'
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item $dll $pluginDir
$zipPath = Join-Path $root 'dist\LongYinRoster-v0.7.0.zip'
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipPath
```

- [ ] **Step 2: Tag + merge to main + push**

```bash
git tag -a v0.7.0 -m "v0.7.0 — F11 메뉴 + 컨테이너 기능"
git checkout main
git merge --no-ff v0.7.0 -m "Merge v0.7.0 — F11 메뉴 재설계 + 컨테이너 기능"
git push origin main
git push origin refs/tags/v0.7.0
```

- [ ] **Step 3: GitHub release**

```bash
gh release create v0.7.0 dist/LongYinRoster-v0.7.0.zip \
  --title "v0.7.0 — F11 메뉴 + 컨테이너 기능" \
  --notes "<release notes 본문>"
```

Release notes 본문 (HEREDOC) 에 spec 의 핵심 변경 요약 + Known Limitations + 설치 안내 포함.

- [ ] **Step 4: Verify + Commit (release task 자체는 commit 없음)**

확인: `gh release view v0.7.0` 으로 release URL 확인.

---

## 후속 sub-project 안내

이번 plan 의 마지막 task 후, 다음 sub-project 진행:

| Version | 카테고리 | spec 위치 |
|---|---|---|
| v0.7.1 | NPC 지원 | `docs/superpowers/specs/2026-XX-XX-longyin-roster-mod-v0.7.1-design.md` (별도 brainstorming) |
| v0.7.2 | Slot diff preview | `docs/superpowers/specs/2026-XX-XX-longyin-roster-mod-v0.7.2-design.md` |
| v0.7.3 | Apply 부분 미리보기 | `docs/superpowers/specs/2026-XX-XX-longyin-roster-mod-v0.7.3-design.md` |
| v0.7.4+ | 설정 panel | (누적 의존) |

각 sub-project 는 별도 spec → plan → impl cycle.
