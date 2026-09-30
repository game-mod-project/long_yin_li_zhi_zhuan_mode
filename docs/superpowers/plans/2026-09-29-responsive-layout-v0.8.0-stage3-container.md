# 반응형 패널 레이아웃 v0.8.0 — 3단계 구현 플랜 (ContainerPanel → PanelWindow + ContainerLayout)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ContainerPanel(컨테이너 관리, F11+2)을 1·2단계에서 만든 `PanelWindow` 창 틀 위로 옮기고, 하드코딩된 `TOTAL_H = 640`·`MIN_W/H`·`MAX_W/H`·열 폭 390·컨테이너 리스트 500 을 `ContainerLayout` 계산기로 대체해, 창 크기·접힘·프리셋에 따라 좌/우 열 폭과 세 리스트 높이가 재조정되게 한다.

**Architecture:** 3단계 착수 전에 1·2단계 리뷰가 남긴 설계 결정 둘을 먼저 처리한다 — (1) IMGUI 암묵 여백(`ImguiSlack`)을 계산기마다 빼던 것을 `PanelWindowLogic.ContentRect` 가 한 번 흡수(`DialogStyle.ChromeH`), (2) 겹친 창의 코너 핸들 우선순위를 `PanelRegistry` 가 z-order 로 소유(클릭 = 맨 앞으로, 핸들 판정은 맨 앞 창만) + 아직 이관 안 된 창(ItemDetail 등)이 덮은 자리는 리사이즈 시작 금지. 그 위에서 `ContainerLayout`(순수 계산기: 열 폭 `SplitWidth`, 인벤/창고 `SplitHeight`, 컨테이너 리스트 = 잔여)을 만들고 ContainerPanel 을 합성으로 이관한다.

**Tech Stack:** C# 10 / net6.0 / BepInEx 6 IL2CPP / Unity IMGUI(GUI·GUILayout) / xunit + Shouldly. 테스트는 소스 링크(`<Compile Include>`) + `UnityStubs.cs`(`GUI.Window` 스텁은 콜백을 호출하지 않음, `Event.current` 는 정적 단일 인스턴스, `Screen` 1920×1080).

**Spec:** `docs/superpowers/specs/2026-09-29-longyin-roster-mod-v0.8.0-responsive-layout-design.md` (§6 ContainerPanel 행, §8, §11 3단계)

