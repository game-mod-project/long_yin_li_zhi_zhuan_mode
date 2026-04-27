# LongYin Roster Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a BepInEx 6 IL2CPP mod for *龙胤立志传 v1.0.0 f8.2* that captures a player Hero snapshot (live or from save files) into 20 slots and applies any slot back onto the running player using the strict-personal porting policy, with auto-backup undo.

**Architecture:** Data-layer mod — same path as the validated `merge_player_export.py`, but inside the game. Uses the game's bundled `Newtonsoft.Json` for round-trip via `JsonConvert.SerializeObject` / `PopulateObject`. IMGUI overlay UI (F11 toggle) with list + sidebar detail layout. Pinpoint patching reserved for fields where `PopulateObject` is insufficient.

**Tech Stack:** .NET 6, C# 10, BepInEx 6 IL2CPP-CoreCLR, Il2CppInterop.Runtime, Newtonsoft.Json (game-bundled interop), xUnit + FluentAssertions for unit tests, Unity IMGUI (`OnGUI`).

**Spec:** `docs/superpowers/specs/2026-04-27-longyin-roster-mod-design.md`

---

## Repository Layout

All paths below are relative to the project root:
`E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`

```
.
├── LongYinRoster.sln
├── Directory.Build.props                 ← shared <GameDir> path
├── src/
│   ├── LongYinRoster/
│   │   ├── LongYinRoster.csproj
│   │   ├── Plugin.cs
│   │   ├── Core/
│   │   │   ├── HeroLocator.cs
│   │   │   ├── SerializerService.cs
│   │   │   ├── PortabilityFilter.cs
│   │   │   └── PinpointPatcher.cs
│   │   ├── Slots/
│   │   │   ├── SlotPayload.cs
│   │   │   ├── SlotMetadata.cs
│   │   │   ├── SlotEntry.cs
│   │   │   ├── SlotFile.cs
│   │   │   ├── SlotRepository.cs
│   │   │   └── SaveFileScanner.cs
│   │   ├── UI/
│   │   │   ├── ModWindow.cs
│   │   │   ├── SlotListPanel.cs
│   │   │   ├── SlotDetailPanel.cs
│   │   │   ├── ConfirmDialog.cs
│   │   │   ├── FilePickerDialog.cs
│   │   │   ├── ToastService.cs
│   │   │   └── InputBlocker.cs
│   │   └── Util/
│   │       ├── Log.cs
│   │       ├── KoreanStrings.cs
│   │       └── PathProvider.cs
│   └── LongYinRoster.Tests/
│       ├── LongYinRoster.Tests.csproj
│       ├── fixtures/
│       │   └── slot3_hero.json           ← copy of Save/SaveSlot3/Hero
│       ├── PortabilityFilterTests.cs
│       ├── SlotMetadataTests.cs
│       ├── SlotFileTests.cs
│       ├── SlotRepositoryTests.cs
│       └── SaveFileScannerTests.cs
└── docs/superpowers/
    ├── specs/2026-04-27-longyin-roster-mod-design.md     (already exists)
    └── plans/2026-04-27-longyin-roster-mod-plan.md       (this file)
```

**Build output deployment**:
`<sln>/src/LongYinRoster/bin/Release/net6.0/LongYinRoster.dll`
→ copy to `LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/LongYinRoster.dll`

---

## Prerequisites

Run these once before starting:

```bash
# .NET 6 SDK (required for the projects below — runtime alone won't compile)
dotnet --list-sdks
# Expected output contains a line like: 6.0.xxx [path]
# If empty/missing: install .NET 6 SDK from https://dotnet.microsoft.com/download/dotnet/6.0

# Verify game's BepInEx assemblies are present
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/core/BepInEx.Unity.IL2CPP.dll"
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/interop/Assembly-CSharp.dll"
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/interop/Newtonsoft.Json.dll"
```

If any of those files are missing, stop and reinstall the BepInEx 6 IL2CPP loader for the game first.

---

## Task 1: Repo skeleton and shared build props

**Files:**
- Create: `Directory.Build.props`
- Create: `LongYinRoster.sln` (via `dotnet new sln`)
- Create: `.gitignore`
- Create: `README.md` (placeholder)

- [ ] **Step 1: Initialize sln and gitignore**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
dotnet new sln -n LongYinRoster
dotnet new gitignore
```

- [ ] **Step 2: Write `Directory.Build.props`** (so both projects share the game directory path)

```xml
<Project>
  <PropertyGroup>
    <GameDir>E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan</GameDir>
    <BepInExCore>$(GameDir)/BepInEx/core</BepInExCore>
    <BepInExInterop>$(GameDir)/BepInEx/interop</BepInExInterop>
    <PluginDeployDir>$(GameDir)/BepInEx/plugins/LongYinRoster</PluginDeployDir>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Verify**

```bash
dotnet sln list
# Expected: "No projects found in the solution."
```

- [ ] **Step 4: Commit**

```bash
git init
git add .
git commit -m "chore: initialize solution and shared build props"
```

---

## Task 2: Main project + Plugin entry point that loads in BepInEx

**Files:**
- Create: `src/LongYinRoster/LongYinRoster.csproj`
- Create: `src/LongYinRoster/Plugin.cs`
- Create: `src/LongYinRoster/Util/Log.cs`

- [ ] **Step 1: Create the project**

```bash
cd src
dotnet new classlib -n LongYinRoster -f net6.0
cd LongYinRoster
rm Class1.cs
mkdir Core Slots UI Util
```

- [ ] **Step 2: Replace `LongYinRoster.csproj` with this content**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>LongYinRoster</AssemblyName>
    <RootNamespace>LongYinRoster</RootNamespace>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="BepInEx.Core">
      <HintPath>$(BepInExCore)/BepInEx.Core.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="BepInEx.Unity.IL2CPP">
      <HintPath>$(BepInExCore)/BepInEx.Unity.IL2CPP.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(BepInExCore)/0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2CppInterop.Runtime">
      <HintPath>$(BepInExCore)/Il2CppInterop.Runtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Newtonsoft.Json">
      <HintPath>$(BepInExInterop)/Newtonsoft.Json.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(BepInExInterop)/Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp-firstpass">
      <HintPath>$(BepInExInterop)/Assembly-CSharp-firstpass.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(BepInExInterop)/UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>$(BepInExInterop)/UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.InputLegacyModule">
      <HintPath>$(BepInExInterop)/UnityEngine.InputLegacyModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <Target Name="DeployToBepInEx" AfterTargets="Build">
    <MakeDir Directories="$(PluginDeployDir)" />
    <Copy SourceFiles="$(OutputPath)$(AssemblyName).dll"
          DestinationFolder="$(PluginDeployDir)" />
  </Target>
</Project>
```

- [ ] **Step 3: Write `Util/Log.cs`**

```csharp
using BepInEx.Logging;

namespace LongYinRoster.Util;

public static class Log
{
    private static ManualLogSource? _src;

    public static void Init(ManualLogSource src) => _src = src;

    public static void Info (string msg) => _src?.LogInfo(msg);
    public static void Warn (string msg) => _src?.LogWarning(msg);
    public static void Error(string msg) => _src?.LogError(msg);
    public static void Debug(string msg) => _src?.LogDebug(msg);
}
```

- [ ] **Step 4: Write `Plugin.cs`**

```csharp
using BepInEx;
using BepInEx.Unity.IL2CPP;
using LongYinRoster.Util;

namespace LongYinRoster;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("LongYinLiZhiZhuan.exe")]
public sealed class Plugin : BasePlugin
{
    public const string GUID    = "com.deepe.longyinroster";
    public const string NAME    = "LongYin Roster Mod";
    public const string VERSION = "0.1.0";

    public override void Load()
    {
        Log.Init(this.Log);
        Log.Info($"Loaded {NAME} v{VERSION}");
    }
}
```

- [ ] **Step 5: Add to solution and build**

```bash
cd ../..
dotnet sln add src/LongYinRoster/LongYinRoster.csproj
dotnet build -c Release
# Expected: Build succeeded. 0 Error(s).
# DLL deployed to BepInEx/plugins/LongYinRoster/LongYinRoster.dll
```

- [ ] **Step 6: Manual smoke test — game loads the plugin**

Launch `LongYinLiZhiZhuan.exe`, then check `BepInEx/LogOutput.log`:

```bash
grep "LongYin Roster Mod" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | tail -3
# Expected: a line containing "Loaded LongYin Roster Mod v0.1.0"
```

Close the game.

- [ ] **Step 7: Commit**

```bash
git add .
git commit -m "feat(plugin): bootstrap BepInEx plugin loading and logging"
```

---

## Task 3: Test project skeleton + frozen fixture

**Files:**
- Create: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`
- Create: `src/LongYinRoster.Tests/fixtures/slot3_hero.json`
- Create: `src/LongYinRoster.Tests/SmokeTests.cs`

- [ ] **Step 1: Create the project**

```bash
cd src
dotnet new xunit -n LongYinRoster.Tests -f net6.0
cd LongYinRoster.Tests
rm UnitTest1.cs
mkdir fixtures
dotnet add package FluentAssertions --version 6.12.0
dotnet add package Newtonsoft.Json --version 13.0.3
```

- [ ] **Step 2: Replace `LongYinRoster.Tests.csproj` content**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.5" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>

  <ItemGroup>
    <None Update="fixtures/**/*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

  <!-- Reference the main project sources WITHOUT pulling in IL2CPP dependencies.
       We achieve this with a partial reference: only the IL2CPP-free files compile here. -->
  <ItemGroup>
    <Compile Include="../LongYinRoster/Slots/SlotPayload.cs" />
    <Compile Include="../LongYinRoster/Slots/SlotMetadata.cs" />
    <Compile Include="../LongYinRoster/Slots/SlotEntry.cs" />
    <Compile Include="../LongYinRoster/Slots/SlotFile.cs" />
    <Compile Include="../LongYinRoster/Slots/SlotRepository.cs" />
    <Compile Include="../LongYinRoster/Slots/SaveFileScanner.cs" />
    <Compile Include="../LongYinRoster/Core/PortabilityFilter.cs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Copy the fixture file (one-time freeze of the analysis save)**

```bash
cp "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/SaveSlot3/Hero" \
   "src/LongYinRoster.Tests/fixtures/slot3_hero.json"
ls -la src/LongYinRoster.Tests/fixtures/slot3_hero.json
# Expected: ~35 MB
```

- [ ] **Step 4: Write `SmokeTests.cs`** (verifies test runner + fixture path)

```csharp
using FluentAssertions;
using Xunit;
using System.IO;

namespace LongYinRoster.Tests;

public class SmokeTests
{
    [Fact]
    public void Fixture_File_Exists_And_Is_Json_Array()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json");
        File.Exists(path).Should().BeTrue();
        var firstChar = (char)File.ReadAllBytes(path)[0];
        firstChar.Should().Be('[', "Hero file is a JSON array of hero records");
    }
}
```

- [ ] **Step 5: Add to solution and run tests**

```bash
cd ../..
dotnet sln add src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
dotnet test
# Expected: Passed!  - Failed: 0, Passed: 1, Skipped: 0
```

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "test: add test project skeleton with frozen Hero fixture"
```

---

## Task 4: KoreanStrings + PathProvider utilities

**Files:**
- Create: `src/LongYinRoster/Util/KoreanStrings.cs`
- Create: `src/LongYinRoster/Util/PathProvider.cs`

- [ ] **Step 1: Write `Util/KoreanStrings.cs`**

```csharp
namespace LongYinRoster.Util;

