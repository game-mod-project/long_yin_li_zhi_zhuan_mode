# v0.7.3 — Item 시각 표시 풍부화 (D-2 scope 재정의)

**작성일**: 2026-05-03
**Sub-project 위치**: v0.7.2 (D-3 검색·정렬) 후속 / D 계열 두 번째 release
**Baseline**: v0.7.2 (`main` HEAD — 133/133 tests PASS)
**브레인스토밍 결과**: HANDOFF §6.B 의 "D-2 = 아이콘 그리드 (sprite reference + IMGUI grid)" 가 brainstorming 도중 두 번 재정의됨 — (1) sprite source 가 v0.7.2 spike 에서 미발견 → placeholder-first 방향 (Q2=B), (2) 정사각형 grid cell 로는 사용자가 요구하는 full ItemName 표시 불가 → grid mode 자체 폐기 (Q4=C-2). 본 release 의 본질은 **"각 row 의 좌측에 24×24 placeholder cell 추가 — GradeColor 배경 / QualityColor 마름모 / CategoryGlyph 중앙 한자 / +N 강화 / 착 마커"** 로 List mode 풍부화.

---

## 1. 목적

ContainerPanel (인벤·창고·외부 디스크 컨테이너 3-area) 의 각 item row 옆에 등급·품질·카테고리·강화·착용중 정보를 시각화한 24×24 placeholder cell 을 추가해, item 식별 속도와 인지 부하를 개선한다.

진짜 game sprite 는 IL2CPP 환경의 sprite asset 접근 미지수 + v0.7.2 spike 에서 ItemData 의 `iconID` 류 field 미발견 두 가지 이유로 본 sub-project 에서는 도입하지 않는다. v0.8 또는 별도 후속 sub-project 에서 진짜 sprite 가 도입될 때 본 release 의 `ItemCellRenderer` 가 baseline 이 되어 placeholder rendering 만 sprite blit 으로 교체된다.

## 2. 사용자 보고 + 확정 scope

브레인스토밍 Q1~Q4 결과:

### 2.1 시각 모델 (Q1 = C → Q4 = C-2)
초기 선택: 정사각형 grid mode + list-with-thumbnail mode 의 hybrid 토글. Q4 에서 사용자가 "full ItemName 이 보여야 한다, 한 종류 안에서도 이름이 중복되는 경우가 많아 일부만으로는 식별 불가" 로 정정 → 64×64 정사각형 grid 와 충돌 → grid mode 자체 폐기 / list 풍부화로 전환.

### 2.2 sprite 정책 (Q2 = B)
Placeholder-first. 진짜 sprite 는 본 release 비범위. 등급 색상 배경 + 품질 마름모 + 카테고리 한자 1자 + 강화/착용 badge 의 IMGUI placeholder 만으로 시각 풍부화 달성.

### 2.3 stack 표시 (Q3 = B)
Stack count badge 미구현. 게임 자체가 1 ItemData = 1 stack 으로 처리 — 같은 type 이어도 내부 속성값 (강화 / 품질 / sub-data) 이 달라 별도 entry 로 표시. 갯수 표시는 list-level (`인벤토리 (180개, 50.0/964.0kg)`) 에서만.

### 2.4 영속성
세션 휘발 — F11 close 시 이미 v0.7.2 SearchSortState 가 초기화되며, 본 release 는 추가 상태 미보유 (cell rendering 은 stateless).

### 2.5 비범위 (Out of scope)
- 진짜 game sprite (`Resources.Load<Sprite>` / AssetBundle / Addressables) — v0.8+
- 정사각형 grid mode / 토글 (Q1 의 C 옵션이 Q4 에서 폐기)
- Stack count badge — Q3
- Cell 자체의 클릭 / 드래그·드롭 / hover tooltip — 본 release 는 view-only placeholder
- Grade/Quality 외 sub-data badge (속성·내구도 등) — v0.7.4 D-1
- SlotListPanel (캐릭터 관리) cell — ContainerPanel 만 대상

## 3. 후속 sub-project 매핑 (HANDOFF §6.B 갱신)

본 release 가 `D-2 = 아이콘 그리드` 의 scope 를 재정의하므로 HANDOFF 의 다음 줄을 변경:

