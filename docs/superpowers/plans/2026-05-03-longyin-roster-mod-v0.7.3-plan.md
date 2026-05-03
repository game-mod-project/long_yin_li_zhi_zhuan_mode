# v0.7.3 — Item 시각 표시 풍부화 (D-2) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ContainerPanel 의 인벤·창고·컨테이너 3-area 각 row 좌측에 24×24 placeholder cell (등급 배경 / 품질 마름모 / 카테고리 한자 / 강화·착 badge) 을 추가해 item 시각 식별 속도를 높인다.

**Architecture:** v0.7.2 의 List 표시·검색·정렬·카테고리 탭 자산 100% 보존. 신규 `CategoryGlyph` (type/subType → 한자 1자) + `ItemCellRenderer` (strip-safe IMGUI cell + 색상 6단계 단일 source) 두 모듈 추가. `ContainerPanel.DrawItemList` 의 row 마다 `BeginHorizontal → ItemCellRenderer.Draw(24) → Toggle(label) → EndHorizontal` 로 변경. v0.7.2 의 `ContainerPanel.GradeColor` (private) 는 제거되고 `ItemCellRenderer.GradeColor` (public) 로 일원화. 진짜 game sprite 는 비범위 (v0.8+ 별도).

**Tech Stack:** C# / .NET (BepInEx 6 IL2CPP), UnityEngine.GUI/GUILayout (default skin overload only — strip-safe), xUnit + Shouldly 단위 테스트.

**Spec:** [docs/superpowers/specs/2026-05-03-longyin-roster-mod-v0.7.3-design.md](../specs/2026-05-03-longyin-roster-mod-v0.7.3-design.md)

---

## Task 1: CategoryGlyph 매핑 (신규 모듈)

**Files:**
- Create: `src/LongYinRoster/Containers/CategoryGlyph.cs`
- Test: `src/LongYinRoster.Tests/CategoryGlyphTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/LongYinRoster.Tests/CategoryGlyphTests.cs`:

```csharp
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class CategoryGlyphTests
{
    [Theory]
    [InlineData(0, 0, "装")]   // Equipment
    [InlineData(0, 4, "装")]   // Equipment 모든 subType
    [InlineData(2, 0, "药")]   // Medicine (subType=0)
    [InlineData(2, 1, "食")]   // Food (subType≥1)
    [InlineData(2, 2, "食")]
    [InlineData(3, 0, "书")]   // Book
    [InlineData(4, 0, "宝")]   // Treasure
    [InlineData(5, 0, "材")]   // Material
    [InlineData(6, 0, "马")]   // Horse
    [InlineData(6, 1, "马")]
    public void For_KnownTypes_ReturnsCategoryGlyph(int type, int subType, string expected)
    {
        CategoryGlyph.For(type, subType).ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(99, 0)]
    [InlineData(-1, 0)]
    public void For_UnknownType_ReturnsDot(int type, int subType)
    {
        CategoryGlyph.For(type, subType).ShouldBe("·");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `src/`:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~CategoryGlyphTests
```
Expected: 컴파일 실패 — `CategoryGlyph` 타입 미존재 (CS0246 또는 비슷).

- [ ] **Step 3: Write the minimal implementation**

Create `src/LongYinRoster/Containers/CategoryGlyph.cs`:

```csharp
namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.3 D-2 — type/subType → 카테고리 한자 1자. ItemCellRenderer 의 placeholder cell
/// 중앙 글자에 사용. 장비 subType 세분 (무기/갑옷/투구/신발/장신구) 은 v0.7.4 D-1 시점 정밀화.
/// </summary>
public static class CategoryGlyph
{
    public static string For(int type, int subType) => type switch
    {
        0 => "装",                              // Equipment
        2 => subType == 0 ? "药" : "食",        // Medicine / Food
        3 => "书",                              // Book
        4 => "宝",                              // Treasure
        5 => "材",                              // Material
        6 => "马",                              // Horse
        _ => "·",                               // Other (type=1 등 미분류)
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run from `src/`:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~CategoryGlyphTests
```
Expected: 13 tests PASS (10 Theory + 3 Theory).

