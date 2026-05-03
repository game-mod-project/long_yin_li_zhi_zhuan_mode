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
    }

    public struct Vector2
    {
        public float x, y;
        public static readonly Vector2 zero = default;
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
}
