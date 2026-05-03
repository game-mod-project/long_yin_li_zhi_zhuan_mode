# v0.7.4 — Item 상세 panel (D-1)

**작성일**: 2026-05-03
**Sub-project 위치**: v0.7.3 (D-2 Item 시각 표시 풍부화) 후속 / D 계열 세 번째 release
**Baseline**: v0.7.3 (`main` HEAD — 170/170 tests PASS, tag `v0.7.3`)
**브레인스토밍 결과**:
- Q1: View-only (A). Item editor 는 별도 sub-project (v0.7.7 후보) 로 분리
- Q2: cell 클릭 → focus (A). row Toggle 라벨은 multi-check 그대로
- Q3: 별도 non-modal `ItemDetailPanel` window (C). ContainerPanel 자체 패턴 재사용
- Q4: Hybrid — 카테고리별 curated + raw fallback (C). 단계적 cover (장비/비급/단약 우선)

---

## 1. 목적

ContainerPanel 의 인벤·창고·컨테이너 row 에서 단일 item 을 선택해 sub-data wrapper 에 담긴 모든 reflection 정보 (강화lv / 속성 / 학습 진척도 / 효과 등) 를 별도 window 에 표시한다. 사용자가 인벤 안에서 item 비교·확인·정보 추적이 빨라진다.

view-only — 수정 불가. Item 수정은 IL2CPP setter strip 위험이 큰 별개 작업으로 v0.7.7 후보로 분리.

## 2. 사용자 보고 + 확정 scope

### 2.1 view-only (Q1=A)
선택 item 의 모든 sub-data 필드 + 상위 필드 표시. 수정 불가. v0.7.0 의 외부 disk container ↔ 인벤/창고 워크플로우가 이미 "외부에서 미리 만든 item 가져오기" 시나리오 커버하므로, edit 는 별도 release 로 분리 (v0.7.7 "Item editor" 후보).

### 2.2 cell 클릭 → focus (Q2=A)
v0.7.3 의 24×24 placeholder cell 자체를 invisible Button 으로 변경. row 텍스트 (Toggle 라벨) 은 multi-check 그대로 (이동·복사 워크플로우 보존). cell 클릭 = single focus 변경.

**Focus 모델 — 글로벌 1 focus**: ContainerPanel 이 `(ContainerArea Area, int Index)?` nullable 튜플 보유. 가장 최근 클릭한 cell 의 (area, idx) 로 focus 이동. ItemDetailPanel 은 그 1 focus 의 item 만 표시.

**Focus 시각**: focused row 의 cell 외곽선 cyan 1px (v0.7.2 sort key active 패턴 — `GUI.color = Color.cyan` + `GUI.DrawTexture` border, 검증된 strip-safe).

### 2.3 별도 non-modal window (Q3=C)
신규 `ItemDetailPanel` class — ContainerPanel 자체 패턴 (DialogStyle + GUI.Window + drag + position persist) 재사용. ContainerPanel toolbar 우측 끝의 신규 ⓘ 버튼으로 토글. F11 (ContainerPanel 토글) 시 sync 닫기. position 영속 = Config.cs 신규 entry.

**Default position / size**: ContainerPanel 우측 옆 (`x = ContainerPanel.x + 800 + 10`), `380×500`.

### 2.4 Hybrid — curated + raw fallback (Q4=C)
표시 깊이는 두 섹션:
- **Curated 섹션** (위): 카테고리별 의미 있는 필드 한글 라벨 + 값. v0.7.4 첫 release 우선 cover 3 카테고리 = 장비 / 비급 / 단약. 나머지 (음식 / 보물 / 재료 / 말) 는 빈 list → raw fallback 만 보임. 후속 v0.7.4.x patch 에서 추가
- **Raw 섹션** (아래, 접이식 default closed): item + 활성화 sub-data wrapper 의 모든 reflection 필드 dump. IL2CPP wrapper meta (`Pointer`/`ObjectClass`/`WasCollected`/`isWrapped`/`pooledPtr`) 필터

**spike 의 정확한 sub-data 필드 inventory** 가 첫 task. v0.7.2 의 spike dump 는 ItemData top-level 만 — sub-data wrapper 는 미발견 영역.

