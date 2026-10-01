# 반응형 패널 레이아웃 v0.8.0 — 4단계 구현 플랜 (ItemDetailPanel → PanelWindow + ItemDetailLayout)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ItemDetailPanel(Item 상세, 컨테이너 창의 ⓘ 토글)을 `PanelWindow` 창 틀 위로 옮기고, `_rect.height - 140` 고정 스크롤·고정 폭 입력 필드·`Init` 의 480×640 강제 bump 를 `ItemDetailLayout` 계산기로 대체한다. 컨테이너 창과 동시에 보이는 첫 등록 창이므로, 레지스트리의 z-order 를 "클릭으로 추측" 에서 "Unity 가 실제로 칠한 순서" 로 바꾼다.

**Architecture:** (D3) `PanelWindow.DrawWindow` 가 Repaint 패스에서 `PanelRegistry.NotePainted(this)` 를 호출해 칠해진 순번을 기록한다 — Unity 는 창을 뒤에서 앞으로 칠하므로 마지막에 칠해진 창이 맨 앞이다. `TopmostVisibleAt` 은 그 순번이 가장 큰 창을 고르고, 3단계의 클릭 기반 `BringToFront` 는 삭제한다. 모달(삭제 확인창·선택 팝업)이 떠 있으면 리사이즈를 시작하지 않는다(`IsModalOpen`). (D4) `PanelWindow` 에 헤더 추가 컨트롤 훅(`DrawHeaderExtra` + `HeaderExtraW`)을 넣어 상세 창의 [편집] 토글을 헤더에 유지한다. 그 위에서 `ItemDetailLayout`(스크롤 높이 = 내용 − 이름 행 − 편집 안내문, 입력 필드·선택 버튼 폭 = 잔여)을 만들고 ItemDetailPanel 을 합성으로 이관한다.

**Tech Stack:** C# 10 / net6.0 / BepInEx 6 IL2CPP / Unity IMGUI / xunit + Shouldly. 테스트는 소스 링크 + `UnityStubs.cs`(`GUI.Window` 스텁은 콜백을 호출하지 않음 → `NotePainted` 는 테스트가 직접 호출, `Event.current` 정적 단일 인스턴스, `Screen` 1920×1080).

**Spec:** `docs/superpowers/specs/2026-09-29-longyin-roster-mod-v0.8.0-responsive-layout-design.md` (§6 ItemDetailPanel 행, §8, §11 4단계)

