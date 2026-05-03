# v0.7.1 — 컨테이너 UX 개선 (destination 명시 + capacity 표시 + 가드)

**작성일**: 2026-05-03
**Sub-project 위치**: v0.7.0.1 hotfix 후속의 첫 feature release
**Baseline**: v0.7.0.1 (`0d25e14` — 3 critical fix 통합 main HEAD)
**브레인스토밍 결과**: 사용자 보고 4 UX 이슈 중 3건 (A·B·C) 만 v0.7.1 scope 로 확정. D 계열 (Item 상세 / 아이콘 그리드 / 검색·정렬) 은 D-1 / D-2 / D-3 sub-project 로 분할 후 각자 별도 release 로 분리.

---

## 1. 목적

v0.7.0.1 smoke 도중 사용자가 인게임 사용 중 발견한 4 UX 이슈 가운데, **destination 모호 / 용량 정보 부재 / 용량 가드 부재** 3건을 한 release 로 마무리한다. 이 3건은 모두 컨테이너 ↔ 게임 (인벤/창고) 양방향 흐름의 운용 안전성·명료성에 직결되며 서로 짝지어 풀려야 자연스럽다 (capacity 값을 모르면 가드도 표시도 못 함, destination 이 분리돼야 가드 정책을 destination 별로 다르게 줄 수 있음).

D 계열 (Item 상세 panel / 아이콘 그리드 / 검색·정렬) 은 자체 design + 신규 IL2CPP IMGUI challenge (sprite reference, grid layout, asset reference cache invalidate) 가 많아 별도 brainstorm·spec 사이클 가치가 있다 — 이번 release 에 묶지 않는다.

## 2. 후속 sub-project 재정렬

브레인스토밍 결과 D 분할로 인해 HANDOFF.md §1 의 v0.7.x 매핑이 변경된다. 새 우선순위:

| Version | 카테고리 | 의존성 |
|---|---|---|
| **v0.7.1** | **본 spec — 컨테이너 UX 1차 (destination 명시 / capacity 표시 / 가드)** | v0.7.0.1 안정 |
| v0.7.2 | D-1: Item 상세 panel (선택 item reflection 표시) | v0.7.1 안정 |
| v0.7.3 | D-2: 아이콘 그리드 (sprite reference + IMGUI grid) | v0.7.2 안정 |
| v0.7.4 | D-3: 검색·정렬 (이름·카테고리·강화 등 query) | v0.7.2 안정 |
| v0.7.5 | 설정 panel (hotkey / 정원 / 창 크기) | 독립 |
| v0.7.6 | Apply 부분 미리보기 | 독립 |
| v0.7.7 | Slot diff preview | 독립 |
| v0.7.8 | NPC 지원 (heroID≠0) | 메뉴 / Apply path 의존 |

각 sub-project 는 별도 brainstorming → spec → plan → impl cycle.

## 3. 사용자 보고 + 확정 scope

브레인스토밍 Q1~Q5 결과:

### 3.1 Task A — destination 모호

**시나리오**: ContainerPanel 우측 column 의 `[← 이동]` / `[← 복사]` 단일 버튼 → 인벤토리 / 창고 어느 쪽으로 가는지 시각·코드 양쪽으로 결정돼 있지 않음. 코드 레벨에서도 `OnContainerToInventoryMove` / `OnContainerToInventoryCopy` 두 callback 만 wire 돼있고 창고 방향 callback 자체가 부재.

### 3.2 Task B — 인벤/창고 용량 정보 부재

**시나리오**: 좌측 column 라벨 `"인벤토리 (45개)"` / `"창고 (32개)"` — 절대 갯수만 표시, MAX 모름. 사용자는 안전 한계를 외부 지식으로만 추정해야 함.

### 3.3 Task C — 용량 가드 부재 (silent fail / overflow 가능)

**시나리오**: 컨테이너 → 인벤/창고 [이동/복사] 시 destination 에 capacity 가 남아있는지 사전 check 가 없음. 현재 `ContainerOps.AddItemsJsonToGame` 이 partial 처리는 안전하게 하지만 (Succeeded 갯수만큼만 컨테이너에서 제거) 사용자에게 결과 toast 가 부족 — "조금이라도 들어갔는지" 외부에서 확인해야 함.

### 3.4 게임 시스템 사실 (Q5 user input + spike 결과)

