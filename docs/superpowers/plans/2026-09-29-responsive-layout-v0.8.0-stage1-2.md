# 반응형 패널 레이아웃 v0.8.0 — 1·2단계 구현 플랜 (PanelWindow + SettingsPanel / ItemGeneratorPanel)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 여섯 패널이 공유할 창 틀(`PanelWindow`)과 순수 레이아웃 수식(`LayoutMath`/`PanelWindowLogic`)을 만들고, SettingsPanel(1단계)과 ItemGeneratorPanel(2단계)을 그 위로 옮겨 창 크기에 따라 내부 컨트롤·페이지 크기가 재조정되게 한다.

**Architecture:** 각 패널은 `PanelWindow`(GUI.Window·헤더·X·드래그·코너 리사이즈·화면 클램프·설정 저장)를 합성으로 들고, `Draw`는 `XxxLayout.Compute(contentRect, state)`가 낸 숫자만 IMGUI에 넘긴다. 계산기와 `PanelWindowLogic`은 Unity 의존이 없어 테스트 프로젝트가 소스 링크 + `UnityStubs`로 단위 테스트한다. `PanelRegistry`가 이관된 패널의 `Hydrate/Persist`를 모아 ModWindow의 설정 주입·저장 코드를 대체한다.

**Tech Stack:** C# 10 / net6.0 / BepInEx 6 IL2CPP / Unity IMGUI(GUI·GUILayout) / xunit + Shouldly / Mono.Cecil 없음(런타임 reflection 없음 — 이 작업은 순수 UI)

**Spec:** `docs/superpowers/specs/2026-09-29-longyin-roster-mod-v0.8.0-responsive-layout-design.md`

**이 플랜의 범위:** 스펙 §11의 1단계·2단계(PR 2개). 3~6단계(Container / ItemDetail / PlayerEditor / ModWindow)는 각각 앞 단계가 `develop`에 머지된 뒤 같은 형식으로 별도 플랜을 쓴다 — 그 단계들의 코드는 여기서 만든 `PanelWindow`/`LayoutMath`의 실제 형태에 의존하므로 지금 쓰면 추측이 섞인다.

## Global Constraints

- `LangVersion` 10, `net6.0`, `Nullable` enable, `TreatWarningsAsErrors` true — 경고 하나가 빌드 실패.
- IMGUI 금지 API: `GUILayout.FlexibleSpace`, `GUILayoutUtility.GetLastRect`, `GUILayout.BeginArea`, `GUILayout.ExpandWidth/ExpandHeight` (IL2CPP strip 전례 / 스텁 없음). 새 IMGUI API 도입 금지 — `GUI.Window`, `GUI.DragWindow`, `GUI.Button(Rect)`, `GUI.DrawTexture`, `GUILayout.Width/Height/Space/Label/Button/TextField/BeginScrollView/BeginHorizontal` 만.
- 게임 조작 코드(`Core/*Applier`, `*Reflector`, `*Patch`) 변경 금지.
- 소스 `.cs` 는 CRLF. 테스트 프로젝트는 `LongYinRoster.Tests.csproj` 의 `<Compile Include>` 로 소스를 링크한다 — 새 파일은 반드시 링크 추가.
- 기존 설정 키 이름 불변. 신설은 `[UI] SettingsPanelX/Y/W/H` (기본 200/120/480/600) 만.
- `Plugin.VERSION` 은 이 플랜에서 바꾸지 않는다(6단계 완료 시 0.8.0).
- 행 높이·간격 상수의 단일 출처는 `DialogStyle` (`HeaderHeight=28`, `RowHeight=24`, `ButtonRowHeight=28`, `Padding=12`, `Gap=4`).
- 커밋 메시지 끝에 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. 브랜치: 1단계 `feat/v0.8.0-step1-settings`, 2단계 `feat/v0.8.0-step2-itemgen` — 각각 `develop` 에서 분기, PR → `develop`, merge commit.

## Review Focus

1. 다른 해상도에서 저장된 cfg(창이 화면 밖) → 로드 시 창이 화면 안으로 들어와야 한다 — Task 3 `Hydrate_ClampsOffscreenRectIntoScreen`.
2. 화면이 패널 최소 크기보다 작음(저해상도) → 창이 (0,0) 최소 크기로 놓이고 예외 없음 — Task 2 `ClampToScreen_ScreenSmallerThanMin_PinsToOrigin`.
3. 코너 드래그를 화면 밖으로 끌고 감 → 크기는 화면에서 잘리고 위치가 안으로 밀림 — Task 2 `Resize_BeyondScreen_ClampsSizeAndMovesInside`.
4. 설정 패널 숫자 입력에 "abc"·"10" 같은 값 → 파싱 실패는 무시, 너무 작은 W/H 는 최소로 올라감 — Task 3 `SetRect_BelowMin_ClampsToMin`, Task 5 `SelfRect_TextParse_IgnoresGarbage`.
5. 창을 키워 페이지 수가 줄었는데 현재 페이지가 그보다 큼 → 마지막 페이지로 당겨짐 — Task 7 `Page_ClampsWhenPageSizeGrows`.

---

## 파일 구조

**신규**
- `src/LongYinRoster/UI/Layout/PanelBounds.cs` — 최소 크기 값 타입.
- `src/LongYinRoster/UI/Layout/LayoutMath.cs` — 순수 수식 5개.
- `src/LongYinRoster/UI/Layout/PanelWindowLogic.cs` — 리사이즈·클램프·내용 rect 순수 로직.
- `src/LongYinRoster/UI/Layout/SettingsLayout.cs` — 설정 패널 계산기.
- `src/LongYinRoster/UI/Layout/ItemGenLayout.cs` — 아이템 생성 패널 계산기.
- `src/LongYinRoster/UI/PanelWindow.cs` — `RectBinding` + `PanelWindow` (얇은 IMGUI).
- `src/LongYinRoster/UI/PanelRegistry.cs` — 이관 패널 창들의 Hydrate/Persist 묶음.
- `src/LongYinRoster.Tests/LayoutMathTests.cs`, `PanelWindowLogicTests.cs`, `PanelWindowTests.cs`, `SettingsLayoutTests.cs`, `ItemGenLayoutTests.cs`.

**수정**
- `src/LongYinRoster/UI/DialogStyle.cs` — 상수 4개 추가.
- `src/LongYinRoster/Config.cs` — `SettingsPanelX/Y/W/H`.
- `src/LongYinRoster/UI/SettingsPanel.cs` — PanelWindow 합성, 자기 rect 버퍼, 레이아웃 적용.
- `src/LongYinRoster/UI/ItemGeneratorPanel.cs` — PanelWindow 합성, 자동 페이지, 탭 줄바꿈.
- `src/LongYinRoster/UI/ModWindow.cs` — `PanelRegistry`, `OnDestroy`, ItemGen Init/저장 블록 제거.
- `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` — 링크 추가.
- `src/LongYinRoster.Tests/SettingsPanelTests.cs`, `ItemGenFilterTests.cs` — 케이스 추가.

---

### Task 0: 스펙·플랜 PR + 1단계 브랜치

**Files:** 없음 (git 만)

- [ ] **Step 1: 스펙·플랜 브랜치를 develop 에 PR**

```bash
cd E:/LylzzBox
git push -u origin feat/v0.8.0-responsive-layout
gh pr create --base develop --head feat/v0.8.0-responsive-layout \
  --title "docs: v0.8.0 반응형 패널 레이아웃 스펙 + 1·2단계 플랜" \
  --body "스펙·플랜 문서만. 구현은 단계별 PR.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
gh pr merge --merge
```

- [ ] **Step 2: 1단계 브랜치 생성**

```bash
git fetch origin develop && git checkout -b feat/v0.8.0-step1-settings origin/develop
```

---

### Task 1: DialogStyle 상수 + PanelBounds + LayoutMath

**Files:**
- Modify: `src/LongYinRoster/UI/DialogStyle.cs:16` (상수 추가)
- Create: `src/LongYinRoster/UI/Layout/PanelBounds.cs`
- Create: `src/LongYinRoster/UI/Layout/LayoutMath.cs`
- Test: `src/LongYinRoster.Tests/LayoutMathTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` (링크)

**Interfaces:**
- Produces: `DialogStyle.RowHeight/ButtonRowHeight/Padding/Gap` (const float), `PanelBounds(float MinW, float MinH)` (readonly record struct), `LayoutMath.RowsThatFit(float,float,float,int)`, `SplitHeight(float,float[],float[],bool[],float)`, `SplitWidth(float,float[],float,float[])`, `Wrap(float,float,float)`, `Clamp(Rect,PanelBounds,float,float)`.

- [ ] **Step 1: 테스트 파일 작성 (실패 예정)**

`src/LongYinRoster.Tests/LayoutMathTests.cs`:

```csharp
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — LayoutMath 순수 수식. UnityStubs 의 Rect 로 검증.</summary>
public class LayoutMathTests
{
    [Theory]
    [InlineData(100f, 24f, 0f, 4)]   // 4*24 = 96 ≤ 100
    [InlineData(100f, 24f, 4f, 3)]   // 3*24 + 2*4 = 80 ≤ 100 < 108
    [InlineData(48f,  24f, 0f, 2)]   // 정확히 두 행
    [InlineData(23f,  24f, 0f, 1)]   // 한 행도 안 들어가도 min 1
    [InlineData(0f,   24f, 0f, 1)]
    [InlineData(-50f, 24f, 0f, 1)]
    public void RowsThatFit_CountsRowsIncludingSpacing(float h, float row, float gap, int expected)
    {
        LayoutMath.RowsThatFit(h, row, gap).ShouldBe(expected);
    }

    [Fact]
    public void RowsThatFit_NeverBelowMin()
    {
        LayoutMath.RowsThatFit(10f, 24f, 0f, min: 3).ShouldBe(3);
    }

    [Fact]
    public void RowsThatFit_ZeroRowHeight_ReturnsMin()
    {
        LayoutMath.RowsThatFit(100f, 0f, 0f, min: 2).ShouldBe(2);
    }

    [Fact]
    public void SplitHeight_TwoExpanded_SharesRemainingByWeight()
    {
        // 컨테이너 인벤/창고: 총 640, 각 오버헤드 56, 가중치 1:1, gap 4 → 잔여 524 → 262/262
        var r = LayoutMath.SplitHeight(640f, new[] { 56f, 56f }, new[] { 1f, 1f }, new[] { false, false }, 4f);
        r.ShouldBe(new[] { 262f, 262f });
    }

    [Fact]
    public void SplitHeight_OneCollapsed_GivesRemainingToOther()
    {
        // 접힌 쪽은 헤더(28)만 차지, 잔여 640-28-56-4 = 552 전부 펼친 쪽
        var r = LayoutMath.SplitHeight(640f, new[] { 28f, 56f }, new[] { 1f, 1f }, new[] { true, false }, 4f);
        r.ShouldBe(new[] { 0f, 552f });
    }

    [Fact]
    public void SplitHeight_AllCollapsed_ReturnsZeros()
    {
        var r = LayoutMath.SplitHeight(640f, new[] { 28f, 28f }, new[] { 1f, 1f }, new[] { true, true }, 4f);
        r.ShouldBe(new[] { 0f, 0f });
    }

    [Fact]
    public void SplitHeight_NotEnoughForOverheads_ReturnsZerosNotNegative()
    {
        var r = LayoutMath.SplitHeight(50f, new[] { 56f, 56f }, new[] { 1f, 1f }, new[] { false, false }, 4f);
        r.ShouldBe(new[] { 0f, 0f });
    }

    [Fact]
    public void SplitWidth_MinsFirstThenWeights()
    {
        // 총 800, 가중치 1:3, gap 8, 최소 100/100 → 잔여 592 → 148/444 → 248/544
        var r = LayoutMath.SplitWidth(800f, new[] { 1f, 3f }, 8f, new[] { 100f, 100f });
        r.ShouldBe(new[] { 248f, 544f });
    }

    [Fact]
    public void SplitWidth_TooNarrow_ReturnsMins()
    {
        var r = LayoutMath.SplitWidth(100f, new[] { 1f, 1f }, 8f, new[] { 100f, 100f });
        r.ShouldBe(new[] { 100f, 100f });
    }

    [Theory]
    [InlineData(400f, 55f, 4f, 6)]   // floor(404/59) = 6
    [InlineData(55f,  55f, 4f, 1)]
    [InlineData(10f,  55f, 4f, 1)]   // 최소 1
    [InlineData(0f,   55f, 4f, 1)]
    public void Wrap_CellsPerRow(float totalW, float cellW, float gap, int expected)
    {
        LayoutMath.Wrap(totalW, cellW, gap).ShouldBe(expected);
    }

    [Fact]
    public void Clamp_BelowMin_RaisesToMin()
    {
        var r = LayoutMath.Clamp(new Rect(0, 0, 100, 100), new PanelBounds(600, 400), 1920, 1080);
        (r.width, r.height).ShouldBe((600f, 400f));
    }

    [Fact]
    public void Clamp_AboveMax_LowersToMax()
    {
        var r = LayoutMath.Clamp(new Rect(0, 0, 3000, 2000), new PanelBounds(600, 400), 1920, 1080);
        (r.width, r.height).ShouldBe((1920f, 1080f));
    }

    [Fact]
    public void Clamp_MinAboveMax_MinWins()
    {
        // 화면(500×300)이 최소(600×400)보다 작으면 최소가 이긴다 — 창이 화면을 넘더라도 사용 가능하게
        var r = LayoutMath.Clamp(new Rect(0, 0, 100, 100), new PanelBounds(600, 400), 500, 300);
        (r.width, r.height).ShouldBe((600f, 400f));
    }

    [Fact]
    public void Clamp_KeepsPosition()
    {
        var r = LayoutMath.Clamp(new Rect(70, 80, 100, 100), new PanelBounds(600, 400), 1920, 1080);
        (r.x, r.y).ShouldBe((70f, 80f));
    }
}
```

