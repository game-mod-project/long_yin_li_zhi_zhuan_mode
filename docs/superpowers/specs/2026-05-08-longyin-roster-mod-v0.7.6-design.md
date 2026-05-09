# LongYinRoster v0.7.6 — 설정 panel (Hybrid stateful-only)

**일시**: 2026-05-08
**baseline**: v0.7.5.2 — 216/216 tests + 인게임 smoke 11/11 PASS
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.3 v0.7.6 1-pager
**brainstorm 결과 (2026-05-08)**:
- Q1 = **B** (Hybrid stateful-only) — 자체 panel 은 stateful runtime state 만 노출. BepInEx `ConfigEntry` 16개는 sinai BepInExConfigManager F5 위임 (자체 panel mirror 안 함).
- Q2 = (default mix) — 검색 textbox 만 세션 휘발, 나머지 5개 (정렬 key/방향 / 카테고리 탭 / 마지막 컨테이너 idx / ContainerPanel rect) 영속화.
- Q3-1 = **B** — hotkey rebind 범위 = MainKey + CharacterModeKey + ContainerModeKey + (신규) SettingsModeKey 4개 ConfigEntry. Numpad 변형 2개는 사용자 hotkey 의 Numpad 대응을 자동 derive (예: Alpha1 ↔ Keypad1 매핑 테이블).
- Q3-2 = **A** — 컨테이너 정원 = 기존 InventoryMaxWeight (964) / StorageMaxWeight (300) 만. 외부 디스크 컨테이너 무제한 유지 (mod 정체성).
- 자유 입력: ① F11 메뉴 (ModeSelector) 에 "설정" 항목 추가, ② "저장" 버튼 명시 (immediate 반영 X), ③ "기본값 복원" 버튼 제공.

## 0. 한 줄 요약

신규 `SettingsPanel` IMGUI window (ModeSelector 메뉴 + F11+3 진입). 사용자 편집 = hotkey 4개 + ContainerPanel rect 4개 + 영속화 정보 read-only 표시. [저장] / [기본값 복원] 버튼. 검색·정렬·카테고리 필터·마지막 컨테이너·창 rect 는 ContainerPanel 사용 중 ConfigEntry 자동 write 로 영속.

## 1. 디자인 결정 (Section 0 brainstorm 의 전개)

### 1.1 Hybrid 의 분할 경계 (Q1=B)

| 항목 | 위치 | 사용자 편집 경로 |
|---|---|---|
| ToggleHotkey (F11) / Character/Container/Settings hotkey | `Config.cs` ConfigEntry | **자체 SettingsPanel** (rebind UX 핵심) |
| ContainerPanel rect | `Config.cs` 신규 ConfigEntry | **자체 SettingsPanel** (창 위치 reset UX) |
| MaxSlots / Inventory/StorageMaxWeight / SlotDirectory / LogLevel / AutoBackup / AllowApply / PauseGameWhileOpen | `Config.cs` 기존 ConfigEntry | **ConfigManager F5** (편집 빈도 낮음, GUI mirror 작업 가성비 낮음) |
| Mod window WindowX/Y/W/H / ItemDetailPanel rect | `Config.cs` 기존 ConfigEntry | **자체 SettingsPanel** read-only 표시 + [reset] (편집 = 드래그) |

ConfigManager 통팩 동봉 검증 완료 (`dumps/2026-05-05-bepinexconfigmanager-analysis.md` MD5 일치). 사용자 mod 가 이를 의존성으로 가정해도 비교적 안전. 단 **ConfigManager 미설치 환경**에서 자체 SettingsPanel 헤더에 안내 라벨 1줄 — "고급 설정 = BepInExConfigManager 필요 (F5)".

### 1.2 영속화 storage 선택 (Q2)

`Config.cs` 의 BepInEx ConfigEntry 추가 — `ItemDetailPanel*` 영속화 (v0.7.4 D-1) 와 동일 패턴. 별도 `settings.json` 안 만듦 (storage 단일화).

| stateful 항목 | ConfigEntry 신규 | 변경 시점 (immediate write) |
|---|---|---|
| 검색 textbox 내용 | (없음) | (휘발) |
| 정렬 key (`SortKey` enum) | `Container.SortKey` (string) | `ContainerPanel.DrawGlobalToolbar` 의 `_globalState = newState` |
| 정렬 방향 ▲/▼ | `Container.SortAscending` (bool) | 동상 |
| 카테고리 탭 (`ItemCategory`) | `Container.FilterCategory` (string) | `ContainerPanel.DrawCategoryTabs` 의 `_filter = cat` |
| 마지막 선택 컨테이너 idx | `Container.LastIndex` (int, default -1) | dropdown 클릭 시 (`_selectedContainerIdx = m.ContainerIndex`) |
| ContainerPanel 창 위치/크기 | `UI.ContainerPanelX/Y/W/H` (float) | `ModWindow.OnGUI` postframe — ItemDetailPanel mirror |

