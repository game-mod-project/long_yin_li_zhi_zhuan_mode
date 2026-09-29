# LongYin Roster Mod v0.8.0 — 반응형 패널 레이아웃 (Responsive Panel Layout) 설계

날짜: 2026-09-29
sub-project: v0.8.0 (전역 UI 배율·다이얼로그·기능/UX 개선 목록은 별도 후속)
baseline: v0.7.13.1 (427 tests PASS, `develop` = da919f5)
브레인스토밍 결정: 반응형 = 유동 배치(reflow) / 대상 = 주 패널 5 + SettingsPanel / 코너 드래그 전 패널 + 설정 자동 저장 / 접근 = 레이아웃 계산기 분리 / 페이지 크기 자동(10·15·20 버튼 제거)

---

## 1. 한 줄 요약

설정이나 코너 드래그로 패널 폭·높이를 바꾸면 **내부 컨트롤이 공간을 따라 재배치**되도록, (a) 여섯 패널이 복붙해 둔 창 틀을 `PanelWindow` 하나로 모으고, (b) "창 크기 → 각 영역 치수"를 계산하는 **순수 계산기**(`LayoutMath` + 패널별 `*Layout`)를 두어 `Draw` 는 숫자만 받아 그리게 한다. 계산기는 Unity 의존이 없어 기존 테스트 체계(소스 링크 + `UnityStubs`)로 단위 테스트한다.

**핵심 통찰**: 이 코드베이스는 이미 "IMGUI 호출은 smoke, 로직은 단위 테스트" 로 나뉘어 있다. 레이아웃 수치를 계산기로 뽑아내면 그 경계가 그대로 적용되고, IL2CPP 가 `FlexibleSpace`/`GetLastRect` 를 strip 했던 전례(v0.7.0.1, v0.7.3)도 피해 간다 — 숫자를 직접 넘기므로 IMGUI 내부 계산에 기대지 않는다.

## 2. 목표 / 비목표

**목표**:
- 주 패널 6개(ModWindow / ContainerPanel / PlayerEditorPanel / ItemGeneratorPanel / ItemDetailPanel / SettingsPanel)에서 창 크기 변경 시 열 폭·리스트 높이·페이지당 행 수가 따라 조정
- 코너 드래그 리사이즈를 6개 패널 전부에 제공, 결과를 설정에 자동 저장. 설정 패널의 X/Y/W/H 필드는 정밀 입력·초기화용으로 유지
- 최소 크기는 내용에서 계산(고정 부분 합 + 주 리스트 3행), 최대 = 화면 크기. 창이 화면 밖으로 나가지 않음
- SettingsPanel 에 크기 설정 신설(`SettingsPanelX/Y/W/H`)
- 리팩토링은 위 작업에 필요한 만큼만: `PanelWindow` 추출, `PanelRegistry` 추출, PlayerEditorPanel 섹션 분할

**비목표**:
- 전역 UI 배율(글자 포함 zoom) — 설정 항목 하나짜리 후속 (`GUI.matrix`)
- 다이얼로그 6종(Selector / SkillBreakthrough / Confirm / Input / FilePicker / ModeSelector) — 고정 크기 유지
- 게임 sprite 도입, 기능·UX 개선 목록(별도 브레인스토밍)
- 게임 조작 코드(Applier / Reflector / Patch) 변경 — 한 줄도 건드리지 않음
- 새 IMGUI API 도입

## 3. 아키텍처

```
ModWindow (MonoBehaviour)
  └─ PanelRegistry ── 패널 5개 생성 · Hydrate(설정→rect) · Persist(rect→설정)     [신규, ModWindow 에서 추출]
        ├─ ContainerPanel ──┐
        ├─ PlayerEditorPanel│   각 패널 = PanelWindow(창 틀) + *Layout(계산기) + Draw(내용)
        ├─ ItemGeneratorPanel│
        ├─ ItemDetailPanel  │
        └─ SettingsPanel ───┘
  └─ (ModWindow 자신도 PanelWindow 사용)

UI/PanelWindow.cs          얇은 IMGUI: GUI.Window · 배경/헤더/X · DragWindow · 코너 핸들 이벤트
UI/Layout/PanelWindowLogic  순수: 리사이즈 수식 · 화면 클램프 · 내용 rect
UI/Layout/LayoutMath        순수: RowsThatFit · SplitHeight · SplitWidth · Wrap · Clamp
UI/Layout/*Layout           순수: 패널별 계산기 6개 (입력 = 내용 rect + 패널 상태, 출력 = 불변 치수)
```

