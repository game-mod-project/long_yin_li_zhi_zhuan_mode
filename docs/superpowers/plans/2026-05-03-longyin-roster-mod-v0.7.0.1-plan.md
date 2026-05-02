# v0.7.0.1 Hotfix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v0.7.0 출시 후 발견된 3 버그 (천부 휴식 회귀, ContainerPanel 높이 부족, ContainerOps NRE) 를 patch release 로 수정.

**Architecture:** Spec §4 의 fix path 들을 phase 별로 적용. Phase 1 (UI 단순) → Phase 2 (ContainerOps 진단 가드 + DrawToast strip 우회) → Phase 3 (잔존 issue 가설별 fix) → Phase 4 (#1 systematic-debugging) → Phase 5~7 (smoke / release / handoff). #3 의 root cause 1순위는 ContainerPanel.DrawToast 의 `GUILayout.FlexibleSpace()` IL2CPP strip — HANDOFF §4.3 에 명시된 알려진 한계. ContainerOpsHelper 가 `CurrentContainerIndex < 0` 시 toast 호출 → strip 된 method 발화 → IMGUI frame 폐기 = 사용자 보고의 "UI 전부 날라감 → 다시 UI 표시" 패턴.

**Tech Stack:** BepInEx 6 IL2CPP, Unity IMGUI, .NET 6 SDK, xUnit, HarmonyLib, System.Text.Json. 현재 baseline v0.7.0 main HEAD (`e57aa0a`).

---

## File Structure

**Modify:**
- `src/LongYinRoster/UI/ContainerPanel.cs:28` — window height 600 → 760
- `src/LongYinRoster/UI/ContainerPanel.cs:142-162` — DrawLeftColumn ScrollView wrap
- `src/LongYinRoster/UI/ContainerPanel.cs:278-290` — DrawToast FlexibleSpace 제거 + ToastService.Push 로 통합 변경
- `src/LongYinRoster/UI/ContainerPanel.cs:104-125` — Draw outer try/catch (frame 보호 안전장치)
- `src/LongYinRoster/UI/ModWindow.cs:181-226` — DoGameToContainer / DoContainerToGame 진입점 try/catch 보강
- `src/LongYinRoster/Util/KoreanStrings.cs:끝` — 컨테이너 안내 메시지 5건 추가
- `src/LongYinRoster/Core/PinpointPatcher.cs:65,501-614` — RebuildHeroTagData 보강 (Phase 4 결정 후)
- `src/LongYinRoster/Plugin.cs:17` — VERSION 0.7.0 → 0.7.0.1

**Create:**
- `src/LongYinRoster/Core/RestKeepHeroTagPatch.cs` — Phase 4 의 Harmony Postfix on 휴식 method (root cause 식별 후 method name 확정)
- `src/LongYinRoster.Tests/ContainerPanelToastTests.cs` — DrawToast non-throwing test (mock GUILayout 어렵지만 logic 분리 가능한 부분)
- `src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs` — null target / 빈 indices guard
- `dist/release-notes-v0.7.0.1.md` — release notes

**Update:**
- `docs/HANDOFF.md` — §1 헤더 + §6 새 sub-project numbering

---

## Phase 1: ContainerPanel 높이 부족 (#2 UI fix)

### Task 1.1: ContainerPanel window height 600 → 760

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:28`

- [ ] **Step 1: 현재 line 28 확인**

```bash
sed -n '28p' "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/UI/ContainerPanel.cs"
```

Expected output (정확한 매칭):
```
    private Rect _rect = new Rect(150, 100, 800, 600);
```

- [ ] **Step 2: height 600 → 760 변경**

`src/LongYinRoster/UI/ContainerPanel.cs:28` 의 `600` 을 `760` 으로 변경:

```csharp
    private Rect _rect = new Rect(150, 100, 800, 760);
```

- [ ] **Step 3: build 검증**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded` (warnings 0 또는 기존 warnings 만).

### Task 1.2: 좌측 column ScrollView wrap

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:142-162`

- [ ] **Step 1: 현재 DrawLeftColumn 본체 확인**

```bash
sed -n '142,162p' "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/UI/ContainerPanel.cs"
```

Expected: `private void DrawLeftColumn()` 시작, 인벤토리/창고 vertical split.

- [ ] **Step 2: ScrollView field 추가** (line 47 근처 `_conScroll` 옆)

`src/LongYinRoster/UI/ContainerPanel.cs:47` 다음에 추가:

```csharp
    private Vector2 _leftColumnScroll = Vector2.zero;
```

- [ ] **Step 3: DrawLeftColumn 본체를 BeginScrollView 로 wrap**

`src/LongYinRoster/UI/ContainerPanel.cs:142-162` 의 `private void DrawLeftColumn()` 전체를 다음으로 교체:

```csharp
    private void DrawLeftColumn()
    {
        GUILayout.BeginVertical(GUILayout.Width(390));
        // 좌측 column 전체를 ScrollView 로 wrap — 작은 해상도 fallback 안전장치 (헤더 + 카테고리 탭 + 인벤/창고 list + 4 버튼).
        _leftColumnScroll = GUILayout.BeginScrollView(_leftColumnScroll, GUILayout.Height(640));

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

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }
```

- [ ] **Step 4: build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 5: 인게임 manual smoke (#2 § 5.2)**

게임 종료 → build → 게임 재시작 → F11 → 컨테이너 관리(F11+2) → 다음 항목 확인:
- 인벤토리 측 [→이동] [→복사] 버튼 표시
- 창고 측 [→이동] [→복사] 버튼 표시
- 컨테이너 측 [←이동] [←복사] [☓삭제] 표시
- 컨테이너 관리 [신규] [이름변경] [삭제] 표시
- 카테고리 탭 (전체/장비/단약/음식/비급/보물/재료/말) 표시
- 좌측 column 마우스 휠 스크롤 정상

Expected: 모든 버튼 잘림 없이 표시. 1080p 모니터 기준.

- [ ] **Step 6: Commit**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "$(cat <<'EOF'
fix(ui): ContainerPanel height 600→760 + 좌측 column ScrollView (#2)

ContainerPanel 800x600 좌측 column 의 창고 [→이동][→복사] 버튼이
화면 하단으로 잘림 회귀 fix. window height 760 으로 확대 + 좌측
column 전체를 BeginScrollView 로 wrap (작은 해상도 fallback 안전장치).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 2: ContainerOps NRE — DrawToast strip 우회 + callback 가드 (#3)

### Task 2.1: KoreanStrings 에 컨테이너 안내 메시지 추가

**Files:**
- Modify: `src/LongYinRoster/Util/KoreanStrings.cs:끝`

- [ ] **Step 1: 현재 파일 마지막 라인 확인**

```bash
tail -5 "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/src/LongYinRoster/Util/KoreanStrings.cs"
```

Expected: `public const string ApplySectionHeader  = "─── Apply 항목 ───";` + 빈 줄 + `}` 닫는 괄호.

- [ ] **Step 2: 마지막 } 위에 컨테이너 메시지 추가**

`src/LongYinRoster/Util/KoreanStrings.cs:95` (`public const string ApplySectionHeader = "─── Apply 항목 ───";`) 다음 + 닫는 `}` 위에 추가:

```csharp

    // v0.7.0.1 — 컨테이너 안내 메시지
    public const string ToastContainerNotSelected     = "컨테이너를 먼저 [신규] 버튼으로 생성하세요";
    public const string ToastContainerEmptyChecks     = "선택된 항목이 없습니다";
    public const string ToastContainerOpsThrew        = "컨테이너 작업 실패: {0} (BepInEx 로그 확인)";
    public const string ToastContainerNeedGameEnter   = "게임 진입 후 사용 가능합니다";
    public const string ToastContainerCreated         = "신규 컨테이너 #{0} 생성 완료";