**전제:** `develop` = dcd8ce7 (PR #4~#7 머지, tests 495). 1·2단계 산출물 `UI/PanelWindow.cs`, `UI/PanelRegistry.cs`, `UI/Layout/{PanelBounds,LayoutMath,PanelWindowLogic,SettingsLayout,ItemGenLayout}.cs` 가 있다.

**이 플랜이 내리는 설계 결정 (1·2단계 리뷰 이월분)**
- **D1 ImguiSlack 흡수**: `DialogStyle.ChromeH = HeaderHeight + 2*Padding + ImguiSlack (= 84)`. `PanelWindowLogic.ContentRect` 가 `bottomSlack` 을 빼서 계산기가 받는 `content.height` 는 이미 "쓸 수 있는 높이". 계산기는 `Compute` 에서 슬랙을 다시 빼지 않고, `MinSize.MinH = ChromeH + 내용 최소` 로 쓴다. 렌더 결과는 1·2단계와 동일(수치 이동만).
- **D2 z-order**: `PanelRegistry` 가 등록 창 리스트를 z-order(끝 = 맨 앞)로 유지. `HandleEvents()`(ModWindow.OnGUI 맨 앞)가 MouseDown 위치의 맨 앞 창을 앞으로 올리고 그 창에만 핸들 판정을 시킨다. 등록된 창의 `PanelWindow.OnGUI` 는 더 이상 이벤트를 직접 처리하지 않는다(미등록 단독 창은 그대로). 이관 안 된 창은 `IsCoveredByForeign` 콜백(ModWindow 제공)으로 "덮여 있으면 시작 금지" — 6단계에서 전부 등록되면 제거.

## Global Constraints

- `LangVersion` 10, `net6.0`, `Nullable` enable, `TreatWarningsAsErrors` true — 경고 하나가 빌드 실패.
- IMGUI 금지 API: `GUILayout.FlexibleSpace`, `GUILayoutUtility.GetLastRect`, `GUILayout.BeginArea`, `GUILayout.ExpandWidth/ExpandHeight`. 새 IMGUI API 도입 금지 — ContainerPanel 이 이미 쓰는 `GUILayoutUtility.GetRect`, `GUILayout.Toggle`, `GUILayout.BeginVertical` 은 유지 가능.
- 게임 조작 코드(`Core/*Applier`, `*Reflector`, `*Patch`, `Containers/*Ops*`) 변경 금지. ContainerPanel 의 이동/복사/삭제/Undo/focus 로직은 손대지 않는다(레이아웃·창 틀만).
- 소스 `.cs` 는 CRLF. 새 파일은 `LongYinRoster.Tests.csproj` 에 `<Compile Include>` 링크 추가.
- 기존 설정 키 이름 불변. 이 단계 신설 키 없음(`ContainerPanelX/Y/W/H`, `Container/InventoryCollapsed·StorageCollapsed·SplitPreset` 그대로).
- `Plugin.VERSION` 불변(6단계 완료 시 0.8.0).
- 행 높이·간격 상수의 단일 출처는 `DialogStyle` (`HeaderHeight=28`, `RowHeight=24`, `ButtonRowHeight=28`, `Padding=12`, `Gap=4`, `ScrollbarW=20`, `ImguiSlack=32`, 신설 `ChromeH=84`). Draw 의 모든 행은 `GUILayout.Height(DialogStyle.RowHeight | ButtonRowHeight)` 를 명시해 계산기와 렌더가 같은 숫자를 쓴다. 행 사이 `GUILayout.Space(n)` 는 넣지 않는다(행마다 `Gap` 하나로 계산).
- 커밋 메시지 끝에 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. 브랜치: 플랜 문서 `docs/v0.8.0-stage3-plan`, 구현 `feat/v0.8.0-step3-container` — 각각 `develop` 에서 분기, PR → `develop`, merge commit.
- Release 빌드(DeployToBepInEx)는 게임이 꺼진 상태에서만 — `tasklist | grep -i LongYinLiZhiZhuan` 이 비어야 한다.

## Review Focus

1. 인벤·창고를 둘 다 접은 뒤 창을 최소로 줄이고 다시 펼침 → 리스트가 3행 밑으로 안 내려가고 예외 없음 — Task 3 `Compute_Tiny_ListsFloorAtThreeRows`, `Compute_BothCollapsed_ZeroLists`.
2. 컨테이너 드롭다운을 연 채(컨테이너 20개) → 우측 리스트가 줄되 3행 밑으로 안 감 — Task 3 `Compute_ExtraRightRows_ShrinkContainerList`.
3. ⓘ 상세 패널이 컨테이너 창 우하단을 덮은 상태에서 그 자리를 클릭 → 컨테이너가 리사이즈되지 않음 — Task 2 `HandleEvents_ForeignCoverBlocksResize`.
4. 설정 패널 컨테이너 W 에 `700`(새 최소 758 미만) 입력·저장 → 무시, `900` → 저장 즉시 창이 따라감 — Task 4 `ContainerRect_TextParse_BelowLayoutMin_Ignored` + smoke 8.
5. 구 cfg 의 `ContainerPanelW = 600`(구 MIN_W) 로드 → 창이 758 로 올라가고 위치는 화면 안 — Task 4 `Window_StartsHiddenWithContainerMinSize` + PanelWindow 의 기존 `Hydrate_RaisesBelowMinToMin`.

---

## 파일 구조

**신규**
- `src/LongYinRoster/UI/Layout/ContainerLayout.cs` — `ContainerLayoutState` + `ContainerLayout` 계산기.
- `src/LongYinRoster.Tests/ContainerLayoutTests.cs`, `src/LongYinRoster.Tests/PanelRegistryTests.cs`.

**수정**
- `src/LongYinRoster/UI/DialogStyle.cs` — `ChromeH` 상수.
- `src/LongYinRoster/UI/Layout/PanelWindowLogic.cs` — `ContentRect(..., float bottomSlack)`.
- `src/LongYinRoster/UI/Layout/SettingsLayout.cs`, `ItemGenLayout.cs` — 슬랙 제거·`ChromeH` 사용.
- `src/LongYinRoster/UI/PanelWindow.cs` — `Registry`, `TryBeginResize`, `HandleResizeContinuation`.
- `src/LongYinRoster/UI/PanelRegistry.cs` — z-order + `HandleEvents` + `IsCoveredByForeign`.
- `src/LongYinRoster/UI/ContainerPanel.cs` — PanelWindow 합성, `ContainerLayout` 적용, 자체 리사이즈/창 코드 삭제.
- `src/LongYinRoster/UI/SearchSortToolbar.cs` — 검색창 폭 인자.
- `src/LongYinRoster/UI/SettingsPanel.cs` — 컨테이너 W/H 필드 하한을 `ContainerLayout.MinSize` 로.
- `src/LongYinRoster/UI/ModWindow.cs` — 컨테이너 창 등록·`HandleEvents`·`IsCoveredByForeign`·per-frame 저장 블록 삭제·`OnSaved` 경로.
- 테스트: `PanelWindowLogicTests.cs`, `PanelWindowTests.cs`, `SettingsLayoutTests.cs`, `ItemGenLayoutTests.cs`(수치 이동), `ContainerPanelFocusTests.cs`, `SettingsPanelTests.cs`(케이스 추가), `LongYinRoster.Tests.csproj`(링크 2개).
- `docs/HANDOFF.md`.

테스트 수: 495 → Task 1 495(수정만) → Task 2 500 → Task 3 516 → Task 4 518.

---

### Task 0: 플랜 PR + 3단계 브랜치

**Files:** 없음 (git 만)

- [ ] **Step 1: 플랜 브랜치를 develop 에 PR**

```bash
cd E:/LylzzBox
git push -u origin docs/v0.8.0-stage3-plan
gh pr create --base develop --head docs/v0.8.0-stage3-plan \
  --title "docs: v0.8.0 3단계(ContainerPanel) 플랜" \
  --body "플랜 문서만. 설계 결정 D1(ImguiSlack → ContentRect 흡수) · D2(PanelRegistry z-order) 포함.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
gh pr merge --merge
```

- [ ] **Step 2: 3단계 브랜치 생성**

```bash
git fetch origin develop && git checkout -b feat/v0.8.0-step3-container origin/develop
```

---

### Task 1: D1 — `DialogStyle.ChromeH` + `ContentRect` 가 암묵 여백을 흡수

**Files:**
- Modify: `src/LongYinRoster/UI/DialogStyle.cs` (`ImguiSlack` 줄 아래)
- Modify: `src/LongYinRoster/UI/Layout/PanelWindowLogic.cs` (`ContentRect`)
- Modify: `src/LongYinRoster/UI/PanelWindow.cs` (`ContentRect` 프로퍼티)
- Modify: `src/LongYinRoster/UI/Layout/SettingsLayout.cs`, `src/LongYinRoster/UI/Layout/ItemGenLayout.cs`
- Test: `src/LongYinRoster.Tests/PanelWindowLogicTests.cs`, `PanelWindowTests.cs`, `SettingsLayoutTests.cs`, `ItemGenLayoutTests.cs`

**Interfaces:**
- Produces: `DialogStyle.ChromeH : const float (84)`, `PanelWindowLogic.ContentRect(Rect window, float headerH, float padding, float bottomSlack) : Rect`. 계산기 계약 변경: `Compute(content)` 의 `content.height` 는 슬랙이 이미 빠진 값; `MinSize.MinH = DialogStyle.ChromeH + 내용 최소`.

- [ ] **Step 1: 테스트 수정 (실패 예정)**

`src/LongYinRoster.Tests/PanelWindowLogicTests.cs` — 기존 `ContentRect_SubtractsHeaderAndPadding_InLocalCoords` 와 `ContentRect_NeverNegative` 두 메서드를 아래로 교체:

```csharp
    [Fact]
    public void ContentRect_SubtractsHeaderPaddingAndBottomSlack_InLocalCoords()
    {
        // 800×600 창, 헤더 28, 여백 12, 하단 암묵 여백 32 → (12, 40, 776, 516). 계산기는 이 516 을 그대로 쓴다(D1).
        var c = PanelWindowLogic.ContentRect(new Rect(100, 50, 800, 600), headerH: 28f, padding: 12f, bottomSlack: 32f);
        (c.x, c.y, c.width, c.height).ShouldBe((12f, 40f, 776f, 516f));
    }

    [Fact]
    public void ContentRect_NeverNegative()
    {
        var c = PanelWindowLogic.ContentRect(new Rect(0, 0, 10, 10), 28f, 12f, 32f);
        (c.width, c.height).ShouldBe((0f, 0f));
    }
```

`src/LongYinRoster.Tests/PanelWindowTests.cs` — `ContentRect_UsesDialogStyleConstants` 의 기대값을:

```csharp
        (c.x, c.y, c.width, c.height).ShouldBe(
            (DialogStyle.Padding, DialogStyle.HeaderHeight + DialogStyle.Padding,
             400f - 2 * DialogStyle.Padding, 300f - DialogStyle.ChromeH));
```

`src/LongYinRoster.Tests/SettingsLayoutTests.cs` — 세 곳:

```csharp
    // 기본 창 480×600 → 내용 456×516 (ChromeH 84 = 헤더 28 + 여백 24 + 암묵 여백 32 — ContentRect 가 이미 뺀다, D1)
    private static readonly Rect Default = new(12, 40, 456, 516);
```
```csharp
        // 버튼 줄 + gap 만 뺀다 — 암묵 여백은 ContentRect 가 이미 뺐다(D1)
        L.ScrollH.ShouldBe(516f - DialogStyle.ButtonRowHeight - DialogStyle.Gap);   // 484
```
```csharp
        m.MinH.ShouldBe(DialogStyle.ChromeH + 3 * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight);   // 188
```
(`Compute_TallerContent_MonotonicScroll` 의 `new Rect(12, 40, 456, 548)` 도 `516` 으로 맞춘다 — 값 자체는 결과에 영향 없음.)

`src/LongYinRoster.Tests/ItemGenLayoutTests.cs` — 세 곳:

```csharp
    // 기본 창 620×560 → 내용 596×476 (ChromeH 84 — ContentRect 가 이미 뺀다, D1)
    private static readonly Rect Default = new(12, 40, 596, 476);
```
```csharp
        // 탭 2줄(2×32) + 고정 행(검색 24 + 페이저 24 + 등급/품질/수량 3×28 + 5×4 = 152) → 리스트 476-64-152 = 260 → floor(264/28) = 9
        var L = ItemGenLayout.Compute(Default, hasSecondary: true);
        L.ListH.ShouldBe(476f - 64f - 152f);
        L.PageSize.ShouldBe(9);
```
`Compute_NarrowContent_WrapsTabs` 의 `new Rect(12, 40, 300, 508)` → `new Rect(12, 40, 300, 476)`. `MinSize_CoversWidestFixedRowAndThreeListRows` 의 `Compute(...)` 인자:
```csharp
        var L = ItemGenLayout.Compute(new Rect(12, 40, m.MinW - 2 * DialogStyle.Padding, m.MinH - DialogStyle.ChromeH), true);
```

- [ ] **Step 2: 실패 확인**

Run: `cd E:/LylzzBox && DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error CS" | sed 's/.*error //; s/\[E:.*//' | sort -u | head -3`
Expected: `CS0117: 'DialogStyle' does not contain a definition for 'ChromeH'` 와 `CS1501: No overload for method 'ContentRect' takes 4 arguments`.

- [ ] **Step 3: 구현**

`src/LongYinRoster/UI/DialogStyle.cs` — `ImguiSlack` 줄 바로 아래:

```csharp
    /// <summary>창 크롬 총 높이 = 헤더 + 상하 여백 + 암묵 여백(84). 창 높이 − ChromeH = 계산기가 쓸 수 있는 내용 높이(PanelWindow.ContentRect.height).
    /// 계산기 MinH 는 ChromeH + 내용 최소 — Compute 에서 ImguiSlack 을 다시 빼지 않는다.</summary>
    public  const float ChromeH         = HeaderHeight + 2f * Padding + ImguiSlack;
```

`src/LongYinRoster/UI/Layout/PanelWindowLogic.cs` — `ContentRect` 교체:

```csharp
    /// <summary>헤더·여백·하단 암묵 여백(GUILayout margin + window skin padding)을 뺀 내용 영역. 창 로컬 좌표(0,0 = 창 좌상단).
    /// 폭·높이는 0 미만으로 내려가지 않음. 계산기는 이 높이를 그대로 쓴다(슬랙을 다시 빼지 않음).</summary>
    public static Rect ContentRect(Rect window, float headerH, float padding, float bottomSlack)
    {
        float w = Math.Max(0f, window.width - 2f * padding);
        float h = Math.Max(0f, window.height - headerH - 2f * padding - bottomSlack);
        return new Rect(padding, headerH + padding, w, h);
    }
```

`src/LongYinRoster/UI/PanelWindow.cs` — `ContentRect` 프로퍼티:

```csharp
    public Rect ContentRect => PanelWindowLogic.ContentRect(_rect, DialogStyle.HeaderHeight, DialogStyle.Padding, DialogStyle.ImguiSlack);
```

`src/LongYinRoster/UI/Layout/SettingsLayout.cs` — `MinSize` 와 `Compute` 의 `scrollH`:

```csharp
    public static PanelBounds MinSize => new(
        MinW: HotkeyLabelW + HotkeyDisplayW + HotkeyButtonW + HotkeyRowMargins + DialogStyle.ScrollbarW + 2f * DialogStyle.Padding,
        MinH: DialogStyle.ChromeH + MinListRows * DialogStyle.RowHeight + DialogStyle.Gap + DialogStyle.ButtonRowHeight);
```
```csharp
        // 세로: 버튼 줄 + gap. 암묵 여백은 ContentRect 가 이미 뺐다(D1). 가로: 세로 스크롤바 폭 + 컨트롤 margin.
        float scrollH = Math.Max(MinListRows * DialogStyle.RowHeight,
                                 content.height - DialogStyle.ButtonRowHeight - DialogStyle.Gap);
```
클래스 doc 의 "암묵 여백 32" 는 삭제.

`src/LongYinRoster/UI/Layout/ItemGenLayout.cs` — `MinSize` 의 `MinH`, `Compute` 의 `listH`, 클래스 doc 의 마지막 줄:

```csharp
        MinH: DialogStyle.ChromeH
              + 2f * (DialogStyle.ButtonRowHeight + DialogStyle.Gap)                            // 탭 2줄(한 줄씩)
              + FixedRowsH
              + MinListRows * DialogStyle.RowHeight + (MinListRows - 1) * DialogStyle.Gap);
```
```csharp
        float listH  = Math.Max(MinListRows * DialogStyle.RowHeight, content.height - tabsH - FixedRowsH);
```
doc: `/// 암묵 여백은 PanelWindowLogic.ContentRect 가 뺀다(D1) — 여기서 다시 빼지 않는다.`

- [ ] **Step 4: 통과 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error|Passed!|Failed!"`
Expected: `Passed: 495`(수정만, 개수 불변). `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Debug --nologo -v minimal` → `Build succeeded`, 경고 0.

- [ ] **Step 5: 커밋**

```bash
git add src/LongYinRoster/UI/DialogStyle.cs src/LongYinRoster/UI/Layout/PanelWindowLogic.cs src/LongYinRoster/UI/PanelWindow.cs src/LongYinRoster/UI/Layout/SettingsLayout.cs src/LongYinRoster/UI/Layout/ItemGenLayout.cs src/LongYinRoster.Tests/PanelWindowLogicTests.cs src/LongYinRoster.Tests/PanelWindowTests.cs src/LongYinRoster.Tests/SettingsLayoutTests.cs src/LongYinRoster.Tests/ItemGenLayoutTests.cs
git -c core.safecrlf=false commit -m "refactor(v0.8.0-s3): ImguiSlack 을 PanelWindowLogic.ContentRect 가 흡수 — DialogStyle.ChromeH, 계산기는 슬랙을 다시 빼지 않음 (D1)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: D2 — `PanelRegistry` z-order 이벤트 라우팅 + 미이관 창 가드

**Files:**
- Modify: `src/LongYinRoster/UI/PanelWindow.cs` (`OnGUI` 의 `HandleResizeEvents();` 줄, `HandleResizeEvents` 메서드)
- Modify: `src/LongYinRoster/UI/PanelRegistry.cs` (전체 교체)
- Create: `src/LongYinRoster.Tests/PanelRegistryTests.cs` (테스트 프로젝트 폴더 안이라 csproj 링크 불필요)

**Interfaces:**
- Consumes: `PanelWindowLogic.ResizeHandleRect(Rect)`, `PanelWindow.Rect/Visible/Persist`.
- Produces: `PanelWindow.Registry : PanelRegistry? (internal set)`, `PanelWindow.IsResizing : bool (internal)`, `internal bool TryBeginResize(Event e)`, `internal bool HandleResizeContinuation(Event e)`; `PanelRegistry.HandleEvents()`, `PanelRegistry.IsCoveredByForeign : Func<Vector2,bool>?`, `internal PanelWindow? TopmostVisibleAt(Vector2)`. `Register` 가 `window.Registry = this` 를 세팅한다.

- [ ] **Step 1: 테스트 작성**

`src/LongYinRoster.Tests/PanelRegistryTests.cs`:

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

/// <summary>v0.8.0 S3 — PanelRegistry 가 등록 창의 코너 리사이즈 이벤트를 z-order 로 라우팅한다.
/// 스텁의 Event.current 는 정적 단일 인스턴스라 각 테스트가 finally 로 되돌린다. GUI.Window 스텁은 콜백을 호출하지 않는다.</summary>
public class PanelRegistryTests
{
    private static readonly PanelBounds B = new(300f, 200f);

    private static RectBinding MakeBinding(float x, float y, float w, float h)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lyr-registry-{Guid.NewGuid():N}.cfg");
        var cfg = new ConfigFile(path, false) { SaveOnConfigSet = false };
        return RectBinding.Of(cfg.Bind("T", "X", x, ""), cfg.Bind("T", "Y", y, ""), cfg.Bind("T", "W", w, ""), cfg.Bind("T", "H", h, ""));
    }

    private static PanelWindow Make(int id, float x, float y, float w, float h)
    {
        var win = new PanelWindow(id, $"w{id}", () => B, MakeBinding(x, y, w, h));
        win.Hydrate(1920, 1080);
        win.Visible = true;
        return win;
    }

    // A: (100,100,400,300) → 핸들 (484..500, 384..400). B: (300,200,400,300) 가 A 의 핸들 자리를 덮는다.
    private static (PanelRegistry reg, PanelWindow a, PanelWindow b) Overlapping()
    {
        var a = Make(1, 100, 100, 400, 300);
        var b = Make(2, 300, 200, 400, 300);
        var reg = new PanelRegistry();
        reg.Register(a);
        reg.Register(b);     // 나중 등록 = 맨 앞
        return (reg, a, b);
    }

    private static void Fire(PanelRegistry reg, EventType type, float x, float y)
    {
        var e = Event.current;
        e.type = type; e.mousePosition = new Vector2(x, y);
        reg.HandleEvents();
    }

    private static void Reset() { Event.current.type = EventType.Repaint; Event.current.mousePosition = default; }

    [Fact]
    public void HandleEvents_HandleOfCoveredWindow_DoesNotResize()
    {
        var (reg, a, b) = Overlapping();
        try
        {
            Fire(reg, EventType.MouseDown, 492, 392);   // A 의 핸들이지만 B 가 그 자리를 덮고 있다
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
            (b.Rect.width, b.Rect.height).ShouldBe((400f, 300f));
            a.IsResizing.ShouldBeFalse();
        }
        finally { Reset(); }
    }

    [Fact]
    public void HandleEvents_ClickInsideWindow_BringsItToFront_ThenItsHandleWins()
    {
        var (reg, a, b) = Overlapping();
        try
        {
            Fire(reg, EventType.MouseDown, 150, 150);   // A 만 포함하는 자리 → A 가 맨 앞으로
            Fire(reg, EventType.MouseUp,   150, 150);
            reg.TopmostVisibleAt(new Vector2(492, 392)).ShouldBeSameAs(a);
            Fire(reg, EventType.MouseDown, 492, 392);   // 이제 A 의 핸들이 이긴다
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((608f, 508f));
            (b.Rect.width, b.Rect.height).ShouldBe((400f, 300f));
            Fire(reg, EventType.MouseUp, 700, 600);
            a.IsResizing.ShouldBeFalse();
        }
        finally { Reset(); }
    }

    [Fact]
    public void HandleEvents_HiddenWindowIsIgnored()
    {
        var (reg, a, b) = Overlapping();
        try
        {
            b.Visible = false;
            Fire(reg, EventType.MouseDown, 492, 392);
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((608f, 508f));
            Fire(reg, EventType.MouseUp, 700, 600);
        }
        finally { Reset(); }
    }

    [Fact]
    public void HandleEvents_ForeignCoverBlocksResize()
    {
        // 아직 이관 안 된 창(ItemDetail 등)이 그 자리를 덮고 있으면 등록 창은 리사이즈를 시작하지 않는다
        var (reg, a, b) = Overlapping();
        try
        {
            b.Visible = false;
            reg.IsCoveredByForeign = _ => true;
            Fire(reg, EventType.MouseDown, 492, 392);
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
        }
        finally { Reset(); }
    }

    [Fact]
    public void RegisteredWindow_OnGUI_DoesNotHandleEventsItself()
    {
        // 등록된 창은 레지스트리만 이벤트를 처리한다 — 창의 OnGUI 가 또 처리하면 이중 처리
        var a = Make(1, 100, 100, 400, 300);
        var reg = new PanelRegistry();
        reg.Register(a);
        var e = Event.current;
        try
        {
            e.type = EventType.MouseDown; e.mousePosition = new Vector2(492, 392);
            a.OnGUI(_ => { });
            e.type = EventType.MouseDrag; e.mousePosition = new Vector2(700, 600);
            a.OnGUI(_ => { });
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
        }
        finally { Reset(); }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error CS" | sed 's/.*error //; s/\[E:.*//' | sort -u | head -3`
Expected: `CS1061: 'PanelRegistry' does not contain a definition for 'HandleEvents'` / `'IsCoveredByForeign'` / `'TopmostVisibleAt'`, `'PanelWindow' ... 'IsResizing'`.

- [ ] **Step 3: PanelWindow 수정**

`src/LongYinRoster/UI/PanelWindow.cs`:

(a) `public bool IsHydrated => _hydrated;` 줄 아래에:

```csharp
    /// <summary>등록된 레지스트리. 있으면 코너 리사이즈 이벤트는 레지스트리가 z-order 로 라우팅한다(OnGUI 가 직접 처리하지 않음).</summary>
    internal PanelRegistry? Registry { get; set; }
    internal bool IsResizing => _resizing;
```

(b) `OnGUI` 의 `HandleResizeEvents();   // GUI.Window 앞: ...` 줄을:

```csharp
        if (Registry == null) HandleResizeEvents();   // 미등록(단독) 창만 직접 처리 — 등록 창은 PanelRegistry.HandleEvents 가 z-order 로 라우팅(D2)
```

(c) `HandleResizeEvents` 메서드 전체(doc 포함)를 아래로 교체:

```csharp
    /// <summary>미등록 창의 코너 리사이즈 — 화면 좌표. 콜백 안(창 로컬)에서 처리하면 창이 마우스보다 느리게 커질 때
    /// 마우스가 창 밖으로 나가 MouseDrag 를 못 받는다(2026-09-29 smoke). 등록 창은 PanelRegistry.HandleEvents 가 같은 두 메서드를 호출한다.</summary>
    private void HandleResizeEvents()
    {
        var e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseDown) TryBeginResize(e);
        else HandleResizeContinuation(e);
    }

    /// <summary>MouseDown 이 코너 핸들(화면 좌표) 안이면 리사이즈 시작 + Use. 레지스트리는 "그 자리의 맨 앞 창" 에 대해서만 호출한다.</summary>
    internal bool TryBeginResize(Event e)
    {
        if (e.type != EventType.MouseDown) return false;
        if (!PanelWindowLogic.ResizeHandleRect(_rect).Contains(e.mousePosition)) return false;
        _resizing        = true;
        _resizeStart     = e.mousePosition;
        _resizeStartSize = new Vector2(_rect.width, _rect.height);
        e.Use();
        return true;
    }

    /// <summary>리사이즈 중이면 MouseDrag → 크기 갱신, MouseUp → 종료 + 저장. 이벤트를 소비했으면 true.</summary>
    internal bool HandleResizeContinuation(Event e)
    {
        if (!_resizing) return false;
        if (e.type == EventType.MouseDrag)
        {
            _rect = PanelWindowLogic.Resize(_rect, _resizeStart, _resizeStartSize, e.mousePosition,
                                            _bounds(), Screen.width, Screen.height);
            // e.Use() 필수: 리사이즈로 PageSize 등이 바뀌어 같은 패스의 GUILayout 컨트롤 수가 Layout 캐시와 달라져도,
            // 이벤트가 Used 면 GUILayoutUtility 가 더미 rect 를 돌려줘 "Getting control N position" ArgumentException 이 나지 않는다.
            e.Use();
            return true;
        }
        if (e.type == EventType.MouseUp)
        {
            _resizing = false;
            Persist();
            e.Use();
            return true;
        }
        return false;
    }
```

- [ ] **Step 4: PanelRegistry 전체 교체**

`src/LongYinRoster/UI/PanelRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// v0.8.0 — 이관된 패널 창의 Hydrate/Persist 를 묶고(1단계), 코너 리사이즈 이벤트를 z-order 로 라우팅한다(3단계, D2).
/// 리스트 순서 = z-order(끝 = 맨 앞). MouseDown 이 어떤 등록 창 안이면 그 창을 맨 앞으로 올린다 — Unity 도 같은 클릭으로
/// GUI.Window 를 앞으로 가져오므로 두 순서가 함께 움직인다. 핸들 판정은 그 맨 앞 창에만 시킨다.
/// 아직 이관 안 된 창(ItemDetail/PlayerEditor/본체)이 덮은 자리는 IsCoveredByForeign 으로 막는다 — 6단계에 전부 등록되면 제거.
/// </summary>
public sealed class PanelRegistry
{
    private readonly List<PanelWindow> _windows = new();

    /// <summary>등록 안 된 창이 이 화면 좌표를 덮고 있으면 true. ModWindow 가 세팅. null 이면 검사 안 함.</summary>
    public Func<Vector2, bool>? IsCoveredByForeign;

    public void Register(PanelWindow window)
    {
        _windows.Add(window);
        window.Registry = this;
    }

    public void HydrateAll(float screenW, float screenH)
    {
        foreach (var w in _windows) w.Hydrate(screenW, screenH);
    }

    public void ReloadRects(float screenW, float screenH)
    {
        foreach (var w in _windows) w.ReloadRect(screenW, screenH);
    }

    public void PersistAll()
    {
        foreach (var w in _windows) w.Persist();
    }

    /// <summary>ModWindow.OnGUI 맨 앞에서 매 패스 한 번. 패널들의 OnGUI(GUI.Window) 보다 먼저 실행돼야 핸들이 창 내부 컨트롤보다 먼저 이벤트를 잡는다.</summary>
    public void HandleEvents()
    {
        var e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseDown)
        {
            var top = TopmostVisibleAt(e.mousePosition);
            if (top == null) return;
            BringToFront(top);
            if (IsCoveredByForeign != null && IsCoveredByForeign(e.mousePosition)) return;
            top.TryBeginResize(e);
        }
        else if (e.type == EventType.MouseDrag || e.type == EventType.MouseUp)
        {
            foreach (var w in _windows)
                if (w.HandleResizeContinuation(e)) return;
        }
    }

    /// <summary>화면 좌표를 포함하는 보이는 창 중 맨 앞. 없으면 null.</summary>
    internal PanelWindow? TopmostVisibleAt(Vector2 pos)
    {
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var w = _windows[i];
            if (w.Visible && w.Rect.Contains(pos)) return w;
        }
        return null;
    }

    private void BringToFront(PanelWindow w)
    {
        _windows.Remove(w);
        _windows.Add(w);
    }
}
```

- [ ] **Step 5: 통과 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error|Passed!|Failed!"`
Expected: `Passed: 500`(495 + 5). 기존 `PanelWindowTests.CornerDrag_ContinuesOutsideWindow_AndPersistsOnMouseUp`(미등록 창)은 그대로 통과해야 한다.

- [ ] **Step 6: 커밋**

```bash
git add src/LongYinRoster/UI/PanelWindow.cs src/LongYinRoster/UI/PanelRegistry.cs src/LongYinRoster.Tests/PanelRegistryTests.cs
git -c core.safecrlf=false commit -m "feat(v0.8.0-s3): PanelRegistry 가 코너 리사이즈를 z-order 로 라우팅 — 클릭 = 맨 앞, 핸들 판정은 맨 앞 창만, 미이관 창 가드 (D2)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: `ContainerLayout` 계산기

**Files:**
- Create: `src/LongYinRoster/UI/Layout/ContainerLayout.cs`
- Test: `src/LongYinRoster.Tests/ContainerLayoutTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` (링크)

**Interfaces:**
- Consumes: `LayoutMath.SplitWidth/SplitHeight`, `DialogStyle.*`(`ChromeH` 포함), `PanelBounds`.
- Produces: `record struct ContainerLayoutState(bool HasSecondaryTabs, bool InvCollapsed, bool StoCollapsed, int SplitPreset, int ExtraRightRows)`; `record struct ContainerLayout(float LeftW, float RightW, float SearchFieldW, float InvListH, float StoListH, float ContainerListH)`; `ContainerLayout.Compute(Rect content, ContainerLayoutState s)`; `ContainerLayout.MinSize : PanelBounds`(758×588); consts `CategoryTabCount=8`, `CategoryTabW=70`, `SecondaryTabW=50`, `RightSelectRowW=365`, `MinColW=365`, `ToolbarFixedW=318`, `MinSearchFieldW=120`, `ToolbarRows=2`, `MinListRows=3`; statics `MinListH`(80), `SectionExpandedOverhead`(96), `SectionCollapsedOverhead`(32), `PresetRowH`(32), `RightFixedH`(156).

- [ ] **Step 1: 테스트 작성**

```csharp
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S3 — 컨테이너 패널 계산기. 좌/우 열 폭 SplitWidth, 인벤/창고 SplitHeight(접힘·프리셋), 컨테이너 리스트 = 우측 잔여.</summary>
public class ContainerLayoutTests
{
    // 기본 창 800×760 → 내용 776×676 (ChromeH 84)
    private static readonly Rect Default = new(12, 40, 776, 676);

    private static ContainerLayoutState S(bool secondary = false, bool inv = false, bool sto = false, int preset = 0, int extra = 0)
        => new(HasSecondaryTabs: secondary, InvCollapsed: inv, StoCollapsed: sto, SplitPreset: preset, ExtraRightRows: extra);

    [Fact]
    public void Compute_Default_ColumnsShareWidthAfterMins()
    {
        // 776 - 2×365 - gap 4 = 42 → 21 씩 → 386 / 386
        var L = ContainerLayout.Compute(Default, S());
        (L.LeftW, L.RightW).ShouldBe((386f, 386f));
    }

    [Fact]
    public void Compute_Default_5050_SplitsLeftRemainingEvenly()
    {
        // 열 높이 676 - 탭 32 - 툴바 56 = 588; 프리셋 행 32 → 556; 펼침 오버헤드 96×2 → 364 → 182 / 182
        var L = ContainerLayout.Compute(Default, S());
        (L.InvListH, L.StoListH).ShouldBe((182f, 182f));
    }

    [Fact]
    public void Compute_Default_ContainerListTakesRightRemaining()
    {
        // 588 - 우측 고정(선택 행 32 + 라벨 28 + 버튼 3행 96 = 156) = 432
        var L = ContainerLayout.Compute(Default, S());
        L.ContainerListH.ShouldBe(432f);
    }

    [Fact]
    public void Compute_Preset7030_WeightsLeftSplit()
    {
        var L = ContainerLayout.Compute(Default, S(preset: 1));
        L.InvListH.ShouldBe(254.8f, 0.01);
        L.StoListH.ShouldBe(109.2f, 0.01);
    }

    [Fact]
    public void Compute_Preset3_StorageGetsExactlyMinRows_InventoryGetsRest()
    {
        // 확장:최소 — 창고 3행(80) 고정, 인벤 = 556 - 96 - (96 + 80) = 284
        var L = ContainerLayout.Compute(Default, S(preset: 3));
        (L.InvListH, L.StoListH).ShouldBe((284f, ContainerLayout.MinListH));
    }

    [Fact]
    public void Compute_InvCollapsed_StorageTakesAll()
    {
        // 접힌 인벤은 헤더(32)만 → 556 - 32 - 96 = 428
        var L = ContainerLayout.Compute(Default, S(inv: true));
        (L.InvListH, L.StoListH).ShouldBe((0f, 428f));
    }

    [Fact]
    public void Compute_StoCollapsed_InventoryTakesAll()
    {
        var L = ContainerLayout.Compute(Default, S(sto: true));
        (L.InvListH, L.StoListH).ShouldBe((428f, 0f));
    }

    [Fact]
    public void Compute_Preset3070_WeightsLeftSplit()
    {
        var L = ContainerLayout.Compute(Default, S(preset: 2));
        L.InvListH.ShouldBe(109.2f, 0.01);
        L.StoListH.ShouldBe(254.8f, 0.01);
    }

    [Fact]
    public void Compute_BothCollapsed_ZeroLists()
    {
        var L = ContainerLayout.Compute(Default, S(inv: true, sto: true));
        (L.InvListH, L.StoListH).ShouldBe((0f, 0f));
    }

    [Fact]
    public void Compute_SecondaryTabs_ShrinkEveryList()
    {
        // 2차 탭 한 줄(32) 만큼 세 리스트가 줄어든다: 좌 166/166, 우 400
        var L = ContainerLayout.Compute(Default, S(secondary: true));
        (L.InvListH, L.StoListH, L.ContainerListH).ShouldBe((166f, 166f, 400f));
    }

    [Fact]
    public void Compute_ExtraRightRows_ShrinkContainerList()
    {
        // 드롭다운 3항목 → 우측 리스트 432 - 3×32 = 336
        var L = ContainerLayout.Compute(Default, S(extra: 3));
        L.ContainerListH.ShouldBe(336f);
        // 항목이 아주 많아도 3행 밑으로는 안 내려간다
        ContainerLayout.Compute(Default, S(extra: 40)).ContainerListH.ShouldBe(ContainerLayout.MinListH);
    }

    [Fact]
    public void Compute_Narrow_ColumnsFloorAtMinColW()
    {
        var L = ContainerLayout.Compute(new Rect(12, 40, 600, 676), S());
        (L.LeftW, L.RightW).ShouldBe((ContainerLayout.MinColW, ContainerLayout.MinColW));
    }

    [Fact]
    public void Compute_Tiny_ListsFloorAtThreeRows()
    {
        var L = ContainerLayout.Compute(new Rect(12, 40, 300, 200), S());
        (L.InvListH, L.StoListH, L.ContainerListH).ShouldBe((ContainerLayout.MinListH, ContainerLayout.MinListH, ContainerLayout.MinListH));
        L.SearchFieldW.ShouldBe(ContainerLayout.MinSearchFieldW);
    }

    [Fact]
    public void Compute_SearchField_FollowsWidth()
    {
        ContainerLayout.Compute(Default, S()).SearchFieldW.ShouldBe(776f - ContainerLayout.ToolbarFixedW);   // 458
    }

    [Fact]
    public void Compute_Taller_MonotonicLists()
    {
        var a = ContainerLayout.Compute(Default, S());
        var b = ContainerLayout.Compute(new Rect(12, 40, 776, 900), S());
        b.InvListH.ShouldBeGreaterThan(a.InvListH);
        b.ContainerListH.ShouldBeGreaterThan(a.ContainerListH);
    }

    [Fact]
    public void MinSize_FitsFixedPartsPlusThreeRowsPerList()
    {
        var m = ContainerLayout.MinSize;
        m.MinW.ShouldBe(758f);   // max(탭 8×70 + 8×4 = 592, 두 열 2×365 + 4 = 734) + 24
        m.MinH.ShouldBe(588f);   // 84 + 탭 2줄 64 + 툴바 56 + 프리셋 32 + 펼침 오버헤드 192 + 리스트 2×80
        var L = ContainerLayout.Compute(new Rect(12, 40, m.MinW - 2 * DialogStyle.Padding, m.MinH - DialogStyle.ChromeH), S(secondary: true));
        (L.InvListH, L.StoListH).ShouldBe((ContainerLayout.MinListH, ContainerLayout.MinListH));   // 정확히 3행
        L.ContainerListH.ShouldBeGreaterThanOrEqualTo(ContainerLayout.MinListH);
        (L.LeftW, L.RightW).ShouldBe((ContainerLayout.MinColW, ContainerLayout.MinColW));
    }
}
```

- [ ] **Step 2: csproj 링크** — `LongYinRoster.Tests.csproj` 의 `<!-- v0.8.0 S2 -->` 블록(`ItemGenLayout.cs` 링크) 바로 아래:

```xml
    <!-- v0.8.0 S3 -->
    <Compile Include="../LongYinRoster/UI/Layout/ContainerLayout.cs">
      <Link>UI/Layout/ContainerLayout.cs</Link>
    </Compile>
```

- [ ] **Step 3: 실패 확인** — `dotnet test ... | grep "error CS"` → `CS2001 ... ContainerLayout.cs could not be found`.

- [ ] **Step 4: 구현**

`src/LongYinRoster/UI/Layout/ContainerLayout.cs`:

```csharp
using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>ContainerPanel 계산기 입력 상태. ExtraRightRows = 우측 열의 가변 행(신규/이름변경 입력 행 + 열린 드롭다운 항목 수).</summary>
public readonly record struct ContainerLayoutState(bool HasSecondaryTabs, bool InvCollapsed, bool StoCollapsed, int SplitPreset, int ExtraRightRows);

/// <summary>
/// v0.8.0 S3 — ContainerPanel 계산기. 고정: 카테고리 탭(8×70)·2차 탭(50)·툴바 2행·섹션 헤더/일괄선택/이동복사 행(28)·프리셋 행·우측 선택/버튼 행.
/// 가로: 좌/우 열 = SplitWidth(1:1, 최소 365 씩), 검색창 = 잔여. 세로: 인벤/창고 = SplitHeight(접힘·프리셋), 컨테이너 리스트 = 우측 잔여. 리스트 최소 3행.
/// content.height 는 PanelWindowLogic.ContentRect 가 ImguiSlack 을 이미 뺀 값(D1) — 여기서 다시 빼지 않는다.
/// </summary>
public readonly record struct ContainerLayout(
    float LeftW, float RightW, float SearchFieldW,
    float InvListH, float StoListH, float ContainerListH)
{
    public const int   CategoryTabCount = 8;     // 전체/장비/단약/음식/비급/보물/재료/말
    public const float CategoryTabW     = 70f;
    public const float SecondaryTabW    = 50f;
    // 우측 선택 행: [이름 ▼ 150][신규 45][이름변경 60][복사 45][삭제 45] + 컨트롤 margin 5×Gap = 365 — 두 열의 최소 폭
    public const float RightSelectRowW  = 150f + 45f + 60f + 45f + 45f + 5f * DialogStyle.Gap;
    public const float MinColW          = RightSelectRowW;
    // 툴바 1행의 고정부: Space 4 + 정렬 버튼 70+60+60+60 + Space 4 + 방향 32 + margin 7×Gap = 318 → 검색창 = 폭 − 318 (최소 120)
    public const float ToolbarFixedW    = 4f + 70f + 60f + 60f + 60f + 4f + 32f + 7f * DialogStyle.Gap;
    public const float MinSearchFieldW  = 120f;
    public const int   ToolbarRows      = 2;     // [검색/정렬] + [상세/착용중 제외/Undo/카운터]
    public const int   MinListRows      = 3;

    public static float MinListH                 => MinListRows * DialogStyle.RowHeight + (MinListRows - 1) * DialogStyle.Gap;   // 80
    public static float SectionExpandedOverhead  => 3f * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);   // 헤더 + 일괄선택 + 이동/복사 = 96
    public static float SectionCollapsedOverhead => DialogStyle.ButtonRowHeight + DialogStyle.Gap;          // 헤더만 = 32
    public static float PresetRowH               => DialogStyle.ButtonRowHeight + DialogStyle.Gap;          // 32
    public static float ToolbarH                 => ToolbarRows * (DialogStyle.RowHeight + DialogStyle.Gap); // 56
    // 우측 고정: 선택 행 32 + 라벨 28 + 이동/복사 2행 + 삭제 행 = 156
    public static float RightFixedH              => (DialogStyle.ButtonRowHeight + DialogStyle.Gap) + (DialogStyle.RowHeight + DialogStyle.Gap) + 3f * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);

    public static float TabsH(bool hasSecondaryTabs) => (hasSecondaryTabs ? 2f : 1f) * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);

    public static PanelBounds MinSize => new(
        MinW: Math.Max(CategoryTabCount * CategoryTabW + CategoryTabCount * DialogStyle.Gap,   // 탭 한 줄 592
                       2f * MinColW + DialogStyle.Gap)                                          // 두 열 734
              + 2f * DialogStyle.Padding,                                                        // 758
        MinH: DialogStyle.ChromeH + TabsH(true) + ToolbarH + PresetRowH
              + 2f * SectionExpandedOverhead + 2f * MinListH);                                   // 588

    public static ContainerLayout Compute(Rect content, ContainerLayoutState s)
    {
        float colsH = Math.Max(0f, content.height - TabsH(s.HasSecondaryTabs) - ToolbarH);

        var cols = LayoutMath.SplitWidth(content.width, new[] { 1f, 1f }, DialogStyle.Gap, new[] { MinColW, MinColW });

        // 좌측: 프리셋 행을 뺀 나머지를 인벤/창고에 분배. 접힌 섹션은 헤더만 차지하고 0.
        float leftAvail = colsH - PresetRowH;
        float invOver = s.InvCollapsed ? SectionCollapsedOverhead : SectionExpandedOverhead;
        float stoOver = s.StoCollapsed ? SectionCollapsedOverhead : SectionExpandedOverhead;
        float[] weights;
        if (s.InvCollapsed || s.StoCollapsed) weights = new[] { 1f, 1f };            // 펼친 쪽만 가중치가 살아남는다
        else weights = s.SplitPreset switch
        {
            1 => new[] { 7f, 3f },
            2 => new[] { 3f, 7f },
            3 => new[] { 1f, 0f },   // 확장:최소 — 창고는 아래서 3행 고정
            _ => new[] { 1f, 1f },
        };
        bool stoFixedMin = s.SplitPreset == 3 && !s.InvCollapsed && !s.StoCollapsed;
        if (stoFixedMin) stoOver += MinListH;
        var split = LayoutMath.SplitHeight(leftAvail, new[] { invOver, stoOver }, weights,
                                           new[] { s.InvCollapsed, s.StoCollapsed }, 0f);
        float invListH = s.InvCollapsed ? 0f : Math.Max(MinListH, split[0]);
        float stoListH = s.StoCollapsed ? 0f : Math.Max(MinListH, split[1] + (stoFixedMin ? MinListH : 0f));

        // 우측: 선택 행 + 가변 행(신규/이름변경/드롭다운) + 라벨 + 버튼 3행을 빼고 전부 컨테이너 리스트
        float rightFixed = RightFixedH + s.ExtraRightRows * (DialogStyle.ButtonRowHeight + DialogStyle.Gap);
        float conListH   = Math.Max(MinListH, colsH - rightFixed);

        float searchW = Math.Max(MinSearchFieldW, content.width - ToolbarFixedW);
        return new ContainerLayout(cols[0], cols[1], searchW, invListH, stoListH, conListH);
    }
}
```

- [ ] **Step 5: 통과 확인** — `--filter "FullyQualifiedName~ContainerLayoutTests"` → `Passed: 16`. 전체 `dotnet test --nologo -v quiet` → `Passed: 516`(500 + 16).

- [ ] **Step 6: 커밋**

```bash
git add src/LongYinRoster/UI/Layout/ContainerLayout.cs src/LongYinRoster.Tests/ContainerLayoutTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git -c core.safecrlf=false commit -m "feat(v0.8.0-s3): ContainerLayout 계산기 — 열 폭 SplitWidth·인벤/창고 SplitHeight(접힘·프리셋)·컨테이너 리스트 잔여

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: ContainerPanel 이관 + 툴바 폭 + 설정 하한 + ModWindow 연결 + smoke + PR

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs` (아래 블록 단위 교체 (a)~(n))
- Modify: `src/LongYinRoster/UI/SearchSortToolbar.cs` (`Draw` 서명·높이)
- Modify: `src/LongYinRoster/UI/SettingsPanel.cs` (컨테이너 W/H 파싱 하한 2줄)
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (OnSaved / 등록 / IsCoveredByForeign / OnGUI HandleEvents / per-frame 저장 블록 삭제)
- Test: `src/LongYinRoster.Tests/ContainerPanelFocusTests.cs`, `src/LongYinRoster.Tests/SettingsPanelTests.cs`
- Modify: `docs/HANDOFF.md`

**Interfaces:**
- Consumes: `PanelWindow`, `RectBinding.Of(x,y,w,h)`(Open 없음 — 컨테이너 표시는 모드 전환이 결정), `PanelRegistry.Register/HandleEvents/ReloadRects/PersistAll/IsCoveredByForeign`, `ContainerLayout.Compute/MinSize`, `ContainerLayoutState`.
- Produces: `ContainerPanel.Window : PanelWindow`, `Visible`/`WindowRect` 위임, `SearchSortToolbar.Draw(SearchSortState, bool, float searchFieldW = 200f)`. 삭제: `ContainerPanel.SetRect`, `DrawResizeHandle`, `DrawToast`, `MIN_W/MAX_W/MIN_H/MAX_H`, `_rect`.

- [ ] **Step 1: 테스트 추가**

`src/LongYinRoster.Tests/ContainerPanelFocusTests.cs` — 상단 `using` 에 `using LongYinRoster.UI.Layout;` 추가, 클래스 끝에:

```csharp
    [Fact]
    public void Window_StartsHiddenWithContainerMinSize()
    {
        // 구 cfg 의 600×400(구 MIN_W/H) 은 Hydrate 가 758×588 로 올린다(PanelWindow.Hydrate_RaisesBelowMinToMin) — 여기선 생성 직후 최소 크기 확인
        var panel = new ContainerPanel();
        panel.Visible.ShouldBeFalse();
        (panel.Window.Rect.width, panel.Window.Rect.height).ShouldBe((ContainerLayout.MinSize.MinW, ContainerLayout.MinSize.MinH));
    }
