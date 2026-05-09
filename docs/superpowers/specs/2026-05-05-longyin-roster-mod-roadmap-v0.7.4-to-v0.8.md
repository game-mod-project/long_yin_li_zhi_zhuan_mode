# Roadmap — v0.7.4 → v0.8 (Meta Spec)

**일시**: 2026-05-05
**범위**: v0.7.4 (현 baseline) 이후 6단계 sub-project 의 sequence·의존성·결정 게이트
**목적**: 각 sub-project 가 진입 시점에 정식 brainstorm → spec → plan → impl cycle 을 받기 위한 메타 가이드. 메타 자체는 implementation 을 포함하지 않으며, 게이트 결정마다 append 되는 living document.

## 0. 한 줄 요약

v0.7.4 (Item 상세 panel view-only) 이후 다음 단계는 **v0.7.4.x → v0.7.5 → v0.7.6 → G1 게이트 → (v0.7.7 또는 v0.8 또는 maintenance)** 순으로 진행. 후보 단계 (v0.7.7 / v0.8) 는 G1 결정 게이트에서 GO/DEFER/NO-GO 평가 후 진입.

## 1. Roadmap Sequence & Dependency Graph

```
[v0.7.4 ✅]                  Item 상세 panel view-only — 현재 main baseline
    │
    ▼
v0.7.4.x patch              ItemDetailReflector.GetCuratedFields switch 확장
    │                       (말 / 보물 / 재료 curated 추가)
    ▼
v0.7.5 D-4 한글화           ContainerPanel + ItemDetailPanel 한자 → 한글
    │                       Hybrid 자체사전 + ModFix reflection fallback
    ▼
v0.7.6 설정 panel           hotkey / 컨테이너 정원 / 창 크기 / 검색·정렬 영속화
    │
    ▼
[결정 게이트 G1] ─────────  v0.7.7 / v0.8 / maintenance 분기 평가
    │
    ├──▶ v0.7.7 (후보) Item editor       ItemDetailReflector baseline 활용
    │       │
    │       ▼
    │   [결정 게이트 G2] ── v0.8 / maintenance / 신규 후보 분기
    │
    ├──▶ v0.8 (후보) 진짜 sprite        ItemCellRenderer placeholder 교체
    │                                    IL2CPP sprite asset + IMGUI texture
    │
    └──▶ maintenance 모드               게임 patch 알림 시 진입 (operational)
```

### 핵심 의존성

- **v0.7.4.x → v0.7.5**: 한글화 hook 이 ItemDetailPanel curated 라벨까지 적용되려면 curated 가 먼저 7 카테고리 모두 cover 되어야 영향 범위 명확
- **v0.7.5 → v0.7.7**: Item editor 가 사용자에게 보여줄 필드 라벨이 한글화된 후라야 의미 있음
- **v0.7.6 → G1**: 설정 panel 까지 끝나면 사용자 mod 의 "기본 기능" 완성 → v0.7.7 / v0.8 / maintenance 결정 시점 도달
- **v0.7.7 → G2**: Item editor 완료 후 다음 분기 결정 (v0.8 진입 또는 신규 후보 또는 maintenance)

전제: 각 단계는 별도 brainstorm → spec → plan → impl cycle (Section 5 컨벤션 참조).

## 2. 각 단계 1-pager

### 2.1 v0.7.4.x patch ✅ 완료 (v0.7.4.1, 2026-05-05)

| 항목 | 내용 |
|---|---|
| **목표** | `ItemDetailReflector.GetCuratedFields` switch 에 말·보물·재료 case 추가 → ItemDetailPanel curated 섹션이 7 카테고리 cover (현재 장비/비급/단약 만) |
| **입력 자산** | `dumps/2026-05-03-v0.7.4-subdata-spike.md` (sub-data wrapper 인벤토리), `dumps/2026-05-05-v075-cheat-feature-reference.md` Section 2 (`ItemGenerator.GenerateHorseData/GenerateTreasure/GenerateMaterial` 시그니처 + `CategoryNames[12]` 한글 라벨) |
| **출력** | 1 spec + 1 plan + 1 release tag (v0.7.4.1 등). switch 3 case 추가 + 각 카테고리 unit test (curated 필드 매칭) |
| **결정 게이트** | 단일 patch (말+보물+재료 한꺼번에) vs 분할 (말 우선 별도 patch). HorseItemData 가 가장 풍부 → 분할이 안전한지 단계 진입 시 재평가. 기본 추정: 단일 patch |

**Result** (2026-05-05):
- Release: [v0.7.4.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.4.1)
- Spec: [2026-05-05-longyin-roster-mod-v0.7.4.1-design.md](2026-05-05-longyin-roster-mod-v0.7.4.1-design.md)
- Plan: [2026-05-05-longyin-roster-mod-v0.7.4.1-plan.md](../plans/2026-05-05-longyin-roster-mod-v0.7.4.1-plan.md)
- Smoke: [2026-05-05-v0.7.4.1-smoke-results.md](../dumps/2026-05-05-v0.7.4.1-smoke-results.md)
- Tests: 182 → 193 PASS, 인게임 smoke 12/12 PASS.
- Patch 단위: (i) 단일 v0.7.4.1, Curated 분량: (A) Minimal — 말 12 / 보물 4 / 재료 2.

