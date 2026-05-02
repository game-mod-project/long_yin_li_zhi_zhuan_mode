# LongYin Roster Mod v0.5.2 — Design Spec

**일시**: 2026-05-02
**Scope**: 무공 list (KungfuList) Replace — slot 의 무공 list 로 player 완전 교체 (clear + add all). v0.5.1 active 의 N7 한계 (다른 캐릭터 active set 적용 불가) 자동 해소.
**선행 spec**:
- `2026-04-29-longyin-roster-mod-v0.3-design.md` (PinpointPatcher / Apply / Restore 기반)
- `2026-04-30-longyin-roster-mod-v0.4-design.md` (selection-aware 9-step pipeline + ApplySelection)
- `2026-05-01-longyin-roster-mod-v0.5-design.md` (PoC-driven dual track — 양쪽 FAIL)
- `2026-05-02-longyin-roster-mod-v0.5.1-design.md` (무공 active full — game 패턴 mirror)
**HANDOFF**: `docs/HANDOFF.md` §6.B — v0.6 통합 작업 후보
**v0.5.1 PoC artifact**: `docs/superpowers/dumps/2026-05-02-active-ui-refresh-spike.md`, `docs/superpowers/dumps/2026-05-02-v0.5.1-smoke.md`

---

## 1. Context

### 1.1 v0.5.1 까지의 도달점

- **무공 active 활성화** — slot 의 active 11-슬롯 set 을 player 에 정확히 복원 (game 패턴 mirror — `currentEquipped ∪ equipTargets` union pattern)
- **UI cache invalidate trigger** — 11-swap 으로 무공 패널 즉시 갱신
- **save → reload persistence** — 정확히 유지
- **천부 중복 추가 방지 fix** — `RebuildHeroTagData` 의 영구 천부 중복 검사
- **Tests**: 50/50 PASS

### 1.2 v0.5.1 의 N7 한계 — v0.5.2 의 동기

**v0.5.1 의 N7**: slot 의 active skillID 가 현재 player 의 무공 list 에 없으면 skip + warning. 다른 캐릭터의 active set 을 Apply 하면 `missing=N` 으로 모두 실패.

**v0.5.2 해소**: 무공 list 자체를 slot 으로 Replace 하면, list 가 정확해진 후 active set 도 매칭 → missing=0. v0.5.1 의 active 와 시너지.

### 1.3 v0.5.1 의 algorithm 통찰 transferability

**v0.5.1 핵심 통찰**:
- v0.5 Phase B Harmony trace 가 발견한 game 자체 패턴 (UnequipSkill × N → EquipSkill × M) 정확히 mirror
- `currentEquipped ∪ equipTargets` union 모두 unequip → equipTargets 만 equip — silent fail 회피 + UI cache invalidate trigger

**v0.5.2 적용**:
- 무공 list 도 game 자체 method (`LoseAllKungfu` / `LearnKungfu` 류) mirror
- clear all → add all (slot list 의 각 entry) — silent fail 회피
- v0.4 PoC A1 FAIL (KungfuSkillLvData wrapper ctor) 을 game-self method 로 우회

### 1.4 v0.5.2 process — Hybrid (v0.5.1 패턴 mirror)

1. **Spike Phase 1** (small, time-boxed) — game-self method 후보 발견 + clear + add 검증
2. **User gate** — spike 결과 보고 → spec impl 진행 / wrapper ctor 재도전 / abort
3. **Spec impl** — Spike PASS path 만 implementation
4. **Smoke + release** — v0.5.2 minor release

### 1.5 출하 단위

**v0.5.2 minor release** (`v0.5.2` tag) — sub-project 분해 원칙. 외형 / 인벤토리 / 창고는 별도 sub-project (v0.6.x).

---

## 2. Goals & Non-goals

### 2.1 v0.5.2 Goals

