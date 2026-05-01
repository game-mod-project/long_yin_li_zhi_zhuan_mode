# LongYin Roster Mod v0.5.1 — Design Spec

**일시**: 2026-05-02
**Scope**: 무공 active full — `kungfuSkills[i].equiped` 11-슬롯 swap + UI cache invalidation discovery + save→reload persistence
**선행 spec**:
- `2026-04-29-longyin-roster-mod-v0.3-design.md` (PinpointPatcher / Apply / Restore 기반)
- `2026-04-30-longyin-roster-mod-v0.4-design.md` (selection-aware 9-step pipeline + ApplySelection)
- `2026-05-01-longyin-roster-mod-v0.5-design.md` (PoC-driven dual track — 양쪽 FAIL)
**HANDOFF**: `docs/HANDOFF.md` §6.B — v0.6 통합 작업 후보
**v0.5 PoC artifact**:
- `docs/superpowers/dumps/2026-05-01-active-kungfu-diff.md`
- `docs/superpowers/dumps/2026-05-01-active-kungfu-trace.md`
- `docs/superpowers/dumps/2026-05-01-active-kungfu-poc-result.md`
- `docs/superpowers/dumps/2026-05-01-portrait-poc-result.md`
- `docs/superpowers/dumps/2026-05-01-v0.5-poc-report.md`

---

## 1. Context

### 1.1 v0.5 PoC 결정적 발견 (v0.5.1 자산)

**무공 active 영역 — Phase A/B/C 결과**:

- **Source-of-truth**: `kungfuSkills[i].equiped` (NOT `nowActiveSkill` — v0.4 A3 PoC FAIL 의 진짜 원인)
- **ID 필드**: `KungfuSkillLvData.skillID` (NOT `kungfuID` — wrapper.kungfuID 는 모두 -1 fallback)
- **Method path**: `HeroData.EquipSkill(KungfuSkillLvData wrapper, bool=true)` + `HeroData.UnequipSkill(KungfuSkillLvData wrapper, bool=true)`
- **Game 자체 swap 패턴**: 사용자가 active 변경 시 `UnequipSkill(wrapper, true) × 11` → `EquipSkill(wrapper, true) × 11` 시퀀스 (Phase B Harmony trace)
- **wrapper shape**: `equiped (bool)`, `skillID (int)`, `lv`, `fightExp`, `bookExp`, `belongHeroID`, `speEquipData / speUseData / extraAddData (HeroSpeAddData)`, `cdTimeLeft / activeTimeLeft / power / battleDamageCount`, `skillIconDirty / maxManaChanged` flags

### 1.2 v0.5 미해결 — v0.5.1 의 도전

**Phase C 의 G3 보수적 FAIL 사유**:
1. **read-back PASS, 게임 UI 미반영** — F12 후 무공 패널의 active 슬롯 변경 안 보임 (v0.4 외형 PoC 와 동일 패턴 — "method 호출됐으나 sprite/UI cache invalidate 별도 필요")
2. **save → reload persistence 미검증** — 사용자가 직접 비운 시나리오만 시도 → PoC 입력 조건 불성립

**v0.5.1 의 두 핵심 unknown**:
- **UI refresh path**: `KungfuSkillLvData.skillIconDirty / maxManaChanged` flag toggle, `HeroData.HeroIconDirty / heroIconDirtyCount` flag toggle, 또는 game-self refresh method 발견
- **save → reload persistence**: active 11 채워진 상태 + mod swap + save + reload 시나리오 검증

### 1.3 v0.5.1 의 새 접근 — Hybrid spike + spec + impl

**v0.5 의 PoC-first dual-track 양쪽 FAIL 의 교훈**:
- 한 번에 한 sub-project 만 (dual-track 회피)
- spike → 결과 따라 spec scope 자동 조정 (binary PASS/FAIL 회피)

**v0.5.1 의 process**:
1. **Spike Phase 1 (small, time-boxed)** — flag toggle + 11-swap + persistence 검증
2. **User gate** — spike 결과 보고 → spec impl 진행 vs trace round 2 진행 vs abort
3. **Spec impl** — spike PASS path 만 implementation
4. **Smoke + release** — v0.5.1 minor release