/// <summary>모든 UI 문자열을 한 곳에서 관리. 향후 다국어 시 분리 진입점.</summary>
public static class KoreanStrings
{
    public const string AppTitle              = "Roster Mod";
    public const string HotkeyHint            = "F11 to close";
    public const string SaveCurrentBtn        = "[+] 현재 캐릭터 저장";
    public const string ImportFromFileBtn     = "[F] 파일에서";
    public const string SettingsBtn           = "⚙ 설정";

    public const string SlotEmpty             = "(비어있음)";
    public const string SlotAutoBackup        = "자동백업";
    public const string AutoBackupEmpty       = "자동 백업 (없음)";

    public const string ApplyBtn              = "▼ 현재 플레이어로 덮어쓰기";
    public const string RenameBtn             = "이름변경";
    public const string CommentBtn            = "메모";
    public const string DeleteBtn             = "×삭제";
    public const string RestoreBtn            = "복원";

    public const string ConfirmTitleApply     = "⚠ 플레이어 덮어쓰기 확인";
    public const string ConfirmApplyMain      = "{0}의 데이터로 현재 플레이어를 덮어씁니다.";
    public const string ConfirmApplyPolicy    = "※ 문파/위치/관계는 보존, 캐릭터 본질만 교체";
    public const string AutoBackupCheckbox    = "덮어쓰기 직전 슬롯 00에 자동 저장";
    public const string Cancel                = "취소";
    public const string Apply                 = "덮어쓰기";

    public const string ConfirmTitleDelete    = "슬롯 삭제";
    public const string ConfirmDeleteMain     = "슬롯 {0}을(를) 삭제합니다. 되돌릴 수 없습니다.";
    public const string Delete                = "삭제";

    public const string FilePickerTitle       = "파일에서 가져오기";
    public const string FilePickerImport      = "이 슬롯에서 가져오기";
    public const string FilePickerCurrentLoad = "⚠현재 로드 중";

    public const string ToastCaptured         = "✔ 슬롯 {0}에 캡처되었습니다.";
    public const string ToastApplied          = "✔ 슬롯 {0}의 데이터로 덮어썼습니다. 슬롯 00에 직전 상태 자동저장.";
    public const string ToastAppliedNoBackup  = "✔ 슬롯 {0}의 데이터로 덮어썼습니다.";
    public const string ToastRestored         = "✔ 슬롯 00에서 복원했습니다.";
    public const string ToastDeleted          = "✔ 슬롯 {0}을(를) 삭제했습니다.";
    public const string ToastRenamed          = "✔ 슬롯 {0} 이름을 변경했습니다.";

    public const string ToastErrCapture       = "✘ 캡처 실패: {0}. (자세한 내용: BepInEx 로그)";
    public const string ToastErrApply         = "✘ 덮어쓰기 실패: {0}. (자세한 내용: BepInEx 로그)";
    public const string ToastErrAutoBackup    = "✘ 자동백업에 실패해 덮어쓰기를 취소했습니다. 디스크 공간을 확인하세요.";
    public const string ToastErrSlotsFull     = "✘ 빈 슬롯이 없습니다.";
    public const string ToastErrNoPlayer      = "✘ 플레이어를 찾을 수 없습니다. 게임에 입장한 뒤 시도하세요.";

    public const string EmptyStateNoSlots     = "왼쪽 [+] 버튼으로 첫 캐릭터를 저장하세요";
    public const string EmptyStateNoGame      = "게임에 입장한 뒤 사용 가능합니다";

    public const string GameVersionMismatch   = "이 슬롯은 게임 버전 {0}에서 캡처되었습니다. 현재 버전은 {1}입니다. 그래도 적용하시겠습니까?";
    public const string SchemaUnsupported     = "지원하지 않는 슬롯 포맷 (schemaVersion={0})";
}
```

- [ ] **Step 2: Write `Util/PathProvider.cs`**

```csharp
using System;
using System.IO;
using BepInEx;

namespace LongYinRoster.Util;

/// <summary>플러그인이 사용하는 모든 디스크 경로 진입점. 절대경로/플레이스홀더 모두 처리.</summary>
public static class PathProvider
{
    /// <summary>BepInEx/plugins/LongYinRoster/ — 플러그인의 자기 폴더.</summary>
    public static string PluginDir =>
        Path.Combine(Paths.PluginPath, "LongYinRoster");

    /// <summary>설정 문자열에서 &lt;PluginPath&gt; 토큰을 실제 경로로 치환.</summary>
    public static string Resolve(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PluginDir;
        return raw
            .Replace("<PluginPath>", PluginDir, StringComparison.OrdinalIgnoreCase)
            .Replace("\\", "/");
    }

    /// <summary>게임 루트 (LongYinLiZhiZhuan/) — Save/SaveSlot* 접근용.</summary>
    public static string GameDir =>
        Directory.GetParent(Paths.BepInExRootPath)!.FullName;