- [ ] **Step 5: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Containers/CategoryGlyph.cs src/LongYinRoster.Tests/CategoryGlyphTests.cs
git commit -m "feat(containers): v0.7.3 D-2 — CategoryGlyph type/subType → 한자 1자 매핑"
```

---

## Task 2: ItemCellRenderer 의 helper logic + 색상 단일 source (신규 모듈)

**Files:**
- Create: `src/LongYinRoster/UI/ItemCellRenderer.cs` (helper part — IMGUI 부분은 Task 3)
- Test: `src/LongYinRoster.Tests/ItemCellRendererHelperTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/LongYinRoster.Tests/ItemCellRendererHelperTests.cs`:

```csharp
using LongYinRoster.UI;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemCellRendererHelperTests
{
    [Theory]
    [InlineData(0, "")]
    [InlineData(-1, "")]
    [InlineData(1, "+1")]
    [InlineData(3, "+3")]
    [InlineData(15, "+15")]
    public void BadgeText_RendersOnlyWhenPositive(int enhanceLv, string expected)
    {
        ItemCellRenderer.BadgeText(enhanceLv).ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, "착")]
    [InlineData(false, "")]
    public void EquippedMarker_RendersOnlyWhenTrue(bool equipped, string expected)
    {
        ItemCellRenderer.EquippedMarker(equipped).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 0.61f, 0.64f, 0.69f)]    // 회색
    [InlineData(1, 0.13f, 0.77f, 0.37f)]    // 녹
    [InlineData(2, 0.22f, 0.74f, 0.97f)]    // 하늘
    [InlineData(3, 0.66f, 0.33f, 0.97f)]    // 보라
    [InlineData(4, 0.98f, 0.45f, 0.09f)]    // 오렌지
    [InlineData(5, 0.94f, 0.27f, 0.27f)]    // 빨강
    public void GradeColor_Returns6StepHex(int grade, float r, float g, float b)
    {
        var c = ItemCellRenderer.GradeColor(grade);
        c.r.ShouldBe(r, 0.001f);
        c.g.ShouldBe(g, 0.001f);
        c.b.ShouldBe(b, 0.001f);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(99)]
    public void GradeColor_OutOfRange_ReturnsWhite(int grade)
    {
        ItemCellRenderer.GradeColor(grade).ShouldBe(Color.white);
    }

    [Theory]
    [InlineData(0, 0.61f, 0.64f, 0.69f)]    // 회색 (잔품)
    [InlineData(5, 0.94f, 0.27f, 0.27f)]    // 빨강 (극품)
    public void QualityColor_Returns6StepHex(int quality, float r, float g, float b)
    {
        var c = ItemCellRenderer.QualityColor(quality);
        c.r.ShouldBe(r, 0.001f);
        c.g.ShouldBe(g, 0.001f);
        c.b.ShouldBe(b, 0.001f);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void QualityColor_OutOfRange_ReturnsWhite(int quality)
    {
        ItemCellRenderer.QualityColor(quality).ShouldBe(Color.white);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `src/`:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ItemCellRendererHelperTests
```
Expected: 컴파일 실패 — `ItemCellRenderer` 타입 미존재.

- [ ] **Step 3: Write the minimal implementation (helper part)**

Create `src/LongYinRoster/UI/ItemCellRenderer.cs`:

```csharp
using LongYinRoster.Containers;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.3 D-2 — strip-safe 24×24 IMGUI placeholder cell + 색상 6단계 단일 source.
///
/// 색상 단일 source (Grade/Quality 6단계 hex) 가 본 class 에 통합. v0.7.2 의
/// `ContainerPanel.GradeColor` (private static) 는 본 release 에서 제거되고 row
/// 텍스트 색상도 `ItemCellRenderer.GradeColor` 호출로 대체.
///
/// IMGUI Draw method 는 Task 3 에서 추가 (본 step 은 helper 만).
/// </summary>
public static class ItemCellRenderer
{
    /// <summary>
    /// v0.7.2 색상과 동일 hex (회색→녹→하늘→보라→오렌지→빨강). 모든 ContainerPanel
    /// row 텍스트와 ItemCellRenderer cell 배경의 단일 source.
    /// </summary>
    public static Color GradeColor(int grade) => grade switch
    {
        0 => new Color(0.61f, 0.64f, 0.69f),    // 회색  #9CA3AF (열악/잔품 baseline)
        1 => new Color(0.13f, 0.77f, 0.37f),    // 녹   #22C55E
        2 => new Color(0.22f, 0.74f, 0.97f),    // 하늘 #38BDF8
        3 => new Color(0.66f, 0.33f, 0.97f),    // 보라 #A855F7
        4 => new Color(0.98f, 0.45f, 0.09f),    // 오렌지 #F97316
        5 => new Color(0.94f, 0.27f, 0.27f),    // 빨강 #EF4444
        _ => Color.white,
    };

    /// <summary>
    /// 품질 6단계 hex — Grade 와 같은 팔레트 (게임 내 색상 매핑이 grade/quality 동일).
    /// 마름모 (cell 우상단 8×8) 색상에 사용.
    /// </summary>
    public static Color QualityColor(int quality) => quality switch
    {
        0 => new Color(0.61f, 0.64f, 0.69f),
        1 => new Color(0.13f, 0.77f, 0.37f),
        2 => new Color(0.22f, 0.74f, 0.97f),
        3 => new Color(0.66f, 0.33f, 0.97f),
        4 => new Color(0.98f, 0.45f, 0.09f),
        5 => new Color(0.94f, 0.27f, 0.27f),
        _ => Color.white,
    };

    /// <summary>강화 lv > 0 일 때 "+N", 아니면 "". 단위 테스트 가능한 helper.</summary>
    public static string BadgeText(int enhanceLv) => enhanceLv > 0 ? $"+{enhanceLv}" : "";

    /// <summary>착용중일 때 "착", 아니면 "". 단위 테스트 가능한 helper.</summary>
    public static string EquippedMarker(bool equipped) => equipped ? "착" : "";
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run from `src/`:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter FullyQualifiedName~ItemCellRendererHelperTests
```
Expected: 18 tests PASS (5 BadgeText + 2 EquippedMarker + 6 GradeColor 6step + 3 GradeColor outofrange + 2 QualityColor 6step + 2 QualityColor outofrange = 20 actually — count Theory rows). 모두 PASS.

- [ ] **Step 5: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/UI/ItemCellRenderer.cs src/LongYinRoster.Tests/ItemCellRendererHelperTests.cs
git commit -m "feat(ui): v0.7.3 D-2 — ItemCellRenderer helper + 색상 6단계 단일 source"
```

---

## Task 3: ItemCellRenderer.Draw IMGUI 메서드 추가

IMGUI 자체는 단위 테스트 불가 (UnityEngine.GUI 호출이 game runtime 필요). Task 5 smoke 에서 시각 검증.

**Files:**
- Modify: `src/LongYinRoster/UI/ItemCellRenderer.cs` (Task 2 에서 만든 파일)

- [ ] **Step 1: Add Draw method**

`ItemCellRenderer` class 안에 `Draw` 와 `GradeBackground` 추가. Task 2 에서 정의한 `GradeColor` 위에 추가:

```csharp
    /// <summary>
    /// 24×24 (또는 size 지정) placeholder cell.
    /// - 배경: GradeColor (alpha 0.6 — row 텍스트 색상보다 약화)
    /// - 중앙: CategoryGlyph 한자 1자
    /// - 우상단 8×8: QualityColor 마름모 (Box, QualityOrder ≥ 0 일 때만)
    /// - 우하단: 강화 +N (EnhanceLv > 0 일 때만)
    /// - 좌하단: 착 (Equipped 일 때만)
    /// strip-safe — default skin overload (GUI.Label/GUI.Box) 만 사용.
    /// GUIStyle ctor 회피.
    /// </summary>
    public static void Draw(ContainerPanel.ItemRow r, int size = 24)
    {
        var prevColor = GUI.color;

        // 1. 배경 — GradeColor (alpha 0.6)
        GUI.color = GradeBackground(r.GradeOrder);
        GUILayout.Box("", GUILayout.Width(size), GUILayout.Height(size));
        var rect = GUILayoutUtility.GetLastRect();
        GUI.color = prevColor;

        // 2. 중앙 카테고리 한자
        GUI.Label(rect, CategoryGlyph.For(r.Type, r.SubType));

        // 3. 우상단 품질 마름모 (8×8 Box, alpha 1.0)
        if (r.QualityOrder >= 0)
        {
            GUI.color = QualityColor(r.QualityOrder);
            GUI.Box(new Rect(rect.xMax - 9, rect.yMin + 1, 8, 8), "");
            GUI.color = prevColor;
        }

        // 4. 우하단 강화 (있을 때만)
        var badge = BadgeText(r.EnhanceLv);
        if (!string.IsNullOrEmpty(badge))
            GUI.Label(new Rect(rect.xMax - 18, rect.yMax - 14, 18, 14), badge);

        // 5. 좌하단 착용중 (있을 때만)
        var marker = EquippedMarker(r.Equipped);
        if (!string.IsNullOrEmpty(marker))
            GUI.Label(new Rect(rect.xMin + 1, rect.yMax - 14, 14, 14), marker);
    }

    private static Color GradeBackground(int grade)
    {
        var c = GradeColor(grade);
        c.a = 0.6f;
        return c;
    }
```

- [ ] **Step 2: Verify build (no test for IMGUI)**

Run from `src/`:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet build LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: Build succeeded, 0 errors. (warnings 가 있으면 IDE/Roslyn 분석 — 무시 가능 단 CS warning 0 권장)

- [ ] **Step 3: Re-run all unit tests to confirm no regression**

Run from `src/`:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 기존 133 + 신규 (Task 1 의 13 + Task 2 의 ~20) = 약 166 tests PASS. (정확한 갯수는 Theory rows 합산 결과)

- [ ] **Step 4: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/UI/ItemCellRenderer.cs
git commit -m "feat(ui): v0.7.3 D-2 — ItemCellRenderer.Draw 24x24 placeholder cell IMGUI"
```

---

## Task 4: ContainerPanel — GradeColor 제거 + DrawItemList 변경

v0.7.2 의 private `GradeColor` 를 제거하고, row 마다 placeholder cell 을 prefix 로 그리도록 `DrawItemList` 변경.

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs` (line 366~381 의 DrawItemList + line 391~405 의 GradeColor)

- [ ] **Step 1: Remove ContainerPanel.GradeColor**

`src/LongYinRoster/UI/ContainerPanel.cs` 의 line 391 부터 시작하는 `GradeColor` 메서드 (XML doc + 본문) 전체 제거:

**제거 대상 (line 391~405)**:
```csharp
    /// <summary>
    /// v0.7.2 D-3 — 등급 색상 매핑. 0~5: 회색·녹·하늘·보라·오렌지·빨강. 미발견(-1) → 흰색.
    /// IL2CPP IMGUI strip-safe (GUI.color 만 사용, GUIStyle ctor 우회).
    /// 사용자 입력 (spec §4.9) 기준 hex 추정값 — 정확한 게임 sprite 색은 v0.7.3 D-2 sprite 분석 시 확정.
    /// </summary>
    private static Color GradeColor(int gradeOrder) => gradeOrder switch
    {
        0 => new Color(0.61f, 0.64f, 0.69f),    // 회색  #9CA3AF
        1 => new Color(0.13f, 0.77f, 0.37f),    // 녹    #22C55E
        2 => new Color(0.22f, 0.74f, 0.97f),    // 하늘 #38BDF8
        3 => new Color(0.66f, 0.33f, 0.97f),    // 보라 #A855F7
        4 => new Color(0.98f, 0.45f, 0.09f),    // 오렌지 #F97316
        5 => new Color(0.94f, 0.27f, 0.27f),    // 빨강 #EF4444
        _ => Color.white,
    };
```

Use Edit tool — old_string = 전체 위 블록, new_string = "" (빈 문자열).

- [ ] **Step 2: Modify DrawItemList — add cell prefix per row**

`src/LongYinRoster/UI/ContainerPanel.cs` 의 line 366~381 `DrawItemList` 메서드 전체 교체.

**기존 (line 366~381)**:
```csharp
    private void DrawItemList(List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
    {
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
        var prevColor = GUI.color;
        foreach (var r in rows)
        {
            if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;
            GUI.color = GradeColor(r.GradeOrder);
            bool was = checks.Contains(r.Index);
            bool now = GUILayout.Toggle(was, BuildLabel(r));
            GUI.color = prevColor;
            if (now && !was) checks.Add(r.Index);
            if (!now && was) checks.Remove(r.Index);
        }
        GUILayout.EndScrollView();
    }
```

**신규**:
```csharp
    private void DrawItemList(List<ItemRow> rows, HashSet<int> checks, ref Vector2 scroll, float height)
    {
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(height));
        var prevColor = GUI.color;
        foreach (var r in rows)
        {
            if (!ItemCategoryFilter.Matches(_filter, r.Type, r.SubType)) continue;

            GUILayout.BeginHorizontal();
            ItemCellRenderer.Draw(r, size: 24);
            GUI.color = ItemCellRenderer.GradeColor(r.GradeOrder);   // v0.7.2 row 텍스트 색상 — source 단일화
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

- [ ] **Step 3: Run all unit tests to confirm no regression**

Run from `src/`:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: 모든 기존 + 신규 tests PASS (약 166 tests). `ContainerPanelFormatTests` 같은 v0.7.2 기존 tests 가 BuildLabel 변경 없이 그대로 PASS.

- [ ] **Step 4: Build to confirm release dll deploy**

Run:
```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```
Expected: Build succeeded, 0 errors. `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 가 갱신됨.

- [ ] **Step 5: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "feat(ui): v0.7.3 D-2 — ContainerPanel row 마다 24px placeholder cell prefix + GradeColor 단일 source 이동"
```

---

## Task 5: 인게임 smoke 5/5

게임 닫혀 있는지 확인 후 빌드 + 실행 + 5 시나리오 시각 검증. **단위 테스트 불가능 항목** — 사용자 수동 확인.

**Files:** (코드 수정 없음 — smoke 결과 dump 만)

- [ ] **Step 1: 게임 닫혀있는지 확인**

```bash
tasklist | grep -i LongYinLiZhiZhuan
```
Expected: 출력 없음 (게임 안 돌고 있음). 출력 있으면 사용자에게 게임 종료 요청.

- [ ] **Step 2: BepInEx 로그 클리어**

```bash
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

- [ ] **Step 3: 사용자에게 게임 실행 요청**

사용자에게 다음 메시지:
> "게임 실행해서 캐릭터 로드 후 F11 → 컨테이너 관리 진입 부탁드립니다. 컨테이너 1개 신규 생성하고 인벤·창고·컨테이너 3-area 모두 placeholder cell 표시되는지 5 시나리오 확인 후 알려주세요."

- [ ] **Step 4: Smoke 5 시나리오 확인 (사용자 보고 받음)**

각 시나리오 인벤·창고·컨테이너 3-area 모두 동일 결과 기대:

1. **cell 배경 6단계 색상**: 등급 0~5 의 row 들이 회색 → 녹 → 하늘 → 보라 → 오렌지 → 빨강 으로 cell 배경색 구분
2. **우상단 마름모 6단계 색상**: 품질 0~5 의 row 들이 같은 6 색상으로 cell 우상단 8×8 마름모 구분
3. **중앙 한자 카테고리별 1자**: 장비=装, 비급=书, 단약=药, 음식=食, 보물=宝, 재료=材, 말=马
4. **강화 `+N` / `착` badge 표시**: 강화 lv > 0 row 만 우하단 `+N`, 착용중 row 만 좌하단 `착`
5. **v0.7.2 자산 보존 + perf**: 검색 box / 정렬 4-key / ▲▼ / 카테고리 탭 모두 v0.7.2 와 동일 동작 + 60fps frame drop 없음

**Strip 회귀 발견 시 fallback**:
- `GUI.Box(Rect, "")` 가 strip 되어 마름모 미표시 → spec §6 fallback 적용: `GUI.Label(Rect, "")` + GUI.color 또는 `GUI.DrawTexture(rect, Texture2D.whiteTexture)`
- `GUILayoutUtility.GetLastRect()` 가 잘못된 rect 반환 → cell 을 절대좌표 (Plugin.cs 의 fixed 좌표) 로 그리고 `GUILayout.Space(28)` 로 자리만 잡기

- [ ] **Step 5: BepInEx 로그 확인 + smoke 결과 dump 작성**

```bash
grep -n "ContainerPanel\|ItemCellRenderer\|threw" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | head -30
```
Expected: `threw` exception 0건. ContainerPanel 관련 정상 로그만.

`docs/superpowers/dumps/2026-05-XX-v0.7.3-smoke-results.md` 새 파일 생성 (실제 날짜로):
```markdown
# v0.7.3 D-2 — 인게임 smoke 결과

**일시**: 2026-05-XX
**테스터**: 사용자 (deepestdark@gmail.com)
**baseline**: v0.7.3 build (Task 4 commit)

| # | 시나리오 | 인벤 | 창고 | 컨테이너 | 비고 |
|---|---|---|---|---|---|
| 1 | cell 배경 6단계 색상 | ✅/❌ | ✅/❌ | ✅/❌ | |
| 2 | 우상단 마름모 6단계 | ✅/❌ | ✅/❌ | ✅/❌ | |
| 3 | 중앙 한자 카테고리 | ✅/❌ | ✅/❌ | ✅/❌ | |
| 4 | +N / 착 badge | ✅/❌ | ✅/❌ | ✅/❌ | |
| 5 | v0.7.2 자산 + perf | ✅/❌ | ✅/❌ | ✅/❌ | |

**총 결과**: X/5 PASS

**발견 회귀**: (있으면 fix 후 재smoke)
**Fallback 적용**: (있으면 명시)
```

- [ ] **Step 6: Commit smoke 결과**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add docs/superpowers/dumps/
git commit -m "docs(smoke): v0.7.3 D-2 인게임 smoke 5/5 (날짜 보정)"
```

---

## Task 6: VERSION bump + HANDOFF + README + release

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs:17` (VERSION)
- Modify: `docs/HANDOFF.md` (§1, §6.B, §7 매핑 갱신)
- Modify: `README.md` (placeholder cell 사용법 1단락 추가)

- [ ] **Step 1: Bump VERSION in Plugin.cs**

`src/LongYinRoster/Plugin.cs` line 17 수정:

**기존**:
```csharp
    public const string VERSION = "0.7.2";
```

**신규**:
```csharp
    public const string VERSION = "0.7.3";
```

- [ ] **Step 2: HANDOFF.md §6.B v0.7.3 항목 재정의**

`docs/HANDOFF.md` 의 §6.B 의 다음 줄 변경:

**기존**:
```markdown
2. **v0.7.3 D-2 아이콘 그리드** — sprite reference (item.iconID) IMGUI grid 표시. challenge: IL2CPP sprite asset 접근, IMGUI texture caching. v0.7.2 spike 결과로 itemLv/rareLv 외에 sub-data wrapper (`equipmentData` / `bookData` / etc.) presence 확인 path 활용 가능.
```

**신규**:
```markdown
2. ✅ **v0.7.3 D-2 Item 시각 표시 풍부화** (release 완료 — placeholder cell). 24×24 cell prefix per row: GradeColor 배경 + QualityColor 마름모 + CategoryGlyph 한자 + 강화 `+N` / 착용 `착` badge. v0.7.2 spike 의 ItemData 가 `iconID` 류 미보유 → 진짜 sprite 는 v0.8+ 별도 sub-project 로 보류. ItemCellRenderer 가 색상 6단계 단일 source.
```

또한 §6.B 끝에 v0.8 후보 추가:

```markdown
**진짜 sprite 도입 (v0.8 후보)**:
- ItemCellRenderer 의 GradeBackground / CategoryGlyph 부분만 sprite blit 으로 교체
- IL2CPP 환경에서 sprite asset 접근 spike 필요 (Resources.Load<Sprite> / AssetBundle / Addressables / 게임 자체 asset manager)
- 진짜 sprite 가 들어와도 등급 배경 / 품질 마름모 / 강화·착용 badge layout 보존 (overlay)
```

§1 의 main baseline + §7 컨텍스트 압축본의 "현재 main baseline = v0.7.X" 도 0.7.3 로 갱신 + 다음 단계 후보 목록도 v0.7.4 부터 시작하도록 정리.

- [ ] **Step 3: README.md placeholder cell 사용법 1단락**

`README.md` 의 컨테이너 관리 사용법 section 끝에 추가 (정확한 anchor 는 README 구조에 맞춤):

```markdown
### v0.7.3 — Item 시각 표시 (placeholder cell)

각 item row 좌측에 24×24 placeholder cell 이 표시됩니다 (real game sprite 는 v0.8+ 도입 예정):
- **배경 색상** = 등급 6단계 (열악 회색 → 절세 빨강)
- **우상단 작은 마름모** = 품질 6단계 (잔품 회색 → 극품 빨강)
- **중앙 한자 1자** = 카테고리 (装=장비, 书=비급, 药=단약, 食=음식, 宝=보물, 材=재료, 马=말)
- **우하단 `+N`** = 강화 lv (있을 때만)
- **좌하단 `착`** = 착용중 (있을 때만)

색상 매핑은 게임 본 UI 의 아이콘 표시 (등급 = 아이콘 배경, 품질 = 아이콘 상단 마름모) 와 일치합니다.
```

- [ ] **Step 4: 최종 빌드 + 테스트 한번 더**

```
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```
Expected: build OK + 모든 tests PASS.

- [ ] **Step 5: Commit + tag**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/Plugin.cs docs/HANDOFF.md README.md
git commit -m "chore(release): v0.7.3 — Item 시각 표시 풍부화 (D-2 placeholder cell)"
git tag v0.7.3
```

- [ ] **Step 6: Release 패키징 + GitHub release**

dist zip 생성 (PowerShell):
```powershell
Compress-Archive -Path "dist/LongYinRoster_v0.7.3/*" -DestinationPath "dist/LongYinRoster_v0.7.3.zip" -Force
```
(`dist/LongYinRoster_v0.7.3/` 폴더 구조는 v0.7.2 와 동일 — `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 만 갱신)

GitHub release (gh CLI):
```bash
gh release create v0.7.3 dist/LongYinRoster_v0.7.3.zip \
    --title "v0.7.3 — Item 시각 표시 풍부화 (D-2 placeholder cell)" \
    --notes "ContainerPanel row 좌측 24×24 placeholder cell 추가: 등급 배경 + 품질 마름모 + 카테고리 한자 + 강화/착용 badge. v0.7.2 의 검색·정렬·카테고리 탭 100% 보존. 진짜 sprite 는 v0.8+ 후속."
```

- [ ] **Step 7: git push**

```bash
git push origin main --tags
```
Expected: main + v0.7.3 tag push 완료.

---

## Self-Review (작성 후 점검)

### Spec coverage
- §3 (HANDOFF mapping 갱신): Task 6 Step 2 ✓
- §4.1 (Approach List 풍부화): Task 4 ✓
- §4.2 (ItemRow 변경 없음): no task — by design ✓
- §4.3 (신규/변경 모듈 4개): Task 1 (CategoryGlyph), Task 2+3 (ItemCellRenderer), Task 4 (ContainerPanel 변경 + GradeColor 제거) ✓
- §4.4 (CategoryGlyph 매핑): Task 1 ✓
- §4.5 (ItemCellRenderer strip-safe 패턴): Task 2 (helper) + Task 3 (Draw) ✓
- §4.6 (DrawItemList 변경): Task 4 ✓
- §4.7 (row 높이 / ScrollView): no task — Task 5 smoke 에서 시각 검증 ✓
- §4.8 (IL2CPP / 성능 가드): Task 5 smoke #5 ✓
- §5.1 (Unit +6 → 139): Task 1 (13 tests) + Task 2 (~20 tests) — spec 의 +6 추정보다 많음. 더 많은 test = 더 좋은 coverage ✓
- §5.2 (인게임 smoke 5/5): Task 5 ✓
- §6 (위험·미지수): Task 5 의 strip fallback 노트 ✓
- §7 (완료 기준): Task 5 + Task 6 통합 ✓
- §8 (release contract): Task 6 ✓

### Placeholder scan
- "(날짜 보정)" — Task 5 Step 6 commit msg: 사용자가 실제 smoke 날짜로 dump 파일명 fix — qualified placeholder, 실행자 책임 명시 ✓
- "정확한 anchor 는 README 구조에 맞춤" — Task 6 Step 3: README anchor 위치 약간 유동적, 실행자 책임 명시 ✓
- 모든 코드 step 은 완전한 코드 블록 포함 ✓
- 추상적 "validation/error handling 추가" 류 표현 0건 ✓

### Type consistency
- `CategoryGlyph.For(int type, int subType)` — Task 1 정의, Task 3 의 Draw 에서 호출 ✓
- `ItemCellRenderer.GradeColor(int grade)` / `QualityColor(int quality)` / `BadgeText(int)` / `EquippedMarker(bool)` — Task 2 정의, Task 3+4 에서 호출 ✓
- `ItemCellRenderer.Draw(ContainerPanel.ItemRow r, int size)` — Task 3 정의, Task 4 에서 호출 ✓
- `ContainerPanel.ItemRow` — v0.7.2 기존 type, 변경 없음 (spec §4.2 일관) ✓
- `ContainerPanel.GradeColor` — Task 4 Step 1 에서 제거. Task 4 Step 2 의 새 DrawItemList 가 `ItemCellRenderer.GradeColor` 호출로 변경 ✓
- `ItemCategoryFilter.Matches` — v0.7.2 기존, Task 4 그대로 사용 ✓

issue 없음.