복원 = `Plugin.cs` 의 ConfigEntry bind 직후 + ContainerPanel.SetRepository 시점에 lazy hydrate. invalid 값 (예: LastIndex 가 삭제된 컨테이너 가리킴) 은 try/catch + fallback to default.

### 1.3 Hotkey rebind UX (Q3-1=B)

**ConfigEntry 추가 (4 신규)**:
```csharp
Hotkey.MainKey         (KeyCode, default F11)     // ToggleHotkey 와 별개? — 통합 (1.4)
Hotkey.CharacterMode   (KeyCode, default Alpha1)
Hotkey.ContainerMode   (KeyCode, default Alpha2)
Hotkey.SettingsMode    (KeyCode, default Alpha3)
```

**Numpad 자동 derive**: 현재 `HotkeyMap` 의 `CharacterModeKeyNumpad = Keypad1`, `ContainerModeKeyNumpad = Keypad2` 는 hardcoded. Sub-project 결과: 사용자가 `CharacterMode = Alpha5` 로 변경하면 자동으로 `Keypad5` 매핑. 매핑 테이블:

```csharp
private static readonly Dictionary<KeyCode, KeyCode> AlphaToKeypad = new()
{
    { KeyCode.Alpha0, KeyCode.Keypad0 }, { KeyCode.Alpha1, KeyCode.Keypad1 },
    { KeyCode.Alpha2, KeyCode.Keypad2 }, { KeyCode.Alpha3, KeyCode.Keypad3 },
    { KeyCode.Alpha4, KeyCode.Keypad4 }, { KeyCode.Alpha5, KeyCode.Keypad5 },
    { KeyCode.Alpha6, KeyCode.Keypad6 }, { KeyCode.Alpha7, KeyCode.Keypad7 },
    { KeyCode.Alpha8, KeyCode.Keypad8 }, { KeyCode.Alpha9, KeyCode.Keypad9 },
};
// non-Alpha (예: F-키, 알파벳) 으로 rebind 시 Numpad pair 없음 — Numpad 매핑 disabled.
```

**Rebind UX 패턴 — 키 캡처**:
- 자체 SettingsPanel 의 hotkey row: `[라벨]: [현재 키 표시] [재설정]`
- [재설정] 클릭 → row "키 입력 대기..." 상태 → 다음 `Event.current.type == EventType.KeyDown` 발생 시 KeyCode 캡처 → buffer 에 저장
- 충돌 검증 — 4 hotkey 가 같은 키 사용 시 [저장] 버튼 disable + 경고 라벨
- ESC 키 입력 = 캡처 취소 (이전 값 유지)

`Event.current` 패턴 strip-safe — v0.7.4 D-1 의 cell 클릭 (`EventType.MouseDown`) 검증됨, 같은 EventType enum 의 KeyDown 도 strip-safe 가정. spike 1단계: smoke 첫 frame 에서 `Method unstripping failed` 패턴 확인.

### 1.4 신규 vs 기존 ConfigEntry 통합

기존 `Config.ToggleHotkey` 가 이미 KeyCode F11. v0.7.6 의 `Hotkey.MainKey` 는 별개 ConfigEntry 안 만들고 **기존 ToggleHotkey 재사용** — section 만 "General" → "Hotkey" 로 옮기는 것은 user config 마이그레이션 부담 → **section 그대로 ("General") 유지**, label 만 명확화. SettingsPanel UI 에서는 "MainKey" 로 표시.

### 1.5 진입 UX (자유 입력 ①)

**ModeSelector.Mode** enum 확장:
```csharp
public enum Mode { None, Character, Container, Settings }
```

ModeSelector 창에 신규 버튼 "설정 (F11+3)" 추가. ModWindow Update() 에서 F11+3 (Alpha3 + Keypad3) 단축키 추가 — `HotkeyMap.SettingsShortcut()` 신규.

ContainerPanel / 캐릭터 panel 처럼 ModWindow.\_lastSeenMode transition handler 에 Settings case 추가 — 다른 panel 닫고 SettingsPanel 열기.

### 1.6 "저장" 버튼 명시 (자유 입력 ②)

**Buffer 패턴**:
- SettingsPanel 의 `_buffer` (in-memory POCO) — hotkey 4 KeyCode + Container rect 4 float
- 사용자 입력 시 `_buffer` 만 변경 (ConfigEntry / live state 반영 안 함)
- [저장] 클릭 → `_buffer` → ConfigEntry.Value = ... → `HotkeyMap.Bind(Config)` 재호출 (정적 필드 sync) → ContainerPanel.SetRect(buffer.X/Y/W/H) → 토스트 "✔ 설정 저장됨"
- [취소] 또는 X 닫기 → `_buffer` 폐기 (ConfigEntry 재로드)
- panel 진입 (Visible = true) 시 → ConfigEntry → `_buffer` 로 hydrate

