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
    public static ConfigEntry<bool>    RunPinpointPatchOnApply = null!;

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
