# LongYin Roster Mod v0.5 — Design Spec

**일시**: 2026-05-01
**Scope**: PoC-driven dual track — 외형 (portraitID + gender) + 무공 active semantic 재조사
**선행 spec**:
- `2026-04-29-longyin-roster-mod-v0.3-design.md` (PinpointPatcher / Apply / Restore 기반)
- `2026-04-30-longyin-roster-mod-v0.4-design.md` (selection-aware 9-step pipeline + ApplySelection)
**HANDOFF**: `docs/HANDOFF.md` §6.A — v0.5+ 후보

---

## 1. Context

### 1.1 v0.4 까지의 도달점

- **selection-aware 9-step pipeline** — `_meta.applySelection` 슬롯별 저장 + 카테고리 토글 즉시 저장
- **17 SimpleFieldMatrix entry** + **IdentityFieldMatrix 9 entry (setter direct)** + heroTagData rebuild
- **40/40 unit tests** + **smoke D15 PASS** (save→reload 정보창 정상)
- 정체성 9 필드 PASS, 부상/충성/호감 영구 보존 회귀, V03Default 호환
- v0.4 PoC 결과:
  - **A2 Identity PASS** → v0.4 포함
  - **A3 ActiveKungfu FAIL** — wrapper.lv vs nowActiveSkill ID semantic mismatch (commit `c83e808`)
  - **A4 ItemData FAIL** — sub-data wrapper graph 미해결 (commit `7d57fea`)

### 1.2 v0.5 의 동기

v0.4 의 deferred list (HANDOFF §6.A) 중 두 항목을 dual PoC 로 도전. v0.4 의 PoC-driven scope 결정 패턴을 그대로 적용 — PoC 결과가 spec implementation 의 어느 섹션을 활성화할지 결정 (OR gate).

대상 선정 사유:
- **외형** (portraitID + gender) — 단순 setter + sprite cache invalidate 만 해결하면 됨. 사용자 임팩트 큼 (보이는 변화). 위험도 낮음.
- **무공 active** — v0.4 PoC A3 의 미해결 잔여물. 가설 변경 (wrapper-based 시도 → save-diff + 동적 trace) 으로 재도전.

### 1.3 v0.5 의 새 접근 — PoC-driven OR-gate

**핵심 패턴**:
- 두 PoC 를 독립적으로 실행 → 각자 PASS / FAIL
- **OR gate**: 한쪽이라도 PASS 면 v0.5.0 release. 양쪽 다 FAIL 시 maintenance 모드 + PoC report commit
- spec 의 implementation 섹션은 PoC 결과에 따라 조건부 활성화
- v0.4 와의 연속성: 9-step pipeline / ApplySelection / SimpleFieldMatrix 패턴 재사용. **신규 인프라 없음 — 기존 패턴에 entry / step 추가만**

**최소 침습적 확장**:
- UI 변경 없음 (체크박스 grid 의 disabled flag 만 토글)
- Slot schema 변경 없음 (v0.4 의 `applySelection.appearance / activeKungfu` 는 이미 정의되어 있음)
- legacy 슬롯 자동 호환 (V03Default 그대로)

---

## 2. Goals & Non-goals

### 2.1 v0.5 Goals

1. **외형 PoC** — `portraitID` + `gender` setter direct + sprite cache invalidate method 발견
2. **무공 active PoC** — save-diff 로 영향 필드 식별 → Harmony trace 로 method path 발견 → in-memory 검증
3. **PASS 항목 promote** — 발견된 path 를 `Core/PortraitRefresh.cs` / `Core/ActiveKungfuPath.cs` 로 캡슐화
4. **SimpleFieldMatrix 확장** — 외형 PASS 시 `portraitID`, `gender` 2 entry 추가 (Category=`Appearance`)
5. **PinpointPatcher step 추가** — active PASS 시 9-step → 10-step 으로 확장
6. **Capabilities 자동 감지** — `appearance: true/false`, `activeKungfu: true/false` 프로듀스 → UI 의 disabled flag 와 연결
7. **v0.1~v0.4 슬롯 호환** — schema 변경 없음. V03Default 자동 적용
8. **OR-gate release** — 한쪽이라도 PASS 면 v0.5.0 release

