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
            catch (System.Exception ex) { Util.Logger.Warn($"ContainerPanel initial container load: {ex.Message}"); }
        }
        try
        {
            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        }
        catch (System.Exception ex)
        {
            // v0.7.0.1 fix — IMGUI frame 폐기 회피. 미래 strip 회귀 발견 시 진단 가능.
            Util.Logger.Warn($"ContainerPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
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
            // DragWindow 영역 — 헤더 전체 (X 버튼 제외)
            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (System.Exception ex)
        {
            Util.Logger.Warn($"ContainerPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
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
                _filter = cat;
            GUI.color = prevColor;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawLeftColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));
        _leftColumnScroll = GUILayout.BeginScrollView(_leftColumnScroll, GUILayout.Height(640));

        // 인벤
        var invView = _invView.ApplyView(_inventoryRows, _globalState);
        float invWeight = 0f;
        foreach (var r in _inventoryRows) invWeight += r.Weight;   // 라벨은 raw 기준 (전체 무게)
        GUILayout.Label(FormatCount(KoreanStrings.Lbl_Inventory, _inventoryRows.Count, invWeight, _inventoryMaxWeight, allowOvercap: true));
        DrawItemList(ContainerArea.Inventory, invView, _inventoryChecks, ref _invScroll, 220);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnInventoryToContainerMove?.Invoke(new HashSet<int>(_inventoryChecks));
        if (GUILayout.Button("→ 복사")) OnInventoryToContainerCopy?.Invoke(new HashSet<int>(_inventoryChecks));
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        // 창고
        var stoView = _stoView.ApplyView(_storageRows, _globalState);
        float stoWeight = 0f;
        foreach (var r in _storageRows) stoWeight += r.Weight;
        GUILayout.Label(FormatCount(KoreanStrings.Lbl_Storage, _storageRows.Count, stoWeight, _storageMaxWeight, allowOvercap: false));
        DrawItemList(ContainerArea.Storage, stoView, _storageChecks, ref _stoScroll, 220);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ 이동")) OnStorageToContainerMove?.Invoke(new HashSet<int>(_storageChecks));
        if (GUILayout.Button("→ 복사")) OnStorageToContainerCopy?.Invoke(new HashSet<int>(_storageChecks));
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
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

        var conView = _conView.ApplyView(_containerRows, _globalState);
        GUILayout.Label($"{KoreanStrings.Lbl_Container} ({_containerRows.Count}개)");
        DrawItemList(ContainerArea.Container, conView, _containerChecks, ref _conScroll, 500);

        // v0.7.1: destination 별 4 버튼 (좌측 column mirror) + 삭제
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(KoreanStrings.BtnInvMove)) OnContainerToInventoryMove?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button(KoreanStrings.BtnInvCopy)) OnContainerToInventoryCopy?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(KoreanStrings.BtnStoMove)) OnContainerToStorageMove?.Invoke(new HashSet<int>(_containerChecks));
        if (GUILayout.Button(KoreanStrings.BtnStoCopy)) OnContainerToStorageCopy?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("☓ 삭제")) OnContainerDelete?.Invoke(new HashSet<int>(_containerChecks));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void DrawGlobalToolbar()
    {
        var newState = SearchSortToolbar.Draw(_globalState, _gradeQualityEnabled);
        if (!newState.Equals(_globalState))
        {
            _globalState = newState;
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
            var cellRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
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
        return $"{r.Name} ({cat}{enh}/{r.Weight:F1}kg){equipped}";
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

    private void DrawToast()
    {
        // v0.7.0.1 fix — IL2CPP IMGUI 가 GUILayout.FlexibleSpace() 를 strip → 매 frame 호출 시
        // unhandled exception → IMGUI frame 폐기 (사용자 보고 "UI 전부 날라감 → 다시 표시").
        // global ToastService 로 통합. 본 method 는 backwards-compat 으로 남겨둠 (no-op).
    }
}