- [ ] **Step 2: csproj 에 링크 추가 (테스트가 컴파일되도록 — 아직 소스는 없음)**

`src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` 의 마지막 `</ItemGroup>` 바로 앞에:

```xml
    <!-- v0.8.0 S1 — 레이아웃 계산기 + 창 틀 -->
    <Compile Include="../LongYinRoster/UI/Layout/PanelBounds.cs">
      <Link>UI/Layout/PanelBounds.cs</Link>
    </Compile>
    <Compile Include="../LongYinRoster/UI/Layout/LayoutMath.cs">
      <Link>UI/Layout/LayoutMath.cs</Link>
    </Compile>
```

- [ ] **Step 3: 실패 확인**

Run: `cd E:/LylzzBox && DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error CS|Passed!|Failed!" | head`
Expected: `error CS0234`/`CS0246` — `LongYinRoster.UI.Layout` / `LayoutMath` / `PanelBounds` 없음 (링크한 파일이 없어 빌드 오류). 기능 부재로 인한 실패임을 확인.

- [ ] **Step 4: DialogStyle 상수 추가**

`src/LongYinRoster/UI/DialogStyle.cs` 16번 줄 `public  const float HeaderHeight = 28f;` 아래에:

```csharp
    // v0.8.0 — 레이아웃 계산기(UI/Layout)와 Draw 가 공유하는 단일 출처. 계산 ≠ 렌더 어긋남 방지.
    public  const float RowHeight       = 24f;   // 리스트 행 · 입력 행
    public  const float ButtonRowHeight = 28f;   // 버튼 줄 (기존 GUILayout.Height(28) 관례)
    public  const float Padding         = 12f;   // 창 내부 여백 (GUI.Window skin padding 흡수, smoke 로 조정)
    public  const float Gap             = 4f;    // 요소 간 간격
```

- [ ] **Step 5: PanelBounds + LayoutMath 구현**

`src/LongYinRoster/UI/Layout/PanelBounds.cs`:

```csharp
namespace LongYinRoster.UI.Layout;

/// <summary>v0.8.0 — 패널 최소 크기. 최대는 항상 화면 크기라 상수로 두지 않는다.
/// 각 패널 계산기(*Layout.MinSize)가 고정 부분 합 + 주 리스트 3행으로 계산해 낸다.</summary>
public readonly record struct PanelBounds(float MinW, float MinH);
```

`src/LongYinRoster/UI/Layout/LayoutMath.cs`:

```csharp
using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 — 레이아웃 공통 수식. Unity 의존 없음(Rect 만 값 타입으로 사용) → UnityStubs 로 단위 테스트.
/// IMGUI 내부 계산(FlexibleSpace 등, IL2CPP strip 전례)에 기대지 않고 숫자를 직접 낸다.
/// </summary>
public static class LayoutMath
{
    /// <summary>높이 → 행 수. n행은 n*rowH + (n-1)*spacing 을 차지. min 미만으로 내려가지 않음(페이지 크기 하한).</summary>
    public static int RowsThatFit(float availableH, float rowH, float spacing, int min = 1)
    {
        if (rowH <= 0f || availableH <= 0f) return min;
        int n = (int)Math.Floor((availableH + spacing) / (rowH + spacing));
        return Math.Max(min, n);
    }

    /// <summary>
    /// 세로 분배(컨테이너 인벤/창고의 일반형). 각 섹션은 overhead(헤더·버튼 줄)를 먼저 차지하고,
    /// 잔여 = totalH - Σoverhead - gap*(n-1) 을 펼친 섹션끼리 weight 비율로 나눈다. 접힌 섹션은 0.
    /// 반환값은 각 섹션의 '내용' 높이(overhead 제외). 잔여가 음수면 전부 0.
    /// </summary>
    public static float[] SplitHeight(float totalH, float[] overheads, float[] weights, bool[] collapsed, float gap)
    {
        int n = overheads.Length;
        if (weights.Length != n || collapsed.Length != n)
            throw new ArgumentException("SplitHeight: overheads/weights/collapsed 길이가 다름");
        var result = new float[n];
        float fixedSum = 0f, weightSum = 0f;
        for (int i = 0; i < n; i++)
        {
            fixedSum += overheads[i];
            if (!collapsed[i]) weightSum += weights[i];
        }
        float remaining = totalH - fixedSum - gap * Math.Max(0, n - 1);
        if (remaining < 0f) remaining = 0f;
        for (int i = 0; i < n; i++)
            result[i] = (collapsed[i] || weightSum <= 0f) ? 0f : remaining * (weights[i] / weightSum);
        return result;
    }

    /// <summary>가로 분배. mins 를 먼저 보장하고 잔여 = totalW - Σmins - gap*(n-1) 을 weight 비율로. 잔여 음수면 mins 그대로.</summary>
    public static float[] SplitWidth(float totalW, float[] weights, float gap, float[] mins)
    {
        int n = weights.Length;
        if (mins.Length != n) throw new ArgumentException("SplitWidth: weights/mins 길이가 다름");
        var result = new float[n];
        float minSum = 0f, weightSum = 0f;
        for (int i = 0; i < n; i++) { minSum += mins[i]; weightSum += weights[i]; }
        float remaining = totalW - minSum - gap * Math.Max(0, n - 1);
        if (remaining < 0f) remaining = 0f;
        for (int i = 0; i < n; i++)
            result[i] = mins[i] + (weightSum <= 0f ? 0f : remaining * (weights[i] / weightSum));
        return result;
    }

    /// <summary>셀 폭 기준 한 줄에 들어가는 개수(탭·버튼 줄바꿈). 최소 1.</summary>
    public static int Wrap(float totalW, float cellW, float gap)
    {
        if (cellW <= 0f) return 1;
        int n = (int)Math.Floor((totalW + gap) / (cellW + gap));
        return Math.Max(1, n);
    }

    /// <summary>크기만 [min, max] 로 자른다(위치 불변). max &lt; min 이면 min 이 이긴다 — 화면이 최소보다 작아도 창은 사용 가능해야 함.</summary>
    public static Rect Clamp(Rect r, PanelBounds min, float maxW, float maxH)
    {
        float w = Math.Max(min.MinW, Math.Min(maxW, r.width));
        float h = Math.Max(min.MinH, Math.Min(maxH, r.height));
        return new Rect(r.x, r.y, w, h);
    }
}
```

- [ ] **Step 6: 테스트 통과 확인**

Run: `cd E:/LylzzBox && DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet --filter "FullyQualifiedName~LayoutMathTests" 2>&1 | grep -E "error|Passed!|Failed!"`
Expected: `Passed! - Failed: 0, Passed: 22` (Theory 케이스 포함). 그다음 전체: `dotnet test --nologo -v quiet` → 기존 427 + 22 = 449 pass.

- [ ] **Step 7: 줄끝 확인 + 커밋**

새 `.cs` 파일이 LF 로 생성됐으면 CRLF 로: `sed -i 's/\r$//; s/$/\r/' <file>` (Git Bash).

```bash
cd E:/LylzzBox
git add src/LongYinRoster/UI/DialogStyle.cs src/LongYinRoster/UI/Layout/PanelBounds.cs src/LongYinRoster/UI/Layout/LayoutMath.cs src/LongYinRoster.Tests/LayoutMathTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git -c core.safecrlf=false commit -m "feat(v0.8.0-s1): LayoutMath 순수 수식 5종 + PanelBounds + DialogStyle 행/간격 상수

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: PanelWindowLogic (리사이즈·화면 클램프·내용 rect)

**Files:**
- Create: `src/LongYinRoster/UI/Layout/PanelWindowLogic.cs`
- Test: `src/LongYinRoster.Tests/PanelWindowLogicTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`

**Interfaces:**
- Consumes: `LayoutMath.Clamp`, `PanelBounds` (Task 1).
- Produces: `PanelWindowLogic.ContentRect(Rect window, float headerH, float padding) : Rect` (창 로컬 좌표), `Resize(Rect r, Vector2 startMouse, Vector2 startSize, Vector2 mouse, PanelBounds b, float screenW, float screenH) : Rect`, `ClampToScreen(Rect r, float screenW, float screenH, PanelBounds b) : Rect`.

- [ ] **Step 1: 테스트 작성**

`src/LongYinRoster.Tests/PanelWindowLogicTests.cs`:

```csharp
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — 창 틀 순수 로직. 마우스 좌표는 GUI.Window 콜백 안의 창 로컬 좌표 — 델타만 쓰므로 무관.</summary>
public class PanelWindowLogicTests
{
    private static readonly PanelBounds B = new(600f, 400f);

    [Fact]
    public void ContentRect_SubtractsHeaderAndPadding_InLocalCoords()
    {
        var c = PanelWindowLogic.ContentRect(new Rect(100, 50, 800, 600), headerH: 28f, padding: 12f);
        (c.x, c.y, c.width, c.height).ShouldBe((12f, 40f, 776f, 548f));
    }

    [Fact]
    public void ContentRect_NeverNegative()
    {
        var c = PanelWindowLogic.ContentRect(new Rect(0, 0, 10, 10), 28f, 12f);
        (c.width, c.height).ShouldBe((0f, 0f));
    }

    [Fact]
    public void Resize_AppliesMouseDelta()
    {
        var r = PanelWindowLogic.Resize(new Rect(100, 100, 800, 600),
            startMouse: new Vector2(790, 590), startSize: new Vector2(800, 600), mouse: new Vector2(890, 690),
            B, 1920, 1080);
        (r.x, r.y, r.width, r.height).ShouldBe((100f, 100f, 900f, 700f));
    }

    [Fact]
    public void Resize_BelowMin_ClampsToMin()
    {
        var r = PanelWindowLogic.Resize(new Rect(100, 100, 800, 600),
            new Vector2(790, 590), new Vector2(800, 600), new Vector2(90, 90), B, 1920, 1080);
        (r.width, r.height).ShouldBe((600f, 400f));
    }

    [Fact]
    public void Resize_BeyondScreen_ClampsSizeAndMovesInside()
    {
        // (1500,900) 에서 300×100 창을 +1000 끌면 폭 1300 → x+w=2800 > 1920 → x 가 620 으로 밀림
        var r = PanelWindowLogic.Resize(new Rect(1500, 900, 600, 400),
            new Vector2(590, 390), new Vector2(600, 400), new Vector2(1590, 390), B, 1920, 1080);
        r.width.ShouldBe(1600f);
        (r.x + r.width).ShouldBe(1920f);
        r.x.ShouldBe(320f);
    }

    [Fact]
    public void ClampToScreen_MovesOffscreenRectInside()
    {
        var r = PanelWindowLogic.ClampToScreen(new Rect(1800, 1000, 700, 500), 1920, 1080, B);
        (r.x, r.y, r.width, r.height).ShouldBe((1220f, 580f, 700f, 500f));
    }

    [Fact]
    public void ClampToScreen_NegativeOrigin_PinsToZero()
    {
        var r = PanelWindowLogic.ClampToScreen(new Rect(-50, -20, 700, 500), 1920, 1080, B);
        (r.x, r.y).ShouldBe((0f, 0f));
    }

