using System;
using LongYinRoster.UI.Layout;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.6 — Hybrid stateful-only 설정 panel.
/// hotkey 4 + ContainerPanel rect 4 + (v0.8.0) SettingsPanel 자기 rect 4 buffer 편집 + [저장]/[기본값 복원]/[취소].
/// v0.8.0 — 창 틀은 PanelWindow(코너 리사이즈·설정 자동 저장), 내부 치수는 SettingsLayout.
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
    internal const float DefaultSelfX = 200f, DefaultSelfY = 120f;      // v0.8.0 — Config.SettingsPanel* 기본과 동일
    internal const float DefaultSelfW = 480f, DefaultSelfH = 600f;

    private const int WindowID = 0x4C593733;  // "LY73"

    public PanelWindow Window { get; }
    public bool Visible { get => Window.Visible; set => Window.Visible = value; }
    public Rect WindowRect => Window.Rect;

    public SettingsPanel()
    {
        Window = new PanelWindow(WindowID, "설정", () => SettingsLayout.MinSize,
            RectBinding.Of(Config.SettingsPanelX, Config.SettingsPanelY, Config.SettingsPanelW, Config.SettingsPanelH));
        Window.OnClosed = DiscardState;
    }

    // Buffer (저장 누르기 전까지 ConfigEntry 안 건드림)
    public KeyCode BufferMain        { get; private set; } = DefaultMain;
    public KeyCode BufferCharacter   { get; private set; } = DefaultCharacter;
    public KeyCode BufferContainer   { get; private set; } = DefaultContainer;
    public KeyCode BufferSettings    { get; private set; } = DefaultSettings;
    public float   BufferContainerX  { get; private set; } = DefaultContainerX;
    public float   BufferContainerY  { get; private set; } = DefaultContainerY;
    public float   BufferContainerW  { get; private set; } = DefaultContainerW;
    public float   BufferContainerH  { get; private set; } = DefaultContainerH;
    public float   BufferSelfX       { get; private set; } = DefaultSelfX;
    public float   BufferSelfY       { get; private set; } = DefaultSelfY;
    public float   BufferSelfW       { get; private set; } = DefaultSelfW;
    public float   BufferSelfH       { get; private set; } = DefaultSelfH;

    // Original (hydrate 시점) — IsDirty 비교용
    private KeyCode _origMain, _origCharacter, _origContainer, _origSettings;
    private float   _origContainerX, _origContainerY, _origContainerW, _origContainerH;
    private float   _origSelfX, _origSelfY, _origSelfW, _origSelfH;
    private bool    _hydrated;

    public Action? OnSaved;
    /// <summary>"영속화 정보 reset" 뒤 — ModWindow 가 PanelRegistry.ReloadRects 로 이관 패널 창을 즉시 따라가게 한다.</summary>
    public Action? OnPersistedViewReset;

    public bool HasConflict { get; private set; }
    public string ConflictMessage { get; private set; } = "";

    public bool IsDirty =>
        BufferMain != _origMain || BufferCharacter != _origCharacter
        || BufferContainer != _origContainer || BufferSettings != _origSettings
        || BufferContainerX != _origContainerX || BufferContainerY != _origContainerY
        || BufferContainerW != _origContainerW || BufferContainerH != _origContainerH
        || BufferSelfX != _origSelfX || BufferSelfY != _origSelfY
        || BufferSelfW != _origSelfW || BufferSelfH != _origSelfH;

    public bool CanSave => IsDirty && !HasConflict;
    internal bool IsSelfRectDirty => BufferSelfX != _origSelfX || BufferSelfY != _origSelfY || BufferSelfW != _origSelfW || BufferSelfH != _origSelfH;

    /// <summary>헤더 드래그/코너 리사이즈로 바뀐 창 rect 를 버퍼·_orig 에 반영 — 창 조작은 "편집" 이 아니므로 dirty 로 만들지 않는다.
    /// 사용자가 필드를 직접 고치는 중(IsSelfRectDirty)이면 덮어쓰지 않음. 안 하면 [저장] 이 방금 한 드래그를 되돌린다(리뷰 I-1).</summary>
    internal void SyncSelfRectFromWindow()
    {
        if (IsSelfRectDirty) return;
        var r = Window.Rect;
        if (r.x == _origSelfX && r.y == _origSelfY && r.width == _origSelfW && r.height == _origSelfH) return;
        BufferSelfX = _origSelfX = r.x;     BufferSelfY = _origSelfY = r.y;
        BufferSelfW = _origSelfW = r.width; BufferSelfH = _origSelfH = r.height;
        _rectBufHydrated = false;
    }

    /// <summary>Production hydrate — Config 읽기. ModWindow Settings transition 에서 호출.</summary>
    public void Hydrate()
    {
        // 창 rect 는 PanelRegistry.HydrateAll(Awake) 이 먼저 읽지만, 순서가 바뀌어도 안전하도록 가드
        if (!Window.IsHydrated) Window.Hydrate(Screen.width, Screen.height);
        HydrateFromValues(
            Config.ToggleHotkey.Value, Config.HotkeyCharacterMode.Value,
            Config.HotkeyContainerMode.Value, Config.HotkeySettingsMode.Value,
            Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
            Config.ContainerPanelW.Value, Config.ContainerPanelH.Value,
            Window.Rect.x, Window.Rect.y, Window.Rect.width, Window.Rect.height);
    }

    /// <summary>Test-only — Config 의존성 회피. 자기 rect 4개는 선택 인자(기존 테스트 호환).</summary>
    internal void HydrateFromValues(
        KeyCode main, KeyCode ch, KeyCode co, KeyCode se,
        float x, float y, float w, float h,
        float sx = DefaultSelfX, float sy = DefaultSelfY, float sw = DefaultSelfW, float sh = DefaultSelfH)
    {
        BufferMain       = _origMain       = main;
        BufferCharacter  = _origCharacter  = ch;
        BufferContainer  = _origContainer  = co;
        BufferSettings   = _origSettings   = se;
        BufferContainerX = _origContainerX = x;
        BufferContainerY = _origContainerY = y;
        BufferContainerW = _origContainerW = w;
        BufferContainerH = _origContainerH = h;
        BufferSelfX      = _origSelfX      = sx;
        BufferSelfY      = _origSelfY      = sy;
        BufferSelfW      = _origSelfW      = sw;
        BufferSelfH      = _origSelfH      = sh;
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

    public void SetBufferSelfRect(float x, float y, float w, float h)
    {
        BufferSelfX = x; BufferSelfY = y;
        BufferSelfW = w; BufferSelfH = h;
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
        BufferSelfX = DefaultSelfX; BufferSelfY = DefaultSelfY;
        BufferSelfW = DefaultSelfW; BufferSelfH = DefaultSelfH;
        RecomputeConflict();
    }

    /// <summary>Buffer → ConfigEntry. CanSave false 면 no-op. 자기 rect 는 Window.SetRect(클램프+저장). 호출 후 OnSaved 발화.</summary>
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
        // 자기 rect 는 필드로 고쳤을 때만 — 드래그/리사이즈로 바뀐 창을 옛 버퍼로 되돌리지 않는다
        if (IsSelfRectDirty) Window.SetRect(BufferSelfX, BufferSelfY, BufferSelfW, BufferSelfH);
        // 클램프된 실제 값을 버퍼에 반영
        BufferSelfX = Window.Rect.x; BufferSelfY = Window.Rect.y;
        BufferSelfW = Window.Rect.width; BufferSelfH = Window.Rect.height;
        // _orig 갱신 (다시 dirty 안 보이도록)
        _origMain = BufferMain; _origCharacter = BufferCharacter;
        _origContainer = BufferContainer; _origSettings = BufferSettings;
        _origContainerX = BufferContainerX; _origContainerY = BufferContainerY;
        _origContainerW = BufferContainerW; _origContainerH = BufferContainerH;
        _origSelfX = BufferSelfX; _origSelfY = BufferSelfY; _origSelfW = BufferSelfW; _origSelfH = BufferSelfH;
        _rectBufHydrated = false;
        OnSaved?.Invoke();
    }

    /// <summary>자동 영속 항목 + 6 패널 window rect 를 hardcoded default 로 즉시 reset (v0.8.0: PlayerEditor/ItemGen/Settings 추가).</summary>
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
        Config.PlayerEditorPanelX.Value  = 200f; Config.PlayerEditorPanelY.Value = 120f;
        Config.PlayerEditorPanelW.Value  = 720f; Config.PlayerEditorPanelH.Value = 720f;
        Config.ItemGenPanelX.Value       = 300f; Config.ItemGenPanelY.Value = 150f;
        Config.ItemGenPanelW.Value       = 620f; Config.ItemGenPanelH.Value = 560f;
        Window.SetRect(DefaultSelfX, DefaultSelfY, DefaultSelfW, DefaultSelfH);
        // buffer + orig 동기화 (사용자가 panel 닫고 재열 때 immediate 효과)
        BufferContainerX = _origContainerX = DefaultContainerX;
        BufferContainerY = _origContainerY = DefaultContainerY;
        BufferContainerW = _origContainerW = DefaultContainerW;
        BufferContainerH = _origContainerH = DefaultContainerH;
        BufferSelfX = _origSelfX = Window.Rect.x;  BufferSelfY = _origSelfY = Window.Rect.y;
        BufferSelfW = _origSelfW = Window.Rect.width; BufferSelfH = _origSelfH = Window.Rect.height;
        OnPersistedViewReset?.Invoke();
    }

    /// <summary>rect 텍스트 필드 파싱. 실패 또는 min 미만이면 false (무시).</summary>
    internal static bool TryParseRectField(string text, float min, out float value)
    {
        if (float.TryParse(text, out value) && value >= min) return true;
        value = 0f;
        return false;
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

    // ── UI / 키 캡처 / textfield buffer ──

    private enum CaptureSlot { None, Main, Character, Container, Settings }
    private CaptureSlot _capture = CaptureSlot.None;

    private Vector2 _scroll = Vector2.zero;

    // Rect textfield buffer — IMGUI string 입력 → float parse
    private string _xBuf = "", _yBuf = "", _wBuf = "", _hBuf = "";
    private string _sxBuf = "", _syBuf = "", _swBuf = "", _shBuf = "";
    private bool   _rectBufHydrated;

    public void OnGUI()
    {
        if (!Visible) return;
        if (!_hydrated) Hydrate();
        Window.OnGUI(DrawContent);

        // 키 캡처 — Event.current 는 OnGUI scope 안에서만 valid.
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

    private void DrawContent(Rect content)
    {
        SyncSelfRectFromWindow();
        var L = SettingsLayout.Compute(content);

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(L.ScrollH));

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
        HydrateRectBuffersIfNeeded();
        DrawRectPair("X:", ref _xBuf, "Y:", ref _yBuf, L.FieldW);
        DrawRectPair("W:", ref _wBuf, "H:", ref _hBuf, L.FieldW);
        if (TryParseRectField(_xBuf, float.MinValue, out var cx)) BufferContainerX = cx;
        if (TryParseRectField(_yBuf, float.MinValue, out var cy)) BufferContainerY = cy;
        if (TryParseRectField(_wBuf, ContainerLayout.MinSize.MinW, out var cw)) BufferContainerW = cw;
        if (TryParseRectField(_hBuf, ContainerLayout.MinSize.MinH, out var chh)) BufferContainerH = chh;

        GUILayout.Space(10);

        // ──── v0.8.0 설정 panel 자기 rect 섹션 ────
        GUILayout.Label("▼ 설정 panel 위치/크기 (코너 드래그로도 조절)");
        DrawRectPair("X:", ref _sxBuf, "Y:", ref _syBuf, L.FieldW);
        DrawRectPair("W:", ref _swBuf, "H:", ref _shBuf, L.FieldW);
        if (TryParseRectField(_sxBuf, float.MinValue, out var sx)) BufferSelfX = sx;
        if (TryParseRectField(_syBuf, float.MinValue, out var sy)) BufferSelfY = sy;
        if (TryParseRectField(_swBuf, SettingsLayout.MinSize.MinW, out var sw)) BufferSelfW = sw;
        if (TryParseRectField(_shBuf, SettingsLayout.MinSize.MinH, out var sh)) BufferSelfH = sh;

        GUILayout.Space(10);

        // ──── 영속화 정보 (read-only) ────
        GUILayout.Label("▼ 영속화 정보 (자동 저장)");
        DrawPersistedView();

        GUILayout.EndScrollView();

        // ──── 하단 버튼 (scrollview 밖 — 항상 보임) ────
        GUILayout.BeginHorizontal();
        var prevEnabled = GUI.enabled;
        GUI.enabled = CanSave;
        if (GUILayout.Button("저장", GUILayout.Height(DialogStyle.ButtonRowHeight)))
        {
            DoSave();
            ToastService.Push("✔ 설정 저장됨", ToastKind.Success);
        }
        GUI.enabled = prevEnabled;
        if (GUILayout.Button("기본값 복원", GUILayout.Height(DialogStyle.ButtonRowHeight)))
        {
            DoRestoreDefaults();
            _rectBufHydrated = false;   // textfield buffer 재hydrate
        }
        if (GUILayout.Button("취소", GUILayout.Height(DialogStyle.ButtonRowHeight))) Window.Close();
        GUILayout.EndHorizontal();
    }

    /// <summary>X / 취소 뒤 정리. Visible 은 PanelWindow 가 이미 false 로 둠.</summary>
    private void DiscardState()
    {
        _capture = CaptureSlot.None;
        _hydrated = false;        // 다음 진입 시 ConfigEntry 재hydrate
        _rectBufHydrated = false;
    }

    private void HydrateRectBuffersIfNeeded()
    {
        if (_rectBufHydrated) return;
        _xBuf = BufferContainerX.ToString("F0");
        _yBuf = BufferContainerY.ToString("F0");
        _wBuf = BufferContainerW.ToString("F0");
        _hBuf = BufferContainerH.ToString("F0");
        _sxBuf = BufferSelfX.ToString("F0");
        _syBuf = BufferSelfY.ToString("F0");
        _swBuf = BufferSelfW.ToString("F0");
        _shBuf = BufferSelfH.ToString("F0");
        _rectBufHydrated = true;
    }

    private void DrawHotkeyRow(string label, CaptureSlot slot, KeyCode current)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(SettingsLayout.HotkeyLabelW));
        var prev = GUI.color;
        if (_capture == slot) GUI.color = Color.cyan;
        string display = _capture == slot ? "키 입력 대기..." : current.ToString();
        GUILayout.Label($"[{display}]", GUILayout.Width(SettingsLayout.HotkeyDisplayW));
        GUI.color = prev;
        if (GUILayout.Button("재설정", GUILayout.Width(SettingsLayout.HotkeyButtonW)))
        {
            _capture = slot;
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>"X: [   ]   Y: [   ]" 한 줄. 필드 폭은 계산기가 준 값(가로 확장).</summary>
    private static void DrawRectPair(string labelA, ref string bufA, string labelB, ref string bufB, float fieldW)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(labelA, GUILayout.Width(SettingsLayout.RectLabelW));
        bufA = GUILayout.TextField(bufA, GUILayout.Width(fieldW));
        GUILayout.Space(SettingsLayout.RectPairSpace);
        GUILayout.Label(labelB, GUILayout.Width(SettingsLayout.RectLabelW));
        bufB = GUILayout.TextField(bufB, GUILayout.Width(fieldW));
        GUILayout.EndHorizontal();
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
        GUILayout.Label($"PlayerEditor: ({Config.PlayerEditorPanelX.Value:F0}, {Config.PlayerEditorPanelY.Value:F0}, {Config.PlayerEditorPanelW.Value:F0}×{Config.PlayerEditorPanelH.Value:F0})");
        GUILayout.Label($"ItemGen: ({Config.ItemGenPanelX.Value:F0}, {Config.ItemGenPanelY.Value:F0}, {Config.ItemGenPanelW.Value:F0}×{Config.ItemGenPanelH.Value:F0})");

        GUILayout.Space(4);
        if (GUILayout.Button("영속화 정보 reset", GUILayout.Width(140)))
        {
            DoResetPersistedView();
            _rectBufHydrated = false;
            ToastService.Push("✔ 영속화 정보 reset 됨", ToastKind.Success);
        }
    }
}