### 2.2 v0.5 Non-goals

| # | 항목 | 미루는 사유 |
|---|---|---|
| N1 | 무공 list (kungfuSkills) | `KungfuSkillLvData` wrapper ctor — v0.6 후보 |
| N2 | 인벤토리 / 창고 (itemListData / selfStorage) | sub-data wrapper graph — v0.6 후보 |
| N3 | faceData (얼굴 features) | sub-data wrapper graph — v0.6 후보 (인벤토리와 묶음) |
| N4 | 외형 확장 (의상 / 체형) | dump 추가 필요 — v0.6 검토 |
| N5 | active 의 list 의존성 자동 동기화 | active 만 PASS 시 list 부재면 skip + warning. 자동 추가는 v0.6 list PoC 와 묶음 |
| N6 | 부상/충성/호감 backup 옵션화 | v0.6+ 검토 (v0.4 N3 그대로) |
| N7 | Apply preview (dry-run) | v0.6+ (v0.4 N4 그대로) |
| N8 | Selection 프리셋 ("전체"/"외형만"/"active만") | v0.6+ (v0.4 N5 그대로) |
| N9 | 자동 smoke harness | v0.6+ (v0.4 N7 그대로) |

---

## 3. Architecture

### 3.1 PoC-driven dual track 흐름

```
[D1] 외형 PoC                           [D2] 무공 active PoC
  ├─ Phase 1: static dump                 ├─ Phase A: save-diff (필드 식별)
  │   (Refresh*Portrait*, *Sprite*)       │   (사용자 game 내 active 변경 → save 두 번 → diff)
  └─ Phase 2: in-memory PoC               ├─ Phase B: Harmony trace (method path)
      (portraitID setter + invalidate     │   (Phase A 의 변경 필드 set method 후보 hook)
       method 시도, 화면 + save-reload)   └─ Phase C: in-memory PoC
                                              (발견된 method 호출 + save-reload 검증)
        ↓                                       ↓
       G1                                      G2 (Phase A) + G3 (Phase C)
        ↓                                       ↓
        └────────────── OR-gate ─────────────────┘
                          ↓
        ┌────────────────┼────────────────┐
        ↓                ↓                ↓
    외형 PASS only   양쪽 PASS      active PASS only
    + active defer   + 양쪽 promote  + 외형 defer
        ↓                ↓                ↓
        └────────── 통합 build ──────────┘
                          ↓
                       smoke E (G4)
                          ↓
                    release v0.5.0 (G5)
```

양쪽 FAIL 시: release 안 함 → maintenance 모드 + PoC report commit + Probe 코드 cleanup.

### 3.2 PoC 결과 → release scope 매트릭스

| 외형 | active | v0.5.0 scope | §10 v0.6+ 후보 |
|---|---|---|---|
| PASS | PASS | 외형 + active | (감소 — list / 인벤토리 / faceData / 의상) |
| PASS | FAIL | 외형만 | active 유지 + v0.6 후보 추가 evidence |
| FAIL | PASS | active 만 | 외형 유지 + v0.6 후보 추가 evidence |
| FAIL | FAIL | (release 없음) | 둘 다 유지 + maintenance 모드 |

### 3.3 코드 파일 영향 범위

**기존 패턴 재사용 — 신규 인프라 없음**:

| 파일 | 변경 종류 | 조건부? |
|---|---|---|
| `Core/Probes/ProbePortraitRefresh.cs` | 신규 (임시) | 항상 — PoC 단계 |
| `Core/Probes/ProbeActiveKungfuV2.cs` | 신규 (임시) | 항상 — PoC 단계 |
| `Core/PortraitRefresh.cs` | 신규 | ✓ 외형 PASS |
| `Core/ActiveKungfuPath.cs` | 신규 | ✓ active PASS |
| `Core/SimpleFieldMatrix.cs` | +2 entry (portraitID, gender, Category=Appearance) | ✓ 외형 PASS |
| `Core/PinpointPatcher.cs` | step 추가 (active) + sprite invalidate hook (외형) | PoC 따라 조건부 |
| `Core/Capabilities.cs` | flag 토글 | 항상 |
| `UI/SlotDetailPanel.cs` | disabled label 제거 | PASS 따라 조건부 |
| `Util/KoreanStrings.cs` | label 수정 | PASS 따라 조건부 |
| `Slots/`, `UI/ModWindow.cs`, `UI/SlotListPanel.cs` | 변경 없음 | — |
| `LongYinRoster.Tests/` | +4~8 tests (PoC 결과 따라) | PASS 따라 조건부 |