### 2.5 비범위 (Out of scope)
- Item 수정 (edit) — v0.7.7 후보로 분리
- 6 카테고리 모두 curated 섹션 — v0.7.4 는 3 우선 + raw fallback
- ItemDetailPanel multi-window (2 item 동시 비교) — focus 는 글로벌 1
- Cell hover preview / tooltip
- Cell 더블 클릭 / 우클릭 등 추가 워크플로우 — 본 release 는 단일 클릭 = focus 만
- ItemDetailPanel 자체의 검색·정렬 — 단일 view
- Container area 별 multiple focus — 글로벌 1

## 3. 후속 sub-project 매핑 (HANDOFF §6.B 갱신)

| Version | 카테고리 | 변경 사유 |
|---|---|---|
| v0.7.2 | D-3 검색·정렬 | 완료 |
| v0.7.3 | D-2 Item 시각 표시 풍부화 (placeholder cell) | 완료 |
| **v0.7.4** | **D-1 Item 상세 panel (view-only, hybrid curated+raw)** | 본 spec |
| **v0.7.4.x (후속)** | **나머지 3 카테고리 curated** (음식·보물·재료·말) | v0.7.4 의 단계적 cover |
| v0.7.5 | D-4 Item 한글화 | unchanged |
| v0.7.6 | 설정 panel | unchanged |
| **v0.7.7 (후보)** | **Item editor** | v0.7.4 view-only 위에 setter reflection 추가. IL2CPP setter strip spike 다수 필요 |
| v0.7.8 | Apply 부분 미리보기 | unchanged |
| v0.7.9 | Slot diff preview | unchanged |
| v0.8 (후보) | NPC 지원 + 진짜 sprite | unchanged |

## 4. 디자인 결정

### 4.1 Approach (브레인스토밍 채택)

**view-only ItemDetailPanel + ContainerPanel 글로벌 focus**.

1. ContainerPanel 의 v0.7.3 cell 을 invisible Button 으로 변경 — 클릭 시 `_focus` 갱신
2. ContainerPanel toolbar 에 ⓘ 토글 버튼 추가 — `ItemDetailPanel.Visible` 제어
3. ItemDetailPanel 매 frame `containerPanel.GetFocusedRawItem()` 호출 → reflection
4. ItemDetailReflector 가 카테고리별 curated + raw fields 추출 (UI 와 무관, 단위 테스트 가능)
5. v0.7.3 cell rendering / SearchSortState / ContainerView / 카테고리 탭 그대로 보존

**탈락 후보**:
- Modal popup (Q3 A 탈락 — 비교 워크플로우 부적합)
- ContainerPanel 하단 strip (Q3 B 탈락 — window 늘려야 + 늘 보임)
- 4번째 column (Q3 D 탈락 — window 1100+ 작은 화면 부적합)
- Item editor (Q1 B 탈락 — IL2CPP setter strip 위험. v0.7.7 별도 release)

### 4.2 데이터 모델

#### 4.2.1 신규 enum + focus 상태

```csharp
namespace LongYinRoster.Containers;

public enum ContainerArea { Inventory, Storage, Container }
```

ContainerPanel 의 신규 필드:
```csharp
private (ContainerArea Area, int Index)? _focus;
```

null = focus 없음 (panel 빈 상태). cell 클릭 시 set, 이동·복사 후 idx OOB 검출 시 자동 clear.

#### 4.2.2 raw item paired source

ContainerPanel.SetXxxRows 시그니처 변경 — `List<object> rawItems` 추가:
```csharp
public void SetInventoryRows(List<ItemRow> rows, List<object> rawItems, float maxWeight = 964f);
public void SetStorageRows(List<ItemRow> rows, List<object> rawItems, float maxWeight = 300f);
public void SetContainerRows(List<ItemRow> rows, List<object> rawItems);
```

`rawItems` = ItemRow.Index 기준 paired raw item objects (IL2CPP `ItemData` reference 또는 JSON deserialized object). ContainerPanel 내부 보관 → focus.Index 로 즉시 조회.

ItemDetailPanel 은 `containerPanel.GetFocusedRawItem()` 호출:
```csharp
public object? GetFocusedRawItem() {
    if (_focus is not (var area, var idx)) return null;
    var raw = area switch {
        ContainerArea.Inventory => _inventoryRawItems,
        ContainerArea.Storage   => _storageRawItems,
        ContainerArea.Container => _containerRawItems,
        _ => null,
    };
    if (raw == null || idx < 0 || idx >= raw.Count) {
        _focus = null;   // OOB 자동 해제
        return null;
    }
    return raw[idx];
}
```

