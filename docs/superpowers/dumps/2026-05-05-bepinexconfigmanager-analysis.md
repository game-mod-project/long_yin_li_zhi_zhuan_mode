# BepInExConfigManager 분석 (sinai v1.3.0)

날짜: 2026-05-05
패처: `BepInEx/patchers/BepInExConfigManager.Il2Cpp.Patcher.dll` (6,144 B)
플러그인: `BepInEx/plugins/BepInExConfigManager.Il2Cpp.CoreCLR.dll` (54,272 B)
디컴파일 결과:
- `C:/Users/deepe/AppData/Local/Temp/configmgr_patcher_decomp/`
- `C:/Users/deepe/AppData/Local/Temp/configmgr_plugin_decomp/`

## 결론: 한글화 mod 가 아님

**sinai 가 만든 BepInEx config UI 편집 도구**입니다. 한글 통팩에 함께 동봉돼 있지만, 한글화는 별개의 `LongYinLiZhiZhuan_Mod.dll` (Sirius) 가 담당합니다. ConfigManager 는 그 한글화 mod 의 BepInEx config 값을 게임 안에서 GUI 로 편집할 수 있게 해주는 보조 도구입니다.

## 기본 정보

| 항목 | 값 |
|------|------|
| Patcher GUID | `com.sinai.bepinexconfigmanager.patcher` |
| Plugin GUID | `com.sinai.BepInExConfigManager` |
| 버전 | 1.3.0 (2022) |
| 저자 | Sinai |
| 메뉴 토글 | F5 (`Main_Menu_Toggle` config) |
| 의존성 | UniverseLib (UI 라이브러리), BepInEx 6 IL2CPP |

## 아키텍처

### Patcher (Preloader 단계)
```
ConfigManagerPatcher : BasePatcher
  └─ Patcher.Init()
       └─ new Harmony("com.sinai.bepinexconfigmanager.patcher").PatchAll()
            └─ ConfigFile_ctor (Postfix)
                 └─ 모든 ConfigFile 인스턴스를 CachedConfigFile 로 래핑 → ConfigFiles 리스트에 누적
                      → ConfigFileCreated event 발화
```

### Plugin (Runtime 단계)
```
ConfigManager : BasePlugin
  ├─ ManagerBehaviour (MonoBehaviour, DontDestroyOnLoad)
  │   └─ Update() → DoUpdate() → F5 키 감지 → UIManager.ShowMenu 토글
  ├─ Universe.Init(Startup_Delay, LateInit, LogHandler, UniverseLibConfig)
  │   ├─ Disable_EventSystem_Override
  │   ├─ Force_Unlock_Mouse = true
  │   └─ Unhollowed_Modules_Folder = BepInEx/interop
  └─ LateInit() → UIManager.Init()
```

### Config 항목 (`BepInEx/config/com.sinai.BepInExConfigManager.cfg`)
- `Main Menu Toggle` (KeyCode, default F5)
- `Auto-save settings` (bool, default false)
- `Startup Delay` (Single, default 1.0)
- `Disable EventSystem Override` (bool, default false)

## InteractiveValues UI 컴포넌트

ConfigManager.UI.InteractiveValues 폴더 (10종):
- InteractiveBool / InteractiveColor / InteractiveEnum / InteractiveFlags
- InteractiveFloatStruct / InteractiveKeycode / InteractiveNumber
- InteractiveString / InteractiveTomlObject / InteractiveValue / InteractiveValueList

각 BepInEx config 타입에 대응하는 IMGUI 편집기. ConfigFile/EntryInfo/CachedConfigEntry 단위로 카테고리화돼서 UIManager 가 트리 표시.

## 한글 통팩 vs 게임 설치본 동일성

| 파일 | md5 | 일치 |
|---|---|---|
| `BepInExConfigManager.Il2Cpp.Patcher.dll` (6,144 B) | 동일 | ✅ |
| `BepInExConfigManager.Il2Cpp.CoreCLR.dll` (54,272 B) | `3432286a679c9b4424dc51ea4f5e72fd` | ✅ |

게임 설치본과 통팩의 sinai mod 는 완전 동일.

## LongYinRoster 와의 관련성

- ConfigManager 는 어떤 BepInEx plugin 이든 `ConfigEntry<T>` 로 정의한 설정을 자동 인식·편집해 주므로, **LongYinRoster 가 BepInEx config 를 추가하면 사용자가 ConfigManager F5 메뉴에서 즉시 GUI 로 편집 가능**.
- v0.7.6 설정 panel 작업 시 자체 IMGUI 로 만들지 vs ConfigManager 에 위임할지는 trade-off 결정 필요. 자체 panel 은 hotkey/창 크기 등 LongYinRoster 전용 옵션을 한 곳에 모을 수 있지만, ConfigManager 는 zero-cost 로 모든 BepInEx config 를 노출.