    [Fact]
    public void ClampToScreen_ScreenSmallerThanMin_PinsToOrigin()
    {
        // 화면 500×300 < 최소 600×400 → 최소 크기 유지, (0,0) 에 맞춤, 예외 없음
        var r = PanelWindowLogic.ClampToScreen(new Rect(100, 100, 100, 100), 500, 300, B);
        (r.x, r.y, r.width, r.height).ShouldBe((0f, 0f, 600f, 400f));
    }

    [Fact]
    public void ClampToScreen_AlreadyInside_Unchanged()
    {
        var r = PanelWindowLogic.ClampToScreen(new Rect(100, 100, 700, 500), 1920, 1080, B);
        (r.x, r.y, r.width, r.height).ShouldBe((100f, 100f, 700f, 500f));
    }
}
```

- [ ] **Step 2: csproj 링크 추가** (Task 1 의 블록 안에 이어서)

```xml
    <Compile Include="../LongYinRoster/UI/Layout/PanelWindowLogic.cs">
      <Link>UI/Layout/PanelWindowLogic.cs</Link>
    </Compile>
```

- [ ] **Step 3: 실패 확인**

Run: `dotnet test --nologo -v quiet 2>&1 | grep -E "error CS" | head -3`
Expected: `CS0246: 'PanelWindowLogic' 형식... 찾을 수 없습니다` (파일 부재).

- [ ] **Step 4: 구현**

`src/LongYinRoster/UI/Layout/PanelWindowLogic.cs`:

```csharp
using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>v0.8.0 — PanelWindow 의 순수 부분. Screen 크기는 호출자가 숫자로 넘긴다(테스트 가능).</summary>
public static class PanelWindowLogic
{
    /// <summary>헤더·여백을 뺀 내용 영역. 창 로컬 좌표(0,0 = 창 좌상단). 폭·높이는 0 미만으로 내려가지 않음.</summary>
    public static Rect ContentRect(Rect window, float headerH, float padding)
    {
        float w = Math.Max(0f, window.width - 2f * padding);
        float h = Math.Max(0f, window.height - headerH - 2f * padding);
        return new Rect(padding, headerH + padding, w, h);
    }

    /// <summary>코너 드래그: 시작 크기 + 마우스 델타 → 경계·화면 클램프. 위치는 ClampToScreen 이 필요할 때만 옮긴다.</summary>
    public static Rect Resize(Rect r, Vector2 startMouse, Vector2 startSize, Vector2 mouse,
                              PanelBounds b, float screenW, float screenH)
    {
        // UnityStubs 의 Vector2 에 - 연산자가 없어 성분별로 계산
        float dx = mouse.x - startMouse.x;
        float dy = mouse.y - startMouse.y;
        var resized = new Rect(r.x, r.y, startSize.x + dx, startSize.y + dy);
        return ClampToScreen(resized, screenW, screenH, b);
    }

    /// <summary>크기를 [min, screen] 로 자르고 창 전체가 화면 안에 오도록 이동. 화면 &lt; 최소면 (0,0) 에 최소 크기.</summary>
    public static Rect ClampToScreen(Rect r, float screenW, float screenH, PanelBounds b)
    {
        var c = LayoutMath.Clamp(r, b, screenW, screenH);
        float x = c.x, y = c.y;
        if (x + c.width  > screenW) x = screenW - c.width;
        if (y + c.height > screenH) y = screenH - c.height;
        if (x < 0f) x = 0f;
        if (y < 0f) y = 0f;
        return new Rect(x, y, c.width, c.height);
    }
}
```

- [ ] **Step 5: 통과 확인**

Run: `dotnet test --nologo -v quiet --filter "FullyQualifiedName~PanelWindowLogicTests" 2>&1 | grep -E "error|Passed!|Failed!"`
Expected: `Passed: 9`. 전체 458 pass.

- [ ] **Step 6: 커밋**

```bash
git add src/LongYinRoster/UI/Layout/PanelWindowLogic.cs src/LongYinRoster.Tests/PanelWindowLogicTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git -c core.safecrlf=false commit -m "feat(v0.8.0-s1): PanelWindowLogic — 리사이즈·화면 클램프·내용 rect (순수)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Config 설정 키 + RectBinding + PanelWindow + PanelRegistry

**Files:**
- Modify: `src/LongYinRoster/Config.cs:75` (선언), `:187` (Bind)
- Create: `src/LongYinRoster/UI/PanelWindow.cs`
- Create: `src/LongYinRoster/UI/PanelRegistry.cs`
- Test: `src/LongYinRoster.Tests/PanelWindowTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`

**Interfaces:**
- Consumes: `PanelWindowLogic`, `LayoutMath`, `PanelBounds`, `DialogStyle.*`, `Logger.WarnOnce(string key, string msg)`, `BepInEx.Configuration.ConfigEntry<T>`.
- Produces:
  - `RectBinding.Of(ConfigEntry<float> x, ConfigEntry<float> y, ConfigEntry<float> w, ConfigEntry<float> h, ConfigEntry<bool>? open = null)`; `RectBinding.IsBound : bool`.
  - `PanelWindow(int windowId, string title, Func<PanelBounds> bounds, RectBinding binding)`; `Rect Rect`, `Rect ContentRect`, `bool IsHydrated`, `bool Visible`, `Action? OnClosed`, `void Hydrate(float screenW, float screenH)`, `void SetRect(float x, float y, float w, float h)`, `void Persist()`, `void Close()`, `void OnGUI(Action<Rect> drawContent)`.
  - `PanelRegistry.Register(PanelWindow)`, `HydrateAll(float, float)`, `PersistAll()`.
  - `Config.SettingsPanelX/Y/W/H : ConfigEntry<float>`.

- [ ] **Step 1: 테스트 작성**

`src/LongYinRoster.Tests/PanelWindowTests.cs` — BepInEx.Core 의 실제 `ConfigFile` 을 임시 경로에 만들어 `ConfigEntry` 를 얻는다(테스트 프로젝트가 `$(BepInExCore)/BepInEx.Core.dll` 을 참조).

```csharp
using System;
using System.IO;
using BepInEx.Configuration;
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — PanelWindow 의 IMGUI 아닌 부분(Hydrate/SetRect/Persist/ContentRect). Screen 스텁 = 1920×1080.</summary>
public class PanelWindowTests
{
    private static readonly PanelBounds B = new(300f, 200f);

    private static RectBinding MakeBinding(float x, float y, float w, float h, bool? open = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lyr-panelwindow-{Guid.NewGuid():N}.cfg");
        var cfg = new ConfigFile(path, false);
        return RectBinding.Of(
            cfg.Bind("T", "X", x, ""), cfg.Bind("T", "Y", y, ""),
            cfg.Bind("T", "W", w, ""), cfg.Bind("T", "H", h, ""),
            open.HasValue ? cfg.Bind("T", "Open", open.Value, "") : null);
    }

    private static PanelWindow Make(RectBinding b) => new(0x1234, "test", () => B, b);

    [Fact]
    public void Hydrate_ClampsOffscreenRectIntoScreen()
    {
        var w = Make(MakeBinding(1800, 1000, 400, 300));
        w.Hydrate(1920, 1080);
        (w.Rect.x, w.Rect.y, w.Rect.width, w.Rect.height).ShouldBe((1520f, 780f, 400f, 300f));
    }

    [Fact]
    public void Hydrate_RaisesBelowMinToMin()
    {
        var w = Make(MakeBinding(0, 0, 100, 100));
        w.Hydrate(1920, 1080);
        (w.Rect.width, w.Rect.height).ShouldBe((300f, 200f));
    }

    [Fact]
    public void Hydrate_ReadsOpenEntryIntoVisible()
    {
        var w = Make(MakeBinding(10, 10, 400, 300, open: true));
        w.Visible.ShouldBeFalse();
        w.Hydrate(1920, 1080);
        w.Visible.ShouldBeTrue();
    }

    [Fact]
    public void SetRect_ClampsAndPersistsToConfig()
    {
        var b = MakeBinding(0, 0, 400, 300);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.SetRect(10, 20, 500, 400);
        (b.X.Value, b.Y.Value, b.W.Value, b.H.Value).ShouldBe((10f, 20f, 500f, 400f));
        (w.Rect.width, w.Rect.height).ShouldBe((500f, 400f));
    }

    [Fact]
    public void SetRect_BelowMin_ClampsToMin()
    {
        var b = MakeBinding(0, 0, 400, 300);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.SetRect(10, 20, 10, 10);
        (b.W.Value, b.H.Value).ShouldBe((300f, 200f));
    }

    [Fact]
    public void Persist_WritesVisibleToOpenEntry()
    {
        var b = MakeBinding(10, 10, 400, 300, open: false);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        w.Visible = true;
        w.Persist();
        b.Open!.Value.ShouldBeTrue();
    }

    [Fact]
    public void Close_HidesPersistsAndRaisesOnClosed()
    {
        var b = MakeBinding(10, 10, 400, 300, open: true);
        var w = Make(b);
        w.Hydrate(1920, 1080);
        bool raised = false;
        w.OnClosed = () => raised = true;
        w.Close();
        w.Visible.ShouldBeFalse();
        b.Open!.Value.ShouldBeFalse();
        raised.ShouldBeTrue();
    }

    [Fact]
    public void ContentRect_UsesDialogStyleConstants()
    {
        var w = Make(MakeBinding(0, 0, 400, 300));
        w.Hydrate(1920, 1080);
        var c = w.ContentRect;
        (c.x, c.y, c.width, c.height).ShouldBe(
            (DialogStyle.Padding, DialogStyle.HeaderHeight + DialogStyle.Padding,
             400f - 2 * DialogStyle.Padding, 300f - DialogStyle.HeaderHeight - 2 * DialogStyle.Padding));
    }

    [Fact]
    public void UnboundBinding_HydrateAndPersist_DoNotThrow()
    {
        // 테스트 환경의 SettingsPanel 은 Config 가 Bind 되지 않아 null 엔트리로 생성된다
        var w = Make(RectBinding.Of(null!, null!, null!, null!));
        Should.NotThrow(() => w.Hydrate(1920, 1080));
        Should.NotThrow(() => w.Persist());
        (w.Rect.width, w.Rect.height).ShouldBe((300f, 200f));   // 최소 크기로 시작
    }

    [Fact]
    public void Registry_HydrateAllAndPersistAll_TouchEveryWindow()
    {
        var b1 = MakeBinding(1800, 1000, 400, 300);
        var b2 = MakeBinding(0, 0, 100, 100);
        var w1 = Make(b1); var w2 = Make(b2);
        var reg = new PanelRegistry();
        reg.Register(w1); reg.Register(w2);
        reg.HydrateAll(1920, 1080);
        w1.Rect.x.ShouldBe(1520f);
        w2.Rect.width.ShouldBe(300f);
        reg.PersistAll();
        b1.X.Value.ShouldBe(1520f);
        b2.W.Value.ShouldBe(300f);
    }
}
```

- [ ] **Step 2: csproj 링크 추가**

```xml
    <Compile Include="../LongYinRoster/UI/PanelWindow.cs">
      <Link>UI/PanelWindow.cs</Link>
    </Compile>
    <Compile Include="../LongYinRoster/UI/PanelRegistry.cs">
      <Link>UI/PanelRegistry.cs</Link>
    </Compile>
```

- [ ] **Step 3: 실패 확인**

Run: `dotnet test --nologo -v quiet 2>&1 | grep -E "error CS" | head -3`
Expected: `CS0246: 'RectBinding'/'PanelWindow'/'PanelRegistry'` 없음.

- [ ] **Step 4: Config 키 추가**

`src/LongYinRoster/Config.cs` 75번 줄(`ContainerPanelH`) 아래:

```csharp
    // v0.8.0 — SettingsPanel 크기·위치 (이전엔 480×600 고정)
    public static ConfigEntry<float>   SettingsPanelX = null!;
    public static ConfigEntry<float>   SettingsPanelY = null!;
    public static ConfigEntry<float>   SettingsPanelW = null!;
    public static ConfigEntry<float>   SettingsPanelH = null!;
```

187번 줄(`ContainerPanelH = cfg.Bind(...)`) 아래:

```csharp
        SettingsPanelX = cfg.Bind("UI", "SettingsPanelX", 200f, "설정 panel X 좌표 (v0.8.0)");
        SettingsPanelY = cfg.Bind("UI", "SettingsPanelY", 120f, "설정 panel Y 좌표 (v0.8.0)");
        SettingsPanelW = cfg.Bind("UI", "SettingsPanelW", 480f, "설정 panel 폭 (v0.8.0)");
        SettingsPanelH = cfg.Bind("UI", "SettingsPanelH", 600f, "설정 panel 높이 (v0.8.0)");
```

- [ ] **Step 5: PanelWindow 구현**

`src/LongYinRoster/UI/PanelWindow.cs`:

```csharp
using System;
using BepInEx.Configuration;
using LongYinRoster.UI.Layout;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>rect ↔ ConfigEntry 4개(+ 선택적 Open). 테스트 환경에서는 null 엔트리일 수 있어 IsBound 로 가드.</summary>
public sealed class RectBinding
{
    public ConfigEntry<float> X { get; private set; } = null!;
    public ConfigEntry<float> Y { get; private set; } = null!;
    public ConfigEntry<float> W { get; private set; } = null!;
    public ConfigEntry<float> H { get; private set; } = null!;
    public ConfigEntry<bool>? Open { get; private set; }

    public bool IsBound => X != null && Y != null && W != null && H != null;

    public static RectBinding Of(ConfigEntry<float> x, ConfigEntry<float> y, ConfigEntry<float> w, ConfigEntry<float> h,
                                 ConfigEntry<bool>? open = null)
        => new() { X = x, Y = y, W = w, H = h, Open = open };
}

/// <summary>
/// v0.8.0 — 여섯 패널이 공유하는 창 틀. GUI.Window · 배경/헤더/X · 헤더 드래그 · 코너 리사이즈 ·
/// 최소/화면 클램프 · rect ↔ 설정 저장. 순수 수식은 PanelWindowLogic, 여기는 IMGUI 만.
/// 사용: 패널이 인스턴스를 들고 OnGUI(drawContent) 호출. drawContent 는 ContentRect(창 로컬)를 받는다.
/// </summary>
public sealed class PanelWindow
{
    private readonly int _id;
    private readonly string _title;
    private readonly Func<PanelBounds> _bounds;
    private readonly RectBinding _binding;

    private Rect _rect;
    private bool _hydrated;
    private Action<Rect>? _drawContent;

    // 코너 리사이즈 (ContainerPanel v0.7.11 에서 승격)
    private bool    _resizing;
    private Vector2 _resizeStart;
    private Vector2 _resizeStartSize;
    private bool    _mouseUpSeen;
    private Rect    _lastPersisted;
    private Rect    _lastSeenRect;      // 직전 OnGUI 호출의 rect (settle 검출용)
    private bool    _pendingSettle;     // rect 가 바뀐 뒤 아직 저장 안 됨

    public PanelWindow(int windowId, string title, Func<PanelBounds> bounds, RectBinding binding)
    {
        _id = windowId; _title = title; _bounds = bounds; _binding = binding;
        var b = bounds();
        _rect = new Rect(0f, 0f, b.MinW, b.MinH);
    }

    public Rect Rect => _rect;
    public Rect ContentRect => PanelWindowLogic.ContentRect(_rect, DialogStyle.HeaderHeight, DialogStyle.Padding);
    public bool IsHydrated => _hydrated;
    public bool Visible { get; set; }
    /// <summary>X 버튼/Close() 뒤 패널이 부가 정리(버퍼 폐기 등)를 할 때.</summary>
    public Action? OnClosed;

    /// <summary>설정 → rect (화면 클램프). Open 엔트리가 있으면 Visible 도 읽는다. 미바인딩이면 최소 크기 유지.</summary>
    public void Hydrate(float screenW, float screenH)
    {
        if (_binding.IsBound)
        {
            var r = new Rect(_binding.X.Value, _binding.Y.Value, _binding.W.Value, _binding.H.Value);
            _rect = PanelWindowLogic.ClampToScreen(r, screenW, screenH, _bounds());
            if (_binding.Open != null) Visible = _binding.Open.Value;
        }
        else
        {
            _rect = PanelWindowLogic.ClampToScreen(_rect, screenW, screenH, _bounds());
        }
        _lastPersisted = _rect;
        _lastSeenRect  = _rect;
        _hydrated = true;
    }

    /// <summary>설정 패널 숫자 입력 경로. 클램프 후 즉시 저장.</summary>
    public void SetRect(float x, float y, float w, float h)
    {
        _rect = PanelWindowLogic.ClampToScreen(new Rect(x, y, w, h), Screen.width, Screen.height, _bounds());
        Persist();
    }

    /// <summary>rect(+Visible) → 설정. BepInEx 가 값이 바뀔 때만 파일에 쓴다.</summary>
    public void Persist()
    {
        if (!_binding.IsBound) return;
        _binding.X.Value = _rect.x;
        _binding.Y.Value = _rect.y;
        _binding.W.Value = _rect.width;
        _binding.H.Value = _rect.height;
        if (_binding.Open != null) _binding.Open.Value = Visible;
        _lastPersisted = _rect;
    }

    public void Close()
    {
        Visible = false;
        Persist();
        OnClosed?.Invoke();
    }

    /// <summary>매 프레임. Visible 아니면 아무것도 안 함. 순서: 배경 → 헤더 → X → 내용 → 코너 핸들 → DragWindow.</summary>
    public void OnGUI(Action<Rect> drawContent)
    {
        if (!Visible) return;
        if (!_hydrated) Hydrate(Screen.width, Screen.height);
        _drawContent = drawContent;
        try
        {
            _rect = GUI.Window(_id, _rect, (GUI.WindowFunction)DrawWindow, "");
        }
        catch (Exception ex)
        {
            Logger.WarnOnce($"PanelWindow/{_title}/OnGUI", $"{_title} OnGUI: {ex.GetType().Name}: {ex.Message}");
        }
        // 헤더 드래그 종료 검출 — 두 경로:
        //  (a) 콜백에서 MouseUp 을 봤다(정상 종료)
        //  (b) rect 가 바뀐 뒤 이번 호출에서 더 이상 안 바뀐다(게임 창 밖에서 마우스를 놓아 MouseUp 을 못 본 경우)
        // 어느 쪽이든 마지막 저장값과 다르면 화면 클램프 후 저장. (b) 는 드래그 중 잠깐 멈춰도 발동하지만
        // 클램프는 멱등이고 저장은 값이 바뀔 때만 파일에 쓴다(기존 per-frame 저장과 같은 비용 상한).
        bool changed = !SameRect(_rect, _lastSeenRect);
        if (_mouseUpSeen || (_pendingSettle && !changed))
        {
            _mouseUpSeen = false;
            _pendingSettle = false;
            if (!SameRect(_rect, _lastPersisted))
            {
                _rect = PanelWindowLogic.ClampToScreen(_rect, Screen.width, Screen.height, _bounds());
                Persist();
            }
        }
        else if (changed)
        {
            _pendingSettle = true;
        }
        _lastSeenRect = _rect;
    }

    private void DrawWindow(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, _title);
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X")) { Close(); return; }

            var e = Event.current;
            if (e != null && e.type == EventType.MouseUp) _mouseUpSeen = true;

            GUILayout.Space(DialogStyle.HeaderHeight + DialogStyle.Padding);
            _drawContent?.Invoke(ContentRect);

            DrawResizeHandle();
            GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
        }
        catch (Exception ex)
        {
            Logger.WarnOnce($"PanelWindow/{_title}/Draw", $"{_title} Draw: {ex.GetType().Name}: {ex.Message}");
        }
    }

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
            _rect = PanelWindowLogic.Resize(_rect, _resizeStart, _resizeStartSize, e.mousePosition,
                                            _bounds(), Screen.width, Screen.height);
            e.Use();
        }
        else if (_resizing && e.type == EventType.MouseUp)
        {
            _resizing = false;
            Persist();
            e.Use();
        }
    }

    private static bool SameRect(Rect a, Rect b)
        => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
}
```

`src/LongYinRoster/UI/PanelRegistry.cs`:

```csharp
using System.Collections.Generic;

namespace LongYinRoster.UI;

/// <summary>v0.8.0 — 이관된 패널 창의 Hydrate/Persist 를 묶는다. ModWindow 의 설정 주입·저장 블록을 단계별로 흡수.</summary>
public sealed class PanelRegistry
{
    private readonly List<PanelWindow> _windows = new();

    public void Register(PanelWindow window) => _windows.Add(window);

    public void HydrateAll(float screenW, float screenH)
    {
        foreach (var w in _windows) w.Hydrate(screenW, screenH);
    }

    public void PersistAll()
    {
        foreach (var w in _windows) w.Persist();
    }
}
```

- [ ] **Step 6: 통과 확인**

Run: `dotnet test --nologo -v quiet --filter "FullyQualifiedName~PanelWindowTests" 2>&1 | grep -E "error|Passed!|Failed!"`
Expected: `Passed: 10`. `ConfigFile` 생성자가 다르면(BepInEx 6 은 `ConfigFile(string configPath, bool saveOnInit)`) `E:/Games/龙胤立志传.v1.1.0f5/game/BepInEx/core/BepInEx.Core.xml` 에서 서명 확인. 전체 468 pass.

- [ ] **Step 7: 커밋**

```bash
git add src/LongYinRoster/Config.cs src/LongYinRoster/UI/PanelWindow.cs src/LongYinRoster/UI/PanelRegistry.cs src/LongYinRoster.Tests/PanelWindowTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git -c core.safecrlf=false commit -m "feat(v0.8.0-s1): PanelWindow 공통 창 틀 + RectBinding + PanelRegistry + SettingsPanel 크기 설정 키

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: SettingsLayout 계산기

**Files:**
- Create: `src/LongYinRoster/UI/Layout/SettingsLayout.cs`
- Test: `src/LongYinRoster.Tests/SettingsLayoutTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`

**Interfaces:**
- Produces: `SettingsLayout.Compute(Rect content) : SettingsLayout` with `float ScrollH`, `float FieldW`; `SettingsLayout.MinSize : PanelBounds`; consts `HotkeyLabelW=120`, `HotkeyDisplayW=180`, `HotkeyButtonW=80`, `RectLabelW=20`, `RectPairSpace=8`, `MinListRows=3`.

- [ ] **Step 1: 테스트 작성**

```csharp
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S1 — 설정 패널 계산기. 세로 확장 = 설정 스크롤, 가로 확장 = rect 입력 필드.</summary>
public class SettingsLayoutTests
{
    // 기본 창 480×600 → 내용 456×548 (Padding 12, Header 28)
    private static readonly Rect Default = new(12, 40, 456, 548);

    [Fact]
    public void Compute_DefaultWindow_ScrollTakesRemainingHeight()
    {
        var L = SettingsLayout.Compute(Default);
        L.ScrollH.ShouldBe(548f - DialogStyle.ButtonRowHeight - DialogStyle.Gap);   // 516
    }

    [Fact]
    public void Compute_DefaultWindow_FieldWidthSplitsRemaining()
    {
        var L = SettingsLayout.Compute(Default);
        // (456 - 2*20 - 8 - 4) / 2 = 202
        L.FieldW.ShouldBe(202f);
    }

    [Fact]
    public void Compute_TinyContent_UsesFloors()
    {
        var L = SettingsLayout.Compute(new Rect(12, 40, 100, 50));
        L.ScrollH.ShouldBe(3 * DialogStyle.RowHeight);   // 72
        L.FieldW.ShouldBe(40f);
    }

    [Fact]
    public void Compute_TallerContent_MonotonicScroll()
    {
        var a = SettingsLayout.Compute(new Rect(12, 40, 456, 548));
        var b = SettingsLayout.Compute(new Rect(12, 40, 456, 900));
        b.ScrollH.ShouldBeGreaterThan(a.ScrollH);
    }

    [Fact]
    public void MinSize_FitsFixedPartsPlusThreeRows()
    {
        var m = SettingsLayout.MinSize;
        m.MinW.ShouldBe(120f + 180f + 80f + 2 * DialogStyle.Gap + 2 * DialogStyle.Padding);   // 412
        m.MinH.ShouldBe(DialogStyle.HeaderHeight + 2 * DialogStyle.Padding
                        + 3 * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight);   // 156
    }
}
```

- [ ] **Step 2: csproj 링크**

```xml
    <Compile Include="../LongYinRoster/UI/Layout/SettingsLayout.cs">
      <Link>UI/Layout/SettingsLayout.cs</Link>
    </Compile>
