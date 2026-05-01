# v0.5 active PoC — Phase C 결과 (2026-05-01) — **G3 FAIL**

## Outcome
**G3 보수적 FAIL** — Method path 작동 + read-back PASS 했으나 게임 UI 미반영 + save→reload 미검증. v0.5.0 release 안전하지 않음 → defer to v0.6.

## 진척 단계

| Phase | 결과 | Evidence |
|---|---|---|
| Phase A (save-diff) | ✅ G2 PASS | `kungfuSkills[i].equiped` source-of-truth 발견 |
| Phase B (Harmony trace) | ✅ PASS | `HeroData.EquipSkill / UnequipSkill (KungfuSkillLvData, bool)` 발견 |
| Phase C (in-memory toggle) — read-back | ✅ PASS | `read-back — old equiped=False / new equiped=True` |
| Phase C — game UI reflection | ❌ FAIL | 사용자 보고: F12 후 무공 패널 변화 안 보임 |
| Phase C — save→reload persistence | ❓ 미검증 | 사용자가 직접 비운 시나리오만 시도 (PoC 입력 조건 불성립) |

## 1차 시도 (commit `d4532c9`)

ID 매칭 (`kungfuID`) 의존 — wrapper.kungfuID 가 모두 -1 fallback → `firstUnequippedDifferent` skip. PoC swap 시도 자체 못 함.

```
PhaseC: 전체 — equiped=11, unequipped=119
PhaseC: equiped=false && kungfuID != -1 항목 없음 — 후보 부족. skip.
```

## 2차 시도 (commit `4a8aa24`)

ID 매칭 → idx 매칭 + DumpWrapperShape 추가.

### KungfuSkillLvData wrapper shape (결정적 발견)

```
property: Boolean equiped = True
property: Int32 skillID = 283        ← 진짜 ID 필드 (NOT kungfuID)
property: Int32 lv = 10
property: Single fightExp = 9680
property: Single bookExp = 9680
property: Boolean isNew = False
property: Int32 belongHeroID = 0
property: HeroSpeAddData speEquipData = HeroSpeAddData
property: HeroSpeAddData speUseData = HeroSpeAddData
property: HeroSpeAddData extraAddData = HeroSpeAddData
property: Single equipUseSpeAddValue / damageUseSpeAddValue / selfUseSpeAddValue / enemyUseSpeAddValue
property: Single cdTimeLeft / activeTimeLeft / power / battleDamageCount
property: Int32 useTime
property: Boolean skillIconDirty = False
property: Boolean maxManaChanged = False
property: IntPtr ObjectClass / Pointer / WasCollected (IL2CPP runtime)
field:    Boolean isWrapped = False
field:    IntPtr pooledPtr (IL2CPP runtime)
```

### 첫 swap 결과

```
PhaseC: 시도 — Unequip kungfuID=-1 (idx 27); Equip kungfuID=-1 (idx 0)
PhaseC: UnequipSkill OK
PhaseC: EquipSkill OK
PhaseC: read-back — old equiped=False (expect false); new equiped=True (expect true)
PhaseC: SUCCESS — read-back 확인.
```

**결정적 PASS**: method 호출 작동, equiped flag 데이터 layer 변경 확인.

### 게임 UI 반영 — FAIL

사용자 보고: **F12 후 무공 패널의 active 슬롯 변경 안 보임**.

→ v0.4 외형 PoC 와 동일 패턴 ("method 호출됐으나 sprite/UI cache invalidate 별도 필요").

추정 원인:
- `KungfuSkillLvData.skillIconDirty` (현재 False) flag 가 UI 갱신의 trigger 일 가능성
- `KungfuSkillLvData.maxManaChanged` flag 도 후보
- `HeroIconDirty` (HeroData) flag 도 후보
- 또는 game-internal `RefreshFightSkillUI / RefreshKungfuPanel` 류 method 후속 호출 필요

### save → reload — 미검증

사용자 시나리오:
1. 사용자가 active 11개를 직접 (게임 UI 로) 모두 unequip → equiped=0
2. F12 → ProbeC: equiped=true 항목 없음 → swap skip (no operation)
3. save → reload → 비어있음 그대로

이 시나리오는 PoC 의 입력 조건이 불성립 (PoC 는 equiped=true 1+ 와 equiped=false 1+ 가 필요). 따라서 save→reload persistence 검증 정보 없음.

진짜 검증 시나리오는 미시도: **active 11 채워진 상태 + F12 swap + save + reload + active 정확히 유지 확인**.

## v0.4 A3 PoC FAIL 의 진짜 원인 (재확인)

- v0.4 A3: `nowActiveSkill = ID` setter direct 시도 → **잘못된 source field**
- 진짜 source 는 `kungfuSkills[i].equiped` flag (Phase A 발견)
- 진짜 method path 는 `HeroData.EquipSkill / UnequipSkill (KungfuSkillLvData wrapper, bool)` (Phase B 발견)
- 그러나 game UI cache invalidate 별도 필요 — PoC 미해결 (Phase C UI 미반영)

## v0.6 후보 evidence (귀중한 발견)

production code 작성 시 사용:

| 발견 | 사용처 |
|---|---|
| `kungfuSkills[i].equiped` 가 source-of-truth | active 식별 |
| `KungfuSkillLvData.skillID` (NOT kungfuID) | slot JSON 의 active ID 매칭 |
| `HeroData.EquipSkill (wrapper, true)` | active 추가 |
| `HeroData.UnequipSkill (wrapper, true)` | active 해제 |
| 11-슬롯 array swap 패턴 (게임 자체 호출) | full active swap 시 11회 Unequip + 11회 Equip |
| `KungfuSkillLvData.skillIconDirty` flag | UI refresh trigger 후보 |
| `KungfuSkillLvData.maxManaChanged` flag | mana 재계산 trigger 후보 |
| `HeroData.HeroIconDirty` / `heroIconDirtyCount` | hero icon refresh 후보 |
| sub-data graph (`HeroSpeAddData speEquipData / speUseData / extraAddData`) | 무공 effect 적용 (v0.6 ItemData 와 같은 graph 패턴) |

## v0.6 통합 작업 후보

v0.5 의 외형 FAIL + active 부분 PASS 모두 **sub-data wrapper graph + UI cache invalidation** 으로 귀결:

1. **외형** — `faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data + sprite cache invalidate
2. **active** — `kungfuSkills[i].equiped` 작동 + UI refresh path
3. **인벤토리** — `itemListData.allItem (ItemData[])` sub-data graph (v0.4 PoC A4 미해결)
4. **창고** — `selfStorage.allItem (ItemData[])` 동상

→ v0.6 의 핵심 작업 = **IL2CPP wrapper graph 통합 + game UI refresh path discovery**.

## G3 결정

❌ **FAIL — v0.5 active scope 보수적 defer**.
- read-back PASS 만으로는 release 가치 부족 (게임 UI 미반영)
- save→reload persistence 미검증
- v0.4 외형 PoC 와 동일 패턴 — UI cache invalidate 가 진짜 challenge
- 양쪽 PoC FAIL → maintenance flow → release 안 함