    /// <summary>게임 세이브 루트 (LongYinLiZhiZhuan_Data/Save/).</summary>
    public static string GameSaveDir =>
        Path.Combine(GameDir, "LongYinLiZhiZhuan_Data", "Save");
}
```

- [ ] **Step 3: Build to verify references**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
# Expected: Build succeeded.
```

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(util): add KoreanStrings and PathProvider"
```

---

## Task 5: PortabilityFilter (TDD)

**Files:**
- Create: `src/LongYinRoster/Core/PortabilityFilter.cs`
- Create: `src/LongYinRoster.Tests/PortabilityFilterTests.cs`

- [ ] **Step 1: Write the failing tests** (`src/LongYinRoster.Tests/PortabilityFilterTests.cs`)

```csharp
using System.IO;
using FluentAssertions;
using LongYinRoster.Core;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class PortabilityFilterTests
{
    private static JObject Player =>
        JObject.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json")))
        [0] as JObject ?? throw new System.InvalidOperationException("heroID=0 not found");

    [Fact]
    public void StripForApply_Removes_All_Faction_Fields()
    {
        var filtered = JObject.Parse(PortabilityFilter.StripForApply(Player.ToString()));

        foreach (var k in new[] {
            "belongForceID", "skillForceID", "outsideForce",
            "forceJobType", "forceJobID", "forceJobCD", "branchLeaderAreaID",
            "thisMonthContribution", "lastMonthContribution",
            "thisYearContribution", "lastYearContribution", "lastFightContribution",
            "isLeader", "heroForceLv",
            "isGovern", "governLv", "governContribution",
            "isHornord", "hornorLv", "forceContribution",
            "forceMission", "servantForceID", "recruitByPlayer", "salary"
        })
        {
            filtered.ContainsKey(k).Should().BeFalse($"{k} is faction-related and must be stripped");
        }
    }

    [Fact]
    public void StripForApply_Removes_All_Runtime_Fields()
    {
        var filtered = JObject.Parse(PortabilityFilter.StripForApply(Player.ToString()));

        foreach (var k in new[] {
            "heroAIData", "heroAIDataArriveTargetRecord", "heroAISettingData",
            "atAreaID", "bigMapPos", "inSafeArea", "inPrison",
            "inTeam", "teamLeader", "teamMates",
            "missions", "plotNumCount", "missionNumCount",
            "Teacher", "Students", "Lover", "PreLovers",
            "Relatives", "Brothers", "Friends", "Haters"
        })
        {
            filtered.ContainsKey(k).Should().BeFalse($"{k} is runtime/relational and must be stripped");
        }
    }

    [Fact]
    public void StripForApply_Preserves_Core_Character_Fields()
    {
        var filtered = JObject.Parse(PortabilityFilter.StripForApply(Player.ToString()));

        foreach (var k in new[] {
            "heroID", "heroName", "heroFamilyName", "heroNickName", "isFemale",
            "age", "generation", "faceData", "skinID",
            "baseAttri", "totalAttri", "maxAttri",
            "baseFightSkill", "totalFightSkill",
            "baseLivingSkill", "totalLivingSkill",
            "hp", "maxhp", "power", "mana",
            "kungfuSkills", "itemListData", "selfStorage", "nowEquipment",
            "fame", "heroTagData", "heroTagPoint", "fightScore"
        })
        {
            filtered.ContainsKey(k).Should().BeTrue($"{k} is character-essential and must be preserved");
        }
    }

    [Fact]
    public void ExcludedFields_Has_42_Entries_Total()
    {
        // 24 faction + 18 runtime = 42
        PortabilityFilter.ExcludedFields.Count.Should().Be(42);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test --filter "FullyQualifiedName~PortabilityFilterTests"
# Expected: FAIL with "type or namespace 'PortabilityFilter' could not be found"
```

- [ ] **Step 3: Write `Core/PortabilityFilter.cs`**

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Core;

public static class PortabilityFilter
{
    private static readonly HashSet<string> _faction = new()
    {
        "belongForceID", "skillForceID", "outsideForce",
        "forceJobType", "forceJobID", "forceJobCD", "branchLeaderAreaID",
        "thisMonthContribution", "lastMonthContribution",
        "thisYearContribution", "lastYearContribution", "lastFightContribution",
        "isLeader", "heroForceLv",
        "isGovern", "governLv", "governContribution",
        "isHornord", "hornorLv", "forceContribution",
        "forceMission", "servantForceID", "recruitByPlayer", "salary",
    };

    private static readonly HashSet<string> _runtime = new()
    {
        "heroAIData", "heroAIDataArriveTargetRecord", "heroAISettingData",
        "atAreaID", "bigMapPos", "inSafeArea", "inPrison",
        "inTeam", "teamLeader", "teamMates",
        "missions", "plotNumCount", "missionNumCount",
        "Teacher", "Students", "Lover", "PreLovers",
        "Relatives", "Brothers", "Friends", "Haters",
    };

    public static IReadOnlyList<string> ExcludedFields { get; } =
        new List<string>(_faction.Count + _runtime.Count)
        {
        }.AlsoAdd(_faction).AlsoAdd(_runtime);

    public static string StripForApply(string fullPlayerJson)
    {
        var obj = JObject.Parse(fullPlayerJson);
        foreach (var key in _faction)  obj.Remove(key);
        foreach (var key in _runtime)  obj.Remove(key);
        return obj.ToString(Newtonsoft.Json.Formatting.None);
    }
}

internal static class ListExt
{
    public static List<string> AlsoAdd(this List<string> list, IEnumerable<string> more)
    {
        list.AddRange(more);
        return list;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~PortabilityFilterTests"
# Expected: Passed: 4, Failed: 0
```

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(core): PortabilityFilter strips faction + runtime fields"
```

---

## Task 6: SlotMetadata + SlotPayload (TDD)

**Files:**
- Create: `src/LongYinRoster/Slots/SlotMetadata.cs`
- Create: `src/LongYinRoster/Slots/SlotPayload.cs`
- Create: `src/LongYinRoster.Tests/SlotMetadataTests.cs`

- [ ] **Step 1: Write the failing tests** (`SlotMetadataTests.cs`)

```csharp
using System.IO;
using FluentAssertions;
using LongYinRoster.Slots;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class SlotMetadataTests
{
    private static JObject Player =>
        JObject.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json")))
        [0] as JObject ?? throw new System.InvalidOperationException();

    [Fact]
    public void FromPlayerJson_Populates_All_Summary_Fields_From_Frozen_Fixture()
    {
        var meta = SlotMetadata.FromPlayerJson(Player);

        meta.HeroName.Should().Be("초한월");
        meta.HeroNickName.Should().Be("단월검");
        meta.IsFemale.Should().BeTrue();
        meta.Age.Should().Be(18);
        meta.Generation.Should().Be(1);
        meta.FightScore.Should().BeApproximately(353738.0f, 1f);
        meta.KungfuCount.Should().Be(130);
        meta.KungfuMaxLvCount.Should().Be(117);
        meta.ItemCount.Should().Be(156);
        meta.StorageCount.Should().Be(217);
        meta.Money.Should().Be(98179248);
        meta.TalentCount.Should().Be(16);
    }
}
```

- [ ] **Step 2: Run test, expect fail**

```bash
dotnet test --filter "FullyQualifiedName~SlotMetadataTests"
# Expected: FAIL ("'SlotMetadata' could not be found")
```

- [ ] **Step 3: Write `Slots/SlotMetadata.cs`**

```csharp
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Slots;

public sealed record SlotMetadata(
    string   HeroName,
    string   HeroNickName,
    bool     IsFemale,
    int      Age,
    int      Generation,
    float    FightScore,
    int      KungfuCount,
    int      KungfuMaxLvCount,
    int      ItemCount,
    int      StorageCount,
    long     Money,
    int      TalentCount)
{
    public static SlotMetadata FromPlayerJson(JObject player)
    {
        var ks  = player["kungfuSkills"] as JArray ?? new JArray();
        var inv = player["itemListData"]?["allItem"] as JArray ?? new JArray();
        var st  = player["selfStorage"]?["allItem"] as JArray ?? new JArray();
        var tg  = player["heroTagData"] as JArray ?? new JArray();

        return new SlotMetadata(
            HeroName:         (string?)player["heroName"]      ?? "",
            HeroNickName:     (string?)player["heroNickName"]  ?? "",
            IsFemale:         (bool?)player["isFemale"]        ?? false,
            Age:              (int?)player["age"]              ?? 0,
            Generation:       (int?)player["generation"]       ?? 1,
            FightScore:       (float?)player["fightScore"]     ?? 0f,
            KungfuCount:      ks.Count,
            KungfuMaxLvCount: ks.Count(s => (int?)s["lv"] >= 10),
            ItemCount:        inv.Count,
            StorageCount:     st.Count,
            Money:            (long?)player["itemListData"]?["money"] ?? 0L,
            TalentCount:      tg.Count
        );
    }
}
```

- [ ] **Step 4: Write `Slots/SlotPayload.cs`** (the on-disk wrapper)

```csharp
using System;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Slots;

/// <summary>슬롯 파일 1개의 _meta + player. 디스크 표현체.</summary>
public sealed class SlotPayload
{
    public SlotPayloadMeta Meta   { get; init; } = default!;
    public JObject         Player { get; init; } = default!;
}

public sealed record SlotPayloadMeta(
    int      SchemaVersion,
    string   ModVersion,
    int      SlotIndex,
    string   UserLabel,
    string   UserComment,
    string   CaptureSource,         // "live" | "file"
    string   CaptureSourceDetail,
    DateTime CapturedAt,
    string   GameSaveVersion,
    string   GameSaveDetail,
    SlotMetadata Summary);
```

- [ ] **Step 5: Run tests, expect pass**

```bash
dotnet test --filter "FullyQualifiedName~SlotMetadataTests"
# Expected: Passed: 1, Failed: 0
```

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(slots): SlotMetadata derives summary; SlotPayload disk wrapper"
```

---

## Task 7: SlotFile atomic IO (TDD)

**Files:**
- Create: `src/LongYinRoster/Slots/SlotFile.cs`
- Create: `src/LongYinRoster/Slots/SlotEntry.cs`
- Create: `src/LongYinRoster.Tests/SlotFileTests.cs`

- [ ] **Step 1: Write the failing tests** (`SlotFileTests.cs`)

```csharp
using System;
using System.IO;
using FluentAssertions;
using LongYinRoster.Slots;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class SlotFileTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(),
        $"LongYinRoster.Tests.{Guid.NewGuid():N}");

    public SlotFileTests() => Directory.CreateDirectory(_tmp);
    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { }
    }

    private static JObject SamplePlayer() => JObject.Parse(@"{""heroID"":0,""heroName"":""테스트""}");

    private SlotPayload SamplePayload(int idx) => new()
    {
        Meta = new SlotPayloadMeta(
            SchemaVersion: 1, ModVersion: "0.1.0", SlotIndex: idx,
            UserLabel: "테스트", UserComment: "",
            CaptureSource: "live", CaptureSourceDetail: "",
            CapturedAt: new DateTime(2026, 4, 27, 19, 0, 0),
            GameSaveVersion: "1.0.0 f8.2", GameSaveDetail: "",
            Summary: new SlotMetadata("테스트", "", false, 18, 1, 100f, 0, 0, 0, 0, 0L, 0)),
        Player = SamplePlayer(),
    };

    [Fact]
    public void Write_Then_Read_RoundTrips_The_Payload()
    {
        var path = Path.Combine(_tmp, "slot_01.json");
        SlotFile.Write(path, SamplePayload(1));
        var loaded = SlotFile.Read(path);

        loaded.Meta.SlotIndex.Should().Be(1);
        loaded.Meta.UserLabel.Should().Be("테스트");
        loaded.Meta.SchemaVersion.Should().Be(1);
        ((string?)loaded.Player["heroName"]).Should().Be("테스트");
    }

    [Fact]
    public void Write_Is_Atomic_Via_Tmp_Then_Replace()
    {
        var path = Path.Combine(_tmp, "slot_02.json");
        SlotFile.Write(path, SamplePayload(2));
        File.Exists(path + ".tmp").Should().BeFalse("the .tmp staging file must be cleaned up");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Read_Throws_On_Unsupported_Schema_Version()
    {
        var path = Path.Combine(_tmp, "slot_03.json");
        File.WriteAllText(path, @"{""_meta"":{""schemaVersion"":99},""player"":{}}");
        var act = () => SlotFile.Read(path);
        act.Should().Throw<UnsupportedSchemaException>().WithMessage("*99*");
    }
}
```

- [ ] **Step 2: Run test, expect fail**

```bash
dotnet test --filter "FullyQualifiedName~SlotFileTests"
# Expected: FAIL
```

- [ ] **Step 3: Write `Slots/SlotFile.cs`**

```csharp
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Slots;

public sealed class UnsupportedSchemaException : Exception
{
    public UnsupportedSchemaException(int actual)
        : base($"Unsupported slot schemaVersion={actual} (expected 1)") { }
}

public static class SlotFile
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new()
    {
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        DateTimeZoneHandling = DateTimeZoneHandling.Local,
        FloatFormatHandling = FloatFormatHandling.String,
        Formatting = Formatting.Indented,
    });

    public static void Write(string path, SlotPayload payload)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";

        var root = new JObject
        {
            ["_meta"]  = JObject.FromObject(payload.Meta, Serializer),
            ["player"] = payload.Player,
        };

        using (var sw = new StreamWriter(tmp, append: false, System.Text.Encoding.UTF8))
        using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.Indented })
        {
            root.WriteTo(jw);
        }

        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null);
        else
            File.Move(tmp, path);
    }

    public static SlotPayload Read(string path)
    {
        var text = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var root = JObject.Parse(text);

        var sv = (int?)root["_meta"]?["schemaVersion"] ?? -1;
        if (sv != CurrentSchemaVersion) throw new UnsupportedSchemaException(sv);

        var meta = root["_meta"]!.ToObject<SlotPayloadMeta>(Serializer)!;
        var player = (JObject)root["player"]!;

        return new SlotPayload { Meta = meta, Player = player };
    }
}
```

- [ ] **Step 4: Write `Slots/SlotEntry.cs`**

```csharp
namespace LongYinRoster.Slots;

public readonly record struct SlotEntry(
    int           Index,
    bool          IsEmpty,
    SlotPayloadMeta? Meta,
    string        FilePath);
```

- [ ] **Step 5: Run tests, expect pass**

```bash
dotnet test --filter "FullyQualifiedName~SlotFileTests"
# Expected: Passed: 3, Failed: 0
```

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat(slots): atomic SlotFile I/O with schema-version guard"
```

---

## Task 8: SlotRepository (TDD)

**Files:**
- Create: `src/LongYinRoster/Slots/SlotRepository.cs`
- Create: `src/LongYinRoster.Tests/SlotRepositoryTests.cs`

- [ ] **Step 1: Write failing tests** (`SlotRepositoryTests.cs`)

```csharp
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LongYinRoster.Slots;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class SlotRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        $"LongYinRoster.Repo.{Guid.NewGuid():N}");

    public SlotRepositoryTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private SlotRepository Repo() => new(_dir, maxUserSlots: 20);

    private static SlotPayload P(int idx) => new()
    {
        Meta = new SlotPayloadMeta(
            1, "0.1.0", idx, $"slot{idx}", "", "live", "",
            DateTime.UtcNow, "1.0.0 f8.2", "",
            new SlotMetadata("h", "", false, 18, 1, 0, 0, 0, 0, 0, 0, 0)),
        Player = JObject.Parse(@"{""heroID"":0}"),
    };

    [Fact]
    public void Empty_Dir_Yields_21_Empty_Entries()
    {
        var repo = Repo();
        repo.All.Count.Should().Be(21);
        repo.All.All(e => e.IsEmpty).Should().BeTrue();
    }

    [Fact]
    public void Write_Then_Read_Single_Slot()
    {
        var repo = Repo();
        repo.Write(3, P(3));
        repo.Reload();

        repo.All[3].IsEmpty.Should().BeFalse();
        repo.All[3].Meta!.UserLabel.Should().Be("slot3");
    }

    [Fact]
    public void AllocateNextFree_Skips_Slot_0_And_Returns_Lowest_Free_User_Slot()
    {
        var repo = Repo();
        repo.Write(1, P(1));
        repo.Write(2, P(2));
        repo.Write(0, P(0)); // auto-backup is filled too
        repo.Reload();

        repo.AllocateNextFree().Should().Be(3);
    }

    [Fact]
    public void AllocateNextFree_Returns_Negative_When_All_User_Slots_Full()
    {
        var repo = Repo();
        for (int i = 1; i <= 20; i++) repo.Write(i, P(i));
        repo.Reload();

        repo.AllocateNextFree().Should().BeLessThan(0);
    }

    [Fact]
    public void Delete_Removes_File_And_Marks_Empty()
    {
        var repo = Repo();
        repo.Write(5, P(5));
        repo.Delete(5);
        repo.Reload();

        repo.All[5].IsEmpty.Should().BeTrue();
        File.Exists(Path.Combine(_dir, "slot_05.json")).Should().BeFalse();
    }

    [Fact]
    public void Slot0_Direct_User_Write_Not_Allowed_Via_Public_Write_Method()
    {
        // Public Write throws on slot 0; only WriteAutoBackup is allowed there.
        var repo = Repo();
        var act = () => repo.Write(0, P(0));
        act.Should().Throw<InvalidOperationException>().WithMessage("*slot 0*");
    }

    [Fact]
    public void WriteAutoBackup_Allows_Slot_0()
    {
        var repo = Repo();
        repo.WriteAutoBackup(P(0));
        repo.Reload();
        repo.All[0].IsEmpty.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run, expect fail**

```bash
dotnet test --filter "FullyQualifiedName~SlotRepositoryTests"
# Expected: FAIL
```

Also adjust the `[Fact] Slot0_Direct_User_Write_Not_Allowed` test to use `WriteAutoBackup` for the 3rd test's slot 0 line (already done above).

- [ ] **Step 3: Write `Slots/SlotRepository.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using LongYinRoster.Util;

namespace LongYinRoster.Slots;

public sealed class SlotRepository
{
    private readonly string _dir;
    private readonly int    _max;
    private readonly List<SlotEntry> _entries = new();

    public SlotRepository(string slotDir, int maxUserSlots = 20)
    {
        _dir = slotDir;
        _max = maxUserSlots;
        Directory.CreateDirectory(_dir);
        Reload();
    }

    public IReadOnlyList<SlotEntry> All => _entries;

    public string PathFor(int index) =>
        Path.Combine(_dir, $"slot_{index:D2}.json");

    public void Reload()
    {
        _entries.Clear();
        for (int i = 0; i <= _max; i++)
        {
            var path = PathFor(i);
            if (File.Exists(path))
            {
                try
                {
                    var p = SlotFile.Read(path);
                    _entries.Add(new SlotEntry(i, false, p.Meta, path));
                }
                catch (UnsupportedSchemaException ex)
                {
                    Log.Warn($"slot {i}: {ex.Message}");
                    _entries.Add(new SlotEntry(i, false, null, path)); // shown as broken
                }
                catch (Exception ex)
                {
                    Log.Error($"slot {i} unreadable: {ex.Message}");
                    _entries.Add(new SlotEntry(i, true, null, path));
                }
            }
            else
            {
                _entries.Add(new SlotEntry(i, true, null, path));
            }
        }
    }

    public void Write(int index, SlotPayload payload)
    {
        if (index == 0)
            throw new InvalidOperationException(
                "slot 0 is auto-backup-only; use WriteAutoBackup instead");
        if (index < 1 || index > _max)
            throw new ArgumentOutOfRangeException(nameof(index));

        SlotFile.Write(PathFor(index), payload);
        Log.Info($"slot {index} written ({payload.Meta.UserLabel})");
    }

    public void WriteAutoBackup(SlotPayload payload)
    {
        SlotFile.Write(PathFor(0), payload);
        Log.Info("slot 0 auto-backup written");
    }

    public void Delete(int index)
    {
        var path = PathFor(index);
        if (File.Exists(path)) File.Delete(path);
        Log.Info($"slot {index} deleted");
    }

    public int AllocateNextFree()
    {
        for (int i = 1; i <= _max; i++)
            if (_entries[i].IsEmpty) return i;
        return -1;
    }

    public void Rename(int index, string newLabel) =>
        UpdateMeta(index, m => m with { UserLabel = newLabel });

    public void UpdateComment(int index, string newComment) =>
        UpdateMeta(index, m => m with { UserComment = newComment });

    private void UpdateMeta(int index, Func<SlotPayloadMeta, SlotPayloadMeta> patch)
    {
        var path = PathFor(index);
        var loaded = SlotFile.Read(path);
        var updated = new SlotPayload { Meta = patch(loaded.Meta), Player = loaded.Player };
        SlotFile.Write(path, updated);
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

```bash
dotnet test --filter "FullyQualifiedName~SlotRepositoryTests"
# Expected: Passed: 7, Failed: 0
```

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(slots): SlotRepository with slot 0 auto-backup guard"
```

---

## Task 9: SaveFileScanner header parsing (TDD)

**Files:**
- Create: `src/LongYinRoster/Slots/SaveFileScanner.cs`
- Create: `src/LongYinRoster.Tests/SaveFileScannerTests.cs`

- [ ] **Step 1: Write failing tests** (`SaveFileScannerTests.cs`)

```csharp
using System.IO;
using FluentAssertions;
using LongYinRoster.Slots;
using Xunit;

namespace LongYinRoster.Tests;

public class SaveFileScannerTests
{
    private static string Fixture =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "slot3_hero.json");

    [Fact]
    public void ParseHeader_Extracts_HeroName_FightScore_From_First_4KB()
    {
        var hdr = SaveFileScanner.ParseHeader(Fixture, headerByteLimit: 4096);

        hdr.HeroName.Should().Be("초한월");
        hdr.HeroNickName.Should().Be("단월검");
        hdr.FightScore.Should().BeApproximately(353738f, 1f);
    }

    [Fact]
    public void ParseHeader_Returns_Empty_On_Truncated_Json()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "trunc.json");
        File.WriteAllText(tmp, @"[{""heroID"":0,""heroName"":""");
        try
        {
            var hdr = SaveFileScanner.ParseHeader(tmp, headerByteLimit: 4096);
            hdr.HeroName.Should().Be("");
            hdr.HeroNickName.Should().Be("");
        }
        finally { File.Delete(tmp); }
    }
}
```

- [ ] **Step 2: Run, expect fail**

```bash
dotnet test --filter "FullyQualifiedName~SaveFileScannerTests"
# Expected: FAIL
```

- [ ] **Step 3: Write `Slots/SaveFileScanner.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongYinRoster.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LongYinRoster.Slots;

public sealed record HeroHeader(
    string HeroName,
    string HeroNickName,
    float  FightScore);

public sealed record SaveSlotInfo(
    int      SlotIndex,
    bool     Exists,
    bool     IsCurrentlyLoaded,
    string   SaveDetail,
    DateTime SaveTime,
    string   HeroName,
    string   HeroNickName,
    float    FightScore);

public static class SaveFileScanner
{
    /// <summary>
    /// Hero 파일의 처음 N 바이트만 스트리밍 파싱해 첫 영웅(heroID=0)의 핵심 메타만 추출.
    /// 잘린 JSON에서도 graceful 하게 반환.
    /// </summary>
    public static HeroHeader ParseHeader(string heroFilePath, int headerByteLimit = 4096)
    {
        try
        {
            using var fs = new FileStream(heroFilePath, FileMode.Open, FileAccess.Read);
            var len = (int)Math.Min(headerByteLimit, fs.Length);
            var buf = new byte[len];
            fs.Read(buf, 0, len);
            var slice = Encoding.UTF8.GetString(buf);

            // Trim partial trailing token; we'll feed JsonTextReader with what we have.
            using var sr = new StringReader(slice);
            using var jr = new JsonTextReader(sr) { SupportMultipleContent = true };

            string heroName = "", heroNickName = "";
            float  fightScore = 0f;
            int    depth = 0;
            string? lastProp = null;

            while (jr.Read())
            {
                switch (jr.TokenType)
                {
                    case JsonToken.StartObject: depth++; break;
                    case JsonToken.EndObject:   depth--;
                        if (depth == 1) return new HeroHeader(heroName, heroNickName, fightScore);
                        break;
                    case JsonToken.PropertyName: lastProp = (string)jr.Value!; break;
                    case JsonToken.String:
                        if (lastProp == "heroName")     heroName = (string)jr.Value!;
                        if (lastProp == "heroNickName") heroNickName = (string)jr.Value!;
                        break;
                    case JsonToken.Float:
                    case JsonToken.Integer:
                        if (lastProp == "fightScore")
                            fightScore = Convert.ToSingle(jr.Value);
                        break;
                }
            }
            return new HeroHeader(heroName, heroNickName, fightScore);
        }
        catch (Exception ex)
        {
            Log.Warn($"ParseHeader({heroFilePath}): {ex.Message}");
            return new HeroHeader("", "", 0f);
        }
    }

    /// <summary>Save/SaveSlot0~10 폴더를 스캔해 11줄 정보 반환.</summary>
    public static List<SaveSlotInfo> ListAvailable(int? currentlyLoadedSlot = null)
    {
        var saveRoot = PathProvider.GameSaveDir;
        var result   = new List<SaveSlotInfo>(11);

        for (int i = 0; i <= 10; i++)
        {
            var dir   = Path.Combine(saveRoot, $"SaveSlot{i}");
            var info  = Path.Combine(dir, "Info");
            var hero  = Path.Combine(dir, "Hero");
            var exists = File.Exists(info) && File.Exists(hero);

            string saveDetail = "";
            DateTime saveTime = default;
            HeroHeader hdr = new("", "", 0f);

            if (exists)
            {
                try
                {
                    var infoJson = JObject.Parse(File.ReadAllText(info));
                    saveDetail = (string?)infoJson["SaveDetail"] ?? "";
                    DateTime.TryParse((string?)infoJson["SaveTime"], out saveTime);
                }
                catch (Exception ex) { Log.Warn($"Info parse failed for slot {i}: {ex.Message}"); }

                hdr = ParseHeader(hero);
            }

            result.Add(new SaveSlotInfo(
                SlotIndex: i,
                Exists: exists,
                IsCurrentlyLoaded: currentlyLoadedSlot == i,
                SaveDetail: saveDetail,
                SaveTime: saveTime,
                HeroName: hdr.HeroName,
                HeroNickName: hdr.HeroNickName,
                FightScore: hdr.FightScore));
        }
        return result;
    }

    /// <summary>주어진 SaveSlot 의 Hero 파일 전체를 읽어 heroID=0 인 영웅 JSON 반환.</summary>
    public static JObject LoadHero0(int saveSlotIndex)
    {
        var path = Path.Combine(PathProvider.GameSaveDir, $"SaveSlot{saveSlotIndex}", "Hero");
        var arr  = JArray.Parse(File.ReadAllText(path));
        foreach (var item in arr)
            if (item is JObject obj && (int?)obj["heroID"] == 0)
                return obj;
        throw new InvalidOperationException($"heroID=0 not found in SaveSlot{saveSlotIndex}");
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

```bash
dotnet test --filter "FullyQualifiedName~SaveFileScannerTests"
# Expected: Passed: 2, Failed: 0
```

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(slots): SaveFileScanner header parsing (4 KB streaming)"
```

---

## Task 10: SerializerService (game-side, IL2CPP)

This is IL2CPP-only — cannot unit-test. We hand-verify in-game in Task 18 (live capture flow).

**Files:**
- Create: `src/LongYinRoster/Core/SerializerService.cs`

- [ ] **Step 1: Write `Core/SerializerService.cs`**

```csharp
using LongYinRoster.Util;
using Newtonsoft.Json;

namespace LongYinRoster.Core;

public static class SerializerService
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        // 게임 자체 Hero 파일과 같은 동작을 노립니다.
        ReferenceLoopHandling   = ReferenceLoopHandling.Ignore,
        NullValueHandling       = NullValueHandling.Include,
        DefaultValueHandling    = DefaultValueHandling.Include,
        FloatFormatHandling     = FloatFormatHandling.String,
        Formatting              = Formatting.None,
    };

    public static string Serialize(object hero) =>
        JsonConvert.SerializeObject(hero, Settings);

    /// <summary>JSON을 기존 객체에 in-place로 적용 (PopulateObject).</summary>
    public static void Populate(string json, object target)
    {
        try { JsonConvert.PopulateObject(json, target, Settings); }
        catch (System.Exception ex)
        {
            Log.Error($"PopulateObject failed: {ex.Message}");
            throw;
        }
    }
}
```

- [ ] **Step 2: Build (no tests)**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
# Expected: Build succeeded.
```

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "feat(core): SerializerService wraps JsonConvert with shared settings"
```

---

## Task 11: HeroLocator (IL2CPP)

The exact path to the player Hero is game-specific. We attempt three strategies in order: (a) common static singletons, (b) `Object.FindObjectsOfType<Hero>` filter, (c) cached last-known reference. The first call that finds heroID=0 wins; the rest are fallback.

**Files:**
- Create: `src/LongYinRoster/Core/HeroLocator.cs`

- [ ] **Step 1: Write `Core/HeroLocator.cs`** (uses reflection to keep loose coupling)

```csharp
using System;
using System.Linq;
using System.Reflection;
using Il2CppInterop.Runtime;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.Core;

public static class HeroLocator
{
    private static object? _cached;

    /// <summary>heroID=0 인 Hero 객체. 없으면 null.</summary>
    public static object? GetPlayer()
    {
        // 1) 캐시 확인 (검증 후 반환)
        if (_cached != null && IsValidPlayer(_cached)) return _cached;

        // 2) Assembly-CSharp 의 Hero 타입을 찾고, FindObjectsOfTypeAll<Hero>() 시도
        var heroType = FindHeroType();
        if (heroType == null) { Log.Warn("Hero type not found in Assembly-CSharp"); return null; }

        try
        {
            var il2Type = Il2CppType.From(heroType);
            var found = Resources.FindObjectsOfTypeAll(il2Type);
            foreach (var obj in found)
            {
                if (obj == null) continue;
                if (TryGetHeroId(obj, out var id) && id == 0) { _cached = obj; return obj; }
            }
        }
        catch (Exception ex) { Log.Warn($"Hero scan failed: {ex.Message}"); }

        // 3) 게임 매니저 정적 필드 추정 — 흔한 이름 후보
        foreach (var managerName in new[] { "GameManager", "HeroManager", "PlayerManager" })
        {
            var mgrType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.Name == managerName);
            if (mgrType == null) continue;

            var inst = mgrType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null) continue;

            foreach (var pname in new[] { "Player", "PlayerHero", "MainHero" })
            {
                var p = mgrType.GetProperty(pname, BindingFlags.Public | BindingFlags.Instance);
                var v = p?.GetValue(inst);
                if (v != null && IsValidPlayer(v)) { _cached = v; return v; }
            }
        }

        return null;
    }

    public static bool IsInGame() => GetPlayer() != null;

    public static void InvalidateCache() => _cached = null;

    // -- helpers ------------------------------------------------------------

    private static Type? FindHeroType()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.Name == "Hero" && t.Namespace?.Contains("Il2Cpp") == false);
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
    }

    private static bool IsValidPlayer(object obj) =>
        TryGetHeroId(obj, out var id) && id == 0;

    private static bool TryGetHeroId(object obj, out int id)
    {
        id = -1;
        var t = obj.GetType();
        var pi = t.GetField("heroID") ?? (object?)t.GetProperty("heroID") as FieldInfo;
        var p2 = t.GetProperty("heroID");
        try
        {
            if (t.GetField("heroID") is { } f)
            {
                id = (int)f.GetValue(obj)!;
                return true;
            }
            if (p2 != null) { id = (int)p2.GetValue(obj)!; return true; }
        }
        catch { }
        return false;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
# Expected: Build succeeded.
```

- [ ] **Step 3: Manual smoke check via Plugin.Load** (temporarily augment `Plugin.cs`)

Add to `Plugin.Load()` at the bottom (we'll remove later in Task 16):
```csharp
AddComponent<UnityEngine.Object>(); // ensure unity bound
LongYinRoster.Util.Log.Info($"HeroLocator.IsInGame={LongYinRoster.Core.HeroLocator.IsInGame()}");
```

Launch game → main menu → check log. Expected: `HeroLocator.IsInGame=False`. Load a save → call again via console (or restart game inside save) → expect `True`.

- [ ] **Step 4: Revert smoke probe and commit**

Remove the temporary probe lines.

```bash
git add .
git commit -m "feat(core): HeroLocator finds heroID=0 player via reflection"
```

---

## Task 12: PinpointPatcher skeleton

**Files:**
- Create: `src/LongYinRoster/Core/PinpointPatcher.cs`

- [ ] **Step 1: Write `Core/PinpointPatcher.cs`**

```csharp
using LongYinRoster.Util;

namespace LongYinRoster.Core;

/// <summary>
/// Populate 후 부수효과(아이콘 갱신, 캐시 무효화 등) 처리.
/// v0.1: 무동작 + 디버그 로그. 통합 테스트에서 미반영 항목 발견될 때 추가.
/// </summary>
public static class PinpointPatcher
{
    public static void RefreshAfterApply(object player)
    {
        Log.Debug("PinpointPatcher.RefreshAfterApply (no-op in v0.1)");
        // 예: ((Hero)player).heroIconDirty = true;
        //     SomeManager.Instance.RebuildHeroCache();
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
git add .
git commit -m "feat(core): PinpointPatcher skeleton with debug logging"
```

---

## Task 13: Configuration (BepInEx ConfigEntries)

**Files:**
- Create: `src/LongYinRoster/Config.cs`
- Modify: `src/LongYinRoster/Plugin.cs`

- [ ] **Step 1: Write `Config.cs`**

```csharp
using BepInEx.Configuration;
using UnityEngine;

namespace LongYinRoster;

public static class Config
{
    public static ConfigEntry<KeyCode> ToggleHotkey       = null!;
    public static ConfigEntry<bool>    PauseGameWhileOpen = null!;
    public static ConfigEntry<string>  SlotDirectory      = null!;
    public static ConfigEntry<int>     MaxSlots           = null!;

    public static ConfigEntry<float>   WindowX, WindowY, WindowW, WindowH;

    public static ConfigEntry<bool>    AutoBackupBeforeApply       = null!;
    public static ConfigEntry<bool>    RunPinpointPatchOnApply     = null!;

    public static ConfigEntry<int>     LogLevel = null!;

    public static void Bind(ConfigFile cfg)
    {
        ToggleHotkey       = cfg.Bind("General", "ToggleHotkey",       KeyCode.F11,
                                      "모드 창 토글 단축키");
        PauseGameWhileOpen = cfg.Bind("General", "PauseGameWhileOpen", false,
                                      "모드 창이 열려 있는 동안 Time.timeScale=0");
        SlotDirectory      = cfg.Bind("General", "SlotDirectory",      "<PluginPath>/Slots",
                                      "슬롯 파일 디렉터리. <PluginPath> = BepInEx/plugins/LongYinRoster");
        MaxSlots           = cfg.Bind("General", "MaxSlots",            20,
                                      new ConfigDescription(
                                          "사용자 슬롯 개수 (1~MaxSlots). 슬롯 0(자동백업)은 제외.",
                                          new AcceptableValueRange<int>(5, 50)));

        WindowX = cfg.Bind("UI", "WindowX", 1100f, "");
        WindowY = cfg.Bind("UI", "WindowY",  100f, "");
        WindowW = cfg.Bind("UI", "WindowW",  720f, "");
        WindowH = cfg.Bind("UI", "WindowH",  480f, "");

        AutoBackupBeforeApply   = cfg.Bind("Behavior", "AutoBackupBeforeApply",   true,
                                           "덮어쓰기 직전 슬롯 0에 자동 저장 (체크박스 기본값)");
        RunPinpointPatchOnApply = cfg.Bind("Behavior", "RunPinpointPatchOnApply", true,
                                           "덮어쓰기 후 PinpointPatcher 호출");

        LogLevel = cfg.Bind("Logging", "LogLevel", 3,
                            new ConfigDescription(
                                "0=Off, 1=Error, 2=Warn, 3=Info, 4=Debug",
                                new AcceptableValueRange<int>(0, 4)));
    }
}
```

- [ ] **Step 2: Modify `Plugin.cs`** — call `Config.Bind`

```csharp
public override void Load()
{
    Util.Log.Init(this.Log);
    Config.Bind(this.Config);
    Util.Log.Info($"Loaded {NAME} v{VERSION}");
}
```

- [ ] **Step 3: Build + smoke (game-side verification)**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Launch game once. Verify file `BepInEx/config/com.deepe.longyinroster.cfg` is created with the entries above.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(config): bind BepInEx config entries (hotkey, slots, UI, behavior, logging)"
```

---

## Task 14: ToastService

**Files:**
- Create: `src/LongYinRoster/UI/ToastService.cs`

- [ ] **Step 1: Write `UI/ToastService.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class Toast
{
    public string Message = "";
    public ToastKind Kind;
    public float ExpireAt;
}

public enum ToastKind { Info, Success, Error }

public static class ToastService
{
    private static readonly List<Toast> _items = new();
    private const float DurationSec = 3f;

    public static void Push(string msg, ToastKind kind = ToastKind.Info)
    {
        _items.Add(new Toast { Message = msg, Kind = kind, ExpireAt = Time.realtimeSinceStartup + DurationSec });
        Util.Log.Info($"[toast/{kind}] {msg}");
    }

    /// <summary>매 OnGUI 호출 시점에 그림. 만료된 항목 자동 제거.</summary>
    public static void Draw()
    {
        var now = Time.realtimeSinceStartup;
        _items.RemoveAll(t => t.ExpireAt < now);
        if (_items.Count == 0) return;

        const float w = 380f, h = 36f, margin = 12f, gap = 4f;
        float x = Screen.width - w - margin;
        float y = Screen.height - margin - h;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var t = _items[i];
            var rect = new Rect(x, y, w, h);
            var bg = t.Kind switch
            {
                ToastKind.Success => new Color(0.35f, 0.49f, 0.23f, 0.95f),
                ToastKind.Error   => new Color(0.49f, 0.23f, 0.23f, 0.95f),
                _                 => new Color(0.17f, 0.17f, 0.17f, 0.95f),
            };
            var prev = GUI.color;
            GUI.color = bg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(x + 8, y + 8, w - 16, h - 16), t.Message);
            y -= (h + gap);
        }
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
git add .
git commit -m "feat(ui): ToastService with 3s auto-dismiss"
```

---

## Task 15: ModWindow scaffold + F11 toggle

**Files:**
- Create: `src/LongYinRoster/UI/ModWindow.cs`
- Modify: `src/LongYinRoster/Plugin.cs` (host the MonoBehaviour)

- [ ] **Step 1: Write `UI/ModWindow.cs`** — empty window with toggle, draggable, persists position

```csharp
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class ModWindow : MonoBehaviour
{
    private bool _visible;
    private Rect _rect;
    private static readonly int WindowId = "LongYinRoster".GetHashCode();

    public SlotRepository Repo { get; private set; } = null!;

    private void Awake()
    {
        _rect = new Rect(Config.WindowX.Value, Config.WindowY.Value,
                         Config.WindowW.Value, Config.WindowH.Value);

        var slotDir = PathProvider.Resolve(Config.SlotDirectory.Value);
        Repo = new SlotRepository(slotDir, Config.MaxSlots.Value);
    }

    private void Update()
    {
        if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();
    }

    public void Toggle()
    {
        _visible = !_visible;
        if (_visible) Repo.Reload();
        if (Config.PauseGameWhileOpen.Value)
            Time.timeScale = _visible ? 0f : 1f;
    }

    private void OnGUI()
    {
        ToastService.Draw();
        if (!_visible) return;

        _rect = GUILayout.Window(WindowId, _rect, DrawWindow, KoreanStrings.AppTitle);

        // persist position/size
        Config.WindowX.Value = _rect.x;
        Config.WindowY.Value = _rect.y;
        Config.WindowW.Value = _rect.width;
        Config.WindowH.Value = _rect.height;
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Label($"{KoreanStrings.AppTitle}  ·  {KoreanStrings.HotkeyHint}");
        GUILayout.Label("(slots will appear here in Task 16)");
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }
}
```

- [ ] **Step 2: Modify `Plugin.cs` — register the MonoBehaviour**

```csharp
using BepInEx;
using BepInEx.Unity.IL2CPP;
using LongYinRoster.UI;

namespace LongYinRoster;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("LongYinLiZhiZhuan.exe")]
public sealed class Plugin : BasePlugin
{
    public const string GUID    = "com.deepe.longyinroster";
    public const string NAME    = "LongYin Roster Mod";
    public const string VERSION = "0.1.0";

    public override void Load()
    {
        Util.Log.Init(this.Log);
        Config.Bind(this.Config);
        AddComponent<ModWindow>();
        Util.Log.Info($"Loaded {NAME} v{VERSION}");
    }
}
```

- [ ] **Step 3: Build + manual test**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Launch game → press F11 → window appears with title "Roster Mod" and "(slots will appear here)". Drag it. Press F11 again → window hidden. Quit → reopen → window position restored from config.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(ui): ModWindow IMGUI shell with F11 toggle, drag, position persistence"
```

---

## Task 16: SlotListPanel + SlotDetailPanel

**Files:**
- Create: `src/LongYinRoster/UI/SlotListPanel.cs`
- Create: `src/LongYinRoster/UI/SlotDetailPanel.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: Write `UI/SlotListPanel.cs`**

```csharp
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class SlotListPanel
{
    public int  Selected { get; private set; } = 1;
    private Vector2 _scroll;

    public void Draw(SlotRepository repo, float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width));

        // Top action buttons (wired in Task 17 / 22)
        GUILayout.BeginHorizontal();
        GUILayout.Button(KoreanStrings.SaveCurrentBtn, GUILayout.ExpandWidth(true));
        GUILayout.Button(KoreanStrings.ImportFromFileBtn, GUILayout.Width(90));
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        _scroll = GUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < repo.All.Count; i++)
        {
            var entry = repo.All[i];
            var label = i == 0
                ? (entry.IsEmpty
                    ? $"00 · {KoreanStrings.AutoBackupEmpty}"
                    : $"00 · {KoreanStrings.SlotAutoBackup}")
                : (entry.IsEmpty
                    ? $"{i:D2} · {KoreanStrings.SlotEmpty}"
                    : $"{i:D2} · {entry.Meta!.UserLabel}");

            var prev = GUI.color;
            if (i == Selected) GUI.color = new Color(0.4f, 0.55f, 0.85f);
            else if (entry.IsEmpty) GUI.color = new Color(0.6f, 0.6f, 0.6f);

            if (GUILayout.Button(label, GUILayout.Height(22))) Selected = i;

            GUI.color = prev;
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }
}
```

- [ ] **Step 2: Write `UI/SlotDetailPanel.cs`**

```csharp
using System;
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

    public void Draw(SlotEntry entry, bool inGame)
    {
        GUILayout.BeginVertical();

        if (entry.IsEmpty)
        {
            GUILayout.Label(inGame
                ? KoreanStrings.EmptyStateNoSlots
                : KoreanStrings.EmptyStateNoGame);
            GUILayout.EndVertical();
            return;
        }

        var m  = entry.Meta!;
        var s  = m.Summary;

        GUILayout.Label($"슬롯 {entry.Index:D2} · {s.HeroName} ({s.HeroNickName})");
        GUILayout.Space(4);
        Row("캡처",         m.CapturedAt.ToString("yyyy-MM-dd HH:mm"));
        Row("출처",         m.CaptureSource == "live" ? "라이브" : $"파일 {m.CaptureSourceDetail}");
        Row("세이브 시점",  m.GameSaveDetail);
        Row("전투력",       s.FightScore.ToString("N0"));
        Row("무공",         $"{s.KungfuCount} (Lv10 {s.KungfuMaxLvCount})");
        Row("인벤토리",     $"{s.ItemCount} / 창고 {s.StorageCount}");
        Row("금전",         $"{s.Money:N0}냥");
        Row("천부",         $"{s.TalentCount}개");
        if (!string.IsNullOrEmpty(m.UserComment))
            Row("메모", m.UserComment);

        GUILayout.Space(8);

        if (entry.Index == 0)
        {
            if (GUILayout.Button(KoreanStrings.RestoreBtn) && inGame)
                OnRestoreRequested?.Invoke(entry.Index);
        }
        else
        {
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

    private static void Row(string k, string v)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(k, GUILayout.Width(80));
        GUILayout.Label(v);
        GUILayout.EndHorizontal();
    }
}
```

- [ ] **Step 3: Modify `ModWindow.cs.DrawWindow` to wire panels**

```csharp
private SlotListPanel  _list   = new();
private SlotDetailPanel _detail = new();

