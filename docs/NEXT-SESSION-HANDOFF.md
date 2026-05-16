# LongYin Roster Mod — 다음 세션 핸드오프 (focused)

**작성**: 2026-05-10
**현재 baseline**: **v0.7.12** (commit `3cee426`, tag `v0.7.12` pushed) — Cat 3 deferred 완료
**저장소**: https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode (`main`)
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`

---

## 0. 상태 한 줄 요약

v0.7.12 release 완료 (push + GitHub release 모두 OK). 399 tests PASS. **단 인게임 smoke 미완료** — 마지막 세션 종료 시 사용자 게임 실행 중이라 DLL 자동 배포 실패. 다음 세션 첫 작업 = DLL 재배포 + smoke.

---

## 1. 즉시 작업 (다음 세션 시작 시) — **사용자 게이트**

### A. v0.7.12 smoke (필수, 미완료)

**전제**: 게임 종료된 상태.

```bash
# 1. DLL 재배포 (build trigger, 자동 deploy)
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
dotnet build -c Release src/LongYinRoster/LongYinRoster.csproj

# 2. DLL deploy 확인
ls -la "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/LongYinRoster.dll"
# 갱신 timestamp 확인

# 3. 게임 실행 → F11+2 (ContainerPanel)
```

**Smoke 매트릭스 (7 op + UI)**:

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 인벤 row 선택 → [→ 이동] → 컨테이너 추가 | 토스트 "이동: N개 성공" (Info) + 컨테이너 갱신 |
| 2 | toolbar `[↶ Undo]` 노랑 강조 → 클릭 | 인벤 복원 + 컨테이너 row 제거. 토스트 "↶ Undo: ..." |
| 3 | 인벤 row 선택 → [→ 복사] | Undo 클릭 시 컨테이너 row 만 제거 (인벤 unchanged) |
| 4 | 창고 [→ 이동] / [→ 복사] | sourceLabel "창고" 인식 |
| 5 | 컨테이너 row 선택 → [← 인벤으로 이동] | Undo 시 컨테이너 복원 + 인벤 마지막 N 제거 |
| 6 | 컨테이너 row 선택 → [← 창고로 복사] | Undo 시 창고 마지막 N 제거 |
| 7 | 컨테이너 row 선택 → [☓ 삭제] (다중) | Undo 시 컨테이너 row 복원 |
| 8 | 무게 cap 초과 → [← 창고로 이동] | 토스트 "이동 → 창고: 0개 성공 / N개 실패" (Error 색상) |
| 9 | over-cap 발생 → [← 인벤으로 이동] | KoreanStrings.ToastInvOvercap format 토스트 (Warning이전 → 현재 Error 색상) |
| 10 | 작업 도중 다른 op 수행 | Undo 가 단일 op 만 — 새 op 가 _last 덮어쓰는지 |

**회귀 검증**: v0.7.11 UX 기능 (collapse / split / 일괄선택 / 등급 cycle / 무공 secondary / 결과 카운터 / Clone / resize) 모두 정상.

**Smoke 결과 기록**: `docs/superpowers/dumps/2026-05-10-v0.7.12-smoke-results.md` 작성 → commit.

### B. 발견된 issue 가 있으면 — v0.7.12.1 hotfix

알려진 trade-off (smoke 시 정상 동작 여부 확인):
1. **Move undo 의 무게 cap**: maxWeight=9999 사용 (cap 검증 skip). 게임 안 무게 표시가 이상해도 underlying 정상.
2. **Copy undo 의 마지막 N 제거**: 사용자가 다른 op 후 Undo 시 stale (single-stack 으로 자동 mitigate).
3. **Undo 도중 toast 형식**: `↶ Undo: 이동 5개 from 인벤토리 → 컨테이너` — 사용자가 의도 명확 인지 가능?

---

## 2. G6 Gate 후보 sub-project (우선순위 추정)

### ★★★ v0.7.13 NPC dropdown (가성비 가장 높음)

**Scope**: PlayerEditorPanel (F11+4) 의 모든 자산이 `hero` 인자 받음 (v0.7.10 의 generalize 완료). v0.7.13 = heroID dropdown 추가만.

**구현 진입점**:
- `src/LongYinRoster/UI/PlayerEditorPanel.cs` — 헤더에 `[heroName ▼]` dropdown 추가
- `_currentHero` field (default `HeroLocator.GetPlayer()`)
- SelectorDialog 2단계 탭: primary = force (문파), secondary = 캐릭터 list
- HeroLocator 확장 — `GetHeroById(int)`, `GetAllHeroes()` (이미 일부 있을 수 있음 — 확인)

**자산 baseline**: v0.7.10 `PlayerEditApplier` / `HeroAttriReflector` / `CharacterAttriEditor` / `HeroTagDataReflector` / `KungfuSkillEditor` 모두 hero 인자 받음 → NPC 도 그대로 작동.

**Phase 3 cap-bypass 의 player-only constraint**: `HeroDataCapBypassPatch` 가 `__instance.heroID == player.heroID` 검사 — NPC editor 진입 시 per-hero list (HashSet&lt;int&gt; uncappedHeroIDs) 로 generalize 필요.

**예상 LOC**: ~300 source + ~30 tests.

### ★★ Cat 3 deep stack + Redo (v0.7.12 후속)

**Scope**: v0.7.12 single-op stack 을 deep (10+) 로 확장 + Redo button.

**기존 자산**:
- `ContainerOpUndo` static class — 단일 `_last` 변수
- `OpRecord` immutable

**변경**: `List<OpRecord> _undoStack` + `List<OpRecord> _redoStack`. push/pop pattern.

**예상 LOC**: ~100 source + ~10 tests.

### ★★ v0.7.10.x 자질 grade marker

**Scope**: PlayerEditorPanel 의 [속성] 탭 row 에 신/하 등 grade marker 표시.

**Spike 필요**:
- 게임 schema 에 별도 `attriQuality` field 가 있는지
- 또는 value threshold 기반 derivation (e.g., ≥400=신, 200~399=상, ...)
- cheat reference 분석

**예상 LOC**: ~150 source + ~10 tests + spike.

### ★ v0.8 진짜 sprite (큰 spike)

**Scope**: ItemCellRenderer 의 placeholder 글리프 → real game sprite blit. ContainerPanel Cat 6 + 통합.

**Spike**:
- IL2CPP sprite asset 접근 (Sprite/Texture2D resolution + lazy-load)
- IMGUI texture caching
- cheat IconHelper.cs 316 LOC 참조 (`C:/Users/deepe/AppData/Local/Temp/longyincheat_decomp/LongYinCheat/IconHelper.cs` 추정)

**Risk**: spike NO-GO 가능 (IL2CPP sprite resolution 미검증).

**예상 LOC**: 큰 변동, spike 결과 따라.

### ★ v0.7.9 Slot diff preview

**Scope**: Slot Apply 직전 변경 필드 미리보기.

**Risk**: Apply pipeline 구조 변경 필요. v0.6.x Applier chain 영향 큼.

### maintenance

**Trigger**: 게임 patch / 통팩 한글모드 release / BepInEx 호환성 변경 시. 현재 trigger 미발견.

---

## 3. 시작 명령 (다음 세션 첫 메시지 후보)

다음 세션에서 사용자가 입력할 수 있는 단축 명령:

- **`smoke`** — v0.7.12 인게임 smoke 진행 (위 §1 매트릭스 따라 검증 + 결과 dump)
- **`v0.7.13 NPC dropdown 진입`** — G6 게이트 채택 + brainstorm cycle 시작
- **`Cat 3 deep stack`** — Cat 3 후속 brainstorm 시작
- **`자질 grade marker spike`** — derivation rule 조사 진입
- **`v0.8 sprite spike`** — IL2CPP sprite asset 가능성 조사
- **`maintenance 진입`** — 게임/통팩 release trigger 시 활성
- **`G6 게이트 brainstorm`** — 후보 우선순위 종합 평가 (β 분할 또는 다른 결정)
- **`다른 작업`** — 사용자 자유 지시

---

## 4. 알려진 한계 / 주의사항

### 4.1 IL2CPP IMGUI strip-safe pattern (memory 참조)

신규 IMGUI API 도입 시 `docs/superpowers/dumps/2026-05-03-v0.7.3-smoke-results.md` 의 strip-safe set 안에서만. v0.7.6+ KeyDown/Event 자산 + v0.7.11 MouseDrag/MouseUp 자산 검증됨.

### 4.2 IL2CPP 호환 reflection 패턴

- `Il2CppSystem.Collections.Generic.List<T>` 는 .NET IEnumerable 미구현 → `Count` property + `get_Item(int)` indexer reflection
- `BindingFlags.FlattenHierarchy` for static singleton accessors
- HarmonyLib `AccessTools.TypeByName` for game types (compile-time 미참조)

### 4.3 v0.7.12 Undo trade-off

- Move undo 의 game 측 add 가 `maxWeight=9999` (cap 검증 skip)
- Copy undo 가 "마지막 N 제거" (사용자가 다른 op 후 Undo 시 mismatch 가능)
- Container file 자체 삭제 (5A confirm 후) Undo 미지원

### 4.4 다른 mod 와의 충돌

- **LongYinCheat** 와 `RefreshMaxAttriAndSkill` patch 충돌 → 우리 patch priority = Priority.Last (Postfix) / Priority.First (Prefix). v0.7.10 Phase 3 fix 검증됨.

---

## 5. 자산 인덱스 (다음 세션 빠른 참조)

### 핵심 source 파일
- `src/LongYinRoster/Containers/ContainerOpUndo.cs` (v0.7.12) — Undo stack
- `src/LongYinRoster/Containers/ContainerOps.cs` — JSON manipulation + IL2CPP game list helpers
- `src/LongYinRoster/Containers/ContainerOpsHelper.cs` — high-level op result wrapper
- `src/LongYinRoster/UI/ContainerPanel.cs` (~900+ LOC) — ContainerPanel main UI
- `src/LongYinRoster/UI/ModWindow.cs` — Plugin entry + callbacks + Do* methods
- `src/LongYinRoster/UI/PlayerEditorPanel.cs` — Player editor (v0.7.13 NPC dropdown 진입점)
- `src/LongYinRoster/Core/HeroLocator.cs` — singleton lookup
- `src/LongYinRoster/Core/HeroDataCapBypassPatch.cs` — Phase 3 cap-bypass (v0.7.13 NPC 시 per-hero generalize)

### 메타 문서
- `docs/HANDOFF.md` — 전체 history (긴 누적 문서)
- `docs/NEXT-SESSION-HANDOFF.md` — **본 문서** (다음 세션 focused)
- `docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md` — 메타 로드맵, §G6 Gate Pending 마지막 섹션
- `docs/superpowers/specs/2026-05-10-longyin-roster-mod-v0.7.12-design.md` — v0.7.12 design
- `docs/superpowers/specs/2026-05-09-longyin-roster-mod-v0.7.10-design.md` — v0.7.10 design (NPC generalize 시 참조)

### Cheat reference (외부 mod 분석)
- `C:/Users/deepe/AppData/Local/Temp/longyincheat_decomp/` — LongYinCheat v1.4.7 디컴파일
  - `LongYinCheat.Features/CharacterFeature.cs` (스탯 / 천부 / 무공)
  - `LongYinCheat.Patches/MultiplierPatch.cs` (cap-bypass 패턴 mirror)
  - `LongYinCheat.Patches/GameplayPatch.cs` (GetMaxTagNum 패턴 mirror)
  - `LongYinCheat/IconHelper.cs` (v0.8 sprite reference, 316 LOC)

### 메모리 (claude `~/.claude/memory/` — 5일+ 된 stale 일 수 있음)
- `LongYin item stacking convention` — 1 item = 1 stack
- `LongYin IL2CPP IMGUI strip-safe patterns` — strip-safe API list

---

## 6. 다음 세션 첫 작업 추천

1. **본 문서 + `docs/HANDOFF.md` 의 line 1-70** 읽기 (1-2 min)
2. **smoke 명령 받으면 §1 A 순서대로 진행**
3. **새 sub-project 진입 시 §2 의 우선순위 + §3 시작 명령 활용**
4. **brainstorming skill 호출 후 5-10 Q cycle → spec → plan → impl** (project workflow 표준)
5. **Auto Mode 활성 상태에서 진입 시 사용자 confirmation 가능한 한 줄여서 진행**

---

**Last commit**: `3cee426` (v0.7.12 release)
**Last tag**: `v0.7.12`
**Working tree**: clean
**다음 세션 첫 메시지 기대**: smoke 결과 또는 `v0.7.13 NPC dropdown 진입` 같은 시작 명령