```

- [ ] **Step 3: 실패 확인** — Run: `dotnet test --nologo -v quiet 2>&1 | grep -E "error CS" | head -2` → `CS0246 'SettingsLayout'`.

- [ ] **Step 4: 구현**

`src/LongYinRoster/UI/Layout/SettingsLayout.cs`:

```csharp
using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 — SettingsPanel 계산기. 고정: 단축키 행(라벨 120 / 표시 180 / 버튼 80), rect 라벨 20, 하단 버튼 줄 28.
/// 세로 확장: 설정 스크롤(남는 높이 전부). 가로 확장: rect 입력 필드 두 개가 잔여 폭을 반씩.
/// </summary>
public readonly record struct SettingsLayout(float ScrollH, float FieldW)
{
    public const float HotkeyLabelW   = 120f;
    public const float HotkeyDisplayW = 180f;
    public const float HotkeyButtonW  = 80f;
    public const float RectLabelW     = 20f;
    public const float RectPairSpace  = 8f;
    public const int   MinListRows    = 3;

    public static PanelBounds MinSize => new(
        MinW: HotkeyLabelW + HotkeyDisplayW + HotkeyButtonW + 2f * DialogStyle.Gap + 2f * DialogStyle.Padding,
        MinH: DialogStyle.HeaderHeight + 2f * DialogStyle.Padding
              + MinListRows * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight);

    public static SettingsLayout Compute(Rect content)
    {
        float scrollH = Math.Max(MinListRows * DialogStyle.RowHeight,
                                 content.height - DialogStyle.ButtonRowHeight - DialogStyle.Gap);
        float fieldW  = Math.Max(40f, (content.width - 2f * RectLabelW - RectPairSpace - DialogStyle.Gap) / 2f);
        return new SettingsLayout(scrollH, fieldW);
    }
}
```

- [ ] **Step 5: 통과 확인** — `--filter "FullyQualifiedName~SettingsLayoutTests"` → `Passed: 5`. 전체 473.

- [ ] **Step 6: 커밋**

```bash
git add src/LongYinRoster/UI/Layout/SettingsLayout.cs src/LongYinRoster.Tests/SettingsLayoutTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git -c core.safecrlf=false commit -m "feat(v0.8.0-s1): SettingsLayout 계산기 (스크롤 높이·필드 폭·최소 크기)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: SettingsPanel 이관 + ModWindow 등록 + 인게임 smoke + PR

**Files:**
- Modify: `src/LongYinRoster/UI/SettingsPanel.cs` (전체 구조 변경 — 아래 diff 단위로)
- Modify: `src/LongYinRoster/UI/ModWindow.cs:93` (필드), `:170-177` (wire-up), `:1053` (OnGUI 앞에 OnDestroy 추가)
- Test: `src/LongYinRoster.Tests/SettingsPanelTests.cs` (케이스 추가)

**Interfaces:**
- Consumes: `PanelWindow`, `RectBinding`, `PanelRegistry`, `SettingsLayout`, `Config.SettingsPanelX/Y/W/H`, `Config.PlayerEditorPanel*`, `Config.ItemGenPanel*`.
- Produces: `SettingsPanel.Window : PanelWindow`, `Visible`(위임), `WindowRect`(위임), `BufferSelfX/Y/W/H`, `SetBufferSelfRect(float,float,float,float)`, `HydrateFromValues(main, ch, co, se, x, y, w, h, sx = 200, sy = 120, sw = 480, sh = 600)`, `internal static bool TryParseRectField(string, float min, out float)`.

- [ ] **Step 1: 테스트 추가 (SettingsPanelTests.cs)**

먼저 파일 상단 `using` 블록에 `using LongYinRoster.UI.Layout;` 를 추가한다(`SettingsLayout` 참조). 그다음 클래스 닫는 `}` 앞에:

```csharp
    // ── v0.8.0 S1 — 설정 패널 자기 rect 버퍼 ──

    [Fact]
    public void SelfRect_DefaultsAfterHydrate()
    {
        var p = MakePanel();
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((200f, 120f, 480f, 600f));
        p.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void SetBufferSelfRect_MarksDirty()
    {
        var p = MakePanel();
        p.SetBufferSelfRect(10, 20, 700, 800);
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((10f, 20f, 700f, 800f));
        p.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void RestoreDefaults_ResetsSelfRectToo()
    {
        var p = MakePanel();
        p.SetBufferSelfRect(10, 20, 700, 800);
        p.DoRestoreDefaults();
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((200f, 120f, 480f, 600f));
    }

    [Fact]
    public void SelfRect_TextParse_IgnoresGarbage()
    {
        SettingsPanel.TryParseRectField("abc", 100f, out _).ShouldBeFalse();
        SettingsPanel.TryParseRectField("10", 100f, out _).ShouldBeFalse();     // 최소 미만 무시
        SettingsPanel.TryParseRectField("640", 100f, out var v).ShouldBeTrue();
        v.ShouldBe(640f);
    }

    [Fact]
    public void Window_StartsHiddenWithSettingsMinSize()
    {
        var p = new SettingsPanel();
        p.Visible.ShouldBeFalse();
        p.Window.Rect.width.ShouldBe(SettingsLayout.MinSize.MinW);
    }
```


- [ ] **Step 2: 실패 확인** — `dotnet test --nologo -v quiet 2>&1 | grep -E "error CS" | head -3` → `CS1061 'SettingsPanel'에 'BufferSelfX' 정의가 없음` 등.

- [ ] **Step 3: SettingsPanel 구현 — 상태·버퍼 (IMGUI 아닌 부분)**

`src/LongYinRoster/UI/SettingsPanel.cs` 를 아래처럼 바꾼다. 유지되는 코드는 생략 없이 표기 — 이 파일은 통째로 교체한다고 생각하고 작성할 것.

```csharp
using System;
using LongYinRoster.UI.Layout;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;   // UnityEngine.Logger 모호성 회피

namespace LongYinRoster.UI;

/// <summary>
/// v0.7.6 — Hybrid stateful-only 설정 panel.
/// hotkey 4 + ContainerPanel rect 4 + (v0.8.0) SettingsPanel 자기 rect 4 buffer 편집 + [저장]/[기본값 복원]/[취소].
/// v0.8.0 — 창 틀은 PanelWindow(코너 리사이즈·설정 자동 저장), 내부 치수는 SettingsLayout.
/// </summary>
public sealed class SettingsPanel
{
    // 자체 default (Config default 와 sync 유지 — 둘 다 변경 시 같이 갱신)
    internal const KeyCode DefaultMain      = KeyCode.F11;
    internal const KeyCode DefaultCharacter = KeyCode.Alpha1;
    internal const KeyCode DefaultContainer = KeyCode.Alpha2;
    internal const KeyCode DefaultSettings  = KeyCode.Alpha3;
    internal const float DefaultContainerX = 150f, DefaultContainerY = 100f;
    internal const float DefaultContainerW = 800f, DefaultContainerH = 760f;
    internal const float DefaultSelfX = 200f, DefaultSelfY = 120f;      // v0.8.0 — Config.SettingsPanel* 기본과 동일
    internal const float DefaultSelfW = 480f, DefaultSelfH = 600f;

    private const int WindowID = 0x4C593733;  // "LY73"

    public PanelWindow Window { get; }
    public bool Visible { get => Window.Visible; set => Window.Visible = value; }
    public Rect WindowRect => Window.Rect;

    public SettingsPanel()
    {
        Window = new PanelWindow(WindowID, "설정", () => SettingsLayout.MinSize,
            RectBinding.Of(Config.SettingsPanelX, Config.SettingsPanelY, Config.SettingsPanelW, Config.SettingsPanelH));
        Window.OnClosed = DiscardState;
    }

    // Buffer (저장 누르기 전까지 ConfigEntry 안 건드림)
    public KeyCode BufferMain        { get; private set; } = DefaultMain;
    public KeyCode BufferCharacter   { get; private set; } = DefaultCharacter;
    public KeyCode BufferContainer   { get; private set; } = DefaultContainer;
    public KeyCode BufferSettings    { get; private set; } = DefaultSettings;
    public float   BufferContainerX  { get; private set; } = DefaultContainerX;
    public float   BufferContainerY  { get; private set; } = DefaultContainerY;
    public float   BufferContainerW  { get; private set; } = DefaultContainerW;
    public float   BufferContainerH  { get; private set; } = DefaultContainerH;
    public float   BufferSelfX       { get; private set; } = DefaultSelfX;
    public float   BufferSelfY       { get; private set; } = DefaultSelfY;
    public float   BufferSelfW       { get; private set; } = DefaultSelfW;
    public float   BufferSelfH       { get; private set; } = DefaultSelfH;

    // Original (hydrate 시점) — IsDirty 비교용
    private KeyCode _origMain, _origCharacter, _origContainer, _origSettings;
    private float   _origContainerX, _origContainerY, _origContainerW, _origContainerH;
    private float   _origSelfX, _origSelfY, _origSelfW, _origSelfH;
    private bool    _hydrated;

    public Action? OnSaved;

    public bool HasConflict { get; private set; }
    public string ConflictMessage { get; private set; } = "";

    public bool IsDirty =>
        BufferMain != _origMain || BufferCharacter != _origCharacter
        || BufferContainer != _origContainer || BufferSettings != _origSettings
        || BufferContainerX != _origContainerX || BufferContainerY != _origContainerY
        || BufferContainerW != _origContainerW || BufferContainerH != _origContainerH
        || BufferSelfX != _origSelfX || BufferSelfY != _origSelfY
        || BufferSelfW != _origSelfW || BufferSelfH != _origSelfH;

    public bool CanSave => IsDirty && !HasConflict;

    /// <summary>Production hydrate — Config 읽기. ModWindow Settings transition 에서 호출.</summary>
    public void Hydrate()
    {
        // 창 rect 는 PanelRegistry.HydrateAll(Awake) 이 먼저 읽지만, 순서가 바뀌어도 안전하도록 가드
        if (!Window.IsHydrated) Window.Hydrate(Screen.width, Screen.height);
        HydrateFromValues(
            Config.ToggleHotkey.Value, Config.HotkeyCharacterMode.Value,
            Config.HotkeyContainerMode.Value, Config.HotkeySettingsMode.Value,
            Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
            Config.ContainerPanelW.Value, Config.ContainerPanelH.Value,
            Window.Rect.x, Window.Rect.y, Window.Rect.width, Window.Rect.height);
    }

    /// <summary>Test-only — Config 의존성 회피. 자기 rect 4개는 선택 인자(기존 테스트 호환).</summary>
    internal void HydrateFromValues(
        KeyCode main, KeyCode ch, KeyCode co, KeyCode se,
        float x, float y, float w, float h,
        float sx = DefaultSelfX, float sy = DefaultSelfY, float sw = DefaultSelfW, float sh = DefaultSelfH)
    {
        BufferMain       = _origMain       = main;
        BufferCharacter  = _origCharacter  = ch;
        BufferContainer  = _origContainer  = co;
        BufferSettings   = _origSettings   = se;
        BufferContainerX = _origContainerX = x;
        BufferContainerY = _origContainerY = y;
        BufferContainerW = _origContainerW = w;
        BufferContainerH = _origContainerH = h;
        BufferSelfX      = _origSelfX      = sx;
        BufferSelfY      = _origSelfY      = sy;
        BufferSelfW      = _origSelfW      = sw;
        BufferSelfH      = _origSelfH      = sh;
        _hydrated = true;
        RecomputeConflict();
    }

    public void SetBufferMain(KeyCode k)      { BufferMain = k;      RecomputeConflict(); }
    public void SetBufferCharacter(KeyCode k) { BufferCharacter = k; RecomputeConflict(); }
    public void SetBufferContainer(KeyCode k) { BufferContainer = k; RecomputeConflict(); }
    public void SetBufferSettings(KeyCode k)  { BufferSettings = k;  RecomputeConflict(); }

    public void SetBufferContainerRect(float x, float y, float w, float h)
    {
        BufferContainerX = x; BufferContainerY = y;
        BufferContainerW = w; BufferContainerH = h;
    }

    public void SetBufferSelfRect(float x, float y, float w, float h)
    {
        BufferSelfX = x; BufferSelfY = y;
        BufferSelfW = w; BufferSelfH = h;
    }

    public void DoRestoreDefaults()
    {
        BufferMain       = DefaultMain;
        BufferCharacter  = DefaultCharacter;
        BufferContainer  = DefaultContainer;
        BufferSettings   = DefaultSettings;
        BufferContainerX = DefaultContainerX;
        BufferContainerY = DefaultContainerY;
        BufferContainerW = DefaultContainerW;
        BufferContainerH = DefaultContainerH;
        BufferSelfX = DefaultSelfX; BufferSelfY = DefaultSelfY;
        BufferSelfW = DefaultSelfW; BufferSelfH = DefaultSelfH;
        RecomputeConflict();
    }

    /// <summary>Buffer → ConfigEntry. CanSave false 면 no-op. 자기 rect 는 Window.SetRect(클램프+저장). 호출 후 OnSaved 발화.</summary>
    public void DoSave()
    {
        if (!CanSave) return;
        Config.ToggleHotkey.Value         = BufferMain;
        Config.HotkeyCharacterMode.Value  = BufferCharacter;
        Config.HotkeyContainerMode.Value  = BufferContainer;
        Config.HotkeySettingsMode.Value   = BufferSettings;
        Config.ContainerPanelX.Value      = BufferContainerX;
        Config.ContainerPanelY.Value      = BufferContainerY;
        Config.ContainerPanelW.Value      = BufferContainerW;
        Config.ContainerPanelH.Value      = BufferContainerH;
        Window.SetRect(BufferSelfX, BufferSelfY, BufferSelfW, BufferSelfH);
        // 클램프된 실제 값을 버퍼에 반영
        BufferSelfX = Window.Rect.x; BufferSelfY = Window.Rect.y;
        BufferSelfW = Window.Rect.width; BufferSelfH = Window.Rect.height;
        // _orig 갱신 (다시 dirty 안 보이도록)
        _origMain = BufferMain; _origCharacter = BufferCharacter;
        _origContainer = BufferContainer; _origSettings = BufferSettings;
        _origContainerX = BufferContainerX; _origContainerY = BufferContainerY;
        _origContainerW = BufferContainerW; _origContainerH = BufferContainerH;
        _origSelfX = BufferSelfX; _origSelfY = BufferSelfY; _origSelfW = BufferSelfW; _origSelfH = BufferSelfH;
        _rectBufHydrated = false;
        OnSaved?.Invoke();
    }

    /// <summary>자동 영속 항목 + 6 패널 window rect 를 hardcoded default 로 즉시 reset (v0.8.0: PlayerEditor/ItemGen/Settings 추가).</summary>
    public void DoResetPersistedView()
    {
        Config.ContainerSortKey.Value        = "Category";
        Config.ContainerSortAscending.Value  = true;
        Config.ContainerFilterCategory.Value = "All";
        Config.ContainerLastIndex.Value      = -1;
        Config.WindowX.Value             = 1100f; Config.WindowY.Value = 100f;
        Config.WindowW.Value             = 720f;  Config.WindowH.Value = 560f;
        Config.ItemDetailPanelX.Value    = 970f;  Config.ItemDetailPanelY.Value = 100f;
        Config.ItemDetailPanelWidth.Value = 380f; Config.ItemDetailPanelHeight.Value = 500f;
        Config.ContainerPanelX.Value     = DefaultContainerX; Config.ContainerPanelY.Value = DefaultContainerY;
        Config.ContainerPanelW.Value     = DefaultContainerW; Config.ContainerPanelH.Value = DefaultContainerH;
        Config.PlayerEditorPanelX.Value  = 200f; Config.PlayerEditorPanelY.Value = 120f;
        Config.PlayerEditorPanelW.Value  = 720f; Config.PlayerEditorPanelH.Value = 720f;
        Config.ItemGenPanelX.Value       = 300f; Config.ItemGenPanelY.Value = 150f;
        Config.ItemGenPanelW.Value       = 620f; Config.ItemGenPanelH.Value = 560f;
        Window.SetRect(DefaultSelfX, DefaultSelfY, DefaultSelfW, DefaultSelfH);
        // buffer + orig 동기화 (사용자가 panel 닫고 재열 때 immediate 효과)
        BufferContainerX = _origContainerX = DefaultContainerX;
        BufferContainerY = _origContainerY = DefaultContainerY;
        BufferContainerW = _origContainerW = DefaultContainerW;
        BufferContainerH = _origContainerH = DefaultContainerH;
        BufferSelfX = _origSelfX = Window.Rect.x;  BufferSelfY = _origSelfY = Window.Rect.y;
        BufferSelfW = _origSelfW = Window.Rect.width; BufferSelfH = _origSelfH = Window.Rect.height;
    }

    /// <summary>rect 텍스트 필드 파싱. 실패 또는 min 미만이면 false (무시).</summary>
    internal static bool TryParseRectField(string text, float min, out float value)
    {
        if (float.TryParse(text, out value) && value >= min) return true;
        value = 0f;
        return false;
    }

    private void RecomputeConflict()
    {
        // 4 hotkey 중 2개 이상 같은 KeyCode → conflict.
        // KeyCode.None 은 무시 (할당 안 됨 의도).
        var keys   = new[] { BufferMain, BufferCharacter, BufferContainer, BufferSettings };
        var labels = new[] { "MainKey", "CharacterMode", "ContainerMode", "SettingsMode" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] == KeyCode.None) continue;
            for (int j = i + 1; j < keys.Length; j++)
            {
                if (keys[j] == KeyCode.None) continue;
                if (keys[i] == keys[j])
                {
                    HasConflict = true;
                    ConflictMessage = $"⚠ 충돌: {keys[i]} ({labels[i]} / {labels[j]} 동일)";
                    return;
                }
            }
        }
        HasConflict = false;
        ConflictMessage = "";
    }
```

