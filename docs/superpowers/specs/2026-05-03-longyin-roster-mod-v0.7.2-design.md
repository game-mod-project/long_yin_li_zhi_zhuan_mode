# v0.7.2 — 컨테이너 검색·정렬 (D-3)

**작성일**: 2026-05-03
**Sub-project 위치**: v0.7.1 컨테이너 UX 1차 후속의 D 계열 첫 release
**Baseline**: v0.7.1 (`main` HEAD — 111/111 tests PASS)
**브레인스토밍 결과**: 사용자 작업 순서 D → C → A → B 재정의에 따라 v0.7.2 = D-3 (검색·정렬), v0.7.3 = D-2 (아이콘 그리드), v0.7.4 = D-1 (Item 상세), v0.7.5 = D-4 (한글화) 로 release tag 와 작업 순서 일치화.

---

## 1. 목적

ContainerPanel (인벤·창고·외부 디스크 컨테이너 3-area mirror layout) 에서 list 가 길어졌을 때 사용자가 원하는 item 을 빠르게 찾고 카테고리·등급·품질 기준으로 정리해 보는 토대를 마련한다.

검색·정렬은 D 계열 4 sub-project (D-1 Item 상세 / D-2 아이콘 그리드 / D-3 검색·정렬 / D-4 한글화) 가운데 가장 의존성이 적고 (item 의 새 reflection 필드 발굴 외에는 자체 완결), 다른 D 작업의 상위 인프라 (정렬된 row 시퀀스 + filtered view list) 가 되어 가장 먼저 들어갈 가치가 크다. 한글화 (v0.7.5 D-4) 후에는 한글 검색이 자연스럽게 추가되도록 hook 만 남긴다.

## 2. 후속 sub-project 재정렬

브레인스토밍 결과 작업 순서 (D → C → A → B) 와 release tag 를 일치시킨다. HANDOFF.md §6.B 매핑이 변경된다.

| Version | 카테고리 | 의존성 |
|---|---|---|
| **v0.7.2** | **본 spec — D-3 컨테이너 검색·정렬** | v0.7.1 안정 |
| v0.7.3 | D-2 아이콘 그리드 (sprite reference + IMGUI grid) | v0.7.2 안정 |
| v0.7.4 | D-1 Item 상세 panel (선택 item reflection 표시) | v0.7.2~v0.7.3 안정. 강화 lv 등 추가 정렬 키 dropdown 합류 hook |
| v0.7.5 | D-4 Item 한글화 (한글 패치 hook 또는 자체 사전) | D-1/D-2/D-3 의 표시 영역 1차 안정 |
| v0.7.6 | 설정 panel (hotkey / 정원 / 창 크기 / 검색·정렬 영속) | 독립 |
| v0.7.7 | Apply 부분 미리보기 | 독립 |
| v0.7.8 | Slot diff preview | 독립 |
| v0.7.9 | NPC 지원 (heroID≠0) | 메뉴 / Apply path 의존 |

각 sub-project 는 별도 brainstorming → spec → plan → impl cycle.

## 3. 사용자 보고 + 확정 scope

브레인스토밍 Q1~Q6 결과:

### 3.1 핵심 use case (Q3 = 5)
"검색 + 정렬 둘 다 고르게 필요" — 단일 item 빠른 lookup, 카테고리·수치 기준 정리, 다중 선택 친화 layout 모두 해당.

### 3.2 적용 영역 (Q4 = 1)
ContainerPanel 의 **3-area 모두** (인벤 / 창고 / 외부 디스크 컨테이너). 각 area 가 독립된 SearchSortState 보유 — 한쪽 검색이 반대쪽 list 를 흔들지 않음.

### 3.3 정렬 키 (Q5 = 6 자유 입력)
사용자 확정 4 키:

1. **카테고리** — item type / category enum (default sort)
2. **이름** — `NameRaw` 한자 그대로 (한글 정렬은 v0.7.5 와 합류)
3. **등급** — 6단계 enum: 열악(0) → 보통(1) → 우수(2) → 정량(3) → 완벽(4) → 절세(5)
4. **품질** — 6단계 enum: 잔품(0) → 하품(1) → 중품(2) → 상품(3) → 진품(4) → 극품(5)

사용자 진술: **모든 아이템이 등급·품질 속성 보유** (Q6) — 즉 universal sort 가능. 단, 게임 내부 reflection 필드명은 v0.7.1 시점 미확인 → §5 spike 로 식별.