**전제:** `develop` = 439fc22 (PR #4~#9 머지, tests 521). `UI/PanelWindow.cs`(`Registry`, `TryBeginResize`, `HandleResizeContinuation`), `UI/PanelRegistry.cs`(`HandleEvents`, `IsCoveredByForeign`, `_foreignInFront`), `DialogStyle.ChromeH = 84`, `PanelWindowLogic.ContentRect(…, bottomSlack)` 가 있다.

**이 플랜이 내리는 설계 결정**
- **D3 칠한 순서 = z-order**: 3단계 리뷰가 "핸들 클릭을 `e.Use()` 로 소비할 때 Unity 가 창을 앞으로 올리는지 소스로 판정 불가" 라고 남긴 문제를, 추측을 없애는 쪽으로 푼다. Repaint 콜백 순서를 관찰하면 클릭·새 창 생성·창 닫힘 어느 경로로 순서가 바뀌든 다음 프레임에 맞춰진다. **검증은 smoke 4번** — 반대로 동작하면(가려진 창의 코너가 잡히면) Unity 가 앞→뒤로 칠한다는 뜻이고, `TopmostVisibleAt` 의 비교 부등호 하나(`>` → `<`)를 뒤집으면 된다.
- **D4 헤더 추가 컨트롤**: `PanelWindow.DrawHeaderExtra : Action<float>?`(인자 = 창 폭)와 `HeaderExtraW`(드래그 영역에서 제외할 폭). 드래그 영역은 `PanelWindowLogic.DragRect` 순수 함수.
- **모달 가드**: `PanelRegistry.IsModalOpen : Func<bool>?`. 컨테이너 삭제 확인창(`ConfirmDialog`, 화면 중앙)·상세 선택 팝업(`SelectorDialog`)이 떠 있으면 MouseDown 을 무시한다. 3단계 리뷰 이월 Minor "삭제 확인창이 가림 목록에 없음" 을 같은 람다를 고치는 김에 함께 처리한다.
- **간격 규칙**: 내용 영역에 요소가 n개면 간격은 n−1개 — 스크롤/리스트도 요소 하나로 센다. `ItemDetailLayout` 은 이 규칙으로 쓴다(3단계 리뷰 M-3 의 원칙화. `ContainerLayout` 의 4px 오차 수정은 이 플랜 범위 밖).

## Global Constraints

- `LangVersion` 10, `net6.0`, `Nullable` enable, `TreatWarningsAsErrors` true — 경고 하나가 빌드 실패.
- IMGUI 금지 API: `GUILayout.FlexibleSpace`, `GUILayoutUtility.GetLastRect`, `GUILayout.BeginArea`, `GUILayout.ExpandWidth/ExpandHeight`, `GUI.BringWindowToFront`/`GUI.FocusWindow`. 새 IMGUI API 도입 금지.
- 게임 조작 코드(`Core/*Applier`, `*Reflector`, `*Patch`, `ItemEditApplier`, `HeroSpeAddDataReflector`) 변경 금지. ItemDetailPanel 의 `ApplyField`/`ApplyStatEdit*`/`ReadField*` 는 손대지 않는다(레이아웃·창 틀만).
- 소스 `.cs` 는 CRLF. 새 소스 파일은 `LongYinRoster.Tests.csproj` 에 `<Compile Include>` 링크 추가.
- 기존 설정 키 이름 불변(`ItemDetailPanelX/Y/Width/Height/Open`). 신설 키 없음. `Plugin.VERSION` 불변.
- 상수 단일 출처는 `DialogStyle`(`HeaderHeight=28`, `RowHeight=24`, `ButtonRowHeight=28`, `Padding=12`, `Gap=4`, `ScrollbarW=20`, `ImguiSlack=32`, `ChromeH=84`). 계산기는 `content.height`(슬랙 제외)를 그대로 쓰고 `MinH = ChromeH + 내용 최소`.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. 브랜치: 플랜 `docs/v0.8.0-stage4-plan`, 구현 `feat/v0.8.0-step4-itemdetail` — `develop` 에서 분기, PR → `develop`, merge commit.
- Release 빌드(DeployToBepInEx)는 게임이 꺼진 상태에서만 — `tasklist | grep -i LongYinLiZhiZhuan` 이 비어야 한다.

## Review Focus

1. 컨테이너 창과 상세 창이 겹칠 때 **화면에 위로 보이는** 창의 코너만 잡힌다 — 뒤 창의 코너가 앞 창 영역 안이면 무시 — Task 1 `HandleEvents_PaintOrderDecidesWhichHandleWins`, `NotePainted_LastPaintedIsTopmost` + smoke 4.
2. 클릭만으로는(다시 칠해지기 전) 순서가 바뀌지 않는다 — Unity 가 실제로 올리지 않은 창을 레지스트리가 앞이라고 믿지 않는다 — Task 1 `HandleEvents_ClickAloneDoesNotReorder`.
3. 삭제 확인창·선택 팝업이 떠 있는 동안 그 버튼 자리가 어떤 창의 코너와 겹쳐도 리사이즈가 시작되지 않는다 — Task 1 `ModalOpen_BlocksResizeStart`.
4. 편집 모드를 켜면 안내문 2행만큼 스크롤이 줄고, 최소 크기(389×244)에서도 스크롤이 3행 밑으로 안 내려간다 — Task 3 `Compute_EditMode_SubtractsDisclaimer`, `MinSize_FitsEditModeWithThreeRows`.
5. 구 cfg/구 reset 값 380×500 + 화면 밖 위치 → 폭 389 로 올라가고 화면 안으로 들어온다 — Task 4 `OldCfgBelowMin_HydratesUpToMin_InsideScreen`.

---

## 파일 구조

**신규**
- `src/LongYinRoster/UI/Layout/ItemDetailLayout.cs` — 상세 패널 계산기.
- `src/LongYinRoster.Tests/ItemDetailLayoutTests.cs`, `src/LongYinRoster.Tests/ItemDetailPanelWindowTests.cs`.

**수정**
- `src/LongYinRoster/UI/PanelRegistry.cs` — `NotePainted`, 순번 기반 `TopmostVisibleAt`, `IsModalOpen`, `BringToFront` 삭제.
- `src/LongYinRoster/UI/PanelWindow.cs` — `PaintOrder`, Repaint 때 `NotePainted`, `DrawHeaderExtra`/`HeaderExtraW`, `DragRect` 사용.
- `src/LongYinRoster/UI/Layout/PanelWindowLogic.cs` — `DragRect`.
- `src/LongYinRoster/UI/ItemDetailPanel.cs` — PanelWindow 합성, `ItemDetailLayout` 적용.
- `src/LongYinRoster/UI/ContainerPanel.cs` — `IsConfirmVisible`.
- `src/LongYinRoster/UI/SettingsPanel.cs` — reset 기본값 380×500 → 480×640.
- `src/LongYinRoster/UI/ModWindow.cs` — 상세 창 등록, `Init` 인자 축소, `IsCoveredByForeign`/`IsModalOpen`, per-frame 저장 블록 삭제.
- 테스트: `PanelRegistryTests.cs`, `PanelWindowLogicTests.cs`, `LongYinRoster.Tests.csproj`(링크 1개).
- `docs/HANDOFF.md`.

테스트 수: 521 → Task 1 524 → Task 2 526 → Task 3 533 → Task 4 535.

---

### Task 0: 플랜 PR + 4단계 브랜치

**Files:** 없음 (git 만)

- [ ] **Step 1: 플랜 브랜치를 develop 에 PR**

```bash
cd E:/LylzzBox
git push -u origin docs/v0.8.0-stage4-plan
gh pr create --base develop --head docs/v0.8.0-stage4-plan \
  --title "docs: v0.8.0 4단계(ItemDetailPanel) 플랜" \
  --body "플랜 문서만. 설계 결정 D3(칠한 순서 = z-order) · D4(헤더 추가 컨트롤) · 모달 가드 포함.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
gh pr merge --merge
```

- [ ] **Step 2: 4단계 브랜치 생성**

```bash
git fetch origin develop && git checkout -b feat/v0.8.0-step4-itemdetail origin/develop
```

---

### Task 1: D3 — 칠한 순서로 z-order 판정 + 모달 가드

**Files:**
- Modify: `src/LongYinRoster/UI/PanelRegistry.cs` (`HandleEvents` 의 MouseDown 분기, `TopmostVisibleAt`, `BringToFront` 삭제, `NotePainted`/`IsModalOpen` 추가)
- Modify: `src/LongYinRoster/UI/PanelWindow.cs` (`PaintOrder`, `DrawWindow` 의 Repaint 통지)
- Test: `src/LongYinRoster.Tests/PanelRegistryTests.cs`

**Interfaces:**
- Consumes: `PanelWindow.Registry`, `TryBeginResize`, `HandleResizeContinuation`.
- Produces: `PanelWindow.PaintOrder : long (internal get/set)`; `PanelRegistry.NotePainted(PanelWindow) (internal)`, `PanelRegistry.IsModalOpen : Func<bool>?`. `TopmostVisibleAt` 의 의미 변경: 보이는 창 중 `PaintOrder` 최대(동률이면 나중 등록). 클릭은 순서를 바꾸지 않는다.

- [ ] **Step 1: 테스트 수정·추가**

`src/LongYinRoster.Tests/PanelRegistryTests.cs`:

(a) 기존 `HandleEvents_ClickInsideWindow_BringsItToFront_ThenItsHandleWins` 메서드 전체를 아래로 교체:

```csharp
    [Fact]
    public void HandleEvents_PaintOrderDecidesWhichHandleWins()
    {
        // Unity 는 창을 뒤 → 앞으로 칠한다. B 다음에 A 가 칠해졌으면 A 가 맨 앞 — 겹친 자리의 A 핸들이 이긴다.
        var (reg, a, b) = Overlapping();
        try
        {
            reg.NotePainted(b);
            reg.NotePainted(a);
            Fire(reg, EventType.MouseDown, 492, 392);
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((608f, 508f));
            (b.Rect.width, b.Rect.height).ShouldBe((400f, 300f));
            Fire(reg, EventType.MouseUp, 700, 600);
            a.IsResizing.ShouldBeFalse();
        }
        finally { Reset(); }
    }
```

(b) 클래스 끝에 추가:

```csharp
    // ── v0.8.0 S4 (D3) — z-order 는 칠해진 순서. 클릭으로 추측하지 않는다 ──

    [Fact]
    public void NotePainted_LastPaintedIsTopmost()
    {
        var (reg, a, b) = Overlapping();
        reg.TopmostVisibleAt(new Vector2(492, 392)).ShouldBeSameAs(b);   // 칠해지기 전: 나중 등록이 앞
        reg.NotePainted(b);
        reg.NotePainted(a);
        reg.TopmostVisibleAt(new Vector2(492, 392)).ShouldBeSameAs(a);
        reg.NotePainted(a);
        reg.NotePainted(b);                                              // 다음 프레임에 B 가 다시 앞
        reg.TopmostVisibleAt(new Vector2(492, 392)).ShouldBeSameAs(b);
    }

    [Fact]
    public void HandleEvents_ClickAloneDoesNotReorder()
    {
        // 클릭한 창을 Unity 가 실제로 앞으로 올렸는지는 다음 Repaint 가 알려 준다 — 그 전에 레지스트리가 앞이라고 믿지 않는다
        var (reg, a, b) = Overlapping();
        try
        {
            Fire(reg, EventType.MouseDown, 150, 150);   // A 만 있는 자리
            Fire(reg, EventType.MouseUp,   150, 150);
            reg.TopmostVisibleAt(new Vector2(492, 392)).ShouldBeSameAs(b);
        }
        finally { Reset(); }
    }

    [Fact]
    public void ModalOpen_BlocksResizeStart()
    {
        // 삭제 확인창·선택 팝업이 떠 있으면 그 버튼 자리가 코너와 겹쳐도 리사이즈를 시작하지 않는다
        var a = Make(1, 100, 100, 400, 300);
        var reg = new PanelRegistry { IsModalOpen = () => true };
        reg.Register(a);
        try
        {
            Fire(reg, EventType.MouseDown, 492, 392);
            Fire(reg, EventType.MouseDrag, 700, 600);
            (a.Rect.width, a.Rect.height).ShouldBe((400f, 300f));
            a.IsResizing.ShouldBeFalse();
        }
        finally { Reset(); }
    }
```

- [ ] **Step 2: 실패 확인**

Run: `cd E:/LylzzBox && DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error CS" | sed 's/.*error //; s/\[E:.*//' | sort -u | head -3`
Expected: `CS1061: 'PanelRegistry' does not contain a definition for 'NotePainted'` 와 `CS0117: 'PanelRegistry' does not contain a definition for 'IsModalOpen'`.

- [ ] **Step 3: 구현**

`src/LongYinRoster/UI/PanelWindow.cs`:

(a) `internal bool IsResizing => _resizing;` 줄 아래에:

```csharp
    /// <summary>마지막 Repaint 에서 칠해진 순번(PanelRegistry.NotePainted 가 매긴다). 클수록 앞. 0 = 아직 안 칠해짐.</summary>
    internal long PaintOrder { get; set; }
```

(b) `DrawWindow` 의 `if (e != null && e.type == EventType.MouseUp) _mouseUpSeen = true;` 줄 아래에:

```csharp
            // D3 — Unity 는 창을 뒤에서 앞으로 칠한다. 칠해질 때마다 순번을 받아 두면 레지스트리가 실제 z-order 를 안다.
            if (e != null && e.type == EventType.Repaint) Registry?.NotePainted(this);
```

`src/LongYinRoster/UI/PanelRegistry.cs`:

(a) 클래스 doc 의 둘째·셋째 줄(`리스트 순서 = z-order … 핸들 판정은 그 맨 앞 창에만 시킨다.`)을:

```csharp
/// z-order 는 추측하지 않고 관찰한다(D3): 각 창이 Repaint 때 NotePainted 로 칠해진 순번을 받는다 — Unity 는 뒤에서 앞으로 칠하므로
/// 순번이 가장 큰 보이는 창이 맨 앞. 핸들 판정은 MouseDown 위치의 맨 앞 창에만 시킨다. 모달(IsModalOpen)이 떠 있으면 시작하지 않는다.
```

(b) `IsCoveredByForeign` 선언 아래에:

```csharp
    /// <summary>모달 대화상자(삭제 확인창·선택 팝업)가 떠 있으면 true — 그동안 코너 리사이즈를 시작하지 않는다. ModWindow 가 세팅.</summary>
    public Func<bool>? IsModalOpen;

    private long _paintSeq;

    /// <summary>창 콜백이 Repaint 패스에서 호출. 나중에 칠해진 창일수록 큰 순번 = 앞.</summary>
    internal void NotePainted(PanelWindow window) => window.PaintOrder = ++_paintSeq;
```

(c) `HandleEvents` 의 MouseDown 분기 전체를:

```csharp
        if (e.type == EventType.MouseDown)
        {
            if (IsModalOpen != null && IsModalOpen()) return;
            var top = TopmostVisibleAt(e.mousePosition);
            bool inForeign = IsCoveredByForeign != null && IsCoveredByForeign(e.mousePosition);
            if (top == null) { if (inForeign) _foreignInFront = true; return; }
            if (inForeign && _foreignInFront) return;   // 겹친 자리 + 미이관 창이 앞 → 그 창의 클릭
            _foreignInFront = false;
            top.TryBeginResize(e);                       // 순서는 바꾸지 않는다 — 다음 Repaint 가 알려 준다
        }
```

(d) `TopmostVisibleAt` 전체와 `BringToFront` 메서드를 아래 하나로 교체:

```csharp
    /// <summary>화면 좌표를 포함하는 보이는 창 중 맨 앞 = PaintOrder 최대. 동률(아직 안 칠해짐)이면 나중에 등록된 창. 없으면 null.</summary>
    internal PanelWindow? TopmostVisibleAt(Vector2 pos)
    {
        PanelWindow? best = null;
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var w = _windows[i];
            if (!w.Visible || !w.Rect.Contains(pos)) continue;
            if (best == null || w.PaintOrder > best.PaintOrder) best = w;
        }
        return best;
    }
```

- [ ] **Step 4: 통과 확인**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error|Passed!|Failed!"`
Expected: `Passed: 524`(521 + 3). `grep -n 'BringToFront' src/LongYinRoster/UI/*.cs` 는 아무것도 출력하지 않아야 한다. `dotnet build src/LongYinRoster/LongYinRoster.csproj -c Debug --nologo -v minimal` → 경고 0.

- [ ] **Step 5: 커밋**

```bash
git add src/LongYinRoster/UI/PanelRegistry.cs src/LongYinRoster/UI/PanelWindow.cs src/LongYinRoster.Tests/PanelRegistryTests.cs
git -c core.safecrlf=false commit -m "feat(v0.8.0-s4): 레지스트리 z-order 를 칠한 순서로 판정 (D3) + 모달 가드 — 클릭 기반 BringToFront 삭제

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: D4 — PanelWindow 헤더 추가 컨트롤 + `DragRect`

**Files:**
- Modify: `src/LongYinRoster/UI/Layout/PanelWindowLogic.cs` (`DragRect` 추가)
- Modify: `src/LongYinRoster/UI/PanelWindow.cs` (`DrawHeaderExtra`, `HeaderExtraW`, `DrawWindow`)
- Test: `src/LongYinRoster.Tests/PanelWindowLogicTests.cs`

**Interfaces:**
- Produces: `PanelWindowLogic.CloseReserveW : const float (32)`, `PanelWindowLogic.DragRect(Rect window, float headerH, float extraW) : Rect`(창 로컬); `PanelWindow.DrawHeaderExtra : Action<float>?`(인자 = 창 폭), `PanelWindow.HeaderExtraW : float`.

- [ ] **Step 1: 테스트 추가** — `src/LongYinRoster.Tests/PanelWindowLogicTests.cs` 클래스 끝에:

```csharp
    [Fact]
    public void DragRect_ExcludesCloseButtonAndHeaderExtra()
    {
        // 헤더 전체에서 X 버튼 자리(32)와 패널 추가 컨트롤 폭(48)을 뺀다 — 창 로컬 좌표
        var d = PanelWindowLogic.DragRect(new Rect(100, 50, 480, 640), headerH: 28f, extraW: 48f);
        (d.x, d.y, d.width, d.height).ShouldBe((0f, 0f, 400f, 28f));
    }

    [Fact]
    public void DragRect_NeverNegative()
    {
        var d = PanelWindowLogic.DragRect(new Rect(0, 0, 40, 40), 28f, 48f);
        d.width.ShouldBe(0f);
    }
```

- [ ] **Step 2: 실패 확인** — `dotnet test … | grep "error CS"` → `CS0117: 'PanelWindowLogic' does not contain a definition for 'DragRect'`.

- [ ] **Step 3: 구현**

`src/LongYinRoster/UI/Layout/PanelWindowLogic.cs` — `HandleSize` 상수 아래에:

```csharp
    /// <summary>헤더 우측 X 버튼이 차지하는 폭(버튼 22 + 여백). 드래그 영역에서 제외.</summary>
    public const float CloseReserveW = 32f;

    /// <summary>헤더 드래그 영역(창 로컬). X 버튼 자리와 패널이 헤더에 그리는 추가 컨트롤 폭을 뺀다. 폭은 0 미만으로 내려가지 않음.</summary>
    public static Rect DragRect(Rect window, float headerH, float extraW)
        => new(0f, 0f, Math.Max(0f, window.width - CloseReserveW - extraW), headerH);
```

`src/LongYinRoster/UI/PanelWindow.cs`:

(a) `public Action? OnClosed;` 줄 아래에:

```csharp
    /// <summary>헤더 우측(X 왼쪽)에 패널이 그리는 추가 컨트롤(예: 상세 창의 [편집]). 인자 = 창 폭(창 로컬 좌표로 배치). D4.</summary>
    public Action<float>? DrawHeaderExtra;
    /// <summary>DrawHeaderExtra 가 차지하는 폭 — 그만큼 헤더 드래그 영역에서 제외한다.</summary>
    public float HeaderExtraW;
```

(b) `DrawWindow` 의 X 버튼 줄 아래에 한 줄:

```csharp
            DrawHeaderExtra?.Invoke(_rect.width);
```

(c) `GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));` 를:

```csharp
            GUI.DragWindow(PanelWindowLogic.DragRect(_rect, DialogStyle.HeaderHeight, HeaderExtraW));