- [ ] **Step 4: SettingsPanel 구현 — IMGUI 부분 (같은 파일, 위 코드에 이어서)**

```csharp
    // ── UI / 키 캡처 / textfield buffer ──

    private enum CaptureSlot { None, Main, Character, Container, Settings }
    private CaptureSlot _capture = CaptureSlot.None;

    private Vector2 _scroll = Vector2.zero;

    // Rect textfield buffer — IMGUI string 입력 → float parse
    private string _xBuf = "", _yBuf = "", _wBuf = "", _hBuf = "";
    private string _sxBuf = "", _syBuf = "", _swBuf = "", _shBuf = "";
    private bool   _rectBufHydrated;

    public void OnGUI()
    {
        if (!Visible) return;
        if (!_hydrated) Hydrate();
        Window.OnGUI(DrawContent);

        // 키 캡처 — Event.current 는 OnGUI scope 안에서만 valid.
        if (_capture != CaptureSlot.None && Event.current != null && Event.current.type == EventType.KeyDown)
        {
            var k = Event.current.keyCode;
            if (k == KeyCode.Escape)
            {
                _capture = CaptureSlot.None;
            }
            else if (k != KeyCode.None)
            {
                switch (_capture)
                {
                    case CaptureSlot.Main:      SetBufferMain(k);      break;
                    case CaptureSlot.Character: SetBufferCharacter(k); break;
                    case CaptureSlot.Container: SetBufferContainer(k); break;
                    case CaptureSlot.Settings:  SetBufferSettings(k);  break;
                }
                _capture = CaptureSlot.None;
            }
            Event.current.Use();
        }
    }

    private void DrawContent(Rect content)
    {
        var L = SettingsLayout.Compute(content);

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(L.ScrollH));

        GUILayout.Label("⚠ 고급 설정은 BepInExConfigManager (F5) 에서 변경");
        GUILayout.Space(8);

        // ──── 단축키 섹션 ────
        GUILayout.Label("▼ 단축키");
        DrawHotkeyRow("메인 토글:",     CaptureSlot.Main,      BufferMain);
        DrawHotkeyRow("캐릭터 관리:",   CaptureSlot.Character, BufferCharacter);
        DrawHotkeyRow("컨테이너 관리:", CaptureSlot.Container, BufferContainer);
        DrawHotkeyRow("설정 panel:",    CaptureSlot.Settings,  BufferSettings);

        if (HasConflict)
        {
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.4f, 0.4f, 1f);
            GUILayout.Label(ConflictMessage);
            GUI.color = prev;
        }

        GUILayout.Space(10);

        // ──── 컨테이너 panel rect 섹션 ────
        GUILayout.Label("▼ 컨테이너 panel 위치/크기");
        HydrateRectBuffersIfNeeded();
        DrawRectPair("X:", ref _xBuf, "Y:", ref _yBuf, L.FieldW);
        DrawRectPair("W:", ref _wBuf, "H:", ref _hBuf, L.FieldW);
        if (TryParseRectField(_xBuf, float.MinValue, out var cx)) BufferContainerX = cx;
        if (TryParseRectField(_yBuf, float.MinValue, out var cy)) BufferContainerY = cy;
        if (TryParseRectField(_wBuf, 100f, out var cw)) BufferContainerW = cw;
        if (TryParseRectField(_hBuf, 100f, out var chh)) BufferContainerH = chh;

        GUILayout.Space(10);

        // ──── v0.8.0 설정 panel 자기 rect 섹션 ────
        GUILayout.Label("▼ 설정 panel 위치/크기 (코너 드래그로도 조절)");
        DrawRectPair("X:", ref _sxBuf, "Y:", ref _syBuf, L.FieldW);
        DrawRectPair("W:", ref _swBuf, "H:", ref _shBuf, L.FieldW);
        if (TryParseRectField(_sxBuf, float.MinValue, out var sx)) BufferSelfX = sx;
        if (TryParseRectField(_syBuf, float.MinValue, out var sy)) BufferSelfY = sy;
        if (TryParseRectField(_swBuf, SettingsLayout.MinSize.MinW, out var sw)) BufferSelfW = sw;
        if (TryParseRectField(_shBuf, SettingsLayout.MinSize.MinH, out var sh)) BufferSelfH = sh;

        GUILayout.Space(10);

        // ──── 영속화 정보 (read-only) ────
        GUILayout.Label("▼ 영속화 정보 (자동 저장)");
        DrawPersistedView();

        GUILayout.EndScrollView();

        // ──── 하단 버튼 (scrollview 밖 — 항상 보임) ────
        GUILayout.BeginHorizontal();
        var prevEnabled = GUI.enabled;
        GUI.enabled = CanSave;
        if (GUILayout.Button("저장", GUILayout.Height(DialogStyle.ButtonRowHeight)))
        {
            DoSave();
            ToastService.Push("✔ 설정 저장됨", ToastKind.Success);
        }
        GUI.enabled = prevEnabled;
        if (GUILayout.Button("기본값 복원", GUILayout.Height(DialogStyle.ButtonRowHeight)))
        {
            DoRestoreDefaults();
            _rectBufHydrated = false;   // textfield buffer 재hydrate
        }
        if (GUILayout.Button("취소", GUILayout.Height(DialogStyle.ButtonRowHeight))) Window.Close();
        GUILayout.EndHorizontal();
    }

    /// <summary>X / 취소 뒤 정리. Visible 은 PanelWindow 가 이미 false 로 둠.</summary>
    private void DiscardState()
    {
        _capture = CaptureSlot.None;
        _hydrated = false;        // 다음 진입 시 ConfigEntry 재hydrate
        _rectBufHydrated = false;
    }

    private void HydrateRectBuffersIfNeeded()
    {
        if (_rectBufHydrated) return;
        _xBuf = BufferContainerX.ToString("F0");
        _yBuf = BufferContainerY.ToString("F0");
        _wBuf = BufferContainerW.ToString("F0");
        _hBuf = BufferContainerH.ToString("F0");
        _sxBuf = BufferSelfX.ToString("F0");
        _syBuf = BufferSelfY.ToString("F0");
        _swBuf = BufferSelfW.ToString("F0");
        _shBuf = BufferSelfH.ToString("F0");
        _rectBufHydrated = true;
    }

    private void DrawHotkeyRow(string label, CaptureSlot slot, KeyCode current)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(SettingsLayout.HotkeyLabelW));
        var prev = GUI.color;
        if (_capture == slot) GUI.color = Color.cyan;
        string display = _capture == slot ? "키 입력 대기..." : current.ToString();
        GUILayout.Label($"[{display}]", GUILayout.Width(SettingsLayout.HotkeyDisplayW));
        GUI.color = prev;
        if (GUILayout.Button("재설정", GUILayout.Width(SettingsLayout.HotkeyButtonW)))
        {
            _capture = slot;
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>"X: [   ]   Y: [   ]" 한 줄. 필드 폭은 계산기가 준 값(가로 확장).</summary>
    private static void DrawRectPair(string labelA, ref string bufA, string labelB, ref string bufB, float fieldW)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(labelA, GUILayout.Width(SettingsLayout.RectLabelW));
        bufA = GUILayout.TextField(bufA, GUILayout.Width(fieldW));
        GUILayout.Space(SettingsLayout.RectPairSpace);
        GUILayout.Label(labelB, GUILayout.Width(SettingsLayout.RectLabelW));
        bufB = GUILayout.TextField(bufB, GUILayout.Width(fieldW));
        GUILayout.EndHorizontal();
    }

    private void DrawPersistedView()
    {
        string sortKr = Config.ContainerSortKey.Value switch
        {
            "Category" => "카테고리", "Name" => "이름",
            "Grade"    => "등급",     "Quality" => "품질",
            _          => Config.ContainerSortKey.Value
        };
        string arrow = Config.ContainerSortAscending.Value ? "▲" : "▼";
        GUILayout.Label($"정렬: {sortKr} {arrow}");
        GUILayout.Label($"필터: {Config.ContainerFilterCategory.Value}");

        int last = Config.ContainerLastIndex.Value;
        GUILayout.Label($"마지막 컨테이너: {(last > 0 ? "#" + last : "(미선택)")}");

        GUILayout.Label($"Mod 창: ({Config.WindowX.Value:F0}, {Config.WindowY.Value:F0}, {Config.WindowW.Value:F0}×{Config.WindowH.Value:F0})");
        GUILayout.Label($"ItemDetail: ({Config.ItemDetailPanelX.Value:F0}, {Config.ItemDetailPanelY.Value:F0}, {Config.ItemDetailPanelWidth.Value:F0}×{Config.ItemDetailPanelHeight.Value:F0})");
        GUILayout.Label($"ContainerPanel: ({Config.ContainerPanelX.Value:F0}, {Config.ContainerPanelY.Value:F0}, {Config.ContainerPanelW.Value:F0}×{Config.ContainerPanelH.Value:F0})");
        GUILayout.Label($"PlayerEditor: ({Config.PlayerEditorPanelX.Value:F0}, {Config.PlayerEditorPanelY.Value:F0}, {Config.PlayerEditorPanelW.Value:F0}×{Config.PlayerEditorPanelH.Value:F0})");
        GUILayout.Label($"ItemGen: ({Config.ItemGenPanelX.Value:F0}, {Config.ItemGenPanelY.Value:F0}, {Config.ItemGenPanelW.Value:F0}×{Config.ItemGenPanelH.Value:F0})");

        GUILayout.Space(4);
        if (GUILayout.Button("영속화 정보 reset", GUILayout.Width(140)))
        {
            DoResetPersistedView();
            _rectBufHydrated = false;
            ToastService.Push("✔ 영속화 정보 reset 됨", ToastKind.Success);
        }
    }
}
```