**v0.4 의 `Core/ItemDataFactory.cs` 처리**: 변경 없음. v0.6 인벤토리 PoC 대상으로 보존 (stub 유지).

---

## 4. PoC Phase 정의

### 4.1 외형 PoC (`ProbePortraitRefresh`)

#### Phase 1 — Static dump (0.5일)

- `Assembly-CSharp.dll` 의 `HeroData` / `MainHeroData` / `PlayerView` / `HeroPanel` method 패턴 매칭:
  - `Refresh*Portrait*`, `Reload*Portrait*`, `Update*Portrait*`
  - `Refresh*Face*`, `Refresh*Avatar*`, `Refresh*Sprite*`
  - `*portrait*ID*` getter/setter
- **Deliverable**: `docs/superpowers/dumps/2026-05-01-portrait-methods.md` — 후보 method list + signature

**Abort 조건**: 후보 method 0 → 외형 PoC FAIL 즉시 선언.

#### Phase 2 — In-memory PoC (0.5일)

- F12 → `ProbePortraitRefresh.Run(player)`
- 단계:
  1. `player.portraitID` 현재값 read
  2. `player.portraitID = N` setter direct 시도 → read-back 검증
  3. 단계 2 가 silent no-op 이면 reflection 으로 backing field 직접 set
  4. 후보 refresh method 들 순회 호출
  5. 화면 관찰 + (사용자) save → reload → 화면 유지 확인
- **G1 게이트**: 사용자가 화면 변경 + save-reload 결과 확인하고 PASS / FAIL 판정.

**PASS 조건**: 화면 초상화가 즉시 + save-reload 후 변경됨.

#### Phase 3 — Promote (PASS 시)

- `Core/PortraitRefresh.cs` 신설 — 발견된 method path 캡슐화
- `Core/SimpleFieldMatrix.cs` — `Appearance` Category enum + `portraitID`, `gender` entry
- `Core/Capabilities.cs.appearance = true`
- `Core/PinpointPatcher.cs` — Apply 후 `PortraitRefresh.Invoke(player)` 호출 hook
- `UI/SlotDetailPanel.cs` — appearance checkbox label disabled 해제
- `Util/KoreanStrings.cs` — "외형" 카테고리 label 활성

### 4.2 무공 active PoC (`ProbeActiveKungfuV2`)

#### Phase A — save-diff (0.5일)

- 사용자 시나리오:
  1. 게임 내 active 무공 X 상태에서 F5 save (save N)
  2. UI 에서 active 를 Y 로 변경
  3. F5 save (save N+1)
- mod 가 두 save 의 player JSON 을 diff:
  - 변경된 필드 set 식별 (`nowActiveSkill` 외 다른 필드 동시 변경 여부)
  - 변경 안 된 필드 (불변 invariant) 식별
- **Deliverable**: `docs/superpowers/dumps/2026-05-01-active-kungfu-diff.md`
- **G2 게이트**: 사용자가 diff 결과 확인 후 Phase B 진행 동의.

**Abort 조건**: 변경 필드 0 → "게임이 active 를 다른 곳 (캐시 / 별도 파일) 에 저장" evidence → active PoC FAIL 즉시 선언.

#### Phase B — Harmony trace (1.5일)

- Phase A 의 변경 필드를 set 하는 method 후보 enumerate:
  - `Set*ActiveSkill*`, `Switch*ActiveSkill*`, `Equip*Kungfu*`, `Change*ActiveKungfu*`