### 2.2 v0.7.5 D-4 Item 한글화 ✅ 완료 (v0.7.5, 2026-05-06)

| 항목 | 내용 |
|---|---|
| **목표** | ContainerPanel + ItemDetailPanel item 이름·라벨에서 한자 노출 제거. UGUI 가 아닌 IMGUI 라벨이라 ModFix 자동 변환이 안 닿음 → 명시적 사전 lookup |
| **입력 자산** | `dumps/2026-05-05-v075-hangul-hook-guide.md` (203 LOC 이 사실상 design input — Hybrid 자체사전+ModFix reflection fallback 패턴), `dumps/2026-05-05-hangul-mod-stack-analysis.md`, `dumps/2026-05-05-hangul-modpack-bundle-analysis.md` |
| **출력** | 1 spec + 1 plan + 1 release tag. 신규 `HangulDict` static class + ItemDetailPanel/ContainerPanel 표시 직전 변환 + unit test (사전 미스 fallback 검증) + 자체 사전 fallback 통팩 단독 환경 검증 |
| **결정 게이트** | hook 전략 — Hybrid (추천) vs ModFix reflection only vs 자체사전 only. 통팩 사용자가 ModFix 없이 Sirius mod 만 사용할 수 있어서 fallback 이 robust. 기본 추정: Hybrid |

**Result** (2026-05-06):
- Release: [v0.7.5](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.5)
- Spec: [2026-05-06-longyin-roster-mod-v0.7.5-design.md](2026-05-06-longyin-roster-mod-v0.7.5-design.md)
- Plan: [2026-05-06-longyin-roster-mod-v0.7.5-plan.md](../plans/2026-05-06-longyin-roster-mod-v0.7.5-plan.md)
- Smoke: [2026-05-06-v0.7.5-smoke-results.md](../dumps/2026-05-06-v0.7.5-smoke-results.md)
- Tests: 193 → 212 PASS, 인게임 smoke 14/14 PASS.
- Hook 전략: (A) Hybrid — ModFix reflection > Sirius reflection > 자체 CSV > LTLocalization.

**Patch v0.7.5.1** (2026-05-06): HangulDict stage 4 — ModFix TranslationEngine.Translate reflection 추가. 합성어 부분 한글화 (절세长矛 → 절세장검). [Spec](2026-05-06-longyin-roster-mod-v0.7.5.1-design.md) / [Smoke](../dumps/2026-05-06-v0.7.5.1-smoke-results.md) / [Release](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.5.1).

**Patch v0.7.5.2** (2026-05-06): Cell 24×24 정사각형 + 한자 → 48×24 가로 직사각형 + 한글 라벨 (장비/단약/음식/비급/보물/재료/말). cell 내부 강화/착 마커 제거 (row text 정보 유지). 216 tests + smoke 11/11. [Spec](2026-05-06-longyin-roster-mod-v0.7.5.2-design.md) / [Smoke](../dumps/2026-05-06-v0.7.5.2-smoke-results.md) / [Release](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.5.2).

### 2.3 v0.7.6 설정 panel ✅ 완료 (v0.7.6, 2026-05-08)

| 항목 | 내용 |
|---|---|
| **목표** | F11 메뉴 또는 별도 panel 에서 사용자가 직접 편집 가능한 설정 — hotkey 변경 / 컨테이너 정원 / 창 크기 / 검색·정렬 상태 영속화 |
| **입력 자산** | 기존 BepInEx ConfigEntry (v0.7.1 의 InventoryMaxWeight / StorageMaxWeight), `dumps/2026-05-05-v075-cheat-feature-reference.md` Section 7 (IMGUI 윈도우 패턴) + Section 11 (CheatProfiles 영속화 패턴) |
| **출력** | 1 spec + 1 plan + 1 release tag. SettingsPanel 신규 IMGUI window + ConfigEntry 추가 + 검색·정렬 상태 BepInEx config 영속화 (현재 메모리만) |
| **결정 게이트** | 자체 IMGUI panel vs sinai BepInExConfigManager 위임 vs Hybrid (단순 항목은 ConfigManager 에 노출, 검색·정렬 같은 stateful 항목만 자체 panel). 기본 추정: Hybrid |