자동 영속화 항목 (검색·정렬·필터·last container·panel rect 6개) 은 buffer 거치지 않음 — ContainerPanel 변경 시 즉시 ConfigEntry write. SettingsPanel 에는 read-only 표시 + [영속화 정보 reset] 버튼만.

### 1.7 "기본값 복원" 버튼 (자유 입력 ③)

[기본값 복원] = `_buffer` 를 hardcoded default 로 reset (ConfigEntry 갱신 X — [저장] 명시 필수).

| Buffer 항목 | Default |
|---|---|
| MainKey | F11 |
| CharacterMode | Alpha1 |
| ContainerMode | Alpha2 |
| SettingsMode | Alpha3 |
| ContainerPanelX/Y/W/H | 150, 100, 800, 760 (`ContainerPanel._rect` hardcoded 초기값) |

[영속화 정보 reset] (별도 버튼) = 자동 영속 항목 6개 ConfigEntry 즉시 reset:
- SortKey = Category, SortAscending = true, FilterCategory = All, LastIndex = -1
- ModWindow rect / ItemDetailPanel rect / ContainerPanel rect 도 hardcoded default 로 (이건 사용자가 즉시 효과 보고 싶을 가능성 높음 — buffer 안 거침)

## 2. 변경 파일

### 2.1 신규 파일

#### 2.1.1 `src/LongYinRoster/UI/SettingsPanel.cs` (~280 LOC)

```csharp
using System;
using System.Collections.Generic;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.6 — Hybrid stateful-only 설정 panel.
/// hotkey 4 + ContainerPanel rect 4 buffer + [저장]/[기본값 복원]/[취소].
/// 자동 영속화 항목 (검색·정렬·필터·last container) 는 read-only 표시 + reset 버튼만.
/// </summary>
public sealed class SettingsPanel
{
    public bool Visible { get; set; } = false;
    public Rect WindowRect => _rect;

    private Rect _rect = new(200, 120, 480, 600);
    private const int WindowID = 0x4C593733;  // "LY73"

    // Buffer (저장 누르기 전까지 ConfigEntry 안 건드림)
    private struct Buffer
    {
        public KeyCode Main, CharacterMode, ContainerMode, SettingsMode;
        public float ContainerX, ContainerY, ContainerW, ContainerH;
    }
    private Buffer _buf;
    private bool   _bufHydrated;

    // 키 캡처 상태 — null 이 아니면 해당 row 가 "키 입력 대기..." 상태
    private enum CaptureSlot { None, Main, Character, Container, Settings }
    private CaptureSlot _capture = CaptureSlot.None;

    // 충돌 검증 캐시
    private bool _conflictCached;
    private string _conflictMsg = "";

    public Action? OnSaved;  // ModWindow 가 wire — HotkeyMap.Bind + ContainerPanel.SetRect 수행

    public void Hydrate()
    {
        _buf = new Buffer
        {
            Main          = Config.ToggleHotkey.Value,
            CharacterMode = Config.HotkeyCharacterMode.Value,
            ContainerMode = Config.HotkeyContainerMode.Value,
            SettingsMode  = Config.HotkeySettingsMode.Value,
            ContainerX    = Config.ContainerPanelX.Value,
            ContainerY    = Config.ContainerPanelY.Value,
            ContainerW    = Config.ContainerPanelW.Value,
            ContainerH    = Config.ContainerPanelH.Value,
        };
        _bufHydrated = true;
        _capture = CaptureSlot.None;
        RecomputeConflict();
    }

    public void OnGUI()
    {
        if (!Visible) return;
        if (!_bufHydrated) Hydrate();
        try { _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, ""); }
        catch (Exception ex) { Logger.Warn($"SettingsPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}"); }

        // 키 캡처 모드일 때 Event.current.KeyDown 처리 (OnGUI 안에서만 Event.current 유효)
        if (_capture != CaptureSlot.None && Event.current?.type == EventType.KeyDown)
        {
            var k = Event.current.keyCode;
            if (k == KeyCode.Escape) { _capture = CaptureSlot.None; }
            else if (k != KeyCode.None)
            {
                switch (_capture)
                {
                    case CaptureSlot.Main:      _buf.Main = k; break;
                    case CaptureSlot.Character: _buf.CharacterMode = k; break;
                    case CaptureSlot.Container: _buf.ContainerMode = k; break;
                    case CaptureSlot.Settings:  _buf.SettingsMode = k; break;
                }
                _capture = CaptureSlot.None;
                RecomputeConflict();
            }
            Event.current.Use();
        }
    }

    private void Draw(int id) { /* §3 layout */ }
    private void RecomputeConflict() { /* §1.3 충돌 검증 */ }
    private void DoSave() { /* §1.6 buffer → ConfigEntry → OnSaved */ }
    private void DoRestoreDefaults() { /* §1.7 buffer → hardcoded */ }
    private void DoResetPersistedView() { /* §1.7 영속화 정보 reset */ }
}
```

