using System;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;   // UnityEngine.Logger 모호성 회피

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.6 — Hybrid stateful-only 설정 panel.
/// hotkey 4 + ContainerPanel rect 4 buffer 편집 + [저장]/[기본값 복원]/[취소].
/// 자동 영속화 항목 (검색·정렬·필터·last container) 은 ContainerPanel 사용 중 immediate ConfigEntry write —
/// 본 panel 은 read-only 표시 + [영속화 정보 reset] 버튼만.
///
/// Task 2 (현재) — buffer / conflict / IsDirty / RestoreDefaults logic + Hydrate (Config 읽기).
/// Task 3 — OnGUI / Draw / 키 캡처 / 충돌 표시 / 버튼.
/// </summary>
public sealed class SettingsPanel
{
    // 자체 default (Config default 와 sync 유지 — 둘 다 변경 시 같이 갱신)
    internal const KeyCode DefaultMain      = KeyCode.F11;
    internal const KeyCode DefaultCharacter = KeyCode.Alpha1;
    internal const KeyCode DefaultContainer = KeyCode.Alpha2;
    internal const KeyCode DefaultSettings  = KeyCode.Alpha3;
    internal const float DefaultContainerX = 150f, DefaultContainerY = 100f;
    internal const float DefaultContainerW = 800f, DefaultContainerH = 760f;

    public bool Visible { get; set; } = false;
    public Rect WindowRect => _rect;
    private Rect _rect = new(200, 120, 480, 600);
    private const int WindowID = 0x4C593733;  // "LY73"

    // Buffer (저장 누르기 전까지 ConfigEntry 안 건드림)
    public KeyCode BufferMain        { get; private set; } = DefaultMain;
    public KeyCode BufferCharacter   { get; private set; } = DefaultCharacter;
    public KeyCode BufferContainer   { get; private set; } = DefaultContainer;
    public KeyCode BufferSettings    { get; private set; } = DefaultSettings;
    public float   BufferContainerX  { get; private set; } = DefaultContainerX;
    public float   BufferContainerY  { get; private set; } = DefaultContainerY;
    public float   BufferContainerW  { get; private set; } = DefaultContainerW;
    public float   BufferContainerH  { get; private set; } = DefaultContainerH;

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