```

`src/LongYinRoster.Tests/SettingsPanelTests.cs` — 클래스 끝에:

```csharp
    [Fact]
    public void ContainerRect_TextParse_BelowLayoutMin_Ignored()
    {
        // 설정 패널의 컨테이너 W/H 필드 하한은 ContainerLayout.MinSize (구 하한 100) — 700 은 무시, 900 은 통과
        SettingsPanel.TryParseRectField("700", ContainerLayout.MinSize.MinW, out _).ShouldBeFalse();
        SettingsPanel.TryParseRectField("900", ContainerLayout.MinSize.MinW, out var v).ShouldBeTrue();
        v.ShouldBe(900f);
    }
```

- [ ] **Step 2: 실패 확인** — `dotnet test ... | grep "error CS"` → `CS1061: 'ContainerPanel' does not contain a definition for 'Window'`.

- [ ] **Step 3: ContainerPanel 블록 교체**

`src/LongYinRoster/UI/ContainerPanel.cs` — 각 항목은 "기존 블록 → 새 블록" 이며 그 외 코드는 손대지 않는다.

**(a) using** — `using LongYinRoster.Containers;` 아래에 `using LongYinRoster.UI.Layout;`.

**(b) 창 상태 필드** — 기존:
```csharp
    public bool Visible { get; set; } = false;
    public Rect WindowRect => _rect;
    private Rect _rect = new Rect(150, 100, 800, 760);
    private const int WindowID = 0x4C593732;  // "LY72"
