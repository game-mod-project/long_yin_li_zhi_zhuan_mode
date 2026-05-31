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
    private int _pageSize = 10;

    private ItemGenCategory _category = ItemGenCategory.Equipment;
    private int _secondary = -1;
    private int _page = 0;
    private string _search = "";
    private ItemGenEntry? _selected = null;
    private int _itemLv = 5;    // 등급 (itemLv 0~5)
    private int _rareLv = 5;    // 품질 (rareLv 0~5)
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
            { _category = c; _secondary = -1; _page = 0; _selected = null; _search = ""; }
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
        var (slice, totalPages) = ItemGenFilter.Page(filtered, _page, _pageSize);
        // 데이터가 줄어든 경우 stale _page 를 보정 (PlayerEditorPanel 무공 list 패턴)
        if (_page >= totalPages) _page = totalPages - 1;
        if (_page < 0) _page = 0;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30)) && _page > 0) _page--;
        GUILayout.Label($"  {_page + 1} / {totalPages} ({filtered.Count}개)", GUILayout.Width(160));
        if (GUILayout.Button("▶", GUILayout.Width(30)) && _page < totalPages - 1) _page++;
        GUILayout.Label("표시:", GUILayout.Width(40));
        foreach (int ps in new[] { 10, 15, 20 })
        {
            var prevPs = GUI.color;
            if (_pageSize == ps) GUI.color = Color.cyan;
            if (GUILayout.Button(ps.ToString(), GUILayout.Width(34))) { _pageSize = ps; _page = 0; }
            GUI.color = prevPs;
        }
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
        foreach (var e in slice)
        {
            GUILayout.BeginHorizontal();
            var prev = GUI.color;
            if (ReferenceEquals(_selected, e)) GUI.color = Color.cyan;
            string mark = ReferenceEquals(_selected, e) ? "▶ " : "  ";
            if (GUILayout.Button($"{mark}{e.Display}", GUILayout.Width(_rect.width - 60))) _selected = e;
            GUI.color = prev;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawGenerateBar(object player)
    {
        // 등급 (itemLv) 선택
        GUILayout.BeginHorizontal();
        GUILayout.Label("등급:", GUILayout.Width(40));
        for (int i = 0; i < ItemRareLvNames.EquipLvNames.Length; i++)
        {
            var prev = GUI.color;
            if (_itemLv == i) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemRareLvNames.EquipLvNames[i], GUILayout.Width(48))) _itemLv = i;
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();

        // 품질 (rareLv) 선택
        GUILayout.BeginHorizontal();
        GUILayout.Label("품질:", GUILayout.Width(40));
        for (int i = 0; i < ItemRareLvNames.QualityNames.Length; i++)
        {
            var prev = GUI.color;
            if (_rareLv == i) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemRareLvNames.QualityNames[i], GUILayout.Width(48))) _rareLv = i;
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();

        // 수량 + 생성
        GUILayout.BeginHorizontal();
        GUILayout.Label("수량:", GUILayout.Width(40));
        _qtyBuf = GUILayout.TextField(_qtyBuf, GUILayout.Width(40));
        if (GUILayout.Button("생성", GUILayout.Width(80))) DoGenerate(player);
        GUILayout.EndHorizontal();
    }

    private void DoGenerate(object player)
    {
        if (_selected == null) { ToastService.Push("아이템을 선택하세요", ToastKind.Error); return; }
        int lv = _itemLv, rare = _rareLv, qty = Math.Clamp(ParseInt(_qtyBuf, 1), 1, 99);
        // 생성 조건은 선택한 entry 의 Category/SubType/Id 에서 — 현재 탭 상태가 아니라 (전체 탭에서도 정확).
        var res = ItemFactory.Generate(player, _selected.Category, _selected.SubType, _selected.Id, lv, rare, qty);
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
