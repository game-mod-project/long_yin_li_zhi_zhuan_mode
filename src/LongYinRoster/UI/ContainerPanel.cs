using System;
using System.Collections.Generic;
using LongYinRoster.Containers;
using LongYinRoster.Util;
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
        // TODO(v0.7.6+): legacy field — write-only since v0.7.5 (writers: ContainerRowBuilder; readers: none in src/ + tests).
        // 외부 reflection 접근 가능성 (game-self / 다른 mod) 검증 후 제거 — v0.7.5 hotfix scope 제외.
        public string  Name      { get; init; } = "";
        public int     Type      { get; init; }
        public int     SubType   { get; init; }
        public int     EnhanceLv { get; init; }
        public float   Weight    { get; init; }
        public bool    Equipped  { get; init; }

        // v0.7.2 D-3 sort keys (ContainerRowBuilder 가 채움). 미발견 시 -1 / "" — 정렬 끝/앞으로 밀림.
        public string  CategoryKey  { get; init; } = "";
        public string  NameRaw      { get; init; } = "";
        public int     GradeOrder   { get; init; } = -1;
        public int     QualityOrder { get; init; } = -1;

        // v0.7.5 D-4 — translated display name (HangulDict.Translate cached at row build time).
        // 사전 hit 시 한글, miss 시 raw 한자 그대로 (== NameRaw). null 은 build 안 거친 ItemRow 만 (안전상
        // string? 유지). 따라서 caller 는 일반적으로 NameKr 만 사용하면 됨; ?? NameRaw 가드는 방어적.
        // Eager cache: IMGUI re-renders every frame, display-time Translate() per row 가 CPU 낭비.
        // Trade-off: dict reload 는 next Set*Rows 까지 기존 row 미반영 — dict 가 init 후 static 이라 OK.
        public string? NameKr      { get; init; }
    }

    public bool Visible { get; set; } = false;
    public Rect WindowRect => _rect;
    private Rect _rect = new Rect(150, 100, 800, 760);
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
    private float         _inventoryMaxWeight = 964f;   // v0.7.1
    private float         _storageMaxWeight   = 300f;   // v0.7.1

    // v0.7.4 D-1 — 글로벌 1 focus + raw item paired sources
    // _focusRawRef: 클릭 시점의 raw item reference. Set*Rows 호출 시 ref equality 검증으로
    // IL2Cpp List packing 후 idx 가 within bounds 여도 다른 item 으로 바뀌면 stale 감지 → focus clear.
    private (ContainerArea Area, int Index)? _focus;
    private object? _focusRawRef;
    private List<object> _inventoryRawItems = new();
    private List<object> _storageRawItems   = new();
    private List<object> _containerRawItems = new();

    // ItemDetailPanel toggle callback — Plugin.cs/ModWindow.cs 가 wire-up (Task 7).
    public Action? OnToggleItemDetailPanel;
    public Func<bool>? IsItemDetailPanelVisible;

    // v0.7.12 Cat 3C — Undo callback. ModWindow 가 wire-up: OnUndoRequested = PerformUndo.
    public Action? OnUndoRequested;

    // v0.7.11 Cat 5A — 삭제 confirm dialog (panel-local, ModWindow 와 별도 instance)
    private readonly ConfirmDialog _confirmDialog = new();

    // v0.7.11 Cat 9A/9D — corner resize handle (사용자 panel 안에서 width/height drag-resize)
    private bool    _resizing;
    private Vector2 _resizeStart;
    private Vector2 _resizeStartSize;
    private const float MIN_W = 600f;
    private const float MAX_W = 1600f;
    private const float MIN_H = 400f;
    private const float MAX_H = 1080f;

    public bool HasFocus => _focus.HasValue;
    public (ContainerArea Area, int Index)? Focus => _focus;
    public void SetFocus(ContainerArea area, int index)
    {
        _focus = (area, index);
        var raw = AreaToRawItems(area);
        _focusRawRef = (raw != null && index >= 0 && index < raw.Count) ? raw[index] : null;
    }
    public void ClearFocus() { _focus = null; _focusRawRef = null; }

    public object? GetFocusedRawItem()
    {
        if (!_focus.HasValue) return null;
        var area = _focus.Value.Area;
        var idx  = _focus.Value.Index;
        var raw = AreaToRawItems(area);
        // raw list 가 비어있는 경우 (Container JSON path 의도) — focus 유지하되 panel 데이터는 null 반환.
        // 이렇게 해야 컨테이너 area cell 클릭 시 outline 시각 피드백 유지 (panel 내용은 빈 상태).
        if (raw == null || raw.Count == 0) return null;
        if (idx < 0 || idx >= raw.Count) { _focus = null; _focusRawRef = null; return null; }
        var current = raw[idx];
        // raw ref 검증 — IL2Cpp List packing 후 idx 가 다른 item 으로 바뀌면 clear.
        if (current == null || !ReferenceEquals(current, _focusRawRef))
        {
            _focus = null; _focusRawRef = null;
            return null;
        }
        return current;
    }

    private List<object>? AreaToRawItems(ContainerArea area) => area switch
    {
        ContainerArea.Inventory => _inventoryRawItems,
        ContainerArea.Storage   => _storageRawItems,
        ContainerArea.Container => _containerRawItems,
        _ => null,
    };

    // v0.7.2 D-3 — 1 global search/sort state (3-area 통합) + cached view
    private SearchSortState _globalState = SearchSortState.Default;
    private readonly ContainerView _invView = new();
    private readonly ContainerView _stoView = new();
    private readonly ContainerView _conView = new();

    // v0.7.2 — Task 0 spike 결과로 enable/disable. 미발견 시 dropdown grade/quality 비활성 + 토스트 1회.
    private bool _gradeQualityEnabled    = true;
    private bool _gradeQualityToastShown = false;

    private Vector2 _invScroll = Vector2.zero;
    private Vector2 _stoScroll = Vector2.zero;
    private Vector2 _conScroll = Vector2.zero;
    private Vector2 _leftColumnScroll = Vector2.zero;

    // 컨테이너 선택 / 신규 / 이름변경
    private ContainerRepository? _repo;
    private List<ContainerMetadata> _containerList = new();
    private int    _selectedContainerIdx = -1;
    private bool   _initialContainerLoadPending = false;   // v0.7.2 Bug B fix v2 — OnGUI 에서 lazy invoke
    private bool   _dropdownOpen = false;
    private string _renameBuffer = "";
    private bool   _renameMode = false;
    private string _newNameBuffer = "";
    private bool   _newMode = false;

    // 호스트 callback 들 (Plugin.cs 가 wire)
    public Action<int>? OnContainerSelected;
    public Action<HashSet<int>>? OnInventoryToContainerMove;
    public Action<HashSet<int>>? OnInventoryToContainerCopy;
    public Action<HashSet<int>>? OnStorageToContainerMove;
    public Action<HashSet<int>>? OnStorageToContainerCopy;
    public Action<HashSet<int>>? OnContainerToInventoryMove;
    public Action<HashSet<int>>? OnContainerToInventoryCopy;
    public Action<HashSet<int>>? OnContainerToStorageMove;     // v0.7.1
    public Action<HashSet<int>>? OnContainerToStorageCopy;     // v0.7.1
    public Action<HashSet<int>>? OnContainerDelete;
    public Action? OnRequestRefresh;  // 호스트에 row 갱신 요청

    public void SetRepository(ContainerRepository repo)
    {
        _repo = repo;
        RefreshContainerList();
    }

    /// <summary>
    /// v0.7.6 — Config 의 영속화 항목 (sort/filter/lastIndex/rect) 을 hydrate.
    /// SetRepository 다음 호출 — containerList 가 채워진 상태에서 lastIndex 검증 가능.
    /// </summary>
    public void HydrateFromConfig()
    {
        _filter = ItemCategoryFilter.ParseOrDefault(Config.ContainerFilterCategory.Value);
        _globalState = new SearchSortState("",
            SortKeyParser.ParseOrDefault(Config.ContainerSortKey.Value),
            Config.ContainerSortAscending.Value);
        _invView.Invalidate(); _stoView.Invalidate(); _conView.Invalidate();

        var lastIdx = Config.ContainerLastIndex.Value;
        if (lastIdx > 0 && _containerList.Exists(c => c.ContainerIndex == lastIdx))
        {
            _selectedContainerIdx = lastIdx;
            _initialContainerLoadPending = true;
        }
        // 삭제된 컨테이너 가리킴 → RefreshContainerList 의 default (첫 컨테이너) 유지

        _rect = new Rect(Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
                         Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
    }

    /// <summary>v0.7.6 — SettingsPanel.OnSaved 에서 호출. ContainerPanel rect 갱신.</summary>
    public void SetRect(float x, float y, float w, float h)
    {
        _rect = new Rect(x, y, w, h);
    }

    public void SetInventoryRows(List<ItemRow> rows, List<object> rawItems, float maxWeight = 964f)
    {
        _inventoryRows = rows;
        _inventoryRawItems = rawItems;
        _inventoryChecks.Clear();
        _inventoryMaxWeight = maxWeight;
        _invView.Invalidate();
        InvalidateFocusIfStale(ContainerArea.Inventory, rawItems);
    }
    public void SetStorageRows(List<ItemRow> rows, List<object> rawItems, float maxWeight = 300f)
    {
        _storageRows = rows;
        _storageRawItems = rawItems;
        _storageChecks.Clear();
        _storageMaxWeight = maxWeight;
        _stoView.Invalidate();
        InvalidateFocusIfStale(ContainerArea.Storage, rawItems);
    }
    public void SetContainerRows(List<ItemRow> rows, List<object> rawItems)
    {
        _containerRows = rows;
        _containerRawItems = rawItems;
        _containerChecks.Clear();
        _conView.Invalidate();
        InvalidateFocusIfStale(ContainerArea.Container, rawItems);
    }

    /// <summary>
    /// v0.7.4 D-1 — Set*Rows 호출 후 focus stale 감지: idx OOB 또는 idx 의 raw item 이
    /// 이전 _focusRawRef 와 다른 (IL2Cpp List packing 또는 이동·삭제) 시 focus clear.
    /// 다른 area 의 focus 는 영향 없음.
    /// raw list 빈 경우 (Container JSON path 의도) — ref 검증 skip 하고 focus 유지
    /// (panel 은 GetFocusedRawItem 에서 null 반환, outline 만 시각 피드백).
    /// </summary>
    private void InvalidateFocusIfStale(ContainerArea area, List<object> rawItems)
    {
        if (!_focus.HasValue || _focus.Value.Area != area) return;
        if (rawItems.Count == 0) return;   // Container JSON path — focus 유지
        int idx = _focus.Value.Index;
        if (idx < 0 || idx >= rawItems.Count
            || rawItems[idx] == null
            || !ReferenceEquals(rawItems[idx], _focusRawRef))
        {
            _focus = null;
            _focusRawRef = null;
        }
    }
    public void SetGradeQualityEnabled(bool enabled) { _gradeQualityEnabled = enabled; }

    /// <summary>
    /// 컨테이너 panel 안 toast — global ToastService.Push 로 위임 (v0.7.0.1 fix:
    /// 자체 DrawToast 의 GUILayout.FlexibleSpace IL2CPP strip 회피).
    /// </summary>
    public void Toast(string msg, float duration = 3.0f)
    {
        ToastService.Push(msg, ToastKind.Info);
    }

    public int SelectedContainerIndex => _selectedContainerIdx;

    private void RefreshContainerList()
    {
        if (_repo == null) return;
        _containerList = _repo.List();
        if (_selectedContainerIdx < 0 && _containerList.Count > 0)
        {
            _selectedContainerIdx = _containerList[0].ContainerIndex;
            // v0.7.2 Bug B fix v2: SetRepository 시점엔 OnContainerSelected callback 이 아직 wiring 안 됨.
            // OnGUI 에서 lazy invoke (callback wiring 후 첫 frame).
            _initialContainerLoadPending = true;
        }
    }

    public void OnGUI()
    {
        if (!Visible) return;
        // v0.7.2 Bug B fix v2 — lazy invoke (SetRepository 시점에 wiring 안 된 경우).
        if (_initialContainerLoadPending && _selectedContainerIdx > 0 && OnContainerSelected != null)
        {
            _initialContainerLoadPending = false;
            try { OnContainerSelected.Invoke(_selectedContainerIdx); }
            catch (System.Exception ex) { Util.Logger.WarnOnce("ContainerPanel", $"ContainerPanel initial container load: {ex.Message}"); }
        }
        try
        {
            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        }
        catch (System.Exception ex)
        {
            // v0.7.0.1 fix — IMGUI frame 폐기 회피. 미래 strip 회귀 발견 시 진단 가능.
            Util.Logger.WarnOnce("ContainerPanel", $"ContainerPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Draw(int id)
    {
        try
        {
            // v0.7.2 D-3 — grade/quality reflection 미발견 1회 토스트
            if (!_gradeQualityEnabled && !_gradeQualityToastShown)
            {
                ToastService.Push(KoreanStrings.Tip_GradeQualityUnavailable, ToastKind.Info);
                _gradeQualityToastShown = true;
            }

            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "컨테이너 관리");

            // 닫기 버튼 (창 우상단) — 헤더 높이 28 안에 배치
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
                Visible = false;

            // content 시작 — 헤더 28 + 여백 4
            GUILayout.Space(DialogStyle.HeaderHeight);
            DrawCategoryTabs();
            GUILayout.Space(2);
            DrawGlobalToolbar();   // v0.7.2 D-3 — global toolbar (인벤/창고/컨테이너 통합)
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            DrawLeftColumn();
            GUILayout.Space(4);
            DrawRightColumn();
            GUILayout.EndHorizontal();
            DrawToast();
            // v0.7.11 Cat 5A — 삭제 confirm dialog (modal overlay)
            _confirmDialog.Draw();
            // v0.7.11 Cat 9A/9D — corner resize handle (DragWindow 보다 먼저 — corner 영역 우선)
            DrawResizeHandle();
            // DragWindow 영역 — 헤더 전체 (X 버튼 제외)
            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (System.Exception ex)
        {
            Util.Logger.WarnOnce("ContainerPanel", $"ContainerPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
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
            {
                if (_filter != cat)
                {
                    _filter = cat;
                    Config.ContainerFilterCategory.Value = cat.ToString();   // v0.7.6 immediate write
                    // v0.7.11 Cat 4G — Book 이 아닌 카테고리로 변경 시 무공 type filter reset
                    if (cat != ItemCategory.Book && _globalState.KungfuTypeFilter >= 0)
                    {
                        _globalState = _globalState.WithKungfuTypeFilter(-1);
                        _invView.Invalidate();
                        _stoView.Invalidate();
                        _conView.Invalidate();
                    }
                }
            }
            GUI.color = prevColor;
        }
        GUILayout.EndHorizontal();

        // v0.7.11 Cat 4G — 카테고리 = Book 일 때만 무공 type secondary tab 표시
        if (_filter == ItemCategory.Book)
        {
            DrawKungfuTypeSecondaryTabs();
        }
    }

    /// <summary>v0.7.11 Cat 4G — 9 무공 type secondary tab (전체 + 내공/.../사술).</summary>
    private static readonly string[] KungfuTypeNames = {
        "내공", "경공", "절기", "권장", "검법", "도법", "장병", "기문", "사술",
    };
    private void DrawKungfuTypeSecondaryTabs()
    {
        GUILayout.BeginHorizontal();
        int filter = _globalState.KungfuTypeFilter;
        bool active = filter == -1;
        var prev = GUI.color;
        if (active) GUI.color = Color.cyan;
        if (GUILayout.Button("전체", GUILayout.Width(50)))
        {
            if (filter != -1)
            {
                _globalState = _globalState.WithKungfuTypeFilter(-1);
                _invView.Invalidate(); _stoView.Invalidate(); _conView.Invalidate();
            }
        }
        GUI.color = prev;
        for (int t = 0; t < KungfuTypeNames.Length; t++)
        {
            active = filter == t;
            prev = GUI.color;
            if (active) GUI.color = Color.cyan;
            if (GUILayout.Button(KungfuTypeNames[t], GUILayout.Width(50)))
            {
                if (filter != t)
                {
                    _globalState = _globalState.WithKungfuTypeFilter(t);
                    _invView.Invalidate(); _stoView.Invalidate(); _conView.Invalidate();
                }
            }
            GUI.color = prev;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawLeftColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));

        // v0.7.11 Cat 1A/1B — collapse 토글 + split preset
        bool invCollapsed = Config.ContainerInventoryCollapsed.Value;
        bool stoCollapsed = Config.ContainerStorageCollapsed.Value;
        int  preset       = Config.ContainerSplitPreset.Value;

        // 사용 가능 height = 640 (기존 outer ScrollView height). 두 list 의 content area 에 분배.
        // EXPANDED_OVERHEAD = 헤더(28) + 버튼row(28) ≈ 56 per section.
        const float TOTAL_H            = 640f;
        const float EXPANDED_OVERHEAD  = 56f;
        const float COLLAPSED_OVERHEAD = 28f;   // 헤더만

        float invContentH, stoContentH;
        if (invCollapsed && stoCollapsed)
        {
            invContentH = 0f; stoContentH = 0f;
        }
        else if (invCollapsed)
        {
            invContentH = 0f;
            stoContentH = TOTAL_H - COLLAPSED_OVERHEAD - EXPANDED_OVERHEAD - 4f;
        }
        else if (stoCollapsed)
        {
            invContentH = TOTAL_H - EXPANDED_OVERHEAD - COLLAPSED_OVERHEAD - 4f;
            stoContentH = 0f;
        }
        else
        {
            float available = TOTAL_H - 2 * EXPANDED_OVERHEAD - 4f;
            (invContentH, stoContentH) = preset switch
            {
                1 => (available * 0.7f, available * 0.3f),  // 70:30
                2 => (available * 0.3f, available * 0.7f),  // 30:70
                3 => (available - 60f, 60f),                // 인벤 확장 / 창고 최소
                _ => (available * 0.5f, available * 0.5f),  // 50:50
            };
        }

        // ─── 인벤토리 ───
        DrawSectionHeader(invCollapsed,
            () => Config.ContainerInventoryCollapsed.Value = !invCollapsed,
            KoreanStrings.Lbl_Inventory, _inventoryRows, _inventoryChecks, _inventoryMaxWeight, allowOvercap: true);
        if (!invCollapsed && invContentH > 10f)
        {
            var invView = _invView.ApplyView(_inventoryRows, _globalState);
            DrawSelectionBulkRow(invView, _inventoryChecks);
            DrawItemList(ContainerArea.Inventory, invView, _inventoryChecks, ref _invScroll, invContentH);
            // v0.7.11 Cat 3G — selection 0 또는 컨테이너 미선택 시 disabled, ≥1 시 녹색 강조
            DrawMoveCopyRow(_inventoryChecks,
                onMove: c => OnInventoryToContainerMove?.Invoke(c),
                onCopy: c => OnInventoryToContainerCopy?.Invoke(c),
                requireContainerSelection: true);
        }

        GUILayout.Space(4);

        // ─── 창고 ───
        DrawSectionHeader(stoCollapsed,
            () => Config.ContainerStorageCollapsed.Value = !stoCollapsed,
            KoreanStrings.Lbl_Storage, _storageRows, _storageChecks, _storageMaxWeight, allowOvercap: false);
        if (!stoCollapsed && stoContentH > 10f)
        {
            var stoView = _stoView.ApplyView(_storageRows, _globalState);
            DrawSelectionBulkRow(stoView, _storageChecks);
            DrawItemList(ContainerArea.Storage, stoView, _storageChecks, ref _stoScroll, stoContentH);
            DrawMoveCopyRow(_storageChecks,
                onMove: c => OnStorageToContainerMove?.Invoke(c),
                onCopy: c => OnStorageToContainerCopy?.Invoke(c),
                requireContainerSelection: true);
        }

        // Split preset cycle button
        GUILayout.Space(4);
        string presetLabel = preset switch { 1 => "70:30", 2 => "30:70", 3 => "확장:최소", _ => "50:50" };
        if (GUILayout.Button($"비율 {presetLabel} ▼", GUILayout.Width(120)))
        {
            Config.ContainerSplitPreset.Value = (preset + 1) % 4;
        }

        GUILayout.EndVertical();
    }

    /// <summary>v0.7.11 Cat 1B + 2B — list section 헤더: collapse toggle + 라벨 (선택 카운터+무게 포함).</summary>
    private void DrawSectionHeader(bool collapsed, System.Action onToggle, string title,
                                   List<ItemRow> rows, HashSet<int> checks, float maxWeight, bool allowOvercap)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(collapsed ? "▶" : "▼", GUILayout.Width(24))) onToggle();
        float totalWeight = 0f;
        foreach (var r in rows) totalWeight += r.Weight;

        // v0.7.11 Cat 2B — 선택 ≥1 시 카운터 + 무게 합계 표시
        int selCount = checks.Count;
        if (selCount > 0)
        {
            float selWeight = 0f;
            foreach (var r in rows) if (checks.Contains(r.Index)) selWeight += r.Weight;
            GUILayout.Label($"{title} (선택: {selCount} / {rows.Count}개, {selWeight:F1}/{totalWeight:F1}kg / 최대 {maxWeight:F1}kg)");
        }
        else
        {
            GUILayout.Label(FormatCount(title, rows.Count, totalWeight, maxWeight, allowOvercap));
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>v0.7.11 Cat 2A + 2C — 일괄선택 button row (전체/해제/반전 + 등급 ≥ cycle).</summary>
    private void DrawSelectionBulkRow(List<ItemRow> view, HashSet<int> checks)
    {
        GUILayout.BeginHorizontal();
        // [☑ 전체] — 현재 visible (filtered) row 모두 선택
        if (GUILayout.Button("☑ 전체", GUILayout.Width(60)))
            foreach (var r in view) checks.Add(r.Index);
        // [☐ 해제] — 선택 모두 제거
        if (GUILayout.Button("☐ 해제", GUILayout.Width(60)))
            checks.Clear();
        // [↺ 반전] — visible row 의 currently checked 빼고 unchecked 는 추가
        if (GUILayout.Button("↺ 반전", GUILayout.Width(60)))
        {
            foreach (var r in view)
            {
                if (checks.Contains(r.Index)) checks.Remove(r.Index);
                else                          checks.Add(r.Index);
            }
        }
        // 등급 ≥ N cycle — 글로벌 state 의 MinGradeOrder 사용 (Cat 4B 와 자산 공유)
        int min = _globalState.MinGradeOrder;
        string rareLabel = min < 0 ? "등급 전체"
            : (min == 0 ? "≥ 열악" : min == 1 ? "≥ 보통" : min == 2 ? "≥ 정량"
            : min == 3 ? "≥ 비전" : min == 4 ? "≥ 정극" : "≥ 절세");
        if (GUILayout.Button($"[{rareLabel}]", GUILayout.Width(80)))
        {
            // -1 → 0 → 1 → ... → 5 → -1
            int next = min < 5 ? min + 1 : -1;
            _globalState = _globalState.WithMinGradeOrder(next);
            _invView.Invalidate(); _stoView.Invalidate(); _conView.Invalidate();
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>v0.7.11 Cat 3G — [→이동] / [→복사] (또는 ←) row. selection 0 / 컨테이너 미선택 시 disabled, ≥1 시 녹색 강조.</summary>
    private void DrawMoveCopyRow(HashSet<int> checks,
                                 System.Action<HashSet<int>>? onMove,
                                 System.Action<HashSet<int>>? onCopy,
                                 bool requireContainerSelection,
                                 string moveLabel = "→ 이동",
                                 string copyLabel = "→ 복사")
    {
        bool hasSelection = checks.Count > 0;
        bool hasContainer = !requireContainerSelection || _selectedContainerIdx > 0;
        bool enabled      = hasSelection && hasContainer;

        GUILayout.BeginHorizontal();
        var prevColor   = GUI.color;
        var prevEnabled = GUI.enabled;
        GUI.enabled = enabled;
        if (enabled) GUI.color = new Color(0.5f, 1.0f, 0.5f);   // 녹색 강조
        if (GUILayout.Button(moveLabel)) onMove?.Invoke(new HashSet<int>(checks));
        if (GUILayout.Button(copyLabel)) onCopy?.Invoke(new HashSet<int>(checks));
        GUI.color   = prevColor;
        GUI.enabled = prevEnabled;
        GUILayout.EndHorizontal();
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
        // v0.7.11 Cat 5C — 컨테이너 복사 (Clone)
        if (GUILayout.Button("복사", GUILayout.Width(45)))
        {
            if (_repo != null && _selectedContainerIdx > 0)
            {
                int newIdx = _repo.Clone(_selectedContainerIdx);
                if (newIdx > 0)
                {
                    RefreshContainerList();
                    _selectedContainerIdx = newIdx;
                    Config.ContainerLastIndex.Value = newIdx;
                    OnContainerSelected?.Invoke(newIdx);
                    Toast($"컨테이너 #{newIdx} 복사됨");
                }
                else
                {
                    Toast("컨테이너 복사 실패");
                }
                _dropdownOpen = false;
            }
        }
        // v0.7.11 Cat 5A — 삭제 confirm dialog (즉시 삭제 → 사용자 확인 후 삭제)
        if (GUILayout.Button("삭제", GUILayout.Width(45)))
        {
            if (_repo != null && _selectedContainerIdx > 0)
            {
                var meta = _containerList.Find(c => c.ContainerIndex == _selectedContainerIdx);
                string name  = meta?.ContainerName ?? $"#{_selectedContainerIdx}";
                int    items = meta?.ItemCount ?? 0;
                _confirmDialog.Show(
                    title: "컨테이너 삭제",
                    body: $"<{name}> 컨테이너를 삭제하시겠습니까?\n안의 {items}개 item 도 함께 삭제됩니다.",
                    confirmLabel: "삭제",
                    onConfirm: DoDeleteSelectedContainer);
                _dropdownOpen = false;
            }
        }
        GUILayout.EndHorizontal();

        if (_dropdownOpen)
        {
            foreach (var m in _containerList)
            {
                // v0.7.11 Cat 5B — dropdown entry 에 ItemCount + TotalWeight 표시
                string dropdownLabel = $"{m.ContainerIndex:D2}: {m.ContainerName} ({m.ItemCount}개, {m.TotalWeight:F1}kg)";
                if (GUILayout.Button(dropdownLabel))
                {
                    _selectedContainerIdx = m.ContainerIndex;
                    Config.ContainerLastIndex.Value = m.ContainerIndex;   // v0.7.6 immediate write
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
                Config.ContainerLastIndex.Value = idx;   // v0.7.6 immediate write
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

        var conView = _conView.ApplyView(_containerRows, _globalState);
        GUILayout.Label($"{KoreanStrings.Lbl_Container} ({_containerRows.Count}개)");
        DrawItemList(ContainerArea.Container, conView, _containerChecks, ref _conScroll, 500);

        // v0.7.1: destination 별 4 버튼 (좌측 column mirror) + 삭제
        // v0.7.11 Cat 3G — selection 0 시 disabled, ≥1 시 녹색 강조
        DrawMoveCopyRow(_containerChecks,
            onMove: c => OnContainerToInventoryMove?.Invoke(c),
            onCopy: c => OnContainerToInventoryCopy?.Invoke(c),
            requireContainerSelection: false,
            moveLabel: KoreanStrings.BtnInvMove, copyLabel: KoreanStrings.BtnInvCopy);
        DrawMoveCopyRow(_containerChecks,
            onMove: c => OnContainerToStorageMove?.Invoke(c),
            onCopy: c => OnContainerToStorageCopy?.Invoke(c),
            requireContainerSelection: false,
            moveLabel: KoreanStrings.BtnStoMove, copyLabel: KoreanStrings.BtnStoCopy);
        // 삭제 — selection 0 시 disabled, ≥1 시 빨강 강조
        GUILayout.BeginHorizontal();
        bool hasContainerSel = _containerChecks.Count > 0;
        var prevColor   = GUI.color;
        var prevEnabled = GUI.enabled;
        GUI.enabled = hasContainerSel;
        if (hasContainerSel) GUI.color = new Color(1.0f, 0.5f, 0.5f);   // 빨강 — destructive
        if (GUILayout.Button("☓ 삭제")) OnContainerDelete?.Invoke(new HashSet<int>(_containerChecks));
        GUI.color   = prevColor;
        GUI.enabled = prevEnabled;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    /// <summary>v0.7.11 Cat 5A — 삭제 confirm dialog 의 onConfirm callback. 기존 즉시 삭제 logic 동일.</summary>
    private void DoDeleteSelectedContainer()
    {
        if (_repo == null || _selectedContainerIdx <= 0) return;
        _repo.Delete(_selectedContainerIdx);
        _selectedContainerIdx = -1;
        Config.ContainerLastIndex.Value = -1;   // v0.7.6 immediate write
        RefreshContainerList();
        OnContainerSelected?.Invoke(-1);
        Toast("컨테이너 삭제됨");
    }

    private void DrawGlobalToolbar()
    {
        var newState = SearchSortToolbar.Draw(_globalState, _gradeQualityEnabled);
        if (!newState.Equals(_globalState))
        {
            _globalState = newState;
            // v0.7.6 — sort key/방향 immediate ConfigEntry write (검색 textbox 는 영속화 안 함)
            if (newState.Key.ToString() != Config.ContainerSortKey.Value)
                Config.ContainerSortKey.Value = newState.Key.ToString();
            if (newState.Ascending != Config.ContainerSortAscending.Value)
                Config.ContainerSortAscending.Value = newState.Ascending;
            _invView.Invalidate();
            _stoView.Invalidate();
            _conView.Invalidate();
        }

        // v0.7.4 D-1 — ⓘ 상세 토글 (active=cyan)
        GUILayout.BeginHorizontal();
        bool detailVisible = IsItemDetailPanelVisible?.Invoke() ?? false;
        var prevColor = GUI.color;
        if (detailVisible) GUI.color = Color.cyan;
        if (GUILayout.Button("ⓘ 상세", GUILayout.Width(60)))
            OnToggleItemDetailPanel?.Invoke();
        GUI.color = prevColor;

        // v0.7.11 Cat 2H/4E — 착용중 제외 toggle (filter context, all 3 areas 영향)
        GUILayout.Space(8);
        bool exclude = _globalState.ExcludeEquipped;
        bool newExclude = GUILayout.Toggle(exclude, "착용중 제외", GUILayout.Width(100));
        if (newExclude != exclude)
        {
            _globalState = _globalState.WithExcludeEquipped(newExclude);
            _invView.Invalidate();
            _stoView.Invalidate();
            _conView.Invalidate();
        }

        // v0.7.12 Cat 3C — Undo button (can-undo 시 enabled, 노랑 강조)
        GUILayout.Space(8);
        var undoPrevColor   = GUI.color;
        var undoPrevEnabled = GUI.enabled;
        GUI.enabled = Containers.ContainerOpUndo.CanUndo;
        if (GUI.enabled) GUI.color = new Color(1.0f, 0.9f, 0.5f);   // 노랑 (warning-ish)
        if (GUILayout.Button("↶ Undo", GUILayout.Width(80))) OnUndoRequested?.Invoke();
        GUI.color   = undoPrevColor;
        GUI.enabled = undoPrevEnabled;

        // v0.7.11 Cat 4K — 결과 카운터 (filter 적용 후 visible / raw total).
        // 직전 frame 의 ApplyView cache 기반 — 1 frame lag 가능 but 사용자 인지 영향 미미.
        GUILayout.Space(8);
        int rawTotal      = _inventoryRows.Count + _storageRows.Count + _containerRows.Count;
        int filteredTotal = _invView.LastViewCount + _stoView.LastViewCount + _conView.LastViewCount;
        if (filteredTotal == rawTotal)
            GUILayout.Label($"({rawTotal}개)", GUILayout.Width(80));
        else
            GUILayout.Label($"(결과: {filteredTotal} / {rawTotal})", GUILayout.Width(140));
        GUILayout.EndHorizontal();
    }

    private void DrawItemList(ContainerArea area, List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
    {
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
        var prevColor = GUI.color;
        foreach (var r in rows)
        {
            if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;

            GUILayout.BeginHorizontal();

            // v0.7.4 D-1 — cell = GetRect 자리잡기 + DrawAtRect overlay + Event.current 클릭 감지
            // (v0.7.3 strip lesson: GetLastRect 는 strip 됨 → Button+GetLastRect 패턴 회피)
            // v0.7.5.2 — cell 가로 직사각형 48×24 (한글 라벨: 장비/단약/음식/비급/보물/재료/말).
            var cellRect = GUILayoutUtility.GetRect(48, 24, GUILayout.Width(48), GUILayout.Height(24));
            ItemCellRenderer.DrawAtRect(r, cellRect);
            if (_focus.HasValue && _focus.Value.Area == area && _focus.Value.Index == r.Index)
                DrawFocusOutline(cellRect);
            if (Event.current != null
                && Event.current.type == EventType.MouseDown
                && cellRect.Contains(Event.current.mousePosition))
            {
                // SetFocus 호출로 _focus + _focusRawRef 동시 갱신 — 직접 _focus 할당 시
                // raw ref 가 null 인 채로 남아 다음 frame GetFocusedRawItem 의 ref equality
                // 검증에서 mismatch → focus 자동 clear (smoke 1차 회귀 root cause).
                SetFocus(area, r.Index);
                // v0.7.4 D-1 smoke 2차 — cell 클릭 = single-select. 같은 area 의 다른 check 모두 해제,
                // 클릭된 row 만 add. 다른 area (인벤·창고·컨테이너) 의 check 는 영향 없음.
                checks.Clear();
                checks.Add(r.Index);
                Event.current.Use();
            }

            GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder);   // v0.7.2 row 텍스트 색상 — source 단일화
            bool was = checks.Contains(r.Index);
            bool now = GUILayout.Toggle(was, BuildLabel(r));
            GUI.color = prevColor;
            GUILayout.EndHorizontal();

            if (now != was)
            {
                // v0.7.4 D-1 smoke 2차 — Toggle 라벨 multi-check 진입 시 같은 area 의 cell focus 해제
                // (single cell-focus 와 multi-check 워크플로우 명확 분리).
                if (_focus.HasValue && _focus.Value.Area == area) ClearFocus();
            }
            if (now && !was) checks.Add(r.Index);
            if (!now && was) checks.Remove(r.Index);
        }
        GUILayout.EndScrollView();
    }

    /// <summary>
    /// v0.7.4 D-1 — 24x24 cell focus outline (4-edge 1px cyan).
    /// strip-safe: GUI.DrawTexture(Rect, Texture2D.whiteTexture) — DialogStyle.FillBackground 와 동일 패턴 (검증됨).
    /// </summary>
    private static void DrawFocusOutline(Rect rect)
    {
        var prev = GUI.color;
        GUI.color = Color.cyan;
        GUI.DrawTexture(new Rect(rect.xMin,     rect.yMin,     rect.width, 1),          Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin,     rect.yMax - 1, rect.width, 1),          Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMin,     rect.yMin,     1,          rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - 1, rect.yMin,     1,          rect.height), Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static string BuildLabel(ItemRow r)
    {
        string cat = ItemCategoryFilter.KoreanLabel(ItemCategoryFilter.Classify(r.Type, r.SubType));
        string enh = r.EnhanceLv > 0 ? $"/강화{r.EnhanceLv}" : "";
        string equipped = r.Equipped ? " [착용중]" : "";
        return $"{r.NameKr ?? r.NameRaw ?? ""} ({cat}{enh}/{r.Weight:F1}kg){equipped}";
    }

    /// <summary>
    /// v0.7.1 — "{label} ({countN}개, {curW:F1} / {maxW:F1} kg)" + (인벤만) over-cap 마커.
    /// 창고 hard cap 이라 allowOvercap=false 일 때는 마커 미부착.
    /// internal — LongYinRoster.Tests 가 InternalsVisibleTo 로 호출.
    /// </summary>
    internal static string FormatCount(string label, int countN, float currentWeight, float maxWeight, bool allowOvercap)
    {
        string s = $"{label} ({countN}개, {currentWeight:F1} / {maxWeight:F1} kg)";
        if (allowOvercap && currentWeight > maxWeight) s += KoreanStrings.Lbl_OvercapMarker;
        return s;
    }

    /// <summary>
    /// v0.7.11 Cat 9A/9D — panel 우하단 corner 영역 (16×16) drag-resize.
    /// MouseDown 으로 _resizing 활성화, MouseDrag 로 _rect width/height 갱신,
    /// MouseUp 시 ConfigEntry 영속화. width/height clamp [MIN_W/MAX_W × MIN_H/MAX_H].
    /// strip-safe: v0.7.4 검증 EventType.MouseDown + v0.7.6 검증 패턴. MouseDrag/MouseUp 은
    /// 동일 enum surface 라 strip-safe 추정 (Phase 0 spike).
    /// </summary>
    private void DrawResizeHandle()
    {
        var handleRect = new Rect(_rect.width - 16, _rect.height - 16, 16, 16);
        var prev = GUI.color;
        GUI.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        GUI.DrawTexture(handleRect, Texture2D.whiteTexture);
        GUI.color = prev;

        var e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
        {
            _resizing        = true;
            _resizeStart     = e.mousePosition;
            _resizeStartSize = new Vector2(_rect.width, _rect.height);
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseDrag)
        {
            // Vector2 직접 빼기 — UnityStubs 가 Vector2 의 - operator 미정의 (test compile 만 영향)
            float dx = e.mousePosition.x - _resizeStart.x;
            float dy = e.mousePosition.y - _resizeStart.y;
            float newW = System.Math.Max(MIN_W, System.Math.Min(MAX_W, _resizeStartSize.x + dx));
            float newH = System.Math.Max(MIN_H, System.Math.Min(MAX_H, _resizeStartSize.y + dy));
            _rect = new Rect(_rect.x, _rect.y, newW, newH);
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseUp)
        {
            _resizing = false;
            // v0.7.11 — ConfigEntry 영속화 (BepInEx 자동 file write)
            Config.ContainerPanelW.Value = _rect.width;
            Config.ContainerPanelH.Value = _rect.height;
            e.Use();
        }
    }

    private void DrawToast()
    {
        // v0.7.0.1 fix — IL2CPP IMGUI 가 GUILayout.FlexibleSpace() 를 strip → 매 frame 호출 시
        // unhandled exception → IMGUI frame 폐기 (사용자 보고 "UI 전부 날라감 → 다시 표시").
        // global ToastService 로 통합. 본 method 는 backwards-compat 으로 남겨둠 (no-op).
    }
}
