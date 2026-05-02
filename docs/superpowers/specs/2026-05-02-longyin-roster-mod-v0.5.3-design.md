# LongYin Roster Mod v0.5.3 — Design Spec

**일시**: 2026-05-02
**Scope**: 인벤토리 (ItemList) Replace — `itemListData.allItem` 을 slot list 로 완전 교체. v0.4 PoC A4 의 ItemDataFactory FAIL 해제.
**선행 spec**:
- `2026-05-02-longyin-roster-mod-v0.5.2-design.md` (KungfuList — wrapper ctor + game-self method + 2-pass retry)
- `2026-05-02-longyin-roster-mod-v0.5.1-design.md` (active full)
- (이전: v0.3 / v0.4 / v0.5)
**HANDOFF**: `docs/HANDOFF.md` §6 — v0.6 통합 작업 후보

---

## 1. Context

### 1.1 v0.5.2 까지의 도달점

- 무공 active 카테고리 활성화 (v0.5.1 — game 패턴 11-swap)
- 무공 list 카테고리 활성화 (v0.5.2 — `LoseAllSkill` clear + `KungfuSkillLvData(int)` ctor + `GetSkill(wrapper)` add + 2-pass retry)
- SlotFile JSON 직렬화 fix (v0.5/v0.5.2 잠재 버그)
- Tests 57/57 PASS

### 1.2 v0.4 PoC A4 의 인벤토리 — v0.5.3 의 동기

**v0.4 PoC A4**: `ItemDataFactory.IsAvailable=false` 로 short-circuit. 이유 — ItemData wrapper ctor IL2CPP 한계 (그 시점 가설).

**v0.5.2 의 발견**: `KungfuSkillLvData(int _skillID)` ctor 가 정상 작동 — ItemData 도 같은 패턴 가능성 높음. v0.5.3 에서 검증.

**v0.5.3 의 동기**:
- v0.4 stub 코드 (`LoseAllItem` clear + `GetItem` add) 활성화 — `ItemData` ctor 만 풀면 됨
- v0.5.2 알고리즘 (clear + add + 2-pass retry) 직접 mirror
- 사용자 가치: 장비 / 소모품 / 책 / 재료 등 인벤토리 전체 복원

### 1.3 v0.5.3 process — Hybrid (v0.5.2 패턴 mirror)

1. **Spike Phase 1** — ItemData type ctor / static factory dump + 통합 검증
2. **User gate** — Spike PASS path 결정
3. **Spec impl** — ItemListApplier (KungfuListApplier 와 거의 동일)
4. **Smoke + release** — v0.5.3 minor release

### 1.4 출하 단위

**v0.5.3 minor release** (`v0.5.3` tag). 창고 / 외형은 별도 sub-project.

---

## 2. Goals & Non-goals

### 2.1 Goals

1. **ItemListApplier 구현** — `LoseAllItem` clear + `ItemData` ctor + `GetItem(wrapper)` add + 2-pass retry
2. **ApplySelection.ItemList** 활성화 — default off (이미 v0.4 부터 존재), 사용자 명시 토글
3. **Capabilities.ItemList** = true 로 — `ProbeItemListCapability` 의 hardcoded false 해제
4. **ItemDataFactory.IsAvailable** = true — Spike PASS 후 stub 해제
5. **save → reload persistence** — itemID + itemCount 정확히 유지
6. **legacy 호환** — v0.3 / v0.4 / v0.5.1 / v0.5.2 슬롯 파일 무손실
7. **회귀 검증** — v0.5.2 의 KungfuList / active / 정체성 / 천부 / 스탯 등 유지

### 2.2 Non-goals

| # | 항목 | 미루는 사유 |
|---|---|---|
| N1 | 창고 (`selfStorage.allItem`) | 별도 sub-project (v0.5.4 또는 v0.6.0) |
| N2 | 외형 (`faceData (HeroFaceData)` + `partPosture (PartPostureData)`) | 별도 sub-project |
| N3 | ItemData sub-data graph (`equipmentData / medFoodData / etc`) | itemID + count 만 복원. sub-data 는 game-internal lookup 으로 자동 초기화 가정. 강화도 / 옵션 등 보존은 v0.6+ |
| N4 | UI cache invalidation 일반화 | v0.5.1 + v0.5.2 의 발견을 framework 추출 — 별도 sub-project |

---

## 3. Architecture

### 3.1 Hybrid spike + spec + impl 흐름