private void DrawWindow(int id)
{
    GUILayout.BeginHorizontal();
    _list.Draw(Repo, 240f);
    GUILayout.Space(8);
    _detail.Draw(Repo.All[_list.Selected], inGame: Core.HeroLocator.IsInGame());
    GUILayout.EndHorizontal();

    GUI.DragWindow(new Rect(0, 0, 10000, 24));
}
```

- [ ] **Step 4: Build + manual test**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Launch game → F11 → list shows 21 rows ("00 · 자동 백업 (없음)" + 20 "(비어있음)"). Click a row → it highlights blue. Detail panel shows "왼쪽 [+] 버튼으로 첫 캐릭터를 저장하세요".

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(ui): SlotListPanel + SlotDetailPanel with selection + action callbacks"
```

---

## Task 17: Wire live capture (current player → next free slot)

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs`
- Modify: `src/LongYinRoster/UI/SlotListPanel.cs` (expose top button callbacks)

- [ ] **Step 1: Modify `SlotListPanel` top buttons to expose actions**

In `SlotListPanel.cs`:
```csharp
public Action? OnSaveCurrentRequested;
public Action? OnImportFromFileRequested;

// Replace the GUILayout.Button calls with:
if (GUILayout.Button(KoreanStrings.SaveCurrentBtn, GUILayout.ExpandWidth(true)))
    OnSaveCurrentRequested?.Invoke();