```

- [ ] **Step 4: 통과 확인** — 전체 `Passed: 526`(524 + 2), Debug 빌드 경고 0.

- [ ] **Step 5: 커밋**

```bash
git add src/LongYinRoster/UI/Layout/PanelWindowLogic.cs src/LongYinRoster/UI/PanelWindow.cs src/LongYinRoster.Tests/PanelWindowLogicTests.cs
git -c core.safecrlf=false commit -m "feat(v0.8.0-s4): PanelWindow 헤더 추가 컨트롤 훅 + PanelWindowLogic.DragRect (D4)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: `ItemDetailLayout` 계산기

**Files:**
- Create: `src/LongYinRoster/UI/Layout/ItemDetailLayout.cs`
- Test: `src/LongYinRoster.Tests/ItemDetailLayoutTests.cs`
- Modify: `src/LongYinRoster.Tests/LongYinRoster.Tests.csproj` (링크)

**Interfaces:**
- Consumes: `DialogStyle.*`(`ChromeH`, `ScrollbarW`), `PanelBounds`.
- Produces: `record struct ItemDetailLayout(float ScrollH, float EditFieldW, float SelectorBtnW, float StatFieldW, float AddSelectorW)`; `ItemDetailLayout.Compute(Rect content, bool editMode)`; `ItemDetailLayout.MinSize`(389×244); consts `EditLabelW=140`, `EditApplyW=50`, `MinEditFieldW=80`, `MinSelectorBtnW=140`, `StatLabelW=110`, `StatBtnW=45`, `MinStatFieldW=60`, `AddLabelW=60`, `AddValueW=60`, `MinAddSelectorW=160`, `MinScrollRows=3`; statics `NameRowH`(24), `DisclaimerH`(48), `MinScrollH`(80).