**v0.4 / v0.5 와의 연속성**:
- v0.4 의 PinpointPatcher 9-step pipeline 끝의 RefreshExternalManagers 직전에 ActiveKungfuApplier step 삽입 (총 10-step)
- ApplySelection schema 변경 없음 (`activeKungfu` flag 는 v0.4 부터 존재)
- Capabilities flag (`Capabilities.ActiveKungfu`) 토글로 enable
- 신규 인프라 없음 — 기존 패턴에 step 추가만

### 1.4 출하 단위

**v0.5.1 minor release** (`v0.5.1` tag) — sub-project 분해 원칙. 외형 / 무공 list / 인벤토리 / 창고는 별도 sub-project (v0.6.x).

---

## 2. Goals & Non-goals

### 2.1 v0.5.1 Goals

1. **UI refresh path 발견** — game 무공 패널이 mod swap 후 즉시 갱신
2. **Active full Apply** — slot JSON 의 `kungfuSkills[].equiped==true` 의 `skillID` set 을 현재 player 에 정확히 복원
3. **Active full Restore** — slot 0 자동백업 → 현재 player 의 active state 복원 (Apply 의 mirror)
4. **Save → reload persistence** — 3 시나리오 모두 PASS (§5)
5. **`Capabilities.ActiveKungfu = true`** — impl PASS 후 enable
6. **`SlotDetailPanel`** — `Cat_ActiveKungfu (deferred)` disabled label 제거, 정상 enable
7. **legacy 호환** — v0.3 / v0.4 슬롯 파일 무손실 (V03Default 자동 적용 그대로)
8. **회귀 검증** — v0.4 의 정체성 9 필드 / 천부 17/17 / 부상충성호감 영구 보존 / disabled 카테고리 (외형 / 인벤토리 / 창고) 모두 v0.4 baseline 과 동일 동작

### 2.2 v0.5.1 Non-goals

| # | 항목 | 미루는 사유 |
|---|---|---|
| N1 | 외형 (`faceData` + `partPosture` sub-data wrapper graph) | v0.6.x 별도 sub-project |
| N2 | 무공 list (`kungfuSkills` 자체 entry 추가/제거, `KungfuSkillLvData` ctor / factory / Add method) | v0.6.x 별도 sub-project |
| N3 | 인벤토리 (`itemListData.allItem (ItemData[])` wrapper graph) | v0.6.x 별도 sub-project |
| N4 | 창고 (`selfStorage.allItem (ItemData[])`) | v0.6.x 별도 sub-project |
| N5 | UI cache invalidation 일반화 | v0.5.1 spike 결과를 v0.6.x 의 startpoint 로 사용. 일반화 자체는 별도 spec |
| N6 | 새 UI 추가 | v0.4 의 active 체크박스 그대로 사용 (disabled → enable 토글만) |
| N7 | active list 의존성 자동 동기화 | slot 의 active skillID 가 현재 player 의 무공 list 에 없으면 skip + warning. 자동 추가는 v0.6.x 무공 list sub-project 와 묶음 |
| N8 | Apply preview (dry-run) | v0.6+ |
| N9 | 자동 smoke harness | v0.6+ |
| N10 | v0.5 foundation revert | `Capabilities.Appearance / FieldCategory.Appearance / ApplySelection.Appearance / KoreanStrings.Cat_Appearance` 보존 — v0.6.x 외형 sub-project 의 prerequisite |

---

## 3. Architecture

### 3.1 Hybrid spike + spec + impl 흐름

```
[Spike Phase 1] active UI refresh discovery (small, time-boxed)
  ├─ Step 1: 1-회 swap baseline 재확인 (v0.5 PoC 와 동일)
  ├─ Step 2: 11-회 swap (Approach A 게임 자체 패턴 mimic)
  ├─ Step 3: 11-회 swap + flag toggle
  │            (skillIconDirty + maxManaChanged + HeroIconDirty + heroIconDirtyCount++)
  └─ Step 4: save → reload persistence 검증
        ↓
       G1 (UI 즉시 갱신 + persistence PASS)
        ↓
   ┌────┴────┐
   PASS    FAIL
    ↓        ↓
 [Impl]  [User gate]
            ↓
   ┌────────┼────────┐
   ↓        ↓        ↓
 Trace   Symbol    abort
 round 2 scan      (sub-project 변경)
   ↓        ↓
 PASS → [Impl]
 FAIL → maintenance (foundation 보존)

[Impl Phase] (Spike PASS 후)
  ├─ ActiveKungfuApplier 작성
  ├─ PinpointPatcher Step 8 wiring
  ├─ Capabilities.ActiveKungfu = true
  ├─ SlotDetailPanel disabled label 제거
  ├─ Unit tests 추가
  └─ Smoke 시나리오 1-3 (G2)
        ↓
     G3 release v0.5.1
```

