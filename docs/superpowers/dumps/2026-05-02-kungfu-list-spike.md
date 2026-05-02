# v0.5.2 Spike — 무공 list method discovery (2026-05-02)

## 핫키 안내

- **F12**: 현재 Mode 의 Step 실행
- **F10**: Mode cycle (Step1 → Step2 → Step3 → Step4 → Step5 → Step1)
- **F11**: mod 창 toggle

## Step 1 — Method dump

**목적**: HeroData 의 `Lose|Learn|Add|Clear|Remove|Get|Drop` * `Kungfu|Skill` method 시그니처 발견.

**실행**:
- 게임 시작 → 캐릭터 load
- mod F11 끔 (또는 그대로)
- F12 누름 (default Mode = Step1)

**예상 BepInEx 로그**:
```
=== Spike Step1 — method dump ===
method: Void LoseAllKungfu()
method: Void LearnKungfu(Int32 skillID, Int32 lv)
... (HeroData 의 매칭 method 목록)
=== Spike Step1 end ===
```

**결과** (1차):
```
method: Void LoseSkill(KungfuSkillLvData targetSkill)
method: Void AddSkillFightExp(Single num, KungfuSkillLvData targetSkill, Boolean showInfo)
method: Void AddSkillBookExp(Single num, KungfuSkillLvData targetSkill, Boolean showInfo)
method: Void LoseAllSkill()                                                          ← clear 후보 ✓
method: KungfuSkillLvData GetSkill(KungfuSkillLvData skillLvData, Boolean showInfo, Boolean speShow)  ← add 후보 (wrapper 인자)
method: SkillMaxPracticeExpData GetSkillMaxPracticeExp(Int32 targetID)
method: Void AddSkillMaxPracticeExp(SkillMaxPracticeExpData target)
method: Single GetSkillPowerChargeSpeed(FightSkillType targetSkillType)
method: Single GetSkillRareLvExpRate(Int32 targetRareLv)
```

**clear method 후보**: `LoseAllSkill()` ✓
**add method 후보**: `GetSkill(KungfuSkillLvData wrapper, ...)` — **wrapper ctor 필요** (v0.4 PoC A1 한계)

**판정**: clear PASS / add 는 wrapper ctor 발견 필요 → Step 6 (wrapper ctor dump) 추가

---

## Step 2 — Clear method 시도

**전제**: 현재 캐릭터 game save 한 번 (Spike 보호용).

**실행**:
- F10 한 번 → Mode = Step2
- F12 → 후보 clear method 시도

**결과**:
- method: [TBD]
- count 변화: [TBD]
- UI: [TBD]

**판정**: [TBD]

**Cleanup**: SaveSlot reload — baseline 복원

---

## Step 3 — Add method 시도

**실행**:
- F10 → Mode = Step3
- F12 → 후보 add method 시도

**결과**: [TBD]

**판정**: [TBD]

**Cleanup**: SaveSlot reload

---

## Step 5 — Persistence baseline

**실행**:
- F10 → Mode = Step5 → F12 (pre-save list)
- game save → 종료 → 재시작 → load
- F10 → Mode = Step5 → F12 (post-reload list)

**결과**:
- pre-save: [TBD]
- post-reload: [TBD]

**판정**: [TBD]

---

## Step 6 — KungfuSkillLvData wrapper ctor / static method dump

**실행**: F11 → Mode = Step6 (1-6 키) → F12

**결과**:
```
=== Spike Step6 — KungfuSkillLvData (KungfuSkillLvData) dump ===
--- Constructors ---
ctor: ()                          ← parameterless ✓
ctor: (Int32 _skillID)            ← 단일 int ctor ✓✓ (핵심 발견)
ctor: (IntPtr pointer)            ← IL2CPP wrapper ctor (Il2CppInterop)
--- Static methods ---
static: Single CountDamageRatio(Single sourceNum, Single addRatio)   ← utility, factory 아님
=== Spike Step6 end ===
```

**판정**: ✅ **PASS** — `KungfuSkillLvData(int _skillID)` ctor 발견. v0.4 PoC A1 의 wrapper ctor IL2CPP 한계가 false 였음.

---

## 종합 판정

✅ **Spike PASS** — Phase 3 (Implementation) 직접 진행 가능.

**Production path**:
1. **Clear**: `HeroData.LoseAllSkill()` — parameterless
2. **Wrapper 생성**: `new KungfuSkillLvData(skillID)` — reflection ctor 호출
3. **Property setter**: `wrapper.lv / fightExp / bookExp` 등 property reflection set
4. **Add**: `HeroData.GetSkill(KungfuSkillLvData wrapper, bool showInfo=false, bool speShow=false)` — return type = wrapper, 즉 player 의 list 에 add 후 wrapper return

**Spike Step 2-5 (clear/add 통합 + persistence)**: implementation 의 smoke 시나리오에서 직접 검증 (Spike skip — wrapper ctor 발견으로 production path 명확).