```
→
```csharp
    private const int WindowID = 0x4C593732;  // "LY72"

    /// <summary>v0.8.0 S3 — 창 틀. 배경/헤더/X/드래그/코너 리사이즈/화면 클램프/rect↔Config 는 전부 PanelWindow.</summary>
    public PanelWindow Window { get; }
    public bool Visible { get => Window.Visible; set => Window.Visible = value; }
    public Rect WindowRect => Window.Rect;

    public ContainerPanel()
    {
        Window = new PanelWindow(WindowID, "컨테이너 관리", () => ContainerLayout.MinSize,
            RectBinding.Of(Config.ContainerPanelX, Config.ContainerPanelY, Config.ContainerPanelW, Config.ContainerPanelH));
    }
```

**(c) 리사이즈 필드 삭제** — 아래 블록 통째로 삭제:
```csharp
    // v0.7.11 Cat 9A/9D — corner resize handle (사용자 panel 안에서 width/height drag-resize)
    private bool    _resizing;
    private Vector2 _resizeStart;
    private Vector2 _resizeStartSize;
    private const float MIN_W = 600f;
    private const float MAX_W = 1600f;
    private const float MIN_H = 400f;
    private const float MAX_H = 1080f;

```

**(d) HydrateFromConfig / SetRect** — doc 의 `(sort/filter/lastIndex/rect)` → `(sort/filter/lastIndex)`. 기존:
```csharp
        // 삭제된 컨테이너 가리킴 → RefreshContainerList 의 default (첫 컨테이너) 유지

        _rect = new Rect(Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
                         Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
    }

    /// <summary>v0.7.6 — SettingsPanel.OnSaved 에서 호출. ContainerPanel rect 갱신.</summary>
    public void SetRect(float x, float y, float w, float h)
    {
        _rect = new Rect(x, y, w, h);
    }