**데이터 흐름 (매 프레임)**: `PanelWindow.OnGUI` → `GUI.Window(rect)` → 배경·헤더·X → `content = PanelWindowLogic.ContentRect(rect)` → 패널의 `DrawContent(content)` → `var L = XxxLayout.Compute(content, state)` → `GUILayout.*` 에 `L.*` 수치 전달 → 코너 핸들 → `DragWindow`.

**데이터 흐름 (크기 변경)**: 코너 MouseDrag → `PanelWindowLogic.Resize(...)` (경계·화면 클램프) → `rect` 갱신 → MouseUp 에 `Persist()` → `ConfigEntry` 4개 기록(BepInEx 자동 파일 저장). 설정 패널의 숫자 입력 → `SetRect` → 같은 `Persist()`. 두 경로가 같은 값을 읽고 쓴다.

## 4. `PanelWindow` 상세

```csharp
public readonly record struct PanelBounds(float MinW, float MinH);   // 최대는 화면 크기

public sealed class RectBinding                                        // ConfigEntry<float> X, Y, W, H
{
    public static RectBinding Of(ConfigEntry<float> x, ConfigEntry<float> y, ConfigEntry<float> w, ConfigEntry<float> h);
}

public sealed class PanelWindow
{
    public PanelWindow(int windowId, string title, Func<PanelBounds> bounds, RectBinding binding);
    public Rect Rect { get; }            // 창 rect (화면 좌표)
    public Rect ContentRect { get; }     // 헤더·여백 제외, 창 로컬 좌표 — 계산기 입력
    public bool Visible { get; set; }
    public void Hydrate(float screenW, float screenH);      // 설정 → rect, 클램프 적용
    public void SetRect(float x, float y, float w, float h); // 설정 패널 경로, 클램프 적용
    public void OnGUI(Action<Rect> drawContent);            // 아래 순서 고정
    public void Persist();                                   // rect → 설정 4개
}
```

- `bounds` 가 `Func` 인 이유: 최소 크기가 패널 상태(섹션 접힘 등)에 따라 달라지므로 계산기가 매번 낸다.
- `OnGUI` 순서: `FillBackground` → `DrawHeader(title)` → X 버튼(`Visible=false` + `Persist`) → `drawContent(ContentRect)` → 코너 핸들(DragWindow 보다 먼저 — 코너 우선) → `GUI.DragWindow(헤더 영역)`.
- 예외 가드: `drawContent` 를 try/catch 로 감싸 `Logger.WarnOnce("<title>", …)` — 지금 패널마다 있는 동일 패턴을 한 곳으로.
- 저장 시점: 리사이즈 MouseUp / 드래그 종료(MouseUp, 또는 rect 변화가 멈춘 다음 호출 — 게임 창 밖에서 마우스를 놓아 MouseUp 을 못 본 경우) / 닫힘(X, 단축키) / `PanelRegistry.PersistAll` (게임 종료 경로 보존).
- 화면 클램프: `Hydrate`·`SetRect`·리사이즈·드래그 후 `PanelWindowLogic.ClampToScreen`. `Screen.width/height` 는 `PanelWindow` 가 읽어 로직에 숫자로 넘긴다(테스트 가능).

```csharp
public static class PanelWindowLogic   // UI/Layout — Unity 의존 없음 (stub Rect/Vector2 로 테스트)
{
    public static Rect ContentRect(Rect window, float headerH, float padding);
    public static Rect Resize(Rect r, Vector2 startMouse, Vector2 startSize, Vector2 mouse, PanelBounds b, float screenW, float screenH);
    public static Rect ClampToScreen(Rect r, float screenW, float screenH, PanelBounds b);
}
```

