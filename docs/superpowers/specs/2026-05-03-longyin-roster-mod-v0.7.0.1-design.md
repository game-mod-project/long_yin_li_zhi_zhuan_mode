# v0.7.0.1 — Hotfix (천부 휴식 회귀 + ContainerPanel 높이 + ContainerOps NRE)

**작성일**: 2026-05-03
**Sub-project 위치**: v0.7.0 baseline 후속의 첫 patch release
**Baseline**: v0.7.0 (`f5c8d9a` / `e57aa0a` merge)
**브레인스토밍 결과**: A+C 결합 — 이번 spec 산출물은 hotfix scope 한정, root cause 진단은 plan 단계의 systematic-debugging 으로 위임

---

## 1. 목적

v0.7.0 출시 후 발견된 3개 회귀/결함을 작은 patch release 로 수정한다. 사용자가 안정 버전을 빠르게 사용할 수 있도록 fix 작업을 새 sub-project (v0.7.1 이후) 와 분리한다.

## 2. 후속 sub-project 와 관계

이번 hotfix 는 **v0.7.0.1** 한정. v0.7.0.1 release 후 사용자가 새로 정한 우선순위에 따라 다음 cycle 을 시작한다:

| Version | 카테고리 (사용자 새 우선순위) | 의존성 |
|---|---|---|
| **v0.7.0.1** | **본 hotfix (3 버그)** | v0.7.0 baseline |
| v0.7.1 | 컨테이너 UX 개선 (Item 상세 / 아이콘 그리드 / 검색·정렬 / 가상화) | v0.7.0.1 안정 |
| v0.7.2 | 설정 panel (hotkey / 정원 / 창 크기) | 독립 |
| v0.7.3 | Apply 부분 미리보기 (선택 카테고리 전후 비교) | 독립 |
| v0.7.4 | Slot diff preview (Apply 전 변경될 필드 시각화) | 독립 |
| v0.7.5 | NPC 지원 (heroID≠0 캐릭터 capture/apply) | 메뉴 / Apply path 의존 |

각 sub-project 는 별도 brainstorming → spec → plan → impl cycle.

## 3. 사용자 보고 증상 + 확정된 scope

브레인스토밍 Q&A 결과 (Q1~Q4):

### 3.1 버그 #1 — 천부 회귀

**시나리오**: 슬롯 1의 천부 카테고리 Apply → 인게임 진입 후 천부 17/17 정상 → 게임 내 "휴식" 커맨드 1회 실행 → 캐릭터 정보 재확인 시 **천부 전부 빈 상태로 reset**.

**확정된 영향 범위**: 천부 카테고리 단독 (다른 카테고리 — 스탯 / 외형 / 인벤 / 창고 / 장비 / 무공 list / 무공 active — 는 휴식 후에도 정상 유지). 회귀 영역이 좁음.

### 3.2 버그 #2 — ContainerPanel 높이 부족

**시나리오**: F11+2 → ContainerPanel (800x600) 표시. 좌측 column 은 인벤토리(상) / 창고(하) vertical split 인데, **창고 측 [→이동] [→복사] 버튼이 창 하단으로 잘려 화면에 보이지 않음**. 결과적으로 창고 → 컨테이너 측 동작은 사용 자체가 불가능.

### 3.3 버그 #3 — ContainerOps 동작 안 함

**시나리오**:
1. ContainerPanel 진입 (컨테이너 빈 상태 — 신규 컨테이너 한 번도 생성 안 함)
2. 인벤토리 측 아이템 1+ 체크박스 선택
3. [→이동] 또는 [→복사] 클릭
4. → **ContainerPanel UI 전체가 1 프레임 사라졌다가 다시 표시**
5. → 게임 인벤토리에 변화 없음, 컨테이너 file 도 디스크에 생성 안 됨

**현재 검증 범위**: 인벤토리 → 컨테이너 측만 검증됨 (a). 창고 측 (b) 은 #2 때문에 버튼이 안 보여 미확인. 컨테이너 → 인벤토리 측 (c), 컨테이너 항목 삭제 (d), 컨테이너 관리 [신규]/[이름변경]/[삭제] (e) 도 a~d 가 안 되므로 미확인.