```
[Spike Phase 1] ItemData ctor / sub-data graph discovery
  ├─ Step 1: HeroData method dump 검증 (LoseAllItem / GetItem 시그니처)
  ├─ Step 2: ItemData type 의 ctor + static factory dump
  ├─ Step 3: ctor 호출 + GetItem 통합 시도
  └─ Step 4: 통합 (clear + add all) + read-back persistence
        ↓
       G1 (PASS / FAIL)
        ↓
   ┌────┴────┐
   PASS    FAIL
    ↓        ↓
 [Impl]  [User gate — abort + 외형 sub-project 변경]

[Impl Phase] (Spike PASS 후)
  ├─ ItemListApplier 작성 (KungfuListApplier mirror)
  ├─ PinpointPatcher.RebuildItemList 본문 교체
  ├─ ItemDataFactory.IsAvailable = true (또는 폐기)
  ├─ ProbeItemListCapability 정확화
  ├─ Unit tests 추가
  └─ Smoke 시나리오 1-3 + 회귀 (G2)
        ↓
     G3 release v0.5.3
```

### 3.2 코드 파일 영향 범위

**기존 패턴 재사용 — 신규 인프라 없음**:

| 영역 | 영향 | 변경 |
|---|---|---|
| Core | `ItemListApplier.cs` (new) | new 파일 1 개 |
| Core | `PinpointPatcher.cs` | `RebuildItemList` 본문 교체 (현재 v0.4 stub — `ItemDataFactory.Create` 호출 path), `ProbeItemListCapability` 정확화 |
| Core | `ItemDataFactory.cs` | `IsAvailable=true` + Spike PASS 결과 따라 `Create` 본문 (또는 폐기) |
| Core/Probes | `ProbeItemList.cs` (new) | Spike Phase 1 |
| Tests | `ItemListApplierTests.cs` (new) | 5 unit tests |
| UI | `SlotDetailPanel.cs` | (변경 없음 — 이미 ItemList 체크박스 존재) |
| Slots | (변경 없음) | ApplySelection.ItemList 이미 v0.4 부터 존재 |
| Plugin | VERSION 0.5.2 → 0.5.3 | release 시 |

---

## 4. Spike Phase 1 detail

### 4.1 입력 조건

- 인벤토리에 다양한 종류의 item (장비/소모품/책/재료) 채워진 상태
- 다른 캐릭터의 인벤토리도 가능 (item 종류 다양화)

### 4.2 Spike steps

#### Step 1 — HeroData method 검증

```
^(Lose|Add|Get|Remove)(All)?Item.*
```
패턴으로 reflection scan. 예상 method:
- `LoseAllItem()` — parameterless clear
- `GetItem(ItemData wrapper)` 또는 `GetItem(ItemData wrapper, bool showInfo, ...)` — add (v0.4 stub 가정)

#### Step 2 — ItemData type dump (v0.5.2 Step 6 mirror)

ItemData 의 ctor / static method dump:
- `ItemData()` parameterless
- `ItemData(int itemID)` — 단일 int (가장 가능성 높음, v0.5.2 KungfuSkillLvData 와 같은 패턴)
- `ItemData(int itemID, int itemCount)` — 더 직접적
- `ItemData(IntPtr pointer)` — IL2CPP wrapper ctor
- `ItemData.Create*` static factory

#### Step 3 — ctor 호출 + GetItem add 통합

ctor 발견 시 wrapper 생성 + property setter (`itemCount`) + `GetItem(wrapper)` 호출 → `itemListData.allItem.Count` 변화 read-back

#### Step 4 — 통합 (LoseAllItem clear + add all) + persistence

다른 캐릭터의 인벤토리 list (itemID + count) 추출 → 현재 player 에 적용 → save → reload → list 정확히 유지 검증

### 4.3 PASS 기준

- Step 2 ctor 발견 (또는 static factory)
- Step 3 add 후 read-back 의 itemID 일치
- Step 4 clear + add all 후 list count = slot count
- save → reload 후 유지

### 4.4 FAIL → User gate

옵션:
- **abort + 외형 sub-project** — `ItemData` wrapper ctor 한계가 v0.4 PoC A4 와 동일하면 abort
- **다른 wrapper graph 탐색** — `ItemData` 의 base class 또는 derived class

---

## 5. Implementation 설계 (Spike PASS 후)

### 5.1 ItemListApplier

**새 파일**: `src/LongYinRoster/Core/ItemListApplier.cs` (KungfuListApplier 와 거의 동일 구조)