가드 정책 + 단위에 영향:
- **인벤토리**: maxWeight 초과 추가 허용 (캐릭터 이동속도 페널티 발생, add 자체는 성공)
- **창고**: maxWeight hard cap (초과분 add 거절)
- **단위는 무게 (kg, Single/float)** — spike (`docs/superpowers/dumps/2026-05-03-v0.7.1-capacity-spike.md`) 로 확정. ItemListData 에 갯수 기반 capacity 자체가 없음. `maxWeight` (Single) property 만 노출.
- 사용자 결정 (B): 라벨에 **갯수 + 무게 둘 다 표시** (`"인벤토리 (45개, 720.3 / 964 kg)"`), 가드는 무게 기반.

→ 두 destination 의 가드 정책을 다르게 분기 + capacity 단위는 float 무게로 통일.

## 4. 디자인 결정

### 4.1 Task A — 대칭 mirror UI

좌측 column 이 `[인벤 list / 인벤 → 컨테이너 2 버튼 / 창고 list / 창고 → 컨테이너 2 버튼]` 으로 이미 destination 별 분리. 우측 column 도 같은 mirror:

```
[컨테이너 list]
[← 인벤으로 이동]   [← 인벤으로 복사]
[← 창고로 이동]     [← 창고로 복사]
[☓ 삭제]
```

**Callback 변경** (ContainerPanel.cs):
- 기존 유지: `OnContainerToInventoryMove`, `OnContainerToInventoryCopy`
- **신규**: `OnContainerToStorageMove`, `OnContainerToStorageCopy`
- (좌측·삭제 callback 은 변경 없음)

**Plugin.cs wiring**: 4 callback 이 ContainerOpsHelper 의 두 method 로 mapping — `OnContainerToInventory{Move,Copy}` → `helper.ContainerToInventory(...)`, `OnContainerToStorage{Move,Copy}` → `helper.ContainerToStorage(...)`. helper signature 는 §4.3 참조.

**Layout 영향**: 컨테이너 list height 420 → 약 360 (버튼 행 1개 추가, ~28px). ScrollView 안이라 스크롤로 보완.

### 4.2 Task B — capacity 표시 (무게 기반 + 갯수 동시)

**Spike 결과** (완료 — `docs/superpowers/dumps/2026-05-03-v0.7.1-capacity-spike.md`):
- ItemListData 에 `maxWeight` (Single/float) property 한 개만 capacity 후보로 매칭
- 갯수 기반 capacity 자체 부재. **단위 = kg, 자료형 = float**
- → PASS path 채택, 단 reflector 가 갯수가 아닌 무게 반환

**Helper**: `Core/ItemListReflector.cs` — `float GetMaxWeight(object? itemList, float fallback)`. `CAPACITY_NAMES = new[] { "maxWeight" }`. 미발견 시 fallback.

**Config fallback**: `Config.cs` 에 BepInEx ConfigEntry 추가 (항상 bind, helper fallback 으로 사용):
- `InventoryMaxWeight` (float, default 964f)
- `StorageMaxWeight` (float, default 300f)

**라벨 포맷** (B + 무게):
- 정상: `"인벤토리 (45개, 720.3 / 964 kg)"` / `"창고 (32개, 156.8 / 300 kg)"`
- 인벤 over-cap: `"인벤토리 (180개, 1020.5 / 964 kg) ⚠ 초과"` (currentWeight > maxWeight 시 마커)
- 창고는 hard cap → over-cap 상태 발생 불가 (가드가 거절), 마커 미표시
- currentWeight = ItemRow.Weight 합계 (ContainerPanel 자체 계산)

**KoreanStrings 추가**:
- `Lbl_Inventory = "인벤토리"`
- `Lbl_Storage = "창고"`
- `Lbl_OvercapMarker = " ⚠ 초과"`

**ContainerPanel signature 변경**:
- `SetInventoryRows(List<ItemRow> rows, float maxWeight)` (maxWeight 인자 추가)
- `SetStorageRows(List<ItemRow> rows, float maxWeight)` (maxWeight 인자 추가)
- 내부 helper: `internal static string FormatCount(string label, int countN, float currentWeight, float maxWeight, bool allowOvercap)`

### 4.3 Task C — destination 별 가드 정책 (무게 기반)

**ContainerOps.AddItemsJsonToGame signature 변경**:

```csharp
public static GameMoveResult AddItemsJsonToGame(
    object player,
    string itemsJson,
    float maxWeight,           // int → float (kg)
    bool  allowOvercap,        // 신규
    string targetField)        // 신규 ("itemListData" or "selfStorage")
```

- 현재 weight = `sum(allItem[i].weight)` (reflection)
- 시도할 entry 의 weight = JSON entry.weight
- `allowOvercap=true` (인벤): weight 가드 skip, 모든 entry 시도. 결과 `OverCapWeight = max(0, finalWeight - maxWeight)` (Single)
- `allowOvercap=false` (창고): 누적 시도 weight 합산해서 `currentWeight + sumTried > maxWeight` 시 그 entry 부터 Failed

**ContainerOpsHelper 변경** — 단일 `ContainerToGame` 분리:

```csharp
public Result ContainerToInventory(object player, HashSet<int> indices, bool removeFromContainer, float maxWeight);
public Result ContainerToStorage  (object player, HashSet<int> indices, bool removeFromContainer, float maxWeight);
```

- 내부적으로 `AddItemsJsonToGame` 에 적절한 (allowOvercap, targetField) 전달
- 양쪽 모두 partial 시 컨테이너 측 Succeeded 갯수만큼만 제거 (현 로직 패턴 유지)

**Result 확장**:

```csharp
public sealed class Result
{
    public int    Succeeded     { get; set; }
    public int    Failed        { get; set; }
    public float  OverCapWeight { get; set; }   // 신규 — 인벤 over-cap 발생 무게 (kg)
    public string Reason        { get; set; } = "";
}
```

**Toast 메시지** (KoreanStrings 신규 / 기존 보강):
- 인벤 정상: `"인벤토리로 N개 처리"`
- 인벤 over-cap: `"인벤토리로 N개 처리 ({finalW:F1}/{maxW:F1} kg 초과 — 이동속도 저하)"`
- 창고 정상: `"창고로 N개 처리"`
- 창고 partial: `"창고로 N개 처리 (K개는 무게 초과로 컨테이너에 남김)"`
- 창고 zero-available: `"창고 무게 한계 — 처리 불가"` + no-op
- 0개 선택: `"선택된 항목 없음"` (기존 유지)
- 컨테이너 미선택: `"컨테이너 미선택"` (기존 유지)

## 5. Out-of-scope (이번 release 절대 변경 안 함)

- 컨테이너 자체 capacity 제한 (file-backed → 무제한 유지, 라벨도 `"컨테이너 (N개)"` 그대로)
- 좌측 column 의 인벤·창고 → 컨테이너 방향 가드 (컨테이너 무제한이라 불필요)
- D 계열 — Item 상세 / 아이콘 / 검색·정렬 (v0.7.2 / v0.7.3 / v0.7.4 로 분리)
- 설정 panel / Apply 미리보기 / Slot diff / NPC 지원 (v0.7.5 이후)
- Capacity reflection 의 PASS/FAIL 결정 외 추가 ItemListData wrapper graph 작업 (필요 시 D-x 작업으로 흡수)

## 6. 파일 변경 (예상)

| 파일 | 변경 |
|---|---|
| `UI/ContainerPanel.cs` | DrawRightColumn 4-callback layout, FormatCount, SetInventory/StorageRows(capacity) |
| `Containers/ContainerOps.cs` | AddItemsJsonToGame(allowOvercap, targetField) signature |
| `Containers/ContainerOpsHelper.cs` | ContainerToInventory / ContainerToStorage 분리, Result.OverCap |
| `Core/Probes/ProbeItemListCapacity.cs` (신규) | Spike — F12 capacity dump |
| `Core/ItemListReflector.cs` (조건부 신규) | spike PASS 시 GetCapacity helper |
| `Plugin.cs` | 4 callback wiring, capacity 인자 row refresh, [F12] handler 임시 |
| `Util/KoreanStrings.cs` | 라벨 / toast 상수 추가 |
| `Config.cs` | InventoryMaxWeight / StorageMaxWeight ConfigEntry<float> (default 964f / 300f, fallback 용) |
| `LongYinRoster.Tests/ContainerOpsTests.cs` (신규) | allowOvercap 분기 테스트 |
| `LongYinRoster.Tests/ContainerOpsHelperTests.cs` (신규) | Result 형 / partial 시나리오 |
| `LongYinRoster.Tests/ContainerPanelFormatTests.cs` (신규) | FormatCount string assertion |

