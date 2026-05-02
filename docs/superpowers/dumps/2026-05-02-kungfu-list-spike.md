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

**결과**: [TBD]

**clear method 후보**: [TBD]
**add method 후보**: [TBD]

**판정**: [TBD]

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

## 종합 판정

[TBD — Step 1-3 + 5 결과 종합]

**다음 단계**:
- All PASS → Phase 3 (Implementation) — KungfuListApplier 의 ClearMethodName / AddMethodName 을 Spike 결과로 hardcoded
- FAIL → User gate (wrapper ctor 재도전 / abort)
