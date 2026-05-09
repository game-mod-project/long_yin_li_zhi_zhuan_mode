# LongYin Roster Mod v0.7.10

**일시**: 2026-05-09
**baseline**: v0.7.8 (327/327 tests + 사용자 11 iteration 검증 PASS)

## 새 기능

### Phase 1 — 천부 max 보유수 lock

- PlayerEditorPanel (F11+4) 의 천부 섹션 헤더에 `[☐ Lock max] [ 999 ]` 토글 추가
- 체크 ON 시 `HeroData.GetMaxTagNum()` Postfix 가 LockedMaxTagNumValue 로 override
- Player (heroID=0) only — NPC 무영향
- BepInEx ConfigEntry 자동 영속 (`LockMaxTagNum` / `LockedMaxTagNumValue`)
- cheat LongYinCheat GameplayPatch.GetMaxTagNum 패턴 100% mirror

### Phase 2 — 속성·무학·기예 editor

- PlayerEditorPanel 헤더에 `[기본 / 속성]` secondary tab 추가
- [기본] 탭 = v0.7.8 의 6 섹션 (Resource / SpeAddData × 3 / 천부 / 무공 / Breakthrough) — 회귀 없음
- [속성] 탭 = 신규 — 3 column:
  - **속성 6** — 근력 / 민첩 / 지력 / 의지 / 체질 / 경맥
  - **무학 9** — 내공 / 경공 / 절기 / 권장 / 검법 / 도법 / 장병 / 기문 / 사술
  - **기예 9** — 의술 / 독술 / 학식 / 언변 / 채벌 / 목식 / 단조 / 제약 / 요리
- 각 row = `[라벨] [수치 input] / [자질값 input] +buff → effective`
- 일괄 button × 3 — `[전체 속성 자질]` / `[전체 무학 자질]` / `[전체 기예 자질]` (cheat SetAllAttri/FightSkill/LivingSkill mirror)
- [저장] gated apply — buffer + dirty 추적 → cheat `ChangeAttri/FightSkill/LivingSkill(hero, idx, val)` × N → `RefreshMaxAttriAndSkill()` 1회
- [되돌리기] → originals 복원
- Clamp [0, 999999] (cheat AddTalent 정렬)

### Phase 3 — 자질값 cap 돌파 (HeroDataCapBypassPatch)

Phase 2 의 자질값 edit 가 실제 작동하려면 게임의 hard cap (속성/무학 120, 기예 100) 우회가 필요. cheat `LongYinCheat.MultiplierPatch` 의 4 Harmony Postfix 패턴 100% mirror + **player heroID=0 only** constraint 적용.

- 4 Postfix patches:
  - `HeroData.GetMaxAttri(int)` Postfix → uncap on 시 `__result = UncapMaxAttri (default 999)`, off 시 defensive re-clamp 120
  - `HeroData.GetMaxFightSkill(int)` Postfix → 동상 (cap 120)
  - `HeroData.GetMaxLivingSkill(int)` Postfix → 동상 (cap 100)
  - `HeroData.RefreshMaxAttriAndSkill()` Prefix+Postfix → snapshot maxXxx 배열 → refresh 후 user-set 값 복원 (game 의 re-clamp 차단), `[ThreadStatic]` 사용
- ConfigEntry 4: `EnableUncapMax` (bool, default false, opt-in) / `UncapMaxAttri/FightSkill/LivingSkill` (int, default 999, range [120/120/100, 999999])
- 신규 file: `Core/HeroDataCapBypassPatch.cs` + `Core/HeroDataCapBypassLogic.cs` (테스트 가능 logic 분리)
- v0.7.11 에서 per-hero list 로 generalize 예정 (NPC 도 개별 적용 가능)

**왜 추가됐는지**: Phase 2 release 직후 사용자 피드백 — 빌드된 모드의 자질값 setter 가 게임 cap (120/120/100) 에 의해 silently overridden. Phase 3 이 cap 돌파를 가능하게 함.

## 변경

- `Plugin.VERSION` 0.7.8 → 0.7.10
- `PlayerEditApplier.TryInvokeRefreshMaxAttriAndSkill` (private) → `RefreshMaxAttriAndSkill` (public, Phase 2 reuse)
- BepInEx Plugin.Load 에 `Core.GetMaxTagNumPatch.Register(harmony)` 추가

## 신규 file

- `src/LongYinRoster/Core/GetMaxTagNumPatch.cs` (Phase 1 — Harmony Postfix wrapper)
- `src/LongYinRoster/Core/GetMaxTagNumOverride.cs` (Phase 1 — 테스트 가능한 logic 분리)
- `src/LongYinRoster/Core/HeroAttriReflector.cs` (Phase 2 — baseAttri/maxAttri × 3 axis read)
- `src/LongYinRoster/Core/CharacterAttriEditor.cs` (Phase 2 — game-self method invoke + clamp)
- `src/LongYinRoster/Util/AttriLabels.cs` (Phase 2 — 24 한글 라벨 + AttriAxis enum)
- `src/LongYinRoster/UI/AttriTabPanel.cs` (Phase 2 — 3-column inline + 일괄 + 저장)
- `src/LongYinRoster/Core/HeroDataCapBypassPatch.cs` (Phase 3 — Harmony Postfix wrapper, 4 patches)
- `src/LongYinRoster/Core/HeroDataCapBypassLogic.cs` (Phase 3 — 테스트 가능한 logic 분리)

## 호환성

- 기존 v0.7.8 사용자 설정 (sort/filter/last/rect/window/hotkey 4) 변경 없음 — append-only
- 게임 patch 없음 (1.0.0f8.2 그대로)

## 미반영 / 후속

- **자질 grade marker** (신/하 등) — derivation rule 미확인. v0.7.10.1 patch 또는 v0.7.11 cycle 에서 추가
- **NPC dropdown** (heroID switch) — v0.7.11 별도 cycle. v0.7.10 자산이 hero 인자 받도록 generalize 후 dropdown 추가
- **Resource stat lock** (hp/power/mana/weight) — Q1 deferred (cheat StatEditor LockedMax 매 frame 패턴, 별도 sub-project)
- **v0.8 진짜 sprite** — G3 Decision DEFER until G4

## Tests

- 327 → 374 (+47). xUnit + Shouldly + POCO mocks.
- 인게임 smoke = 18 항목 매트릭스 (Phase 1 4 + Phase 2 10 + 회귀 4) + Phase 3 7 tests

## 메타

- Spec: `docs/superpowers/specs/2026-05-09-longyin-roster-mod-v0.7.10-design.md`
- Plan: `docs/superpowers/plans/2026-05-09-longyin-roster-mod-v0.7.10-plan.md`
- Smoke: `docs/superpowers/dumps/2026-05-09-v0.7.10-smoke-results.md` (smoke run 후 채움)
- Roadmap: G3 Decision 2026-05-09 (단일 cycle B+A 결합 + Q4=β NPC 분리)
