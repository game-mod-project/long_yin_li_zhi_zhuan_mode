using BepInEx.Configuration;
using UnityEngine;

namespace LongYinRoster;

public static class Config
{
    public static ConfigEntry<KeyCode> ToggleHotkey       = null!;
    public static ConfigEntry<bool>    PauseGameWhileOpen = null!;
    public static ConfigEntry<string>  SlotDirectory      = null!;
    public static ConfigEntry<int>     MaxSlots           = null!;

    public static ConfigEntry<float>   WindowX = null!;
    public static ConfigEntry<float>   WindowY = null!;
    public static ConfigEntry<float>   WindowW = null!;
    public static ConfigEntry<float>   WindowH = null!;

    public static ConfigEntry<bool>    AutoBackupBeforeApply   = null!;
    public static ConfigEntry<bool>    AllowApplyToGame        = null!;

    public static ConfigEntry<int>     LogLevel = null!;

    public static ConfigEntry<float>   InventoryMaxWeight = null!;
    public static ConfigEntry<float>   StorageMaxWeight   = null!;

    // v0.7.4 D-1 — ItemDetailPanel 위치/크기/디폴트 visibility
    public static ConfigEntry<float>   ItemDetailPanelX      = null!;
    public static ConfigEntry<float>   ItemDetailPanelY      = null!;
    public static ConfigEntry<float>   ItemDetailPanelWidth  = null!;
    public static ConfigEntry<float>   ItemDetailPanelHeight = null!;
    public static ConfigEntry<bool>    ItemDetailPanelOpen   = null!;

    // v0.7.6 — Hotkey rebind (MainKey 는 기존 ToggleHotkey 재사용 — 마이그레이션 부담 회피)
    public static ConfigEntry<KeyCode> HotkeyCharacterMode    = null!;
    public static ConfigEntry<KeyCode> HotkeyContainerMode    = null!;
    public static ConfigEntry<KeyCode> HotkeySettingsMode     = null!;
    // v0.7.8 — Player editor hotkey
    public static ConfigEntry<KeyCode> HotkeyPlayerEditorMode = null!;

    // v0.7.8 — PlayerEditorPanel rect 영속화
    public static ConfigEntry<float>   PlayerEditorPanelX = null!;
    public static ConfigEntry<float>   PlayerEditorPanelY = null!;
    public static ConfigEntry<float>   PlayerEditorPanelW = null!;
    public static ConfigEntry<float>   PlayerEditorPanelH = null!;
    public static ConfigEntry<bool>    PlayerEditorPanelOpen = null!;

    // v0.7.10 Phase 1 — Lock 천부 max 보유수 (cheat GameplayPatch.GetMaxTagNum mirror)
    public static ConfigEntry<bool>    LockMaxTagNum         = null!;
    public static ConfigEntry<int>     LockedMaxTagNumValue  = null!;

    // v0.7.10 Phase 3 — Cap bypass (자질값 max 돌파, cheat MultiplierPatch.EnableUncapMax mirror)
    public static ConfigEntry<bool>    EnableUncapMax        = null!;
    public static ConfigEntry<int>     UncapMaxAttri         = null!;
    public static ConfigEntry<int>     UncapMaxFightSkill    = null!;
    public static ConfigEntry<int>     UncapMaxLivingSkill   = null!;

    // v0.7.6 — ContainerPanel rect 영속화 (ItemDetailPanel mirror)
    public static ConfigEntry<float>   ContainerPanelX = null!;
    public static ConfigEntry<float>   ContainerPanelY = null!;
    public static ConfigEntry<float>   ContainerPanelW = null!;
    public static ConfigEntry<float>   ContainerPanelH = null!;

    // v0.7.6 — 자동 영속화 (ContainerPanel 사용 중 immediate ConfigEntry write)
    public static ConfigEntry<string>  ContainerSortKey        = null!;
    public static ConfigEntry<bool>    ContainerSortAscending  = null!;
    public static ConfigEntry<string>  ContainerFilterCategory = null!;
    public static ConfigEntry<int>     ContainerLastIndex      = null!;

