using System.Collections.Generic;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

public sealed class Toast
{
    public string Message  = "";
    public ToastKind Kind  = ToastKind.Info;
    public float ExpireAt;
}

public enum ToastKind { Info, Success, Error }

public static class ToastService
{
    private static readonly List<Toast> _items = new();
    private const float DurationSec = 3f;

    public static void Push(string msg, ToastKind kind = ToastKind.Info)
    {
        _items.Add(new Toast
        {
            Message = msg,
            Kind = kind,
            ExpireAt = Time.realtimeSinceStartup + DurationSec,
        });
        Logger.Info($"[toast/{kind}] {msg}");
    }

    /// <summary>매 OnGUI 호출 시점에 그림. 만료된 항목 자동 제거.</summary>
    public static void Draw()
    {
        var now = Time.realtimeSinceStartup;
        _items.RemoveAll(t => t.ExpireAt < now);
        if (_items.Count == 0) return;

        const float w = 380f, h = 36f, margin = 12f, gap = 4f;
        float x = Screen.width - w - margin;
        float y = Screen.height - margin - h;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var t = _items[i];
            var rect = new Rect(x, y, w, h);
            var bg = t.Kind switch
            {
                ToastKind.Success => new Color(0.35f, 0.49f, 0.23f, 0.95f),
                ToastKind.Error   => new Color(0.49f, 0.23f, 0.23f, 0.95f),
                _                 => new Color(0.17f, 0.17f, 0.17f, 0.95f),
            };
            var prev = GUI.color;
            GUI.color = bg;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(x + 8, y + 8, w - 16, h - 16), t.Message);
            y -= (h + gap);
        }
    }
}