#### 4.2.3 ItemRow 변경 없음

v0.7.3 의 11 필드 그대로 — Index/Name/Type/SubType/EnhanceLv/Weight/Equipped/CategoryKey/NameRaw/GradeOrder/QualityOrder.

### 4.3 신규/변경 모듈

| 모듈 | 위치 | 역할 | 신규/변경 |
|---|---|---|---|
| `ContainerArea` enum | `Containers/ContainerArea.cs` | Inventory/Storage/Container 3 값 | **신규** |
| `ItemDetailReflector` | `Core/ItemDetailReflector.cs` | sub-data wrapper 별 curated 필드 추출 + raw fields enumeration + IL2CPP wrapper meta 필터 | **신규** |
| `ItemDetailPanel` | `UI/ItemDetailPanel.cs` | ContainerPanel 패턴 모방 신규 IMGUI window. focus item 의 curated + raw 표시. F11 sync | **신규** |
| `ContainerPanel.cs` | `UI/` | `_focus` state + cell Button 변경 + ⓘ 토글 + `SetXxxRows` 시그니처 변경 + `GetFocusedRawItem()` | **변경** |
| `ItemCellRenderer.cs` | `UI/` | 신규 `DrawAtRect(r, rect)` overload — 자리 잡기 없이 인자 rect 에 overlay | **변경 (작은)** |
| `Plugin.cs` | 루트 | `_itemDetailPanel` 인스턴스 + OnGUI 호출 + F11 sync close + `Set*Rows` 호출 site `rawItems` 추가 | **변경** |
| `Config.cs` | 루트 | `ItemDetailPanelX/Y/Width/Height/Open` 4 entry | **변경** |

**예상 코드 증분**:
- 신규: ItemDetailPanel.cs ~150 LOC, ItemDetailReflector.cs ~200 LOC, ContainerArea.cs ~5 LOC
- 변경: ContainerPanel.cs +30 LOC (414→~444), ItemCellRenderer.cs +20 LOC, Plugin.cs +10 LOC, Config.cs +5 LOC
- 총 ~420 LOC (v0.7.3 의 130 LOC 보다 큼 — 별도 panel + reflector 분리)

### 4.4 spike (Task 0) — sub-data wrapper 필드 inventory

v0.7.2 spike 는 ItemData top-level 만 cover. v0.7.4 첫 task 는 sub-data wrapper inventory:

| wrapper | 카테고리 | 가설 (spike 후 확정) |
|---|---|---|
| `equipmentData` | 장비 (type=0) | `enhanceLv` ✓ (검증), `equiped` ✓ (검증), 속성 (atk/def/hp/mp 류?), 내구도?, 특수효과? |
| `bookData` | 비급 (type=3) | 학습 lv (현재/최대?), 학습 진척도, 무공 ID 연결? |
| `medFoodData` | 단약/음식 (type=2) | 효과 (회복량/능력치 변경?), 지속시간?, 효과 ID? |
| `treasureData` | 보물 (type=4) | 효과? buff?, 발동 조건? |
| `materialData` | 재료 (type=5) | 사용처? plain int values? |
| `horseData` | 말 (type=6) | `equiped` ✓ (검증), 속도?, hp?, 특수능력? |

**spike 절차**:
1. Plugin.cs 임시 [F12] handler — 인벤/창고/컨테이너 의 각 카테고리 1+ item 의 sub-data wrapper 모든 public/non-public 필드 dump (`BindingFlags.Public | NonPublic | Instance`)
2. 결과 → `docs/superpowers/dumps/2026-05-XX-v0.7.4-subdata-spike.md` 작성 (필드명 / 타입 / sample 값 / 한글 라벨 매핑 후보)
3. spike 결과 기반 `ItemDetailReflector` curated method 3개 (장비/비급/단약) 설계
4. spike 후 [F12] handler 제거 (release 직전 commit)

**사용자 협조 1회 필요**: 인벤/창고/컨테이너에 6 카테고리 sample 다 있는지 확인. 없는 카테고리는 외부 디스크 컨테이너로 받아 spike 시 사용.