### 3.4 검색
substring case-insensitive 매칭 on `NameRaw` (한자). 한글 매칭은 v0.7.5 D-4 합류. 검색 box 는 `GUILayout.TextField` 기본 동작 (IME 입력은 IL2CPP 빌드 한계로 영문/한자만 신뢰). `string.Contains(..., StringComparison.OrdinalIgnoreCase)`.

### 3.5 영속성 (Q 미해당, design 결정)
**세션 휘발** — F11 닫고 재오픈 시 toolbar 상태 초기화. 사용자별 모드 (탐색 / 정리) 가 자주 바뀌고, persist 수요는 v0.7.6 설정 panel 에서 옵션화 추가 검토.

### 3.6 비범위 (Out of scope)
- 한글 검색 (v0.7.5)
- 다중 정렬 키 (1차+2차) — 단일 키 + tie-break (itemID asc) 만
- 카테고리 그룹 헤더 / collapse — view filter 만
- 강화 lv·내구도 등 신규 필드 (v0.7.4 D-1 발굴 후 별도)
- SlotListPanel (캐릭터 관리) — ContainerPanel 만 대상

## 4. 디자인 결정

### 4.1 Approach α (브레인스토밍 채택)

**Cache + 3-area mirror toolbar**.

1. ContainerRowBuilder 진입 시 4 sort key 를 `ItemRow` 에 미리 채움 (reflection 1회).
2. SearchSortState (text / 정렬키 / 방향) 변경 시에만 `ApplyView(rawRows, state)` 재계산 → cache.
3. OnGUI 매 frame 은 cache 만 그림 — IL2CPP IMGUI 부담 ↓.
4. 양방향 이동 (→ / ← 4-callback) 시 source list 변경 → cache invalidate + 재계산.

탈락 후보: β (글로벌 toolbar + 적용 area 라디오) — 동시 검색 불가, mirror layout 일관성 깨짐.

### 4.2 데이터 모델

`ItemRow` (현재 `ContainerRowBuilder` 가 채움) 신규 필드:

| 필드 | 타입 | 설명 | reflection 실패 시 |
|---|---|---|---|
| `CategoryKey` | string | item category enum/string raw | `""` (정렬 시 끝으로 밀림) |
| `NameRaw` | string | 한자 그대로 (기존 `Name` field 가 있다면 그것을 그대로) | `""` |
| `GradeOrder` | int | 0~5 (열악=0, 절세=5) | `-1` |
| `QualityOrder` | int | 0~5 (잔품=0, 극품=5) | `-1` |

기존 `Weight` / `Count` / `EnhanceLv` / `Equipped` / `WrapperRef` 등 그대로 유지. tie-break 키 (안정 정렬용 식별자) 는 plan 단계에서 기존 `ItemRow` 필드 (예: `ItemId` / `WrapperRef.GetHashCode()` / array index) 중 가장 안정적인 것으로 확정. 식별자 필드 부재 시 `ItemRow` 에 `int Index` 신규 추가 (raw row 순서 보존).

### 4.3 신규 모델 — SearchSortState

POCO + JSON 직렬화 가능 (v0.7.6 영속화 합류 시 재사용).

```csharp
public sealed class SearchSortState {
    public string Search { get; set; } = "";
    public SortKey Key { get; set; } = SortKey.Category;
    public bool Ascending { get; set; } = true;
}
public enum SortKey { Category, Name, Grade, Quality }
```

뮤테이터: `WithSearch(text)`, `WithKey(k)`, `ToggleDirection()` (immutable 패턴 — view cache invalidate trigger).

### 4.4 신규 컴포넌트 — SearchSortToolbar

IMGUI 1줄 재사용 컴포넌트:

```
[검색 box (TextField, 폭 ~140)] [정렬 ▼ dropdown (폭 ~80)] [▲ / ▼ (폭 ~28)]
```

dropdown 항목: **카테고리(default) / 이름 / 등급 / 품질**. 방향 버튼은 `▲` (asc) / `▼` (desc) 토글, default = asc.

IL2CPP IMGUI strip 회피: `GUILayout.TextField`, `GUILayout.Button(string)`, `GUILayout.Label(string)` default skin 만 사용 — `GUIStyle` 인자 받는 overload 금지.

dropdown 자체는 IL2CPP 환경에서 `EditorGUILayout.Popup` 미가용 → `GUILayout.Button` 4개를 한 줄에 깔거나 1 버튼 + popup 패턴 (ConfirmDialog 의 modal 패턴 재사용). 추천: **4 버튼 segmented control** (단순, strip-safe, 화면 폭 ~120 정도 차지).

```
[카테고리][이름][등급][품질]   ← 선택된 키만 highlight (text bold or "[X 카테고리 X]" 표기)
```