    /// <summary>Production hydrate — Config 읽기. ModWindow.Awake / Settings transition 에서 호출.</summary>
    public void Hydrate()
    {
        HydrateFromValues(
            Config.ToggleHotkey.Value, Config.HotkeyCharacterMode.Value,
            Config.HotkeyContainerMode.Value, Config.HotkeySettingsMode.Value,
            Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
            Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
    }

    /// <summary>Test-only — Config 의존성 회피.</summary>
    internal void HydrateFromValues(
        KeyCode main, KeyCode ch, KeyCode co, KeyCode se,
        float x, float y, float w, float h)
    {
        BufferMain       = _origMain       = main;
        BufferCharacter  = _origCharacter  = ch;
        BufferContainer  = _origContainer  = co;
        BufferSettings   = _origSettings   = se;
        BufferContainerX = _origContainerX = x;
        BufferContainerY = _origContainerY = y;
        BufferContainerW = _origContainerW = w;
        BufferContainerH = _origContainerH = h;
        _hydrated = true;
        RecomputeConflict();
    }

    public void SetBufferMain(KeyCode k)      { BufferMain = k;      RecomputeConflict(); }
    public void SetBufferCharacter(KeyCode k) { BufferCharacter = k; RecomputeConflict(); }
    public void SetBufferContainer(KeyCode k) { BufferContainer = k; RecomputeConflict(); }
    public void SetBufferSettings(KeyCode k)  { BufferSettings = k;  RecomputeConflict(); }

    public void SetBufferContainerRect(float x, float y, float w, float h)
    {
        BufferContainerX = x; BufferContainerY = y;
        BufferContainerW = w; BufferContainerH = h;
    }

    public void DoRestoreDefaults()
    {
        BufferMain       = DefaultMain;
        BufferCharacter  = DefaultCharacter;
        BufferContainer  = DefaultContainer;
        BufferSettings   = DefaultSettings;
        BufferContainerX = DefaultContainerX;
        BufferContainerY = DefaultContainerY;
        BufferContainerW = DefaultContainerW;
        BufferContainerH = DefaultContainerH;
        RecomputeConflict();
    }

    /// <summary>Buffer → ConfigEntry. CanSave false 면 no-op. 호출 후 OnSaved 발화.</summary>
    public void DoSave()
    {
        if (!CanSave) return;
        Config.ToggleHotkey.Value         = BufferMain;
        Config.HotkeyCharacterMode.Value  = BufferCharacter;
        Config.HotkeyContainerMode.Value  = BufferContainer;
        Config.HotkeySettingsMode.Value   = BufferSettings;
        Config.ContainerPanelX.Value      = BufferContainerX;
        Config.ContainerPanelY.Value      = BufferContainerY;
        Config.ContainerPanelW.Value      = BufferContainerW;
        Config.ContainerPanelH.Value      = BufferContainerH;
        // _orig 갱신 (다시 dirty 안 보이도록)
        _origMain = BufferMain; _origCharacter = BufferCharacter;
        _origContainer = BufferContainer; _origSettings = BufferSettings;
        _origContainerX = BufferContainerX; _origContainerY = BufferContainerY;
        _origContainerW = BufferContainerW; _origContainerH = BufferContainerH;
        OnSaved?.Invoke();
    }

    /// <summary>자동 영속 항목 6개 + window rect 들 hardcoded default 로 즉시 reset.</summary>
    public void DoResetPersistedView()
    {
        Config.ContainerSortKey.Value        = "Category";
        Config.ContainerSortAscending.Value  = true;
        Config.ContainerFilterCategory.Value = "All";
        Config.ContainerLastIndex.Value      = -1;
        Config.WindowX.Value             = 1100f; Config.WindowY.Value = 100f;
        Config.WindowW.Value             = 720f;  Config.WindowH.Value = 560f;
        Config.ItemDetailPanelX.Value    = 970f;  Config.ItemDetailPanelY.Value = 100f;
        Config.ItemDetailPanelWidth.Value = 380f; Config.ItemDetailPanelHeight.Value = 500f;
        Config.ContainerPanelX.Value     = DefaultContainerX; Config.ContainerPanelY.Value = DefaultContainerY;
        Config.ContainerPanelW.Value     = DefaultContainerW; Config.ContainerPanelH.Value = DefaultContainerH;
        // ContainerPanel rect 도 buffer + orig 동기화 (사용자가 panel 닫고 재열 때 immediate 효과)
        BufferContainerX = _origContainerX = DefaultContainerX;
        BufferContainerY = _origContainerY = DefaultContainerY;
        BufferContainerW = _origContainerW = DefaultContainerW;
        BufferContainerH = _origContainerH = DefaultContainerH;
    }

    private void RecomputeConflict()
    {
        // 4 hotkey 중 2개 이상 같은 KeyCode → conflict.
        // KeyCode.None 은 무시 (할당 안 됨 의도).
        var keys   = new[] { BufferMain, BufferCharacter, BufferContainer, BufferSettings };
        var labels = new[] { "MainKey", "CharacterMode", "ContainerMode", "SettingsMode" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == KeyCode.None) continue;
            for (int j = i + 1; j < keys.Length; j++)
            {
                if (keys[j] == KeyCode.None) continue;
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

    // Task 3 — UI / 키 캡처 / textfield buffer

    private enum CaptureSlot { None, Main, Character, Container, Settings }
    private CaptureSlot _capture = CaptureSlot.None;

    private Vector2 _scroll = Vector2.zero;

    // Rect textfield buffer — IMGUI string 입력 → float parse
    private string _xBuf = "", _yBuf = "", _wBuf = "", _hBuf = "";
    private bool   _rectBufHydrated;

    public void OnGUI()
    {
        if (!Visible) return;
        if (!_hydrated) Hydrate();
        try
        {
            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("SettingsPanel", $"SettingsPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }

        // 키 캡처 — Event.current 는 OnGUI scope 안에서만 valid.
        // v0.7.6 Task 0 spike 결과 PASS 가정 (EventType.MouseDown 검증 패턴 mirror).
        // Strip 회귀 발견 시 fallback: ModWindow.Update 안 Input.GetKeyDown polling 으로 전환.
        if (_capture != CaptureSlot.None && Event.current != null && Event.current.type == EventType.KeyDown)
        {
            var k = Event.current.keyCode;
            if (k == KeyCode.Escape)
            {
                _capture = CaptureSlot.None;
            }
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

    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "설정");

            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
            {
                CloseAndDiscard();
            }

            GUILayout.Space(DialogStyle.HeaderHeight);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 110));

            GUILayout.Label("⚠ 고급 설정은 BepInExConfigManager (F5) 에서 변경");
            GUILayout.Space(8);

            // ──── 단축키 섹션 ────
            GUILayout.Label("▼ 단축키");
            DrawHotkeyRow("메인 토글:",     CaptureSlot.Main,      BufferMain);
            DrawHotkeyRow("캐릭터 관리:",   CaptureSlot.Character, BufferCharacter);
            DrawHotkeyRow("컨테이너 관리:", CaptureSlot.Container, BufferContainer);
            DrawHotkeyRow("설정 panel:",    CaptureSlot.Settings,  BufferSettings);

            if (HasConflict)
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.4f, 0.4f, 1f);
                GUILayout.Label(ConflictMessage);
                GUI.color = prev;
            }

            GUILayout.Space(10);

            // ──── 컨테이너 panel rect 섹션 ────
            GUILayout.Label("▼ 컨테이너 panel 위치/크기");
            DrawRectFields();

            GUILayout.Space(10);

            // ──── 영속화 정보 (read-only) ────
            GUILayout.Label("▼ 영속화 정보 (자동 저장)");
            DrawPersistedView();

            GUILayout.EndScrollView();

            // ──── 하단 버튼 (scrollview 밖 — 항상 보임) ────
            GUILayout.BeginHorizontal();
            var prevEnabled = GUI.enabled;
            GUI.enabled = CanSave;
            if (GUILayout.Button("저장", GUILayout.Height(28)))
            {
                DoSave();
                ToastService.Push("✔ 설정 저장됨", ToastKind.Success);
            }
            GUI.enabled = prevEnabled;
            if (GUILayout.Button("기본값 복원", GUILayout.Height(28)))
            {
                DoRestoreDefaults();
                _rectBufHydrated = false;   // textfield buffer 재hydrate
            }
            if (GUILayout.Button("취소", GUILayout.Height(28))) CloseAndDiscard();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("SettingsPanel", $"SettingsPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CloseAndDiscard()
    {
        Visible = false;
        _capture = CaptureSlot.None;
        _hydrated = false;        // 다음 진입 시 ConfigEntry 재hydrate
        _rectBufHydrated = false;
    }

    private void DrawHotkeyRow(string label, CaptureSlot slot, KeyCode current)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(120));
        var prev = GUI.color;
        if (_capture == slot) GUI.color = Color.cyan;
        string display = _capture == slot ? "키 입력 대기..." : current.ToString();
        GUILayout.Label($"[{display}]", GUILayout.Width(180));
        GUI.color = prev;
        if (GUILayout.Button("재설정", GUILayout.Width(80)))
        {
            _capture = slot;
        }
        GUILayout.EndHorizontal();
    }

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
        GUILayout.Space(8);
        GUILayout.Label("Y:", GUILayout.Width(20));
        _yBuf = GUILayout.TextField(_yBuf, GUILayout.Width(70));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("W:", GUILayout.Width(20));
        _wBuf = GUILayout.TextField(_wBuf, GUILayout.Width(70));
        GUILayout.Space(8);
        GUILayout.Label("H:", GUILayout.Width(20));
        _hBuf = GUILayout.TextField(_hBuf, GUILayout.Width(70));
        GUILayout.EndHorizontal();

        // textfield → buffer 동기화 (parse 실패 / 너무 작은 값 무시)
        float nx, ny, nw, nh;
        if (float.TryParse(_xBuf, out nx)) BufferContainerX = nx;
        if (float.TryParse(_yBuf, out ny)) BufferContainerY = ny;
        if (float.TryParse(_wBuf, out nw) && nw >= 100f) BufferContainerW = nw;
        if (float.TryParse(_hBuf, out nh) && nh >= 100f) BufferContainerH = nh;
    }