### 3.2 Spike 결과 → release scope 매트릭스

| Spike | Trace round 2 | v0.5.1 scope | 후속 sub-project |
|---|---|---|---|
| PASS (Step 2 또는 3) | (skip) | active full release | v0.6.x 외형 / 무공 list / 인벤토리 (Spike PASS path 가 외형의 sprite refresh 에 transferable 한지 확인) |
| FAIL Step 1-4, Trace PASS | PASS | active full release | 동상 |
| FAIL Step 1-4, Trace FAIL | FAIL | release 없음 | maintenance 모드 + dump report. v0.5 패턴 mirror. foundation 보존 (`Capabilities.ActiveKungfu = false` 유지) |
| FAIL Step 1-4, abort | (skip) | release 없음 | sub-project 변경 (외형 또는 인벤토리 등) |

### 3.3 코드 파일 영향 범위

**기존 패턴 재사용 — 신규 인프라 없음**:

| 영역 | 영향 | 변경 |
|---|---|---|
| Core | `ActiveKungfuApplier.cs` (new) | new 파일 1 개 |
| Core | `PinpointPatcher.cs` | Step 8 추가 (호출만) |
| Util | `Capabilities.cs` | `ActiveKungfu = true` |
| Util | `KoreanStrings.cs` | `Cat_ActiveKungfu_Disabled` 제거 또는 unused 처리 |
| UI | `SlotDetailPanel.cs` | active 체크박스 disabled flag 제거 |
| Slots | (없음) | schema 변경 없음 |
| Tests | `ActiveKungfuApplierTests.cs` (new) | 5 unit tests |

---

## 4. Spike Phase 1 detail

### 4.1 입력 조건 (필수)

- **active 11 슬롯 채워진 game state** — v0.5 PoC 의 입력 조건 불성립 회피 (사용자가 직접 비운 시나리오만 시도하여 PoC 가 실효성 없는 결과로 끝난 일이 있었음)
- **별도 SaveSlot 2 개** — SaveSlot1 = active set X (11 채움), SaveSlot2 = active set Y (다른 11)
- **slot 1 capture** — SaveSlot1 load → mod 의 [+] capture → slot 1
- **slot 2 capture** — SaveSlot2 load → mod 의 [+] capture → slot 2

### 4.2 Spike steps (cheap → expensive)

#### Step 1 — 1-회 swap baseline 재확인

목적: v0.5 PoC 의 read-back PASS 가 현재 build 에서도 재현되는지 확인.
- player active 11 채워진 상태에서 1-회 swap (UnequipSkill idx X + EquipSkill idx Y, 둘 다 다른 wrapper)
- read-back: 변경된 wrapper 의 `equiped` flag 확인
- F12 후 게임 무공 패널 UI 변경 보이는지 사용자 확인 (예상: NO — v0.5 와 동일)

#### Step 2 — 11-회 swap (Approach A 게임 자체 패턴 mimic)

목적: game 자체가 11 swap 으로 trigger 하는 UI refresh 가 1 회 swap 로는 부족했던 것인지 확인.
- 모든 equiped wrapper 에 `UnequipSkill(wrapper, true)` × 11 호출
- slot 의 active set 의 wrapper 들에 `EquipSkill(wrapper, true)` × 11 호출
- read-back + F12 후 UI 변경 사용자 확인

#### Step 3 — 11-회 swap + flag toggle

목적: flag dirty 가 UI refresh trigger 인지 확인.
- Step 2 의 swap 후
- `KungfuSkillLvData.skillIconDirty = true` (각 변경된 wrapper)
- `KungfuSkillLvData.maxManaChanged = true` (각 변경된 wrapper)
- `HeroData.HeroIconDirty = true`
- `HeroData.heroIconDirtyCount++`
- F12 후 UI 변경 사용자 확인