#### 2.1.2 `src/LongYinRoster.Tests/SettingsPanelTests.cs`

Buffer logic / 충돌 검증 / Numpad derive / hydrate-restore round-trip 검증. IMGUI 호출은 인게임 smoke 만 (`GUIStyle` Unity runtime dependency 회피).

| Test | 내용 |
|---|---|
| `Buffer_RoundTripFromConfigEntry` | Hydrate → buffer 값 == ConfigEntry.Value (4 hotkey + 4 rect) |
| `Conflict_DetectsDuplicateHotkey` | 4 hotkey 중 2개 같은 KeyCode → conflict true |
| `Conflict_AllowsUniqueHotkeys` | 모두 다름 → conflict false |
| `NumpadDerive_AlphaToKeypad` | Alpha5 → Keypad5, F11 → null |
| `NumpadDerive_FunctionKey` | F-키 / 알파벳 등 → null (Numpad 미지원) |
| `RestoreDefaults_ResetsBufferOnly` | RestoreDefaults 후 buffer == hardcoded default, ConfigEntry 미변경 |
| `Save_PropagatesBufferToConfig` | DoSave → ConfigEntry.Value == buffer 값 |
| `Cancel_DiscardsBuffer` | buffer 변경 후 Cancel → 다음 Hydrate 시 ConfigEntry.Value 로 복원 |

#### 2.1.3 `src/LongYinRoster.Tests/HotkeyMapTests.cs`

Numpad 매핑 + Bind 호출 후 정적 필드 sync 검증.

| Test | 내용 |
|---|---|
| `Bind_PropagatesConfigToStaticFields` | ConfigEntry 값 → HotkeyMap.MainKey/CharacterModeKey/... 일치 |
| `Bind_DerivesNumpadFromAlpha` | CharacterModeKey = Alpha7 → CharacterModeKeyNumpad = Keypad7 |
| `Bind_NumpadNoneForFunctionKey` | CharacterModeKey = F5 → CharacterModeKeyNumpad = KeyCode.None (체크 회피) |

### 2.2 변경 파일

#### 2.2.1 `src/LongYinRoster/Config.cs` (+10 ConfigEntry)

```csharp
// v0.7.6 — Hotkey rebind (3 신규, MainKey 는 기존 ToggleHotkey 재사용)
public static ConfigEntry<KeyCode> HotkeyCharacterMode = null!;
public static ConfigEntry<KeyCode> HotkeyContainerMode = null!;
public static ConfigEntry<KeyCode> HotkeySettingsMode  = null!;

// v0.7.6 — ContainerPanel rect 영속화 (ItemDetailPanel mirror)
public static ConfigEntry<float> ContainerPanelX = null!;
public static ConfigEntry<float> ContainerPanelY = null!;
public static ConfigEntry<float> ContainerPanelW = null!;
public static ConfigEntry<float> ContainerPanelH = null!;

// v0.7.6 — 자동 영속화 (ContainerPanel 사용 중 immediate write)
public static ConfigEntry<string> ContainerSortKey       = null!;  // "Category"|"Name"|"Grade"|"Quality"
public static ConfigEntry<bool>   ContainerSortAscending = null!;
public static ConfigEntry<string> ContainerFilterCategory = null!; // "All"|"Equipment"|"Medicine"|...
public static ConfigEntry<int>    ContainerLastIndex     = null!;  // -1 = 미선택

// Bind() 안 추가:
HotkeyCharacterMode = cfg.Bind("Hotkey", "CharacterMode", KeyCode.Alpha1, "캐릭터 관리 단축키 (F11+이 키)");
HotkeyContainerMode = cfg.Bind("Hotkey", "ContainerMode", KeyCode.Alpha2, "컨테이너 관리 단축키 (F11+이 키)");
HotkeySettingsMode  = cfg.Bind("Hotkey", "SettingsMode",  KeyCode.Alpha3, "설정 panel 단축키 (F11+이 키)");

ContainerPanelX = cfg.Bind("UI", "ContainerPanelX", 150f, "컨테이너 panel X");
ContainerPanelY = cfg.Bind("UI", "ContainerPanelY", 100f, "컨테이너 panel Y");
ContainerPanelW = cfg.Bind("UI", "ContainerPanelW", 800f, "컨테이너 panel 폭");
ContainerPanelH = cfg.Bind("UI", "ContainerPanelH", 760f, "컨테이너 panel 높이");

ContainerSortKey       = cfg.Bind("Container", "SortKey", "Category", "정렬 키 (Category|Name|Grade|Quality)");
ContainerSortAscending = cfg.Bind("Container", "SortAscending", true, "정렬 방향 (true=▲ 오름)");
ContainerFilterCategory = cfg.Bind("Container", "FilterCategory", "All", "카테고리 필터 (All|Equipment|Medicine|Food|Book|Treasure|Material|Horse)");
ContainerLastIndex     = cfg.Bind("Container", "LastIndex", -1, new ConfigDescription("마지막 선택 컨테이너 idx (-1=미선택)", new AcceptableValueRange<int>(-1, 9999)));
```