폭이 부담되면 **2번째 라인** 으로 분리. 인벤·창고·컨테이너 area 가 좁으면 toolbar 2-row 가 필수.

### 4.5 컴포넌트 배치

ContainerPanel layout 영향:

```
[인벤 toolbar (검색 + 정렬 키 4 + 방향)]
[인벤 list (ScrollView)]
[인벤 → 컨테이너 2 버튼]
[창고 toolbar]
[창고 list]
[창고 → 컨테이너 2 버튼]
   ‖
[컨테이너 toolbar]
[컨테이너 list]
[← 4 callback + ☓ 삭제]
```

toolbar 1-row 약 28px, 2-row 56px. v0.7.0.1 에서 ContainerPanel 높이 760 으로 키운 만큼 toolbar 3 set (1-row × 3 = 84px) 는 ScrollView 로 자체 흡수 가능. 2-row × 3 = 168px 일 경우 v0.7.6 에서 창 크기 조정 옵션과 합류.

기본은 **1-row** 가정. spike 후 폭이 부족하면 plan 단계에서 2-row fallback.

### 4.6 데이터 흐름

```
ContainerOps.ReadInventoryRows()         (기존 reflection)
  → ItemRow[] rawInv (sort key 4종 채움)
                                   ┐
SearchSortState_inv (toolbar 입력)─┼→ ApplyView(raw, state) → ItemRow[] viewInv (cache)
                                   ┘
                                              → ContainerPanel OnGUI: viewInv 만 그림
```

같은 패턴 storage / container 각각 mirror.

`ApplyView(raw, state)` pseudocode:
```csharp
IEnumerable<ItemRow> q = raw;
if (!string.IsNullOrEmpty(state.Search))
    q = q.Where(r => (r.NameRaw ?? "").IndexOf(state.Search, StringComparison.OrdinalIgnoreCase) >= 0);
q = state.Key switch {
    SortKey.Category => q.OrderBy(r => r.CategoryKey ?? "").ThenBy(r => r.ItemId),
    SortKey.Name     => q.OrderBy(r => r.NameRaw ?? "").ThenBy(r => r.ItemId),
    SortKey.Grade    => q.OrderBy(r => r.GradeOrder).ThenBy(r => r.ItemId),
    SortKey.Quality  => q.OrderBy(r => r.QualityOrder).ThenBy(r => r.ItemId),
    _ => q,
};
if (!state.Ascending) q = q.Reverse();
return q.ToArray();
```

tie-break 는 `ItemId` asc (안정 정렬). reflection 실패 시 `-1` / `""` 가 가장 앞 (asc) 또는 뒤 (desc) 로 정렬됨 — 사용자에게 토스트 1회 알림 + dropdown 의 해당 키 비활성화.

### 4.7 cache invalidate 트리거

| trigger | 처리 |
|---|---|
| toolbar 입력 (텍스트 / 키 / 방향) 변경 | 해당 area cache invalidate + ApplyView 재호출 |
| → / ← 4-callback (이동·복사 후 source/destination 둘 다) | 양 area cache invalidate + ContainerOps 다시 read → raw 재구성 → ApplyView |
| ☓ 삭제 (컨테이너) | container area cache invalidate |
| ContainerPanel open (F11 진입) | 모든 area cache 초기화 (state default — Category asc, search="") |

### 4.8 Reflection spike (v0.7.2 첫 task)

**spike 목표**: ItemData 인스턴스 1개의 등급·품질 필드명 식별.

**경로 후보** (Plugin.cs F12 또는 별도 dump task):
1. ContainerOps 가 이미 인벤 첫 item wrapper 를 reflection 으로 dump 가능 — 같은 path 재사용
2. dump 출력: 모든 public/private field + property + 값 (BepInEx 로그)
3. 필드명 후보 키워드:
   - 등급: `grade` / `level` / `lv` / `tier` / `rank` / `dengji` (한자 "等级" pinyin)
   - 품질: `quality` / `purity` / `pin` / `pinji` (한자 "品级" pinyin) / `pinzhi` (한자 "品质" pinyin)
4. 값 매핑: dump 결과를 게임 인게임 화면에 보이는 등급·품질 글자 (열악/보통/.../절세, 잔품/.../극품) 와 cross-reference

**산출물**: `docs/superpowers/dumps/2026-05-XX-v0.7.2-grade-quality-spike.md`. 어느 필드가 어느 enum (한자 → int 0~5) 인지 매핑표 + reflection access path.

**미발견 시 fallback**:
- dropdown 에서 "등급" / "품질" 항목 disabled 표시 + 토스트 1회 안내
- v0.7.4 D-1 (Item 상세) 시점에 다시 dump 시도 (사용자가 Item 상세에서 모든 field 보고 싶다는 자체 통증 있음)
- spec 단계에서는 4 sort key 그대로 유지 (활성화는 spike 결과 의존)