`ClampToScreen` 규칙: 폭·높이는 `[Min, screen]` 로 자르고, 위치는 창 전체가 화면 안에 들어오도록 이동(화면이 최소 크기보다 작은 극단 상황에서는 좌상단 0,0 에 맞춤).

## 5. `LayoutMath` 상세

```csharp
public static class LayoutMath   // UI/Layout — Unity 의존 없음
{
    // 높이 → 행 수. min 이하로는 내려가지 않음 (페이지 크기 하한)
    public static int RowsThatFit(float availableH, float rowH, float spacing, int min = 1);

    // 컨테이너 인벤/창고 분배의 일반형. 접힌 섹션은 overhead 만 차지, 나머지를 weight 비율로.
    public static float[] SplitHeight(float totalH, float[] overheads, float[] weights, bool[] collapsed, float gap);

    // 열 폭 가중치 분배. mins 를 먼저 보장하고 잔여를 weight 비율로.
    public static float[] SplitWidth(float totalW, float[] weights, float gap, float[] mins);

    // 셀 폭 기준 한 줄에 들어가는 개수 (탭·버튼 줄바꿈). 최소 1.
    public static int Wrap(float totalW, float cellW, float gap);

    public static Rect Clamp(Rect r, PanelBounds min, float maxW, float maxH);
}
```

행 높이·간격 상수는 `DialogStyle` 에 모은다(기존 `HeaderHeight = 28` 옆): `RowHeight = 24`, `ButtonRowHeight = 28`, `Padding = 12`, `Gap = 4`. `Padding` 은 GUI.Window 기본 skin 의 내부 여백을 흡수하는 값이라 인게임 smoke(최소 크기에서 가로 잘림·스크롤바 없음)로 확정한다 — 어긋나면 이 상수 하나만 올린다. 계산기와 `Draw` 가 같은 상수를 쓰는 것이 "계산 ≠ 렌더" 어긋남을 막는 1차 방어선이다.

## 6. 패널별 계산기 규칙

원칙: **고정** = 행 높이·헤더·툴바·버튼 줄·짧은 라벨 폭 / **가로 확장** = 텍스트 필드·검색창·리스트 행·가중치 열 / **세로 확장** = 패널당 주 리스트 하나(또는 펼쳐진 섹션)가 남는 높이를 전부 차지, 페이지당 행 수는 그 높이에서 계산.

| 패널 | 계산기 입력 (내용 rect + 상태) | 출력 | 세로 확장 영역 | 가로 규칙 | 대체되는 상수 |
|---|---|---|---|---|---|
| ContainerPanel | invCollapsed, stoCollapsed, splitPreset | LeftW, RightW, ToolbarH, InvListH, StoListH, ContainerListH | 인벤·창고·컨테이너 리스트 | 좌/우 열 `SplitWidth`, 검색창 확장 | `TOTAL_H = 640`, `MIN_W/H`, `MAX_W/H` |
| ItemGeneratorPanel | categoryTabCount, subTabCount | TabRows, ListH, **PageSize** | 결과 리스트 | 탭 `Wrap` | `Height(260)`, `_pageSize`, 10/15/20 버튼 |
| PlayerEditorPanel | activeTab, expandedSection | ScrollH, SectionListH, **TagPageSize**, **KungfuPageSize** | 펼쳐진 섹션(천부/무공) — 여러 섹션 동시 펼침은 바깥 스크롤 | 드롭다운·입력 확장, 속성 탭 열 `SplitWidth` | `_rect.height - 140`, `PAGE_SIZE = 10` |
| ItemDetailPanel | editMode | HeaderBlockH, ScrollH | raw/curated 스크롤 | 라벨 고정, 값 필드 확장 | `_rect.height - 140` |
| ModWindow | — | ListW, DetailW, ListH | 슬롯 목록 스크롤 | 목록/상세 `SplitWidth` | 암묵 고정 열 폭 |
| SettingsPanel | — | ScrollH | 설정 스크롤 | 라벨 고정, 입력 확장 | `_rect.height - 110`, 고정 480×600 |