#### 2.2.2 `src/LongYinRoster/Util/HotkeyMap.cs` — `Bind()` 추가

```csharp
/// <summary>v0.7.6 — Config 의 ConfigEntry 값을 정적 필드에 sync. Plugin Awake 와 SettingsPanel.DoSave 가 호출.</summary>
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

public static KeyCode SettingsModeKey       = KeyCode.Alpha3;  // 신규
public static KeyCode SettingsModeKeyNumpad = KeyCode.Keypad3;

public static bool SettingsShortcut() =>
    Input.GetKey(MainKey) &&
    (Input.GetKeyDown(SettingsModeKey) ||
     (SettingsModeKeyNumpad != KeyCode.None && Input.GetKeyDown(SettingsModeKeyNumpad)));

// MainKeyPressedAlone 갱신 — Settings/Numpad 도 검사
public static bool MainKeyPressedAlone() { /* 6 키 모두 검사 */ }

private static KeyCode NumpadFor(KeyCode alpha) => alpha switch
{
    KeyCode.Alpha0 => KeyCode.Keypad0, KeyCode.Alpha1 => KeyCode.Keypad1,
    KeyCode.Alpha2 => KeyCode.Keypad2, KeyCode.Alpha3 => KeyCode.Keypad3,
    KeyCode.Alpha4 => KeyCode.Keypad4, KeyCode.Alpha5 => KeyCode.Keypad5,
    KeyCode.Alpha6 => KeyCode.Keypad6, KeyCode.Alpha7 => KeyCode.Keypad7,
    KeyCode.Alpha8 => KeyCode.Keypad8, KeyCode.Alpha9 => KeyCode.Keypad9,
    _              => KeyCode.None,
};
```

#### 2.2.3 `src/LongYinRoster/UI/ModeSelector.cs` — Settings mode 추가

```csharp
public enum Mode { None, Character, Container, Settings }   // Settings 추가

// _windowRect 높이 200 → 240 (버튼 1개 추가)
private Rect _windowRect = new(100, 100, 280, 240);

// DrawWindow 안 버튼 추가
if (GUILayout.Button("설정 (F11+3)", GUILayout.Height(32))) SetMode(Mode.Settings);
```

#### 2.2.4 `src/LongYinRoster/UI/ModWindow.cs` — Settings panel 통합

```csharp
private readonly SettingsPanel _settingsPanel = new();

// Awake() 안 wire-up
_settingsPanel.OnSaved = () =>
{
    HotkeyMap.Bind();
    _containerPanel.SetRect(Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
                            Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
    ToastService.Push("✔ 설정 저장됨", ToastKind.Success);
};

// Update() 안 — F11+3 단축키
if (HotkeyMap.SettingsShortcut()) _modeSelector.SetMode(ModeSelector.Mode.Settings);

// transition handler — Settings case
else if (_modeSelector.CurrentMode == ModeSelector.Mode.Settings)
{
    _settingsPanel.Visible = true;
    _settingsPanel.Hydrate();
    if (_visible) Toggle();
    _containerPanel.Visible = false;
}

// X 닫기 sync
if (_lastSettingsVisible && !_settingsPanel.Visible)
{
    _modeSelector.SetMode(ModeSelector.Mode.None);
    _lastSeenMode = ModeSelector.Mode.None;
}
_lastSettingsVisible = _settingsPanel.Visible;

// OnGUI() 안
_settingsPanel.OnGUI();

// ContainerPanel rect 영속화 (ItemDetailPanel mirror)
Config.ContainerPanelX.Value = _containerPanel.WindowRect.x;
Config.ContainerPanelY.Value = _containerPanel.WindowRect.y;
Config.ContainerPanelW.Value = _containerPanel.WindowRect.width;
Config.ContainerPanelH.Value = _containerPanel.WindowRect.height;

// ShouldBlockMouse 갱신 — SettingsPanel 영역
if (_instance._settingsPanel.Visible && _instance._settingsPanel.WindowRect.Contains(pos)) return true;
```