#### Step 4 — save → reload persistence 검증

목적: data layer 변경이 game save 에 반영되는지 확인.
- Step 2 또는 3 PASS 후
- 게임 메뉴로 save → 게임 종료 → 게임 재시작 → save load
- player 의 active set 이 swap 후 set 인지 사용자 확인

### 4.3 PASS 기준

- **Spike PASS**: Step 2 또는 Step 3 에서 게임 무공 패널 UI 즉시 갱신 + Step 4 의 save→reload persistence PASS
- **Spike FAIL**: Step 1-3 모두 UI 갱신 안 됨 OR Step 4 persistence FAIL

### 4.4 FAIL → User gate

Spike FAIL 시 dump report 작성 후 user 에게 결정 요청:

| 옵션 | 흐름 |
|---|---|
| **Trace round 2 진행** | Harmony trace 로 game UI 가 native active 변경 후 호출하는 method 추적 (3-4 hour) — `Refresh*Kungfu*`, `Refresh*Skill*`, `Refresh*Panel*`, `Update*Skill*`, `Reload*Skill*` 후보 |
| **Symbol scan** | Assembly-CSharp.dll 의 `Refresh.*Skill / Refresh.*Kungfu / Refresh.*Panel / Refresh.*Fight / Update.*Skill` 류 method 전수 dump 후 zero-arg 후보 시도 (2-3 hour) |
| **abort + sub-project 변경** | foundation 보존, dump report, sub-project 변경 결정 받음 (외형 또는 인벤토리 등). v0.5 패턴 mirror |

### 4.5 Spike 시간 예산

- Step 1-4 합쳐 약 2-3 hour
- Trace round 2 또는 Symbol scan 추가 시 +3-4 hour
- 총 cap: 6-7 hour. 그 이상 시 abort 권장

### 4.6 Spike artifact

- `docs/superpowers/dumps/2026-05-XX-active-ui-refresh-spike.md` — Step 별 결과, 사용자 보고, 결정 (PASS/FAIL/Trace/abort)

---

## 5. Implementation 설계 (Spike PASS 후)

### 5.1 ActiveKungfuApplier

**새 파일**: `src/LongYinRoster/Core/ActiveKungfuApplier.cs`

```
public static class ActiveKungfuApplier {
    public static void Apply(HeroData player, JsonElement slotPlayer, ApplySelection sel);
    public static void Restore(HeroData player, JsonElement backupPlayer);
}
```

**Apply 알고리즘** (game 자체의 11-swap 패턴 mirror — Phase B trace 결과):

1. **Selection 검사**: `sel.ActiveKungfu == false` → 즉시 return (no-op)
2. **Slot active skillID 수집**: slot JSON 의 `kungfuSkills` 배열 traverse → `equiped == true` 인 entry 의 `skillID` 수집 (max 11 개) → `targetSkillIDs`
3. **Equip target wrapper 매칭**: 현재 player 의 `kungfuSkills` traverse → `targetSkillIDs` 와 매칭되는 wrapper reference 수집 → `equipTargets`
   - 매칭 실패 (slot 의 skillID 가 현재 list 에 없음) → 로그 + skip (N7 — list 의존성 자동 동기화는 v0.6.x sub-project)
4. **현재 equiped wrapper 수집**: 현재 player 의 `kungfuSkills` traverse → `equiped == true` 인 wrapper reference 수집 → `currentEquipped`
5. **Unequip phase**: `currentEquipped` 의 각 wrapper 에 `HeroData.UnequipSkill(wrapper, true)` 호출 (전체 unequip — slot active 와 겹치는 wrapper 도 포함하여 game 자체 패턴 mirror)
6. **Equip phase**: `equipTargets` 의 각 wrapper 에 `HeroData.EquipSkill(wrapper, true)` 호출 (slot active set 만 equip)
7. **UI refresh trigger** (Spike PASS path 따라):
   - flag toggle path → 변경된 wrapper 에 `skillIconDirty = true` + `maxManaChanged = true`, player 에 `HeroIconDirty = true` + `heroIconDirtyCount++`
   - game-self method path → trace round 2 / symbol scan 발견 method 호출