**핵심 진단 단서**: "**UI 1프레임 사라짐 → 다시 표시**" 는 IMGUI OnGUI 콜백 안에서 unhandled exception 던질 때 정확히 나타나는 패턴. IMGUI가 그 frame 을 throw away + 다음 frame 에서 재시작 = 1 프레임 깜빡임. 컨테이너가 비어있다는 정보 합치면 **null target ContainerFile 에서 NRE** 가 1순위 가설.

## 4. Root cause 가설 + Fix path

### 4.1 #1 천부 회귀

**가설** (가능성 순):
- **H1**: "휴식" routine = day-pass + game-internal `RefreshSelfState` 또는 `RebuildHeroTagData` 류 method 재호출 → 우리 mod 의 heroTagData inject 가 game-self method 경로로 reset
- **H2**: heroTagData inject 가 in-memory data layer 만 변경했고 game-internal "true source" (예: 별도 cache / readOnly mirror) 가 별도 location 에 있어서 day-pass 시 그 source 로부터 rebuild
- **H3**: heroTagData JSON schema 가 일부 필드 (예: 만료 / cooldown / dirty flag) 누락 → 휴식 시 game 이 invalid 로 판단 후 reset

**Fix path 후보** (plan 단계에서 systematic-debugging 으로 결정):
- **F1**: 휴식 hook 발견 — Harmony Postfix on day-pass method (LongYin InGame Cheat 패턴 참고) → 천부 재apply
- **F2**: heroTagData JSON schema 보강 — 빠진 필드 발견 후 추가
- **F3**: heroTagData 외 mirror field (`heroTagPoint` 등) 동시 inject

### 4.2 #2 ContainerPanel 높이 부족

**Fix path** (선택: F1 + F2 결합 — 방어적):
- **F1**: window height 600 → 760 으로 확대 (1080p 모니터 fit)
- **F2**: 좌측 column (인벤토리/창고 split) 을 `GUILayout.BeginScrollView` 로 wrap — 작은 해상도 fallback 안전장치

대안 F3 (인벤/창고 tab 화) 는 사용성 변경이므로 hotfix scope 초과, defer.

### 4.3 #3 ContainerOps NRE

**가설** (가능성 순):
- **H1**: 선택된 컨테이너 null 상태 (신규 생성 한 번도 안 함) 에서 `ContainerOps.MoveToContainer` 호출 → ContainerFile null reference → NRE → IMGUI frame 폐기
- **H2**: ContainerRepository 가 `Containers/` 폴더 auto-create 안 해서 첫 file 작성 시 `DirectoryNotFoundException`
- **H3**: ContainerOps 안의 IL2CPP method 호출 (게임 인벤토리 list 직접 조작) 에서 IL2CPP wrapper ctor 한계 hit → silent NRE
- **H4**: `ItemListApplier.ApplyJsonToObject` 재사용 path 가 player JSON 의 player-level inject 가 없는 단순 ItemData 배열 schema 에서 mismatch

**Fix path** (선택: F1 + F2 + F3 모두 적용 — 방어적 + 사용자 안내):
- **F1**: 모든 ContainerOps 진입점 (Move/Copy/Delete) 에 try/catch + 토스트 + BepInEx 로그 → IMGUI frame 폐기 회피, 사용자 진단 가능, 부분 실패 graceful
- **F2**: 선택 컨테이너 null (= 컨테이너 한 번도 생성 안 함) 상태에서 [→이동]/[→복사] 클릭 시 → "컨테이너를 먼저 [신규] 버튼으로 생성하세요" 안내 토스트 (no-op)
- **F3**: ContainerRepository ctor / Plugin.cs Initialize 단계에서 `Containers/` 폴더 자동 생성

H4 (IL2CPP wrapper 한계) 는 root cause 진단 후 plan 단계에서 결정. 이 가설이 맞으면 hotfix scope 초과 → §6 위험 항목.

## 5. 검증 / 성공 기준

각 버그 별 smoke test 가 모두 PASS 해야 release.