**Result** (2026-05-08):
- Release: [v0.7.6](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.6)
- Spec: [2026-05-08-longyin-roster-mod-v0.7.6-design.md](2026-05-08-longyin-roster-mod-v0.7.6-design.md)
- Plan: [2026-05-08-longyin-roster-mod-v0.7.6-plan.md](../plans/2026-05-08-longyin-roster-mod-v0.7.6-plan.md)
- Smoke: [2026-05-08-v0.7.6-smoke-results.md](../dumps/2026-05-08-v0.7.6-smoke-results.md)
- Tests: 216 → 238 PASS (+22), 인게임 smoke 28/28 PASS.
- Decision Gate: **B** Hybrid stateful-only — 자체 SettingsPanel 은 hotkey rebind 4 + ContainerPanel rect buffer 편집 + 영속화 정보 read-only 표시. ConfigEntry 16개는 sinai BepInExConfigManager F5 위임.
- Q2 영속화 scope: 검색 textbox 휘발 / 정렬 key·방향·필터·last container·panel rect 5개 영속.
- Q3-1 Hotkey 범위: B — 4 키 ConfigEntry (MainKey 재사용 + Character/Container/Settings 신규). Numpad 자동 derive (Alpha↔Keypad).
- Q3-2 컨테이너 정원: A — Inventory/StorageMaxWeight 만 (외부 컨테이너 무제한 유지).
- 자유 입력: F11 메뉴 항목 추가 / [저장] 버튼 명시 (buffer + dirty + can-save) / [기본값 복원] 버튼 + [영속화 정보 reset] 별도.
- Spike: EventType.KeyDown + Event.current.keyCode strip-safe 검증 PASS — fallback 미필요.

### 2.4 v0.7.7 Item editor ✅ 완료 (v0.7.7, 2026-05-09)

| 항목 | 내용 |
|---|---|
| **목표** | ItemDetailPanel view-only 필드를 edit-able 로 확장. game-self method 우선 + reflection setter fallback. Apply/Restore 흐름과 별개 (즉시 in-memory 수정) |
| **입력 자산** | v0.7.4 ItemDetailReflector + v0.7.4.x curated 매트릭스 + `dumps/2026-05-05-v075-cheat-feature-reference.md` Section 2 (`ItemGenerator.AddCloneWithLv`, `SaveDataSanitizer`, `ItemData.CountValueAndWeight`) |
| **출력** | 1 spec + 1 plan + 1 release tag (G1 에서 GO 결정 시) |
| **결정 게이트** | G1 진입 시점에 Item editor scope 확정 (어떤 필드를 edit-able? 강화 lv 만? sub-data 까지?) |

**Result** (2026-05-09):
- Release: [v0.7.7](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.7)
- Spec: [2026-05-08-longyin-roster-mod-v0.7.7-design.md](2026-05-08-longyin-roster-mod-v0.7.7-design.md)
- Plan: [2026-05-08-longyin-roster-mod-v0.7.7-plan.md](../plans/2026-05-08-longyin-roster-mod-v0.7.7-plan.md)
- Smoke: [2026-05-09-v0.7.7-smoke-results.md](../dumps/2026-05-09-v0.7.7-smoke-results.md)
- Tests: 238 → 304 PASS (+66), 사용자 5 iteration 인게임 검증 PASS.
- **Brainstorm 결과**: Q1=A 단일 Phase / Q2=C Hybrid (reflection + read-back + regenerate fallback placeholder) / Q3=D Aggressive sanitize (Range + CountValueAndWeight + RefreshMaxAttriAndSkill) / Q4=A `[편집]` 토글 / Q5=B Disclaimer + range matrix / Q6=A 인벤·창고만 edit.
- **Spike 결과**:
  - itemLv/rareLv/equipmentData.enhanceLv 모두 **Property setter** (Field 부재). reflection setter Field+Property 양쪽 시도.
  - `RefreshSelfState` 부재 → 진짜 method = **`RefreshMaxAttriAndSkill()`**.
  - **HeroSpeAddData 구조** 확정 = `Dictionary<int, float>` + `Get(int) → float`, `Set(int, float) → self`, `GetKeys() → List<int>`. Remove method 부재 → `heroSpeAddData.Remove(int)` Dictionary 직접 호출.
  - LongYinCheat 디컴파일 cache 의 SpeAddTypeNames 풀 dump (134 entry, idx 0~207) 추출.
- **사용자 5 iteration fix narrative**:
  1. rareLv/itemLv row 안 보임 → ItemDetailReflector curated 무시 + ItemEditFieldMatrix 직접 렌더
  2. 강화/특수 강화 단순 수치 무의미 → 매트릭스 제거 + HeroSpeAddData stat editor 도입
  3. spike v1 부족 (Property 검색 안 함) → spike v2 보강
  4. SpeAddType 55+ "기타" 표시 → 134 entry 풀 dump
  5. Dropdown UI 작은 panel → SelectorDialog modal popup + 등급/품질도 selector + panel 480×640 + 라벨 swap fix (itemLv=등급, rareLv=품질)
- **신규 strip-safe 검증**: 추가 IMGUI API 도입 없음 (v0.7.6 검증 그대로 사용).
- **다음 자산 활용 가능**: HeroSpeAddDataReflector + SpeAddTypeNames + SelectorDialog + ItemEditApplier.PostMutationRefresh — v0.7.8 Player editor / NPC editor 등에서 mirror.

### 2.5 v0.8 (후보) 진짜 sprite 도입