## 7. 검증

### 7.1 단위 테스트 (Tests project)

목표: 기존 45/45 → 49~51/51 PASS.

신규 테스트:
- `ContainerOpsTests.AddItemsJsonToGame_AllowOvercap_AddsAll` — mock player wrapper 가능 시
- `ContainerOpsTests.AddItemsJsonToGame_HardCap_PartialOnly`
- `ContainerOpsHelperTests.ContainerToStorage_PartialResult_RemovesOnlySucceeded`
- `ContainerOpsHelperTests.ContainerToInventory_OverCap_ReportsOverCapField`
- `ContainerPanelFormatTests.FormatCount_Normal` — `"인벤토리 (45 / 171 개)"`
- `ContainerPanelFormatTests.FormatCount_Overcap` — `"인벤토리 (175 / 171 개) ⚠ 초과"`
- `ContainerPanelFormatTests.FormatCount_StorageNoMarker` — over-cap path 비활성

(IL2CPP wrapper 의존 테스트는 mock 어려우면 helper-level / pure string 만 검증.)

### 7.2 인게임 smoke (사용자 검증 6 항목)

1. **인벤토리 라벨** `"인벤토리 (N개, X.X / 964 kg)"` 표시 — reflection 우선, 미발견 시 config fallback
2. **인벤토리 over-cap 마커** — 무게 초과 상태에서 라벨 끝에 `⚠ 초과` 표시
3. **창고 라벨** `"창고 (N개, X.X / 300 kg)"` 표시
4. **컨테이너 → 인벤** 4 버튼 (이동·복사) 동작 + over-cap 시 toast `(X.X/964 kg 초과 — 이동속도 저하)`
5. **컨테이너 → 창고** 4 버튼 (이동·복사) 동작 + 무게 한계 시 거절 toast `"창고 무게 한계 — 처리 불가"` / partial 시 `K개는 무게 초과로 컨테이너에 남김`
6. **회귀 검증** — v0.7.0.1 의 컨테이너 신규/이름변경/삭제 + 인벤/창고 → 컨테이너 동작 정상

## 8. Release 절차

1. v0.7.1 plan 작성 (`docs/superpowers/plans/2026-05-03-longyin-roster-mod-v0.7.1-plan.md`) — 본 spec 의 §4 / §6 / §7 을 phase 로 분해
2. spike → fork 결정 → impl → 단위 테스트 → 빌드 → 인게임 smoke
3. VERSION bump 0.7.0.1 → 0.7.1
4. README / HANDOFF 업데이트:
   - v0.7.1 entry 추가
   - v0.7.x sub-project 번호 재정렬 (D-1/D-2/D-3 신규, 기존 v0.7.2~v0.7.5 push back per §2)
   - smoke 결과 dump 첨부 (`docs/superpowers/dumps/2026-05-03-v0.7.1-smoke-results.md`)
5. dist zip 패키징 + GitHub release tag `v0.7.1`

## 9. 위험 / 미지수

| 위험 | 완화 |
|---|---|
| Spike 가 갯수 capacity 가 아닌 무게 maxWeight 만 노출 (이미 발생) | spec §3.4/4.2/4.3 무게 기반으로 재정의. Config fallback 도 float. 영향 phases 모두 update |
| 인벤 over-weight 시 게임 측 추가 부수효과 (속도 외) | smoke 4 에서 인게임 확인. 추가 부수효과 발견 시 toast 메시지 보강 |
| ItemRow.Weight (float) 합산 정확도 (float 누적 오차) | 일반 weight 1~50 kg 단위, 합산 200개 이내라면 float 정밀도 충분 |
| `AddItemsJsonToGame` signature 변경의 회귀 (인벤 → 컨테이너 측은 호출 site 영향 없음) | 회귀 검증 항목 6 으로 fence |
| ContainerPanel layout reflow 로 기존 회귀 (높이 760 다시 모자라는 등) | smoke 1~5 로 직접 확인 |
| KoreanStrings 추가가 기존 const 와 충돌 | 빌드 시 즉시 catch |
| 컨테이너 list height 360 으로 줄어 ScrollView UX 저하 | 360 도 충분 검증, 부족 시 layout 재조정 (height 760 → 800 검토)