### 4.5 `ItemDetailReflector` 인터페이스

```csharp
namespace LongYinRoster.Core;

public static class ItemDetailReflector
{
    /// <summary>
    /// 카테고리별 의미 있는 필드 → (한글 라벨, 표시 값) tuple list.
    /// 우선 cover: 장비 (type=0) / 비급 (type=3) / 단약·음식 (type=2).
    /// 미지원 카테고리 (treasure/material/horse) 는 빈 list — caller 가 raw fallback.
    /// </summary>
    public static List<(string Label, string Value)> GetCuratedFields(object? item);

    /// <summary>
    /// item + 활성화 sub-data wrapper 의 모든 reflection 필드 dump.
    /// IL2CPP wrapper meta 필터 (Pointer/ObjectClass/WasCollected/isWrapped/pooledPtr).
    /// 활성화된 wrapper 1개만 (`item.type` 기준) 의 필드 포함 — 비활성 wrapper 는 dump 안 함.
    /// </summary>
    public static List<(string FieldName, string Value)> GetRawFields(object? item);

    // 카테고리별 helper (private)
    // private static List<(string,string)> GetEquipmentDetails(object item);  // type=0
    // private static List<(string,string)> GetMedFoodDetails(object item);    // type=2
    // private static List<(string,string)> GetBookDetails(object item);       // type=3

    private static readonly HashSet<string> WRAPPER_META = new()
    {
        "ObjectClass", "Pointer", "WasCollected", "isWrapped", "pooledPtr",
    };
}
```

**값 변환**:
- `Convert.ToInt32` / `ToString` 우선 (v0.7.2 ItemReflector.Read 패턴)
- IL2CPP type 이라 unbox 실패 시 fallback = `field.GetType().Name + "<unreadable>"`
- collection field (List<int> 등) raw 섹션에서: `"List<int> attriValues: [count=8]"` (1차 표시 — 펼침은 v0.7.4.x 후보)

### 4.6 ContainerPanel 변경 — cell click + ⓘ 토글

#### 4.6.1 DrawItemList row 패턴

```csharp
GUILayout.BeginHorizontal();

// cell 자체 invisible Button (v0.7.2 toolbar 의 Button + 옵션 검증된 strip-safe)
bool cellClicked = GUILayout.Button("", GUILayout.Width(24), GUILayout.Height(24));
var cellRect = GUILayoutUtility.GetLastRect();   // v0.7.3 GetRect 와 같이 strip-safe (확인 필요 — Button + GetLastRect 패턴 v0.7.2 어디 사용? 첫 smoke 시 확인)

// cell 표면 overlay
ItemCellRenderer.DrawAtRect(r, cellRect);

// focus indicator
if (_focus is (var fa, var fi) && fa == area && fi == r.Index)
    DrawFocusOutline(cellRect);

if (cellClicked) _focus = (area, r.Index);

GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder);
bool was = checks.Contains(r.Index);
bool now = GUILayout.Toggle(was, BuildLabel(r));
GUI.color = prevColor;
GUILayout.EndHorizontal();
// ... checks update
```

`ItemCellRenderer.DrawAtRect(ItemRow r, Rect rect)` = v0.7.3 `Draw(r, size)` 의 overload — `GUILayoutUtility.GetRect` 호출 없이 인자 rect 에 DrawTexture/Label/diamond/badges overlay.

`DrawFocusOutline(Rect rect)`:
```csharp
private static void DrawFocusOutline(Rect rect) {
    var prev = GUI.color;
    GUI.color = Color.cyan;
    GUI.DrawTexture(new Rect(rect.xMin,     rect.yMin,     rect.width, 1), Texture2D.whiteTexture);   // top
    GUI.DrawTexture(new Rect(rect.xMin,     rect.yMax - 1, rect.width, 1), Texture2D.whiteTexture);   // bottom
    GUI.DrawTexture(new Rect(rect.xMin,     rect.yMin,     1,          rect.height), Texture2D.whiteTexture);   // left
    GUI.DrawTexture(new Rect(rect.xMax - 1, rect.yMin,     1,          rect.height), Texture2D.whiteTexture);   // right
    GUI.color = prev;
}
```

