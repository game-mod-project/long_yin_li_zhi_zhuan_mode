using System;
using System.Collections.Generic;
using LongYinRoster.Containers;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// 컨테이너 관리 IMGUI panel. 800x600. 좌측 인벤토리/창고 + 우측 컨테이너.
/// 글로벌 카테고리 탭 (전체/장비/단약/음식/비급/보물/재료/말).
/// 호스트 (Plugin.cs) 가 callback + row source 주입.
/// </summary>
public sealed class ContainerPanel
{
    public sealed class ItemRow
    {
        public int     Index     { get; init; }
        public string  Name      { get; init; } = "";
        public int     Type      { get; init; }
        public int     SubType   { get; init; }
        public int     EnhanceLv { get; init; }
        public float   Weight    { get; init; }
        public bool    Equipped  { get; init; }
    }

    public bool Visible { get; set; } = false;
    private Rect _rect = new Rect(150, 100, 800, 600);
    private const int WindowID = 0x4C593732;  // "LY72"

    private ItemCategory _filter = ItemCategory.All;
    private static readonly ItemCategory[] TabOrder = {
        ItemCategory.All, ItemCategory.Equipment, ItemCategory.Medicine,
        ItemCategory.Food, ItemCategory.Book, ItemCategory.Treasure,
        ItemCategory.Material, ItemCategory.Horse,
    };

    private List<ItemRow> _inventoryRows = new();
    private List<ItemRow> _storageRows   = new();
    private List<ItemRow> _containerRows = new();
    private HashSet<int>  _inventoryChecks = new();
    private HashSet<int>  _storageChecks   = new();
    private HashSet<int>  _containerChecks = new();

    private Vector2 _invScroll = Vector2.zero;
    private Vector2 _stoScroll = Vector2.zero;
    private Vector2 _conScroll = Vector2.zero;

    // 컨테이너 선택 / 신규 / 이름변경
    private ContainerRepository? _repo;
    private List<ContainerMetadata> _containerList = new();
    private int    _selectedContainerIdx = -1;
    private bool   _dropdownOpen = false;
    private string _renameBuffer = "";
    private bool   _renameMode = false;
    private string _newNameBuffer = "";
    private bool   _newMode = false;
    private string _toastMsg = "";
    private float  _toastUntil = 0f;

    // 호스트 callback 들 (Plugin.cs 가 wire)
    public Action<int>? OnContainerSelected;
    public Action<HashSet<int>>? OnInventoryToContainerMove;
    public Action<HashSet<int>>? OnInventoryToContainerCopy;
    public Action<HashSet<int>>? OnStorageToContainerMove;
    public Action<HashSet<int>>? OnStorageToContainerCopy;
    public Action<HashSet<int>>? OnContainerToInventoryMove;
    public Action<HashSet<int>>? OnContainerToInventoryCopy;
    public Action<HashSet<int>>? OnContainerDelete;
    public Action? OnRequestRefresh;  // 호스트에 row 갱신 요청

    public void SetRepository(ContainerRepository repo)
    {
        _repo = repo;
        RefreshContainerList();
    }

    public void SetInventoryRows(List<ItemRow> rows) { _inventoryRows = rows; _inventoryChecks.Clear(); }
    public void SetStorageRows(List<ItemRow> rows)   { _storageRows = rows; _storageChecks.Clear(); }
    public void SetContainerRows(List<ItemRow> rows) { _containerRows = rows; _containerChecks.Clear(); }

    public void Toast(string msg, float duration = 3.0f)
    {
        _toastMsg = msg;
        _toastUntil = Time.realtimeSinceStartup + duration;
    }

    public int SelectedContainerIndex => _selectedContainerIdx;

    private void RefreshContainerList()
    {
        if (_repo == null) return;
        _containerList = _repo.List();
        if (_selectedContainerIdx < 0 && _containerList.Count > 0)
            _selectedContainerIdx = _containerList[0].ContainerIndex;
    }

