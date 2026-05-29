using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// F11 메뉴 — 캐릭터 관리 / 컨테이너 관리 선택. 280x180 작은 창.
/// </summary>
public sealed class ModeSelector
{
    public enum Mode { None, Character, Container, Settings, Player, ItemGen }   // v0.7.13 — ItemGen 추가

    public Mode CurrentMode { get; private set; } = Mode.None;
    public bool MenuVisible { get; private set; } = false;
    public Rect WindowRect => _windowRect;

    private Rect _windowRect = new Rect(100, 100, 280, 320);   // v0.7.13 — 280 → 320 (ItemGen 버튼 자리)
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
        _windowRect = GUI.Window(WindowID, _windowRect, (GUI.WindowFunction)DrawWindow, "");
    }

    private void DrawWindow(int id)
    {
        DialogStyle.FillBackground(_windowRect.width, _windowRect.height);
        DialogStyle.DrawHeader(_windowRect.width, "LongYin Roster Mod");

        // 닫기 버튼 (창 우상단) — 헤더 높이 28 안에 배치
        if (GUI.Button(new Rect(_windowRect.width - 28, 4, 22, 20), "X"))
            MenuVisible = false;

        GUILayout.Space(DialogStyle.HeaderHeight + 4);
        if (GUILayout.Button("캐릭터 관리 (F11+1)", GUILayout.Height(32)))   SetMode(Mode.Character);
        GUILayout.Space(6);
        if (GUILayout.Button("컨테이너 관리 (F11+2)", GUILayout.Height(32))) SetMode(Mode.Container);
        GUILayout.Space(6);
        if (GUILayout.Button("설정 (F11+3)", GUILayout.Height(32)))           SetMode(Mode.Settings);
        GUILayout.Space(6);
        if (GUILayout.Button("플레이어 편집 (F11+4)", GUILayout.Height(32))) SetMode(Mode.Player);
        GUILayout.Space(6);
        if (GUILayout.Button("아이템 생성 (F11+5)", GUILayout.Height(32)))  SetMode(Mode.ItemGen);
        GUILayout.Space(10);
        GUILayout.Label("F11 닫기");
        GUI.DragWindow(new Rect(0, 0, _windowRect.width - 32, DialogStyle.HeaderHeight));
    }
}