- [ ] **Step 1: 테스트 작성**

`src/LongYinRoster.Tests/ItemDetailLayoutTests.cs`:

```csharp
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S4 — 상세 패널 계산기. 세로 확장 = 정보/편집 스크롤, 가로 확장 = 입력 필드·선택 버튼.
/// 간격 규칙: 요소 n개 → 간격 n−1개(스크롤도 요소 하나).</summary>
public class ItemDetailLayoutTests
{
    // 기본 창 480×640 → 내용 456×556 (ChromeH 84)
    private static readonly Rect Default = new(12, 40, 456, 556);

    [Fact]
    public void Compute_ViewMode_ScrollTakesAllButNameRow()
    {
        // 요소 2개(이름 행 24, 스크롤) → 간격 1개: 556 - 24 - 4 = 528
        ItemDetailLayout.Compute(Default, editMode: false).ScrollH.ShouldBe(528f);
    }

    [Fact]
    public void Compute_EditMode_SubtractsDisclaimer()
    {
        // 요소 3개(이름 24, 안내문 2행 48, 스크롤) → 간격 2개: 556 - 24 - 48 - 8 = 476
        ItemDetailLayout.Compute(Default, editMode: true).ScrollH.ShouldBe(476f);
    }

    [Fact]
    public void Compute_Default_FieldsTakeRemainingWidth()
    {
        // 스크롤 안쪽 폭 = 456 - 스크롤바 20 = 436
        var L = ItemDetailLayout.Compute(Default, editMode: true);
        L.EditFieldW.ShouldBe(436f - 140f - 50f - 4 * DialogStyle.Gap);          // 230
        L.SelectorBtnW.ShouldBe(436f - 140f - 3 * DialogStyle.Gap);              // 284
        L.StatFieldW.ShouldBe(436f - 110f - 2 * 45f - 5 * DialogStyle.Gap);      // 216
        L.AddSelectorW.ShouldBe(436f - 60f - 60f - 45f - 5 * DialogStyle.Gap);   // 251
    }

    [Fact]
    public void Compute_Tiny_UsesFloors()
    {
        var L = ItemDetailLayout.Compute(new Rect(12, 40, 100, 50), editMode: true);
        L.ScrollH.ShouldBe(ItemDetailLayout.MinScrollH);
        (L.EditFieldW, L.SelectorBtnW, L.StatFieldW, L.AddSelectorW).ShouldBe(
            (ItemDetailLayout.MinEditFieldW, ItemDetailLayout.MinSelectorBtnW, ItemDetailLayout.MinStatFieldW, ItemDetailLayout.MinAddSelectorW));
    }

    [Fact]
    public void Compute_Taller_MonotonicScroll()
    {
        var a = ItemDetailLayout.Compute(Default, false);
        var b = ItemDetailLayout.Compute(new Rect(12, 40, 456, 900), false);
        b.ScrollH.ShouldBeGreaterThan(a.ScrollH);
    }

    [Fact]
    public void Compute_Wider_MonotonicFields()
    {
        var a = ItemDetailLayout.Compute(Default, true);
        var b = ItemDetailLayout.Compute(new Rect(12, 40, 800, 556), true);
        b.EditFieldW.ShouldBeGreaterThan(a.EditFieldW);
        b.AddSelectorW.ShouldBeGreaterThan(a.AddSelectorW);
    }

    [Fact]
    public void MinSize_FitsEditModeWithThreeRows()
    {
        var m = ItemDetailLayout.MinSize;
        m.MinW.ShouldBe(389f);   // 가장 넓은 고정 행(속성 추가 60+160+60+45 + 5×4 = 345) + 스크롤바 20 + 여백 24
        m.MinH.ShouldBe(244f);   // 84 + 이름 28 + 안내문 52 + 스크롤 3행 80
        var L = ItemDetailLayout.Compute(new Rect(12, 40, m.MinW - 2 * DialogStyle.Padding, m.MinH - DialogStyle.ChromeH), editMode: true);
        L.ScrollH.ShouldBe(ItemDetailLayout.MinScrollH);          // 정확히 3행
        L.AddSelectorW.ShouldBe(ItemDetailLayout.MinAddSelectorW); // 가장 넓은 행이 정확히 들어감
        L.EditFieldW.ShouldBeGreaterThanOrEqualTo(ItemDetailLayout.MinEditFieldW);
    }
}
```

- [ ] **Step 2: csproj 링크** — `<!-- v0.8.0 S3 -->` 블록(`ContainerLayout.cs` 링크) 아래:

```xml
    <!-- v0.8.0 S4 -->
    <Compile Include="../LongYinRoster/UI/Layout/ItemDetailLayout.cs">
      <Link>UI/Layout/ItemDetailLayout.cs</Link>
    </Compile>
```