**우려**: `GUILayout.Button("", GUILayout.Width(24), GUILayout.Height(24))` + `GUILayoutUtility.GetLastRect()` 조합이 strip-safe 인지. v0.7.2/v0.7.3 의 검증된 패턴은 `GUILayout.Label("", options)` + `GUILayoutUtility.GetRect(w, h, options)` 1-call. Button + GetLastRect 의 strip 위험은 §6 에서 명시 + impl 첫 smoke 검증.

**Fallback 패턴 (Button + GetLastRect strip 시)**: `GUILayoutUtility.GetRect(24, 24)` 로 자리 + rect 받음 → `Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)` 로 클릭 감지. 이 패턴도 strip 위험 — v0.7.4 Task 1 spike 의 일부로 검증.

#### 4.6.2 toolbar — ⓘ 토글

v0.7.3 toolbar 끝 (▲/▼ 다음) 에 신규 36px Button 추가:
```csharp
var prevColor = GUI.color;
if (_itemDetailPanel != null && _itemDetailPanel.Visible) GUI.color = Color.cyan;
if (GUILayout.Button("ⓘ 상세", GUILayout.Width(60))) {
    if (_itemDetailPanel != null) _itemDetailPanel.Visible = !_itemDetailPanel.Visible;
}
GUI.color = prevColor;
```

폭: v0.7.3 482px + Space 4 + 60 = 546px. ContainerPanel 800 폭 안에서 여유.

#### 4.6.3 Set*Rows 변경 (raw item paired)

기존:
```csharp
public void SetInventoryRows(List<ItemRow> rows, float maxWeight = 964f);
```

신규:
```csharp
public void SetInventoryRows(List<ItemRow> rows, List<object> rawItems, float maxWeight = 964f);
```

Plugin.cs 의 호출 site 도 같이 변경 — `ContainerOps.ReadInventoryRows()` 등이 raw item list 도 함께 반환하도록.

### 4.7 ItemDetailPanel — 신규 IMGUI window

**파일**: `src/LongYinRoster/UI/ItemDetailPanel.cs`. 패턴 = ContainerPanel 그대로 모방.

```csharp
public sealed class ItemDetailPanel
{
    public bool Visible { get; set; } = false;
    private Rect _rect;
    private const int WindowID = 0x4C593734;   // "LY74"
    private bool _rawExpanded = false;
    private Vector2 _scroll = Vector2.zero;
    private ContainerPanel _hostPanel = null!;   // back-reference for GetFocusedRawItem

    public void Init(ContainerPanel host, Config config) {
        _hostPanel = host;
        _rect = new Rect(config.ItemDetailX, config.ItemDetailY, config.ItemDetailWidth, config.ItemDetailHeight);
    }

    public void OnGUI() {
        if (!Visible) return;
        try {
            _rect = GUI.Window(WindowID, _rect, (GUI.WindowFunction)Draw, "");
        } catch (System.Exception ex) {
            Util.Logger.Warn($"ItemDetailPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Draw(int id) {
        try {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "Item 상세");
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X")) Visible = false;
            GUILayout.Space(DialogStyle.HeaderHeight);

            var raw = _hostPanel.GetFocusedRawItem();
            if (raw == null) DrawEmpty();
            else DrawDetails(raw);

            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        } catch (System.Exception ex) {
            Util.Logger.Warn($"ItemDetailPanel.Draw threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void DrawEmpty() {
        GUILayout.Space(60);
        GUILayout.Label("item 의 cell 을 클릭하세요");
    }

    private void DrawDetails(object raw) {
        // 1. header — cell + 이름 + (area #idx)
        DrawItemHeader(raw);
        GUILayout.Space(4);

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 130));

        // 2. curated 섹션
        var curated = ItemDetailReflector.GetCuratedFields(raw);
        if (curated.Count > 0) {
            GUILayout.Label("== 정보 ==");
            foreach (var (label, value) in curated) {
                GUILayout.Label($"{label}: {value}");
            }
            GUILayout.Space(8);
        }

        // 3. raw fields (접이식)
        var rawFields = ItemDetailReflector.GetRawFields(raw);
        var arrow = _rawExpanded ? "▼" : "▶";
        if (GUILayout.Button($"{arrow} Raw fields ({rawFields.Count})", GUILayout.Width(_rect.width - 20)))
            _rawExpanded = !_rawExpanded;
        if (_rawExpanded) {
            foreach (var (name, value) in rawFields) {
                GUILayout.Label($"  {name}: {value}");
            }
        }

        GUILayout.EndScrollView();
    }

    private void DrawItemHeader(object raw) {
        // ItemRow synthesize 또는 raw 에서 직접 reflection — Type/SubType/GradeOrder/QualityOrder/EnhanceLv/Equipped 추출
        // ItemCellRenderer.DrawAtRect 로 큰 cell (48×48?) 그리고 옆에 이름 + (area #idx) 텍스트
    }

    public Config FlushPosition(Config config) {
        // close 시 또는 release 시 _rect 좌표를 Config 에 저장
        config.ItemDetailX = _rect.x;
        config.ItemDetailY = _rect.y;
        config.ItemDetailWidth = _rect.width;
        config.ItemDetailHeight = _rect.height;
        return config;
    }
}
```

