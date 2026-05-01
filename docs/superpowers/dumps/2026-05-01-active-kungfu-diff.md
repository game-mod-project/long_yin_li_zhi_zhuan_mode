# v0.5 active PoC — Phase A save-diff (2026-05-01)

## Outcome
**G2 PASS** — 새 가설 (`kungfuSkills[i].equiped` 가 source-of-truth) 도출. Phase B 로 진행.

## 시나리오
- SaveSlot1 (active = X)
- SaveSlot2 (active = Y, X 에서 Y 로 변경 + 시간 경과)

hero[0] sizes: before=239867 chars, after=239807 chars. total DIFF entries = 72.

## 결정적 발견

```
DIFF[(root).kungfuSkills[19].equiped]: True → False
```

**`kungfuSkills[i].equiped` 이 active 의 진짜 source-of-truth**. `nowActiveSkill` 은 변경되지 않음 (DIFF list 에 부재).

**v0.4 A3 PoC FAIL 의 진짜 원인 확인**: A3 가 `nowActiveSkill` ID setter 를 시도했지만, 게임은 `kungfuSkills[*].equiped` flag 의 array 단위로 active 를 표현. 따라서 wrapper.lv vs ID mismatch 가 아니라 **잘못된 source field 를 건드린 것**.

> 주의: sample 는 first 20 of 130 kungfuSkills 만 출력. 새 active 의 equiped True 는 entry index 20+ 어딘가에 있을 것 (이번 dump 에서는 미관측, 그러나 추론 명확).

## active 와 직접 관련 DIFF

| Path | Before → After | 의미 |
|---|---|---|
| `kungfuSkills[19].equiped` | True → False | 이전 active 가 deactivate |
| `kungfuSkills[?].equiped` | False → True | 새 active activate (sample limit 으로 미관측, 추론) |

`nowActiveSkill` — DIFF 에 부재 → 변경 안 됨 → source 아님.

## 부수 변경 (active 변경과 무관 — 시간/전투/액션 잔해)

active 직접 영향 외에 67 개 DIFF 가 부수 — 두 save 사이에 게임 시간 경과 / 캐릭터 stat 갱신 / record log 갱신 등으로 발생. 이 부분은 PoC scope 외:

- `totalAttri[*]`, `totalFightSkill[*]` — derived stats
- `hp / maxhp / realMaxHp / mana / maxMana / realMaxMana / power / maxPower / realMaxPower / armor` — runtime stats
- `itemListData.maxWeight`, `fightScore` — derived
- `internalSkillSaveRecord`, `dodgeSkillSaveRecord`, `uniqueSkillSaveRecord`, `attackSkillSaveRecord[*]` — 연습 / 진행 record
- `totalAddData.heroSpeAddData.*` — many derived bonuses (added/removed/changed entries)
- `playerInteractionTimeData`, `recordLog` — game log
- 큰 array sample (itemListData.allItem 171, selfStorage.allItem 217, skillMaxPracticeExpData 85) — 표시 안 됨

## v0.5 active PoC 가설 업데이트

❌ **이전**: `nowActiveSkill` 의 ID setter direct (v0.4 A3 PoC)
✅ **신규**: `kungfuSkills[i].equiped` flag toggle 의 game-self method 발견

## Phase B 후보 method 패턴 (Harmony trace 대상)

`Set*Active*`, `Switch*Active*`, `Equip*Kungfu*`, `*EquipKungfu*`, `Set*Equip*`, `Toggle*Equip*` 등.
**HeroData 외에도** UI / Panel / Manager 클래스에서 호출될 가능성 — Phase B 의 candidate scope 가 HeroData methods 만으로는 부족할 수 있음.

## G2 결정

✅ Phase B 진행 — Harmony trace 로 active 전환 method path 발견.