    private void DrawPersistedView()
    {
        string sortKr = Config.ContainerSortKey.Value switch
        {
            "Category" => "카테고리", "Name" => "이름",
            "Grade"    => "등급",     "Quality" => "품질",
            _          => Config.ContainerSortKey.Value
        };
        string arrow = Config.ContainerSortAscending.Value ? "▲" : "▼";
        GUILayout.Label($"정렬: {sortKr} {arrow}");
        GUILayout.Label($"필터: {Config.ContainerFilterCategory.Value}");

        int last = Config.ContainerLastIndex.Value;
        GUILayout.Label($"마지막 컨테이너: {(last > 0 ? "#" + last : "(미선택)")}");

        GUILayout.Label($"Mod 창: ({Config.WindowX.Value:F0}, {Config.WindowY.Value:F0}, {Config.WindowW.Value:F0}×{Config.WindowH.Value:F0})");
        GUILayout.Label($"ItemDetail: ({Config.ItemDetailPanelX.Value:F0}, {Config.ItemDetailPanelY.Value:F0}, {Config.ItemDetailPanelWidth.Value:F0}×{Config.ItemDetailPanelHeight.Value:F0})");
        GUILayout.Label($"ContainerPanel: ({Config.ContainerPanelX.Value:F0}, {Config.ContainerPanelY.Value:F0}, {Config.ContainerPanelW.Value:F0}×{Config.ContainerPanelH.Value:F0})");

        GUILayout.Space(4);
        if (GUILayout.Button("영속화 정보 reset", GUILayout.Width(140)))
        {
            DoResetPersistedView();
            _rectBufHydrated = false;
            ToastService.Push("✔ 영속화 정보 reset 됨", ToastKind.Success);
        }
    }
}