주의: 기존 `CloseAndDiscard()` 는 사라지고 X → `PanelWindow.Close()` → `OnClosed = DiscardState`. 기존 `_rect`, `GUI.Window`, `DialogStyle.FillBackground/DrawHeader`, `GUI.DragWindow` 호출은 전부 `PanelWindow` 안으로 갔으므로 이 파일에 남아 있으면 안 된다.

- [ ] **Step 5: ModWindow 연결**

`src/LongYinRoster/UI/ModWindow.cs`:

(a) 93번 줄 `private readonly SettingsPanel _settingsPanel = new();` 아래에:

```csharp
    // v0.8.0 — 이관된 패널 창의 Hydrate/Persist 묶음 (단계별로 항목 추가)
    private readonly PanelRegistry _registry = new();
```

(b) 170~177번 줄의 `HotkeyMap.Bind(); _settingsPanel.OnSaved = () => { ... };` 바로 아래에:

```csharp
        // v0.8.0 S1 — SettingsPanel 창 틀 등록 + 설정 → rect (화면 클램프)
        _registry.Register(_settingsPanel.Window);
        _registry.HydrateAll(Screen.width, Screen.height);
```

(c) `private void OnGUI()` (1053번 줄) 바로 위에:

```csharp
    // v0.8.0 — 게임 종료/씬 파괴 시 이관 패널 rect 저장 (드래그/리사이즈 종료 시에도 각자 저장하지만 안전망)
    private void OnDestroy()
    {
        try { _registry.PersistAll(); }
        catch (Exception ex) { Logger.WarnOnce("ModWindow/OnDestroy", $"PersistAll: {ex.GetType().Name}: {ex.Message}"); }
    }
```

`ModWindow.cs` 상단에 `using System;` 이 없으면 추가(`Exception`). `Screen` 은 `UnityEngine` — 이미 using.

- [ ] **Step 6: 테스트·빌드**

Run: `cd E:/LylzzBox && DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error|warning CS|Passed!|Failed!"`
Expected: `Passed: 478` (473 + 5), 경고 0. 기존 `SettingsPanelTests` 12개 전부 유지.
Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Debug --nologo -v minimal 2>&1 | grep -E "warn|error|Build succeeded"` → `Build succeeded`, 경고 0.

- [ ] **Step 7: 인게임 smoke (Release 배포 → 게임 실행 → 체크리스트)**

게임이 꺼져 있는지 확인(`tasklist | grep -i LongYinLiZhiZhuan` 비어야 함) 후:

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release --nologo -v minimal` → `DeployToBepInEx` 가 `E:/Games/龙胤立志传.v1.1.0f5/game/BepInEx/plugins/LongYinRoster/` 로 복사.

게임 실행 → 세이브 로드 → F11 → 3 (설정) 에서:
1. 창이 설정값 위치(기본 200,120)에 480×600 으로 뜸. 로그에 `PanelWindow/설정` 경고 없음.
2. 우하단 코너 드래그로 400×300 까지 줄이기 → 412×156 미만으로 안 줄어듦, 스크롤 영역이 줄고 버튼 줄은 항상 보임, **가로 스크롤바·잘림 없음**(있으면 `DialogStyle.Padding` 12 → 16 으로 올리고 재빌드).
3. 코너 드래그로 900×900 까지 키우기 → rect 입력 필드가 넓어짐, 스크롤 영역이 커짐.
4. 헤더 드래그로 화면 밖으로 밀기 → 창이 화면 안으로 되돌아옴.
5. 설정 panel W 필드에 `abc` 입력 → 무시. `10` 입력 → 저장 시 412 로 클램프되어 저장됨(Toast "설정 저장됨", 창 크기 반영).
6. X 로 닫고 F11+3 재진입 → 마지막 크기·위치 그대로. **게임 재시작** 후에도 그대로(`BepInEx/config/com.deepe.longyinroster.cfg` 의 `SettingsPanelW/H` 확인).
7. "영속화 정보 reset" → 설정 창이 200,120 480×600 으로 즉시 이동·리사이즈.
8. 단축키 재설정·저장·기본값 복원·취소 — v0.7.13 과 동일 동작(회귀 없음).

로그 확인: `grep -a "PanelWindow\|SettingsPanel" E:/Games/龙胤立志传.v1.1.0f5/game/BepInEx/LogOutput.log` 에 `[Warning` 없음.

- [ ] **Step 8: 커밋 + PR**

```bash
git add src/LongYinRoster/UI/SettingsPanel.cs src/LongYinRoster/UI/ModWindow.cs src/LongYinRoster.Tests/SettingsPanelTests.cs
git -c core.safecrlf=false commit -m "feat(v0.8.0-s1): SettingsPanel 을 PanelWindow 로 이관 — 코너 리사이즈·자기 rect 설정·SettingsLayout 적용

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push -u origin feat/v0.8.0-step1-settings
gh pr create --base develop --head feat/v0.8.0-step1-settings \
  --title "feat(v0.8.0-s1): PanelWindow 공통 창 틀 + LayoutMath + SettingsPanel 이관" \
  --body "스펙 §11 1단계. PanelWindow/PanelWindowLogic/LayoutMath/PanelRegistry 신설, SettingsPanel 이관(코너 리사이즈 + SettingsPanelX/Y/W/H 신설), DialogStyle 행/간격 상수. tests 427 → 478. 인게임 smoke 8항목 PASS.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
gh pr merge --merge
```

---

### Task 6: ItemGenLayout 계산기 (2단계 시작)

**Files:**
- Create: `src/LongYinRoster/UI/Layout/ItemGenLayout.cs`
- Test: `src/LongYinRoster.Tests/ItemGenLayoutTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj`

**Interfaces:**
- Consumes: `LayoutMath.Wrap/RowsThatFit`, `DialogStyle.*`, `PanelBounds`.
- Produces: `ItemGenLayout.Compute(Rect content, bool hasSecondary) : ItemGenLayout` with `int CategoryPerRow, CategoryRows, SecondaryPerRow, SecondaryRows`, `float SearchFieldW, ListH, RowButtonW`, `int PageSize`; `ItemGenLayout.MinSize`; consts `CategoryCount=7`, `SecondaryCount=7`, `CategoryCellW=55`, `SecondaryCellW=45`, `SearchLabelW=40`, `GradeCellW=48`, `MinListRows=3`.

- [ ] **Step 0: 2단계 브랜치**

```bash
git fetch origin develop && git checkout -b feat/v0.8.0-step2-itemgen origin/develop
```

- [ ] **Step 1: 테스트 작성**

```csharp
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S2 — 아이템 생성 패널 계산기. 세로 확장 = 결과 리스트(페이지 크기 자동), 탭은 폭에 맞춰 줄바꿈.</summary>
public class ItemGenLayoutTests
{
    // 기본 창 620×560 → 내용 596×508
    private static readonly Rect Default = new(12, 40, 596, 508);

    [Fact]
    public void Compute_DefaultWindow_TabsFitOneRowEach()
    {
        var L = ItemGenLayout.Compute(Default, hasSecondary: true);
        L.CategoryPerRow.ShouldBeGreaterThanOrEqualTo(7);
        L.CategoryRows.ShouldBe(1);
        L.SecondaryRows.ShouldBe(1);
    }

    [Fact]
    public void Compute_DefaultWindow_PageSizeIsTen()
    {
        // 탭 2줄(2×32) + 고정 행(검색 24 + 페이저 24 + 등급/품질/수량 3×28 + 5×4 = 152) → 리스트 292 → floor(296/28) = 10
        var L = ItemGenLayout.Compute(Default, hasSecondary: true);
        L.ListH.ShouldBe(292f);
        L.PageSize.ShouldBe(10);
    }

    [Fact]
    public void Compute_NoSecondary_ListGetsTheRow()
    {
        var a = ItemGenLayout.Compute(Default, hasSecondary: true);
        var b = ItemGenLayout.Compute(Default, hasSecondary: false);
        b.SecondaryRows.ShouldBe(0);
        b.ListH.ShouldBe(a.ListH + DialogStyle.ButtonRowHeight + DialogStyle.Gap);
    }

    [Fact]
    public void Compute_NarrowContent_WrapsTabs()
    {
        var L = ItemGenLayout.Compute(new Rect(12, 40, 300, 508), hasSecondary: true);
        L.CategoryPerRow.ShouldBe(5);    // floor(304/59)
        L.CategoryRows.ShouldBe(2);
        L.SecondaryPerRow.ShouldBe(6);   // floor(304/49)
        L.SecondaryRows.ShouldBe(2);
        L.PageSize.ShouldBeLessThan(10);
    }

    [Fact]
    public void Compute_TallerContent_MorePageRows()
    {
        var a = ItemGenLayout.Compute(Default, true);
        var b = ItemGenLayout.Compute(new Rect(12, 40, 596, 900), true);
        b.PageSize.ShouldBeGreaterThan(a.PageSize);
    }

    [Fact]
    public void Compute_TinyContent_ListFloorsAtThreeRows()
    {
        var L = ItemGenLayout.Compute(new Rect(12, 40, 200, 100), true);
        L.ListH.ShouldBe(3 * DialogStyle.RowHeight);
        L.PageSize.ShouldBe(3);
    }

    [Fact]
    public void Compute_WidthsFollowContent()
    {
        var L = ItemGenLayout.Compute(Default, true);
        L.SearchFieldW.ShouldBe(596f - ItemGenLayout.SearchLabelW - DialogStyle.Gap);
        L.RowButtonW.ShouldBe(596f - 20f);
    }

    [Fact]
    public void MinSize_CoversWidestFixedRowAndThreeListRows()
    {
        var m = ItemGenLayout.MinSize;
        // 카테고리 탭 한 줄(7×55 + 6×4 = 409)이 등급 줄(356)보다 넓다 → 409 + 24 = 433
        m.MinW.ShouldBe(ItemGenLayout.CategoryCount * ItemGenLayout.CategoryCellW + (ItemGenLayout.CategoryCount - 1) * DialogStyle.Gap + 2 * DialogStyle.Padding);
        var L = ItemGenLayout.Compute(new Rect(12, 40, m.MinW - 2 * DialogStyle.Padding, m.MinH - DialogStyle.HeaderHeight - 2 * DialogStyle.Padding), true);
        L.CategoryRows.ShouldBe(1);   // 최소 폭에서도 탭은 한 줄
        L.PageSize.ShouldBe(3);
    }
}
```