각 계산기는 `MinSize` 도 낸다: 고정 부분 합 + 주 리스트 `3 × RowHeight`. `PanelWindow` 의 `bounds` 는 이 값을 참조한다. ContainerPanel 의 기존 600×400 은 계산값과 같으면 유지, 다르면 계산값을 따른다(상수 삭제).

페이지 기반 목록(생성기, 천부, 무공)은 `PageSize` 가 바뀌면 현재 페이지 인덱스를 `min(page, totalPages-1)` 로 보정한다(창을 키우면 페이지 수가 줄어듦).

## 7. 설정

- 신설: `[UI] SettingsPanelX / SettingsPanelY / SettingsPanelW / SettingsPanelH` (기본 200 / 120 / 480 / 600).
- 유지: 기존 5 패널의 X/Y/W(Width)/H(Height) 키 전부 그대로(이름 불변, 구 cfg 호환).
- 로드: `Hydrate` 에서 화면 클램프 — 다른 해상도에서 저장된 값 대응.
- 삭제 없음: `표시 10/15/20` 은 설정 키가 없었음.
- SettingsPanel 의 rect 입력 필드 목록에 자기 자신(SettingsPanel) 추가. "기본값 복원" 은 편집 가능한 버퍼(단축키 + 컨테이너 rect + 설정 rect)만, "영속화 정보 reset" 이 6 패널 전부의 rect 를 기본값으로 즉시 되돌린다(v0.7.6 부터의 두 버튼 역할 유지).

## 8. 리팩토링 범위 (옮기는 김에만)

- `UI/Layout/` 신설: `LayoutMath`, `PanelWindowLogic`, `PanelBounds`, 계산기 6개. 테스트 프로젝트에 소스 링크 추가.
- `PanelWindow` 는 ContainerPanel 의 `DrawResizeHandle` + 각 패널의 배경/헤더/X/DragWindow 블록을 대체. 패널 클래스는 `sealed` 그대로, 합성으로 사용.
- `PanelRegistry`(신규): ModWindow 의 패널 생성·`SetRect(Config…)` 주입(36곳)·닫힘 시 저장 블록(20줄)을 이관. ModWindow 는 등록·단축키·모드 전환만 남김.
- PlayerEditorPanel 분할(5단계): `PlayerEditorPanel`(호스트: 탭·바깥 스크롤·계산기 호출) + `HeroTagSection` + `KungfuSection` + `SpeAddSection`. 분할 전에 섹션 간 공유 상태(입력 버퍼·페이지 인덱스·선택)를 목록화하고, 공유하는 것은 호스트가 소유해 섹션에 넘긴다.
- 그 외 파일 분할 없음.

## 9. 에러 처리 / IL2CPP 제약

- `drawContent` 예외는 `PanelWindow` 가 잡아 `WarnOnce` — 한 패널의 예외가 다른 패널 렌더를 막지 않음(현재와 동일 수준, 위치만 통합).
- 계산기는 입력이 비정상(음수·0 폭)이어도 예외 없이 최소값을 낸다 — 게임 초기 프레임에 `Screen` 이 0 인 경우 대비.
- IMGUI strip 회피: `GUILayout.FlexibleSpace`, `GUILayoutUtility.GetLastRect` 사용 금지 유지. 새로 쓰는 API 없음(`GUI.Window`, `GUI.DragWindow`, `GUILayout.Width/Height`, `GUI.DrawTexture` 는 전부 기존 사용 중).
- `GUI.Window` 가 돌려주는 rect 는 `PanelWindow` 만 받는다 — 드래그로 바뀐 위치를 패널 코드가 따로 들고 있다가 덮어쓰는 이중 소유를 없앤다.

## 10. 테스트 전략