1. **무공 list Replace** — player 의 `kungfuSkills` list 를 slot 의 list 로 완전 교체 (clear + add all)
2. **lv / fightExp / bookExp 보존** — 각 무공의 학습 진도까지 slot 값으로 복원
3. **v0.5.1 active 의 N7 자동 해소** — 다른 캐릭터의 active set Apply 시 `missing=0`
4. **save → reload persistence** — 무공 list + active 모두 정확히 유지
5. **`Capabilities.KungfuList = true`** — impl PASS 후 enable
6. **새 ApplySelection 카테고리 `KungfuList`** — default off (보수적, 사용자 명시 토글)
7. **legacy 호환** — v0.3 / v0.4 / v0.5.1 슬롯 파일 무손실 (V03Default 자동 적용)
8. **회귀 검증** — v0.5.1 의 active / 정체성 / 천부 / 부상충성호감 / disabled 카테고리 모두 v0.5.1 baseline 동일

### 2.2 v0.5.2 Non-goals

| # | 항목 | 미루는 사유 |
|---|---|---|
| N1 | 외형 (`faceData` + `partPosture` sub-data wrapper graph) | v0.6.x 별도 sub-project |
| N2 | 인벤토리 / 창고 (`itemListData.allItem (ItemData[])` wrapper graph) | v0.6.x 별도 sub-project |
| N3 | UI cache invalidation 일반화 | v0.5.1 의 union pattern 이 미 framework. v0.6.x 의 별도 spec |
| N4 | 무공 list 의 Add only / Sync 옵션 | Replace 만 (의미 명확화). 다른 옵션은 v0.6+ |
| N5 | KungfuSkillLvData wrapper ctor (raw) | game-self method 우회 (Spike A approach). FAIL 시 user gate 결정 |
| N6 | Apply preview (dry-run) | v0.6+ |
| N7 | 자동 smoke harness | v0.6+ |

---

## 3. Architecture

### 3.1 Hybrid spike + spec + impl 흐름

```
[Spike Phase 1] 무공 list game-self method discovery
  ├─ Step 1: method dump (Lose/Learn/Add/Clear*Kungfu* 시그니처)
  ├─ Step 2: clear 후보 시도 → kungfuSkills.Count read-back
  ├─ Step 3: add 후보 시도 (skillID, lv) → new wrapper read-back
  ├─ Step 4: 통합 (clear + add all) → 무공 패널 UI + 스탯 변화
  └─ Step 5: save → reload persistence
        ↓
       G1 (clear + add + UI + persistence 모두 PASS)
        ↓
   ┌────┴────┐
   PASS    FAIL
    ↓        ↓
 [Impl]  [User gate]
            ↓
   ┌────────┼────────┐
   ↓        ↓        ↓
 wrapper  abort + 다른
 ctor 재   sub-project
 도전 (B)  변경

[Impl Phase] (Spike PASS 후)
  ├─ KungfuListApplier 작성
  ├─ PinpointPatcher.RebuildKungfuSkills 본문 교체 (현재 SkipKungfuSkills stub)
  ├─ Capabilities.KungfuList = true
  ├─ ApplySelection.KungfuList 필드 추가
  ├─ KoreanStrings.Cat_KungfuList = "무공 목록"
  ├─ SlotDetailPanel — 10 카테고리 grid 확장
  ├─ Unit tests 추가
  └─ Smoke 시나리오 1-3 + 회귀 (G2)
        ↓
     G3 release v0.5.2
```

### 3.2 Spike 결과 → release scope 매트릭스

| Spike | Wrapper ctor 재도전 | v0.5.2 scope | 후속 sub-project |
|---|---|---|---|
| PASS (game-self method) | (skip) | KungfuList Replace release | v0.6.x 외형 / 인벤토리 / 창고 |
| FAIL Step 1-5, ctor PASS | PASS | KungfuList Replace release | 동상 |
| FAIL all, abort | (skip) | release 없음 | sub-project 변경 |

### 3.3 코드 파일 영향 범위

**기존 패턴 재사용 — 신규 인프라 없음**:

| 영역 | 영향 | 변경 |
|---|---|---|
| Core | `KungfuListApplier.cs` (new) | new 파일 1 개 |
| Core | `PinpointPatcher.cs` | `RebuildKungfuSkills` 본문 교체 (현재 `SkipKungfuSkills` stub) |
| Core | `Capabilities.cs` | `KungfuList` flag 추가 |
| Core | `ApplySelection.cs` | `KungfuList` 필드 + JSON 직렬화 |
| Util | `KoreanStrings.cs` | `Cat_KungfuList = "무공 목록"` 추가 |
| UI | `SlotDetailPanel.cs` | 10 카테고리 grid (4 row × 3 col 또는 3 row × 4 col) |
| Slots | `SlotPayload.cs` / `SlotMetadata.cs` (필요 시) | (변경 없음 예상) |
| Tests | `KungfuListApplierTests.cs` (new) | 5 unit tests |
| Tests | `ApplySelectionTests.cs` | `KungfuList` round-trip 검증 |

---

## 4. Spike Phase 1 detail

### 4.1 입력 조건 (필수)

- **다른 캐릭터의 SaveSlot** (예: 강력한 무공 set 있는 NPC 또는 다른 캐릭터의 save) — 현재 캐릭터의 list 와 다른 set
- **현재 캐릭터의 list 백업** — Spike 진행 전 game save (slot 1 같은 자리에) 으로 보호. Spike FAIL 시 reload 로 복귀

### 4.2 Spike steps

#### Step 1 — Method dump

목적: `kungfuSkills` list manipulation 의 game-self method 후보 발견.

reflection scan: `HeroData.GetMethods()` 에서 정규식 매칭:
- `^(Lose|Learn|Add|Clear|Remove|Get|Drop)(All)?(Kungfu|Skill).*`
- 시그니처 dump (parameter types + return type)

후보 패턴:
- `LoseAllKungfu()`, `ClearAllKungfu()`, `RemoveAllKungfu()`
- `LearnKungfu(int skillID, int lv, ...)`, `LearnKungfu(int skillID, float fightExp, ...)`
- `AddKungfuSkill(int skillID, int lv, ...)` 또는 `AddKungfuSkill(KungfuSkillLvData wrapper, ...)`
- `GetKungfu(int skillID, int lv, ...)`

#### Step 2 — Clear method 시도

각 후보에 대해:
1. 현재 player 의 `kungfuSkills.Count` 기록 (예: 130)
2. method 호출
3. `kungfuSkills.Count` read-back — 0 또는 적은 수 (영구 무공 자동 보유 등)
4. 무공 패널 UI 변경 확인

#### Step 3 — Add method 시도

clear 한 후 (또는 reload 후):
1. 후보 method (skillID=특정 ID, lv=10) 호출
2. `kungfuSkills` 의 새 entry 의 `skillID / lv / fightExp` read-back
3. 무공 패널 UI 변경 확인

#### Step 4 — 통합 시나리오

다른 캐릭터의 SaveSlot 의 무공 list (예: 30개) → 현재 캐릭터에 적용:
1. clear method 호출 → kungfuSkills 비워짐 확인
2. slot list 의 각 entry 마다 add method 호출 (lv, fightExp 인자 포함)
3. 결과 list count = slot count 확인
4. active set (slot 의 equiped=true skillID) 도 적용 후 무공 패널 UI 확인

#### Step 5 — save → reload persistence

Step 4 PASS 후:
1. game save → 종료 → 재시작 → load
2. 무공 list count + 각 entry 의 skillID / lv / fightExp 정확히 유지 확인
3. active set 도 유지

### 4.3 PASS 기준

- Step 1-4 모두 작동 (clear PASS + add PASS + UI 갱신 PASS)
- Step 5 persistence PASS

### 4.4 FAIL → User gate

옵션:
1. **wrapper ctor 재도전 (B approach)** — `KungfuSkillLvData` ctor 또는 factory 발견. v0.4 A1 FAIL 의 재시도. IL2CPP 한계 우회 path 모색
2. **abort + sub-project 변경** — 외형 / 인벤토리 / 창고 등 다른 sub-project

### 4.5 Spike 시간 예산

- Step 1-5: 약 3-4 hour
- Wrapper ctor 재도전 추가: +3-4 hour
- 총 cap: 7-8 hour. 그 이상 시 abort

### 4.6 Spike artifact

- `docs/superpowers/dumps/2026-05-XX-kungfu-list-spike.md` — Step 별 결과, 사용자 보고, decision