| 항목 | 내용 |
|---|---|
| **목표** | ItemCellRenderer placeholder block 만 sprite blit 으로 교체. IL2CPP sprite asset 접근 + IMGUI texture caching |
| **입력 자산** | v0.7.3 cell 구조 (`CategoryGlyph`, `ItemCellRenderer.Draw / GradeColor / QualityColor`) + `dumps/2026-05-05-v075-cheat-feature-reference.md` Section 12 (IconHelper 패턴) + LongYinCheat `IconHelper.cs` (316 LOC) deep-dive 필요 |
| **출력** | 1 spec + 1 plan + 1 release tag (G2 에서 GO 결정 시) |
| **결정 게이트** | G2 시점에 IL2CPP sprite asset 접근 가능성 spike 후 GO/NO-GO. partial 도입 (일부 카테고리만 sprite 매칭) 도 고려 |

### 2.6 maintenance 모드 (operational, spec 없음)

| 항목 | 내용 |
|---|---|
| **목표** | 게임 patch / 통팩 한글모드 release / BepInEx 호환성 변경 시 사용자 mod 점검 + 패치 |
| **입력 자산** | 게임 patch notes + `git log` 차이 + 인게임 smoke + Section 4 의 점검 매트릭스 |
| **출력** | hotfix release tag (예: v0.7.6.1) 또는 메타 spec 의 "Maintenance YYYY-MM-DD: PASS" append |
| **트리거** | 사용자 명시적 선언 only (자동 감지 안 함) |

## 3. 후보 단계 결정 메커니즘

### 3.1 G1 결정 게이트 — v0.7.6 완료 직후

| 항목 | GO 조건 | DEFER 조건 | NO-GO 조건 |
|---|---|---|---|
| **v0.7.7 Item editor** | 인게임 사용 중 "강화 lv 직접 수정" 수요 명확 + Apply/Restore 흐름과 충돌 없음 확인 | scope 모호 (어떤 필드까지?) — spike 1단계 추가 필요 | 사용자 mod 정체성 (snapshot 관리) 와 분리 어색 — LongYinCheat 으로 위임 가능 |
| **v0.8 진짜 sprite** | IconHelper deep-dive + IL2CPP sprite asset spike PASS — placeholder block → texture blit 가능성 입증 | Spike 결과 일부 카테고리만 sprite 매칭 가능 — partial 도입 검토 | placeholder 가 충분히 직관적 + 사용자 피드백 우선순위 낮음 |
| **maintenance 모드** | 게임 patch release 감지 (사용자 명시적 선언) | patch 예정 공지만 있고 release 미정 | patch 영향 없음 (BepInEx/Harmony 시그니처 변경 없음) |

**G1 결정 출력 형식** (메타 spec 에 commit 으로 append):
```
## G1 Decision (YYYY-MM-DD)
- v0.7.7 Item editor: GO / DEFER until <date or trigger> / NO-GO  →  rationale
- v0.8 진짜 sprite: GO / DEFER until <date or trigger> / NO-GO   →  rationale
- maintenance: ACTIVATE / WAIT                                    →  rationale
- Next sub-project: vX.Y (or maintenance hotfix)
```

### 3.2 G2 결정 게이트 — v0.7.7 완료 직후 (G1 에서 v0.7.7 GO 했을 때만 도달)

- **목표**: v0.8 vs maintenance vs 신규 후보 sub-project 분기
- **신규 후보 후보군** (HANDOFF.md 의 미결정 candidates): v0.7.8 Apply 미리보기, v0.7.9 Slot diff, v0.7.10 NPC 지원
- **출력 형식**: G1 과 동일 (`## G2 Decision (YYYY-MM-DD)` append)

### 3.3 게이트 진행 룰

1. **각 게이트는 별도 commit** — 메타 spec 에 결정 섹션 append, 결정 근거 명시
2. **DEFER 결정은 시한 명시 필수** — "v0.7.6 까지" / "다음 게이트까지" 같은 명시. indefinite DEFER 금지
3. **NO-GO 결정도 기록** — 영구 폐기 vs 다른 trigger 발생 시 재평가 명시
4. **사용자 명시적 결정 필수** — 게이트 진입 시 brainstorming skill 한 번 더 호출 (각 후보 1-2 질문 cycle)

## 4. Maintenance 모드 정의

### 4.1 진입 조건 (사용자 명시적 선언만)

- **트리거 1**: 게임 patch release (예: 1.0.0f8.2 → 1.0.0f8.3 또는 1.0.1f3 등)
- **트리거 2**: 통팩 한글모드 release (Sirius / ModFix 신규 빌드)
- **트리거 3**: BepInEx / Harmony / Il2CppInterop 의 breaking 업데이트
- **자동 감지 안 함** — 사용자가 "maintenance 모드 진입" 선언했을 때만 활성화

### 4.2 진입 시 점검 매트릭스

| 점검 항목 | 도구 / 자산 |
|---|---|
| **Harmony patch 시그니처 일치** | `git log -- src/LongYinRoster/Patches/` + 게임 patch dll 디컴파일 (`ilspycmd 8.2.0` — `C:\Users\deepe\.dotnet\tools\ilspycmd.exe -p -o <out> <dll>`) |
| **IL2CPP wrapper 변경** | `Il2CppInterop` 버전 체크 + 핵심 reflection path (HeroData.itemListData / faceData / partPosture / kungfuSkills 등) 라이브 검증 |
| **GameAssembly.dll offset (한글화 영향)** | 사용자 mod 는 StringLiteral.json offset 사용 안 함 → 영향 없음. 단 Sirius/ModFix 호환성 영향 시 `dumps/2026-05-05-hangul-mod-stack-analysis.md` 참조 |
| **ItemData / HeroData 필드 schema** | `ItemDetailReflector.GetRawFields` dump 비교 (전 / 후 patch) |
| **save 파일 schema** | `_meta.applySelection` JSON schema 호환성 |
| **인게임 smoke** | 기존 6/6 smoke 절차 (HANDOFF 의 v0.7.4 smoke) 재실행 |