```

- [ ] **Step 3: build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Util/KoreanStrings.cs
git commit -m "$(cat <<'EOF'
feat(strings): v0.7.0.1 컨테이너 안내 메시지 5건 추가

ContainerNotSelected / EmptyChecks / OpsThrew / NeedGameEnter /
Created. v0.7.x 후속 sub-project (UX 개선) 에서 ContainerOpsHelper /
ContainerPanel 의 hard-coded 한국어 string 을 교체할 때 사용. v0.7.0.1
에서는 정의만 — string 안전장치 자산.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 2.2: ContainerPanel.DrawToast 의 FlexibleSpace 제거 (root cause fix)

**핵심**: HANDOFF §4.3 명시 — `GUILayout.FlexibleSpace()` 가 IL2CPP IMGUI strip 됨. ContainerPanel.DrawToast 의 line 285, 287 가 매 frame 호출되면 unhandled exception → IMGUI frame 폐기 = 사용자 보고 "UI 전부 날라감 → 다시 표시". ContainerOpsHelper.GameToContainer 가 `CurrentContainerIndex < 0` 시 toast 호출 → strip 발화 → 3초 동안 frame 폐기 반복.

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:278-290` (DrawToast 본체)
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:82-86` (Toast 메서드 — ToastService.Push 로 위임)

- [ ] **Step 1: Toast 메서드를 ToastService.Push 로 위임 변경**

`src/LongYinRoster/UI/ContainerPanel.cs:82-86` 의 `Toast` 메서드를 다음으로 교체:

```csharp
    /// <summary>
    /// 컨테이너 panel 안 toast — global ToastService.Push 로 위임. 자체 DrawToast 의 FlexibleSpace
    /// IL2CPP strip 회피 (v0.7.0.1 fix). _toastMsg / _toastUntil 자체는 legacy 필드로 잠시 유지
    /// (다른 호출자 호환성), 그러나 DrawToast 는 더 이상 그리지 않음.
    /// </summary>
    public void Toast(string msg, float duration = 3.0f)
    {
        ToastService.Push(msg, ToastKind.Info);
    }
```

- [ ] **Step 2: DrawToast 메서드를 no-op 으로 변경**

`src/LongYinRoster/UI/ContainerPanel.cs:278-290` 의 `DrawToast` 본체 전체를 다음으로 교체:

```csharp
    private void DrawToast()
    {
        // v0.7.0.1 fix — IL2CPP IMGUI 가 GUILayout.FlexibleSpace() 를 strip → 매 frame 호출 시
        // unhandled exception → IMGUI frame 폐기 (사용자 보고 "UI 전부 날라감 → 다시 표시").
        // global ToastService 로 통합. 본 method 는 backwards-compat 으로 남겨둠 (no-op).
    }