- [ ] **Step 2: csproj 링크**

```xml
    <!-- v0.8.0 S2 -->
    <Compile Include="../LongYinRoster/UI/Layout/ItemGenLayout.cs">
      <Link>UI/Layout/ItemGenLayout.cs</Link>
    </Compile>
```

- [ ] **Step 3: 실패 확인** — `CS0246 'ItemGenLayout'`.

- [ ] **Step 4: 구현**

`src/LongYinRoster/UI/Layout/ItemGenLayout.cs`:

```csharp
using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 — ItemGeneratorPanel 계산기. 고정: 탭 셀 폭(카테고리 55 / 2차 45), 검색 라벨 40, 등급·품질 버튼 48,
/// 검색 행·페이저 행(24), 등급·품질·수량 줄(28). 세로 확장: 결과 리스트 → PageSize = 높이에서 계산(최소 3).
/// 탭은 폭에 맞춰 줄바꿈(Wrap) — 줄 수가 늘면 리스트가 그만큼 줄어든다.
/// </summary>
public readonly record struct ItemGenLayout(
    int CategoryPerRow, int CategoryRows, int SecondaryPerRow, int SecondaryRows,
    float SearchFieldW, float ListH, int PageSize, float RowButtonW)
{
    public const int   CategoryCount  = 7;    // ItemGenCategory: 장비/단약/음식/비급/보물/재료/말
    public const int   SecondaryCount = 7;    // 전체 + 무기/갑옷/투구/신발/장신구/마구
    public const float CategoryCellW  = 55f;
    public const float SecondaryCellW = 45f;
    public const float SearchLabelW   = 40f;
    public const float GradeCellW     = 48f;
    public const float ScrollbarSlack = 20f;
    public const int   MinListRows    = 3;

    // 검색 행 + 페이저 행 + 등급/품질/수량 3줄, 각 줄 뒤 Gap
    private static float FixedRowsH =>
        2f * DialogStyle.RowHeight + 3f * DialogStyle.ButtonRowHeight + 5f * DialogStyle.Gap;

    public static PanelBounds MinSize => new(
        MinW: Math.Max(6f * GradeCellW + SearchLabelW + 7f * DialogStyle.Gap,                       // 등급/품질 줄 (356)
                       CategoryCount * CategoryCellW + (CategoryCount - 1) * DialogStyle.Gap)       // 카테고리 탭 한 줄 (409) — 최소 폭에서 탭이 접히지 않게
              + 2f * DialogStyle.Padding,
        MinH: DialogStyle.HeaderHeight + 2f * DialogStyle.Padding
              + 2f * (DialogStyle.ButtonRowHeight + DialogStyle.Gap)                            // 탭 2줄(한 줄씩)
              + FixedRowsH
              + MinListRows * DialogStyle.RowHeight + (MinListRows - 1) * DialogStyle.Gap);

    public static ItemGenLayout Compute(Rect content, bool hasSecondary)
    {
        int catPerRow = LayoutMath.Wrap(content.width, CategoryCellW, DialogStyle.Gap);
        int catRows   = (CategoryCount + catPerRow - 1) / catPerRow;
        int secPerRow = LayoutMath.Wrap(content.width, SecondaryCellW, DialogStyle.Gap);
        int secRows   = hasSecondary ? (SecondaryCount + secPerRow - 1) / secPerRow : 0;

        float tabsH  = (catRows + secRows) * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);
        float listH  = Math.Max(MinListRows * DialogStyle.RowHeight, content.height - tabsH - FixedRowsH);
        int pageSize = LayoutMath.RowsThatFit(listH, DialogStyle.RowHeight, DialogStyle.Gap, MinListRows);

        float searchW = Math.Max(60f, content.width - SearchLabelW - DialogStyle.Gap);
        float rowW    = Math.Max(60f, content.width - ScrollbarSlack);
        return new ItemGenLayout(catPerRow, catRows, secPerRow, secRows, searchW, listH, pageSize, rowW);
    }
}
```

- [ ] **Step 5: 통과 확인** — `--filter "FullyQualifiedName~ItemGenLayoutTests"` → `Passed: 8`. 전체 486.

- [ ] **Step 6: 커밋**

```bash
git add src/LongYinRoster/UI/Layout/ItemGenLayout.cs src/LongYinRoster.Tests/ItemGenLayoutTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git -c core.safecrlf=false commit -m "feat(v0.8.0-s2): ItemGenLayout 계산기 — 탭 줄바꿈·리스트 높이·자동 페이지 크기

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: ItemGeneratorPanel 이관 + ModWindow 정리 + smoke + PR

**Files:**
- Modify: `src/LongYinRoster/UI/ItemGeneratorPanel.cs` (전체 교체)
- Modify: `src/LongYinRoster/UI/ModWindow.cs:189-196` (Init 제거·등록), `:1085-1090` (저장 블록 제거)
- Test: `src/LongYinRoster.Tests/ItemGenFilterTests.cs` (케이스 1개 추가)

**Interfaces:**
- Consumes: `PanelWindow`, `RectBinding.Of(..., Config.ItemGenPanelOpen)`, `ItemGenLayout`, `ItemGenFilter.Apply/Page` (`Core/ItemGenEntry.cs`), `ItemGenCategoryNames.Korean`, `ItemRareLvNames.EquipLvNames/QualityNames`, `ItemFactory.Generate`, `ItemDbCache.All`.
- Produces: `ItemGeneratorPanel.Window : PanelWindow`, `Visible`/`WindowRect` 위임, `Func<object?>? GetPlayer` (유지). `Init(x,y,w,h)` 삭제.

- [ ] **Step 1: 페이지 보정 테스트 추가** (`ItemGenFilterTests.cs` 클래스 끝)

```csharp
    [Fact]
    public void Page_ClampsWhenPageSizeGrows()
    {
        // 25개, 페이지 크기 10 에서 3페이지(idx 2) 보던 중 창을 키워 페이지 크기 20 → 총 2페이지 → idx 1 로 당김
        var items = new List<ItemGenEntry>();
        for (int i = 0; i < 25; i++) items.Add(new ItemGenEntry { Id = i, NameRaw = $"n{i}" });
        var (slice, total) = ItemGenFilter.Page(items, page: 2, pageSize: 20);
        total.ShouldBe(2);
        slice.Count.ShouldBe(5);          // 마지막 페이지(idx 1) = 20..24
        slice[0].Id.ShouldBe(20);
    }
```

Run: `--filter "FullyQualifiedName~ItemGenFilterTests"` → 이미 통과해야 함(`Page` 가 page 를 내부에서 클램프). 통과하면 이 테스트는 회귀 고정용 — 커밋에 포함.

- [ ] **Step 2: ItemGeneratorPanel 전체 교체**

```csharp
using System;
using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.UI.Layout;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

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
```

`DoGenerate`/`WarnIfOverweight`/`ParseInt` 는 v0.7.13 그대로(변경 없음). 사라진 것: `_rect`, `_pageSize`, `Init`, `Draw(int)`, `표시 10/15/20` 버튼, 자체 `GUI.Window`/헤더/X/DragWindow.

- [ ] **Step 3: ModWindow 정리**

(a) 189~196번 줄의 ItemGen wire-up 을 다음으로 교체:

```csharp
        // v0.7.13 — ItemGeneratorPanel wire-up / v0.8.0 S2 — 창 틀 등록 (rect·Open 은 Hydrate 가 읽음)
        _itemGenPanel.GetPlayer = Core.HeroLocator.GetPlayer;
        _registry.Register(_itemGenPanel.Window);
```

`_registry.HydrateAll(...)` 호출(1단계에서 SettingsPanel 등록 직후에 넣은 것)은 **이 Register 뒤로 이동**한다 — 등록 순서: Settings → ItemGen → HydrateAll.

(b) 1085번 줄 주석 `// v0.7.13 — ItemGenPanel rect/visibility 영속화` 와 그 아래 대입 5줄(1086~1090, `Config.ItemGenPanelX/Y/W/H/Open.Value = ...`) 삭제. PanelWindow 가 드래그/리사이즈/닫힘 시 저장하고 `OnDestroy` 가 안전망.

- [ ] **Step 4: 테스트·빌드**

Run: `dotnet test --nologo -v quiet 2>&1 | grep -E "error|warning CS|Passed!|Failed!"` → `Passed: 487` (486 + Page 테스트 1), 경고 0.
Run: `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Debug --nologo -v minimal` → `Build succeeded`, 경고 0. (`ItemGeneratorPanel.cs` 는 테스트에 링크되지 않으므로 Debug 빌드가 컴파일 검증.)

- [ ] **Step 5: 인게임 smoke**

Release 빌드·배포(게임 꺼진 상태) → 게임 실행 → 세이브 로드 → F11 → 5:
1. 기본 620×560 에서 리스트 **10행**(v0.7.13 과 동일), `표시` 버튼 없음, 페이저 라벨에 `(N개, 10행)`.
2. 코너 드래그로 높이 900 → 페이지당 행 수가 20 이상으로 늘고 스크롤바 없음. 폭 420 까지 줄이기 → 카테고리 탭이 2줄로 접히고 리스트가 그만큼 줄어듦, **가로 잘림 없음**.
3. 비급 탭에서 3페이지를 보다가 창을 키우기 → 페이지가 마지막 페이지로 당겨지고 빈 화면 없음.
4. 장비 탭 → 무기 선택 → 검색 "검" → 절세/극품 → 수량 1 → 생성 → Toast 성공 + 인벤에 추가(회귀 없음).
5. X 로 닫고 F11+5 재진입 → 크기·위치 유지. 재시작 후 `ItemGenPanelW/H/Open` cfg 값이 마지막 상태.
6. 로그 `PanelWindow/아이템 생성` 경고 없음.

- [ ] **Step 6: 커밋 + PR**

```bash
git add src/LongYinRoster/UI/ItemGeneratorPanel.cs src/LongYinRoster/UI/ModWindow.cs src/LongYinRoster.Tests/ItemGenFilterTests.cs
git -c core.safecrlf=false commit -m "feat(v0.8.0-s2): ItemGeneratorPanel 을 PanelWindow 로 이관 — 자동 페이지 크기·탭 줄바꿈, 표시 10/15/20 제거

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push -u origin feat/v0.8.0-step2-itemgen
gh pr create --base develop --head feat/v0.8.0-step2-itemgen \
  --title "feat(v0.8.0-s2): ItemGeneratorPanel 반응형 — 자동 페이지 크기 + 탭 줄바꿈 + 코너 리사이즈" \
  --body "스펙 §11 2단계. ItemGenLayout 계산기, ItemGeneratorPanel 을 PanelWindow 로 이관(Init/per-frame 저장 블록 제거, PanelRegistry 등록). 표시 10/15/20 버튼 제거(브레인스토밍 결정). tests 478 → 487. 인게임 smoke 6항목 PASS.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
gh pr merge --merge
```

- [ ] **Step 7: HANDOFF 갱신 (별도 docs 커밋, 같은 PR 에 포함해도 됨)**

`docs/HANDOFF.md` 의 진행 상태 줄에 "v0.8.0 1·2단계 완료(PanelWindow / SettingsPanel / ItemGeneratorPanel), 3단계 ContainerPanel 플랜 작성 예정" 을 추가하고 Releases 목록에 `**v0.8.0-s1/s2** (날짜)` 항목을 넣는다(형식은 v0.7.13.1 항목 참조).

---

## 다음 플랜 (이 문서 범위 밖)

3단계 ContainerPanel 부터는 `PanelWindow`/`LayoutMath` 의 실제 코드가 `develop` 에 있는 상태에서 같은 형식으로 플랜을 쓴다. 3단계에서 특히 확인할 것: `ContainerPanel.DrawResizeHandle`·`MIN_W/H`·`MAX_W/H` 삭제, `TOTAL_H = 640` → `ContainerLayout`(`LayoutMath.SplitHeight` 사용), `SettingsPanel.OnSaved` 의 `_containerPanel.SetRect(...)` → `Window.SetRect`, ModWindow 의 ContainerPanel per-frame 저장 블록 제거.
