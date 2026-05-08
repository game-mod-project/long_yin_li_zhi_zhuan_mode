# LongYinRoster v0.7.5.2 — Cell 가로 직사각형 + 한글 라벨 (patch)

**일시**: 2026-05-06
**baseline**: v0.7.5.1 (commit `bce0be4`) — 216/216 tests + smoke PASS, 합성어 한글화 cover
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §5.5 patch 컨벤션
**trigger**: 사용자 인게임 보고 — row 좌측 24×24 cell 의 카테고리 글리프가 한자 (装/书/药/食/宝/材/马) 라 한국어 사용자에게 직관성 떨어짐.
**design 결정**: 옵션 (α) 48×24 가로 직사각형 + 한글 라벨 (사용자 선택)

## 0. 한 줄 요약

`ItemCellRenderer` 의 cell 크기 24×24 → **48×24 가로 직사각형**. `CategoryGlyph.For()` 가 한자 → **한글 라벨** (장비/단약/음식/비급/보물/재료/말). 등급 색상 배경 + 우상단 quality 마름모 + 우하단 강화 +N + 좌하단 착 마커는 위치 그대로 유지 — cell 가운데에 한글 라벨 (MiddleCenter alignment).

## 1. 변경 파일 (3개 + 1 신규 test)

### 1.1 `src/LongYinRoster/Containers/CategoryGlyph.cs`

```csharp
public static string For(int type, int subType) => type switch
{
    0 => "장비",                              // Equipment (장비)
    2 => subType == 0 ? "단약" : "음식",      // Medicine / Food
    3 => "비급",                              // Book
    4 => "보물",                              // Treasure
    5 => "재료",                              // Material
    6 => "말",                                // Horse
    _ => "기타",                              // Other (type=1 등)
};
```

XML doc 주석도 갱신 — "한자 1자" → "한글 라벨 (1-2자)".

### 1.2 `src/LongYinRoster/UI/ItemCellRenderer.cs`

**default cell size**: `Draw(r, int size = 24)` → `Draw(r, int width = 48, int height = 24)` 또는 `int size = 24` 유지하면서 width 별도. v0.7.4 의 `DrawAtRect(r, Rect)` 는 caller 가 rect 명시하므로 변경 영향 없음 (caller 가 새 폭으로 rect 전달).

**Label centering**: 한글 라벨이 cell 가운데 정렬 — `GUIStyle` 신규 (MiddleCenter alignment) 또는 `GUI.Label(rect, text, GUI.skin.label)` 의 alignment 명시. 가장 단순:

```csharp
private static GUIStyle? _centerLabelStyle;
private static GUIStyle GetCenterLabelStyle()
{
    if (_centerLabelStyle == null)
    {
        _centerLabelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
    }
    return _centerLabelStyle;
}
```

`Draw` / `DrawAtRect` 의 중앙 카테고리 라벨 line:
```csharp
GUI.Label(rect, CategoryGlyph.For(r.Type, r.SubType), GetCenterLabelStyle());
```

**Marker positions** — 그대로 유지 (xMax-9 우상, xMax-18 우하, xMin+1 좌하). 48×24 에서 가운데 한글 라벨 (예: "장비" 약 24px 폭) 은 marker 코너와 자연스럽게 분리.

**Layout 시뮬레이션** (48×24):
```
+------------------------------+
|       장비          ◆       |  y=0~10 (상단)
|  착                  +10    |  y=10~24 (하단)
+------------------------------+
0    8        24      40    48
```
- 우상단 마름모 (39, 1, 8, 8) — 상단 라벨 + 마름모 자연 분리
- 우하단 +N (30, 10, 18, 14)
- 좌하단 착 (1, 10, 14, 14)
- 라벨 (0, 0, 48, 24) MiddleCenter — 한글 2자 약 24px 가운데 그려짐 (마커 코너 영역 침범 우려는 시각 배경 색 차이로 가독성 확보)

### 1.3 `src/LongYinRoster/UI/ContainerPanel.cs:475`

```csharp
// before
var cellRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));
// after
var cellRect = GUILayoutUtility.GetRect(48, 24, GUILayout.Width(48), GUILayout.Height(24));
```

(file 안 cell mouse hit-test 등 다른 cellRect 참조는 width 무관 — `cellRect.Contains(Event.current.mousePosition)` 그대로 작동.)