### 4.8 Config 신규 entry

`Config.cs` 에 추가:
```csharp
public ConfigEntry<float> ItemDetailX { get; private set; }
public ConfigEntry<float> ItemDetailY { get; private set; }
public ConfigEntry<float> ItemDetailWidth { get; private set; }
public ConfigEntry<float> ItemDetailHeight { get; private set; }
public ConfigEntry<bool>  ItemDetailDefaultOpen { get; private set; }   // 사용자 토글 후 next session default
```

Default values: x = (ContainerPanel.x default + 800 + 10), y = ContainerPanel.y default, width 380, height 500, open false.

### 4.9 Plugin.cs 변경

- `_itemDetailPanel` 인스턴스 신규 + Init
- ContainerPanel 의 `Set*Rows` 호출 site 에 `rawItems` 인자 추가
- `OnGUI()` 매 frame 호출 (1줄)
- F11 keydown handler 에서 ContainerPanel 닫을 때 ItemDetailPanel.Visible = false sync
- OnDestroy / quit 시 Position 영속 (Config flush)

### 4.10 IL2CPP / 성능 가드

- 새 IMGUI primitive 미도입 — v0.7.3 검증된 strip-safe 패턴만 (DrawTexture / Label / Button / TextField / Toggle / GetRect 또는 GetLastRect — Button+GetLastRect 검증은 §6 위험)
- ItemDetailPanel 매 frame reflection 호출 — focus item 1개 × 모든 sub-data 필드 (~30~50개) × 60fps = 2~3k reflection call/sec. C# reflection cache 가 처리 가능. 부담 시 cache layer (ItemDetailReflector 내부 `Dictionary<Type, FieldInfo[]>` 캐시)
- raw section collapsed 시 `GetRawFields` 호출 안 함 — perf 부담 ↓
- `GetCuratedFields` 는 항상 호출 — 카테고리별 ~5~8 field, 부담 적음

## 5. 신규/변경 모듈 (요약)

§4.3 의 7 모듈 (3 신규 + 4 변경). 본 spec §4.3 표 참고.

## 6. 위험·미지수

| 항목 | 위험 | 대응 |
|---|---|---|
| **spike 결과 작음** | 어떤 카테고리의 sub-data 가 의미 있는 필드 거의 없음 | curated 빈 list → raw fallback. 단계적 cover 로 보호 |
| **IL2CPP reflection 한계** | sub-data field 가 IL2CPP type 이라 unbox 실패 | `Convert.ToInt32`/`ToString` fallback (v0.7.2 검증). 실패 시 raw 섹션 type name + `<unreadable>` 표시 |
| **Cell Button + GetLastRect strip** | `GUILayout.Button("", options)` + `GUILayoutUtility.GetLastRect()` 조합이 v0.7.2 어디에서도 미사용 → strip 위험 | impl 첫 smoke 검증. strip 시 fallback = `GUILayoutUtility.GetRect(24, 24, options)` (v0.7.3 검증) + `Event.current` 으로 클릭 감지 (그것도 strip 시 spec §3.6 의 다른 fallback 검토) |
| **focus stale index** | 이동·삭제 후 focus.idx OOB | 매 frame `GetFocusedRawItem()` 진입 시 length 검증, OOB 시 focus null + ItemDetailPanel 빈 상태 |
| **F12 spike handler 잔존** | release 에 코드 남음 | release 직전 commit (Task 6) 에 [F12] handler 제거 명시 |
| **ItemDetailPanel 자체 strip 회귀** | 새 IMGUI 패턴 발견 | v0.7.3 검증된 strip-safe 패턴만 재사용. 새 primitive 도입 안 함. impl 첫 smoke 첫 task |
| **collection field 표시** | raw 섹션 List<int> 등 collection 표시 방식 | 1차: type + count 표시 (`List<int> attriValues: [count=8]`). 펼침은 v0.7.4.x 후보 |
| **2 panel 동시 drag** | ContainerPanel 과 ItemDetailPanel 두 window 충돌? | 별도 WindowID 사용, IMGUI 가 각자 독립 처리. 검증된 패턴 (ConfirmDialog 가 이미 다른 WindowID 사용) |
| **spike 의 sample 부족** | 사용자 인벤에 6 카테고리 모두 있는지 미지 | spike Task 0 시 사용자에게 확인. 없는 카테고리는 외부 디스크 컨테이너로 받아 sample 확보 (1회 협조) |