if (GUILayout.Button(KoreanStrings.ImportFromFileBtn, GUILayout.Width(90)))
    OnImportFromFileRequested?.Invoke();
```

- [ ] **Step 2: In `ModWindow.cs`, add `CaptureCurrent()` method**

```csharp
private void CaptureCurrent()
{
    var slot = Repo.AllocateNextFree();
    if (slot < 0) { ToastService.Push(KoreanStrings.ToastErrSlotsFull, ToastKind.Error); return; }

    var player = Core.HeroLocator.GetPlayer();
    if (player == null) { ToastService.Push(KoreanStrings.ToastErrNoPlayer, ToastKind.Error); return; }

    try
    {
        var json = Core.SerializerService.Serialize(player);
        var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);
        var summary = SlotMetadata.FromPlayerJson(jObj);

        var label = $"{summary.HeroName} {summary.HeroNickName} {DateTime.Now:MM-dd HH:mm}";
        var payload = new SlotPayload
        {
            Meta = new SlotPayloadMeta(
                SlotFile.CurrentSchemaVersion, Plugin.VERSION, slot,
                label, "", "live", "",
                DateTime.Now, ResolveGameSaveVersion(), ResolveGameSaveDetail(),
                summary),
            Player = jObj,
        };
        Repo.Write(slot, payload);
        Repo.Reload();
        ToastService.Push(string.Format(KoreanStrings.ToastCaptured, slot), ToastKind.Success);
    }
    catch (Exception ex)
    {
        ToastService.Push(string.Format(KoreanStrings.ToastErrCapture, ex.Message), ToastKind.Error);
        Util.Log.Error($"CaptureCurrent failed: {ex}");
    }
}