- [ ] **Step 3: 실패 확인** — `dotnet test … | grep "error CS"` → `CS2001 … ItemDetailLayout.cs could not be found`.

- [ ] **Step 4: 구현**

`src/LongYinRoster/UI/Layout/ItemDetailLayout.cs`:

```csharp
using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 S4 — ItemDetailPanel 계산기. 고정: 이름 행(24), 편집 안내문(2행 48, 편집 모드에서만), 라벨 폭(편집 140 / 속성 110 / 추가 60),
/// 버튼 폭(적용 50 / 수정·삭제·추가 45), 추가 값 필드 60. 세로 확장: 정보/편집 스크롤(최소 3행). 가로 확장: 값 입력 필드·선택 버튼.
/// 간격 규칙: 내용 요소 n개 → 간격 n−1개(스크롤도 요소 하나). content.height 는 ContentRect 가 ImguiSlack 을 이미 뺀 값.
/// </summary>
public readonly record struct ItemDetailLayout(
    float ScrollH, float EditFieldW, float SelectorBtnW, float StatFieldW, float AddSelectorW)
{
    public const float EditLabelW      = 140f;   // "  {라벨}: "
    public const float EditApplyW      = 50f;    // [적용]
    public const float MinEditFieldW   = 80f;
    public const float MinSelectorBtnW = 140f;   // 등급/품질 "값(번호) ▼"
    public const float StatLabelW      = 110f;   // "  {속성}({번호}):"
    public const float StatBtnW        = 45f;    // [수정] [삭제] [추가]
    public const float MinStatFieldW   = 60f;
    public const float AddLabelW       = 60f;    // "  추가:"
    public const float AddValueW       = 60f;
    public const float MinAddSelectorW = 160f;   // 속성 선택 "이름(번호) ▼"
    public const int   MinScrollRows   = 3;

    public static float NameRowH    => DialogStyle.RowHeight;                                                      // 24
    public static float DisclaimerH => 2f * DialogStyle.RowHeight;                                                 // 48 — 좁은 창에서 2행으로 줄바꿈
    public static float MinScrollH  => MinScrollRows * DialogStyle.RowHeight + (MinScrollRows - 1) * DialogStyle.Gap;   // 80

    // 행별 고정 폭(컨트롤 margin 포함: 컨트롤 k개 → (k+1)×Gap)
    private const float EditRowFixedW     = EditLabelW + EditApplyW + 4f * DialogStyle.Gap;                 // 206
    private const float SelectorRowFixedW = EditLabelW + 3f * DialogStyle.Gap;                              // 152
    private const float StatRowFixedW     = StatLabelW + 2f * StatBtnW + 5f * DialogStyle.Gap;              // 220
    private const float AddRowFixedW      = AddLabelW + AddValueW + StatBtnW + 5f * DialogStyle.Gap;        // 185

    public static PanelBounds MinSize => new(
        MinW: Math.Max(Math.Max(EditRowFixedW + MinEditFieldW, SelectorRowFixedW + MinSelectorBtnW),
                       Math.Max(StatRowFixedW + MinStatFieldW, AddRowFixedW + MinAddSelectorW))            // 345 (속성 추가 행)
              + DialogStyle.ScrollbarW + 2f * DialogStyle.Padding,                                          // 389
        MinH: DialogStyle.ChromeH + NameRowH + DialogStyle.Gap + DisclaimerH + DialogStyle.Gap + MinScrollH);   // 244 (편집 모드 기준)

    public static ItemDetailLayout Compute(Rect content, bool editMode)
    {
        float fixedH  = NameRowH + DialogStyle.Gap + (editMode ? DisclaimerH + DialogStyle.Gap : 0f);
        float scrollH = Math.Max(MinScrollH, content.height - fixedH);

        float inner = content.width - DialogStyle.ScrollbarW;   // 스크롤 뷰 안쪽 폭
        return new ItemDetailLayout(
            ScrollH:      scrollH,
            EditFieldW:   Math.Max(MinEditFieldW,   inner - EditRowFixedW),
            SelectorBtnW: Math.Max(MinSelectorBtnW, inner - SelectorRowFixedW),
            StatFieldW:   Math.Max(MinStatFieldW,   inner - StatRowFixedW),
            AddSelectorW: Math.Max(MinAddSelectorW, inner - AddRowFixedW));
    }
}
```

- [ ] **Step 5: 통과 확인** — `--filter "FullyQualifiedName~ItemDetailLayoutTests"` → `Passed: 7`. 전체 `Passed: 533`(526 + 7). Debug 빌드 경고 0.

- [ ] **Step 6: 커밋**

```bash
git add src/LongYinRoster/UI/Layout/ItemDetailLayout.cs src/LongYinRoster.Tests/ItemDetailLayoutTests.cs src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
git -c core.safecrlf=false commit -m "feat(v0.8.0-s4): ItemDetailLayout 계산기 — 스크롤 높이(편집 안내문 반영)·입력 필드/선택 버튼 폭

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: ItemDetailPanel 이관 + ModWindow 연결 + smoke + PR

**Files:**
- Modify: `src/LongYinRoster/UI/ItemDetailPanel.cs` (블록 (a)~(h))
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs` (`IsConfirmVisible` 한 줄)
- Modify: `src/LongYinRoster/UI/SettingsPanel.cs` (reset 기본값 한 줄)
- Modify: `src/LongYinRoster/UI/ModWindow.cs` (Init·등록·가드·per-frame 저장 삭제)
- Create: `src/LongYinRoster.Tests/ItemDetailPanelWindowTests.cs`
- Modify: `docs/HANDOFF.md`

**Interfaces:**
- Consumes: `PanelWindow`(`DrawHeaderExtra`, `HeaderExtraW`, `Close`), `RectBinding.Of(x, y, w, h, Config.ItemDetailPanelOpen)`, `PanelRegistry.Register/IsModalOpen/IsCoveredByForeign`, `ItemDetailLayout.Compute/MinSize`.
- Produces: `ItemDetailPanel.Window : PanelWindow`, `Visible`/`WindowRect` 위임, `Init(ContainerPanel host)`(rect 인자 삭제); `ContainerPanel.IsConfirmVisible : bool`.

- [ ] **Step 1: 테스트 작성**

`src/LongYinRoster.Tests/ItemDetailPanelWindowTests.cs`:

```csharp
using System;
using System.IO;
using BepInEx.Configuration;
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.8.0 S4 — ItemDetailPanel 창 틀. IMGUI 내용은 인게임 smoke.</summary>
public class ItemDetailPanelWindowTests
{
    [Fact]
    public void Window_StartsHiddenWithItemDetailMinSize()
    {
        var panel = new ItemDetailPanel();
        panel.Visible.ShouldBeFalse();
        (panel.Window.Rect.width, panel.Window.Rect.height).ShouldBe((ItemDetailLayout.MinSize.MinW, ItemDetailLayout.MinSize.MinH));
        panel.Window.HeaderExtraW.ShouldBe(48f);   // 헤더의 [편집] 토글 자리
    }

    [Fact]
    public void OldCfgBelowMin_HydratesUpToMin_InsideScreen()
    {
        // 구 cfg / 구 "영속화 정보 reset" 값 380×500 이 화면 밖 위치에 저장돼 있어도: 폭 389 로 올라가고 화면 안으로 들어온다
        var path = Path.Combine(Path.GetTempPath(), $"lyr-itemdetail-{Guid.NewGuid():N}.cfg");
        var cfg = new ConfigFile(path, false) { SaveOnConfigSet = false };
        var binding = RectBinding.Of(cfg.Bind("T", "X", 1800f, ""), cfg.Bind("T", "Y", 900f, ""),
                                     cfg.Bind("T", "W", 380f, ""), cfg.Bind("T", "H", 500f, ""));
        var w = new PanelWindow(0x1234, "t", () => ItemDetailLayout.MinSize, binding);
        w.Hydrate(1920, 1080);
        (w.Rect.x, w.Rect.y, w.Rect.width, w.Rect.height).ShouldBe((1531f, 580f, 389f, 500f));
    }
}
```