```
→
```csharp
        // 삭제된 컨테이너 가리킴 → RefreshContainerList 의 default (첫 컨테이너) 유지
        // rect 는 PanelWindow(PanelRegistry.HydrateAll) 가 읽는다 — v0.8.0 S3
    }
```

**(e) OnGUI 의 GUI.Window 호출 + Draw 메서드** — 기존 `OnGUI` 안의
```csharp
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
```
부터 `private void Draw(int id)` 메서드 끝(그 `catch` 블록의 닫는 중괄호와 메서드 닫는 중괄호)까지를 아래로 교체:
```csharp
        Window.OnGUI(DrawContent);   // 예외는 PanelWindow 가 WarnOnce 로 잡는다
    }

    /// <summary>창 로컬 내용 영역. 창 틀(배경/헤더/X/드래그/코너)은 PanelWindow 가 그린다.</summary>
    private void DrawContent(Rect content)
    {
        // v0.7.2 D-3 — grade/quality reflection 미발견 1회 토스트
        if (!_gradeQualityEnabled && !_gradeQualityToastShown)
        {
            ToastService.Push(KoreanStrings.Tip_GradeQualityUnavailable, ToastKind.Info);
            _gradeQualityToastShown = true;
        }

        var subTabs = CategorySecondaryTabs.ForCategory(_filter);
        int extraRightRows = (_newMode ? 1 : 0) + (_renameMode ? 1 : 0) + (_dropdownOpen ? _containerList.Count : 0);
        var L = ContainerLayout.Compute(content, new ContainerLayoutState(
            HasSecondaryTabs: subTabs.Count > 0,
            InvCollapsed:     Config.ContainerInventoryCollapsed.Value,
            StoCollapsed:     Config.ContainerStorageCollapsed.Value,
            SplitPreset:      Config.ContainerSplitPreset.Value,
            ExtraRightRows:   extraRightRows));

        DrawCategoryTabs(subTabs);
        DrawGlobalToolbar(L);   // v0.7.2 D-3 — global toolbar (인벤/창고/컨테이너 통합)
        GUILayout.BeginHorizontal();
        DrawLeftColumn(L);
        GUILayout.Space(DialogStyle.Gap);
        DrawRightColumn(L);
        GUILayout.EndHorizontal();
        // v0.7.11 Cat 5A — 삭제 confirm dialog (modal overlay, 창 로컬)
        _confirmDialog.Draw();
    }