```

- [ ] **Step 3: Draw 메서드 안 DrawToast 호출은 그대로 둬도 무해 (no-op)**

`src/LongYinRoster/UI/ContainerPanel.cs:122` 의 `DrawToast();` 호출은 그대로 유지. no-op 이므로 영향 없음.

- [ ] **Step 4: build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 5: 단위 테스트 변경 없음 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 기존 테스트 모두 PASS (변경된 method 들 — Toast / DrawToast — 가 unit test 에서 직접 호출되지 않음).

- [ ] **Step 6: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "$(cat <<'EOF'
fix(ui): ContainerPanel DrawToast IL2CPP strip 우회 (#3 root cause)

GUILayout.FlexibleSpace() 가 IL2CPP IMGUI 에서 strip 되어 매 frame
unhandled exception → IMGUI frame 폐기 → 사용자 보고 "UI 전부 날라감
→ 다시 표시" 회귀. Toast 호출은 global ToastService.Push 로 위임,
자체 DrawToast 는 no-op (backwards-compat).

Root cause 진단: ContainerOpsHelper.GameToContainer 가
CurrentContainerIndex < 0 시 toast 호출 → strip 발화 → 3초간 frame
폐기 반복 = 컨테이너 비어있는 상태에서 [→이동] 클릭 시 정확한 증상.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 2.3: ContainerPanel.OnGUI / Draw 외곽 try/catch 가드 (안전장치)

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs:98-125` (OnGUI / Draw)

- [ ] **Step 1: OnGUI 에 try/catch wrap 추가**

`src/LongYinRoster/UI/ContainerPanel.cs:98-102` 의 `OnGUI` 메서드를 다음으로 교체:

```csharp
    public void OnGUI()
    {
        if (!Visible) return;
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
```

- [ ] **Step 2: Draw 메서드 외곽 try/catch 추가**

`src/LongYinRoster/UI/ContainerPanel.cs:104-125` 의 `Draw(int id)` 메서드 본체 전체를 try/catch 로 감싸기:

```csharp
    private void Draw(int id)
    {
        try
        {
            DialogStyle.FillBackground(_rect.width, _rect.height);
            DialogStyle.DrawHeader(_rect.width, "컨테이너 관리");

            // 닫기 버튼 (창 우상단) — 헤더 높이 28 안에 배치
            if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
                Visible = false;

            // content 시작 — 헤더 28 + 여백 4
            GUILayout.Space(DialogStyle.HeaderHeight);
            DrawCategoryTabs();
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
```

- [ ] **Step 3: build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 4: 기존 단위 테스트 PASS 확인**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 기존 테스트 모두 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs
git commit -m "$(cat <<'EOF'
fix(ui): ContainerPanel.OnGUI/Draw 외곽 try/catch 가드 (안전장치)

미래 IL2CPP IMGUI strip 회귀 시 frame 폐기 회피 + 진단 로그 추가.
Logger.Warn 으로 stack trace 출력해 root cause 즉시 식별 가능.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 2.4: ContainerOpsHelper guard test 추가

**Files:**
- Create: `src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs`

- [ ] **Step 1: failing test 작성**

`src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs` 신규 생성:

```csharp
using System.Collections.Generic;
using System.IO;
using LongYinRoster.Containers;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerOpsHelperGuardTests
{
    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lyr-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void GameToContainer_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        // CurrentContainerIndex 초기값 -1 — "미선택"

        var result = helper.GameToContainer(il2List: new object(), indices: new HashSet<int> { 0 }, removeFromGame: false);

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void GameToContainer_EmptyChecks_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        helper.CurrentContainerIndex = repo.CreateNew("test");

        var result = helper.GameToContainer(il2List: new object(), indices: new HashSet<int>(), removeFromGame: false);

        Assert.Equal(0, result.Succeeded);
        Assert.Equal("선택된 항목 없음", result.Reason);
    }

    [Fact]
    public void ContainerToGame_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);

        var result = helper.ContainerToGame(player: new object(), indices: new HashSet<int> { 0 }, removeFromContainer: false);

        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void DeleteFromContainer_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);

        var result = helper.DeleteFromContainer(new HashSet<int> { 0 });

        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void DeleteFromContainer_EmptyChecks_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        helper.CurrentContainerIndex = repo.CreateNew("test");

        var result = helper.DeleteFromContainer(new HashSet<int>());

        Assert.Equal("선택된 항목 없음", result.Reason);
    }

    [Fact]
    public void ContainerRepository_ContainersDir_AutoCreated()
    {
        // ContainerRepository ctor 가 디렉터리 자동 생성하는지 검증
        var parentDir = MakeTempDir();
        var subDir = Path.Combine(parentDir, "Containers-AutoCreate-Test");
        Assert.False(Directory.Exists(subDir));

        var repo = new ContainerRepository(subDir);

        Assert.True(Directory.Exists(subDir));
    }
}
```