### 4.9 게임 내 색상 표시 (사용자 제공 — 후속 자산)

사용자 보고 (2026-05-03 brainstorming 마지막 turn): 게임 내 모든 아이템 아이콘이 등급·품질을 색상으로 분리 표시.

| 단계 | 등급 (아이콘 **배경색**) | 품질 (아이콘 **상단 작은 마름모 색**) |
|---|---|---|
| 0 | 열악 — 회색 | 잔품 — 회색 |
| 1 | 보통 — 녹색 | 하품 — 녹색 |
| 2 | 우수 — 하늘색 | 중품 — 하늘색 |
| 3 | 정량 — 보라색 | 상품 — 보라색 |
| 4 | 완벽 — 오렌지색 | 진품 — 오렌지색 |
| 5 | 절세 — 빨간색 | 극품 — 빨간색 |

**v0.7.2 본 release 활용**: row 표시에 **이름 텍스트 색상 = 등급 색상** 적용 가능 (정렬 후 시각 cross-reference). plan 단계에서 IL2CPP IMGUI strip-safe color tag 가능 여부 검증 후 결정 — 가능하면 v0.7.2 에 inline 추가, 아니면 v0.7.3 D-2 아이콘 그리드와 합류.

**v0.7.3 D-2 (아이콘 그리드) 자산**: sprite reference 직접 노출이 IL2CPP 에서 막히면, 색상 6단계 매핑만으로 placeholder 색 사각형 + 상단 마름모 (작은 box) 합성으로 대체 가능 — full sprite 없이도 시각 구분 가능.

**v0.7.4 D-1 (Item 상세 panel) 자산**: 등급·품질 행을 색상 박스 + 텍스트로 표시. spike 결과 dump 시 hex 코드 cross-reference 가치 있음.

색상 hex 후보 (사용자 화면 캡처 기반 추정 — 실제 게임 내 정확한 값은 v0.7.3 / v0.7.4 sprite 분석 시점 확정):
- 회색 ≈ `#9CA3AF` / 녹색 ≈ `#22C55E` / 하늘색 ≈ `#38BDF8` / 보라색 ≈ `#A855F7` / 오렌지색 ≈ `#F97316` / 빨간색 ≈ `#EF4444`

### 4.10 한자 → int order 매핑

spike 가 enum 값으로 `0~5` 같은 숫자를 노출하면 그대로 사용. 한자 string 이면 매핑표:

```csharp
static readonly Dictionary<string, int> GradeMap = new() {
    ["열악"] = 0, ["普通"] = 1, ["우수"] = 2, ["정량"] = 3, ["완벽"] = 4, ["절세"] = 5,
    // 한자 raw 도 함께 (게임 내부는 중국어 한자):
    ["劣"] = 0, ["普"] = 1, ["优"] = 2, ["精"] = 3, ["完美"] = 4, ["绝世"] = 5,
};
static readonly Dictionary<string, int> QualityMap = new() {
    ["잔품"] = 0, ["하품"] = 1, ["중품"] = 2, ["상품"] = 3, ["진품"] = 4, ["극품"] = 5,
    ["残"] = 0, ["下"] = 1, ["中"] = 2, ["上"] = 3, ["珍"] = 4, ["极"] = 5,
};
```

spike 결과로 정확한 한자 raw string 을 확정한 후 위 dictionary 채움. (한국어 raw 는 사용자 표시용 fallback. 게임 내부는 한자.)

### 4.11 IL2CPP / 성능 가드

- `System.Linq` 사용 OK (BepInEx 6 mscorlib 표준 — 기존 코드베이스에서 사용 중)
- list 규모 가정: 인벤 ~200 / 창고 ~500 / 컨테이너 ~1000. ApplyView 1회 ~1ms 미만 — cache 효과 충분
- toolbar 입력 후 매 frame 마다 ApplyView 재호출하지 않도록 `state` 변경 감지 후 1회만 재계산
- reflection 은 ContainerRowBuilder 진입 시 1회 (sort key 4종) — 매 frame 호출 금지

## 5. 신규/변경 모듈