---

## 5. Implementation 설계 (Spike PASS 후)

### 5.1 KungfuListApplier

**새 파일**: `src/LongYinRoster/Core/KungfuListApplier.cs`

```
public static class KungfuListApplier {
    public sealed class Result {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int RemovedCount { get; set; }
        public int AddedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public static IReadOnlyList<KungfuEntry> ExtractKungfuList(JsonElement slot);
    public static Result Apply(object? player, JsonElement slot, ApplySelection sel);
    public static Result Restore(object? player, JsonElement backup);
}

public sealed record KungfuEntry(int SkillID, int Lv, float FightExp, float BookExp);
// Spike 결과에 따라 추가 필드 (belongHeroID 등) 확장 가능
```

**Apply 알고리즘** (Spike PASS path 따라):

1. Selection check: `sel.KungfuList == false` → skip
2. slot 의 `kungfuSkills` 추출 → `targetEntries`
3. game-self clear method 호출 (Spike Step 2 발견)
4. `targetEntries` 의 각 entry 에 game-self add method 호출 (skillID, lv, fightExp 등)
5. read-back 검증 — `kungfuSkills.Count == targetEntries.Count`
6. UI refresh trigger (Spike PASS path 따라 — clear + add 자체로 trigger 또는 flag toggle)

**Restore 알고리즘**: backup JSON 으로 Apply 동일 로직.

### 5.2 PinpointPatcher.RebuildKungfuSkills 본문 교체

현재 `SkipKungfuSkills(res)` stub:
```csharp
private static void SkipKungfuSkills(ApplyResult res)
{
    res.SkippedFields.Add("kungfuSkills — collection rebuild deferred to v0.5+");
}
```

v0.5.2 교체:
```csharp
private static void RebuildKungfuSkills(JsonElement slot, object player, ApplySelection selection, ApplyResult res)
{
    if (!selection.KungfuList) { res.SkippedFields.Add("kungfuList (selection off)"); return; }
    if (!Probe().KungfuList)   { res.SkippedFields.Add("kungfuList (capability off)"); return; }

    var r = KungfuListApplier.Apply(player, slot, selection);
    if (r.Skipped) { res.SkippedFields.Add($"kungfuList — {r.Reason}"); return; }
    res.AppliedFields.Add($"kungfuList (removed={r.RemovedCount} added={r.AddedCount} failed={r.FailedCount})");
}
```

PinpointPatcher.Apply 의 step 순서: `RebuildKungfuSkills` 가 `SetActiveKungfu` 보다 **먼저** 호출되어야 함 — list 가 정확해진 후 active 매칭.

### 5.3 Step 순서 변경

v0.5.1 의 step 순서:
```
SetSimpleFields → SetIdentityFields → SetActiveKungfu → RebuildItemList → RebuildSelfStorage → RebuildKungfuSkills (skip) → RebuildHeroTagData → RefreshSelfState → RefreshExternalManagers
```

v0.5.2 변경:
```
SetSimpleFields → SetIdentityFields → RebuildKungfuSkills (NEW step) → SetActiveKungfu → RebuildItemList → RebuildSelfStorage → RebuildHeroTagData → RefreshSelfState → RefreshExternalManagers
```

`RebuildKungfuSkills` 가 `SetActiveKungfu` 직전. list 정확화 후 active 매칭.

### 5.4 Capabilities + ApplySelection

`Capabilities.cs`:
```
public bool KungfuList { get; init; }
```
`AllOff` / `AllOn` / `ToString` 갱신.

`ApplySelection.cs`:
```
public bool KungfuList { get; set; } = false;  // default off
```
`V03Default` / `RestoreAll` / `AnyEnabled` / `ToJson` / `FromJson` / `FromJsonElement` 갱신.

### 5.5 KoreanStrings + UI

`KoreanStrings.cs`:
```
public const string Cat_KungfuList = "무공 목록";
```