```
(`OnGUI` 첫 줄 `if (!Visible) return;` 과 lazy initial load 블록은 그대로 둔다.)

**(f) DrawCategoryTabs** — 서명을 `private void DrawCategoryTabs(IReadOnlyList<(string Label, int Value)> subTabs)` 로, 탭 버튼을 `GUILayout.Button(ItemCategoryFilter.KoreanLabel(cat), GUILayout.Width(ContainerLayout.CategoryTabW), GUILayout.Height(DialogStyle.ButtonRowHeight))` 로, 메서드 끝의
```csharp
        // 카테고리별 secondary tab — 장비/음식/비급/재료 만 표시 (단약/보물/말/기타는 정의 없음).
        var subTabs = CategorySecondaryTabs.ForCategory(_filter);
        if (subTabs.Count > 0)
        {
            DrawSecondaryTabs(subTabs);
        }
```
→
```csharp
        // 카테고리별 secondary tab — 장비/음식/비급/재료 만 표시 (단약/보물/말/기타는 정의 없음). 계산기도 같은 subTabs 로 행 수를 셌다.
        if (subTabs.Count > 0) DrawSecondaryTabs(subTabs);
```

**(g) DrawSecondaryTabs** — 두 곳의 `GUILayout.Button(..., GUILayout.Width(50))` → `GUILayout.Button(..., GUILayout.Width(ContainerLayout.SecondaryTabW), GUILayout.Height(DialogStyle.ButtonRowHeight))`.

**(h) DrawLeftColumn** — 서명 `private void DrawLeftColumn(ContainerLayout L)`. 기존 첫 줄부터 `else { float available = ...; (invContentH, stoContentH) = preset switch { ... }; }` 블록 끝까지(즉 `// ─── 인벤토리 ───` 주석 직전까지)를:
```csharp
        GUILayout.BeginVertical(GUILayout.Width(L.LeftW));

        // v0.7.11 Cat 1A/1B — collapse 토글 + split preset. 높이 분배는 ContainerLayout(SplitHeight) 이 계산 — v0.8.0 S3
        bool invCollapsed = Config.ContainerInventoryCollapsed.Value;
        bool stoCollapsed = Config.ContainerStorageCollapsed.Value;
        int  preset       = Config.ContainerSplitPreset.Value;

```
로 교체. 이어서: `if (!invCollapsed && invContentH > 10f)` → `if (!invCollapsed && L.InvListH > 0f)`; `DrawItemList(ContainerArea.Inventory, invView, _inventoryChecks, ref _invScroll, invContentH);` → `..., L.InvListH);`; 인벤 블록 뒤의 `GUILayout.Space(4);` 삭제; `if (!stoCollapsed && stoContentH > 10f)` → `L.StoListH > 0f`; `..., ref _stoScroll, stoContentH);` → `L.StoListH);`; `// Split preset cycle button` 아래의 `GUILayout.Space(4);` 삭제; 프리셋 버튼 → `GUILayout.Button($"비율 {presetLabel} ▼", GUILayout.Width(120), GUILayout.Height(DialogStyle.ButtonRowHeight))`.

