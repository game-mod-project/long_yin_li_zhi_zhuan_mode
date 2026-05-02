# v0.5.3 Spike — 인벤토리 method + ItemData ctor discovery

## 핫키 안내
- **F11**: mod 창 toggle
- **F12**: 현재 Mode 의 Step 실행
- **mod 창 visible 시 1, 2, 3** (또는 Numpad 1-3): Mode 직접 설정

## Step 1 — HeroData *Item* method dump

**실행**: F11 → 1 (Step1) → F11 끔 → F12

**결과**: [TBD]

**clear method 후보**: [TBD] (예상: `LoseAllItem()`)
**add method 후보**: [TBD] (예상: `GetItem(ItemData wrapper, ...)`)

---

## Step 2 — ItemData ctor + static method dump

**실행**: F11 → 2 (Step2) → F11 끔 → F12

**결과**: [TBD]

**ctor 후보**: [TBD] (예상: `(int _itemID)` 또는 `(int itemID, int itemCount)`)

---

## Step 3 — Persistence baseline (first 10 entries)

**실행**: F11 → 3 (Step3) → F11 끔 → F12

**결과**: [TBD]

---

## 종합 판정

[TBD — Step 1-3 결과 종합. PASS = ctor + method 모두 발견. FAIL = abort]
