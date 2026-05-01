# v0.5 active PoC — Phase B Harmony trace (2026-05-01)

## Outcome
**Phase B PASS** — game-self method path 발견.

## 스캔 범위
- Assembly: `Assembly-CSharp`
- Type filter: `Hero`, `Kungfu`, `Skill`, `Player` (substring, IgnoreCase)
- Method name filter: `EquipKungfu`, `EquipSkill`, `Equiped`, `SetEquip`, `SetActive`, `SwitchActive`, `ChangeActive`, `ToggleActive`, `SetNowActive`, `ChangeNowActive`

## Patched (`patched=5, errors=0`)

```
HeroData.EquipSkill(KungfuSkillLvData, Boolean)
HeroData.UnequipSkill(KungfuSkillLvData, Boolean)
HeroData.SetNowActiveSkill(KungfuSkillLvData)
KungfuSkillLvData.get_equiped()       ← IL2CPP field accessor — patch warning, but normal handler
KungfuSkillLvData.set_equiped(Boolean) ← 동상
```

> `KungfuSkillLvData.set_equiped` 는 IL2CPP field accessor 라 `Il2CppInterop` 가 patch 안 됨 (warning). 그러나 실제 호출 path 는 이 setter 가 아니라 `HeroData.EquipSkill / UnequipSkill` 임이 trace 로 확인.

## TRACE 결과 — 사용자가 active 무공 변경 시

```
TRACE: HeroData.UnequipSkill(KungfuSkillLvData, True)   × 11 (consecutive)
TRACE: HeroData.EquipSkill(KungfuSkillLvData, True)     × 11 (consecutive)
```

`SetNowActiveSkill` 은 호출 안 됨 — 게임 자체가 사용 안 함.
`set_equiped` 도 직접 호출 안 됨 — `EquipSkill / UnequipSkill` 안에서 간접 호출.

## 분석

**Active 무공 = 11-슬롯 array swap**:
- 사용자가 active 변경 시 게임은
  1. 모든 기존 active 11개 → `UnequipSkill(wrapper, true)` 11회
  2. 새 11개 → `EquipSkill(wrapper, true)` 11회
- `set_equiped` field accessor 는 IL2CPP patch 불가, 그러나 `EquipSkill / UnequipSkill` 가 우회 경로

**Boolean 두 번째 인자**:
- 항상 `True` (관측). 의미 미상 — refresh / silent / animation 여부 등 추정. PoC 에서는 동일하게 `true` 사용.

**v0.4 A3 PoC FAIL 의 진짜 원인 (재확인)**:
- v0.4 가 `nowActiveSkill = ID` setter 시도 → 게임은 `EquipSkill(wrapper, true)` 사용 → 우회됨
- `wrapper.lv vs nowActiveSkill ID mismatch` 는 부수 결과, 진짜 원인은 **잘못된 method path** 사용

## v0.5 Phase C 가설

**확정**:
- `HeroData.EquipSkill(KungfuSkillLvData wrapper, bool param=true)` — 새 active set
- `HeroData.UnequipSkill(KungfuSkillLvData wrapper, bool param=true)` — 기존 해제
- `wrapper` 는 player.kungfuSkills (IL2CPP List) 의 실제 instance — 새 인스턴스 생성하면 안 됨

## Phase C 진행

✅ T14 (Phase C in-memory PoC) 진행. ProbeRunner.Current → ActiveInMemory.