### 5.1 #1 smoke
1. 슬롯 1 capture (자기 캐릭터)
2. 같은 슬롯 1 의 천부 카테고리 단독 체크 → 슬롯 2 에 Apply (또는 self overwrite)
3. 인게임 진입 후 캐릭터 정보 → 천부 17/17 표시 ✅
4. 게임 내 "휴식" 커맨드 1회 실행
5. 캐릭터 정보 재확인 → **천부 17/17 유지** ✅
6. 휴식 5회 연속 실행 → 천부 17/17 유지 ✅
7. save → 게임 종료 → 재시작 → 캐릭터 정보 → 천부 17/17 유지 ✅
8. 다른 카테고리 (스탯 / 외형 / 인벤 / 창고 / 장비 / 무공) 회귀 없음 ✅

### 5.2 #2 smoke
1. F11+2 → ContainerPanel 표시
2. 1080p 모니터에서 ContainerPanel 의 모든 버튼이 화면 안에 표시 (잘림 없음) ✅:
   - 인벤토리 측 [→이동] [→복사]
   - 창고 측 [→이동] [→복사]
   - 컨테이너 측 [←이동] [←복사] [☓삭제]
   - 컨테이너 관리 [신규] [이름변경] [삭제]
   - 카테고리 탭 (전체/장비/단약/음식/비급/보물/재료/말)
3. 좌측 column ScrollView 정상 (마우스 휠 스크롤 가능) ✅
4. 800x600 저해상도 fallback 도 ScrollView 덕분에 모든 버튼 접근 가능 ✅

### 5.3 #3 smoke
1. ContainerPanel 진입 (컨테이너 빈 상태) → 인벤토리 측 1 item 체크 → [→이동] 클릭 → **안내 토스트 ("컨테이너를 먼저 신규 생성하세요") + IMGUI 정상 (1프레임 깜빡임 없음)** ✅
2. [신규] → "용병 장비" 입력 → `BepInEx/plugins/LongYinRoster/Containers/container_01.json` 디스크 생성 ✅
3. `Containers/` 폴더가 사전에 없어도 자동 생성 ✅
4. 인벤토리 측 2 item 체크 → [→이동] → 토스트 + 게임 인벤토리에서 제거 + container_01.json 갱신 ✅
5. 창고 측 1 item 체크 → [→이동] → 토스트 + 게임 창고에서 제거 + container_01.json 갱신 ✅
6. 컨테이너 측 1 item 체크 → [←이동] → 토스트 + 컨테이너에서 제거 + 게임 인벤토리 추가 ✅
7. 컨테이너 측 1 item 체크 → [←복사] → 컨테이너 유지 + 게임 인벤토리 추가 ✅
8. 컨테이너 측 1 item 체크 → [☓삭제] → confirm dialog → 삭제 + 디스크 갱신 ✅
9. [이름변경] / [삭제] (드롭다운 옆) 정상 동작 ✅
10. ContainerOps 안에서 의도적 예외 발생 시키면 (mock test) 토스트 + 로그 + IMGUI 정상 (frame 폐기 없음) ✅

## 6. Out of scope (v0.7.x+ defer)