    public static void Bind(ConfigFile cfg)
    {
        ToggleHotkey       = cfg.Bind("General", "ToggleHotkey",       KeyCode.F11,
                                      "모드 창 토글 단축키");
        PauseGameWhileOpen = cfg.Bind("General", "PauseGameWhileOpen", true,
                                      "모드 창이 열려 있는 동안 Time.timeScale=0 (게임 input 통과 차단)");
        SlotDirectory      = cfg.Bind("General", "SlotDirectory",      "<PluginPath>/Slots",
                                      "슬롯 파일 디렉터리. <PluginPath> = BepInEx/plugins/LongYinRoster");
        MaxSlots           = cfg.Bind("General", "MaxSlots",            20,
                                      new ConfigDescription(
                                          "사용자 슬롯 개수 (1~MaxSlots). 슬롯 0(자동백업)은 제외.",
                                          new AcceptableValueRange<int>(5, 50)));

        WindowX = cfg.Bind("UI", "WindowX", 1100f, "");
        WindowY = cfg.Bind("UI", "WindowY",  100f, "");
        WindowW = cfg.Bind("UI", "WindowW",  720f, "");
        WindowH = cfg.Bind("UI", "WindowH",  560f, "");   // v0.4: 480 → 560 (체크박스 grid +60~80px)

        AutoBackupBeforeApply = cfg.Bind("Behavior", "AutoBackupBeforeApply", true,
                                         "덮어쓰기 직전 슬롯 0에 자동 저장 (실패 시 자동복원의 source)");
        AllowApplyToGame      = cfg.Bind("Behavior", "AllowApplyToGame",      true,
                                         "Apply 자체 kill switch. dump phase 에서 false 권장");

        LogLevel = cfg.Bind("Logging", "LogLevel", 3,
                            new ConfigDescription(
                                "0=Off, 1=Error, 2=Warn, 3=Info, 4=Debug",
                                new AcceptableValueRange<int>(0, 4)));

        InventoryMaxWeight = cfg.Bind("Container", "InventoryMaxWeight", 964f,
                                      new ConfigDescription(
                                          "인벤토리 무게 한계 (kg, float). reflection 우선 (ItemListData.maxWeight), 미발견 시 본 값 fallback.",
                                          new AcceptableValueRange<float>(100f, 10000f)));
        StorageMaxWeight   = cfg.Bind("Container", "StorageMaxWeight",   300f,
                                      new ConfigDescription(
                                          "창고 무게 한계 (kg, float). 동상.",
                                          new AcceptableValueRange<float>(10f, 50000f)));

        // v0.7.4 D-1 — ItemDetailPanel 영속화
        ItemDetailPanelX      = cfg.Bind("UI", "ItemDetailPanelX",      970f,  "item 상세 panel X 좌표");
        ItemDetailPanelY      = cfg.Bind("UI", "ItemDetailPanelY",      100f,  "item 상세 panel Y 좌표");
        ItemDetailPanelWidth  = cfg.Bind("UI", "ItemDetailPanelWidth",  480f,  "item 상세 panel 폭 (v0.7.7 stat editor 위해 380→480)");
        ItemDetailPanelHeight = cfg.Bind("UI", "ItemDetailPanelHeight", 640f,  "item 상세 panel 높이 (v0.7.7 stat editor 위해 500→640)");
        ItemDetailPanelOpen   = cfg.Bind("UI", "ItemDetailPanelOpen",   false, "item 상세 panel 디폴트 visibility (F11+2 진입 시 시작 상태)");

        // v0.7.6 — Hotkey rebind (3 신규, MainKey 는 기존 ToggleHotkey 재사용)
        HotkeyCharacterMode    = cfg.Bind("Hotkey", "CharacterMode",    KeyCode.Alpha1, "캐릭터 관리 단축키 (F11+이 키)");
        HotkeyContainerMode    = cfg.Bind("Hotkey", "ContainerMode",    KeyCode.Alpha2, "컨테이너 관리 단축키 (F11+이 키)");
        HotkeySettingsMode     = cfg.Bind("Hotkey", "SettingsMode",     KeyCode.Alpha3, "설정 panel 단축키 (F11+이 키)");
        // v0.7.8 — Player editor hotkey
        HotkeyPlayerEditorMode = cfg.Bind("Hotkey", "PlayerEditorMode", KeyCode.Alpha4, "플레이어 편집 단축키 (F11+이 키)");

        // v0.7.8 — PlayerEditorPanel rect 영속화 (ItemDetailPanel mirror)
        PlayerEditorPanelX    = cfg.Bind("UI", "PlayerEditorPanelX",    200f,  "플레이어 편집 panel X 좌표");
        PlayerEditorPanelY    = cfg.Bind("UI", "PlayerEditorPanelY",    120f,  "플레이어 편집 panel Y 좌표");
        PlayerEditorPanelW    = cfg.Bind("UI", "PlayerEditorPanelW",    720f,  "플레이어 편집 panel 폭 (v0.7.8: 480→720)");
        PlayerEditorPanelH    = cfg.Bind("UI", "PlayerEditorPanelH",    720f,  "플레이어 편집 panel 높이");
        PlayerEditorPanelOpen = cfg.Bind("UI", "PlayerEditorPanelOpen", false, "플레이어 편집 panel 디폴트 visibility");

        // v0.7.10 Phase 1 — Lock 천부 max 보유수
        LockMaxTagNum         = cfg.Bind("Hero", "LockMaxTagNum",         false,
                                         "천부 max 보유수 lock — true 시 GetMaxTagNum() Postfix 가 LockedMaxTagNumValue 로 override (Player heroID=0 only)");
        LockedMaxTagNumValue  = cfg.Bind("Hero", "LockedMaxTagNumValue",  999,
                                         new ConfigDescription(
                                             "LockMaxTagNum=true 시 적용할 천부 max 값 (1~999999)",
                                             new AcceptableValueRange<int>(1, 999999)));

        // v0.7.10 Phase 3 — Cap bypass
        EnableUncapMax        = cfg.Bind("Hero", "EnableUncapMax", false,
                                         "자질값 cap 해제 — true 시 GetMaxAttri/FightSkill/LivingSkill Postfix 가 UncapMax* 값으로 override (Player heroID=0 only). 현재 자질값(baseAttri/...)이 game cap 120/120/100 보다 큰 값으로 설정 가능해짐. v0.7.11 에서 per-hero 확장 예정.");
        UncapMaxAttri         = cfg.Bind("Hero", "UncapMaxAttri", 999,
                                         new ConfigDescription(
                                             "자질값 max override (속성). 게임 cap 120 → 본 값. range [120, 999999]",
                                             new AcceptableValueRange<int>(120, 999999)));
        UncapMaxFightSkill    = cfg.Bind("Hero", "UncapMaxFightSkill", 999,
                                         new ConfigDescription(
                                             "자질값 max override (무학). 게임 cap 120 → 본 값. range [120, 999999]",
                                             new AcceptableValueRange<int>(120, 999999)));
        UncapMaxLivingSkill   = cfg.Bind("Hero", "UncapMaxLivingSkill", 999,
                                         new ConfigDescription(
                                             "자질값 max override (기예). 게임 cap 100 → 본 값. range [100, 999999]",
                                             new AcceptableValueRange<int>(100, 999999)));

        // v0.7.6 — ContainerPanel rect 영속화
        ContainerPanelX = cfg.Bind("UI", "ContainerPanelX", 150f, "컨테이너 panel X 좌표");
        ContainerPanelY = cfg.Bind("UI", "ContainerPanelY", 100f, "컨테이너 panel Y 좌표");
        ContainerPanelW = cfg.Bind("UI", "ContainerPanelW", 800f, "컨테이너 panel 폭");
        ContainerPanelH = cfg.Bind("UI", "ContainerPanelH", 760f, "컨테이너 panel 높이");

        // v0.7.6 — 자동 영속화 (ContainerPanel 사용 중 immediate write)
        ContainerSortKey        = cfg.Bind("Container", "SortKey",        "Category",
                                           "정렬 키 (Category|Name|Grade|Quality)");
        ContainerSortAscending  = cfg.Bind("Container", "SortAscending",  true,
                                           "정렬 방향 (true=▲ 오름)");
        ContainerFilterCategory = cfg.Bind("Container", "FilterCategory", "All",
                                           "카테고리 필터 (All|Equipment|Medicine|Food|Book|Treasure|Material|Horse)");
        ContainerLastIndex      = cfg.Bind("Container", "LastIndex",      -1,
                                           new ConfigDescription(
                                               "마지막 선택 컨테이너 idx (-1=미선택)",
                                               new AcceptableValueRange<int>(-1, 9999)));
    }
}
