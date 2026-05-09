using UnityEngine;

namespace LongYinRoster.Util;

/// <summary>
/// F11 / F11+숫자 hotkey 처리. v0.7.0 부터 ModeSelector 가 사용.
/// v0.7.6 — SettingsModeKey 추가 + Config.Bind 후 Bind() 호출로 정적 필드 sync + Numpad 자동 derive.
/// </summary>
public static class HotkeyMap
{
    public static KeyCode MainKey                  = KeyCode.F11;
    public static KeyCode CharacterModeKey         = KeyCode.Alpha1;
    public static KeyCode ContainerModeKey         = KeyCode.Alpha2;
    public static KeyCode SettingsModeKey          = KeyCode.Alpha3;     // v0.7.6 신규
    public static KeyCode PlayerEditorModeKey      = KeyCode.Alpha4;     // v0.7.8 신규
    public static KeyCode CharacterModeKeyNumpad   = KeyCode.Keypad1;
    public static KeyCode ContainerModeKeyNumpad   = KeyCode.Keypad2;
    public static KeyCode SettingsModeKeyNumpad    = KeyCode.Keypad3;    // v0.7.6 신규
    public static KeyCode PlayerEditorModeKeyNumpad = KeyCode.Keypad4;   // v0.7.8 신규

    /// <summary>
    /// v0.7.6 — Config 의 ConfigEntry 값을 정적 필드에 sync. Plugin Awake 와 SettingsPanel.DoSave 가 호출.
    /// Numpad pair 는 Alpha→Keypad 매핑 테이블로 자동 derive (non-Alpha 면 KeyCode.None).
    /// </summary>
    public static void Bind()
    {
        MainKey                  = Config.ToggleHotkey.Value;
        CharacterModeKey         = Config.HotkeyCharacterMode.Value;
        ContainerModeKey         = Config.HotkeyContainerMode.Value;
        SettingsModeKey          = Config.HotkeySettingsMode.Value;
        PlayerEditorModeKey      = Config.HotkeyPlayerEditorMode.Value;
        CharacterModeKeyNumpad   = NumpadFor(CharacterModeKey);
        ContainerModeKeyNumpad   = NumpadFor(ContainerModeKey);
        SettingsModeKeyNumpad    = NumpadFor(SettingsModeKey);
        PlayerEditorModeKeyNumpad = NumpadFor(PlayerEditorModeKey);
    }

    /// <summary>
    /// Alpha0~9 → Keypad0~9 매핑. 그 외 (F-키 / 알파벳 / 기능키) 는 KeyCode.None.
    /// 사용처: Bind() 의 Numpad pair 자동 derive. KeyCode.None 은 Shortcut 검사에서 skip.
    /// </summary>
    internal static KeyCode NumpadFor(KeyCode alpha) => alpha switch
    {
        KeyCode.Alpha0 => KeyCode.Keypad0,
        KeyCode.Alpha1 => KeyCode.Keypad1,
        KeyCode.Alpha2 => KeyCode.Keypad2,
        KeyCode.Alpha3 => KeyCode.Keypad3,
        KeyCode.Alpha4 => KeyCode.Keypad4,
        KeyCode.Alpha5 => KeyCode.Keypad5,
        KeyCode.Alpha6 => KeyCode.Keypad6,
        KeyCode.Alpha7 => KeyCode.Keypad7,
        KeyCode.Alpha8 => KeyCode.Keypad8,
        KeyCode.Alpha9 => KeyCode.Keypad9,
        _              => KeyCode.None,
    };

    /// <summary>F11 단독 눌림 (메뉴 토글). 동시 숫자키 없을 때만.</summary>
    public static bool MainKeyPressedAlone()
    {
        if (!Input.GetKeyDown(MainKey)) return false;
        return !(Input.GetKey(CharacterModeKey) || Input.GetKey(ContainerModeKey) || Input.GetKey(SettingsModeKey) || Input.GetKey(PlayerEditorModeKey)
              || (CharacterModeKeyNumpad   != KeyCode.None && Input.GetKey(CharacterModeKeyNumpad))
              || (ContainerModeKeyNumpad   != KeyCode.None && Input.GetKey(ContainerModeKeyNumpad))
              || (SettingsModeKeyNumpad    != KeyCode.None && Input.GetKey(SettingsModeKeyNumpad))
              || (PlayerEditorModeKeyNumpad != KeyCode.None && Input.GetKey(PlayerEditorModeKeyNumpad)));
    }

    /// <summary>F11+1 — 캐릭터 관리 직진입.</summary>
    public static bool CharacterShortcut()
    {
        return Input.GetKey(MainKey) &&
               (Input.GetKeyDown(CharacterModeKey) ||
                (CharacterModeKeyNumpad != KeyCode.None && Input.GetKeyDown(CharacterModeKeyNumpad)));
    }

    /// <summary>F11+2 — 컨테이너 관리 직진입.</summary>
    public static bool ContainerShortcut()
    {
        return Input.GetKey(MainKey) &&
               (Input.GetKeyDown(ContainerModeKey) ||
                (ContainerModeKeyNumpad != KeyCode.None && Input.GetKeyDown(ContainerModeKeyNumpad)));
    }

    /// <summary>F11+3 — 설정 panel 직진입. v0.7.6 신규.</summary>
    public static bool SettingsShortcut()
    {
        return Input.GetKey(MainKey) &&
               (Input.GetKeyDown(SettingsModeKey) ||
                (SettingsModeKeyNumpad != KeyCode.None && Input.GetKeyDown(SettingsModeKeyNumpad)));
    }

    /// <summary>F11+4 — 플레이어 편집 직진입. v0.7.8 신규.</summary>
    public static bool PlayerEditorShortcut()
    {
        return Input.GetKey(MainKey) &&
               (Input.GetKeyDown(PlayerEditorModeKey) ||
                (PlayerEditorModeKeyNumpad != KeyCode.None && Input.GetKeyDown(PlayerEditorModeKeyNumpad)));
    }
}
