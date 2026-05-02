using UnityEngine;

namespace LongYinRoster.Util;

/// <summary>
/// F11 / F11+숫자 hotkey 처리. v0.7.0 부터 ModeSelector 가 사용.
/// 향후 settings panel 에서 hotkey 변경 시 정적 필드 갱신.
/// </summary>
public static class HotkeyMap
{
    public static KeyCode MainKey                = KeyCode.F11;
    public static KeyCode CharacterModeKey       = KeyCode.Alpha1;
    public static KeyCode ContainerModeKey       = KeyCode.Alpha2;
    public static KeyCode CharacterModeKeyNumpad = KeyCode.Keypad1;
    public static KeyCode ContainerModeKeyNumpad = KeyCode.Keypad2;

    /// <summary>F11 단독 눌림 (메뉴 토글). 동시 숫자키 없을 때만.</summary>
    public static bool MainKeyPressedAlone()
    {
        if (!Input.GetKeyDown(MainKey)) return false;
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