### 4.3 출력

- **결과 = OK**: 메타 spec 에 "Maintenance YYYY-MM-DD: PASS — no changes needed" append, smoke 결과 commit
- **결과 = 호환성 깨짐**: hotfix release (예: v0.7.6.1) — 별도 spec 없이 fix commit + smoke + tag
- **결과 = 큰 변경 필요**: 정식 sub-project (예: v0.7.10) brainstorm cycle 진입

### 4.4 룰

1. **maintenance 모드 동안 다른 sub-project 진행 안 함** — 한 번에 하나만
2. **maintenance 종료 = 명시적 선언** — "maintenance 종료" + 다음 단계 진입 명시
3. **통팩 한글모드 업데이트는 영향 평가만** — 사용자 mod 가 ModFix dict 에 reflection 의존하는 경우만 영향. v0.7.5 의 Hybrid 구조 (자체 사전 fallback) 가 이 위험 흡수
4. **maintenance 도중 발견된 신규 후보** = G3 게이트 처리 (메타 spec 에 G3 섹션 추가, 평가 매트릭스는 G1/G2 와 동일 형식)

## 5. Spec 컨벤션 + 진행 룰

### 5.1 Per-sub-project Cycle (v0.5~v0.7.4 패턴 유지)

```
1. brainstorm   → docs/superpowers/specs/YYYY-MM-DD-longyin-roster-mod-vX.Y-design.md
2. plan         → docs/superpowers/plans/YYYY-MM-DD-longyin-roster-mod-vX.Y-plan.md
3. impl         → src/LongYinRoster/* + 신규 unit tests
4. smoke        → docs/superpowers/dumps/YYYY-MM-DD-vX.Y-smoke-results.md
5. release      → VERSION bump + CHANGELOG + git tag vX.Y + GitHub release
6. handoff      → HANDOFF.md "현재 main baseline" + "다음 sub-project" 갱신
```

### 5.2 메타 spec 라이프사이클

- **위치**: 본 파일 (`docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`)
- **상태**: living document — 게이트 결정마다 append (G1 Decision / G2 Decision / Maintenance YYYY-MM-DD: PASS 등)
- **각 sub-project 의 spec 은 메타 spec 의 1-pager 를 link 로 참조** + 메타 spec 의 결정 게이트가 진입 시점 결정의 source-of-truth
- **메타 spec 종료 조건**: 모든 후보 sub-project 가 GO/NO-GO 결정 + maintenance 모드 룰 검증 완료 → archive (filename suffix `-archived.md` 으로 rename)

### 5.3 Sub-project 진입 시 brainstorm 범위

- **확정 sub-project (v0.7.4.x / v0.7.5 / v0.7.6)**: 짧은 brainstorm — 메타의 1-pager + dumps 자산이 design input. 2-3 질문 cycle 후 spec/plan 진입
- **후보 sub-project (v0.7.7 / v0.8)**: G1/G2 결정 게이트에서 GO 결정한 직후 정식 brainstorm — 5-10 질문 cycle (scope 미확정이므로)
- **maintenance hotfix**: brainstorm 생략 — 즉시 점검 매트릭스 실행 + fix commit

### 5.4 진행 게이트 룰

| 게이트 | 위치 | 통과 조건 |
|---|---|---|
| **각 sub-project spec 통과** | brainstorm 끝 | 사용자가 spec review 후 명시적 승인 |
| **각 sub-project plan 통과** | writing-plans skill 끝 | 사용자가 plan review 후 명시적 승인 |
| **각 sub-project release** | smoke + tag | 인게임 smoke PASS + unit tests 100% + handoff 갱신 |
| **G1 / G2** | v0.7.6 / v0.7.7 release 직후 | 메타 spec 에 결정 commit |
| **maintenance 진입** | 사용자 명시적 선언 | 점검 매트릭스 즉시 실행 |

### 5.5 명명 컨벤션

- **확정 단계**: `vX.Y` (예: v0.7.5, v0.7.6)
- **patch 단계**: `vX.Y.Z` (예: v0.7.4.1)
- **hotfix**: `vX.Y.Z` (예: v0.7.6.1) — patch 와 형식 동일하지만 의미는 maintenance fix
- **메타 spec slug**: `longyin-roster-mod-roadmap-v0.7.4-to-v0.8` (범위 명시)
- **sub-project spec slug**: `longyin-roster-mod-vX.Y-design` (기존 컨벤션)

## 6. 다음 액션