- [ ] **Step 2: test 실행 — fail 또는 pass 확인**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet test --filter "FullyQualifiedName~ContainerOpsHelperGuardTests"
```

Expected: 6 tests, all PASS (현재 ContainerOpsHelper / ContainerRepository 가 이미 guard logic 포함하므로 PASS 가 정상 — regression test).

- [ ] **Step 3: 만약 fail 하면 root cause 분석 + ContainerOpsHelper.cs / ContainerRepository.cs guard 보강**

만약 어떤 test 가 fail 한다면 (예: "컨테이너 미선택" 메시지 가 변경된 경우) test 를 현재 동작에 맞춰 수정. 단, regression 임을 명확히.

- [ ] **Step 4: 전체 테스트 실행**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: 모든 테스트 PASS (기존 + 신규 6).

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster.Tests/ContainerOpsHelperGuardTests.cs
git commit -m "$(cat <<'EOF'
test(containers): ContainerOpsHelper guard regression tests (6)

null target / 빈 indices / 폴더 자동 생성 — Container 작업의
방어 코드가 미래 회귀 시 즉시 잡히도록.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 2.5: 인게임 #3 smoke 검증

- [ ] **Step 1: build + 게임 시작**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

게임 시작 → 게임 안 진입 (slot load).

- [ ] **Step 2: 컨테이너 비어있는 상태에서 [→이동] 시나리오**

1. F11 → "컨테이너 관리(F11+2)" 선택
2. ContainerPanel 표시 — 컨테이너 드롭다운 "(미선택)" 확인
3. 인벤토리 측 1+ 항목 체크
4. [→이동] 클릭

Expected:
- IMGUI 멈춤/깜빡임 **없음** ✅
- 우하단에 ToastService 토스트 표시: "컨테이너 미선택" ✅
- BepInEx 로그에 `[toast/Info] 이동: 0개 — 컨테이너 미선택` 류 출력 ✅

- [ ] **Step 3: [신규] → 컨테이너 만들기 시나리오**

1. [신규] 클릭 → 이름 "테스트1" 입력 → [확인]
2. ToastService 토스트 "신규 컨테이너 #1 생성" 또는 비슷
3. 디스크 확인:

```bash
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Containers/"
```

Expected: `container_01.json` 파일 생성됨 ✅.

- [ ] **Step 4: 인벤토리 → 컨테이너 [→이동] 정상 동작 시나리오**

1. 드롭다운에서 "01: 테스트1" 선택
2. 인벤토리 측 2 항목 체크 → [→이동]

Expected:
- 토스트 "이동: 2개" ✅
- 게임 인벤토리에서 해당 2 항목 사라짐 ✅
- 컨테이너 측 list 에 2 항목 표시 ✅
- `container_01.json` 의 items 배열 에 2 entries 포함 ✅

- [ ] **Step 5: 창고 측 [→이동] / 컨테이너 → 인벤토리 [←이동] [←복사] [☓삭제] 동상 검증**

각각 시나리오 1회씩 — 각 동작 후 토스트 + 디스크 + UI 검증.

- [ ] **Step 6: smoke 결과 기록**

`docs/superpowers/dumps/2026-05-03-v0.7.0.1-smoke-issue3.md` 신규 작성:
- 각 시나리오 PASS/FAIL
- BepInEx 로그 발췌
- 잔존 issue 있다면 Phase 3 input 으로

- [ ] **Step 7: smoke PASS 시 다음 phase 로 진행. FAIL 시 Phase 3 진입.**

---

## Phase 3: #3 잔존 issue 가설별 fix

### Task 3.1: BepInEx 로그 stack trace 분석

- [ ] **Step 1: Phase 2 smoke 후 BepInEx 로그 grep**

```bash
grep -nE "ContainerPanel|ContainerOps|ContainerRepository|threw|Exception" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
```

Expected: 잔존 issue 가 있다면 stack trace 표시됨 (Phase 2 의 try/catch 가드가 stack trace 를 잡았으므로).

- [ ] **Step 2: stack trace 별 root cause 매핑**

| Stack trace 패턴 | 가설 | Fix path |
|---|---|---|
| `IL2CPP unstripping failed: GUILayout.XXX` | IMGUI strip 추가 발견 | strip 된 method 호출 제거 |
| `MissingMethodException: ItemType ctor` | IL2CPP wrapper ctor 한계 | spec §6.2 H4 — escalate |
| `NullReferenceException` in `IL2CppListOps.Get` | IL2CPP list reflection 한계 | reflection path 보강 |
| `JsonException` | JSON schema mismatch | schema 검증 |
| `없음` (smoke PASS) | 모든 issue 해결 | Phase 4 진입 |

### Task 3.2: 가설별 분기 fix

각 가설별로 다음 Task 를 진행:

#### 3.2.A — 추가 IMGUI strip 발견 시
- 해당 method 호출 제거 + Logger.Warn 추가
- ContainerPanel / ModeSelector / DialogStyle 어디서든 동일 패턴 적용
- commit: `fix(ui): IL2CPP strip 추가 우회 — <method 명>`

#### 3.2.B — IL2CPP wrapper ctor 한계 발견 시 (H4)
- spec §6.2 H4 — hotfix scope 초과
- v0.7.0.1 release 에서 #3 일부 defer 결정 (예: 이동/복사는 작동하지만 특정 item 종류는 실패)
- HANDOFF / release-notes 에 known limitation 기록
- 별도 spec (`docs/superpowers/specs/2026-XX-XX-il2cpp-wrapper-spike.md`) 으로 v0.7.x 후속에서 재시도

#### 3.2.C — JSON schema mismatch
- ContainerOps.WriteItemAsJson / WriteSubData / WriteObjectRecursive 의 누락 필드 식별
- ItemListApplier.ApplyJsonToObject 의 schema 와 일치시킴
- regression test 추가
- commit: `fix(containers): ItemData JSON schema <필드명> 보강`

### Task 3.3: 잔존 issue 해결 후 #3 smoke 재검증

- [ ] **Step 1: spec § 5.3 의 10 step smoke 다시 실행**

각 step PASS/FAIL 기록 후 잔존 issue 0 확인.

- [ ] **Step 2: smoke 결과 기록**

`docs/superpowers/dumps/2026-05-03-v0.7.0.1-smoke-issue3.md` 갱신.

- [ ] **Step 3: 모든 #3 smoke PASS 시 Phase 4 진입**

---

## Phase 4: #1 천부 휴식 회귀 fix

### Task 4.1: systematic-debugging skill 호출

- [ ] **Step 1: superpowers:systematic-debugging skill 활성화**

본 작업은 root cause investigation 이 핵심. systematic-debugging skill 의 framework 적용:
1. 증상 정확히 정의: "천부 Apply 후 인게임 휴식 1회 → heroTagData 빈 상태로 reset. 다른 카테고리 (스탯/외형/인벤/창고/장비/무공) 정상 유지."
2. 가설 H1/H2/H3 (spec §4.1) 각각 evidence 수집
3. 검증 가능한 실험 설계
4. 실험 실행 → 결과 → 가설 좁힘
5. fix path 결정

### Task 4.2: 휴식 routine method 후보 dump

**Files:**
- Create: `docs/superpowers/dumps/2026-05-03-rest-routine-trace.md`

- [ ] **Step 1: HeroData 의 method 중 "rest" / "sleep" / "day" / "Pass" / "End" 패턴 grep**

게임 안에서 [F12] (`docs/superpowers/dumps/HeroData-method-dump.md` 가 있는지 확인 후) 또는 새로운 dump task 작성:

```bash
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/docs/superpowers/dumps/" | grep -i hero
```

Expected: 기존 dump 가 있으면 그 안에서 후보 추출. 없으면 새 dump task.

- [ ] **Step 2: BepInEx 로그에 LongYin InGame Cheat 의 휴식 관련 patch 흔적 확인**

```bash
grep -niE "rest|sleep|dayPass|huxi" "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log" | head -30
```

Expected: 휴식 관련 method 이름 단서.

- [ ] **Step 3: HeroData / GameDataController / WorldController 등의 method 후보 list 작성**

후보 예 (가능성 높은 순):
- `HeroData.HuXi()` 또는 `Rest()` 또는 `RestOneDay()`
- `GameDataController.PassDay()` / `EndDay()` / `NextDay()`
- `HeroData.RecoverState()` (이미 RefreshSelfState 에서 호출됨 — 가능성 낮음)
- `HeroData.RefreshMaxAttriAndSkill()` (이미 호출됨 — 가능성 낮음)
- `HeroData.OnRestEnd()` 류 callback

dump 파일에 후보 + 추정 시그니처 기록.

### Task 4.3: 가장 유력한 method 에 Harmony Postfix 시도 — heroTagData 보존

**Files:**
- Create: `src/LongYinRoster/Core/RestKeepHeroTagPatch.cs`

- [ ] **Step 1: 가장 유력한 method (예: `HeroData.HuXi`) 에 Harmony Postfix patch**

`src/LongYinRoster/Core/RestKeepHeroTagPatch.cs` 신규 생성. method 이름은 Task 4.2 dump 결과에서 확정:

```csharp
using HarmonyLib;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.0.1 fix — 휴식 (day-pass) routine 이 heroTagData 를 reset 하는 회귀 우회.
///
/// Root cause (Task 4.2 dump 결과): <method-name 채울 것>.
///
/// Strategy: 휴식 method Postfix 에서 player 의 heroTagData snapshot 을 미리 capture
/// 하고, 휴식 method 종료 후 heroTagData 가 비어있으면 snapshot 으로 복원.
/// </summary>
[HarmonyPatch]
public static class RestKeepHeroTagPatch
{
    // TODO Task 4.2 결과로 method name 확정 후 수정:
    // [HarmonyPatch(typeof(HeroData), "HuXi")]
    // [HarmonyPatch(typeof(HeroData), "RestOneDay")]

