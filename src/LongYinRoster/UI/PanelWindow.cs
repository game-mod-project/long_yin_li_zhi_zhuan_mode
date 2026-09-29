using System;
using BepInEx.Configuration;
using LongYinRoster.UI.Layout;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>rect ↔ ConfigEntry 4개(+ 선택적 Open). 테스트 환경에서는 null 엔트리일 수 있어 IsBound 로 가드.</summary>
public sealed class RectBinding
{
    public ConfigEntry<float> X { get; private set; } = null!;
    public ConfigEntry<float> Y { get; private set; } = null!;
    public ConfigEntry<float> W { get; private set; } = null!;
    public ConfigEntry<float> H { get; private set; } = null!;
    public ConfigEntry<bool>? Open { get; private set; }

    public bool IsBound => X != null && Y != null && W != null && H != null;

    public static RectBinding Of(ConfigEntry<float> x, ConfigEntry<float> y, ConfigEntry<float> w, ConfigEntry<float> h,
                                 ConfigEntry<bool>? open = null)
        => new() { X = x, Y = y, W = w, H = h, Open = open };
}

/// <summary>
/// v0.8.0 — 여섯 패널이 공유하는 창 틀. GUI.Window · 배경/헤더/X · 헤더 드래그 · 코너 리사이즈 ·
/// 최소/화면 클램프 · rect ↔ 설정 저장. 순수 수식은 PanelWindowLogic, 여기는 IMGUI 만.
/// 사용: 패널이 인스턴스를 들고 OnGUI(drawContent) 호출. drawContent 는 ContentRect(창 로컬)를 받는다.
/// </summary>
public sealed class PanelWindow
{
    private readonly int _id;
    private readonly string _title;
    private readonly Func<PanelBounds> _bounds;
    private readonly RectBinding _binding;

    private Rect _rect;
    private bool _hydrated;
    private Action<Rect>? _drawContent;

    // 코너 리사이즈 (ContainerPanel v0.7.11 에서 승격)
    private bool    _resizing;
    private Vector2 _resizeStart;
    private Vector2 _resizeStartSize;
    private bool    _mouseUpSeen;
    private Rect    _lastPersisted;
    private Rect    _lastSeenRect;      // 직전 OnGUI 호출의 rect (settle 검출용)
    private bool    _pendingSettle;     // rect 가 바뀐 뒤 아직 저장 안 됨

    public PanelWindow(int windowId, string title, Func<PanelBounds> bounds, RectBinding binding)
    {
        _id = windowId; _title = title; _bounds = bounds; _binding = binding;
        var b = bounds();
        _rect = new Rect(0f, 0f, b.MinW, b.MinH);
    }