`SlotDetailPanel.cs`:
- 9 → 10 카테고리. grid 확장:
  - Option a: 4 row × 3 col (12 slot, 2 빈 칸) — row 4 에 KungfuList 추가
  - Option b: 3 row × 4 col (12 slot, 2 빈 칸) — col 4 에 추가
  - Option c: 3 row × 3 col + 4번째 row (1 column) — KungfuList 만 단독
- **권장**: Option a (4 row × 3 col) — 기존 grid 자연 확장

### 5.6 Slot schema

**변경 없음**:
- `_meta.applySelection.kungfuList` flag (default false)
- legacy 슬롯 자동 호환 (V03Default 적용)

### 5.7 Tests

**새 unit tests** (`src/LongYinRoster.Tests/KungfuListApplierTests.cs`):
- `ExtractKungfuList_ReturnsAllEntries` — slot JSON 의 kungfuSkills 모두 추출
- `ExtractKungfuList_HandlesEmptyList` — kungfuSkills [] 처리
- `ExtractKungfuList_MissingKungfuSkills_ReturnsEmpty` — kungfuSkills 필드 없음 처리
- `Apply_RespectsApplySelection_SkipsWhenFalse` — sel.KungfuList == false 시 no-op
- `Apply_HandlesMissingPlayer` — player null (test mode) 시 skip

**ApplySelectionTests** 확장: `KungfuList` round-trip + V03Default + RestoreAll 검증

**총 tests**: 50 → 56+

### 5.8 Plugin.cs VERSION

`0.5.1` → `0.5.2`

---

## 6. Smoke 시나리오 (검증)

### 6.1 시나리오 1 — 다른 캐릭터 무공 set Apply

1. Pre: 강력한 무공 set 있는 다른 캐릭터의 SaveSlot N → game load → mod slot 1 capture
2. 현재 캐릭터의 SaveSlot M load (다른 무공 list)
3. mod F11 → slot 1 → ✓ KungfuList + ✓ ActiveKungfu → ▼ 덮어쓰기
4. 무공 패널 → list 가 slot 1 의 set 으로 변경 + active 도 정확히
5. 스탯 변화 (전투력 등) 반영
6. game save → reload → list + active 유지

### 6.2 시나리오 2 — Self-Apply (같은 캐릭터)

1. SaveSlot N (현재 캐릭터) load → mod slot 1 capture
2. 게임에서 무공 일부 변경 (새 무공 학습 또는 기존 무공 lv up)
3. mod F11 → slot 1 → ✓ KungfuList → ▼ Apply (자동백업 → slot 0)
4. list 가 slot 1 시점의 진도로 복원 (lv / fightExp 정확)

### 6.3 시나리오 3 — Restore (slot 0 자동백업)

1. 시나리오 2 의 step 3 직후 (slot 0 = 변경된 list)
2. mod slot 0 → ↶ 복원
3. list 가 변경된 시점 (자동백업) 으로 복귀

### 6.4 회귀 시나리오

- v0.5.1 의 active 동작 유지 (KungfuList off + ActiveKungfu on)
- 천부 / 정체성 / 스탯 / 명예 / 스킨 / 자기집 동작 유지
- 외형 / 인벤토리 / 창고 disabled 표시 유지
- legacy 슬롯 호환 (v0.3/v0.4/v0.5.1) — V03Default 자동 적용

### 6.5 G2 PASS 기준

시나리오 1, 2, 3 모두 PASS + 회귀 모두 PASS.

---

## 7. Failure mode / Out

| 단계 | FAIL 시 |
|---|---|
| Spike Step 1-5 모두 FAIL | user gate → wrapper ctor 재도전 / abort |
| Wrapper ctor FAIL | abort, foundation 보존 (`Capabilities.KungfuList = false` 유지), dump report, sub-project 변경 |
| Impl unit test FAIL | bug fix → 재검증 |
| Smoke 시나리오 1-3 중 어느 하나라도 FAIL | release 안 함, `Capabilities.KungfuList = false` revert, dump |
| 회귀 FAIL | release 안 함, regression fix 또는 revert |
| Impl PASS, smoke PASS, 회귀 PASS | release v0.5.2 |

---

## 8. Release / Git plan

### 8.1 Branch + commits