```
public static class ItemListApplier {
    public sealed class Result { Skipped, Reason, RemovedCount, AddedCount, FailedCount }
    public sealed record ItemEntry(int ItemID, int ItemCount);

    public static IReadOnlyList<ItemEntry> ExtractItemList(JsonElement slot);
    public static Result Apply(object? player, JsonElement slot, ApplySelection sel);
    public static Result Restore(object? player, JsonElement backup);
}
```

**Apply 알고리즘**:
1. selection check
2. slot 의 `itemListData.allItem` 추출 → `targetEntries`
3. wrapper type 발견 (첫 element)
4. wrapper ctor 발견 (Spike PASS 결과 — `(int)` 또는 `(int, int)`)
5. clear via `LoseAllItem()`
6. add 2-pass retry (v0.5.2 패턴):
   - 각 entry 마다 ctor → `itemCount` setter → `GetItem(wrapper)` 호출
   - read-back 검증 후 누락 시 재시도

### 5.2 PinpointPatcher.RebuildItemList 본문 교체

기존 v0.4 stub:
```csharp
private static void RebuildItemList(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.ItemList) { res.SkippedFields.Add("itemList (selection off)"); return; }
    if (!Probe().ItemList)   { res.SkippedFields.Add("itemList (PoC failed — v0.5+ 후보)"); return; }
    // ... LoseAllItem + GetItem(ItemDataFactory.Create) loop ...
}
```

v0.5.3 교체:
```csharp
private static void RebuildItemList(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.ItemList) { res.SkippedFields.Add("itemList (selection off)"); return; }
    if (!Probe().ItemList)   { res.SkippedFields.Add("itemList (capability off)"); return; }

    var r = ItemListApplier.Apply(player, slot, selection);
    if (r.Skipped) { res.SkippedFields.Add($"itemList — {r.Reason}"); return; }
    res.AppliedFields.Add($"itemList (removed={r.RemovedCount} added={r.AddedCount} failed={r.FailedCount})");
    if (r.FailedCount > 0)
        res.WarnedFields.Add($"itemList — {r.FailedCount} entries failed");
}
```

### 5.3 ProbeItemListCapability

기존 stub:
```csharp
private static bool ProbeItemListCapability(object p)
{
    return ItemDataFactory.IsAvailable
        && p.GetType().GetMethod("LoseAllItem", F) != null
        && p.GetType().GetMethod("GetItem", F) != null;
}
```

v0.5.3 — `ItemDataFactory.IsAvailable` 의존 제거 (또는 IsAvailable=true 변경):
```csharp
private static bool ProbeItemListCapability(object p)
{
    return p.GetType().GetMethod("LoseAllItem", F, null, Type.EmptyTypes, null) != null
        && p.GetType().GetMethod("GetItem", F) != null;
    // ItemData ctor 검사는 ItemListApplier.Apply 시 시도 (lazy)
}
```

### 5.4 ItemDataFactory.cs 폐기 또는 유지

옵션:
- **폐기** — ItemListApplier 가 wrapper ctor 직접 호출. ItemDataFactory 불필요
- **유지 + 본문 교체** — `Create(itemID, itemCount)` 가 wrapper ctor 호출하는 helper

권장: **폐기** — code 단순화 (KungfuListApplier 도 ctor 직접 호출).

### 5.5 Tests

**새 unit tests**: `src/LongYinRoster.Tests/ItemListApplierTests.cs` (5 tests, KungfuListApplierTests mirror):
- `ExtractItemList_ReturnsAllEntries`
- `ExtractItemList_HandlesEmptyList`
- `ExtractItemList_MissingItemListData_ReturnsEmpty`
- `Apply_RespectsApplySelection_SkipsWhenFalse`
- `Apply_HandlesMissingPlayer_SkipsWithReason`

**총 tests**: 57 → 62+

### 5.6 Plugin VERSION

`0.5.2` → `0.5.3`

---

## 6. Smoke 시나리오

### 6.1 시나리오 1 — 다른 캐릭터 인벤토리 Apply

1. Pre: 다른 캐릭터 (다양한 장비/소모품) → mod slot 1 capture
2. 현재 캐릭터 load → mod slot 1 → ✓ ItemList → ▼ Apply
3. 인벤토리 패널 → slot 1 의 item set
4. save → reload → 유지

### 6.2 시나리오 2 — Self-Apply (item 추가/제거 후 복원)

1. Pre: 현재 캐릭터 capture → mod slot 1
2. 게임에서 일부 item 사용/구입 → 인벤토리 변경
3. mod slot 1 → ✓ ItemList → ▼ Apply (자동백업 → slot 0)
4. 인벤토리 = slot 1 시점

