// Minimal UnityEngine stubs so that ContainerPanel.cs (and its UI dependencies)
// compile in the test project without the real Unity runtime.
// Only members actually referenced by the included source files are stubbed.

#pragma warning disable CS0067  // unused events
#pragma warning disable CS8618  // non-nullable field not initialized

namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMin => x;
        public float yMin => y;
        public float xMax => x + width;
        public float yMax => y + height;
        public bool Contains(Vector2 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static readonly Vector2 zero = default;
    }

    public enum EventType { MouseDown, MouseUp, MouseMove, MouseDrag, KeyDown, KeyUp, Repaint, Layout, Used, Ignore }

    public class Event
    {
        public EventType type;
        public Vector2 mousePosition;
        public KeyCode keyCode;   // v0.7.6 — SettingsPanel 키 캡처
        public void Use() { }
        public static Event current { get; } = new Event();
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static readonly Color white = new(1, 1, 1, 1);
        public static readonly Color cyan  = new(0, 1, 1, 1);
    }

    public class Texture { }

    public class Texture2D : Texture
    {
        public static Texture2D whiteTexture { get; } = new Texture2D();
    }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }

    public class GUIStyle
    {
        public TextAnchor alignment { get; set; }
        public int fontSize { get; set; }
    }

    public class GUISkin
    {
        public GUIStyle label { get; } = new GUIStyle();
    }

    public static class GUI
    {
        public static Color color { get; set; }
        public static bool enabled { get; set; } = true;
        public static GUISkin skin { get; } = new GUISkin();

        public delegate void WindowFunction(int id);

        public static void DrawTexture(Rect position, Texture image) { }
        public static void Label(Rect position, string text) { }
        public static void Box(Rect position, string text) { }
        public static bool Button(Rect position, string text) => false;
        public static string TextField(string text, int maxLength = int.MaxValue) => text;
        public static bool Toggle(bool value, string text) => value;
        public static Rect Window(int id, Rect clientRect, WindowFunction func, string title) => clientRect;
        public static void DragWindow(Rect position) { }
    }

    public static class GUILayout
    {
        public static GUILayoutOption Width(float w) => new GUILayoutOption();
        public static GUILayoutOption Height(float h) => new GUILayoutOption();

        public static void Space(float pixels) { }
        public static void Label(string text) { }
        public static void Label(string text, params GUILayoutOption[] options) { }
        public static void Box(string text, params GUILayoutOption[] options) { }
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => value;
        public static string TextField(string text, params GUILayoutOption[] options) => text;
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => scrollPosition;
        public static void EndScrollView() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void BeginVertical(params GUILayoutOption[] options) { }
        public static void EndVertical() { }
        public static bool BeginScrollView(Vector2 pos, bool h, bool v, out Vector2 outPos, params GUILayoutOption[] options) { outPos = pos; return false; }
    }

    public class GUILayoutOption { }

    public static class GUILayoutUtility
    {
        public static Rect GetLastRect() => default;
        public static Rect GetRect(float width, float height, params GUILayoutOption[] options) => default;
    }

    public static class Time
    {
        public static float realtimeSinceStartup => 0f;
    }

    public static class Screen
    {
        public static int width  => 1920;
        public static int height => 1080;
    }

    // v0.7.6 — KeyCode subset (Config / HotkeyMap / SettingsPanel 에서 참조). Unity 실제 KeyCode 와
    // 숫자값 일치하지 않아도 OK — production code 는 enum 비교만 사용. NumpadFor switch / ==/!= 만 신경 씀.
    public enum KeyCode
    {
        None = 0,
        Backspace = 8, Tab = 9, Return = 13, Escape = 27, Space = 32,
        Alpha0 = 48, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,
        A = 97, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Keypad0 = 256, Keypad1, Keypad2, Keypad3, Keypad4, Keypad5, Keypad6, Keypad7, Keypad8, Keypad9,
        F1 = 282, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        LeftShift = 304, RightShift, LeftControl, RightControl, LeftAlt, RightAlt,
    }

    // v0.7.6 — Input static stub. 모든 method false 반환 (test 는 Bind/NumpadFor/buffer logic 만 검증).
    public static class Input
    {
        public static bool GetKey(KeyCode k) => false;
        public static bool GetKeyDown(KeyCode k) => false;
        public static bool GetKeyUp(KeyCode k) => false;
        public static Vector2 mousePosition => default;
        public static bool GetMouseButton(int button) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;
        public static float GetAxis(string axisName) => 0f;
    }
}