    public void OnGUI()
    {
        if (!Visible) return;
        _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "컨테이너 관리");
    }

    private void Draw(int id)
    {
        DrawCategoryTabs();
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        DrawLeftColumn();
        GUILayout.Space(4);
        DrawRightColumn();
        GUILayout.EndHorizontal();
        DrawToast();
        GUI.DragWindow(new Rect(0, 0, _rect.width, 20));
    }

    private void DrawCategoryTabs()
    {
        GUILayout.BeginHorizontal();
        foreach (var cat in TabOrder)
        {
            bool active = _filter == cat;
            var prevColor = GUI.color;
            if (active) GUI.color = Color.cyan;
            if (GUILayout.Button(ItemCategoryFilter.KoreanLabel(cat), GUILayout.Width(70)))
                _filter = cat;
            GUI.color = prevColor;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawLeftColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));

        GUILayout.Label($"인벤토리 ({_inventoryRows.Count}개)");
        DrawItemList(_inventoryRows, _inventoryChecks, ref _invScroll, 220);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnInventoryToContainerMove?.Invoke(new HashSet<int>(_inventoryChecks));
        if (GUILayout.Button("→ 복사")) OnInventoryToContainerCopy?.Invoke(new HashSet<int>(_inventoryChecks));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);
        GUILayout.Label($"창고 ({_storageRows.Count}개)");
        DrawItemList(_storageRows, _storageChecks, ref _stoScroll, 220);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnStorageToContainerMove?.Invoke(new HashSet<int>(_storageChecks));
        if (GUILayout.Button("→ 복사")) OnStorageToContainerCopy?.Invoke(new HashSet<int>(_storageChecks));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawRightColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));

        GUILayout.BeginHorizontal();
        string sel = _selectedContainerIdx > 0
            ? _containerList.Find(c => c.ContainerIndex == _selectedContainerIdx)?.ContainerName ?? "(미선택)"
            : "(미선택)";
        if (GUILayout.Button($"[{sel} ▼]", GUILayout.Width(150)))
            _dropdownOpen = !_dropdownOpen;
        if (GUILayout.Button("신규", GUILayout.Width(45))) { _newMode = true; _newNameBuffer = ""; _dropdownOpen = false; }
        if (GUILayout.Button("이름변경", GUILayout.Width(60)))
        {
            if (_selectedContainerIdx > 0)
            {
                _renameMode = true;
                _renameBuffer = _containerList.Find(c => c.ContainerIndex == _selectedContainerIdx)?.ContainerName ?? "";
                _dropdownOpen = false;
            }
        }
        if (GUILayout.Button("삭제", GUILayout.Width(45)))
        {
            if (_repo != null && _selectedContainerIdx > 0)
            {
                _repo.Delete(_selectedContainerIdx);
                _selectedContainerIdx = -1;
                RefreshContainerList();
                OnContainerSelected?.Invoke(-1);
                Toast("컨테이너 삭제됨");
            }
        }
        GUILayout.EndHorizontal();

        if (_dropdownOpen)
        {
            foreach (var m in _containerList)
            {
                if (GUILayout.Button($"{m.ContainerIndex:D2}: {m.ContainerName}"))
                {
                    _selectedContainerIdx = m.ContainerIndex;
                    _dropdownOpen = false;
                    OnContainerSelected?.Invoke(m.ContainerIndex);
                }
            }
        }

        if (_newMode)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("이름:");
            _newNameBuffer = GUILayout.TextField(_newNameBuffer, GUILayout.Width(180));
            if (GUILayout.Button("확인", GUILayout.Width(45)) && _repo != null)
            {
                int idx = _repo.CreateNew(string.IsNullOrWhiteSpace(_newNameBuffer)
                    ? $"컨테이너{System.DateTime.Now:HHmmss}" : _newNameBuffer);
                _newMode = false;
                RefreshContainerList();
                _selectedContainerIdx = idx;
                OnContainerSelected?.Invoke(idx);
                Toast($"신규 컨테이너 #{idx} 생성");
            }
            if (GUILayout.Button("취소", GUILayout.Width(45))) _newMode = false;
            GUILayout.EndHorizontal();
        }

        if (_renameMode)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("새 이름:");
            _renameBuffer = GUILayout.TextField(_renameBuffer, GUILayout.Width(180));
            if (GUILayout.Button("확인", GUILayout.Width(45)) && _repo != null && _selectedContainerIdx > 0)
            {
                _repo.Rename(_selectedContainerIdx, _renameBuffer);
                _renameMode = false;
                RefreshContainerList();
                Toast("이름 변경 완료");
            }
            if (GUILayout.Button("취소", GUILayout.Width(45))) _renameMode = false;
            GUILayout.EndHorizontal();
        }

        GUILayout.Label($"컨테이너 ({_containerRows.Count}개)");
        DrawItemList(_containerRows, _containerChecks, ref _conScroll, 420);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("← 이동")) OnContainerToInventoryMove?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button("← 복사")) OnContainerToInventoryCopy?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button("☓ 삭제")) OnContainerDelete?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawItemList(List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
    {
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
        foreach (var r in rows)
        {
            if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;
            bool was = checks.Contains(r.Index);
            bool now = GUILayout.Toggle(was, BuildLabel(r));
            if (now && !was) checks.Add(r.Index);
            if (!now && was) checks.Remove(r.Index);
        }
        GUILayout.EndScrollView();
    }

    private static string BuildLabel(ItemRow r)
    {
        string cat = ItemCategoryFilter.KoreanLabel(ItemCategoryFilter.Classify(r.Type, r.SubType));
        string enh = r.EnhanceLv > 0 ? $"/강화{r.EnhanceLv}" : "";
        string equipped = r.Equipped ? " [착용중]" : "";
        return $"{r.Name} ({cat}{enh}/{r.Weight:F1}kg){equipped}";
    }

    private void DrawToast()
    {
        if (string.IsNullOrEmpty(_toastMsg)) return;
        if (Time.realtimeSinceStartup > _toastUntil) { _toastMsg = ""; return; }
        var prevColor = GUI.color;
        GUI.color = Color.yellow;
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(_toastMsg);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.color = prevColor;
    }
}