#### 2.2.5 `src/LongYinRoster/UI/ContainerPanel.cs` — 영속화 hydrate + immediate write

```csharp
// SetRepository 또는 첫 OnGUI 시점에 hydrate (Plugin.cs 가 Config.Bind 끝낸 후 호출)
public void HydrateFromConfig()
{
    _filter = ParseFilter(Config.ContainerFilterCategory.Value);
    _globalState = new SearchSortState("",
        ParseSortKey(Config.ContainerSortKey.Value),
        Config.ContainerSortAscending.Value);
    _selectedContainerIdx = Config.ContainerLastIndex.Value;
    if (_selectedContainerIdx > 0 && !_containerList.Exists(c => c.ContainerIndex == _selectedContainerIdx))
        _selectedContainerIdx = -1;  // 삭제된 컨테이너 가리킴 → fallback
}

// SetRect — SettingsPanel 의 buffer 적용용
public void SetRect(float x, float y, float w, float h) { _rect = new Rect(x, y, w, h); }

// _filter 변경 지점 (DrawCategoryTabs)
if (GUILayout.Button(...)) {
    _filter = cat;
    Config.ContainerFilterCategory.Value = cat.ToString();   // immediate write
}

// _globalState 변경 지점 (DrawGlobalToolbar)
if (!newState.Equals(_globalState)) {
    _globalState = newState;
    Config.ContainerSortKey.Value = newState.Key.ToString();
    Config.ContainerSortAscending.Value = newState.Ascending;
    _invView.Invalidate(); _stoView.Invalidate(); _conView.Invalidate();
}

// _selectedContainerIdx 변경 지점 (dropdown)
if (GUILayout.Button($"{m.ContainerIndex:D2}: {m.ContainerName}")) {
    _selectedContainerIdx = m.ContainerIndex;
    Config.ContainerLastIndex.Value = m.ContainerIndex;
    _dropdownOpen = false;
    OnContainerSelected?.Invoke(m.ContainerIndex);
}
```

`ParseFilter` / `ParseSortKey` invalid 값 (예: 사용자가 cfg 파일 손수 잘못 편집) → fallback default.

#### 2.2.6 `src/LongYinRoster/Plugin.cs` — Awake 흐름

```csharp
// Config.Bind 끝난 직후
HotkeyMap.Bind();
// SlotRepository / ContainerRepository wire-up 후
_containerPanel.HydrateFromConfig();
```

## 3. SettingsPanel UI Layout (480×600)

```
+========== 설정 ============== X ==+
| ⚠ 고급 설정은 ConfigManager (F5)    |
+ - - - - - - - - - - - - - - - - - +
| ▼ 단축키                            |
| 메인 토글:        [F11        ] [재] |
| 캐릭터 관리:      [Alpha1     ] [재] |
| 컨테이너 관리:    [Alpha2     ] [재] |
| 설정 panel:       [Alpha3     ] [재] |
| ⚠ 충돌: Alpha1 / SettingsMode 동일  |
+ - - - - - - - - - - - - - - - - - +
| ▼ 컨테이너 panel 위치/크기            |
| X: [150  ] Y: [100  ]               |
| W: [800  ] H: [760  ]               |
+ - - - - - - - - - - - - - - - - - +
| ▼ 영속화 정보 (자동 저장)            |
| 정렬: 등급 ▲                         |
| 필터: 장비                           |
| 마지막 컨테이너: #3                  |
| Mod 창 위치/크기: (1100, 100, 720×560)|
| ItemDetail 위치/크기: (970, 100, ...)|
| ContainerPanel: (150, 100, 800×760)  |
| [영속화 정보 reset]                  |
+ - - - - - - - - - - - - - - - - - +
| [저장] [기본값 복원] [취소]            |
+===================================+
```

- **X 닫기** = [취소] 와 동일 (buffer 폐기)
- **[저장] disabled** 조건 = hotkey 충돌 또는 변경 사항 없음
- **재설정 클릭** 후 row 라벨이 "키 입력 대기..." 로 바뀌고 다음 KeyDown 캡처. ESC = 취소.
- **읽기 전용 표시 (영속화 정보 섹션)** = ContainerPanel 사용 중 자동 갱신된 ConfigEntry 들. 사용자가 직접 편집 안 함.

## 4. 검증된 IMGUI 패턴 (strip-safe 확인)

기존 v0.7.5.2 까지 검증된 patterns 만 사용 — 메모리 `LongYin IL2CPP IMGUI strip-safe patterns` 의 §Strip-safe 목록 그대로:

