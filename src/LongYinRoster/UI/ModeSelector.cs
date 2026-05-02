using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// F11 메뉴 — 캐릭터 관리 / 컨테이너 관리 선택. 280x180 작은 창.
/// </summary>
public sealed class ModeSelector
{
    public enum Mode { None, Character, Container }

    public Mode CurrentMode { get; private set; } = Mode.None;
    public bool MenuVisible { get; private set; } = false;

    private Rect _windowRect = new Rect(100, 100, 280, 180);
    private const int WindowID = 0x4C593731;  // "LY71" ASCII unique

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
        _windowRect = GUI.Window(WindowID, _windowRect, (GUI.WindowFunction)DrawWindow, "LongYin Roster Mod");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Space(10);
        if (GUILayout.Button("캐릭터 관리 (F11+1)", GUILayout.Height(32)))   SetMode(Mode.Character);
        GUILayout.Space(6);
        if (GUILayout.Button("컨테이너 관리 (F11+2)", GUILayout.Height(32))) SetMode(Mode.Container);
        GUILayout.Space(10);
        GUILayout.Label("v0.7.0 — F11 닫기");
        GUI.DragWindow();
    }
}