**Restore 알고리즘**: backup JSON 으로 Apply 동일 로직 — backup 의 `kungfuSkills[].equiped==true` set 을 현재 player 에 복원.

### 5.2 PinpointPatcher Step 8 wiring

`src/LongYinRoster/Core/PinpointPatcher.cs` 에 Step 8 추가:

```
// Step 8: ActiveKungfu (v0.5.1)
if (sel.ActiveKungfu) {
    ActiveKungfuApplier.Apply(player, slotPlayer, sel);
}
```

**Step 순서** (v0.4 의 9-step 위에 추가):
- Step 1-7: 기존 v0.4 (SimpleFieldMatrix, IdentityFieldMatrix, heroTagData, RebuildSkill, RefreshSelfState 등)
- **Step 8 (new)**: ActiveKungfuApplier
- Step 9: RefreshExternalManagers (기존 v0.3)

### 5.3 Capabilities flag

`src/LongYinRoster/Util/Capabilities.cs`:
- `public static bool ActiveKungfu { get; } = true;` (v0.4: false → v0.5.1: true)
- 다른 capability flag 변경 없음 (Appearance / Inventory / Storage 그대로 false)

### 5.4 SlotDetailPanel 변경

`src/LongYinRoster/UI/SlotDetailPanel.cs`:
- active 카테고리 체크박스의 disabled 분기 제거 (`Capabilities.ActiveKungfu == true` → enable)
- `Cat_ActiveKungfu` label 표시 — `Cat_ActiveKungfu_Disabled` 분기 제거

`src/LongYinRoster/Util/KoreanStrings.cs`:
- `Cat_ActiveKungfu_Disabled` 제거 (또는 unused 처리, 추후 cleanup)

### 5.5 Slot schema

**변경 없음**:
- `_meta.applySelection.activeKungfu` flag 는 v0.4 부터 존재
- Default value (V03Default) 변경 없음 — legacy 슬롯 자동 호환 유지

### 5.6 Tests

**새 unit tests** (`src/LongYinRoster.Tests/ActiveKungfuApplierTests.cs`):
- `Apply_ExtractsEquippedSkillIDs_FromSlotJson` — slot JSON 의 `kungfuSkills[].equiped==true` 의 `skillID` 추출 정확성
- `Apply_HandlesEmptyActiveSet` — slot active 0 entry (모두 false) 처리
- `Apply_HandlesDuplicateSkillID` — defensive (이론상 없지만)
- `Apply_RespectsApplySelection_SkipsWhenFalse` — `sel.ActiveKungfu == false` 시 no-op
- `Apply_HandlesMissingSkillID_InCurrentList` — slot 의 skillID 가 현재 list 에 없으면 skip + warning (N7)

**회귀**:
- `ApplySelectionTests` — `ActiveKungfu` flag default behavior 검증
- 기존 40 unit tests all pass 유지

**총 tests**: 45 → 50+

**IL2CPP 한계**: 게임 측 호출 (`HeroData.EquipSkill` / `UnequipSkill` / flag toggle) 은 mock 불가 — smoke 로만 검증.

---

## 6. Smoke 시나리오 (검증)

### 6.1 시나리오 1 — SaveSlot 다른 set Apply

**목적**: 다른 active set 의 slot 을 Apply → save → reload 후 정확히 유지.

1. Pre: SaveSlot1 (active set X, 11 채움) → mod capture → slot 1
2. Pre: SaveSlot2 (active set Y, 다른 11) → mod capture → slot 2
3. SaveSlot2 game load (active = Y)
4. mod F11 → slot 1 selection → Apply → confirm
5. F12 후 게임 무공 패널 사용자 확인 → active = X 표시
6. game save → game 종료 → game 재시작 → save load
7. **PASS 기준**: active = X (slot 1 set) 정확히 유지

### 6.2 시나리오 2 — 부분 unequip 후 Apply

**목적**: 부분 unequip 상태에서도 Apply 가 full set 을 정확히 복원.

1. Pre: SaveSlot1 → mod capture → slot 1 (active set X)
2. SaveSlot1 game load → 게임 무공 패널에서 active 일부 (예: 5 개) unequip
3. mod F11 → slot 1 → Apply (자동백업 → slot 0)
4. F12 후 active = X (전체 11) 표시
5. game save → reload → active = X 정확히 유지
6. **PASS 기준**: active = X (slot 1 set, 11) 정확히 유지