1. ✅ **본 메타 spec review**: 사용자 확인 후 git commit (2026-05-05 commit `0ff3e00`)
2. ✅ **v0.7.4.x sub-project**: v0.7.4.1 release 완료 (2026-05-05) — §2.1 Result 섹션 참조
3. **v0.7.5 D-4 Item 한글화 sub-project brainstorm 진입** (다음 작업): 메타의 §2.2 1-pager + `dumps/2026-05-05-v075-hangul-hook-guide.md` (Hybrid 자체사전+ModFix reflection fallback) 를 design input 으로 정식 brainstorm cycle 시작
4. **이후 cycle**: §5.1 의 6단계를 v0.7.5 → v0.7.6 → G1 순으로 반복

---

## Decision Log (append-only, 게이트 진입 시 추가)

### G1 Decision (2026-05-08)

v0.7.6 release 직후 G1 게이트 평가:

- **v0.7.7 Item editor**: **GO** → rationale = ItemDetailPanel 의 view-only 자산 (v0.7.4 ItemDetailReflector + v0.7.4.1 7-카테고리 curated 매트릭스) 이 edit-able 로 자연스럽게 확장 가능. 사용자가 명시적으로 v0.7.7 진입 선택 (2026-05-08). game-self method 우선 + reflection setter fallback 패턴은 이미 v0.6.x Apply pipeline 에서 검증됨 (Apply/Restore 흐름과는 별개로 즉시 in-memory 수정). LongYinCheat dump (`dumps/2026-05-05-v075-cheat-feature-reference.md` §2/§9) 가 차용 가능 자산 풍부.
- **v0.8 진짜 sprite**: **DEFER until G2** → rationale = G2 시점까지 보류. v0.7.7 Item editor 가 placeholder 글리프 위에 충분히 작동하면 sprite 시급도 낮음. IL2CPP sprite asset spike 비용 별도. v0.7.7 완료 후 사용자 피드백 보고 G2 에서 재평가.
- **maintenance**: **WAIT** → rationale = 게임 patch / 통팩 한글모드 release / BepInEx breaking 변경 알림 없음. trigger 발생 시 사용자 명시적 선언으로 활성화.

Next sub-project: **v0.7.7 (Item editor)** brainstorm cycle 시작 (메타 §5.3 후보 sub-project = 5~10 질문 cycle).

### G2 Gate Pending (v0.7.7 release 직후, 2026-05-09)

v0.7.7 release 완료 (304 tests + 사용자 5 iteration 검증 PASS). G2 게이트 진입 — 평가 대상:

- **v0.8 진짜 sprite** — IL2CPP sprite asset spike 필요. ItemCellRenderer placeholder 글리프 → sprite blit. 평가: GO / DEFER / NO-GO.
- **신규 후보 v0.7.8 Player editor** — HeroData 의 `baseAddData / totalAddData / heroBuff` (HeroSpeAddData) 편집. v0.7.7 자산 (HeroSpeAddDataReflector + SpeAddTypeNames + SelectorDialog + ItemEditApplier.PostMutationRefresh) **거의 100% 재사용 가능** — 가장 자연스러운 후속.
- **신규 후보 v0.7.8 Apply 미리보기** / **v0.7.9 Slot diff** / **v0.7.10 NPC 지원** — HANDOFF 후보 list 보존.
- **maintenance** — 게임 patch / 통팩 한글모드 release 알림 시 활성화.

G2 Decision 은 사용자 명시 선택 시점에 본 spec 에 append.

### G2 Decision (2026-05-09)

v0.7.7 release 완료 직후 G2 게이트 평가:

- **v0.7.8 Player editor**: **GO** → rationale = v0.7.7 자산 (HeroSpeAddDataReflector + SpeAddTypeNames + SelectorDialog + ItemEditApplier.PostMutationRefresh) 거의 100% 재사용. HeroData 의 baseAddData/totalAddData/heroBuff (HeroSpeAddData 인스턴스 3개) + resource stats (hp/maxhp/power/mana/weight) 편집. 사용자가 명시적으로 v0.7.8 Player editor 진입 선택 (2026-05-09).
- **v0.8 진짜 sprite**: **DEFER until G3** → rationale = v0.7.8 후 재평가. IL2CPP sprite spike 비용 별도. placeholder 가 사용자 사용 흐름에 충분.
- **v0.7.9 Slot diff / v0.7.10 NPC 지원**: **DEFER until G3** → rationale = v0.7.8 의 NPC 스코프 결정 후 자연스러운 후속 평가.
- **maintenance**: **WAIT** → rationale = trigger 미발견.

Next sub-project: **v0.7.8 (Player editor)** brainstorm cycle 시작 (메타 §5.3 후보 sub-project = 5~10 질문 cycle).

### v0.7.8 Result (2026-05-09)

