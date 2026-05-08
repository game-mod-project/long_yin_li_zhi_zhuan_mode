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

### 2.3 v0.7.6 설정 panel (확정 sub-project)

| 항목 | 내용 |
|---|---|
| **목표** | F11 메뉴 또는 별도 panel 에서 사용자가 직접 편집 가능한 설정 — hotkey 변경 / 컨테이너 정원 / 창 크기 / 검색·정렬 상태 영속화 |
| **입력 자산** | 기존 BepInEx ConfigEntry (v0.7.1 의 InventoryMaxWeight / StorageMaxWeight), `dumps/2026-05-05-v075-cheat-feature-reference.md` Section 7 (IMGUI 윈도우 패턴) + Section 11 (CheatProfiles 영속화 패턴) |
| **출력** | 1 spec + 1 plan + 1 release tag. SettingsPanel 신규 IMGUI window + ConfigEntry 추가 + 검색·정렬 상태 BepInEx config 영속화 (현재 메모리만) |
| **결정 게이트** | 자체 IMGUI panel vs sinai BepInExConfigManager 위임 vs Hybrid (단순 항목은 ConfigManager 에 노출, 검색·정렬 같은 stateful 항목만 자체 panel). 기본 추정: Hybrid |

### 2.4 v0.7.7 (후보) Item editor

| 항목 | 내용 |
|---|---|
| **목표** | ItemDetailPanel view-only 필드를 edit-able 로 확장. game-self method 우선 + reflection setter fallback. Apply/Restore 흐름과 별개 (즉시 in-memory 수정) |
| **입력 자산** | v0.7.4 ItemDetailReflector + v0.7.4.x curated 매트릭스 + `dumps/2026-05-05-v075-cheat-feature-reference.md` Section 2 (`ItemGenerator.AddCloneWithLv`, `SaveDataSanitizer`, `ItemData.CountValueAndWeight`) |
| **출력** | 1 spec + 1 plan + 1 release tag (G1 에서 GO 결정 시) |
| **결정 게이트** | G1 진입 시점에 Item editor scope 확정 (어떤 필드를 edit-able? 강화 lv 만? sub-data 까지?) |

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

(아직 결정 없음 — 첫 entry 는 v0.7.6 release 직후 G1 게이트)