다음 항목들은 v0.7.0.1 hotfix scope 외:
- Item 상세 정보 panel / 아이콘 그리드 / 검색·정렬 / 가상화 → **v0.7.1**
- 설정 panel (hotkey 변경 / 컨테이너 정원 / 창 크기 조정) → **v0.7.2**
- Apply 부분 미리보기 → **v0.7.3**
- Slot diff preview → **v0.7.4**
- NPC 지원 → **v0.7.5**
- 컨테이너 file schemaVersion migration
- IL2CPP wrapper 의 sub-data graph 관련 더 깊은 spike (만약 #3 의 H4 가 root cause 면 이건 v0.7.0.1 scope 초과)

## 7. Release 정책

- **버전**: v0.7.0.1 (semver patch — backwards-compatible bug fix only)
- **VERSION bump**: 0.7.0 → 0.7.0.1
- **CHANGELOG entry**: 3 버그 fix 항목 (사용자 시나리오 기반)
- **Release artifact**: `dist/LongYinRoster_v0.7.0.1.zip` + GitHub release tag
- **HANDOFF.md 갱신**: §1 헤더의 "v0.7.0 release" → "v0.7.0.1 hotfix release", 후속 sub-project numbering 사용자 새 우선순위로 갱신, §6 후속 작업 섹션 갱신

### 7.1 Defer 정책

- #1 root cause 못 찾으면 → #1 만 v0.7.0.2 또는 후속으로 defer 가능. #2 와 #3 은 거의 확실히 fix 가능하므로 release block 사유 아님
- #1 이 의도된 game design (휴식 = day-end 천부 reset 설계) 으로 판명되면 → 영구 known limitation 으로 HANDOFF / README 에 명시
- #3 의 H4 (IL2CPP wrapper 한계) 가 root cause 라면 → hotfix scope 초과, fix path 재논의 (별도 spec)

## 8. 위험 / 미지수

### 8.1 #1 정확한 method 발견 시간

휴식 routine 의 정확한 method name 발견에 시간 걸릴 수 있다. plan 단계 첫 task 는 **Harmony trace round** (LongYin InGame Cheat 의 patch list 패턴 + Assembly-CSharp.dll 의 day-pass 관련 method dump) 가 될 것. 발견 못 하면 F2 (schema 보강) 또는 F3 (mirror field) 으로 시도.

### 8.2 #3 H1 vs H4 의 분기

- H1 (null target NRE) 이 맞으면 fix 매우 좁고 빠름 → 본 hotfix scope 안에 충분히 fit
- H4 (IL2CPP wrapper 한계) 가 맞으면 v0.6.x 통합 작업급 spike 필요 → hotfix scope 초과, scope 재논의 필요

진단 순서: F1 (try/catch) 적용 후 BepInEx 로그에 stack trace 가 잡힘 → root cause 즉시 식별 가능.

### 8.3 회귀 위험

- ContainerPanel window height 변경이 v0.7.0 의 다른 UI (CharacterPanel 모드 분기 시 같은 ModWindow 인스턴스 재사용 여부) 에 영향 줄 수 있음. plan 에서 분기 확인.
- ContainerOps try/catch 추가가 기존에 silently 동작하던 path (테스트 환경) 의 timing 에 영향 주는지 unit test 로 확인.

## 9. 기존 코드 활용

- **`HeroTagDataApplier`** (또는 `RebuildHeroTagData` 가 살아있는 곳) — #1 의 inject point. plan 단계에서 정확한 위치 식별
- **`ContainerOps`** — #3 의 try/catch 추가 대상
- **`ContainerRepository`** — #3 의 폴더 auto-create 추가 대상
- **`ContainerPanel`** — #2 의 layout 수정 대상
- **`ToastService`** — #3 의 안내 토스트 활용
- **`Logger`** — #3 의 진단 로그
- **systematic-debugging skill** — 각 버그 별 root cause 가설 검증 시 활용

## 10. 검증 환경

- 게임 빌드: `v1.0.0f8.2` (현재 baseline)
- 빌드: `DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release`
- 테스트: `DOTNET_CLI_UI_LANGUAGE=en dotnet test` (현재 baseline + Container 가드 신규 테스트 추가)
- 로그 추적: `BepInEx/LogOutput.log` — `LongYinRoster|Container|HeroTag` 키워드 grep
- 인게임 검증 cycle 필수 — unit test 만으로는 IL2CPP / IMGUI / 휴식 routine 검증 불가

## 11. 다음 단계

1. ✅ 본 spec OK 시 디스크 저장 + git commit
2. spec self-review (placeholders / contradictions / scope / ambiguity)
3. user review gate
4. **writing-plans skill** 로 implementation plan 작성:
   - Phase 1: #2 UI fix (가장 단순)
   - Phase 2: #3 try/catch 가드 + 폴더 auto-create + null target 안내 (root cause 즉시 식별 가능)
   - Phase 3: #3 root cause 별 추가 fix (H1 이면 추가 작업 없음, H4 면 별도 spec 으로 escalate)
   - Phase 4: #1 Harmony trace round → 휴식 hook 발견 → fix
   - Phase 5: smoke test (5.1 / 5.2 / 5.3) 모두 PASS 확인
   - Phase 6: release packaging (VERSION bump / CHANGELOG / zip / tag / GitHub release)
   - Phase 7: HANDOFF.md 갱신 (v0.7.0.1 release + 새 sub-project numbering)