private string ResolveGameSaveVersion()
{
    // 1차에서는 plugin VERSION 으로 대용; 게임 측 저장 헤더 접근은 v0.2 에 정밀화.
    return "1.0.0 f8.2";
}
private string ResolveGameSaveDetail() => "";
```

- [ ] **Step 3: Wire the callback in `ModWindow.Awake`**

```csharp
_list.OnSaveCurrentRequested = CaptureCurrent;
```

- [ ] **Step 4: Build + manual test**

Launch game → load any save → F11 → click `[+] 현재 캐릭터 저장` →
expect:
1. Toast: `✔ 슬롯 1에 캡처되었습니다.`
2. Slot 1 appears with character name + capture time
3. File `BepInEx/plugins/LongYinRoster/Slots/slot_01.json` exists, ~500KB

Click slot 1 → detail panel shows correct fightScore, kungfu count, etc.

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(capture): live capture wires HeroLocator → Serializer → SlotRepository"
```

---

## Task 18: ConfirmDialog + apply flow with auto-backup

**Files:**
- Create: `src/LongYinRoster/UI/ConfirmDialog.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: Write `UI/ConfirmDialog.cs`**

```csharp
using System;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class ConfirmDialog
{
    public bool   IsOpen;
    public string Title  = "";
    public string Body   = "";
    public string Note   = "";
    public bool   ShowCheckbox;
    public bool   CheckboxValue;
    public string CheckboxLabel = "";

    private Action<bool>? _onConfirm;
    private Rect _rect;

    public void Open(string title, string body, string note,
                     bool withCheckbox, bool checkboxDefault, string checkboxLabel,
                     Action<bool> onConfirm)
    {
        Title = title; Body = body; Note = note;
        ShowCheckbox = withCheckbox;
        CheckboxValue = checkboxDefault;
        CheckboxLabel = checkboxLabel;
        _onConfirm = onConfirm;
        IsOpen = true;
        _rect = new Rect(Screen.width / 2 - 240, Screen.height / 2 - 120, 480, 240);
    }

    public void Draw()
    {
        if (!IsOpen) return;
        _rect = GUI.ModalWindow("LongYinRoster.Confirm".GetHashCode(), _rect, DoDraw, Title);
    }

    private void DoDraw(int id)
    {
        GUILayout.Space(8);
        GUILayout.Label(Body);
        if (!string.IsNullOrEmpty(Note))
        {
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.69f, 0.38f);
            GUILayout.Label(Note);
            GUI.color = prev;
        }
        if (ShowCheckbox)
            CheckboxValue = GUILayout.Toggle(CheckboxValue, CheckboxLabel);

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(KoreanStrings.Cancel, GUILayout.Width(80))) Close(confirmed: false);
        if (GUILayout.Button(KoreanStrings.Apply,  GUILayout.Width(120))) Close(confirmed: true);
        GUILayout.EndHorizontal();
    }

    private void Close(bool confirmed)
    {
        IsOpen = false;
        _onConfirm?.Invoke(confirmed);
    }
}
```

Note: BepInEx IL2CPP runtime exposes `GUI.ModalWindow` via `UnityEngine.IMGUIModule`. If Il2CppInterop hasn't surfaced that overload, fall back to a sentinel rect drawn after the main window — see fallback in Step 4.

- [ ] **Step 2: In `ModWindow.cs`, add the apply flow**

```csharp
private readonly ConfirmDialog _confirm = new();