| Version | 카테고리 (변경 후) | 변경 사유 |
|---|---|---|
| v0.7.2 | D-3 검색·정렬 (release 완료) | unchanged |
| **v0.7.3** | **D-2 Item 시각 표시 풍부화 (placeholder cell)** | "아이콘 그리드 (sprite reference)" → "placeholder cell" 로 재정의. 진짜 sprite 는 v0.8+ |
| v0.7.4 | D-1 Item 상세 panel | unchanged. ItemCellRenderer 의 cell 표현 패턴 + sub-data wrapper reflection 합류 |
| v0.7.5 | D-4 Item 한글화 | unchanged. 한글화 후 row text 가 한글이 되어 cell 의 카테고리 한자와 자연스러운 대비 |
| v0.7.6 | 설정 panel | unchanged |
| v0.7.7~9 | Apply preview / Slot diff / NPC | unchanged |
| **v0.8 (후보)** | **진짜 sprite 도입** (별도 sub-project) | 본 release 가 baseline. ItemCellRenderer 의 placeholder block 만 sprite blit 으로 교체 |

## 4. 디자인 결정

### 4.1 Approach (브레인스토밍 채택)

**List mode 풍부화 — row 마다 24×24 placeholder cell prefix**.

1. v0.7.2 의 `DrawItemList` row 구조 (`Toggle(label)` 1줄) 를 `BeginHorizontal → ItemCellRenderer.Draw(24) → Toggle(label) → EndHorizontal` 로 감싼다.
2. Cell 은 stateless — `ItemRow` 데이터만 받아 GUI op 로 그림. cache 없음.
3. v0.7.2 의 SearchSortState / ContainerView / GradeColor / 카테고리 탭 / row 텍스트 색상 모두 그대로 유지.
4. ContainerView cache 도 변경 없음 — cell rendering 은 cached row 시퀀스를 그대로 따라감.

**탈락 후보**:
- 정사각형 grid mode 토글 (Q1 의 C 채택 후 Q4 에서 full name 표시 요구로 폐기)
- 카드 레이아웃 (가로 ~190 / 세로 ~96×110 cell 에 full name) — 사실상 list 의 변형이라 별 가치 없음 + spec 복잡도 +
- 진짜 sprite (Q2=B 결정으로 본 release 비범위)

### 4.2 데이터 모델 — 변경 없음

`ContainerPanel.ItemRow` (v0.7.2 시점 11 필드) 그대로 사용:

| 필드 (v0.7.2) | cell 에서의 역할 |
|---|---|
| `Type`, `SubType` | `CategoryGlyph.For()` → 중앙 한자 1자 |
| `GradeOrder` | `GradeColor()` → cell 배경 (alpha ~0.6) |
| `QualityOrder` | `QualityColor()` → 우상단 8×8 마름모 |
| `EnhanceLv` | 우하단 `+N` 텍스트 (N>0 일 때만) |
| `Equipped` | 좌하단 `착` 텍스트 (true 일 때만) |
| `Index`/`Name`/`NameRaw`/`Weight`/`CategoryKey` | cell 미사용. row label / sort 유지 |

count 필드 미추가 (§2.3).

### 4.3 신규 / 변경 모듈

| 모듈 | 위치 | 역할 | 신규/변경 |
|---|---|---|---|
| `CategoryGlyph` | `src/LongYinRoster/Containers/CategoryGlyph.cs` | type/subType → 한자 1자 (`装`/`书`/`药`/`食`/`宝`/`材`/`马`/`·`) | **신규** |
| `ItemCellRenderer` | `src/LongYinRoster/UI/ItemCellRenderer.cs` | strip-safe 24×24 IMGUI cell — Box 배경 + GUI.color + GUI.Label/Box 절대좌표 overlay. `public static GradeColor` / `QualityColor` 6단계 hex 매핑의 단일 source 도 보유 (ContainerPanel 의 row 텍스트 색상도 여기서 가져감) | **신규** |
| `ContainerPanel.GradeColor` | `src/LongYinRoster/UI/ContainerPanel.cs` (line 396) | **제거** — `ItemCellRenderer.GradeColor` 로 호출 변경 (DrawItemList 의 `GUI.color = GradeColor(...)` → `ItemCellRenderer.GradeColor(...)`). v0.7.2 의 동작 그대로 보존, source 위치만 이동 | **변경 (제거 + 호출 site 이동)** |
| `ContainerPanel.DrawItemList` | 동상 | row 마다 `BeginHorizontal` → `ItemCellRenderer.Draw(24)` → `Toggle(label)` → `EndHorizontal` 로 변경. row 텍스트 색상 `GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder)` 로 호출 갱신 | **변경** |

