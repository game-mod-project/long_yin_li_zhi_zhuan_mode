using System;
using LongYinRoster.Core;
using LongYinRoster.UI.Layout;
using LongYinRoster.Util;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>v0.7.13 — 아이템 생성 패널 (F11 신규 항목). v0.8.0 — PanelWindow 창 틀 + ItemGenLayout(자동 페이지·탭 줄바꿈).</summary>
public sealed class ItemGeneratorPanel
{
    private const int WindowID = 0x4C593738;   // "LY78"

    public PanelWindow Window { get; }
    public bool Visible { get => Window.Visible; set => Window.Visible = value; }
    public Rect WindowRect => Window.Rect;
    public Func<object?>? GetPlayer;

    private ItemGenCategory _category = ItemGenCategory.Equipment;
    private int _secondary = -1;
    private int _page = 0;
    private string _search = "";
    private ItemGenEntry? _selected = null;
    private int _itemLv = 5;    // 등급 (itemLv 0~5)
    private int _rareLv = 5;    // 품질 (rareLv 0~5)
    private string _qtyBuf = "1";
    private Vector2 _scroll = Vector2.zero;

    public ItemGeneratorPanel()
    {
        Window = new PanelWindow(WindowID, "아이템 생성", () => ItemGenLayout.MinSize,
            RectBinding.Of(Config.ItemGenPanelX, Config.ItemGenPanelY, Config.ItemGenPanelW, Config.ItemGenPanelH,
                           Config.ItemGenPanelOpen));
    }

    public void OnGUI() => Window.OnGUI(DrawContent);

    private void DrawContent(Rect content)
    {
        GUI.enabled = true;
        var player = GetPlayer?.Invoke();
        if (player == null)
        {
            GUILayout.Label("  게임 로드 후 사용 가능");
            return;
        }

        var L = ItemGenLayout.Compute(content, hasSecondary: _category == ItemGenCategory.Equipment);
        DrawCategoryTabs(L);
        DrawSecondaryTabs(L);
        DrawSearch(L);
        DrawList(L);
        DrawGenerateBar(player);
    }

    private void DrawCategoryTabs(ItemGenLayout L)
    {
        var cats = (ItemGenCategory[])Enum.GetValues(typeof(ItemGenCategory));
        for (int i = 0; i < cats.Length; i++)
        {
            if (i % L.CategoryPerRow == 0) GUILayout.BeginHorizontal();
            var c = cats[i];
            var prev = GUI.color;
            if (_category == c) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemGenCategoryNames.Korean(c),
                    GUILayout.Width(ItemGenLayout.CategoryCellW), GUILayout.Height(DialogStyle.ButtonRowHeight)))
            { _category = c; _secondary = -1; _page = 0; _selected = null; _search = ""; }
            GUI.color = prev;
            if (i % L.CategoryPerRow == L.CategoryPerRow - 1 || i == cats.Length - 1) GUILayout.EndHorizontal();
        }
    }

    private void DrawSecondaryTabs(ItemGenLayout L)
    {
        if (_category != ItemGenCategory.Equipment) return;
        string[] labels = { "전체", "무기", "갑옷", "투구", "신발", "장신구", "마구" };   // index-1 = subType
        for (int i = 0; i < labels.Length; i++)
        {
            if (i % L.SecondaryPerRow == 0) GUILayout.BeginHorizontal();
            int sub = i - 1;
            var prev = GUI.color;
            if (_secondary == sub) GUI.color = Color.cyan;
            if (GUILayout.Button(labels[i],
                    GUILayout.Width(ItemGenLayout.SecondaryCellW), GUILayout.Height(DialogStyle.ButtonRowHeight)))
            { _secondary = sub; _page = 0; }
            GUI.color = prev;
            if (i % L.SecondaryPerRow == L.SecondaryPerRow - 1 || i == labels.Length - 1) GUILayout.EndHorizontal();
        }
    }

    private void DrawSearch(ItemGenLayout L)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("검색:", GUILayout.Width(ItemGenLayout.SearchLabelW), GUILayout.Height(DialogStyle.RowHeight));
        _search = GUILayout.TextField(_search ?? "", GUILayout.Width(L.SearchFieldW), GUILayout.Height(DialogStyle.RowHeight));
        GUILayout.EndHorizontal();
    }

    private void DrawList(ItemGenLayout L)
    {
        var filtered = ItemGenFilter.Apply(ItemDbCache.All(), _category, _secondary, _search);
        var (slice, totalPages) = ItemGenFilter.Page(filtered, _page, L.PageSize);
        // 데이터가 줄거나 페이지 크기가 커진 경우 stale _page 보정
        if (_page >= totalPages) _page = totalPages - 1;
        if (_page < 0) _page = 0;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(30), GUILayout.Height(DialogStyle.RowHeight)) && _page > 0) _page--;
        GUILayout.Label($"  {_page + 1} / {totalPages} ({filtered.Count}개, {L.PageSize}행)", GUILayout.Width(200), GUILayout.Height(DialogStyle.RowHeight));
        if (GUILayout.Button("▶", GUILayout.Width(30), GUILayout.Height(DialogStyle.RowHeight)) && _page < totalPages - 1) _page++;
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(L.ListH));
        foreach (var e in slice)
        {
            var prev = GUI.color;
            if (ReferenceEquals(_selected, e)) GUI.color = Color.cyan;
            string mark = ReferenceEquals(_selected, e) ? "▶ " : "  ";
            if (GUILayout.Button($"{mark}{e.Display}", GUILayout.Width(L.RowButtonW), GUILayout.Height(DialogStyle.RowHeight)))
                _selected = e;
            GUI.color = prev;
        }
        GUILayout.EndScrollView();
    }

    private void DrawGenerateBar(object player)
    {
        // 등급 (itemLv) 선택
        GUILayout.BeginHorizontal();
        GUILayout.Label("등급:", GUILayout.Width(ItemGenLayout.SearchLabelW));
        for (int i = 0; i < ItemRareLvNames.EquipLvNames.Length; i++)
        {
            var prev = GUI.color;
            if (_itemLv == i) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemRareLvNames.EquipLvNames[i], GUILayout.Width(ItemGenLayout.GradeCellW), GUILayout.Height(DialogStyle.ButtonRowHeight))) _itemLv = i;
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();

        // 품질 (rareLv) 선택
        GUILayout.BeginHorizontal();
        GUILayout.Label("품질:", GUILayout.Width(ItemGenLayout.SearchLabelW));
        for (int i = 0; i < ItemRareLvNames.QualityNames.Length; i++)
        {
            var prev = GUI.color;
            if (_rareLv == i) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemRareLvNames.QualityNames[i], GUILayout.Width(ItemGenLayout.GradeCellW), GUILayout.Height(DialogStyle.ButtonRowHeight))) _rareLv = i;
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();

        // 수량 + 생성
        GUILayout.BeginHorizontal();
        GUILayout.Label("수량:", GUILayout.Width(ItemGenLayout.SearchLabelW));
        _qtyBuf = GUILayout.TextField(_qtyBuf, GUILayout.Width(40), GUILayout.Height(DialogStyle.ButtonRowHeight));
        if (GUILayout.Button("생성", GUILayout.Width(80), GUILayout.Height(DialogStyle.ButtonRowHeight))) DoGenerate(player);
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