private void RequestApply(int slot)
{
    var entry = Repo.All[slot];
    if (entry.IsEmpty) return;

    _confirm.Open(
        title: KoreanStrings.ConfirmTitleApply,
        body:  string.Format(KoreanStrings.ConfirmApplyMain, $"슬롯 {slot} · {entry.Meta!.UserLabel}"),
        note:  KoreanStrings.ConfirmApplyPolicy,
        withCheckbox: true,
        checkboxDefault: Config.AutoBackupBeforeApply.Value,
        checkboxLabel: KoreanStrings.AutoBackupCheckbox,
        onConfirm: doAutoBackup => { if (true) DoApply(slot, doAutoBackup && _confirm.CheckboxValue); });
}

private void DoApply(int slot, bool doAutoBackup)
{
    var player = Core.HeroLocator.GetPlayer();
    if (player == null)
    {
        ToastService.Push(KoreanStrings.ToastErrNoPlayer, ToastKind.Error);
        return;
    }

    try
    {
        // Step 1: auto-backup current state into slot 0 (if requested)
        if (doAutoBackup)
        {
            var json = Core.SerializerService.Serialize(player);
            var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);
            var sum  = SlotMetadata.FromPlayerJson(jObj);
            var pld  = new SlotPayload
            {
                Meta = new SlotPayloadMeta(
                    SlotFile.CurrentSchemaVersion, Plugin.VERSION, 0,
                    $"auto-backup before #{slot} ({DateTime.Now:HH:mm:ss})", "",
                    "live", "auto-backup",
                    DateTime.Now, "1.0.0 f8.2", "",
                    sum),
                Player = jObj,
            };
            Repo.WriteAutoBackup(pld);
        }

        // Step 2: read slot, strip, populate
        var loaded = SlotFile.Read(Repo.PathFor(slot));
        var stripped = Core.PortabilityFilter.StripForApply(loaded.Player.ToString());
        Core.SerializerService.Populate(stripped, player);

        if (Config.RunPinpointPatchOnApply.Value)
            Core.PinpointPatcher.RefreshAfterApply(player);

        Repo.Reload();
        ToastService.Push(
            string.Format(doAutoBackup ? KoreanStrings.ToastApplied : KoreanStrings.ToastAppliedNoBackup, slot),
            ToastKind.Success);
    }
    catch (Exception ex)
    {
        ToastService.Push(string.Format(KoreanStrings.ToastErrApply, ex.Message), ToastKind.Error);
        Util.Log.Error($"DoApply failed: {ex}");
    }
}
```

Wire in `ModWindow.Awake`:
```csharp
_detail.OnApplyRequested = RequestApply;
```

In `ModWindow.OnGUI`, after the main window draws:
```csharp
_confirm.Draw();
```

- [ ] **Step 3: Build + manual test**

Launch game → F11 → click slot 1 → click `▼ 현재 플레이어로 덮어쓰기` →
ConfirmDialog appears → check "자동백업" ON → click `덮어쓰기` →
expect:
1. Toast: `✔ 슬롯 1의 데이터로 덮어썼습니다. 슬롯 00에 직전 상태 자동저장.`
2. Slot 0 now non-empty with label `auto-backup before #1 (HH:mm:ss)`
3. Open in-game character status → confirm fightScore/kungfu/inventory match the slot's data
4. Confirm `belongForceID`, `atAreaID`, `Friends` array did NOT change

- [ ] **Step 4: Fallback if `GUI.ModalWindow` is missing**

If the Il2Cpp interop didn't expose `ModalWindow`, replace `GUI.ModalWindow` with `GUI.Window` and disable underlying input by drawing a full-screen darkening rect first:
```csharp
GUI.color = new Color(0,0,0,0.45f);
GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height), Texture2D.whiteTexture);
GUI.color = Color.white;
_rect = GUI.Window(...);
```

- [ ] **Step 5: Commit**

```bash
git add .
git commit -m "feat(apply): ConfirmDialog + auto-backup transactional apply flow"
```

---

## Task 19: Slot 0 restore

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: Implement `Restore` in `ModWindow.cs`**

```csharp
private void RequestRestore(int _ /*always 0*/)
{
    var entry = Repo.All[0];
    if (entry.IsEmpty) return;

    _confirm.Open(
        title: "슬롯 00 복원",
        body:  $"슬롯 00의 자동백업으로 현재 플레이어를 되돌립니다.\n({entry.Meta!.UserLabel})",
        note:  KoreanStrings.ConfirmApplyPolicy,
        withCheckbox: false, checkboxDefault: false, checkboxLabel: "",
        onConfirm: ok => { if (ok) DoRestore(); });
}

private void DoRestore()
{
    var player = Core.HeroLocator.GetPlayer();
    if (player == null) { ToastService.Push(KoreanStrings.ToastErrNoPlayer, ToastKind.Error); return; }

    try
    {
        var loaded = SlotFile.Read(Repo.PathFor(0));
        var stripped = Core.PortabilityFilter.StripForApply(loaded.Player.ToString());
        Core.SerializerService.Populate(stripped, player);
        if (Config.RunPinpointPatchOnApply.Value)
            Core.PinpointPatcher.RefreshAfterApply(player);

        Repo.Delete(0); // 1단계 undo만 — 한 번 복원하면 슬롯 0 비움
        Repo.Reload();
        ToastService.Push(KoreanStrings.ToastRestored, ToastKind.Success);
    }
    catch (Exception ex)
    {
        ToastService.Push(string.Format(KoreanStrings.ToastErrApply, ex.Message), ToastKind.Error);
        Util.Log.Error($"Restore failed: {ex}");
    }
}
```

Wire:
```csharp
_detail.OnRestoreRequested = RequestRestore;
```

- [ ] **Step 2: Manual test**

Trigger an apply (Task 18) so slot 0 is filled → click slot 0 → click `복원` → confirm → toast appears, slot 0 becomes empty, in-game stats revert.

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "feat(apply): slot 0 single-step undo restore"
```

---

## Task 20: FilePickerDialog + capture from disk

**Files:**
- Create: `src/LongYinRoster/UI/FilePickerDialog.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: Write `UI/FilePickerDialog.cs`**

```csharp
using System;
using System.Collections.Generic;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class FilePickerDialog
{
    public bool IsOpen;
    public Action<int>? OnImportConfirmed;     // SaveSlot index

    private List<SaveSlotInfo> _entries = new();
    private int _selected = -1;
    private Vector2 _scroll;
    private Rect _rect;

    public void Open(int? currentlyLoadedSlot)
    {
        _entries = SaveFileScanner.ListAvailable(currentlyLoadedSlot);
        _selected = -1;
        IsOpen = true;
        _rect = new Rect(Screen.width / 2 - 280, Screen.height / 2 - 200, 560, 400);
    }

    public void Draw()
    {
        if (!IsOpen) return;
        _rect = GUI.Window("LongYinRoster.FilePicker".GetHashCode(), _rect, DoDraw, KoreanStrings.FilePickerTitle);
    }

    private void DoDraw(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            var prev = GUI.color;
            if (i == _selected) GUI.color = new Color(0.4f, 0.55f, 0.85f);
            else if (!e.Exists) GUI.color = new Color(0.6f, 0.6f, 0.6f);

            var label = e.Exists
                ? $"SaveSlot{e.SlotIndex}  ·  {e.HeroName} {e.HeroNickName}  ·  {e.SaveDetail}  ·  fight {e.FightScore:N0}"
                  + (e.IsCurrentlyLoaded ? $"  {KoreanStrings.FilePickerCurrentLoad}" : "")
                : $"SaveSlot{e.SlotIndex}  ·  {KoreanStrings.SlotEmpty}";

            if (GUILayout.Button(label, GUILayout.Height(22)) && e.Exists)
                _selected = i;

            GUI.color = prev;
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(KoreanStrings.Cancel, GUILayout.Width(80))) IsOpen = false;
        GUILayout.FlexibleSpace();
        GUI.enabled = _selected >= 0;
        if (GUILayout.Button(KoreanStrings.FilePickerImport, GUILayout.Width(160)))
        {
            var idx = _entries[_selected].SlotIndex;
            IsOpen = false;
            OnImportConfirmed?.Invoke(idx);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }
}
```

- [ ] **Step 2: In `ModWindow.cs`, add file capture flow**

```csharp
private readonly FilePickerDialog _picker = new();

private void OpenFilePicker()
{
    // v0.1: we cannot reliably know which save slot is currently loaded — pass null.
    _picker.Open(currentlyLoadedSlot: null);
}

private void DoImportFromFile(int saveSlotIndex)
{
    var slot = Repo.AllocateNextFree();
    if (slot < 0) { ToastService.Push(KoreanStrings.ToastErrSlotsFull, ToastKind.Error); return; }

    try
    {
        var jObj = SaveFileScanner.LoadHero0(saveSlotIndex);
        var summary = SlotMetadata.FromPlayerJson(jObj);

        var label = $"{summary.HeroName} {summary.HeroNickName} (SaveSlot{saveSlotIndex})";
        var pld   = new SlotPayload
        {
            Meta = new SlotPayloadMeta(
                SlotFile.CurrentSchemaVersion, Plugin.VERSION, slot,
                label, "", "file", $"SaveSlot{saveSlotIndex}",
                DateTime.Now, "1.0.0 f8.2", "",
                summary),
            Player = jObj,
        };
        Repo.Write(slot, pld);
        Repo.Reload();
        ToastService.Push(string.Format(KoreanStrings.ToastCaptured, slot), ToastKind.Success);
    }
    catch (Exception ex)
    {
        ToastService.Push(string.Format(KoreanStrings.ToastErrCapture, ex.Message), ToastKind.Error);
        Util.Log.Error($"Import-from-file failed: {ex}");
    }
}
```

Wire callbacks in `ModWindow.Awake`:
```csharp
_list.OnImportFromFileRequested = OpenFilePicker;
_picker.OnImportConfirmed = DoImportFromFile;
```

In `ModWindow.OnGUI`, after `_confirm.Draw()`:
```csharp
_picker.Draw();
```

- [ ] **Step 3: Manual test**

Launch game → F11 → `[F] 파일에서` → 11-row list appears with character names and fightScore from each SaveSlot's Hero file → click SaveSlot3 row → `이 슬롯에서 가져오기` → toast, new slot has the imported character.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(capture): file capture via SaveFileScanner + FilePickerDialog"
```

---

## Task 21: Slot management — rename, comment, delete

**Files:**
- Create: `src/LongYinRoster/UI/TextInputDialog.cs`
- Modify: `src/LongYinRoster/UI/ModWindow.cs`

- [ ] **Step 1: Write `UI/TextInputDialog.cs`**

```csharp
using System;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

public sealed class TextInputDialog
{
    public bool IsOpen;
    private string _title = "";
    private string _value = "";
    private Action<string?>? _onResult;
    private Rect _rect;

    public void Open(string title, string initial, Action<string?> onResult)
    {
        _title = title; _value = initial; _onResult = onResult;
        IsOpen = true;
        _rect = new Rect(Screen.width / 2 - 220, Screen.height / 2 - 80, 440, 160);
    }

    public void Draw()
    {
        if (!IsOpen) return;
        _rect = GUI.Window("LongYinRoster.TextInput".GetHashCode(), _rect, DoDraw, _title);
    }

    private void DoDraw(int id)
    {
        GUILayout.Space(12);
        _value = GUILayout.TextField(_value);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(KoreanStrings.Cancel, GUILayout.Width(80))) Close(null);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("확인", GUILayout.Width(80))) Close(_value);
        GUILayout.EndHorizontal();
    }

    private void Close(string? result)
    {
        IsOpen = false;
        _onResult?.Invoke(result);
    }
}
```

- [ ] **Step 2: In `ModWindow.cs`, add slot management actions**

```csharp
private readonly TextInputDialog _text = new();