**v0.7.2 자산 100% 보존**: `SearchSortState`, `SortKey`, `SearchSortToolbar`, `ContainerView`, `ItemReflector`, `ItemRow`, `ItemCategoryFilter`, `ContainerRowBuilder`, `GradeColor`, 카테고리 탭, 4-key sort, 검색 box, ▲/▼ 토글 모두 변경 없음.

**예상 코드 증분**: `ContainerPanel.cs` +30~50줄, `ItemCellRenderer.cs` 신규 ~80줄, `CategoryGlyph.cs` 신규 ~20줄. 총 ~130줄. ContainerPanel.cs 가 v0.7.2 시점 425줄 → v0.7.3 후 475줄 예상 (600줄 임계 이하).

### 4.4 `CategoryGlyph` 매핑

```csharp
namespace LongYinRoster.Containers;

public static class CategoryGlyph {
    public static string For(int type, int subType) => type switch {
        0 => "装",   // Equipment (subType 별 세분 = v0.7.4 D-1)
        2 => subType == 0 ? "药" : "食",   // Medicine / Food
        3 => "书",   // Book
        4 => "宝",   // Treasure
        5 => "材",   // Material
        6 => "马",   // Horse
        _ => "·",    // Other (type=1 등 미분류)
    };
}
```

장비 subType 세분 (무기/갑옷/투구/신발/장신구) 은 v0.7.4 D-1 시점에 정밀화. 본 release 는 카테고리 1자 + cell 의 다른 마커들로 구분.

### 4.5 `ItemCellRenderer` strip-safe 패턴

```csharp
namespace LongYinRoster.UI;

public static class ItemCellRenderer {
    /// <summary>
    /// 24×24 placeholder cell. GUILayout.Box 1번으로 자리 잡고 GUILayoutUtility.GetLastRect()
    /// 로 rect 받아 GUI 절대좌표 overlay. strip-safe (default skin overload, GUIStyle ctor 회피).
    /// </summary>
    public static void Draw(ContainerPanel.ItemRow r, int size = 24) {
        var prevColor = GUI.color;

        // 1. 배경 — GradeColor (alpha ~0.6 으로 row 텍스트 강조 보존)
        var bg = GradeBackground(r.GradeOrder);
        GUI.color = bg;
        GUILayout.Box("", GUILayout.Width(size), GUILayout.Height(size));
        var rect = GUILayoutUtility.GetLastRect();
        GUI.color = prevColor;

        // 2. 중앙 카테고리 한자 (Label 은 strip 안 됨)
        GUI.Label(rect, CategoryGlyph.For(r.Type, r.SubType));

        // 3. 우상단 품질 마름모 (8×8 Box, QualityColor)
        if (r.QualityOrder >= 0) {
            GUI.color = QualityColor(r.QualityOrder);
            GUI.Box(new Rect(rect.xMax - 9, rect.yMin + 1, 8, 8), "");
            GUI.color = prevColor;
        }

        // 4. 우하단 강화 (있을 때만)
        if (r.EnhanceLv > 0)
            GUI.Label(new Rect(rect.xMax - 18, rect.yMax - 14, 18, 14), $"+{r.EnhanceLv}");

        // 5. 좌하단 착용중 (있을 때만)
        if (r.Equipped)
            GUI.Label(new Rect(rect.xMin + 1, rect.yMax - 14, 14, 14), "착");
    }

    /// <summary>
    /// 6단계 grade hex 매핑의 단일 source. ContainerPanel.DrawItemList 의 row 텍스트
    /// 색상 (`GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder)`) 도 여기서 가져감.
    /// v0.7.2 의 ContainerPanel.GradeColor (private) 는 본 release 에서 제거되고 이쪽으로 일원화.
    /// </summary>
    public static Color GradeColor(int g) => g switch {
        0 => new Color(0.61f, 0.64f, 0.69f),    // 회색 #9CA3AF
        1 => new Color(0.13f, 0.77f, 0.37f),    // 녹 #22C55E
        2 => new Color(0.22f, 0.74f, 0.97f),    // 하늘 #38BDF8
        3 => new Color(0.66f, 0.33f, 0.97f),    // 보라 #A855F7
        4 => new Color(0.98f, 0.45f, 0.09f),    // 오렌지 #F97316
        5 => new Color(0.94f, 0.27f, 0.27f),    // 빨강 #EF4444
        _ => Color.white,
    };

    private static Color GradeBackground(int g) {
        var c = GradeColor(g);
        c.a = 0.6f;   // row 텍스트와 시각 차별화
        return c;
    }

    public static Color QualityColor(int q) => q switch {
        0 => new Color(0.61f, 0.64f, 0.69f),    // 회색 #9CA3AF
        1 => new Color(0.13f, 0.77f, 0.37f),    // 녹 #22C55E
        2 => new Color(0.22f, 0.74f, 0.97f),    // 하늘 #38BDF8
        3 => new Color(0.66f, 0.33f, 0.97f),    // 보라 #A855F7
        4 => new Color(0.98f, 0.45f, 0.09f),    // 오렌지 #F97316
        5 => new Color(0.94f, 0.27f, 0.27f),    // 빨강 #EF4444
        _ => Color.white,
    };

    // 단위 테스트용 — IMGUI 자체 테스트 대신 helper logic 만 검증
    public static string BadgeText(int enhanceLv) => enhanceLv > 0 ? $"+{enhanceLv}" : "";
    public static string EquippedMarker(bool equipped) => equipped ? "착" : "";
}
```