**(i) DrawSectionHeader** — `GUILayout.Button(collapsed ? "▶" : "▼", GUILayout.Width(24))` → `..., GUILayout.Width(24), GUILayout.Height(DialogStyle.ButtonRowHeight))`; 두 `GUILayout.Label(...)` 에 `, GUILayout.Height(DialogStyle.ButtonRowHeight)` 추가.

**(j) DrawSelectionBulkRow** — 네 버튼(`☑ 전체`, `☐ 해제`, `↺ 반전`, `[{rareLabel}]`)에 `, GUILayout.Height(DialogStyle.ButtonRowHeight)` 추가.

**(k) DrawMoveCopyRow** — `if (GUILayout.Button(moveLabel))` → `if (GUILayout.Button(moveLabel, GUILayout.Height(DialogStyle.ButtonRowHeight)))`, `copyLabel` 도 동일.

**(l) DrawRightColumn** — 서명 `private void DrawRightColumn(ContainerLayout L)`; `GUILayout.BeginVertical(GUILayout.Width(390));` → `GUILayout.BeginVertical(GUILayout.Width(L.RightW));`; 선택 행의 다섯 버튼(`[{sel} ▼]`, `신규`, `이름변경`, `복사`, `삭제`)과 드롭다운 항목 버튼(`GUILayout.Button(dropdownLabel)` → `GUILayout.Button(dropdownLabel, GUILayout.Height(DialogStyle.ButtonRowHeight))`), 신규/이름변경 행의 `GUILayout.Label("이름:")`·`GUILayout.Label("새 이름:")`·두 `TextField(…, GUILayout.Width(180))`·`확인`·`취소` 버튼에 전부 `, GUILayout.Height(DialogStyle.ButtonRowHeight)` 추가; `GUILayout.Label($"{KoreanStrings.Lbl_Container} ({_containerRows.Count}개)");` → `..., GUILayout.Height(DialogStyle.RowHeight));`; `DrawItemList(ContainerArea.Container, conView, _containerChecks, ref _conScroll, 500);` → `..., L.ContainerListH);`; `if (GUILayout.Button("☓ 삭제"))` → `if (GUILayout.Button("☓ 삭제", GUILayout.Height(DialogStyle.ButtonRowHeight)))`.

**(m) DrawGlobalToolbar** — 서명 `private void DrawGlobalToolbar(ContainerLayout L)`; `SearchSortToolbar.Draw(_globalState, _gradeQualityEnabled)` → `SearchSortToolbar.Draw(_globalState, _gradeQualityEnabled, L.SearchFieldW)`; 둘째 행의 `GUILayout.Button("ⓘ 상세", GUILayout.Width(60))`·`GUILayout.Toggle(exclude, "착용중 제외", GUILayout.Width(100))`·`GUILayout.Button("↶ Undo", GUILayout.Width(80))`·두 `GUILayout.Label(..., GUILayout.Width(80|140))` 에 `, GUILayout.Height(DialogStyle.RowHeight)` 추가. (`GUILayout.Space(8)` 세 개는 가로 간격이라 유지.)

**(n) 삭제** — `DrawResizeHandle()` 메서드(doc 포함)와 `DrawToast()` 메서드(doc 포함) 통째로 삭제. `Draw` 에서 호출하던 `DrawToast();` 는 (e) 에서 이미 사라졌다.

- [ ] **Step 4: SearchSortToolbar / SettingsPanel**

`src/LongYinRoster/UI/SearchSortToolbar.cs`:
```csharp
    public static SearchSortState Draw(SearchSortState current, bool gradeQualityEnabled = true, float searchFieldW = 200f)
```
`GUILayout.TextField(current.Search ?? "", GUILayout.Width(200))` → `GUILayout.TextField(current.Search ?? "", GUILayout.Width(searchFieldW), GUILayout.Height(DialogStyle.RowHeight))`; 방향 버튼 `GUILayout.Button(arrow, GUILayout.Width(32))` → `..., GUILayout.Width(32), GUILayout.Height(DialogStyle.RowHeight))`; `DrawKeyButton` 의 `GUILayout.Button(label, GUILayout.Width(width))` → `..., GUILayout.Width(width), GUILayout.Height(DialogStyle.RowHeight))`. 클래스 doc 의 `[TextField (~140)]` → `[TextField (폭 = 호출자, 기본 200)]`.

`src/LongYinRoster/UI/SettingsPanel.cs` — `DrawContent` 의 컨테이너 rect 파싱 두 줄:
```csharp
        if (TryParseRectField(_wBuf, ContainerLayout.MinSize.MinW, out var cw)) BufferContainerW = cw;
        if (TryParseRectField(_hBuf, ContainerLayout.MinSize.MinH, out var chh)) BufferContainerH = chh;
```

- [ ] **Step 5: ModWindow 연결**

`src/LongYinRoster/UI/ModWindow.cs`:

(a) `_settingsPanel.OnSaved` 람다 안의
```csharp
            _containerPanel.SetRect(
                Config.ContainerPanelX.Value, Config.ContainerPanelY.Value,
                Config.ContainerPanelW.Value, Config.ContainerPanelH.Value);
```
→
```csharp
            _registry.ReloadRects(Screen.width, Screen.height);   // 설정 패널이 Config 에 쓴 rect(컨테이너 등)를 창이 즉시 따라감 — v0.8.0 S3
            _registry.PersistAll();                                 // 클램프된 실제 값을 Config 에 되씀
```

(b) `        _registry.Register(_settingsPanel.Window);` 줄 **앞**에:
```csharp
        // v0.8.0 S3 — ContainerPanel 창 틀 등록. 그리는 순서(Container → Settings → ItemGen)대로 등록 = 초기 z-order
        _registry.Register(_containerPanel.Window);
```

(c) `        _registry.HydrateAll(Screen.width, Screen.height);   // 등록 순서: ...` 줄의 주석을 `// 등록 순서: Container → Settings → ItemGen → HydrateAll` 로 바꾸고, 그 줄 **아래**에:
```csharp
        // v0.8.0 S3 (D2) — 아직 이관 안 된 창(ItemDetail/그 Selector/PlayerEditor/본체/모드 메뉴)이 덮은 자리에서는
        // 등록 창의 코너 리사이즈를 시작하지 않는다. 6단계에 전부 등록되면 삭제.
        _registry.IsCoveredByForeign = pos =>
            (_itemDetailPanel.Visible && _itemDetailPanel.WindowRect.Contains(pos))
            || (_itemDetailPanel.Selector.Visible && _itemDetailPanel.Selector.WindowRect.Contains(pos))
            || (_playerEditorPanel.Visible && _playerEditorPanel.WindowRect.Contains(pos))
            || (_visible && _rect.Contains(pos))
            || (_modeSelector.MenuVisible && _modeSelector.WindowRect.Contains(pos));
```

(d) `OnGUI` 의 `ToastService.Draw();` 바로 아래에:
```csharp
        // v0.8.0 S3 (D2) — 등록 창의 코너 리사이즈를 z-order 로 라우팅. 패널 OnGUI(GUI.Window) 보다 먼저.
        _registry.HandleEvents();
```

(e) `OnGUI` 의 아래 5줄 삭제:
```csharp
        // v0.7.6 — ContainerPanel rect 영속화 (ItemDetailPanel mirror)
        Config.ContainerPanelX.Value = _containerPanel.WindowRect.x;
        Config.ContainerPanelY.Value = _containerPanel.WindowRect.y;
        Config.ContainerPanelW.Value = _containerPanel.WindowRect.width;
        Config.ContainerPanelH.Value = _containerPanel.WindowRect.height;
```

- [ ] **Step 6: 테스트·빌드**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error|warning CS|Passed!|Failed!"` → `Passed: 518`(516 + 2), 경고 0.
Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Debug --nologo -v minimal 2>&1 | grep -E "warn|error|Build succeeded"` → `Build succeeded`, 경고 0.
그다음 `grep -n '_rect\b\|MIN_W\|MAX_H\|TOTAL_H\|DrawResizeHandle\|DrawToast\|SetRect' src/LongYinRoster/UI/ContainerPanel.cs` 가 **아무것도 출력하지 않아야** 한다.

- [ ] **Step 7: 커밋**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs src/LongYinRoster/UI/SearchSortToolbar.cs src/LongYinRoster/UI/SettingsPanel.cs src/LongYinRoster/UI/ModWindow.cs src/LongYinRoster.Tests/ContainerPanelFocusTests.cs src/LongYinRoster.Tests/SettingsPanelTests.cs
git -c core.safecrlf=false commit -m "feat(v0.8.0-s3): ContainerPanel 을 PanelWindow 로 이관 — ContainerLayout(열 폭·인벤/창고/컨테이너 리스트 자동), 자체 리사이즈·TOTAL_H 삭제, 검색창 확장

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

- [ ] **Step 8: 인게임 smoke**

게임 종료 확인 후 `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release --nologo -v minimal` → 배포. `md5sum src/LongYinRoster/bin/Release/LongYinRoster.dll "E:/Games/龙胤立志传.v1.1.0f5/game/BepInEx/plugins/LongYinRoster/LongYinRoster.dll"` 해시 일치 확인.

게임 실행 → 세이브 로드 → F11 → 2 (컨테이너 관리):
1. 기본 800×760 에서 인벤/창고/컨테이너 리스트 3개가 보이고, 하단 "비율" 버튼과 우측 "☓ 삭제" 버튼이 잘리지 않음. 가로 스크롤바 없음(있으면 `RightSelectRowW`/`ToolbarFixedW` 의 margin 계수를 올림).
2. 코너로 1400×1000 까지 키우기 → 세 리스트가 늘어남. 최소(758×588)까지 줄이기 → 각 리스트 3행, 잘림 없음, 창이 그 밑으로 안 줄어듦.
3. 프리셋 4종 순환(50:50 / 70:30 / 30:70 / 확장:최소)과 인벤 접기·창고 접기·둘 다 접기 → 즉시 반영, 예외 없음.
4. 컨테이너 드롭다운 열기·신규·이름변경 행 → 우측 리스트가 줄었다가 닫으면 복원.
5. 검색어 입력 → 검색창이 창 폭을 따라 넓어짐. 정렬 4종·등급 ≥ 필터·착용중 제외·결과 카운터 회귀 없음.
6. 인벤→컨테이너 이동 1건 → Undo → 복귀. 복사·삭제 confirm 회귀 없음.
7. ⓘ 상세 패널을 켜고 컨테이너 창 우하단 위로 옮겨 덮은 뒤 그 자리를 클릭 → 컨테이너가 리사이즈되지 않고 상세 패널이 반응.
8. 설정 패널(F11+3)에서 컨테이너 W 에 `700` → 저장 시 무시(758 미만), `900` → 저장 즉시 컨테이너 창(F11+2)이 900 폭.
9. X 로 닫고 F11+2 재진입 → 크기·위치 유지. 게임 재시작 후에도 유지(`ContainerPanelW/H` cfg).

로그: `grep -a -i 'PanelWindow\|ContainerPanel\|ModWindow' .../BepInEx/LogOutput.log | grep -a -i 'warn\|error'` 에 새 항목 없음.

- [ ] **Step 9: HANDOFF + PR**

`docs/HANDOFF.md` 4행(진행 상태)의 `**v0.8.0 1·2단계 완료(2026-09-29)**: ... 3단계 ContainerPanel 플랜 작성 예정.` 를 `... 3단계 ContainerPanel 완료(PR #<gh pr create 가 출력한 번호>). 4단계 ItemDetailPanel 플랜 작성 예정.` 로, Releases 목록 맨 앞(`- **v0.8.0-s1/s2**` 앞)에:

```markdown
- **v0.8.0-s3** (커밋 당일 날짜) — **ContainerPanel 반응형** (PR #<번호> → develop). D1: `DialogStyle.ChromeH=84`, `PanelWindowLogic.ContentRect(…, bottomSlack)` 가 IMGUI 암묵 여백을 흡수 — 계산기는 슬랙을 다시 빼지 않음. D2: `PanelRegistry.HandleEvents` 가 코너 리사이즈를 z-order 로 라우팅(클릭 = 맨 앞, 핸들 판정은 맨 앞 창만), `IsCoveredByForeign` 로 미이관 창이 덮은 자리는 시작 금지(6단계에 제거). `ContainerLayout`: 좌/우 열 `SplitWidth`(최소 365 씩), 인벤/창고 `SplitHeight`(접힘·프리셋 4종, 확장:최소는 창고 3행 고정), 컨테이너 리스트 = 우측 잔여, 검색창 = 폭 − 318, 최소 758×588. ContainerPanel 의 `_rect`/`MIN_W..MAX_H`/`TOTAL_H`/`DrawResizeHandle`/`SetRect`/`DrawToast` 삭제, 모든 행 `Height` 명시. `SettingsPanel.OnSaved` → `PanelRegistry.ReloadRects + PersistAll`. tests 495 → 518. smoke 9항목 PASS.
```

```bash
git add docs/HANDOFF.md
git -c core.safecrlf=false commit -m "docs(handoff): v0.8.0 3단계 완료 상태

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push -u origin feat/v0.8.0-step3-container
gh pr create --base develop --head feat/v0.8.0-step3-container \
  --title "feat(v0.8.0-s3): ContainerPanel 반응형 — ContainerLayout + PanelWindow 이관 + 레지스트리 z-order" \
  --body "스펙 §11 3단계. D1(ChromeH 흡수)·D2(PanelRegistry z-order) 선행 후 ContainerPanel 이관. tests 495 → 518. 인게임 smoke 9항목 PASS.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
gh pr merge --merge
```

---

## 다음 플랜 (이 문서 범위 밖)

4단계 ItemDetailPanel: `Init(_containerPanel, x, y, w, h)` 의존을 유지한 채 `PanelWindow` 합성 + `ItemDetailLayout`(HeaderBlockH, ScrollH). 등록되면 `ModWindow.IsCoveredByForeign` 에서 ItemDetail 두 항목을 지우고, ModWindow.OnGUI 의 ItemDetail per-frame 저장 5줄을 삭제한다. `ItemDetailPanel.Selector`(SelectorDialog) 는 창이 아니라 오버레이라 등록 대상이 아님 — 가드 항목은 남긴다.