    public Rect Rect => _rect;
    public Rect ContentRect => PanelWindowLogic.ContentRect(_rect, DialogStyle.HeaderHeight, DialogStyle.Padding);
    public bool IsHydrated => _hydrated;
    private bool _visible;
    /// <summary>표시 여부. 바뀔 때 Open 엔트리에 즉시 반영 — ModWindow 모드 전환이 직접 세팅하는 경로도 영속화(v0.7.13 per-frame 저장 대체).
    /// BepInEx ConfigEntry 는 값이 같으면 저장하지 않으므로 매 프레임 대입해도 파일 I/O 없음.</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            if (_binding.IsBound && _binding.Open != null) _binding.Open.Value = value;
        }
    }
    /// <summary>X 버튼/Close() 뒤 패널이 부가 정리(버퍼 폐기 등)를 할 때.</summary>
    public Action? OnClosed;

    /// <summary>설정 → rect (화면 클램프). Open 엔트리가 있으면 Visible 도 읽는다. 미바인딩이면 최소 크기 유지.
    /// 화면 크기가 아직 0×0 이면(초기 프레임) 클램프가 저장값을 (0,0,최소) 로 부숴 버리므로 클램프 없이 읽고 hydrated 를 세우지 않는다 — 첫 OnGUI 가 재시도.</summary>
    public void Hydrate(float screenW, float screenH)
    {
        bool screenValid = screenW > 0f && screenH > 0f;
        if (_binding.IsBound)
        {
            var r = new Rect(_binding.X.Value, _binding.Y.Value, _binding.W.Value, _binding.H.Value);
            _rect = screenValid ? PanelWindowLogic.ClampToScreen(r, screenW, screenH, _bounds()) : r;
            if (_binding.Open != null) Visible = _binding.Open.Value;
        }
        else if (screenValid)
        {
            _rect = PanelWindowLogic.ClampToScreen(_rect, screenW, screenH, _bounds());
        }
        _lastPersisted = _rect;
        _lastSeenRect  = _rect;
        _hydrated = screenValid;
    }

    /// <summary>설정 → rect 만 다시 읽는다(Visible/Open 불변). "영속화 정보 reset" 이 Config 를 바꾼 뒤 창이 즉시 따라가도록 — 안 하면 다음 Persist 가 옛 rect 로 되덮는다.</summary>
    public void ReloadRect(float screenW, float screenH)
    {
        if (!_binding.IsBound) return;
        var r = new Rect(_binding.X.Value, _binding.Y.Value, _binding.W.Value, _binding.H.Value);
        _rect = (screenW > 0f && screenH > 0f) ? PanelWindowLogic.ClampToScreen(r, screenW, screenH, _bounds()) : r;
        _lastPersisted = _rect;
        _lastSeenRect  = _rect;
        _resizing = false;
    }

    /// <summary>설정 패널 숫자 입력 경로. 클램프 후 즉시 저장.</summary>
    public void SetRect(float x, float y, float w, float h)
    {
        _rect = PanelWindowLogic.ClampToScreen(new Rect(x, y, w, h), Screen.width, Screen.height, _bounds());
        Persist();
    }

    /// <summary>rect(+Visible) → 설정. BepInEx 가 값이 바뀔 때만 파일에 쓴다.</summary>
    public void Persist()
    {
        if (!_binding.IsBound) return;
        _binding.X.Value = _rect.x;
        _binding.Y.Value = _rect.y;
        _binding.W.Value = _rect.width;
        _binding.H.Value = _rect.height;
        if (_binding.Open != null) _binding.Open.Value = Visible;
        _lastPersisted = _rect;
    }

    public void Close()
    {
        Visible = false;
        Persist();
        OnClosed?.Invoke();
    }

    /// <summary>매 프레임. Visible 아니면 아무것도 안 함. 순서: 배경 → 헤더 → X → 내용 → 코너 핸들 → DragWindow.</summary>
    public void OnGUI(Action<Rect> drawContent)
    {
        if (!Visible) return;
        if (!_hydrated) Hydrate(Screen.width, Screen.height);
        _drawContent = drawContent;
        HandleResizeEvents();   // GUI.Window 앞: 창 밖으로 나간 드래그도 받고, 스크롤바·버튼보다 먼저 핸들이 이벤트를 잡는다
        try
        {
            _rect = GUI.Window(_id, _rect, (GUI.WindowFunction)DrawWindow, "");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce($"PanelWindow/{_title}/OnGUI", $"{_title} OnGUI: {ex.GetType().Name}: {ex.Message}");
        }
        // 헤더 드래그 종료 검출 — 두 경로:
        //  (a) 콜백에서 MouseUp 을 봤다(정상 종료)
        //  (b) rect 가 바뀐 뒤 이번 호출에서 더 이상 안 바뀐다(게임 창 밖에서 마우스를 놓아 MouseUp 을 못 본 경우)
        // 어느 쪽이든 마지막 저장값과 다르면 화면 클램프 후 저장. (b) 는 드래그 중 잠깐 멈춰도 발동하지만
        // 클램프는 멱등이고 저장은 값이 바뀔 때만 파일에 쓴다(기존 per-frame 저장과 같은 비용 상한).
        bool changed = !SameRect(_rect, _lastSeenRect);
        if (_mouseUpSeen || (_pendingSettle && !changed))
        {
            _mouseUpSeen = false;
            _pendingSettle = false;
            if (!SameRect(_rect, _lastPersisted))
            {
                _rect = PanelWindowLogic.ClampToScreen(_rect, Screen.width, Screen.height, _bounds());
                Persist();
            }
        }
        else if (changed)
        {
            _pendingSettle = true;
        }
        _lastSeenRect = _rect;
    }

    private void DrawWindow(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, _title);
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X")) { Close(); return; }

            var e = Event.current;
            if (e != null && e.type == EventType.MouseUp) _mouseUpSeen = true;

            GUILayout.Space(DialogStyle.HeaderHeight + DialogStyle.Padding);
            _drawContent?.Invoke(ContentRect);

            DrawResizeHandle();
            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce($"PanelWindow/{_title}/Draw", $"{_title} Draw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>코너 리사이즈 이벤트 — 화면 좌표. 콜백 안(창 로컬)에서 처리하면 창이 마우스보다 느리게 커질 때
    /// 마우스가 창 밖으로 나가 MouseDrag 를 못 받는다(2026-09-29 smoke: 늘리기가 자꾸 끊김·최소 크기에서 늘리기 불가).</summary>
    private void HandleResizeEvents()
    {
        var e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseDown)
        {
            if (!PanelWindowLogic.ResizeHandleRect(_rect).Contains(e.mousePosition)) return;
            _resizing        = true;
            _resizeStart     = e.mousePosition;
            _resizeStartSize = new Vector2(_rect.width, _rect.height);
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseDrag)
        {
            _rect = PanelWindowLogic.Resize(_rect, _resizeStart, _resizeStartSize, e.mousePosition,
                                            _bounds(), Screen.width, Screen.height);
            // e.Use() 필수: 리사이즈로 PageSize 등이 바뀌어 같은 패스의 GUILayout 컨트롤 수가 Layout 캐시와 달라져도,
            // 이벤트가 Used 면 GUILayoutUtility 가 더미 rect 를 돌려줘 "Getting control N position" ArgumentException 이 나지 않는다.
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseUp)
        {
            _resizing = false;
            Persist();
            e.Use();
        }
    }

    /// <summary>핸들 그리기만(창 로컬 좌표). 이벤트는 HandleResizeEvents.</summary>
    private void DrawResizeHandle()
    {
        float s = PanelWindowLogic.HandleSize;
        var handleRect = new Rect(_rect.width - s, _rect.height - s, s, s);
        var prev = GUI.color;
        GUI.color = _resizing ? new Color(0.9f, 0.9f, 0.9f, 0.9f) : new Color(0.6f, 0.6f, 0.6f, 0.8f);
        GUI.DrawTexture(handleRect, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static bool SameRect(Rect a, Rect b)
        => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
}