- Release: [v0.7.8](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.8)
- Spec: [2026-05-09-longyin-roster-mod-v0.7.8-design.md](2026-05-09-longyin-roster-mod-v0.7.8-design.md)
- Plan: [2026-05-09-longyin-roster-mod-v0.7.8-plan.md](../plans/2026-05-09-longyin-roster-mod-v0.7.8-plan.md)
- Smoke: [2026-05-09-v0.7.8-smoke-results.md](../dumps/2026-05-09-v0.7.8-smoke-results.md)
- Tests: 304 → 327 PASS (+23 PlayerEditApplier + clamp). 사용자 **11 iteration** 검증 PASS.
- **Brainstorm 결과**: Q1=B+자유입력 (Resource + HeroSpeAddData + 천부 + 무공) / Q2=C 3 wrapper / Q3=C Hybrid / Q4=A 별도 PlayerEditorPanel + F11+4 / Q5=B Quick actions / Q6=A heroID=0 only.
- **Spike 3 결과**: 부상 stat = `externalInjury/internalInjury/poisonInjury` (Property). heroTagData = `List<HeroTagData>` 27, AddTag/RemoveTag/FindTag/HaveTag method ✓. kungfuSkills = `List<KungfuSkillLvData>` 168, **`lv` (NOT `level`) read-only**, propWrite=True for fightExp/bookExp/skillID/equiped, LoseSkill ✓ single remove, GetSkill 3-arg ✓.
- **Spike v3 결과**: HeroTagDataBase fields = `name/value/category/sameMeaning/order` — 카테고리 (무학/고급/기예/천생/지향/취향/전법) + sameMeaning 그룹 progression 인식.
- **사용자 11 iteration fix narrative**:
  1. maxhp/Mana/Power camelCase + realMax sync (cheat StatEditor 패턴)
  2. Quick 통합 (회복+채움) + max 표시 제거 + current value max clamp
  3. 무공 lv → `lv` (cheat 검증)
  4. SpeAddType 134 entry 풀 dump (idx 0~207)
  5. SelectorDialog modal popup + 등급/품질 selector
  6. 강화/특수강화 → 돌파속성 dialog (Iteration 6 = v0.7.7 사용자 피드백 with v0.7.7 release 직전)
  7. PlayerEditorPanel 5 fixes (lv 표시 / 카테고리 9탭 / 페이징 / F-B 안내 / 등급 secondary tab)
  8. 돌파속성 inline expand → SkillBreakthroughDialog 별도 popup
  9. 로그 폭주 16만 lines fix → InfoOnce/WarnOnce + HeroLocator/모든 reflector/UI panel 적용
  10. 천부 추가 인게임 메커니즘 (sameMeaning 그룹 progression with auto-remove + downgrade 거부)
  11. 추가 버튼 삭제 + selector 즉시-add + 보유 marker (✓) + 줄바꿈 fix
- **신규 자산**: PlayerEditApplier / HeroTagDataReflector / HeroTagNameCache / KungfuSkillEditor / SkillNameCache / ForceNameCache / SkillBreakthroughDialog / SelectorDialog 2단계 탭 + markedFn / Logger.InfoOnce-WarnOnce / ApplyTagAddSmart.

### G3 Gate Pending (v0.7.8 release 직후, 2026-05-09)

v0.7.8 release 완료 (327 tests + 사용자 11 iteration 검증 PASS). G3 게이트 진입 — 평가 대상:
- **v0.8 진짜 sprite** — IL2CPP sprite asset spike → GO/DEFER/NO-GO. cheat IconHelper.cs 316 LOC 참조 가능.
- **v0.7.8.1 Lock 시스템 / 천부 max 수정** — cheat StatEditor LockedMax 매 frame 패턴. v0.7.8 자산 그대로 차용.
- **v0.7.10 NPC 지원** — heroID=0 외 캐릭터 (v0.7.8 자산 100% 재사용 — heroID dropdown 만 추가).
- **v0.7.9 Slot diff preview** — Apply 흐름 변경 (별도 spike).
- **maintenance** — trigger 발생 시 활성.

G3 Decision 은 사용자 명시 선택 시점에 본 spec 에 append.

### G3 Decision (2026-05-09)

v0.7.8 release 직후 G3 게이트 평가:

- **v0.7.8.1 LockedMax + v0.7.10 NPC editor 결합 (사용자 ③ 채택, 단일 cycle)**: **GO** → rationale = LockedMax (cheat GameplayPatch.GetMaxTagNum Postfix 100% mirror, 매우 작음) + NPC editor 의 자연스러운 결합. 그러나 brainstorm Q4 = β 채택 → NPC dropdown 은 v0.7.11 분리, **v0.7.10 = LockedMax + 속성·무학·기예 editor**. 사용자 screenshot 으로 속성 6 (근력/민첩/지력/의지/체질/경맥) / 무학 9 (내공/경공/절기/권장/검법/도법/장병/기문/사술) / 기예 9 (의술/독술/학식/언변/채벌/목식/단조/제약/요리) 의 수치 + 자질값 편집 통증 명확. cheat CharacterFeature.cs 의 ChangeAttri/ChangeFightSkill/ChangeLivingSkill (baseAttri/maxAttri/ChangeAttri game-self method + reflection fallback) 검증된 패턴. v0.7.8 PlayerEditorPanel 자산 90% 재사용 + secondary tab `[기본 / 속성]` 추가.
- **v0.8 진짜 sprite**: **DEFER until G4** → rationale = G4 시점까지 보류. v0.7.10 + v0.7.11 후 재평가. IL2CPP sprite asset spike 비용 별도.
- **v0.7.9 Slot diff preview**: **DEFER until G4** → rationale = Apply pipeline 변경 cycle, NPC editor (v0.7.11) 후 자연스러운 후속.
- **v0.7.11 NPC dropdown** (β 의 후속): brainstorm Q4 결정 — v0.7.10 의 모든 자산이 hero 인자 받도록 generalize 후 v0.7.11 cycle 에서 heroID dropdown 추가. v0.7.10 release 후 G3.5 mini-gate 또는 G4 에서 진입.
- **maintenance**: **WAIT** → rationale = trigger 미발견.