## 7. 테스트 전략

### 7.1 Unit (LongYinRoster.Tests, +12 추가 → 182/182)

**ItemDetailReflectorCuratedTests** (6):
- type=0 장비 → curated list 갖는 정상 매핑 (강화/착용/속성)
- type=2 단약·음식 → curated list (효과)
- type=3 비급 → curated list (학습 lv)
- type=4 보물 → 빈 list (미지원)
- type=5 재료 → 빈 list
- type=6 말 → 빈 list (또는 minimal — equiped 만)

**ItemDetailReflectorRawTests** (4):
- 모든 reflection 필드 enum
- IL2CPP wrapper meta 5개 필터 검증
- 비활성 sub-data wrapper 는 dump 안 함 (type=3 item 의 equipmentData 제외)
- null item → 빈 list

**ContainerPanelFocusTests** (2):
- `_focus` set / clear (cell 클릭 시뮬)
- OOB 자동 해제 (focused idx 의 row 가 사라지면 GetFocusedRawItem null + focus auto-clear)

### 7.2 인게임 smoke (6/6 minimum)

각 시나리오:
1. cell 클릭 시 focus 갱신, 외곽선 cyan 표시 (3-area 모두)
2. ⓘ 버튼으로 ItemDetailPanel 열기/닫기 — 처음 열 때 ContainerPanel 옆 default position
3. 카테고리별 curated 섹션 표시 (장비/비급/단약 1개씩 sample) — 한글 라벨 + 값 정상
4. Raw section 토글 (펼침/접힘) — 펼친 상태에서 모든 reflection 필드 표시 + IL2CPP meta 필터
5. → 이동·복사 후 focus 자동 해제 (item OOB), 다른 item 클릭 시 panel 새로 갱신
6. F11 닫기 시 ItemDetailPanel 도 같이 닫힘. 재오픈 시 position persist

## 8. 완료 기준

- ContainerPanel cell 클릭 → focus 변경 + 외곽선 cyan 표시 (3-area 모두)
- ⓘ 토글 버튼으로 ItemDetailPanel 열기/닫기, F11 sync 닫기
- 장비/비급/단약 3 카테고리 curated 섹션 정상 표시
- Raw fields 토글 + 모든 reflection 필드 표시 + IL2CPP meta 필터
- 이동·복사 후 focus 자동 해제 (item OOB → focus null)
- **182/182 unit tests PASS** (기존 170 + 신규 12)
- 인게임 smoke 6/6 PASS
- HANDOFF.md §6.B 갱신 (D-1 ✅, v0.7.4.x patch 후보 — 나머지 3 카테고리 curated, v0.7.7 후보 — Item editor)
- README 사용자 가이드에 ItemDetailPanel 사용법 1단락
- spike dump (`docs/superpowers/dumps/2026-05-XX-v0.7.4-subdata-spike.md`) 작성 + commit
- F12 spike handler 제거 (release 직전 commit)

## 9. release 후 contract

- v0.7.4 release tag + dist zip + VERSION bump
- HANDOFF.md update (§1, §6.B, §7 컨텍스트 압축본)
- 다음 sub-project = v0.7.5 (D-4 Item 한글화) 또는 v0.7.4.x patch (나머지 카테고리 curated)
- v0.7.7 후보 = Item editor (view-only 위에 setter reflection 추가, IL2CPP setter strip spike 다수 필요)