### 6.3 시나리오 3 — Restore (slot 0 자동백업)

**목적**: Apply 직전 자동백업으로부터 Restore.

1. 시나리오 2 의 step 3 실행 (Apply → slot 0 자동백업 = 부분 unequip 상태)
2. mod F11 → Restore → confirm
3. F12 후 active = step 2 의 부분 unequip 상태 (5 개) 표시
4. game save → reload → active = 부분 unequip 상태 유지
5. **PASS 기준**: active = 자동백업 시점 set 정확히 유지

### 6.4 회귀 시나리오 (v0.4 baseline 동작 유지)

- 정체성 9 필드 Apply → save → reload PASS (v0.4 D15 와 동일)
- 천부 17/17 Apply PASS
- 부상/충성/호감 영구 보존 (Apply 후 변경되지 않음)
- 외형 / 인벤토리 / 창고 disabled 카테고리 표시 유지 (v0.4 와 동일)
- legacy 슬롯 (v0.3 / v0.4) 자동 호환

### 6.5 G2 PASS 기준

시나리오 1, 2, 3 모두 PASS + 회귀 시나리오 모두 PASS.

---

## 7. Failure mode / Out

| 단계 | FAIL 시 |
|---|---|
| Spike Step 1-4 모두 FAIL | user gate → trace round 2 OR symbol scan 진행 vs abort |
| Trace round 2 + Symbol scan 모두 FAIL | abort, foundation 보존 (`Capabilities.ActiveKungfu = false` 유지), dump report (`docs/superpowers/dumps/2026-05-XX-active-kungfu-v0.5.1-fail.md`), HANDOFF §6 업데이트, sub-project 변경 user 결정 |
| Impl 진행 중 unit test FAIL | bug fix → 재검증 |
| Smoke 시나리오 1-3 중 어느 하나라도 FAIL | release 안 함, `Capabilities.ActiveKungfu = false` 로 revert, dump report, 원인 분석 후 재시도 또는 sub-project 변경 결정 |
| 회귀 시나리오 FAIL | release 안 함, regression 원인 분석 + fix 또는 revert |
| Impl PASS, smoke PASS, 회귀 PASS | `Capabilities.ActiveKungfu = true` commit + v0.5.1 release |

---

## 8. Release / Git plan

### 8.1 Branch 전략

- **Branch**: `v0.5.1` (main 에서 분기)
- main 의 v0.4.0 baseline 유지 (PoC FAIL / abort 시 main 영향 없음)

### 8.2 Commits 흐름

| 단계 | commit 메시지 | 비고 |
|---|---|---|
| Spike | `spike(v0.5.1): active UI refresh discovery — Phase 1 결과` | dump 파일 추가 (PASS path 명시) |
| Impl | `feat(core): ActiveKungfuApplier — 11-swap + UI refresh` | 새 파일 |
| Impl | `feat(core): PinpointPatcher Step 8 — ActiveKungfu wiring` | |
| Impl | `feat(capabilities): enable ActiveKungfu` | flag = true |
| Impl | `feat(ui): SlotDetailPanel — ActiveKungfu enable` | disabled 라벨 제거 |
| Test | `test: ActiveKungfuApplier unit tests` | 5 tests |
| Smoke | `docs: v0.5.1 smoke 결과 PASS — 시나리오 1/2/3 + 회귀` | dump |
| Release | `chore(release): v0.5.1 — VERSION bump + README/HANDOFF update` | 0.4.0 → 0.5.1 |

### 8.3 VERSION

- 현재: `0.4.0`
- v0.5.1 release 시: `0.5.1`
- (v0.5.0 은 PoC FAIL 로 release 안 됨)

### 8.4 dist / release artifact

- dist: `dist/LongYinRoster_v0.5.1/` 디렉터리
- zip: `dist/LongYinRoster_v0.5.1.zip` (PowerShell `Compress-Archive`)
- GitHub release tag: `v0.5.1`
- Release notes:
  - 무공 active 카테고리 활성화
  - v0.5 PoC 미해결 issue 해소 (UI refresh + save persistence)
  - v0.4 의 다른 카테고리 동작 변경 없음
  - legacy 호환 (v0.1~v0.4 슬롯 무손실)