- `GUI.Window`, `GUI.Button(Rect, string)`, `GUI.Label(Rect, string)`, `GUI.DrawTexture(Rect, Texture)`, `GUI.color`, `GUI.DragWindow`
- `GUILayout.Button/Label/Toggle/TextField(*, params)`, `GUILayout.Space(float)`, `GUILayout.BeginHorizontal/EndHorizontal/BeginVertical/EndVertical`, `GUILayout.Width/Height(float)`
- `Event.current` + `EventType.MouseDown` (v0.7.4 D-1 검증) — **`EventType.KeyDown`** 도 같은 enum 이라 strip-safe 가정. **§7 Risk 의 spike 1 항목**.

신규 IMGUI API 사용 안 함. `GUIStyle` ctor / `GUILayout.Box` / `GUILayoutUtility.GetLastRect` / `FlexibleSpace` 등 strip 확인 API 회피.

## 5. Test 변경

- `SettingsPanelTests.cs` (신규) — 8 case (§2.1.2)
- `HotkeyMapTests.cs` (신규) — 3 case (§2.1.3)
- 기존 216 → **227 (+11)**.

ContainerPanel 의 immediate ConfigEntry write 는 BepInEx ConfigFile 객체 instance 가 unit test 에서 사용 불가 (BepInEx runtime dependency) → 인게임 smoke 만.

## 6. 인게임 Smoke

### 6.1 신규 시나리오 (12)

| # | 시나리오 | 기대 |
|---|---|---|
| S1 | F11 → 메뉴에 "설정 (F11+3)" 표시 | 280×240 메뉴, 3개 버튼 |
| S2 | F11+3 → SettingsPanel 진입 | 480×600 panel, hydrate 됨 |
| S3 | hotkey 재설정 클릭 → 키 입력 → 캡처 | row 라벨 갱신 (예: "Alpha5") |
| S4 | hotkey 충돌 발생 (2 row 같은 키) | 충돌 경고 라벨 + [저장] disabled |
| S5 | ESC 캡처 취소 | row 라벨 이전 값 유지 |
| S6 | [저장] → 토스트 + 단축키 즉시 반영 | F11+신규키 작동 |
| S7 | [기본값 복원] | buffer 만 reset, ConfigEntry 미변경 (X 닫고 다시 열면 이전 값) |
| S8 | [취소] / X | buffer 폐기, 다음 진입 시 ConfigEntry 값 hydrate |
| S9 | ContainerPanel 검색·정렬·필터·dropdown 변경 → SettingsPanel 재진입 | 영속화 정보 섹션에 갱신된 값 표시 |
| S10 | [영속화 정보 reset] | SortKey=Category/▲/필터=All/LastIndex=-1 즉시 반영 |
| S11 | ContainerPanel rect 영속화 | 패널 드래그 후 게임 재시작 → 같은 위치 |
| S12 | Numpad 자동 derive | CharacterMode=Alpha7 저장 → F11+Keypad7 도 캐릭터 모드 진입 |

### 6.2 회귀 시나리오 (16)

기존 v0.7.5.2 smoke 11/11 + v0.7.4 D-1 핵심 회귀 5 (cell focus, 검색 한글/한자, 정렬, ItemDetail 7 카테고리, ⓘ 토글) — 모두 PASS 기대.

### 6.3 Strip 검증

S2 진입 + S3 키 캡처 직후 BepInEx 로그 grep:
```
grep -n "Method unstripping failed" LogOutput.log
```
→ 0건 기대. 발견 시 §7 Risk fallback 패턴 적용.

총 28 시나리오.

## 7. Risk

### 7.1 `EventType.KeyDown` strip 위험 (HIGH)

**우려**: v0.7.4 의 `EventType.MouseDown` 만 검증됨. KeyDown 은 동일 enum 이라 strip-safe 가정이지만 IL2CPP 빌드 strip 패턴이 enum value 별로 다를 수 있음.

**Spike 1 (impl 첫 단계)**: SettingsPanel skeleton 만 만들고 키 캡처 테스트 — 진입 + KeyDown 한 번 → `Method unstripping failed` 없음 확인.

**Fallback 1차**: `Input.GetKeyDown` polling — `Update()` 안에서 `_capture != None` 일 때 `KeyCode` enum 전체 iterate (`Enum.GetValues<KeyCode>()` foreach `Input.GetKeyDown(k)`). CPU 비용 있지만 strip-safe 보장.

**Fallback 2차**: KeyCode dropdown — Alpha0~9 + F1~12 + 알파벳 26 + 자주 쓰는 modifier 만 hardcoded list 로 보여주고 클릭 선택. UX 떨어지지만 가장 안전.

### 7.2 ConfigEntry section 변경 (MEDIUM)