- [ ] **Step 2: 실패 확인** — `dotnet test … | grep "error CS"` → `CS1061: 'ItemDetailPanel' does not contain a definition for 'Window'`.

- [ ] **Step 3: ItemDetailPanel 블록 교체**

`src/LongYinRoster/UI/ItemDetailPanel.cs` — 각 항목은 "기존 → 새 것". 그 외(`ReadFieldValueAsText`, `ReadFieldOrPropertyRaw`, `ApplyField`, `ApplyStatEdit*`, `ReadIntField`, `ReadFloatField`, `DrawEmpty`)는 손대지 않는다.

**(a) using** — `using LongYinRoster.Core;` 아래에 `using LongYinRoster.UI.Layout;`.

**(b) 창 상태** — 기존:
```csharp
    public bool Visible { get; set; } = false;
    private Rect _rect = new Rect(820, 100, 380, 500);
    private const int WindowID = 0x4C593734;   // "LY74"
```
→
```csharp
    private const int WindowID = 0x4C593734;   // "LY74"
    private const float EditToggleReserveW = 48f;   // 헤더의 [편집] 버튼(44) + 여백

    /// <summary>v0.8.0 S4 — 창 틀. 배경/헤더/X/드래그/코너 리사이즈/화면 클램프/rect·Open↔Config 는 전부 PanelWindow.</summary>
    public PanelWindow Window { get; }
    public bool Visible { get => Window.Visible; set => Window.Visible = value; }
    public Rect WindowRect => Window.Rect;

    public ItemDetailPanel()
    {
        Window = new PanelWindow(WindowID, "Item 상세", () => ItemDetailLayout.MinSize,
            RectBinding.Of(Config.ItemDetailPanelX, Config.ItemDetailPanelY,
                           Config.ItemDetailPanelWidth, Config.ItemDetailPanelHeight, Config.ItemDetailPanelOpen));
        Window.HeaderExtraW    = EditToggleReserveW;
        Window.DrawHeaderExtra = DrawEditToggle;
    }
```

**(c) Init / WindowRect** — 기존 `public void Init(ContainerPanel host, float defaultX, float defaultY, float defaultWidth, float defaultHeight) { … }` 메서드 전체와 그 아래 `public Rect WindowRect => _rect;` 줄을:
```csharp
    /// <summary>호스트(ContainerPanel) 연결. rect·Open 은 PanelWindow(PanelRegistry.HydrateAll) 가 읽는다 — v0.8.0 S4.
    /// 구 480×640 강제 bump 는 ItemDetailLayout.MinSize(389×244) 클램프로 대체.</summary>
    public void Init(ContainerPanel host)
    {
        _hostPanel = host;
    }
```

**(d) OnGUI + Draw** — 기존 `public void OnGUI()` 부터 `private void Draw(int id)` 메서드 끝까지를:
```csharp
    public void OnGUI()
    {
        if (!Visible) return;
        try
        {
            // Focus 변경 감지 → textfield buffer reset (v0.7.4 D-1 stale focus 패턴 mirror)
            var current = _hostPanel?.GetFocusedRawItem();
            if (!ReferenceEquals(current, _lastFocusedRawRef))
            {
                _textBuf.Clear();
                _lastFocusedRawRef = current;
            }
        }
        catch (Exception ex)
        {
            Util.Logger.WarnOnce("ItemDetailPanel", $"ItemDetailPanel.OnGUI threw: {ex.GetType().Name}: {ex.Message}");
        }

        Window.OnGUI(DrawContent);   // 창 틀·예외 처리는 PanelWindow

        // v0.7.7 — modal selector popup (등급/품질/SpeAddType 통합)
        _selector.OnGUI();
    }

    private bool IsExternalContainerFocus => _hostPanel?.Focus?.Area == ContainerArea.Container;

    /// <summary>헤더의 [편집] 토글(X 왼쪽) — 외부 컨테이너 area 시 disabled. PanelWindow.DrawHeaderExtra 로 호출된다(창 로컬 좌표).</summary>
    private void DrawEditToggle(float windowW)
    {
        bool isExternalContainer = IsExternalContainerFocus;
        var prevEnabled = GUI.enabled;
        GUI.enabled = !isExternalContainer;
        var prevColor = GUI.color;
        if (_editMode && !isExternalContainer) GUI.color = Color.cyan;
        if (GUI.Button(new Rect(windowW - 76, 4, 44, 20), KoreanStrings.EditModeBtn))
        {
            _editMode = !_editMode;
            _textBuf.Clear();
        }
        GUI.color = prevColor;
        GUI.enabled = prevEnabled;
    }

    private void DrawContent(Rect content)
    {
        var raw = _hostPanel?.GetFocusedRawItem();
        if (raw == null) DrawEmpty();
        else DrawDetails(raw, IsExternalContainerFocus, content);
    }
```

**(e) DrawDetails 머리** — 서명을 `private void DrawDetails(object raw, bool isExternalContainer, Rect content)` 로. 기존
```csharp
        GUILayout.Label($"  {name}");
        GUI.color = prevColor;
        GUILayout.Space(4);
```
→
```csharp
        GUILayout.Label($"  {name}", GUILayout.Height(ItemDetailLayout.NameRowH));
        GUI.color = prevColor;

        // 편집 안내문이 보이는지에 따라 스크롤 높이가 달라진다 — 계산과 Draw 가 같은 플래그를 쓴다
        bool showEdit = _editMode && !isExternalContainer;
        var L = ItemDetailLayout.Compute(content, showEdit);
```
기존 `if (_editMode && !isExternalContainer)` (disclaimer 블록의 조건) → `if (showEdit)`, 그 안의 `GUILayout.Label(KoreanStrings.EditDisclaimer);` → `GUILayout.Label(KoreanStrings.EditDisclaimer, GUILayout.Height(ItemDetailLayout.DisclaimerH));`. 스크롤 줄 `GUILayout.BeginScrollView(_scroll, GUILayout.Height(_rect.height - 140))` → `GUILayout.BeginScrollView(_scroll, GUILayout.Height(L.ScrollH))`. 스크롤 안의 `if (_editMode && !isExternalContainer)` → `if (showEdit)`.

**(f) 편집 행 호출** — `DrawEditRow(raw, ef);` → `DrawEditRow(raw, ef, L);`. 네 곳의 `DrawHeroSpeAddDataSection(raw, "…", "…", KoreanStrings.StatEditSection_…);` 호출 끝에 `, L` 인자 추가.