    private static System.Collections.Generic.List<object>? _snapshot;

    [HarmonyPrefix]
    public static void Prefix(object __instance)
    {
        try
        {
            var ht = __instance.GetType().GetField("heroTagData",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance)
                ?? __instance.GetType().GetProperty("heroTagData",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance);
            if (ht == null) { _snapshot = null; return; }

            int n = IL2CppListOps.Count(ht);
            _snapshot = new System.Collections.Generic.List<object>(n);
            for (int i = 0; i < n; i++)
            {
                var entry = IL2CppListOps.Get(ht, i);
                if (entry != null) _snapshot.Add(entry);
            }
            Logger.Info($"RestKeepHeroTagPatch.Prefix: snapshot {_snapshot.Count} tags");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Prefix threw: {ex.GetType().Name}: {ex.Message}");
            _snapshot = null;
        }
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance)
    {
        try
        {
            if (_snapshot == null) return;
            var ht = __instance.GetType().GetField("heroTagData",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance)
                ?? __instance.GetType().GetProperty("heroTagData",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance);
            if (ht == null) return;

            int curCount = IL2CppListOps.Count(ht);
            if (curCount >= _snapshot.Count) return;  // 이미 충분 — restore 불필요

            // 현재 list 가 snapshot 보다 작으면 → reset 됨. snapshot 으로 복원.
            // IL2CppListOps.Add 패턴 사용
            int restored = 0;
            foreach (var tag in _snapshot)
            {
                try { IL2CppListOps.Add(ht, tag); restored++; } catch { }
            }
            Logger.Info($"RestKeepHeroTagPatch.Postfix: restored {restored} tags after rest reset");
        }
        catch (System.Exception ex)
        {
            Logger.Warn($"RestKeepHeroTagPatch.Postfix threw: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
```

- [ ] **Step 2: Plugin.cs 의 Harmony.PatchAll 에 patch 등록**

`src/LongYinRoster/Plugin.cs:27` 의 `harmony.PatchAll(typeof(InputBlockerPatch));` 다음 라인에 추가:

```csharp
        harmony.PatchAll(typeof(Core.RestKeepHeroTagPatch));
```

- [ ] **Step 3: build 검증**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: `Build succeeded`.

- [ ] **Step 4: 인게임 #1 smoke 검증**

1. 슬롯 1 capture (자기 캐릭터)
2. 슬롯 1 의 천부 카테고리 단독 체크 → Apply
3. 인게임 진입 → 천부 17/17 ✅
4. "휴식" 1회 → 천부 17/17 유지 확인
5. BepInEx 로그 확인:
   ```bash
   grep "RestKeepHeroTagPatch" "E:/Games/.../BepInEx/LogOutput.log"
   ```
   Expected: `Prefix: snapshot N tags` + `Postfix: restored N tags after rest reset` 출력

- [ ] **Step 5: smoke FAIL 시 method name 후보 다음 옵션으로 재시도**

Task 4.2 dump 의 후보 list 를 순회. RestKeepHeroTagPatch 의 `[HarmonyPatch]` attribute 만 변경 + rebuild + 인게임 재검증.

- [ ] **Step 6: 모든 후보 시도 후 FAIL 시 — F2 fallback (heroTagData JSON schema 보강)**

PinpointPatcher.RebuildHeroTagData (PinpointPatcher.cs:501-614) 의 AddTag 인자 검토:
- `lv` 가 1.0 default — 게임 lookup 결과와 다를 수 있음
- `isHidden` 가 false default — 일부 천부는 hidden=true 일 수 있음

각 천부 ID 별 AddTag 후 game-self refresh method 재호출:

```csharp
// RebuildHeroTagData 끝 (line 613) 에 추가
try
{
    InvokeMethod(player, "RefreshHeroTagData", System.Array.Empty<object>());
}
catch { /* ignore */ }
```

- [ ] **Step 7: 모든 시도 후도 FAIL 시 — F3 fallback (mirror field) 또는 known limitation**

다음 옵션:
1. `heroTagPoint` (int, FieldCategory.TalentPoint) 도 동시 inject — 이미 SimpleFieldMatrix 에 있음, 검증
2. `heroTagDataReadOnly` 같은 mirror field 가 dump 에 있는지 확인
3. 모두 실패 시 — known limitation 으로 HANDOFF / release-notes 에 기록 + #1 fix defer

### Task 4.4: #1 fix 결정 + commit

- [ ] **Step 1: Task 4.3 결과별 commit**

#### 4.4.A — Postfix patch 성공 시:
```bash
git add src/LongYinRoster/Core/RestKeepHeroTagPatch.cs src/LongYinRoster/Plugin.cs
git commit -m "$(cat <<'EOF'
fix(core): 휴식 (day-pass) 후 heroTagData reset 회귀 우회 (#1)

Harmony Prefix/Postfix patch on <method-name>: 휴식 진입 직전 heroTagData
snapshot, 종료 후 reset 되었으면 복원. mod 의 천부 inject 가 휴식
routine 으로부터 생존.

Root cause: <method-name> 이 day-pass 종료 시 heroTagData 를 IL2CPP
list clear 함. user-injected tag 도 무차별 reset.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

#### 4.4.B — F2 (schema 보강) 성공 시:
```bash
git add src/LongYinRoster/Core/PinpointPatcher.cs
git commit -m "$(cat <<'EOF'
fix(core): RebuildHeroTagData — RefreshHeroTagData 후호출 (#1)

휴식 후 천부 reset 회귀 fix. AddTag 후 game-self refresh method 호출로
internal mirror field 동기화. (또는 lv/isHidden 인자 정정 — 작성 시점)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

#### 4.4.C — 모든 시도 실패 시 — known limitation:
```bash
# code 변경 없음. HANDOFF.md / release-notes 에만 기록.
```

---

## Phase 5: Smoke test (spec §5.1 / §5.2 / §5.3)

### Task 5.1: #1 smoke (천부 휴식 회귀)

- [ ] **Step 1**: 슬롯 1 capture (자기 캐릭터, 천부 포함)
- [ ] **Step 2**: 슬롯 1 의 천부 카테고리 단독 체크 → Apply
- [ ] **Step 3**: 인게임 → 캐릭터 정보 → 천부 17/17 ✅
- [ ] **Step 4**: 휴식 1회 → 천부 17/17 유지 ✅
- [ ] **Step 5**: 휴식 5회 연속 → 천부 17/17 유지 ✅
- [ ] **Step 6**: save → 게임 종료 → 재시작 → 천부 17/17 유지 ✅
- [ ] **Step 7**: 다른 카테고리 (스탯/외형/인벤/창고/장비/무공) 회귀 없음 확인 ✅
- [ ] **Step 8**: 결과 기록 → `docs/superpowers/dumps/2026-05-03-v0.7.0.1-smoke.md` 의 § 5.1

### Task 5.2: #2 smoke (ContainerPanel 높이)

- [ ] **Step 1**: F11+2 → ContainerPanel 표시 (1080p 모니터)
- [ ] **Step 2**: 모든 버튼 표시 확인 ✅:
  - 인벤토리 측 [→이동][→복사]
  - 창고 측 [→이동][→복사]
  - 컨테이너 측 [←이동][←복사][☓삭제]
  - 컨테이너 관리 [신규][이름변경][삭제]
  - 카테고리 탭 8개
- [ ] **Step 3**: 좌측 column 마우스 휠 스크롤 정상 ✅
- [ ] **Step 4**: 결과 기록 → `docs/superpowers/dumps/2026-05-03-v0.7.0.1-smoke.md` 의 § 5.2

### Task 5.3: #3 smoke (ContainerOps 동작)

- [ ] **Step 1**: 빈 컨테이너 상태에서 [→이동] → 안내 토스트 + IMGUI 정상 ✅
- [ ] **Step 2**: [신규] → "테스트1" → container_01.json 디스크 생성 ✅
- [ ] **Step 3**: Containers/ 폴더 사전 부재 시 자동 생성 ✅ (Task 2.4 의 6번째 unit test 가 자동 검증)
- [ ] **Step 4**: 인벤토리 → 컨테이너 [→이동] 정상 ✅
- [ ] **Step 5**: 창고 → 컨테이너 [→이동] 정상 ✅
- [ ] **Step 6**: 컨테이너 → 인벤토리 [←이동] / [←복사] 정상 ✅
- [ ] **Step 7**: 컨테이너 [☓삭제] 정상 ✅
- [ ] **Step 8**: [이름변경] / [삭제] 정상 ✅
- [ ] **Step 9**: smoke 결과 기록

### Task 5.4: smoke 결과 git commit

- [ ] **Step 1: smoke 문서 commit**

```bash
git add docs/superpowers/dumps/2026-05-03-v0.7.0.1-smoke.md
git commit -m "$(cat <<'EOF'
docs: v0.7.0.1 smoke 결과 (3 카테고리 PASS)

§5.1 #1 천부 휴식 회귀 / §5.2 #2 ContainerPanel 높이 / §5.3 #3
ContainerOps NRE — spec 검증 기준 모두 만족.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 6: Release packaging

### Task 6.1: VERSION bump

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs:17`

- [ ] **Step 1: VERSION 0.7.0 → 0.7.0.1**

`src/LongYinRoster/Plugin.cs:17`:

```csharp
    public const string VERSION = "0.7.0.1";
```

- [ ] **Step 2: build + 테스트**

```bash
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
DOTNET_CLI_UI_LANGUAGE=en dotnet test
```

Expected: build PASS, all tests PASS.

### Task 6.2: CHANGELOG / release notes 작성

**Files:**
- Create: `dist/release-notes-v0.7.0.1.md`

- [ ] **Step 1: release notes 작성**

`dist/release-notes-v0.7.0.1.md` 신규 생성:

```markdown
# v0.7.0.1 — Hotfix Release

**작성일**: 2026-05-03
**Baseline**: v0.7.0 (`e57aa0a`)

## 버그 수정

### 1. 천부 Apply 후 휴식 1회 → 천부 사라짐 회귀
인게임 "휴식" 커맨드가 day-pass routine 에서 heroTagData 를 reset 하던
것을 Harmony Prefix/Postfix patch 로 보존.

(또는 — F2 path 적용 시) RebuildHeroTagData 의 game-self refresh method
후호출 추가.

### 2. ContainerPanel 800x600 좌측 column 의 창고 측 버튼 잘림
window height 600 → 760 으로 확대 + 좌측 column 전체 ScrollView wrap
(작은 해상도 fallback 안전장치).

### 3. 인벤토리 → 컨테이너 [→이동]/[→복사] 시 IMGUI 1프레임 깜빡임 + 무동작
ContainerPanel.DrawToast 의 GUILayout.FlexibleSpace() 가 IL2CPP IMGUI
strip 되어 unhandled exception → IMGUI frame 폐기. global ToastService
로 toast 통합 + ContainerPanel.OnGUI/Draw 외곽 try/catch 가드 추가.

## 검증
- 40+ unit tests PASS (Container guard regression test 6 신규)
- 인게임 smoke (§ 5.1 / 5.2 / 5.3) 모두 PASS

## 알려진 한계
v0.7.0 에서 documented 된 known limitation 그대로 유지. 추가 limitation
없음.

(만약 #1 known limitation 으로 defer 된 경우만 — release 결정 시 작성)
```

- [ ] **Step 2: commit (Phase 6 마지막 task 와 함께)**

### Task 6.3: dist/ zip 생성

- [ ] **Step 1: 디렉터리 구조 준비**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
mkdir -p dist/LongYinRoster_v0.7.0.1/BepInEx/plugins/LongYinRoster
cp "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/LongYinRoster.dll" \
   dist/LongYinRoster_v0.7.0.1/BepInEx/plugins/LongYinRoster/
```

- [ ] **Step 2: README + release-notes 포함**

```bash
cp README.md dist/LongYinRoster_v0.7.0.1/
cp dist/release-notes-v0.7.0.1.md dist/LongYinRoster_v0.7.0.1/
```

- [ ] **Step 3: zip 압축**

```powershell
Compress-Archive -Path "dist/LongYinRoster_v0.7.0.1/*" -DestinationPath "dist/LongYinRoster_v0.7.0.1.zip" -Force
```

Expected: `dist/LongYinRoster_v0.7.0.1.zip` 생성.

### Task 6.4: GitHub release tag 생성

- [ ] **Step 1: VERSION + release notes commit**

```bash
git add src/LongYinRoster/Plugin.cs dist/release-notes-v0.7.0.1.md
git commit -m "$(cat <<'EOF'
chore(release): v0.7.0.1 — VERSION bump + release notes

3 버그 fix (천부 휴식 회귀 / ContainerPanel 높이 / ContainerOps NRE).
40+ unit tests PASS, 인게임 smoke 3 카테고리 PASS.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 2: tag 생성 + push**

**사용자 확인 필요** — tag 생성 + push 는 publishing 작업. 사용자에게 진행 확인 후:

```bash
git tag -a v0.7.0.1 -m "v0.7.0.1 hotfix"
git push origin main
git push origin v0.7.0.1
```

- [ ] **Step 3: GitHub release 생성 (gh CLI)**

**사용자 확인 필요** — release publishing.

```bash
gh release create v0.7.0.1 \
  dist/LongYinRoster_v0.7.0.1.zip \
  --title "v0.7.0.1 — Hotfix" \
  --notes-file dist/release-notes-v0.7.0.1.md
```

Expected: GitHub release URL 출력.

---

## Phase 7: HANDOFF.md 갱신

### Task 7.1: HANDOFF §1 헤더 + §6 sub-project numbering

**Files:**
- Modify: `docs/HANDOFF.md` (다양한 라인)

- [ ] **Step 1: §1 (line 3-29) 헤더 갱신**

`docs/HANDOFF.md:3-4` 의 일시 + 진행 상태 갱신:

```markdown
**일시 중지**: 2026-05-03
**진행 상태**: **v0.7.0.1 hotfix release** — 천부 휴식 회귀 / ContainerPanel 높이 / ContainerOps NRE 3 버그 수정. 다음 sub-project 는 사용자 새 우선순위에 따라 v0.7.1 (컨테이너 UX 개선) 부터 시작.
```

- [ ] **Step 2: §1 의 Releases list 에 v0.7.0.1 항목 추가** (`docs/HANDOFF.md:23` 다음)

```markdown
- [v0.7.0.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.0.1) — Hotfix: 천부 휴식 회귀 / ContainerPanel 높이 / ContainerOps NRE
```

- [ ] **Step 3: §1 의 후속 sub-project numbering 갱신** (`docs/HANDOFF.md:38-46` 영역)

기존 v0.7.1=NPC, v0.7.2=Slot diff... 를 사용자 새 우선순위로 교체:

```markdown
**다음 세션 후속 sub-project** (모두 v0.7.0 의 ModeSelector 진입점 활용):
- v0.7.1: 컨테이너 UX 개선 (Item 상세 / 아이콘 그리드 / 검색·정렬 / 가상화)
- v0.7.2: 설정 panel (hotkey 변경 / 컨테이너 정원 / 창 크기 조정)
- v0.7.3: Apply 부분 미리보기 (선택 카테고리 전후 비교)
- v0.7.4: Slot diff preview (Apply 전 변경될 필드 시각화)
- v0.7.5: NPC 지원 (heroID≠0 캐릭터 capture/apply)
```

- [ ] **Step 4: §1 의 v0.7.0 Known Limitations 항목에 #1 known limitation 이 있다면 추가** (Phase 4 결과에 따라)

만약 Phase 4 가 known limitation 으로 defer 됐으면 (Task 4.4.C 시나리오):

```markdown
## v0.7.0.1 Known Limitations
- (v0.7.0 항목 그대로 유지)
- **휴식 후 천부 reset** (Phase 4 모든 시도 실패 — game-internal day-pass routine 이 inject 결과 reset). 사용자 워크어라운드: 휴식 직후 다시 Apply.
```

- [ ] **Step 5: §1 의 baseline reference + project root 갱신**

`docs/HANDOFF.md:6` 부근:

```markdown
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`
```

(이미 정확하면 변경 없음)

### Task 7.2: HANDOFF commit

- [ ] **Step 1: commit**

```bash
git add docs/HANDOFF.md
git commit -m "$(cat <<'EOF'
docs: HANDOFF v0.7.0.1 hotfix release 반영 + 새 sub-project numbering

§1 헤더 갱신 / Releases list 에 v0.7.0.1 추가 / 후속 sub-project
numbering 사용자 새 우선순위로 (v0.7.1=컨테이너 UX → v0.7.5=NPC).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 2: 사용자에게 push 확인 후 push**

```bash
git push origin main
```

---

## 완료 체크리스트

- [ ] Phase 1: ContainerPanel 높이 fix + 인게임 검증 + commit
- [ ] Phase 2: DrawToast strip 우회 + Draw outer try/catch + KoreanStrings + guard tests + 인게임 smoke + commit
- [ ] Phase 3: 잔존 issue 가설별 fix (필요 시)
- [ ] Phase 4: 천부 휴식 회귀 root cause 식별 + Harmony patch 또는 schema 보강 또는 known limitation
- [ ] Phase 5: § 5.1 / § 5.2 / § 5.3 smoke 모두 PASS + commit
- [ ] Phase 6: VERSION bump + release notes + dist zip + tag + GitHub release
- [ ] Phase 7: HANDOFF 갱신 + commit + push

**다음 cycle**: v0.7.1 (컨테이너 UX 개선) brainstorm 진입 — 사용자 새 우선순위 1번 항목.