| 모듈 | 위치 | 역할 |
|---|---|---|
| **신규** `SearchSortState` POCO | `src/LongYinRoster/Containers/` | text / key / 방향 + immutable mutators |
| **신규** `SortKey` enum | 동상 | Category / Name / Grade / Quality |
| **신규** `ContainerView` (cache) | 동상 | `ApplyView(raw, state)` + raw·state hash 비교로 cache hit/miss |
| **신규** `SearchSortToolbar` IMGUI | `src/LongYinRoster/UI/` | 검색 box + 4 segmented + 방향 버튼 |
| **변경** `ItemRow` | 기존 | 4 sort key 필드 추가 |
| **변경** `ContainerRowBuilder` | 기존 | sort key 채우는 reflection helper 호출 |
| **신규** `ItemReflector.GetGradeOrder` / `GetQualityOrder` | `Core/ItemReflector.cs` (**신규** — item-level reflection. 기존 `ItemListReflector` 는 list-level maxWeight 전용이라 구분) | spike 결과 기반 reflection access |
| **변경** `ContainerPanel` | 기존 | area 별 toolbar 그리기 + state 보유 + cache 사용 |

기존 reflection helper 패턴 (`ItemListReflector.GetMaxWeight` v0.7.1 도입) 그대로 따른다.

## 6. 테스트 전략

### 6.1 Unit (LongYinRoster.Tests, +11 추가 → 122/122)

- `SearchSortStateTests` (3): 기본값, mutator 3종 (WithSearch / WithKey / ToggleDirection) — 새 인스턴스 반환 확인
- `ContainerViewTests` (5):
  - filter substring case-insensitive
  - sort 4-key 각각 (tie-break itemID asc)
  - 방향 토글 (Reverse 결과)
  - reflection 실패 row (-1 / "") 끝/앞 배치
  - cache hit (raw·state 동일 시 같은 array 인스턴스 반환)
- `ItemReflectorGradeQualityTests` (3): grade/quality dictionary 매핑 (한자/한글 양쪽), 미매핑 시 `-1` 반환

### 6.2 인게임 smoke (3-area × 4-key + 검색 + 방향 = 6/6 minimum)

각 area (인벤·창고·컨테이너) 별:
1. 검색 substring (한자 or 영문) 매칭 동작
2. 정렬 키 4종 각각 (카테고리·이름·등급·품질) 동작
3. 방향 ▲/▼ 토글
4. → / ← 이동·복사 후 view 자동 갱신 (cache invalidate)
5. F11 닫고 재오픈 시 state 초기화 확인 (영속 안 함)
6. spike 결과로 등급·품질 dropdown 활성화 (또는 fallback 시 disabled + 토스트)

## 7. 위험·미지수

| 항목 | 위험 | 대응 |
|---|---|---|
| **Spike 실패** (등급·품질 reflection 필드 미발견) | 4 정렬 키 중 2개 기능 상실 | dropdown 비활성화 + 토스트. v0.7.4 D-1 에서 재시도. release 자체는 카테고리·이름 2-key 만으로도 진행 |
| **IME 한글 입력 strip** | 검색 box 가 한자만 받으면 사용자 경험 악화 | 한글 검색은 v0.7.5 한글화와 합류 — v0.7.2 는 한자 검색만 지원 (사용자가 게임 내 한자명 일부 알면 가능) |
| **dropdown layout 폭 부족** | 1-row toolbar 가 area 폭 초과 → 깨짐 | 2-row fallback 명시. 또는 정렬 키 4 segmented 폭 좁히기 (icon-only?) — plan 단계 결정 |
| **cache invalidate 누락** | 4-callback 후 view 갱신 안 됨 → stale list | callback 내 invalidate 명시 + 인게임 smoke 4번에서 검증 |
| **양방향 이동의 race** | source 변경 직후 destination 변경 사이 프레임 사이 stale | OnGUI 진입 시 cache hit 시도 + raw hash 비교 (raw 가 바뀌면 무조건 재계산) |

## 8. 완료 기준

- 4 정렬 키 + 검색 box 가 3-area (인벤·창고·컨테이너) 모두 동작
- → / ← 이동·복사 후 view 자동 갱신
- reflection spike 결과 dump 문서 commit
- spike 결과에 따라 dropdown 활성화 / fallback (disabled + 토스트) 동작
- **122/122 unit tests PASS** (기존 111 + 신규 11)
- 인게임 smoke 6/6 PASS (3-area × 검색·정렬 시나리오)
- HANDOFF.md §6.B 의 v0.7.2~v0.7.5 mapping 갱신 (D-3=v0.7.2, D-2=v0.7.3, D-1=v0.7.4, D-4=v0.7.5)

## 9. release 후 contract

- v0.7.2 release tag + dist zip + VERSION bump
- HANDOFF.md update (§1, §6.B, §7 컨텍스트 압축본)
- README 사용자 가이드에 검색·정렬 사용법 1단락
- 다음 sub-project = v0.7.3 (D-2 아이콘 그리드)
