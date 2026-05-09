# LongYinRoster v0.7.6 Implementation Plan — 설정 panel (Hybrid stateful-only)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** F11 메뉴 + F11+3 진입 신규 SettingsPanel — hotkey 4 + ContainerPanel rect 4 buffer 편집 + [저장] 명시. 자동 영속화 6 항목 (정렬·필터·last container·창 rect) 은 ContainerPanel 사용 중 immediate ConfigEntry write.

**Architecture:** Layered approach — (1) Config 신규 ConfigEntry → (2) HotkeyMap.Bind → (3) SettingsPanel skeleton + buffer logic + tests → (4) SettingsPanel UI (키 캡처 + 충돌 검증) → (5) ContainerPanel hydrate + immediate write → (6) ModWindow / ModeSelector 통합 → (7) Smoke. EventType.KeyDown strip 검증은 **Task 0 spike** 우선.

**Tech Stack:** C# / .NET (BepInEx 6 IL2CPP), Unity IMGUI, BepInEx ConfigEntry 영속화, xUnit + Shouldly 단위 테스트.

**Spec:** [docs/superpowers/specs/2026-05-08-longyin-roster-mod-v0.7.6-design.md](../specs/2026-05-08-longyin-roster-mod-v0.7.6-design.md)
**Roadmap:** [docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md](../specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.3

---

## Task 0: Spike — `EventType.KeyDown` strip 검증

**Files:**
- Edit (temp): `src/LongYinRoster/UI/ModWindow.cs` — temporary KeyDown probe in `OnGUI`

**Goal:** SettingsPanel 의 키 캡처 패턴이 IL2CPP 빌드에서 strip-safe 한지 사전 검증. spec §7.1 Risk 의 fallback 경로 결정.

- [ ] **Step 0.1: ModWindow.OnGUI 에 임시 probe 추가**

  ```csharp
  // TEMP — Task 0 spike, before Task 1
  if (Event.current?.type == EventType.KeyDown)
  {
      Logger.Info($"[Spike] KeyDown captured: {Event.current.keyCode}");
  }
  ```

- [ ] **Step 0.2: 빌드 + 인게임 진입**
  ```pwsh
  DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
  ```
  - 게임 실행 → F11 진입 → 키 입력 (예: `K`, `1`, `F11`, `Esc`) → BepInEx 콘솔 + LogOutput.log 확인.

- [ ] **Step 0.3: 결과 분류**
  - **결과 A (PASS — 기대)**: `[Spike] KeyDown captured: K` 로그 + `Method unstripping failed` 0 건. → spec §1.3 의 키 캡처 패턴 그대로 진행.
  - **결과 B (strip 회귀)**: `Method unstripping failed` 발생 또는 KeyDown 이벤트 미발화. → fallback 1차 (`Input.GetKeyDown` polling) 채택. spec §7.1 의 fallback 1 패턴으로 §3 전체 재작성.
  - **결과 C (부분 strip)**: KeyCode 값 일부만 캡처됨. → fallback 2차 (KeyCode dropdown) 검토.

- [ ] **Step 0.4: temp probe 제거**

  Step 0.1 의 임시 코드 삭제 + commit message: `spike: v0.7.6 EventType.KeyDown strip 검증 — PASS / FAIL`. 결과 dump:
  - Create: `docs/superpowers/dumps/2026-05-08-v0.7.6-keydown-spike.md` (10~30 LOC, A/B/C 결과 + LogOutput grep)

**Decision Gate:** Step 0.3 결과에 따라 Task 3 의 키 캡처 구현 분기. **Task 1~2 는 결과와 무관하게 진행 가능** (Config / HotkeyMap / Buffer 로직).

---

## Task 1: Config + HotkeyMap (Layer 1)

**Files:**
- Edit: `src/LongYinRoster/Config.cs`
- Edit: `src/LongYinRoster/Util/HotkeyMap.cs`
- Create: `src/LongYinRoster.Tests/HotkeyMapTests.cs`

**Goal:** ConfigEntry 10개 신규 추가 + HotkeyMap 의 `Bind()` 메소드 + Numpad 자동 derive + `SettingsModeKey` / `SettingsShortcut` 신규.

### Subtask 1.1: HotkeyMapTests (TDD red)

- [ ] **Step 1.1.1: 신규 test file**

  `src/LongYinRoster.Tests/HotkeyMapTests.cs`:

  ```csharp
  using LongYinRoster.Util;
  using Shouldly;
  using UnityEngine;
  using Xunit;

  namespace LongYinRoster.Tests;

  public class HotkeyMapTests
  {
      [Theory]
      [InlineData(KeyCode.Alpha0, KeyCode.Keypad0)]
      [InlineData(KeyCode.Alpha1, KeyCode.Keypad1)]
      [InlineData(KeyCode.Alpha5, KeyCode.Keypad5)]
      [InlineData(KeyCode.Alpha9, KeyCode.Keypad9)]
      public void NumpadFor_AlphaKeys_ReturnsKeypadEquivalent(KeyCode alpha, KeyCode expected)
      {
          HotkeyMap.NumpadFor(alpha).ShouldBe(expected);
      }

      [Theory]
      [InlineData(KeyCode.F11)]
      [InlineData(KeyCode.A)]
      [InlineData(KeyCode.Escape)]
      [InlineData(KeyCode.None)]
      public void NumpadFor_NonAlphaKeys_ReturnsNone(KeyCode key)
      {
          HotkeyMap.NumpadFor(key).ShouldBe(KeyCode.None);
      }

      [Fact]
      public void Numpad_Pair_DistinctFromAlpha()
      {
          // Sanity — Alpha2 != Keypad2 enum value (UI capture 시 두 값 분리 검증)
          ((int)KeyCode.Alpha2).ShouldNotBe((int)KeyCode.Keypad2);
      }
  }
  ```

  Note: `HotkeyMap.Bind()` 의 ConfigEntry 의존성 검증은 BepInEx ConfigFile 인스턴스 필요 → unit test 에서 instantiate 불가 → 인게임 smoke 만. 따라서 Bind 자체 logic 은 Step 1.3 에 작성하되 unit test 는 NumpadFor 만.

- [ ] **Step 1.1.2: dotnet test → 컴파일 fail (red)**
  ```pwsh
  DOTNET_CLI_UI_LANGUAGE=en dotnet test
  ```
  → `HotkeyMap.NumpadFor` 미존재 fail. 정상.

### Subtask 1.2: Config.cs 신규 ConfigEntry

- [ ] **Step 1.2.1: 10 ConfigEntry 필드 선언 추가** (Config.cs §2.2.1 spec 그대로)

  ```csharp
  // v0.7.6 — Hotkey rebind (3 신규, MainKey 는 기존 ToggleHotkey 재사용)
  public static ConfigEntry<KeyCode> HotkeyCharacterMode = null!;
  public static ConfigEntry<KeyCode> HotkeyContainerMode = null!;
  public static ConfigEntry<KeyCode> HotkeySettingsMode  = null!;

  // v0.7.6 — ContainerPanel rect 영속화
  public static ConfigEntry<float> ContainerPanelX = null!;
  public static ConfigEntry<float> ContainerPanelY = null!;
  public static ConfigEntry<float> ContainerPanelW = null!;
  public static ConfigEntry<float> ContainerPanelH = null!;

  // v0.7.6 — 자동 영속화 (ContainerPanel immediate write)
  public static ConfigEntry<string> ContainerSortKey       = null!;
  public static ConfigEntry<bool>   ContainerSortAscending = null!;
  public static ConfigEntry<string> ContainerFilterCategory = null!;
  public static ConfigEntry<int>    ContainerLastIndex     = null!;
  ```

- [ ] **Step 1.2.2: Bind() 안 신규 cfg.Bind 호출 추가**

  ```csharp
  HotkeyCharacterMode = cfg.Bind("Hotkey", "CharacterMode", KeyCode.Alpha1, "캐릭터 관리 단축키 (F11+이 키)");
  HotkeyContainerMode = cfg.Bind("Hotkey", "ContainerMode", KeyCode.Alpha2, "컨테이너 관리 단축키 (F11+이 키)");
  HotkeySettingsMode  = cfg.Bind("Hotkey", "SettingsMode",  KeyCode.Alpha3, "설정 panel 단축키 (F11+이 키)");

  ContainerPanelX = cfg.Bind("UI", "ContainerPanelX", 150f, "컨테이너 panel X");
  ContainerPanelY = cfg.Bind("UI", "ContainerPanelY", 100f, "컨테이너 panel Y");
  ContainerPanelW = cfg.Bind("UI", "ContainerPanelW", 800f, "컨테이너 panel 폭");
  ContainerPanelH = cfg.Bind("UI", "ContainerPanelH", 760f, "컨테이너 panel 높이");

  ContainerSortKey       = cfg.Bind("Container", "SortKey", "Category", "정렬 키 (Category|Name|Grade|Quality)");
  ContainerSortAscending = cfg.Bind("Container", "SortAscending", true, "정렬 방향 (true=▲ 오름)");
  ContainerFilterCategory = cfg.Bind("Container", "FilterCategory", "All",
      "카테고리 필터 (All|Equipment|Medicine|Food|Book|Treasure|Material|Horse)");
  ContainerLastIndex     = cfg.Bind("Container", "LastIndex", -1,
      new ConfigDescription("마지막 선택 컨테이너 idx (-1=미선택)",
          new AcceptableValueRange<int>(-1, 9999)));
  ```

### Subtask 1.3: HotkeyMap 갱신

- [ ] **Step 1.3.1: 신규 정적 필드 + Bind + Numpad derive**

  ```csharp
  public static KeyCode SettingsModeKey       = KeyCode.Alpha3;
  public static KeyCode SettingsModeKeyNumpad = KeyCode.Keypad3;

  public static void Bind()
  {
      MainKey                = Config.ToggleHotkey.Value;
      CharacterModeKey       = Config.HotkeyCharacterMode.Value;
      ContainerModeKey       = Config.HotkeyContainerMode.Value;
      SettingsModeKey        = Config.HotkeySettingsMode.Value;
      CharacterModeKeyNumpad = NumpadFor(CharacterModeKey);
      ContainerModeKeyNumpad = NumpadFor(ContainerModeKey);
      SettingsModeKeyNumpad  = NumpadFor(SettingsModeKey);
  }

  internal static KeyCode NumpadFor(KeyCode alpha) => alpha switch
  {
      KeyCode.Alpha0 => KeyCode.Keypad0, KeyCode.Alpha1 => KeyCode.Keypad1,
      KeyCode.Alpha2 => KeyCode.Keypad2, KeyCode.Alpha3 => KeyCode.Keypad3,
      KeyCode.Alpha4 => KeyCode.Keypad4, KeyCode.Alpha5 => KeyCode.Keypad5,
      KeyCode.Alpha6 => KeyCode.Keypad6, KeyCode.Alpha7 => KeyCode.Keypad7,
      KeyCode.Alpha8 => KeyCode.Keypad8, KeyCode.Alpha9 => KeyCode.Keypad9,
      _              => KeyCode.None,
  };
  ```

- [ ] **Step 1.3.2: SettingsShortcut() 추가**

  ```csharp
  public static bool SettingsShortcut() =>
      Input.GetKey(MainKey) &&
      (Input.GetKeyDown(SettingsModeKey) ||
       (SettingsModeKeyNumpad != KeyCode.None && Input.GetKeyDown(SettingsModeKeyNumpad)));
  ```

- [ ] **Step 1.3.3: MainKeyPressedAlone 갱신**

  Settings + Numpad 도 검사:

  ```csharp
  public static bool MainKeyPressedAlone()
  {
      if (!Input.GetKeyDown(MainKey)) return false;
      return !(Input.GetKey(CharacterModeKey) || Input.GetKey(ContainerModeKey) || Input.GetKey(SettingsModeKey)
            || (CharacterModeKeyNumpad != KeyCode.None && Input.GetKey(CharacterModeKeyNumpad))
            || (ContainerModeKeyNumpad != KeyCode.None && Input.GetKey(ContainerModeKeyNumpad))
            || (SettingsModeKeyNumpad  != KeyCode.None && Input.GetKey(SettingsModeKeyNumpad)));
  }
  ```

- [ ] **Step 1.3.4: CharacterShortcut/ContainerShortcut 도 Numpad None 가드**

  현재는 hardcoded 라 항상 사용 — Numpad None 가능성 추가:

  ```csharp
  public static bool CharacterShortcut() =>
      Input.GetKey(MainKey) &&
      (Input.GetKeyDown(CharacterModeKey) ||
       (CharacterModeKeyNumpad != KeyCode.None && Input.GetKeyDown(CharacterModeKeyNumpad)));
  // ContainerShortcut 동상
  ```

- [ ] **Step 1.3.5: InternalsVisibleTo 확인**

  `LongYinRoster.csproj` 의 `InternalsVisibleTo("LongYinRoster.Tests")` 가 이미 있음 (v0.7.5 기준). `internal static KeyCode NumpadFor` 가 test 에서 호출 가능 확인.

### Subtask 1.4: dotnet test → green

- [ ] **Step 1.4.1: tests 통과 확인**
  ```pwsh
  DOTNET_CLI_UI_LANGUAGE=en dotnet test
  ```
  → 216 → 219 (+3 NumpadFor cases). Bind 은 BepInEx 의존이라 unit test 안 함.

- [ ] **Step 1.4.2: commit**
  - Message: `feat(config+hotkey): v0.7.6 Layer 1 — ConfigEntry +10 + HotkeyMap.Bind + Numpad derive`

---

## Task 2: SettingsPanel skeleton + Buffer logic + Tests

**Files:**
- Create: `src/LongYinRoster/UI/SettingsPanel.cs`
- Create: `src/LongYinRoster.Tests/SettingsPanelTests.cs`

**Goal:** Buffer struct + Hydrate / DoSave / DoRestoreDefaults / DoResetPersistedView / 충돌 검증 logic — UI 없이. TDD red → green.

### Subtask 2.1: SettingsPanelTests (TDD red)

- [ ] **Step 2.1.1: 신규 test file (8 case spec §2.1.2)**

  ```csharp
  using LongYinRoster.UI;
  using Shouldly;
  using UnityEngine;
  using Xunit;

  namespace LongYinRoster.Tests;

  public class SettingsPanelTests
  {
      [Fact]
      public void Buffer_HydrateFromInitial_DefaultsApplied()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(
              KeyCode.F11, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
              150f, 100f, 800f, 760f);
          p.BufferMain.ShouldBe(KeyCode.F11);
          p.BufferCharacter.ShouldBe(KeyCode.Alpha1);
          p.BufferSettings.ShouldBe(KeyCode.Alpha3);
          p.BufferContainerW.ShouldBe(800f);
      }

      [Fact]
      public void Conflict_DetectsDuplicateHotkey()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(KeyCode.F11, KeyCode.Alpha1, KeyCode.Alpha1, KeyCode.Alpha3, 0,0,0,0);
          p.HasConflict.ShouldBeTrue();
          p.ConflictMessage.ShouldContain("Alpha1");
      }

      [Fact]
      public void Conflict_AllowsUniqueHotkeys()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(KeyCode.F11, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, 0,0,0,0);
          p.HasConflict.ShouldBeFalse();
      }

      [Fact]
      public void RestoreDefaults_BufferOnly()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(KeyCode.K, KeyCode.M, KeyCode.N, KeyCode.O, 999, 888, 777, 666);
          p.DoRestoreDefaults();
          p.BufferMain.ShouldBe(KeyCode.F11);
          p.BufferCharacter.ShouldBe(KeyCode.Alpha1);
          p.BufferContainer.ShouldBe(KeyCode.Alpha2);
          p.BufferSettings.ShouldBe(KeyCode.Alpha3);
          p.BufferContainerX.ShouldBe(150f);
          p.BufferContainerY.ShouldBe(100f);
          p.BufferContainerW.ShouldBe(800f);
          p.BufferContainerH.ShouldBe(760f);
      }

      [Fact]
      public void IsDirty_TrueAfterBufferChange()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(KeyCode.F11, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, 150,100,800,760);
          p.IsDirty.ShouldBeFalse();
          p.SetBufferMain(KeyCode.F12);
          p.IsDirty.ShouldBeTrue();
      }

      [Fact]
      public void IsDirty_FalseAfterRevertingChanges()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(KeyCode.F11, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, 150,100,800,760);
          p.SetBufferMain(KeyCode.F12);
          p.SetBufferMain(KeyCode.F11);
          p.IsDirty.ShouldBeFalse();
      }

      [Fact]
      public void CanSave_FalseWhenConflict()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(KeyCode.F11, KeyCode.Alpha1, KeyCode.Alpha1, KeyCode.Alpha3, 150,100,800,760);
          p.SetBufferMain(KeyCode.F12);  // dirty 상태 강제
          p.CanSave.ShouldBeFalse();
      }

      [Fact]
      public void CanSave_TrueWhenDirtyAndUnique()
      {
          var p = new SettingsPanel();
          p.HydrateFromValues(KeyCode.F11, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, 150,100,800,760);
          p.SetBufferMain(KeyCode.F12);
          p.CanSave.ShouldBeTrue();
      }
  }
  ```

  Note: `HydrateFromValues` / `BufferMain` 등 test-only 접근자는 `internal` (InternalsVisibleTo).

- [ ] **Step 2.1.2: dotnet test → fail (red)**
  → SettingsPanel 미존재 컴파일 에러. 정상.

### Subtask 2.2: SettingsPanel skeleton

- [ ] **Step 2.2.1: SettingsPanel.cs 작성 (UI 없이 logic 만)**

  ```csharp
  using System;
  using LongYinRoster.Util;
  using UnityEngine;

  namespace LongYinRoster.UI;

  public sealed class SettingsPanel
  {
      // 자체 SettingsPanel 자체 default (Config default 와 sync 유지)
      private const KeyCode DefaultMain = KeyCode.F11;
      private const KeyCode DefaultCharacter = KeyCode.Alpha1;
      private const KeyCode DefaultContainer = KeyCode.Alpha2;
      private const KeyCode DefaultSettings = KeyCode.Alpha3;
      private const float DefaultContainerX = 150f, DefaultContainerY = 100f;
      private const float DefaultContainerW = 800f, DefaultContainerH = 760f;

      public bool Visible { get; set; } = false;
      public Rect WindowRect => _rect;
      private Rect _rect = new(200, 120, 480, 600);
      private const int WindowID = 0x4C593733;  // "LY73"

      // Buffer
      internal KeyCode BufferMain      { get; private set; } = DefaultMain;
      internal KeyCode BufferCharacter { get; private set; } = DefaultCharacter;
      internal KeyCode BufferContainer { get; private set; } = DefaultContainer;
      internal KeyCode BufferSettings  { get; private set; } = DefaultSettings;
      internal float   BufferContainerX { get; private set; } = DefaultContainerX;
      internal float   BufferContainerY { get; private set; } = DefaultContainerY;
      internal float   BufferContainerW { get; private set; } = DefaultContainerW;
      internal float   BufferContainerH { get; private set; } = DefaultContainerH;

      // Original (hydrate 시점) — IsDirty 비교용
      private KeyCode _origMain, _origCharacter, _origContainer, _origSettings;
      private float   _origContainerX, _origContainerY, _origContainerW, _origContainerH;
      private bool    _hydrated;

      public Action? OnSaved;

      public bool HasConflict { get; private set; }
      public string ConflictMessage { get; private set; } = "";

      public bool IsDirty =>
          BufferMain != _origMain || BufferCharacter != _origCharacter
          || BufferContainer != _origContainer || BufferSettings != _origSettings
          || BufferContainerX != _origContainerX || BufferContainerY != _origContainerY
          || BufferContainerW != _origContainerW || BufferContainerH != _origContainerH;

      public bool CanSave => IsDirty && !HasConflict;

      public void Hydrate()
      {
          HydrateFromValues(
              Config.ToggleHotkey.Value, Config.HotkeyCharacterMode.Value,
              Config.HotkeyContainerMode.Value, Config.HotkeySettingsMode.Value,
              Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
              Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
      }

      internal void HydrateFromValues(
          KeyCode main, KeyCode ch, KeyCode co, KeyCode se,
          float x, float y, float w, float h)
      {
          BufferMain = _origMain = main;
          BufferCharacter = _origCharacter = ch;
          BufferContainer = _origContainer = co;
          BufferSettings = _origSettings = se;
          BufferContainerX = _origContainerX = x;
          BufferContainerY = _origContainerY = y;
          BufferContainerW = _origContainerW = w;
          BufferContainerH = _origContainerH = h;
          _hydrated = true;
          RecomputeConflict();
      }

      internal void SetBufferMain(KeyCode k)      { BufferMain = k; RecomputeConflict(); }
      internal void SetBufferCharacter(KeyCode k) { BufferCharacter = k; RecomputeConflict(); }
      internal void SetBufferContainer(KeyCode k) { BufferContainer = k; RecomputeConflict(); }
      internal void SetBufferSettings(KeyCode k)  { BufferSettings = k; RecomputeConflict(); }
      internal void SetBufferContainerRect(float x, float y, float w, float h)
      {
          BufferContainerX = x; BufferContainerY = y; BufferContainerW = w; BufferContainerH = h;
      }

      public void DoRestoreDefaults()
      {
          BufferMain = DefaultMain;
          BufferCharacter = DefaultCharacter;
          BufferContainer = DefaultContainer;
          BufferSettings = DefaultSettings;
          BufferContainerX = DefaultContainerX;
          BufferContainerY = DefaultContainerY;
          BufferContainerW = DefaultContainerW;
          BufferContainerH = DefaultContainerH;
          RecomputeConflict();
      }

      public void DoSave()
      {
          if (!CanSave) return;
          Config.ToggleHotkey.Value = BufferMain;
          Config.HotkeyCharacterMode.Value = BufferCharacter;
          Config.HotkeyContainerMode.Value = BufferContainer;
          Config.HotkeySettingsMode.Value = BufferSettings;
          Config.ContainerPanelX.Value = BufferContainerX;
          Config.ContainerPanelY.Value = BufferContainerY;
          Config.ContainerPanelW.Value = BufferContainerW;
          Config.ContainerPanelH.Value = BufferContainerH;
          // 이제 _orig 도 갱신 (다시 dirty 안 보이도록)
          Hydrate();
          OnSaved?.Invoke();
      }

      public void DoResetPersistedView()
      {
          Config.ContainerSortKey.Value = "Category";
          Config.ContainerSortAscending.Value = true;
          Config.ContainerFilterCategory.Value = "All";
          Config.ContainerLastIndex.Value = -1;
          Config.WindowX.Value = 1100f; Config.WindowY.Value = 100f;
          Config.WindowW.Value = 720f;  Config.WindowH.Value = 560f;
          Config.ItemDetailPanelX.Value = 970f; Config.ItemDetailPanelY.Value = 100f;
          Config.ItemDetailPanelWidth.Value = 380f; Config.ItemDetailPanelHeight.Value = 500f;
          Config.ContainerPanelX.Value = DefaultContainerX; Config.ContainerPanelY.Value = DefaultContainerY;
          Config.ContainerPanelW.Value = DefaultContainerW; Config.ContainerPanelH.Value = DefaultContainerH;
          // ContainerPanel rect 도 reset 시 buffer 도 sync (사용자가 panel 닫고 재열 때 immediate 효과)
          BufferContainerX = DefaultContainerX; BufferContainerY = DefaultContainerY;
          BufferContainerW = DefaultContainerW; BufferContainerH = DefaultContainerH;
          _origContainerX = DefaultContainerX; _origContainerY = DefaultContainerY;
          _origContainerW = DefaultContainerW; _origContainerH = DefaultContainerH;
      }

      private void RecomputeConflict()
      {
          // 4 hotkey 중 2개 이상 같은 KeyCode → conflict.
          // KeyCode.None 은 무시 (할당 안 됨 의도).
          var keys = new[] { BufferMain, BufferCharacter, BufferContainer, BufferSettings };
          var labels = new[] { "MainKey", "CharacterMode", "ContainerMode", "SettingsMode" };
          for (int i = 0; i < keys.Length; i++)
          {
              if (keys[i] == KeyCode.None) continue;
              for (int j = i + 1; j < keys.Length; j++)
              {
                  if (keys[i] == keys[j])
                  {
                      HasConflict = true;
                      ConflictMessage = $"⚠ 충돌: {keys[i]} ({labels[i]} / {labels[j]} 동일)";
                      return;
                  }
              }
          }
          HasConflict = false;
          ConflictMessage = "";
      }

      // OnGUI / Draw — Task 3 에서 작성
      public void OnGUI() { /* placeholder */ }
  }
  ```

- [ ] **Step 2.2.2: dotnet test → green**
  → 219 → 227 (+8 SettingsPanelTests). 통과 확인.

- [ ] **Step 2.2.3: commit**
  - Message: `feat(ui): v0.7.6 Layer 2 — SettingsPanel skeleton + buffer/conflict logic + 8 tests`

---

## Task 3: SettingsPanel UI (키 캡처 + 충돌 + 버튼)

**Files:**
- Edit: `src/LongYinRoster/UI/SettingsPanel.cs`

**Goal:** OnGUI / Draw 작성. Spec §3 layout 구현. Task 0 결과에 따라 키 캡처 패턴 결정 (A=`Event.current.KeyDown` 직접, B=`Input.GetKeyDown` polling).

### Subtask 3.1: 키 캡처 (Task 0 결과 A 가정)

- [ ] **Step 3.1.1: CaptureSlot enum + Event.current 처리**

  ```csharp
  private enum CaptureSlot { None, Main, Character, Container, Settings }
  private CaptureSlot _capture = CaptureSlot.None;

  public void OnGUI()
  {
      if (!Visible) return;
      if (!_hydrated) Hydrate();
      try { _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, ""); }
      catch (Exception ex) { Logger.Warn($"SettingsPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}"); }

      if (_capture != CaptureSlot.None && Event.current?.type == EventType.KeyDown)
      {
          var k = Event.current.keyCode;
          if (k == KeyCode.Escape) { _capture = CaptureSlot.None; }
          else if (k != KeyCode.None)
          {
              switch (_capture)
              {
                  case CaptureSlot.Main:      SetBufferMain(k);      break;
                  case CaptureSlot.Character: SetBufferCharacter(k); break;
                  case CaptureSlot.Container: SetBufferContainer(k); break;
                  case CaptureSlot.Settings:  SetBufferSettings(k);  break;
              }
              _capture = CaptureSlot.None;
          }
          Event.current.Use();
      }
  }
  ```

  **Task 0 결과 B (strip 회귀)**: Event.current.KeyDown 분기 → `Update()` (또는 별도 polling tick) 안에서 `Enum.GetValues<KeyCode>()` foreach + `Input.GetKeyDown(k)` 패턴. SettingsPanel 가 MonoBehaviour 아니라 Update 없음 → ModWindow.Update 에서 `_settingsPanel.PollKeyCapture()` 호출 추가.

### Subtask 3.2: Draw 메소드

- [ ] **Step 3.2.1: layout 작성** (spec §3 480×600)

  ```csharp
  private Vector2 _scroll = Vector2.zero;

  private void Draw(int id)
  {
      try
      {
          DialogStyle.FillBackground(_rect.width, _rect.height);
          DialogStyle.DrawHeader(_rect.width, "설정");

          if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
          {
              Visible = false;
              _capture = CaptureSlot.None;
              // X 닫기 = [취소] (buffer 폐기, 다음 Hydrate 가 ConfigEntry 재로드)
              _hydrated = false;
          }

          GUILayout.Space(DialogStyle.HeaderHeight);
          _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 110));

          // 안내 라벨
          GUILayout.Label("⚠ 고급 설정은 BepInExConfigManager (F5) 에서 변경");
          GUILayout.Space(8);

          // 단축키 섹션
          GUILayout.Label("▼ 단축키");
          DrawHotkeyRow("메인 토글:",     CaptureSlot.Main,      BufferMain);
          DrawHotkeyRow("캐릭터 관리:",   CaptureSlot.Character, BufferCharacter);
          DrawHotkeyRow("컨테이너 관리:", CaptureSlot.Container, BufferContainer);
          DrawHotkeyRow("설정 panel:",    CaptureSlot.Settings,  BufferSettings);
          if (HasConflict)
          {
              var prev = GUI.color;
              GUI.color = Color.red;
              GUILayout.Label(ConflictMessage);
              GUI.color = prev;
          }

          GUILayout.Space(8);
          GUILayout.Label("▼ 컨테이너 panel 위치/크기");
          DrawRectFields();

          GUILayout.Space(8);
          GUILayout.Label("▼ 영속화 정보 (자동 저장)");
          DrawPersistedView();

          GUILayout.EndScrollView();

          // 하단 버튼 영역 (scrollview 밖 — 항상 보임)
          GUILayout.BeginHorizontal();
          var prevEnabled = GUI.enabled;
          GUI.enabled = CanSave;
          if (GUILayout.Button("저장", GUILayout.Height(28)))
          {
              DoSave();
              ToastService.Push("✔ 설정 저장됨", ToastKind.Success);
          }
          GUI.enabled = prevEnabled;
          if (GUILayout.Button("기본값 복원", GUILayout.Height(28))) DoRestoreDefaults();
          if (GUILayout.Button("취소", GUILayout.Height(28)))
          {
              Visible = false;
              _capture = CaptureSlot.None;
              _hydrated = false;
          }
          GUILayout.EndHorizontal();

          GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
      }
      catch (Exception ex)
      {
          Logger.Warn($"SettingsPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
      }
  }

  private void DrawHotkeyRow(string label, CaptureSlot slot, KeyCode current)
  {
      GUILayout.BeginHorizontal();
      GUILayout.Label(label, GUILayout.Width(120));
      string display = _capture == slot ? "키 입력 대기..." : current.ToString();
      var prev = GUI.color;
      if (_capture == slot) GUI.color = Color.cyan;
      GUILayout.Label($"[{display}]", GUILayout.Width(180));
      GUI.color = prev;
      if (GUILayout.Button("재설정", GUILayout.Width(80)))
      {
          _capture = slot;
      }
      GUILayout.EndHorizontal();
  }

  private string _xBuf = "", _yBuf = "", _wBuf = "", _hBuf = "";
  private bool _rectBufHydrated;

  private void DrawRectFields()
  {
      if (!_rectBufHydrated)
      {
          _xBuf = BufferContainerX.ToString("F0");
          _yBuf = BufferContainerY.ToString("F0");
          _wBuf = BufferContainerW.ToString("F0");
          _hBuf = BufferContainerH.ToString("F0");
          _rectBufHydrated = true;
      }
      GUILayout.BeginHorizontal();
      GUILayout.Label("X:", GUILayout.Width(20));
      _xBuf = GUILayout.TextField(_xBuf, GUILayout.Width(70));
      GUILayout.Label("Y:", GUILayout.Width(20));
      _yBuf = GUILayout.TextField(_yBuf, GUILayout.Width(70));
      GUILayout.EndHorizontal();
      GUILayout.BeginHorizontal();
      GUILayout.Label("W:", GUILayout.Width(20));
      _wBuf = GUILayout.TextField(_wBuf, GUILayout.Width(70));
      GUILayout.Label("H:", GUILayout.Width(20));
      _hBuf = GUILayout.TextField(_hBuf, GUILayout.Width(70));
      GUILayout.EndHorizontal();

      // textfield → buffer 동기화 (parse 실패 시 무시)
      if (float.TryParse(_xBuf, out var nx)) BufferContainerX = nx;
      if (float.TryParse(_yBuf, out var ny)) BufferContainerY = ny;
      if (float.TryParse(_wBuf, out var nw) && nw > 100) BufferContainerW = nw;
      if (float.TryParse(_hBuf, out var nh) && nh > 100) BufferContainerH = nh;
  }

  private void DrawPersistedView()
  {
      string sortKr = Config.ContainerSortKey.Value switch
      {
          "Category" => "카테고리", "Name" => "이름",
          "Grade" => "등급", "Quality" => "품질", _ => Config.ContainerSortKey.Value
      };
      string arrow = Config.ContainerSortAscending.Value ? "▲" : "▼";
      GUILayout.Label($"정렬: {sortKr} {arrow}");
      GUILayout.Label($"필터: {Config.ContainerFilterCategory.Value}");
      int last = Config.ContainerLastIndex.Value;
      GUILayout.Label($"마지막 컨테이너: {(last > 0 ? "#" + last : "(미선택)")}");
      GUILayout.Label($"Mod 창: ({Config.WindowX.Value:F0}, {Config.WindowY.Value:F0}, {Config.WindowW.Value:F0}×{Config.WindowH.Value:F0})");
      GUILayout.Label($"ItemDetail: ({Config.ItemDetailPanelX.Value:F0}, {Config.ItemDetailPanelY.Value:F0}, {Config.ItemDetailPanelWidth.Value:F0}×{Config.ItemDetailPanelHeight.Value:F0})");
      GUILayout.Label($"ContainerPanel: ({Config.ContainerPanelX.Value:F0}, {Config.ContainerPanelY.Value:F0}, {Config.ContainerPanelW.Value:F0}×{Config.ContainerPanelH.Value:F0})");
      if (GUILayout.Button("영속화 정보 reset", GUILayout.Width(140)))
      {
          DoResetPersistedView();
          ToastService.Push("✔ 영속화 정보 reset 됨", ToastKind.Success);
      }
  }
  ```

- [ ] **Step 3.2.2: 빌드 확인 (compile only — UI 검증은 smoke 단계)**
  ```pwsh
  DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
  ```

- [ ] **Step 3.2.3: commit**
  - Message: `feat(ui): v0.7.6 Layer 3 — SettingsPanel UI (키 캡처 + 충돌 표시 + buttons)`

---

## Task 4: ModWindow / ModeSelector 통합

**Files:**
- Edit: `src/LongYinRoster/UI/ModeSelector.cs`
- Edit: `src/LongYinRoster/UI/ModWindow.cs`

### Subtask 4.1: ModeSelector

- [ ] **Step 4.1.1: Mode enum 확장 + 신규 버튼**

  ```csharp
  public enum Mode { None, Character, Container, Settings }

  // _windowRect 높이 200 → 240
  private Rect _windowRect = new(100, 100, 280, 240);

  // DrawWindow 안 — 컨테이너 버튼 다음
  GUILayout.Space(6);
  if (GUILayout.Button("설정 (F11+3)", GUILayout.Height(32))) SetMode(Mode.Settings);
  ```

### Subtask 4.2: ModWindow 통합

- [ ] **Step 4.2.1: 필드 + Awake wiring**

  ```csharp
  private readonly SettingsPanel _settingsPanel = new();
  private bool _lastSettingsVisible = false;

  // Awake() 안 — Repo wire-up 다음
  HotkeyMap.Bind();   // ★ Config.Bind 끝난 후 (Plugin.cs 호출 순서 확인)
  _settingsPanel.OnSaved = () =>
  {
      HotkeyMap.Bind();
      _containerPanel.SetRect(
          Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
          Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
  };
  ```

- [ ] **Step 4.2.2: Update() — F11+3 + transition handler**

  ```csharp
  if (HotkeyMap.SettingsShortcut()) _modeSelector.SetMode(ModeSelector.Mode.Settings);

  // transition handler — Settings case 추가
  else if (_modeSelector.CurrentMode == ModeSelector.Mode.Settings)
  {
      _settingsPanel.Visible = true;
      _settingsPanel.Hydrate();
      if (_visible) Toggle();
      _containerPanel.Visible = false;
  }

  // X 닫기 sync (기존 _lastVisible / _lastContainerVisible 옆)
  if (_lastSettingsVisible && !_settingsPanel.Visible)
  {
      _modeSelector.SetMode(ModeSelector.Mode.None);
      _lastSeenMode = ModeSelector.Mode.None;
  }
  _lastSettingsVisible = _settingsPanel.Visible;
  ```

- [ ] **Step 4.2.3: OnGUI() — SettingsPanel.OnGUI 호출 + ContainerPanel rect 영속화**

  ```csharp
  // 기존 _itemDetailPanel.OnGUI() 다음
  _settingsPanel.OnGUI();

  // ContainerPanel rect 영속화 (ItemDetailPanel mirror)
  Config.ContainerPanelX.Value = _containerPanel.WindowRect.x;
  Config.ContainerPanelY.Value = _containerPanel.WindowRect.y;
  Config.ContainerPanelW.Value = _containerPanel.WindowRect.width;
  Config.ContainerPanelH.Value = _containerPanel.WindowRect.height;
  ```

- [ ] **Step 4.2.4: ShouldBlockMouse 갱신**

  ```csharp
  if (_instance._settingsPanel.Visible && _instance._settingsPanel.WindowRect.Contains(pos)) return true;
  ```

- [ ] **Step 4.2.5: commit**
  - Message: `feat(ui): v0.7.6 Layer 4a — ModeSelector + ModWindow Settings mode integration`

---

## Task 5: ContainerPanel hydrate + immediate write

**Files:**
- Edit: `src/LongYinRoster/UI/ContainerPanel.cs`
- Edit: `src/LongYinRoster/Containers/ItemCategoryFilter.cs` (해당 enum 매핑 helper 확인 / 추가)

**Goal:** ContainerPanel 의 _filter / _globalState / _selectedContainerIdx / _rect 가 변경 시 ConfigEntry write + 첫 진입 시 hydrate.

- [ ] **Step 5.1: ItemCategoryFilter.Parse / SortKey.Parse helper**

  `src/LongYinRoster/Containers/ItemCategoryFilter.cs` 또는 새 helper 에:

  ```csharp
  public static ItemCategory ParseOrDefault(string s) => s switch
  {
      "All" => ItemCategory.All, "Equipment" => ItemCategory.Equipment,
      "Medicine" => ItemCategory.Medicine, "Food" => ItemCategory.Food,
      "Book" => ItemCategory.Book, "Treasure" => ItemCategory.Treasure,
      "Material" => ItemCategory.Material, "Horse" => ItemCategory.Horse,
      _ => ItemCategory.All,
  };
  ```

  `src/LongYinRoster/Containers/SortKey.cs` 또는 inline:
  ```csharp
  public static SortKey ParseOrDefault(string s) => s switch
  {
      "Category" => SortKey.Category, "Name" => SortKey.Name,
      "Grade" => SortKey.Grade, "Quality" => SortKey.Quality,
      _ => SortKey.Category,
  };
  ```

  - [ ] 확인: `SortKey` enum 이 `LongYinRoster.Containers` 안 어디 정의돼있는지 grep
  - [ ] Parse 실패 시 `Logger.Warn($"ContainerSortKey invalid value: {s}, fallback Category");`

- [ ] **Step 5.2: ContainerPanel.HydrateFromConfig()**

  ```csharp
  public void HydrateFromConfig()
  {
      _filter = ItemCategoryFilter.ParseOrDefault(Config.ContainerFilterCategory.Value);
      _globalState = new SearchSortState("",
          SortKeyHelper.ParseOrDefault(Config.ContainerSortKey.Value),
          Config.ContainerSortAscending.Value);
      var lastIdx = Config.ContainerLastIndex.Value;
      // 삭제된 컨테이너 가리킴 → fallback
      if (lastIdx > 0 && _containerList.Exists(c => c.ContainerIndex == lastIdx))
          _selectedContainerIdx = lastIdx;
      else
          _selectedContainerIdx = (_containerList.Count > 0 ? _containerList[0].ContainerIndex : -1);
      _initialContainerLoadPending = (_selectedContainerIdx > 0);
      // 또한 panel rect 도 hydrate
      _rect = new Rect(Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
                       Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
  }

  public void SetRect(float x, float y, float w, float h)
  {
      _rect = new Rect(x, y, w, h);
  }
  ```

- [ ] **Step 5.3: _filter 변경 immediate write**

  `DrawCategoryTabs` 안:
  ```csharp
  if (GUILayout.Button(ItemCategoryFilter.KoreanLabel(cat), GUILayout.Width(70)))
  {
      _filter = cat;
      Config.ContainerFilterCategory.Value = cat.ToString();
  }
  ```

- [ ] **Step 5.4: _globalState 변경 immediate write**

  `DrawGlobalToolbar` 안:
  ```csharp
  if (!newState.Equals(_globalState))
  {
      _globalState = newState;
      Config.ContainerSortKey.Value = newState.Key.ToString();
      Config.ContainerSortAscending.Value = newState.Ascending;
      _invView.Invalidate(); _stoView.Invalidate(); _conView.Invalidate();
  }
  ```

- [ ] **Step 5.5: _selectedContainerIdx 변경 immediate write**

  Dropdown 클릭 분기 안:
  ```csharp
  _selectedContainerIdx = m.ContainerIndex;
  Config.ContainerLastIndex.Value = m.ContainerIndex;
  ```

  컨테이너 삭제 분기 안:
  ```csharp
  _selectedContainerIdx = -1;
  Config.ContainerLastIndex.Value = -1;
  ```

  컨테이너 신규 생성 분기 안:
  ```csharp
  _selectedContainerIdx = idx;
  Config.ContainerLastIndex.Value = idx;
  ```

- [ ] **Step 5.6: Plugin.cs Awake 흐름 정정**

  순서:
  1. `Config.Bind(Config)` (BepInPlugin 의 ConfigFile 인스턴스)
  2. `HotkeyMap.Bind()` (ConfigEntry 정적 필드 sync)
  3. ModWindow MonoBehaviour 추가
  4. ModWindow.Awake 안에서 SlotRepository / ContainerRepository wire-up
  5. ContainerRepository wire 후 `_containerPanel.HydrateFromConfig()` 호출 (containerList 가 채워진 후)
  6. SettingsPanel.OnSaved wire

- [ ] **Step 5.7: dotnet test → green**
  → 227 ConfigEntry write 는 BepInEx 의존이라 unit test 안 함. 컴파일 통과 확인.

- [ ] **Step 5.8: commit**
  - Message: `feat(ui): v0.7.6 Layer 4b — ContainerPanel hydrate + immediate ConfigEntry write`

---

## Task 6: 인게임 Smoke (28 시나리오)

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-v0.7.6-smoke-results.md`

**Goal:** Spec §6 의 12 신규 + 16 회귀 = 28 시나리오 인게임 검증.

### Subtask 6.1: 빌드 + 게임 실행

- [ ] **Step 6.1.1: 게임 닫기 + 빌드**
  ```pwsh
  tasklist | grep -i LongYinLiZhiZhuan
  DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
  ```

- [ ] **Step 6.1.2: 로그 클리어**
  ```pwsh
  Clear-Content "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
  ```

- [ ] **Step 6.1.3: 게임 실행 + 슬롯 진입**

### Subtask 6.2: 신규 시나리오 (12)

- [ ] **S1**: F11 → ModeSelector 메뉴 280×240. "설정 (F11+3)" 버튼 보임. **PASS 기대**.
- [ ] **S2**: F11+3 → SettingsPanel 480×600 진입. hydrate 됨 (현재 ConfigEntry 값 표시).
- [ ] **S3**: hotkey 재설정 클릭 → row 라벨 "키 입력 대기..." cyan + 키 입력 (예: K) → row 라벨 "K" 갱신.
- [ ] **S4**: 두 row 같은 키 (예: ContainerMode = Alpha1, CharacterMode 도 Alpha1) → 충돌 경고 라벨 빨강 + [저장] 버튼 disabled.
- [ ] **S5**: 캡처 대기 중 ESC → row 라벨 이전 값 유지.
- [ ] **S6**: 변경 후 [저장] → 토스트 "✔ 설정 저장됨" → SettingsPanel 닫음 (또는 유지) → F11+신규키 작동.
- [ ] **S7**: [기본값 복원] → buffer 만 reset (라벨이 default 로 복귀). [취소] 후 다시 진입 → 이전 ConfigEntry 값으로 hydrate (즉 reset 효과 안 남음).
- [ ] **S8**: [취소] 또는 X → buffer 폐기. 다음 진입 시 ConfigEntry 값 hydrate.
- [ ] **S9**: ContainerPanel 검색 box 입력 / 정렬 변경 / 카테고리 탭 / dropdown 선택 → SettingsPanel 재진입 → 영속화 정보 섹션에 갱신된 값 표시.
- [ ] **S10**: [영속화 정보 reset] → 정렬 = "카테고리 ▲" / 필터 = "All" / 마지막 컨테이너 = "(미선택)" / 창 rect 들 default 로.
- [ ] **S11**: ContainerPanel 드래그 → ConfigEntry 갱신 → 게임 재시작 → 같은 위치.
- [ ] **S12**: SettingsPanel 에서 CharacterMode = Alpha7 저장 → F11+Keypad7 도 캐릭터 모드 진입 (Numpad derive).

### Subtask 6.3: 회귀 시나리오 (16)

- [ ] **R1~R11**: v0.7.5.2 smoke 11/11 (cell label 가로 직사각형, 한글 라벨, 검색 한글/한자, 등급 색상 row, 정렬 4 키, ItemDetail header, curated 7 카테고리, raw fields, ⓘ 토글 active state, focus outline, X 닫기 sync)
- [ ] **R12**: F11+1 / F11+2 hotkey 변경 후에도 정상 작동
- [ ] **R13**: 검색·정렬 영속화 — 게임 재시작 → 이전 정렬 상태 유지
- [ ] **R14**: 마지막 컨테이너 idx 영속화 — 게임 재시작 → dropdown 자동 선택
- [ ] **R15**: 카테고리 탭 영속화 — 게임 재시작 → 이전 탭 유지
- [ ] **R16**: 삭제된 컨테이너 ConfigEntry 값 → fallback 첫 컨테이너로 복원

### Subtask 6.4: Strip 검증

- [ ] **Step 6.4.1: BepInEx 로그 grep**
  ```pwsh
  Select-String -Path "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" -Pattern "Method unstripping failed"
  ```
  → 0 건 기대.

- [ ] **Step 6.4.2: GUI exception 로그**
  ```pwsh
  Select-String -Path "...LogOutput.log" -Pattern "SettingsPanel\.(OnGUI|Draw) threw"
  ```
  → 0 건 기대 (try/catch 가 catch 하긴 하지만 발생 자체 X 가 정상).

### Subtask 6.5: smoke dump 작성

- [ ] **Step 6.5.1**: `docs/superpowers/dumps/2026-05-XX-v0.7.6-smoke-results.md`
  - 28 시나리오 결과 (PASS/FAIL/SKIP)
  - iteration fix narrative (bug 발견 → 수정 → 재시도)
  - strip 검증 결과
  - 자산 (검증된 IMGUI 패턴 추가 / 폐기 패턴)

- [ ] **Step 6.5.2: commit**
  - Message: `docs: v0.7.6 인게임 smoke 결과 — 28/28 PASS`

---

## Task 7: Release prep

**Files:**
- Edit: `VERSION`
- Edit: `README.md`
- Edit: `docs/HANDOFF.md`
- Edit: `docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md` (메타 §2.3 Result append)

- [ ] **Step 7.1: VERSION bump**
  - `0.7.5.2` → `0.7.6`

- [ ] **Step 7.2: README.md**
  - "최신 버전 v0.7.6" 갱신
  - 신규 기능 한 줄: "F11+3 설정 panel — hotkey rebind / 컨테이너 panel 위치·크기 / 검색·정렬·필터 자동 영속"

- [ ] **Step 7.3: HANDOFF.md**
  - Releases 섹션에 v0.7.6 entry 추가
  - "현재 main baseline" → v0.7.6
  - "다음 sub-project" → G1 결정 게이트 진입 명시
  - 핵심 자산 (HotkeyMap.Bind / SettingsPanel buffer 패턴 / 영속화 매트릭스) 1-pager

- [ ] **Step 7.4: 메타 spec §2.3 Result append**
  - `**Result** (2026-05-XX):` 섹션 추가 (v0.7.5 / v0.7.4.1 패턴)
  - Spec / Plan / Smoke / Release link

- [ ] **Step 7.5: dist zip + GitHub release**
  ```pwsh
  Compress-Archive -Path "dist/LongYinRoster_v0.7.6/*" -DestinationPath "dist/LongYinRoster_v0.7.6.zip" -Force
  gh release create v0.7.6 dist/LongYinRoster_v0.7.6.zip --title "..." --notes-file release-notes.md
  ```

- [ ] **Step 7.6: G1 결정 게이트 진입 안내**
  - HANDOFF + 메타 spec 에 G1 트리거 명시 — "v0.7.6 release 직후 v0.7.7 / v0.8 / maintenance 결정 게이트 평가 필요"

- [ ] **Step 7.7: commit + tag**
  - Message: `chore(release): v0.7.6 — 설정 panel (Hybrid stateful-only)`
  - `git tag v0.7.6`

---

## 예상 commit 시퀀스

| commit | 내용 | tests |
|---|---|---|
| 1 | `spike: v0.7.6 EventType.KeyDown 검증` | 216 |
| 2 | `feat(config+hotkey): v0.7.6 Layer 1` | 219 (+3) |
| 3 | `feat(ui): v0.7.6 Layer 2 SettingsPanel skeleton + 8 tests` | 227 (+8) |
| 4 | `feat(ui): v0.7.6 Layer 3 SettingsPanel UI` | 227 |
| 5 | `feat(ui): v0.7.6 Layer 4a ModWindow integration` | 227 |
| 6 | `feat(ui): v0.7.6 Layer 4b ContainerPanel hydrate` | 227 |
| 7 | `docs: v0.7.6 인게임 smoke 결과` | 227 |
| 8 | `chore(release): v0.7.6` | 227 |

총 8 commits + 1 tag.

## 위험 / 변동 요인

- **Task 0 결과 B/C**: Task 3 키 캡처 로직 재작성 + commit 1~2개 추가 가능
- **Smoke 회귀 발견**: iteration fix commit 추가 (v0.7.5.2 가 3 iteration 으로 끝남. v0.7.6 은 통합 작업 많아서 2~4 iteration 가능성)
- **ConfigManager F5 미설치 환경 user**: spec 의 안내 라벨로 충분 가정. 실제 사용자 보고 시 hotfix.

## 참고 자산 / dumps

- `dumps/2026-05-05-bepinexconfigmanager-analysis.md` — sinai mod scope
- `dumps/2026-05-05-v075-cheat-feature-reference.md` §7 / §11 — IMGUI 윈도우 + CheatProfiles 영속화 패턴
- `dumps/2026-05-03-v0.7.4-smoke-results.md` — IL2CPP IMGUI strip-safe patterns (Event.current.MouseDown 검증)
- 본 spec: [`2026-05-08-longyin-roster-mod-v0.7.6-design.md`](../specs/2026-05-08-longyin-roster-mod-v0.7.6-design.md)