기존 `Config.ToggleHotkey` 가 `[General]` section. 신규 hotkey 3개는 `[Hotkey]` section. **사용자 cfg 파일 마이그레이션 필요 없음** — BepInEx 가 신규 section 자동 생성. 다만 ConfigManager F5 가 `[General].ToggleHotkey` 를 따로 보여주고 `[Hotkey]` 섹션을 별도로 보여줌 → UX 혼란 가능. **결정**: ToggleHotkey 도 `[Hotkey]` 로 옮기면 마이그레이션 부담. 그대로 두고 SettingsPanel 에서만 통합 표시. ConfigManager 분산은 수용.

### 7.3 SearchSortState invalid 복원 (LOW)

cfg 파일 손수 편집으로 `Container.SortKey = "Foo"` 같은 invalid 값 → `ParseSortKey` 가 default 반환 + 1회 warn log. 회귀 위험 없음.

### 7.4 ContainerPanel.WindowRect drag 후 ConfigEntry write (LOW)

매 frame `Config.ContainerPanelX.Value = _rect.x` 호출 — BepInEx ConfigEntry 는 setter 가 변경 감지 후 dirty flag 만 set, 실제 file write 는 SaveOnChange 또는 게임 종료 시. 매 frame setter 호출 자체는 cheap. ItemDetailPanel 와 동일 패턴 (이미 v0.7.4 검증됨).

### 7.5 Hotkey 변경 후 즉시 반영 (LOW)

`HotkeyMap.Bind()` 호출 시 정적 필드 갱신 — `ModWindow.Update()` 의 `HotkeyMap.MainKeyPressedAlone()` 등이 다음 프레임부터 새 값 사용. 추가 작업 불필요.

### 7.6 Plugin.cs 의 Awake order (MEDIUM)

`HotkeyMap.Bind()` 가 `Config.Bind(cfg)` **이후** 호출돼야 ConfigEntry 가 populated. `ContainerPanel.HydrateFromConfig()` 도 마찬가지. Plugin.cs 의 Initialize/Load 순서 정확히 명시. spike 후 plan §1 단계 명시.

## 8. Out-of-scope

- BepInEx ConfigEntry mirror 편집 (Q1=B 결정) — ConfigManager F5 위임
- 외부 컨테이너 max items per container limit (Q3-2=A 결정) — 무제한 유지
- "프로필" 패턴 (`CheatProfiles` mirror) — 사용자 1 환경 가정. 설정 export/import 도 OOS.
- ContainerPanel drag-resize 핸들 (`DrawPanelResizeHandle` 패턴) — 현재 GUI.Window 가 drag-only. 사용자 명시 요청 없으면 OOS. cfg 파일 직접 편집은 가능.
- "최근 검색어" history 또는 검색 textbox 영속화 (Q2=a 결정)

## 9. Cycle 계획

per 메타 §5.1:
1. **brainstorm** = 본 spec (사용자 review + 승인 게이트)
2. **plan** = 별도 plan 문서 (Spike 1, EventType.KeyDown 검증 → Layer 1 Config + HotkeyMap → Layer 2 SettingsPanel skeleton + Tests → Layer 3 SettingsPanel UI + 키 캡처 + 충돌 검증 → Layer 4 ModWindow / ModeSelector / ContainerPanel hydrate 통합 → Layer 5 smoke 28)
3. **impl** = layer 별 단위 commit
4. **smoke** = 인게임 28 시나리오
5. **release** = v0.7.6 tag + GitHub release
6. **handoff** = HANDOFF.md / 메타 §2.3 Result 섹션 / G1 게이트 진입 명시

## 10. 명명 / 버전 / 호환성

- 버전: `v0.7.6` (확정 sub-project, patch 아님)
- spec slug: `longyin-roster-mod-v0.7.6-design`
- plan slug: `longyin-roster-mod-v0.7.6-plan`
- smoke dump: `2026-05-XX-v0.7.6-smoke-results.md`
- 사용자 cfg 마이그레이션: 신규 `[Hotkey]` / `[Container]` / `[UI]` (ContainerPanel*) section 자동 생성. 기존 사용자 영향 없음. legacy slot 파일 schema 변경 없음.

## 11. 다음 단계 (메타 G1 진입 준비)

v0.7.6 release 직후 G1 결정 게이트 (메타 §3.1):
- v0.7.7 (후보) Item editor — GO/DEFER/NO-GO
- v0.8 (후보) 진짜 sprite — GO/DEFER/NO-GO
- maintenance — ACTIVATE/WAIT

본 spec 은 G1 진입의 prerequisite 마지막 사용자 mod "기본 기능" 완성 단계. spec 통과 → plan 작성 → impl → smoke → release → G1.