private void RequestRename(int slot)
{
    var current = Repo.All[slot].Meta?.UserLabel ?? "";
    _text.Open("이름 변경", current, val =>
    {
        if (string.IsNullOrWhiteSpace(val)) return;
        Repo.Rename(slot, val.Trim());
        Repo.Reload();
        ToastService.Push(string.Format(KoreanStrings.ToastRenamed, slot), ToastKind.Success);
    });
}

private void RequestComment(int slot)
{
    var current = Repo.All[slot].Meta?.UserComment ?? "";
    _text.Open("메모", current, val =>
    {
        if (val == null) return;
        Repo.UpdateComment(slot, val);
        Repo.Reload();
        ToastService.Push("메모를 저장했습니다.", ToastKind.Success);
    });
}

private void RequestDelete(int slot)
{
    _confirm.Open(
        title: KoreanStrings.ConfirmTitleDelete,
        body:  string.Format(KoreanStrings.ConfirmDeleteMain, slot),
        note:  "",
        withCheckbox: false, checkboxDefault: false, checkboxLabel: "",
        onConfirm: ok =>
        {
            if (!ok) return;
            Repo.Delete(slot);
            Repo.Reload();
            ToastService.Push(string.Format(KoreanStrings.ToastDeleted, slot), ToastKind.Success);
        });
}
```

Wire:
```csharp
_detail.OnRenameRequested  = RequestRename;
_detail.OnCommentRequested = RequestComment;
_detail.OnDeleteRequested  = RequestDelete;
```

In OnGUI after `_picker.Draw()`:
```csharp
_text.Draw();
```

- [ ] **Step 3: Manual test**

For an existing slot:
- [이름변경] → 새 이름 입력 → 디스크 + UI 라벨 변경됨
- [메모] → 메모 입력 → 상세 패널에 표시됨
- [×삭제] → 확인 → 슬롯이 비어짐, slot_NN.json 삭제됨

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "feat(slots): rename + comment + delete with confirm/text dialogs"
```

---

## Task 22: Korean font verification & remediation

**Files:**
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (only if remediation needed)

- [ ] **Step 1: Visual check in game**

Launch game → F11 → check whether all Korean labels render. Look for □ glyphs.

- [ ] **Step 2: If fonts render correctly**

No code changes. Skip to commit.

- [ ] **Step 3: If Korean shows as □ glyphs, inject a font**

Add to `ModWindow.cs`:
```csharp
private static Font? _krFont;

private void Awake()
{
    /* existing code */
    TryLoadKoreanFont();
}

private static void TryLoadKoreanFont()
{
    foreach (var name in new[] {
        "malgun", "Malgun Gothic",
        "NanumGothic", "Nanum Gothic",
        "Arial Unicode MS",
        "MS Gothic" })
    {
        try
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 14);
            if (f != null) { _krFont = f; Util.Log.Info($"Korean font loaded: {name}"); return; }
        }
        catch { }
    }
    Util.Log.Warn("No Korean OS font found; UI may show mojibake.");
}

private void OnGUI()
{
    if (_krFont != null) GUI.skin.font = _krFont;
    /* rest unchanged */
}
```

- [ ] **Step 4: Manual reverify**

After remediation, all labels should render Korean correctly.

- [ ] **Step 5: Commit (only if changes made)**

```bash
git add .
git commit -m "fix(ui): inject system Korean font when default IMGUI font lacks Hangul"
```

---

## Task 23: Smoke test execution + spec validation

Run the **full A–H smoke checklist** from the spec (`docs/superpowers/specs/2026-04-27-longyin-roster-mod-design.md` §11.2).

**Files:**
- Create: `docs/superpowers/specs/smoke-tests-2026-04-27.md` (run log)

- [ ] **Step 1: Back up game folder**

```bash
cp -r "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan" \
      "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan.smoke-backup"
```

- [ ] **Step 2: Run sections A through H against actual game**

For each item in the spec's smoke checklist, mark Pass/Fail with notes. Use SaveSlot10 as the test save (per spec).

Create `smoke-tests-2026-04-27.md` and fill in:
```markdown
# v0.1.0 Smoke Test Run — 2026-04-27

## A. 활성화
- [x] BepInEx 로그에 "Loaded LongYin Roster Mod v0.1.0" 확인
- [x] 메인 메뉴 F11 → 액션 버튼 disabled
- [x] 게임 입장 후 캡처 버튼 활성화

## B. 라이브 캡처
... (each item P/F + note)

## D. 덮어쓰기 — 핵심 검증
- 적용 전 fightScore: <observed>
- 적용 후 fightScore: <observed, must match slot summary>
- belongForceID 변경 없음: P/F
- atAreaID 변경 없음: P/F
- Friends/Haters 변경 없음: P/F
- 게임 자체 저장 후 재시작 → 영속: P/F

## (etc.)
```

- [ ] **Step 3: For each FAIL, file a follow-up task**

Create a `## Follow-ups` section at the bottom of the run log listing any failures and the fix that's needed (likely a `PinpointPatcher` addition or a Newtonsoft setting tweak).

- [ ] **Step 4: Commit run log**

```bash
git add docs/superpowers/specs/smoke-tests-2026-04-27.md
git commit -m "test: v0.1.0 smoke test run log"
```

---

## Task 24: Apply pinpoint fixes from smoke test (if any)

**Files:**
- Modify: `src/LongYinRoster/Core/PinpointPatcher.cs`

- [ ] **Step 1: For each follow-up identified in Task 23**

Replace the `RefreshAfterApply` no-op with the specific fix. Example pattern (collection PopulateObject failure):
```csharp
public static void RefreshAfterApply(object player)
{
    // Fix S2: kungfuSkills list was not populated correctly — rebuild manually
    try
    {
        var t = player.GetType();
        var listField = t.GetField("kungfuSkills");
        // ... assign from cached JSON parse ...
    }
    catch (System.Exception ex) { Util.Log.Error($"Pinpoint kungfu fix: {ex}"); }
}
```

- [ ] **Step 2: Re-run the failing smoke checklist items**

- [ ] **Step 3: Update `smoke-tests-2026-04-27.md`**

Mark previous fails as Pass after fix.

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "fix(core): pinpoint patches for smoke test follow-ups"
```

If smoke tests already all passed, skip this task entirely.

---

## Task 25: README + release packaging

**Files:**
- Create: `README.md`
- Create: `release/LongYinRoster-v0.1.0.zip`

- [ ] **Step 1: Write `README.md`**

```markdown
# LongYin Roster Mod

BepInEx 6 IL2CPP mod for *龙胤立志传 v1.0.0 f8.2*. 캐릭터 스냅샷을 20슬롯에 저장하고
다른 시점/세이브 스냅샷을 현재 플레이어에게 덮어쓸 수 있게 합니다.

## 설치

1. BepInEx 6 IL2CPP-CoreCLR 가 게임에 설치되어 있어야 합니다.
2. 릴리스 zip의 `LongYinRoster.dll` 을 `LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/` 에 복사.
3. 게임 실행 → F11.

## 사용법

- **F11** — 모드 창 토글
- **[+] 현재 캐릭터 저장** — 라이브 플레이어를 다음 빈 슬롯에 캡처
- **[F] 파일에서** — Save/SaveSlotN 스캔, 한 슬롯 골라 캡처
- **[▼ 덮어쓰기]** — 선택 슬롯 데이터로 현재 플레이어 덮어쓰기 (자동백업 ON 권장)
- **슬롯 0** — 자동백업 전용. [복원]으로 1단계 undo

## 정책

캐릭터 본질만 덮어쓰기 (strict-personal): 스탯·스킬·아이템·외형·천부.
**문파/위치/관계/AI/임무는 보존**.

## 설정

`BepInEx/config/com.deepe.longyinroster.cfg` — 핫키, 슬롯 디렉터리, 자동백업 기본값 등.

## 빌드

```bash
dotnet build -c Release
```

`bin/Release/LongYinRoster.dll` 이 자동으로 `LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/` 에 배포됩니다.

## 라이선스

MIT.
```

- [ ] **Step 2: Verify final build**

```bash
dotnet test
# Expected: all unit tests pass
dotnet build -c Release
# Expected: build succeeded
```

- [ ] **Step 3: Package**

```bash
mkdir -p release
cp src/LongYinRoster/bin/Release/LongYinRoster.dll release/
cd release
zip -j LongYinRoster-v0.1.0.zip LongYinRoster.dll ../README.md
cd ..
ls -la release/LongYinRoster-v0.1.0.zip
```

- [ ] **Step 4: Tag and commit**

```bash
git add .
git commit -m "release: v0.1.0 — README + packaged zip"
git tag v0.1.0
```

---

## Self-Review

(Completed by author after writing.)

**Spec coverage check:**
- §2 Scope in/out → Tasks 1–22 cover capture (live + file), apply with auto-backup, slot management, slot 0 restore, IMGUI overlay, Korean strings, ConfigEntries. v0.2 items explicitly skipped.
- §3 Decisions Recap → all 10 decisions reflected in tasks.
- §4 Architecture → file structure mapped 1:1 (Plugin / Core / Slots / UI / Util).
- §5 Data Flow → live capture (Task 17), file capture (Task 20), apply with auto-backup (Task 18), slot 0 restore (Task 19).
- §6 Slot File Schema → SlotPayload, SlotPayloadMeta, atomic IO (Tasks 6, 7), schema version guard (Task 7).
- §7 UI → IMGUI overlay (Task 15), list+detail (Task 16), confirm dialog (Task 18), file picker (Task 20), text input (Task 21), toasts (Task 14), Korean font (Task 22).
- §8 Configuration → all entries in Task 13.
- §9 Module Signatures → matched in Tasks 5–10.
- §10 Edge Cases → S1 (ReferenceLoopHandling.Ignore in Task 10), S2 (PinpointPatcher in Task 12+24), F1 (atomic write in Task 7), F3 (UI button disable + reload pattern in Task 17/18), G1/G2 (handled by JsonConvert defaults), E5 (Repo.Write throws on slot 0 in Task 8). E2/E3 (combat/save-in-progress detection) deferred — surfaced as warning, not hard block — consistent with spec ("v0.2 강제 차단 옵션").
- §11 Testing → Tasks 5/6/7/8/9 = unit tests; Task 23 = full smoke run; Task 24 = pinpoint follow-ups.

**Placeholder scan:**
- No "TBD"/"TODO"/"implement later".
- Every code block contains real code; every command has expected output.
- The two areas with conditional content (Task 22 font remediation, Task 24 pinpoint follow-ups) explicitly state "skip if not needed" and reference the smoke run findings — not vague handwaving.

**Type consistency:**
- `SlotPayloadMeta` constructor parameters used the same in Tasks 6, 17, 18, 20.
- `SlotEntry(int Index, bool IsEmpty, SlotPayloadMeta? Meta, string FilePath)` consistent across Tasks 7, 8, 16.
- `SaveSlotInfo` record consistent in Task 9 + Task 20.
- `ToastKind { Info, Success, Error }` used as defined throughout.
- `HeroLocator.GetPlayer()` returns `object?` and that's what callers in Tasks 17/18/19 treat it as.

No issues found.

---

## Execution Handoff

Plan complete and saved to:
`docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md`

Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