**(g) DrawEditRow / DrawSelectorRow** — 서명 `private void DrawEditRow(object raw, ItemEditField ef, ItemDetailLayout L)`; 그 안의 두 `DrawSelectorRow(raw, ef, …, …Options());` 호출 끝에 `, L` 추가. `DrawSelectorRow` 서명 마지막 인자로 `ItemDetailLayout L` 추가:
```csharp
    private void DrawSelectorRow(
        object raw, ItemEditField ef,
        Func<int, string> labelFor, string dialogTitle,
        IReadOnlyList<(int Value, string Label)> options, ItemDetailLayout L)
```
두 메서드에 하나씩 있는 `GUILayout.Label($"  {ef.KrLabel}: ", GUILayout.Width(140));` (총 2곳) → `GUILayout.Width(ItemDetailLayout.EditLabelW)`. `GUILayout.TextField(_textBuf[ef.Path], GUILayout.Width(80))` → `GUILayout.Width(L.EditFieldW)`. `GUILayout.Button(KoreanStrings.EditApplyBtn, GUILayout.Width(50))` → `GUILayout.Width(ItemDetailLayout.EditApplyW)`. `GUILayout.Button($"{display} ▼", GUILayout.Width(140))` → `GUILayout.Width(L.SelectorBtnW)`.

**(h) DrawHeroSpeAddDataSection** — 서명 마지막에 `ItemDetailLayout L` 추가. `GUILayout.Label($"  {label}({entType}):", GUILayout.Width(110))` → `GUILayout.Width(ItemDetailLayout.StatLabelW)`; `GUILayout.TextField(_textBuf[tbKey], GUILayout.Width(60))` → `GUILayout.Width(L.StatFieldW)`; `StatEditEditBtn`·`StatEditDeleteBtn`·`StatEditAddBtn` 세 버튼의 `GUILayout.Width(45)` → `GUILayout.Width(ItemDetailLayout.StatBtnW)`; `GUILayout.Label($"  {KoreanStrings.StatEditAddRowLabel}", GUILayout.Width(60))` → `GUILayout.Width(ItemDetailLayout.AddLabelW)`; `GUILayout.Button($"{currentLabel}({currentIdx}) ▼", GUILayout.Width(160))` → `GUILayout.Width(L.AddSelectorW)`; `GUILayout.TextField(_textBuf[newValKey], GUILayout.Width(60))` → `GUILayout.Width(ItemDetailLayout.AddValueW)`.

클래스 doc 의 `GUI.Window / GUI.DragWindow` 언급은 "창 틀은 PanelWindow" 로 고친다.

- [ ] **Step 4: ContainerPanel / SettingsPanel**

`src/LongYinRoster/UI/ContainerPanel.cs` — `private readonly ConfirmDialog _confirmDialog = new();` 줄 아래에:
```csharp
    /// <summary>삭제 확인창이 떠 있는가 — PanelRegistry.IsModalOpen 용(그동안 코너 리사이즈 시작 금지). v0.8.0 S4.</summary>
    public bool IsConfirmVisible => _confirmDialog.IsVisible;
```

`src/LongYinRoster/UI/SettingsPanel.cs` — `DoResetPersistedView` 의
```csharp
        Config.ItemDetailPanelWidth.Value = 380f; Config.ItemDetailPanelHeight.Value = 500f;
```
→
```csharp
        Config.ItemDetailPanelWidth.Value = 480f; Config.ItemDetailPanelHeight.Value = 640f;   // Config 기본값과 동일(구 380×500 은 v0.7.7 이전 값)
```

- [ ] **Step 5: ModWindow 연결**

`src/LongYinRoster/UI/ModWindow.cs`:

(a) 기존
```csharp
        _itemDetailPanel.Init(
            _containerPanel,
            Config.ItemDetailPanelX.Value,
            Config.ItemDetailPanelY.Value,
            Config.ItemDetailPanelWidth.Value,
            Config.ItemDetailPanelHeight.Value);
        _itemDetailPanel.Visible = Config.ItemDetailPanelOpen.Value;
```
→
```csharp
        _itemDetailPanel.Init(_containerPanel);   // rect·Open 은 PanelRegistry.HydrateAll 이 읽는다 — v0.8.0 S4
```

(b) `        _registry.Register(_containerPanel.Window);` 줄 아래에:
```csharp
        _registry.Register(_itemDetailPanel.Window);   // v0.8.0 S4 — 컨테이너 창과 동시에 보이는 첫 등록 창(z-order 는 칠한 순서로 판정)
```
그 위 주석의 `(Container → Settings → ItemGen)` → `(Container → ItemDetail → Settings → ItemGen)`, `HydrateAll` 줄 주석의 `Container → Settings → ItemGen → HydrateAll` → `Container → ItemDetail → Settings → ItemGen → HydrateAll`.

(c) `_registry.IsCoveredByForeign = pos => …;` 블록(주석 2줄 포함) 전체를:
```csharp
        // v0.8.0 (D2) — 아직 이관 안 된 창(PlayerEditor/본체/모드 메뉴)의 영역. 레지스트리는 마지막 클릭으로 그 창들의 앞/뒤를 추적해,
        // 앞일 때만 겹친 자리의 코너 리사이즈를 양보한다. 5·6단계에 전부 등록되면 삭제.
        _registry.IsCoveredByForeign = pos =>
            (_playerEditorPanel.Visible && _playerEditorPanel.WindowRect.Contains(pos))
            || (_visible && _rect.Contains(pos))
            || (_modeSelector.MenuVisible && _modeSelector.WindowRect.Contains(pos));
        // v0.8.0 S4 — 모달(컨테이너 삭제 확인창 · 상세 선택 팝업)이 떠 있는 동안은 코너 리사이즈를 시작하지 않는다
        _registry.IsModalOpen = () => _containerPanel.IsConfirmVisible || _itemDetailPanel.Selector.Visible;
```

(d) `OnGUI` 의 아래 6줄 삭제:
```csharp
        // ItemDetailPanel 위치/크기/visibility 영속화
        Config.ItemDetailPanelX.Value      = _itemDetailPanel.WindowRect.x;
        Config.ItemDetailPanelY.Value      = _itemDetailPanel.WindowRect.y;
        Config.ItemDetailPanelWidth.Value  = _itemDetailPanel.WindowRect.width;
        Config.ItemDetailPanelHeight.Value = _itemDetailPanel.WindowRect.height;
        Config.ItemDetailPanelOpen.Value   = _itemDetailPanel.Visible;
```

- [ ] **Step 6: 테스트·빌드**

Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet test --nologo -v quiet 2>&1 | grep -E "error|warning CS|Passed!|Failed!"` → `Passed: 535`(533 + 2), 경고 0.
Run: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Debug --nologo -v minimal 2>&1 | grep -E "warn|error|Build succeeded"` → `Build succeeded`, 경고 0.
`grep -n '_rect\b\|GUI.Window\|DragWindow\|FillBackground\|DrawHeader(' src/LongYinRoster/UI/ItemDetailPanel.cs` 는 코드 줄을 출력하지 않아야 한다(doc 주석 제외). `grep -n 'ItemDetailPanelX.Value *=' src/LongYinRoster/UI/ModWindow.cs` 도 비어야 한다.

- [ ] **Step 7: 커밋**