Next sub-project: **v0.7.10 (천부 max lock + 속성·무학·기예 editor)** brainstorm cycle 종료 — spec 작성 완료 (`2026-05-09-longyin-roster-mod-v0.7.10-design.md`). plan 진입 대기.

### v0.7.10 Brainstorm 결과 (2026-05-09)

9 question cycle:
- G3 = E (B + A 조합) → ③ 단일 cycle (1 spec / 2-phase impl / tag = v0.7.10)
- Q1 = A — Lock scope = 천부 max only (`GetMaxTagNum` Postfix)
- Q2 = A — Lock 적용 = Player heroID=0 only (cheat 패턴)
- Q3 = A — Lock UX = PlayerEditorPanel 천부 섹션 헤더 (체크박스 + TextField)
- Q4 = β — 2 단계 분리 (v0.7.10 = LockedMax + 속성/무학/기예, v0.7.11 = NPC dropdown)
- Q5 = E — secondary tab `[기본 / 속성]`
- Q6 = B — per-row inline TextField × 2 + 일괄 button
- Q7 = B — [저장] gated, sanitize 1회
- Q8 = B — 수치/자질값 + buff/effective read-only (자질 grade marker deferred)
- Q9 = A — Lock 토글 즉시 적용 + ConfigEntry 자동 영속

Spec: [2026-05-09-longyin-roster-mod-v0.7.10-design.md](2026-05-09-longyin-roster-mod-v0.7.10-design.md)

### v0.7.10 Result (2026-05-09)

- Release: [v0.7.10](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.10) (push 후 link active)
- Spec: [2026-05-09-longyin-roster-mod-v0.7.10-design.md](2026-05-09-longyin-roster-mod-v0.7.10-design.md) — §13 Phase 3 addendum 포함
- Plan: [2026-05-09-longyin-roster-mod-v0.7.10-plan.md](../plans/2026-05-09-longyin-roster-mod-v0.7.10-plan.md)
- Smoke: smoke run 후 `docs/superpowers/dumps/2026-05-09-v0.7.10-smoke-results.md` 추가 (사용자 게이트)
- Tests: 327 → **374 PASS** (+47). 인게임 smoke 18+ 항목 (Phase 1 4 + Phase 2 10 + Phase 3 4+ 회귀).
- **Brainstorm 결과 (9 Q + post-Phase 2 사용자 피드백)**:
  - G3=E (B+A 결합 ③) / Q1=A LockedMax scope / Q2=A Player only / Q3=A 천부 섹션 헤더 / Q4=β 분리 (v0.7.10+v0.7.11) / Q5=E secondary tab / Q6=B inline+일괄 / Q7=B [저장] gated / Q8=B 수치/자질값+buff/effective / Q9=A 즉시 적용
  - **Phase 3 추가** (post-Phase 2 사용자 피드백): Phase 2 의 자질값 setter 가 게임 cap (120/120/100) 에 의해 silently overridden 되는 문제 발견 → cheat `LongYinCheat.MultiplierPatch` 의 4 Postfix 패턴 mirror 추가 (player heroID=0 only, ConfigEntry `EnableUncapMax` opt-in)
- **신규 자산**: GetMaxTagNumPatch / GetMaxTagNumOverride / HeroAttriReflector / CharacterAttriEditor / AttriLabels / AttriTabPanel / AttriTabBuffer / **HeroDataCapBypassPatch** / **HeroDataCapBypassLogic**
- **Phase 분리**: Phase 1 (LockedMax) commits 1-4 / Phase 2 (속성/무학/기예) commits 5-10 / Phase 3 (cap-bypass) commit 13 / docs commits 11/12/14

### G4 Gate Pending (v0.7.10 release 직후, 2026-05-09)

평가 대상:
- **v0.7.11 NPC dropdown** (★★★ 가성비 가장 높음) — heroID switch + v0.7.10 자산 generalize. PlayerEditorPanel header 에 SelectorDialog 2단계 탭 (force/문파 + name search) 추가. **Phase 3 cap-bypass 의 player-only constraint 도 per-hero list 로 generalize** (HashSet&lt;int&gt; uncappedHeroIDs).
- **v0.7.10.1 자질 grade marker** — derivation rule 또는 별도 field spike (신/하 등 enum)
- **v0.8 진짜 sprite** — IL2CPP sprite asset spike. cheat IconHelper.cs 316 LOC 참조.
- **v0.7.9 Slot diff preview** — Apply pipeline 변경 cycle.
- **maintenance** — trigger 시 활성.

G4 Decision 은 사용자 명시 선택 시점에 본 spec 에 append.