- 후보 method 들에 `[HarmonyPatch] Prefix` hook → BepInEx log 출력 (인자 + caller)
- 사용자가 게임 내 active 변경 UI 클릭 → 어느 method 가 진짜 호출되는지 evidence
- **Deliverable**: `docs/superpowers/dumps/2026-05-01-active-kungfu-trace.md`

**Abort 조건**: 후보 5+ 시도 후 무반응 → active PoC FAIL.

#### Phase C — In-memory PoC (0.5일)

- 발견된 method path 를 mod 코드에서 직접 호출
- 단계:
  1. slot JSON 의 `nowActiveSkill` ID 추출
  2. 현재 player.kungfuSkills 에 해당 ID 존재 여부 검증
  3. 존재 시: 발견된 method 호출 → 화면 변경 + save-reload 검증
  4. 부재 시: skip + warning toast
- **G3 게이트**: 사용자 검증 후 PASS / FAIL 판정.

**PASS 조건**: list 존재 시 active 즉시 변경 + save-reload 후 유지.

#### Phase D — Promote (PASS 시)

- `Core/ActiveKungfuPath.cs` 신설 — method path + list 존재 검증 + warning toast
- `Core/PinpointPatcher.cs` — 9-step → 10-step (active step 추가, list 부재 시 skip)
- `Core/Capabilities.cs.activeKungfu = true`
- `UI/SlotDetailPanel.cs`, `Util/KoreanStrings.cs` — 활성

### 4.3 PoC 임시 코드 cleanup

v0.4 의 D16 패턴 그대로:
- `Core/Probes/ProbePortraitRefresh.cs`, `Core/Probes/ProbeActiveKungfuV2.cs`, F12 trigger handler 는 release 직전 일괄 제거
- 양쪽 FAIL 일 때도: PoC report commit + Probe 코드 cleanup commit 분리

---

## 5. Data Flow

### 5.1 외형 Apply 흐름 (PASS 시)

```
[1] ApplySelection 읽기 (slot._meta.applySelection)
       ↓ appearance == true 인 경우만 진입
[2] PinpointPatcher — selection-aware 9-step (기존 v0.4)
       ↓
[3] SetSimpleFields step (기존) — Appearance category entry 활성
       ├─ SimpleFieldMatrix["portraitID"]: int setter direct
       └─ SimpleFieldMatrix["gender"]: int setter direct
       ↓
[4] 외형 hook (신규) — PortraitRefresh.Invoke(player)
       └─ 발견된 sprite cache invalidate method 호출
       ↓
[5] 기존 RefreshSelfState / RefreshExternalManagers (변경 없음)
       ↓
[6] ToastService — "외형 적용 완료"
```

### 5.2 무공 active Apply 흐름 (PASS 시)

```
[1] ApplySelection 읽기
       ↓ activeKungfu == true 인 경우만 진입
[2] PinpointPatcher — 기존 9-step (변경 없음)
       ↓
[3] Step 10 (신규) — ActiveKungfuPath.Apply(slotJson, player)
       ├─ slot JSON 의 nowActiveSkill ID 추출
       ├─ list 존재 검증
       │   ├─ 존재: 발견된 set method 호출
       │   └─ 부재: skip + warning toast ("무공 list 부재 — active 건너뜀")
       └─ ID semantic 변환 (Phase A 결과 따라)
       ↓
[4] 기존 RefreshSelfState (active 변경이 stat 영향 줄 경우 cache 갱신)
       ↓
[5] ToastService
```

### 5.3 PoC 단계 임시 흐름 (release 전 제거)

```
[F12] 핸들러 (임시)
   ↓
ProbeRunner — 다음 중 하나 실행:
   ├─ ProbePortraitRefresh
   └─ ProbeActiveKungfuV2 (Phase A / B / C 분기)
        ├─ Phase A: save-diff 출력
        ├─ Phase B: Harmony patch wiring + log
        └─ Phase C: 발견된 method 직접 호출
```

PoC 결과는 BepInEx 로그 + `docs/superpowers/dumps/` 의 markdown 으로 캡처.

### 5.4 Slot 파일 schema 영향