```bash
git add src/LongYinRoster/UI/ItemDetailPanel.cs src/LongYinRoster/UI/ContainerPanel.cs src/LongYinRoster/UI/SettingsPanel.cs src/LongYinRoster/UI/ModWindow.cs src/LongYinRoster.Tests/ItemDetailPanelWindowTests.cs
git -c core.safecrlf=false commit -m "feat(v0.8.0-s4): ItemDetailPanel 을 PanelWindow 로 이관 — ItemDetailLayout(스크롤·필드 폭 자동), 레지스트리 등록, 모달 가드, 480×640 강제 bump 삭제

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

- [ ] **Step 8: 인게임 smoke**

게임 종료 확인 후 Release 빌드·배포, `md5sum` 으로 bin/Release 와 게임 폴더 DLL 해시 일치 확인.

게임 실행 → 세이브 로드 → F11 → 2 → ⓘ 상세:
1. 기본 480×640 으로 뜨고, 아이템 셀을 클릭하면 정보가 표시되며 하단 잘림·가로 스크롤바 없음.
2. 코너로 키우기 / 최소(389×244)까지 줄이기 → 스크롤 영역만 변하고 잘림 없음. 아이템 미선택(빈 상태)에서 최소 크기여도 예외 없음.
3. 헤더의 [편집] 토글 → 안내문이 뜨고 스크롤이 그만큼 줄며 하단 잘림 없음. 창을 넓히면 값 입력 필드·선택 버튼이 넓어짐. 값 [적용], 등급/품질 선택 팝업, 속성 수정·삭제·추가 회귀 없음. [편집] 버튼 자리로는 창이 드래그되지 않고, 헤더 나머지로는 드래그됨.
4. **z-order (D3)** — 상세 창과 컨테이너 창을 겹치게 둔다.
   (a) 상세 창이 위에 보일 때, 상세 창 영역 안에 들어간 컨테이너 코너를 클릭 → 컨테이너가 리사이즈되지 **않음**.
   (b) 컨테이너를 클릭해 앞으로 올린 뒤 같은 코너 → 리사이즈됨.
   (c) 뒤에 있는 창의 **가려지지 않은** 코너를 잡아 리사이즈 → 됨. 그 직후 겹친 자리에서의 판정이 화면에 보이는 앞/뒤와 일치.
   (d) ⓘ 로 상세 창을 닫았다 다시 열면 상세가 맨 앞 — 겹친 자리 판정이 보이는 대로.
   **(a)(b) 가 반대로 나오면**(가려진 창이 잡히면) Unity 가 앞→뒤로 칠한다는 뜻: `PanelRegistry.TopmostVisibleAt` 의 `w.PaintOrder > best.PaintOrder` 를 `<` 로 바꾸고, `NotePainted_LastPaintedIsTopmost`·`HandleEvents_PaintOrderDecidesWhichHandleWins` 테스트의 `NotePainted` 호출 순서와 이름을 그에 맞게 뒤집은 뒤 재배포해 다시 확인한다(장부에 Ruling 으로 기록).
5. 컨테이너 [삭제] 확인창 또는 상세의 선택 팝업이 떠 있는 동안 창 코너 자리를 클릭 → 리사이즈가 시작되지 않고 대화상자가 반응.
6. 설정(F11+3) "영속화 정보 reset" → 다시 F11+2·ⓘ → 상세 창이 970,100 480×640.
7. 컨테이너 창을 X 로 닫으면 상세 창도 닫힘. 재진입·게임 재시작 후 상세 창 크기·위치 유지.

로그: `grep -a -i 'PanelWindow\|ItemDetailPanel\|ModWindow' …/BepInEx/LogOutput.log | grep -a -i 'warn\|error'` 에 새 항목 없음.

- [ ] **Step 9: HANDOFF + PR (머지는 전체 브랜치 리뷰 뒤)**

PR 을 먼저 만들어 번호를 받는다:

```bash
git push -u origin feat/v0.8.0-step4-itemdetail
gh pr create --base develop --head feat/v0.8.0-step4-itemdetail \
  --title "feat(v0.8.0-s4): ItemDetailPanel 반응형 — ItemDetailLayout + PanelWindow 이관 + 칠한 순서 z-order" \
  --body "스펙 §11 4단계. D3(칠한 순서 = z-order)·D4(헤더 추가 컨트롤)·모달 가드 선행 후 ItemDetailPanel 이관. tests 521 → 535. 인게임 smoke 7항목 PASS.

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

`docs/HANDOFF.md` 4행(진행 상태)의 `4단계 ItemDetailPanel 플랜 작성 예정.` 을 `**4단계 ItemDetailPanel 완료(커밋 당일 날짜, PR #<gh pr create 가 출력한 번호>)** — 플랜 \`docs/superpowers/plans/2026-10-01-responsive-layout-v0.8.0-stage4-itemdetail.md\`. 5단계 PlayerEditorPanel 플랜 작성 예정.` 로 바꾸고, Releases 목록 맨 앞(`- **v0.8.0-s3**` 앞)에:

```markdown
- **v0.8.0-s4** (커밋 당일 날짜) — **ItemDetailPanel 반응형** (PR #<플랜 PR 번호> 플랜, #<구현 PR 번호> 구현 → develop). D3: `PanelWindow.DrawWindow` 가 Repaint 때 `PanelRegistry.NotePainted` 로 칠해진 순번을 기록, `TopmostVisibleAt` = 보이는 창 중 순번 최대 — z-order 를 클릭으로 추측하지 않고 Unity 가 칠한 순서로 관찰(3단계의 `BringToFront` 삭제). `PanelRegistry.IsModalOpen`(컨테이너 삭제 확인창·상세 선택 팝업)이면 리사이즈 시작 금지. D4: `PanelWindow.DrawHeaderExtra`/`HeaderExtraW` + `PanelWindowLogic.DragRect` — 상세 창의 [편집] 토글을 헤더에 유지. `ItemDetailLayout`: 스크롤 = 내용 − 이름 행 − (편집 시 안내문 2행), 값 필드·선택 버튼 = 잔여 폭, 최소 389×244(구 `Init` 의 480×640 강제 bump 삭제, cfg 기본값 480×640 은 그대로). 간격 규칙 "요소 n개 → 간격 n−1개" 원칙화. ItemDetail 등록으로 `IsCoveredByForeign` 에서 ItemDetail·Selector 항목 제거. `SettingsPanel` reset 의 ItemDetail 기본값 380×500 → 480×640. ModWindow 의 ItemDetail per-frame 저장 블록 삭제. tests 521 → 535. smoke 7항목 PASS.
```

```bash
git add docs/HANDOFF.md
git -c core.safecrlf=false commit -m "docs(handoff): v0.8.0 4단계 완료 상태

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push
```

전체 브랜치 리뷰(executing-plans 의 Final Review)를 거친 뒤 `gh pr merge --merge`.

---

## 다음 플랜 (이 문서 범위 밖)

5단계 PlayerEditorPanel: 스펙 §8 대로 착수 전에 섹션 간 공유 상태(입력 버퍼·페이지 인덱스·선택)를 목록화하고 `HeroTagSection`/`KungfuSection`/`SpeAddSection` 으로 분할, `PlayerEditorLayout`(ScrollH, SectionListH, TagPageSize, KungfuPageSize). 등록되면 `IsCoveredByForeign` 에서 PlayerEditor 항목을 지우고, 그 `Selector`/`BreakthroughDialog` 를 `IsModalOpen` 에 추가한다. 3단계 리뷰 이월분 중 `ContainerLayout` 의 좌측 열 4px 오차(간격 규칙 적용)는 사용자 결정 대기.