- **`LayoutMath`**: 함수 5개 × 경계값(0·음수·정확히 한 행·큰 값). `SplitHeight` 는 컨테이너의 접힘 4조합 × 프리셋.
- **`PanelWindowLogic`**: 리사이즈가 `[Min, screen]` 안에 머묾 / 화면 밖 rect 가 안으로 들어옴 / 화면 < 최소 극단 / `ContentRect` 가 헤더·여백을 정확히 뺌.
- **계산기 6개**: 크기 3종(최소·보통·큼)에 대해 (a) 부분 합 ≤ 전체, (b) 커지면 행 수 단조 증가, (c) 최소 크기에서 고정 부분이 모두 들어감, (d) `MinSize` 가 (c) 를 만족하는 최소값.
- **페이지 보정**: `PageSize` 변화 시 페이지 인덱스 클램프.
- 기존 427 tests 는 그대로 green.
- **인게임 smoke (단계마다 동일 체크리스트)**: 최소·최대·중간 크기 / 화면 밖으로 끌기 / 재시작 후 크기·위치 복원 / 설정 패널 숫자 입력 ↔ 드래그 일치 / 그 패널 고유 동작 회귀 없음(컨테이너 이동·복사·Undo, 생성기 생성, 편집 Apply, 슬롯 캡처·Apply).

## 11. 구현 단계 (plan 에서 상세화) — 단계마다 PR 하나 → `develop`

1. **`PanelWindow` + `LayoutMath` + `PanelWindowLogic` + SettingsPanel** — 창 틀·수식·클램프를 가장 작은 패널에서 검증. `SettingsPanelX/Y/W/H` 신설. ModWindow 의 SettingsPanel 주입/저장을 `PanelRegistry` 첫 항목으로.
2. **ItemGeneratorPanel** — `ItemGenLayout`, 자동 `PageSize`, 10/15/20 버튼 제거, 탭 `Wrap`.
3. **ContainerPanel** — 기존 리사이즈 코드 삭제 → `PanelWindow`, `TOTAL_H` → `ContainerLayout`, 열 폭 `SplitWidth`.
4. **ItemDetailPanel** — `ItemDetailLayout`.
5. **PlayerEditorPanel** — 상태 목록화 → 섹션 3개 분할 → `PlayerEditorLayout` + 섹션별 페이지 자동.
6. **ModWindow** — 본체 창을 `PanelWindow` 로, `ModWindowLayout`, `PanelRegistry` 완성(모든 주입/저장 코드 이관).

버전: `Plugin.VERSION` 은 6단계 완료 시 **0.8.0** (중간 단계는 develop 에만 머지, 릴리스 없음). 브랜치: 스펙·플랜은 `feat/v0.8.0-responsive-layout` 에, 단계 PR 은 `feat/v0.8.0-step<N>-<panel>` 브랜치로 develop 에 올린다(각 PR 이 독립 배포 가능 단위).

## 12. 위험 / 완화

| 위험 | 완화 |
|---|---|
| 계산기 수치와 IMGUI 실제 렌더 높이 불일치 → 스크롤바/여백 | 행 높이·간격 상수를 `DialogStyle` 단일 출처로; smoke 체크리스트에 "최소 크기에서 스크롤바 없음" 명시 |
| PlayerEditor 분할 중 섹션 간 상태 얽힘 | 5단계 착수 전 상태 목록화(8절), 공유 상태는 호스트 소유 |
| `GUI.Window` rect 이중 소유(드래그 위치 덮어쓰기) | rect 소유자를 `PanelWindow` 로 단일화(9절) |
| 최소 크기가 커져 저해상도에서 창이 화면을 넘침 | `ClampToScreen` 이 최소 > 화면 극단을 처리; 최소는 3행 기준으로 보수적 |
| 자동 페이지 크기로 UX 변화(사용자가 10/15/20 에 익숙) | 브레인스토밍 결정(자동, 버튼 제거). 필요 시 후속에서 "자동/고정" 토글 |
| 단계 PR 사이에 패널마다 창 틀이 다른 과도기 | 각 PR 이 한 패널을 완결; 과도기는 시각 차이만 있고 기능 회귀 없음 |