`GUI.Box(Rect, "")` strip 위험 — impl 첫 smoke 때 검증. strip 시 fallback 패턴: `GUI.Label(Rect, "")` + GUI.color (Label 은 v0.7.2 검증됨) 또는 `GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture)` (단 EditorGUIUtility 은 game 빌드에서 부재 — `Texture2D.whiteTexture` 사용). 위험 §6.

### 4.6 `ContainerPanel.DrawItemList` 변경

```csharp
private void DrawItemList(List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height) {
    scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
    var prevColor = GUI.color;
    foreach (var r in rows) {
        if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;

        GUILayout.BeginHorizontal();
        ItemCellRenderer.Draw(r, size: 24);
        GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder);   // v0.7.2 의 ContainerPanel.GradeColor → 단일 source 로 이동
        bool was = checks.Contains(r.Index);
        bool now = GUILayout.Toggle(was, BuildLabel(r));
        GUI.color = prevColor;
        GUILayout.EndHorizontal();

        if (now && !was) checks.Add(r.Index);
        if (!now && was) checks.Remove(r.Index);
    }
    GUILayout.EndScrollView();
}
```

`ItemCellRenderer.GradeColor` 의 6단계 hex 매핑값은 v0.7.2 `ContainerPanel.GradeColor` 와 동일 — source 위치만 ItemCellRenderer 로 이동, 동작 보존. cell 배경은 `GradeBackground` (alpha 0.6) 로 row 텍스트 색상보다 약하게 — 텍스트의 시각 강조 보존.

### 4.7 row 높이 / ScrollView

- v0.7.2 row ~22px → v0.7.3 row ~28px (cell 24 + padding 4)
- 인벤 180 items × 28 = 5040px → ScrollView 높이 220 안에서 7~8 row 동시 보임
- window (800×760) / 좌우 column (390/390) / scroll 영역 (220/220/500) 모두 변경 없음

### 4.8 IL2CPP / 성능 가드

- `GUILayoutUtility.GetLastRect()`: v0.7.2 / v0.7.0.1 에서 사용 중 (검증됨)
- `GUI.Label(Rect, string)`, `GUI.Box(Rect, "")`: default skin overload — strip-safe (단 Box 는 §6 에서 검증)
- `GUI.color` 변경 후 복원 — `prevColor` 패턴 (v0.7.2 와 동일)
- BeginHorizontal/EndHorizontal 추가 — row 당 1회. 200 items × 60fps = 12k op/sec → IL2CPP 부담 가능 but 검증된 IMGUI primitive 라 우선 진행. perf 문제 발견 시 fallback = `GUI.color` + `GUI.Label` 만으로 cell 그리고 `GUILayout.Space(28)` 로 자리만 잡기

## 5. 테스트 전략

### 5.1 Unit (LongYinRoster.Tests, +6 추가 → 139/139)

- `CategoryGlyphTests` (3):
  - type 0/2/3/4/5/6 매핑 (장비/단약·음식/비급/보물/재료/말)
  - subType 분기 (type=2, subType=0 → "药" / subType=1 → "食")
  - 미분류 type → "·"