**v0.4 schema 와 100% 호환 — 변경 없음**:
- `_meta.applySelection.appearance / activeKungfu` 는 v0.4 에서 이미 정의됨 (disabled 만 토글)
- `player.portraitID / gender / nowActiveSkill` 도 게임 자체 직렬화로 이미 캡처됨
- v0.5 는 schema 변경 없이 **읽기 path 만 활성화**
- legacy slot (v0.1~v0.4) 도 V03Default 자동 적용 — 마이그레이션 불필요

### 5.5 Restore 흐름 (변경 없음)

자동백업 슬롯 0 → 현재 player 로 복귀 (v0.3 흐름 그대로):
- 외형 PASS 시 → Restore 도 외형 자동 포함 (대칭)
- active PASS 시 → 동상
- `AttemptAutoRestore` (Apply 실패 시 복원) 변경 없음

---

## 6. Risk Handling

### 6.1 PoC 시간 budget

| Phase | 예산 | abort 기준 |
|---|---|---|
| 외형 Phase 1 (static dump) | 0.5일 | 후보 method 0 시 즉시 외형 FAIL |
| 외형 Phase 2 (in-memory) | 0.5일 | 후보 method 절반 시도 후 무반응 시 FAIL |
| active Phase A (save-diff) | 0.5일 | 변경 필드 0 시 즉시 FAIL |
| active Phase B (Harmony trace) | 1.5일 | 후보 5+ 시도 후 무반응 시 FAIL |
| active Phase C (in-memory) | 0.5일 | method 호출 됐으나 효과 없으면 FAIL |
| 통합 / smoke / release | 1.0일 | — |

**총 예산**: 약 4.5일.

### 6.2 game state 손상 방지

| 위협 | 방어 |
|---|---|
| portraitID 가 invalid range → sprite asset NRE | 후보 ID 만 사용 (slot 의 기존 player.portraitID 같은 값) |
| active set 후 list 에 해당 ID 부재 → NRE | Apply 진입 직전 list 검증, 부재 시 skip + toast |
| Probe 가 game-self method 의 부수효과 (UI flush 등) 트리거 | static dump 에서 후보 부수효과 미리 식별 → 위험 method 는 PoC 제외 |
| save-diff 단계에서 사용자 game save 손상 | mod 가 readonly 로 파일 비교, save 자체는 게임 F5 사용자 트리거 |

### 6.3 PoC 단계 안전장치

- PoC 코드는 separate `Probes/` 클래스 — production code path 와 격리
- `Capabilities.appearance / activeKungfu = false` 동안 production Apply 경로 진입 차단
- F12 trigger 는 개발 빌드만 — release 전 제거 (D16 패턴)
- 모든 Probe 시도는 in-memory mutate 만, save 자체는 사용자 수동
- BepInEx log 에 try/catch 가드 + 명시적 PASS / FAIL 메시지

### 6.4 사용자 user-gate 위치

| Gate | 시점 | 사용자 액션 |
|---|---|---|
| G1 | 외형 Phase 2 직후 | 화면 + save-reload 결과 확인 → PASS / FAIL 판정 |
| G2 | active Phase A 직후 | save-diff 결과 확인 + Phase B 진행 동의 |
| G3 | active Phase C 직후 | 화면 + save-reload 후 active 동작 확인 → PASS / FAIL |
| G4 | smoke E (통합 후) | 전체 v0.5 build 의 game-load verify |
| G5 | `gh release create` 직전 | release 게이트 |

각 게이트 마다 사용자 확인 후 다음 단계 진행. 자동 진행 안 함.

### 6.5 양쪽 FAIL fallback

- v0.5 release 안 함 — 코드 변경은 PoC report + dump markdown 만
- HANDOFF.md 에 "v0.5 PoC report" 섹션 추가, 미해결 항목 v0.6 명시
- 게임 패치 (v1.0.0 f8.3+) 대기, 그 시점에 재검증
- Probe 코드는 cleanup commit 으로 main 에서 제거

---

## 7. Testing Strategy

### 7.1 Unit tests (v0.4 의 40 위에 추가)