### 8.5 README / HANDOFF update

- README: 9-카테고리 표에 "무공 active" 활성화 표시
- HANDOFF: §2 git history v0.5.1 commits 추가, §5 검증 완료 list 에 active full 추가, §6 v0.6 후보 갱신 (외형 / 무공 list / 인벤토리 / 창고 / UI cache 일반화 — active 의 spike 결과 path 가 transferable 한지 명시)

---

## 9. v0.6+ 후보 (HANDOFF §6 갱신용)

v0.5.1 release 후 다음 sub-project 후보 (한 번에 한 sub-project):

| 후보 | v0.5.1 의 영향 |
|---|---|
| **외형** (`faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data wrapper graph) | v0.5.1 의 spike PASS path (flag toggle 또는 game-self method) 가 외형의 sprite cache invalidate 에 transferable 한지 확인. 이론적으로 `HeroIconDirty / heroIconDirtyCount` 는 외형도 trigger 함 |
| **무공 list** (`kungfuSkills` entry 추가/제거, `KungfuSkillLvData` ctor / factory / Add method) | active 와 wrapper class 공유. v0.5.1 에서 wrapper shape 검증됨. 다음 challenge = ctor / factory 발견 |
| **인벤토리** (`itemListData.allItem (ItemData[])` wrapper graph) | 외형 sub-project 와 동일 challenge — sub-data wrapper graph |
| **창고** (`selfStorage.allItem (ItemData[])`) | 동상 |
| **UI cache invalidation 일반화** | v0.5.1 spike 결과를 일반 framework 로 추출 (외형 / 무공 list / 인벤토리 / 창고 모두 사용) |

---

## 10. v0.5.1 의 결정 사항 (Q&A 요약)

| Q | A | 사유 |
|---|---|---|
| Q1: 큰 방향 | A — v0.6 통합 작업 시작 + sub-project 분해 | maintenance 보다 가치 추가, dual-track FAIL 회피 |
| Q2: 첫 sub-project | A — 무공 active full | v0.5 에서 read-back PASS — 가장 가까이 도달 + UI refresh path 의 testbed |
| Q3: process | C — Hybrid (small spike + spec + impl) | binary PASS/FAIL 회피 + spec scope 자동 조정 |
| Q4: 출하 target | A — v0.5.1 minor active only | sub-project 분해 원칙 + 빠른 출하 + scope creep 회피 |
| Q5: spike protocol | C — adaptive (1 후 user gate) | "사용자 시나리오 입력 조건 불성립" 같은 상황 조기 발견 |

---

## Appendix A — v0.5 evidence 요약 (v0.5.1 의 startpoint)

### A.1 KungfuSkillLvData wrapper shape

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
property: Boolean skillIconDirty   ← UI refresh 후보 1
property: Boolean maxManaChanged   ← UI refresh 후보 2
```

### A.2 HeroData 의 active 관련 영역

- `kungfuSkills` (Il2CppSystem.Collections.Generic.List<KungfuSkillLvData>)
- `EquipSkill(KungfuSkillLvData wrapper, bool=true)` — active 추가
- `UnequipSkill(KungfuSkillLvData wrapper, bool=true)` — active 해제
- `HeroIconDirty` (bool) — UI refresh 후보 3
- `heroIconDirtyCount` (int) — UI refresh 후보 4

### A.3 v0.5 Phase B Harmony trace 결과

- 사용자가 game UI 로 active 변경 시:
  - `UnequipSkill(wrapper, true)` × 11 호출
  - `EquipSkill(wrapper, true)` × 11 호출
- 11-슬롯 swap 패턴 = game 자체 동작

### A.4 v0.4 A3 PoC FAIL 의 진짜 원인 (재확인)

- v0.4 A3: `nowActiveSkill = ID` setter direct 시도 → **잘못된 source field**
- 진짜 source = `kungfuSkills[i].equiped` (Phase A 발견)
- 진짜 method path = `EquipSkill / UnequipSkill (wrapper, bool)` (Phase B 발견)
- 그러나 game UI cache invalidate 별도 필요 — Phase C 미해결, v0.5.1 의 핵심 도전
