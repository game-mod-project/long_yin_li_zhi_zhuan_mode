using System;
using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>v0.7.13 — 아이템 생성 패널 (F11 신규 항목). PlayerEditorPanel 무공 list 패턴 mirror.</summary>
public sealed class ItemGeneratorPanel
{
    public bool Visible { get; set; } = false;
    public Rect WindowRect => _rect;
    public Func<object?>? GetPlayer;

    private Rect _rect = new(300, 150, 620, 560);
    private const int WindowID = 0x4C593738;   // "LY78"
    private const int PAGE_SIZE = 10;

    private ItemGenCategory _category = ItemGenCategory.Equipment;
    private int _secondary = -1;
    private int _page = 0;
    private string _search = "";
    private int _selectedId = -1;
    private string _lvBuf = "5";
    private string _rareBuf = "5";
    private string _qtyBuf = "1";
    private Vector2 _scroll = Vector2.zero;

    public void Init(float x, float y, float w, float h)
    {
        _rect = new Rect(x, y, Math.Max(w, 620f), Math.Max(h, 560f));
    }

    public void OnGUI()
    {
        if (!Visible) return;
        try { _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, ""); }
        catch (Exception ex) { Logger.WarnOnce("ItemGenPanel", $"OnGUI: {ex.GetType().Name}: {ex.Message}"); }
    }

    private void Draw(int id)
    {
        try
        {
            GUI.enabled = true;
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "아이템 생성");
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X")) { Visible = false; return; }
            GUILayout.Space(DialogStyle.HeaderHeight);

            var player = GetPlayer?.Invoke();
            if (player == null)
            {
                GUILayout.Label("  게임 로드 후 사용 가능");
                GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
                return;
            }

            DrawCategoryTabs();
            DrawSecondaryTabs();
            DrawSearch();
            DrawList();
            DrawGenerateBar(player);

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex) { Logger.WarnOnce("ItemGenPanel", $"Draw: {ex.GetType().Name}: {ex.Message}"); }
    }

    private void DrawCategoryTabs()
    {
        GUILayout.BeginHorizontal();
        foreach (ItemGenCategory c in Enum.GetValues(typeof(ItemGenCategory)))
        {
            var prev = GUI.color;
            if (_category == c) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemGenCategoryNames.Korean(c), GUILayout.Width(55)))
            { _category = c; _secondary = -1; _page = 0; _selectedId = -1; }
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSecondaryTabs()
    {
        if (_category != ItemGenCategory.Equipment) return;
        string[] labels = { "무기", "갑옷", "투구", "신발", "장신구", "마구" };
        GUILayout.BeginHorizontal();
        var p0 = GUI.color; if (_secondary == -1) GUI.color = Color.cyan;
        if (GUILayout.Button("전체", GUILayout.Width(45))) { _secondary = -1; _page = 0; }
        GUI.color = p0;
        for (int i = 0; i < labels.Length; i++)
        {
            var prev = GUI.color; if (_secondary == i) GUI.color = Color.cyan;
            if (GUILayout.Button(labels[i], GUILayout.Width(45))) { _secondary = i; _page = 0; }
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSearch()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("검색:", GUILayout.Width(40));
        _search = GUILayout.TextField(_search ?? "", GUILayout.Width(_rect.width - 80));
        GUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        var filtered = ItemGenFilter.Apply(ItemDbCache.All(), _category, _secondary, _search);
        var (slice, totalPages) = ItemGenFilter.Page(filtered, _page, PAGE_SIZE);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30)) && _page > 0) _page--;
        GUILayout.Label($"  {_page + 1} / {totalPages} ({filtered.Count}개)", GUILayout.Width(160));
        if (GUILayout.Button("▶", GUILayout.Width(30)) && _page < totalPages - 1) _page++;
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
        foreach (var e in slice)
        {
            GUILayout.BeginHorizontal();
            var prev = GUI.color;
            if (_selectedId == e.Id) GUI.color = Color.cyan;
            string mark = _selectedId == e.Id ? "▶ " : "  ";
            if (GUILayout.Button($"{mark}{e.Display}", GUILayout.Width(_rect.width - 60))) _selectedId = e.Id;
            GUI.color = prev;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawGenerateBar(object player)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("레벨:", GUILayout.Width(40));
        _lvBuf = GUILayout.TextField(_lvBuf, GUILayout.Width(40));
        GUILayout.Label("등급:", GUILayout.Width(40));
        _rareBuf = GUILayout.TextField(_rareBuf, GUILayout.Width(40));
        GUILayout.Label("수량:", GUILayout.Width(40));
        _qtyBuf = GUILayout.TextField(_qtyBuf, GUILayout.Width(40));
        if (GUILayout.Button("생성", GUILayout.Width(80))) DoGenerate(player);
        GUILayout.EndHorizontal();
    }

    private void DoGenerate(object player)
    {
        if (_selectedId < 0) { ToastService.Push("아이템을 선택하세요", ToastKind.Error); return; }
        int lv = ParseInt(_lvBuf, 5), rare = ParseInt(_rareBuf, 5), qty = ParseInt(_qtyBuf, 1);
        int subType = _category == ItemGenCategory.Equipment ? Math.Max(0, _secondary) : 0;
        var res = ItemFactory.Generate(player, _category, subType, _selectedId, lv, rare, qty);
        if (res.Ok)
        {
            ToastService.Push($"✓ {res.ItemName ?? "아이템"} ×{res.Created} 생성됨", ToastKind.Success);
            WarnIfOverweight(player);
        }
        else
        {
            ToastService.Push($"✘ 생성 실패: {res.Reason}", ToastKind.Error);
        }
    }

    // spec §7 — 생성 후 인벤 무게 초과 시 경고 (인벤은 over-weight 허용, 속도 페널티)
    private void WarnIfOverweight(object player)
    {
        try
        {
            const System.Reflection.BindingFlags F =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var ild = player.GetType().GetProperty("itemListData", F)?.GetValue(player)
                      ?? player.GetType().GetField("itemListData", F)?.GetValue(player);
            if (ild == null) return;
            float max = ItemListReflector.GetMaxWeight(ild, 964f);
            var wp = ild.GetType().GetProperty("weight", F);
            float cur = wp != null ? Convert.ToSingle(wp.GetValue(ild)) : 0f;
            if (cur > max) ToastService.Push($"⚠ 인벤 무게 초과 ({cur:F0}/{max:F0}kg) — 속도 페널티", ToastKind.Info);
        }
        catch { }
    }

    private static int ParseInt(string s, int def) => int.TryParse(s, out var v) ? v : def;
}