- `ItemCellRendererHelperTests` (3):
  - `BadgeText(enhanceLv)`: 0 → "", 1 → "+1", 5 → "+5", -1 → ""
  - `EquippedMarker(equipped)`: true → "착", false → ""
  - `QualityColor(q)`: 0~5 + -1 (-1 → white)

### 5.2 인게임 smoke (5/5 minimum)

각 시나리오 인벤·창고·컨테이너 3-area 모두 확인:
1. cell 배경이 등급별로 6단계 색상 (회색→녹→하늘→보라→오렌지→빨강) 정상 구분
2. 우상단 마름모가 품질별로 6단계 색상 정상 구분
3. 중앙 한자가 카테고리별 1자 정상 (장비=装, 비급=书, 단약=药, 음식=食, 보물=宝, 재료=材, 말=马)
4. 강화 `+N` badge 와 `착` 마커가 해당 row 만 표시
5. 검색·정렬·카테고리 탭 (v0.7.2) 토글 후에도 cell 동기화 + 60fps frame drop 없음

## 6. 위험·미지수

| 항목 | 위험 | 대응 |
|---|---|---|
| **`GUI.Box(Rect, "")` IL2CPP strip** | cell 배경 사각형이 안 그려질 수 있음 | impl 첫 smoke 때 검증. strip 시 fallback = `GUI.Label(Rect, "")` (v0.7.2 검증) 또는 `GUI.DrawTexture(rect, Texture2D.whiteTexture)` |
| **`GUILayoutUtility.GetLastRect()` perf** | row 당 1회 호출 × 200 items × 60fps = 12k call/sec | v0.7.2 / v0.7.0.1 에서 비슷한 패턴 작동 중 (검증됨). 부담 시 cell 을 절대좌표로 그리고 `GUILayout.Space(28)` 로 자리만 잡기 |
| **CategoryGlyph 한자 가독성** | 24×24 안에 한자 1자가 작게 보일 수 있음 | font 변경은 GUIStyle ctor strip 위험. 안전책: cell 28×28 또는 32×32 로 확대 (impl 시 시각 평가). 우선 24 로 진행 후 부족 시 28 |
| **착 + +N 동시 표시 중복** | 우하단 + 좌하단 동시 차면 cell 비좁음 | 14×14 작은 box 로 24×24 안 가능. 그래도 겹치면 cell 28~32 확대 |
| **alpha 0.6 cell 배경 시각 강도** | 너무 약하면 6단계 구분 흐려짐, 너무 강하면 row 텍스트와 충돌 | impl smoke 시 0.5/0.6/0.7 비교. 최종 hex 는 plan 단계 결정 |
| **placeholder 가 진짜 sprite 처럼 보일 가능성** | 사용자가 실제 sprite 라 오해 | 의도된 시각 (등급=배경, 품질=마름모) — 인게임 sprite 와 색상 매핑 일치. README 에 1단락 명시 |

## 7. 완료 기준

- 인벤·창고·컨테이너 3-area 모두 row 좌측 24×24 cell 표시
- 등급 6단계 배경 + 품질 6단계 마름모 + 카테고리 한자 + 강화/착 badge 동작
- 검색·정렬·카테고리 탭 (v0.7.2 자산) 100% 보존
- **139/139 unit tests PASS** (기존 133 + 신규 6)
- 인게임 smoke 5/5 PASS
- HANDOFF.md §6.B 의 v0.7.3 (D-2) 항목을 "아이콘 그리드" → "Item 시각 표시 풍부화 (placeholder cell)" 로 갱신 + v0.8 후보로 "진짜 sprite 도입" 추가
- README 사용자 가이드에 cell 배경/마름모/한자 의미 1단락

## 8. release 후 contract

- v0.7.3 release tag + dist zip + VERSION bump
- HANDOFF.md update (§1, §6.B, §7 컨텍스트 압축본)
- 다음 sub-project = v0.7.4 (D-1 Item 상세 panel) — `ItemCellRenderer` 의 cell 표현 패턴이 panel 의 등급/품질 표시 영역에 재사용됨
- v0.8 또는 별도 sub-project 에서 진짜 sprite 도입 시 `ItemCellRenderer.Draw` 의 GradeBackground / CategoryGlyph 부분만 sprite blit 으로 교체하면 됨 — cell rect / badge layout 보존