| 그룹 | 시나리오 | 조건부? |
|---|---|---|
| `SimpleFieldMatrixTests` | `portraitID`, `gender` entry — Category=`Appearance`, type=int | ✓ 외형 PASS |
| `CapabilitiesTests` | `appearance: true/false` 직렬화 round-trip | 항상 |
| `ApplySelectionTests` | `appearance: true`, `activeKungfu: true` 토글 + JSON round-trip | 항상 |
| `SlotRepositoryTests` | legacy slot 로드 → V03Default (false) | 항상 |
| `PortraitRefreshTests` (신규) | mock player setter 호출 검증 | ✓ 외형 PASS |
| `ActiveKungfuPathTests` (신규) | mock player list 부재 시 skip + warning | ✓ active PASS |

**목표**: 40 → 44~48 tests, 모두 PASS.
**제외**: PoC 단계 Probe 클래스 — game runtime 의존, smoke 로 검증.

### 7.2 Smoke E (게임 검증 — 사용자 게이트)

| ID | 시나리오 | 통과 조건 | 조건부? |
|---|---|---|---|
| E1 | 외형 미선택 + Apply | 외형 변경 없음, 다른 카테고리 정상 | 항상 |
| E2 | 외형 선택 + Apply | 게임 화면 초상화 즉시 변경 | ✓ 외형 PASS |
| E3 | 외형 + Apply → save → reload | reload 후 초상화 유지 | ✓ 외형 PASS |
| E4 | 외형 + 정체성 동시 + Apply | 두 카테고리 모두 적용, 충돌 없음 | ✓ 외형 PASS |
| E5 | active 미선택 + Apply | active 변경 없음 | 항상 |
| E6 | active + list 부재 → Apply | active step skip + 경고 toast, 다른 정상 | ✓ active PASS |
| E7 | active + list 존재 + Apply | active 즉시 적용, save-reload 유지 | ✓ active PASS |
| E8 | 외형 + active 동시 (양쪽 PASS) + Apply | 양쪽 다 적용 | ✓ 양쪽 PASS |
| E9 | Restore 후 외형 / active 복원 | 자동백업 슬롯 0 의 상태로 복귀 | 결과 따라 |
| E10 | legacy slot (v0.4) + 외형/active + Apply | legacy 무손실, V03Default 적용 | 항상 |

**G4 게이트**: smoke E 의 적용 가능 항목 모두 PASS 일 때만 release 진행.

### 7.3 PoC 단계 검증 (markdown evidence)

```
docs/superpowers/dumps/
├── 2026-05-01-portrait-methods.md           ← Phase 1 static dump
├── 2026-05-01-portrait-poc-result.md        ← Phase 2 PASS/FAIL evidence
├── 2026-05-01-active-kungfu-diff.md         ← Phase A save-diff
├── 2026-05-01-active-kungfu-trace.md        ← Phase B Harmony trace
└── 2026-05-01-active-kungfu-poc-result.md   ← Phase C PASS/FAIL evidence
```

각 파일에 BepInEx 로그 발췌 + 시도 method list + PASS/FAIL 판정 + 사용자 게이트 결과 (v0.4 의 4887f01 / c83e808 / 7d57fea 패턴).

### 7.4 회귀 검증

| 검증 | 방법 |
|---|---|
| v0.4 9 카테고리 체크박스 동작 | smoke E10 (legacy slot Apply) 통합 |
| v0.4 정체성 9 필드 Apply | smoke E4 (외형 + 정체성 동시) 가 회귀 검증 역할 |
| v0.4 부상/충성/호감 보존 | smoke E1, E5 (외형/active 미선택) |
| v0.4 의 40 tests | 작업 중 항상 `dotnet test` 통과 유지 |

---

## 8. Capabilities & UI 영향

### 8.1 Capabilities flag 변화

| flag | v0.4 | v0.5 (외형 PASS) | v0.5 (active PASS) | v0.5 (양쪽 PASS) |
|---|---|---|---|---|
| `stats` | true | true | true | true |
| `identity` | true | true | true | true |
| `appearance` | false | **true** | false | **true** |
| `activeKungfu` | false | false | **true** | **true** |
| `kungfuList` | false | false | false | false |
| `inventory` | false | false | false | false |
| `storage` | false | false | false | false |