### 1.4 `src/LongYinRoster.Tests/CategoryGlyphTests.cs` (신규 file)

```csharp
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CategoryGlyphTests
{
    [Theory]
    [InlineData(0, 0, "장비")]
    [InlineData(0, 1, "장비")]   // 장비 subType 무관
    [InlineData(2, 0, "단약")]
    [InlineData(2, 1, "음식")]
    [InlineData(2, 5, "음식")]
    [InlineData(3, 0, "비급")]
    [InlineData(4, 0, "보물")]
    [InlineData(5, 0, "재료")]
    [InlineData(6, 0, "말")]
    [InlineData(1, 0, "기타")]
    [InlineData(99, 0, "기타")]
    public void For_ReturnsKoreanLabel(int type, int subType, string expected)
    {
        CategoryGlyph.For(type, subType).ShouldBe(expected);
    }
}
```

11 신규 case (Theory). 기존 ItemCellRendererHelperTests 는 BadgeText/EquippedMarker/GradeColor/QualityColor 만 다루며 변경 없음.

총 test: 216 → 227 (+11).

## 2. Test 변경

- `ItemCellRendererHelperTests.cs` — 변경 없음 (BadgeText/EquippedMarker/GradeColor/QualityColor 모두 라벨 매핑과 무관)
- 신규 `CategoryGlyphTests.cs` — 11 case (위 §1.4)

## 3. 인게임 Smoke

### 신규 4 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| S1 | ContainerPanel row toggle cell | 가로 직사각형 (48×24) + 가운데 한글 라벨 (장비/단약/음식/비급/보물/재료/말) |
| S2 | 카테고리별 라벨 매핑 | 무기 = 장비, 갑옷 = 장비, 비급 = 비급, 단약 = 단약, 음식 = 음식, 보물 = 보물, 재료 = 재료, 말 = 말 |
| S3 | Cell 시각 마커 회귀 | 등급 색상 배경, 우상단 마름모, 우하단 +N, 좌하단 착 모두 v0.7.3/v0.7.4 동일 |
| S4 | Cell 클릭 single-focus 동작 | v0.7.4 D-1 동작 그대로 (cellRect.Contains hit-test 작동) |

### 회귀 14 (v0.7.5 + v0.7.5.1)

- 한글화 row 라벨 / 검색 (한글/한자) / 정렬 / ItemDetailPanel header / curated / raw / 7 카테고리 ⓘ / 컨테이너 area 미지원 → 모두 PASS

총 18/18.

## 4. Release & Cycle

Single hotfix cycle:
1. spec (본 파일)
2. impl (3 file 수정 + 1 신규 test file)
3. smoke
4. release v0.7.5.2

명명: `v0.7.5.2` (메타 §5.5 patch 컨벤션). VERSION bump + README + HANDOFF + 메타 spec §2.2 Result 에 patch link 추가.

## 5. Out-of-scope

- Cell 폭 추가 옵션 (56/64 등) — 사용자 (α) 48 선택, fit 안 되면 후속 patch
- Marker (강화/착용) 위치 조정 — 현재 위치 유지
- 진짜 sprite 도입 (v0.8 후보)

## 6. Risk

- **한글 라벨 cell 안 fit** — 48×24 안에 한글 2자 + marker 3개. IMGUI default 폰트 12px 기준 한글 2자 ≈ 24px 가로, MiddleCenter 정렬로 cell 중앙. 마커 영역 (xMax-9~xMax, xMax-18~xMax y=10~24, xMin~xMin+15 y=10~24) 에 라벨 텍스트 겹침 가능 — 시각 배경 색상 차이로 가독성 확보. 만약 잘림/겹침 심하면 후속 patch (cell 폭 56 확장 또는 라벨 1자 약자).
- **카테고리 자동 분류 변경** — `ItemCategoryFilter.KoreanLabel` (기존 "장비"/"단약" 등) 과 `CategoryGlyph.For` (변경 후) 가 동일 한글 사용 — 통일성 OK.
- **비IL2CPP 환경 (xUnit unit test)** — `GUIStyle` / `GUI.skin` 은 Unity runtime 종속. unit test 는 `CategoryGlyph.For` 만 테스트 (라벨 매핑), `ItemCellRenderer.Draw` IMGUI 호출은 인게임 smoke 만.