- Branch: `v0.5.2` (main 에서 분기)
- Commits:
  - `spike(v0.5.2): Probe + F12 trigger — game-self method dump + clear/add 시도`
  - `spike(v0.5.2): UI cache + persistence 결과`
  - `feat(core): KungfuListApplier — clear + add all + UI refresh + 5 tests`
  - `feat(core): PinpointPatcher.RebuildKungfuSkills 본문 교체 + step 순서 변경`
  - `feat(capabilities): enable KungfuList`
  - `feat(slots): ApplySelection.KungfuList field`
  - `feat(ui): SlotDetailPanel 10 카테고리 grid 확장`
  - `test: KungfuList + ApplySelection 회귀`
  - `docs: v0.5.2 smoke PASS`
  - `chore(release): remove Probe code (D16 패턴)`
  - `chore(release): v0.5.2 — VERSION + README + HANDOFF`

### 8.2 VERSION

`0.5.1` → `0.5.2`

### 8.3 dist + tag

- `dist/LongYinRoster_v0.5.2/` 디렉토리
- `dist/LongYinRoster_v0.5.2.zip`
- GitHub release tag `v0.5.2`

### 8.4 README + HANDOFF

- README: v0.5.2 highlights + Releases 표 entry + 10 카테고리 표 갱신
- HANDOFF: §1 main baseline = v0.5.2, §2 git history v0.5.2 commits, §6 v0.6 후보 갱신 (외형 / 인벤토리 / 창고 / UI cache 일반화)

---

## 9. v0.6+ 후보 (HANDOFF 갱신용)

v0.5.2 release 후 다음 sub-project (한 번에 한 sub-project):

| 후보 | v0.5.2 의 영향 |
|---|---|
| **외형** (`faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data wrapper graph) | sub-data wrapper graph 도전. v0.5.1 의 `HeroIconDirty/heroIconDirtyCount` UI cache trigger 적용 가능 |
| **인벤토리** (`itemListData.allItem (ItemData[])` wrapper graph) | v0.5.2 의 game-self method 발견 패턴 transferable. `LoseAllItem` / `GetItem` 이미 PinpointPatcher 에 분기 코드 존재 |
| **창고** (`selfStorage.allItem`) | 인벤토리와 동일 패턴 |
| **UI cache invalidation 일반화** | v0.5.1 + v0.5.2 의 union pattern + clear/add pattern 을 framework 으로 추출 |

---

## 10. v0.5.2 의 결정 사항 (Q&A 요약)

| Q | A | 사유 |
|---|---|---|
| Q1: 첫 sub-project | B — 무공 list | v0.5.1 통찰 가장 직접 활용 + active 시너지 |
| Q2: 동작 정의 | B — Replace | mod 핵심 의도 ("이 슬롯 캐릭터로 만들기") + 자동백업으로 안전 |
| Q3: process | Hybrid (v0.5.1 패턴 mirror) | binary PASS/FAIL 회피 |
| Q4: 출하 단위 | v0.5.2 minor | sub-project 분해 원칙 |
| Q5: spike protocol | adaptive (v0.5.1 패턴 mirror) | user gate 보존 |

---

## Appendix A — KungfuSkillLvData wrapper shape (v0.5 발견)

```
property: Boolean equiped
property: Int32 skillID            ← 진짜 ID 필드
property: Int32 lv
property: Single fightExp
property: Single bookExp
property: Boolean isNew
property: Int32 belongHeroID
property: HeroSpeAddData speEquipData / speUseData / extraAddData
property: Single equipUseSpeAddValue / damageUseSpeAddValue / selfUseSpeAddValue / enemyUseSpeAddValue
property: Single cdTimeLeft / activeTimeLeft / power / battleDamageCount
property: Int32 useTime
property: Boolean skillIconDirty
property: Boolean maxManaChanged
```

v0.5.2 의 game-self add method 가 받을 수 있는 인자 후보:
- `(int skillID, int lv)` — 최소
- `(int skillID, int lv, float fightExp, float bookExp)` — 진도 보존
- `(KungfuSkillLvData wrapper)` — 전체 — wrapper ctor 한계

Spike 결과에 따라 결정.