### 8.2 UI 변경 (PASS 따라 조건부)

- `SlotDetailPanel.cs` — `Capabilities.appearance / activeKungfu` 가 true 인 경우 disabled label 제거
- `KoreanStrings.cs` — disabled suffix "(v0.5+ 후보)" 제거 (PASS 항목만)
- 외형 / active 카테고리의 체크박스가 클릭 가능해짐 (이전: disabled 회색 표시)

### 8.3 Slot schema 변경 없음

- `_meta.applySelection` 의 boolean flag 들은 v0.4 에서 이미 정의됨
- legacy 슬롯 (v0.1~v0.4) 은 SlotRepository.UpdateApplySelection 호출 시점에 V03Default 자동 적용 — 파일 안 건드림

---

## 9. Release Plan

### 9.1 v0.5.0 release 게이트

OR-gate: 외형 OR active PASS → release 진행. 양쪽 FAIL → release 안 함.

### 9.2 Release 단계 (D-task)

| Task | 내용 | 사용자 게이트 |
|---|---|---|
| 외형 PoC (Phase 1+2) | 4.1 | G1 |
| active PoC (Phase A/B/C) | 4.2 | G2, G3 |
| Promote (PASS 항목만) | 4.1 Phase 3 / 4.2 Phase D | — |
| 통합 build + unit tests | `dotnet test` 모두 PASS | — |
| smoke E | 7.2 | G4 |
| Probe 코드 cleanup | D16 패턴 | — |
| HANDOFF / README / VERSION 업데이트 | v0.5.0 반영 | — |
| `dist/LongYinRoster_v0.5.0.zip` 생성 | release packaging | — |
| `git tag v0.5.0` + push + `gh release create` | GitHub release | G5 |

### 9.3 양쪽 FAIL 시 alternate flow

| Task | 내용 |
|---|---|
| PoC report commit | `docs/superpowers/dumps/2026-05-01-v0.5-poc-report.md` (양쪽 FAIL evidence) |
| HANDOFF.md update | "v0.5 PoC report" 섹션 추가, v0.6 후보 갱신 |
| Probe 코드 cleanup commit | main 에서 임시 코드 제거 |
| (release 안 함) | tag / dist zip 생성 안 함 |

---

## 10. v0.6+ 후보 (이번 spec scope 외)

| 항목 | v0.5 결과로 진척된 점 |
|---|---|
| 무공 list (kungfuSkills) | active PASS 시 list set method dump 단서 가능 (Phase B trace) |
| 인벤토리 / 창고 | v0.4 ItemDataFactory stub 보존, sub-data graph 미해결 |
| faceData (얼굴 features) | 외형 PASS 시 sprite invalidate method 발견 → faceData refresh 단서 가능 |
| 의상 / 체형 | dump 라운드 추가 필요 |
| active 의 list 의존성 자동 동기화 | active PASS + list PASS 후 통합 |

---

## 11. 부록 — v0.4 PoC 결과 cross-reference

| v0.4 PoC | 결과 | v0.5 처리 |
|---|---|---|
| A2 Identity (heroName 등 9 필드 setter direct) | PASS | v0.4 에 포함 — v0.5 무영향 |
| A3 ActiveKungfu (wrapper-based SetNowActiveSkill) | FAIL — wrapper.lv vs nowActiveSkill ID semantic mismatch | **v0.5 재도전** (가설 변경: save-diff + 동적 trace) |
| A4 ItemData (factory ctor / Add method) | FAIL — sub-data wrapper graph 미해결 | v0.6 후보 (v0.5 무영향) |

v0.4 의 commit reference:
- `4887f01` poc: Identity PASS — setter direct (in-memory)
- `c83e808` poc: ActiveKungfu FAIL — wrapper-based mismatch
- `7d57fea` poc: ItemData FAIL — sub-data graph unsolved
- `02e349e` IdentityFieldMatrix + ItemDataFactory stub