### 6.3 시나리오 3 — Restore (slot 0)

시나리오 2 직후 → mod slot 0 → ↶ 복원 → 인벤토리 = 변경된 시점

### 6.4 회귀

- v0.5.2 KungfuList Apply 동작 유지
- v0.5.1 active / 정체성 / 천부 / 스탯 / 명예 / 스킨 / 자기집 동작
- 외형 / 창고 disabled 표시 유지
- legacy 슬롯 (v0.1~v0.5.2) 호환

---

## 7. Failure mode / Out

| 단계 | FAIL 시 |
|---|---|
| Spike Step 1-4 모두 FAIL | user gate → abort + 외형 sub-project 변경 |
| 2-pass retry 도 silent fail | 3-pass 또는 다른 method path (예: `(itemID, itemCount)` ctor) |
| sub-data graph (강화도/옵션) 미보존 | MVP 는 itemID + count 만. v0.6+ 에서 sub-data 보존 검토 |
| Smoke FAIL | release 안 함, foundation 보존, dump |
| 회귀 FAIL | release 안 함, fix 또는 revert |
| 모두 PASS | release v0.5.3 |

---

## 8. Release / Git plan

- Branch: `v0.5.3` (main 에서 분기)
- VERSION: 0.5.2 → 0.5.3
- Tag: `v0.5.3`
- Commits 흐름:
  - `spike(v0.5.3): ProbeItemList + F12 trigger`
  - `spike(v0.5.3): ItemData ctor 발견 결과`
  - `feat(core): ItemListApplier — clear + add all + 2-pass retry + 5 tests`
  - `feat(core): PinpointPatcher.RebuildItemList 본문 교체 + Probe 정확화`
  - `chore: ItemDataFactory 폐기 (또는 본문 교체)`
  - `docs: v0.5.3 smoke 결과 PASS`
  - `chore(release): remove Probe code (D16 패턴)`
  - `chore(release): v0.5.3 — VERSION + README + HANDOFF`
- dist: `dist/LongYinRoster_v0.5.3.zip`
- GitHub release tag `v0.5.3`

---

## 9. v0.6+ 후보 (HANDOFF 갱신용)

v0.5.3 release 후 다음 sub-project (한 번에 한 sub-project):

| 후보 | v0.5.3 의 영향 |
|---|---|
| **창고** (`selfStorage.allItem`) | 인벤토리와 거의 동일 패턴 — ItemData ctor 재사용. selfStorage 별도 method (LoseAll 동등 없음) 만 발견 |
| **외형** (`faceData (HeroFaceData)` + `partPosture (PartPostureData)`) | sub-data wrapper graph 2개 동시 + sprite cache invalidate (skeleton graphic) |
| **무공 list 진도 보존** (`speEquipData / speUseData / extraAddData`) | v0.5.2 의 KungfuList 가 lv/fightExp/bookExp 만 복원. sub-data 보존 추가 |
| **인벤토리 sub-data 보존** (`equipmentData / medFoodData / etc`) | v0.5.3 가 itemID + count 만 복원. 강화도/옵션 보존 추가 |
| **UI cache invalidation 일반화** | v0.5.1 + v0.5.2 + v0.5.3 의 패턴을 framework 으로 추출 |

---

## 10. v0.5.3 의 결정 사항 (Q&A 요약)

| Q | A | 사유 |
|---|---|---|
| Q1: 첫 sub-project | A — 인벤토리 | v0.4 PoC A4 의 stub 코드 이미 존재 + v0.5.2 통찰 직접 적용 |
| Q2: 동작 정의 | B — Replace | v0.5.2 KungfuList 와 일관, mod 의도 일치 |
| Q3: process | Hybrid (v0.5.2 패턴 mirror) | 검증된 패턴 |
| Q4: 출하 단위 | v0.5.3 minor | sub-project 분해 원칙 |

---

## Appendix A — v0.4 PoC A4 의 ItemDataFactory stub (현재)

```csharp
// src/LongYinRoster/Core/ItemDataFactory.cs (v0.4 stub)
public static class ItemDataFactory
{
    public static bool IsAvailable => false;  // PoC A4 FAIL
    public static object Create(int itemID, int count)
    {
        throw new InvalidOperationException("ItemDataFactory not available — PoC A4 FAIL");
    }
}
```

v0.5.3 에서 폐기 (또는 본문 교체).
