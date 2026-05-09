# LongYinRoster v0.7.8 Implementation Plan — Player editor

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 신규 PlayerEditorPanel (F11+4) — 5 섹션 (Resource stats / HeroSpeAddData × 3 / 천부 / 무공 list / Quick actions). v0.7.7 자산 (HeroSpeAddDataReflector + SpeAddTypeNames + SelectorDialog + ItemEditApplier.PostMutationRefresh) 90%+ 재사용.

**Architecture:** Phase 분리 — 각 phase 별 commit + smoke. 천부/무공 list 는 별도 spike 우선.

**Tech Stack:** C# / .NET (BepInEx 6 IL2CPP), reflection setter + game-self method (ChangeXxx / RefreshMaxAttriAndSkill / GetSkill / LoseAllSkill), xUnit + Shouldly POCO mock.

**Spec:** [docs/superpowers/specs/2026-05-09-longyin-roster-mod-v0.7.8-design.md](../specs/2026-05-09-longyin-roster-mod-v0.7.8-design.md)
**Roadmap:** [docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md](../specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §G2 Decision (2026-05-09, GO)

---

## Task 0: Spikes (3종) — 사용자 인게임 검증 1회로 통합

**Goal:** Phase 1/4/5 진입 전 3 spike 통합 — 부상 stat 필드명 + heroTagData schema + kungfuSkills single remove API + lv setter.

**Files:**
- Edit (temp): `src/LongYinRoster/UI/ModWindow.cs` — F8 핸들러에 v0.7.8 spike 코드 (release 전 제거)

### Step 0.1: 부상 stat 필드 dump

- [ ] HeroData 의 `*Injury*` / `*injury*` 패턴 field/property 검색
  ```csharp
  foreach (var f in player.GetType().GetFields(F))
      if (f.Name.ToLower().Contains("injury")) Logger.Info($"injury field: {f.Name} ({f.FieldType.Name}) = {f.GetValue(player)}");
  foreach (var p in player.GetType().GetProperties(F))
      if (p.Name.ToLower().Contains("injury")) Logger.Info($"injury prop: {p.Name} ({p.PropertyType.Name}) = {p.GetValue(player)}");
  ```
- [ ] CureInjuries / Heal* method 검색
  ```csharp
  foreach (var m in player.GetType().GetMethods(F))
      if (m.Name.StartsWith("Cure") || m.Name.StartsWith("Heal")) Logger.Info($"heal method: {m.Name}({sig}) → {m.ReturnType.Name}");
  ```

### Step 0.2: heroTagData schema dump

- [ ] HeroData.heroTagData type / Count / 첫 entry 의 fields 모두 dump
  ```csharp
  var tagListProp = player.GetType().GetProperty("heroTagData", F);
  var tagList = tagListProp.GetValue(player);
  Logger.Info($"heroTagData type = {tagList.GetType().FullName}");
  // Count + 첫 entry get_Item(0) → DumpFieldsAndProperties
  ```
- [ ] Tag add/remove method 검색 — `AddTag*` / `RemoveTag*` / `LoseTag*` 패턴
  ```csharp
  foreach (var m in player.GetType().GetMethods(F))
      if (m.Name.Contains("Tag")) Logger.Info($"Tag method: {m.Name}({sig}) → {m.ReturnType.Name}");
  ```
- [ ] HeroTagData 자체의 sub-data graph (HeroSpeAddData? value? duration?) dump

### Step 0.3: kungfuSkills single skill remove + lv setter dump

- [ ] HeroData 의 LoseSkill / RemoveSkill / ForgetSkill 패턴 method 검색
- [ ] KungfuSkillLvData 의 setter 검증 — level / fightExp / bookExp 가 Property setter 작동? (v0.7.7 패턴)
- [ ] GameDataController.kungfuSkillDataBase iterate — Count + sample skill 의 (skillID, name, belongForceID, rareLv) 정보

### Step 0.4: spike dump 작성

- [ ] Create: `docs/superpowers/dumps/2026-05-09-v0.7.8-spike.md`
- [ ] 3 spike 결과 + Phase 1/4/5 의 fallback 결정 명시
- [ ] commit message: `spike: v0.7.8 Player editor 3종 검증 (부상/heroTagData/kungfu remove)`

**Decision Gate:** Step 0.4 결과로 Phase 1/4/5 의 fallback 활성 결정. Phase 2 (HeroSpeAddData × 3) 는 spike 불필요 — v0.7.7 자산 그대로.

---

## Task 1: Phase 1 — Resource stats + Quick actions

**Files:**
- Create: `src/LongYinRoster/Core/PlayerEditApplier.cs`
- Create: `src/LongYinRoster.Tests/PlayerEditApplierTests.cs`

### Subtask 1.1: PlayerEditApplierTests (TDD red — POCO mock)

- [ ] `FakePlayer` POCO mock — hp/maxhp/power/maxpower/mana/maxmana/fame fields + ChangeHp(delta) method
- [ ] Test: ApplyResource_HpDirectSet → game-self method 우선
- [ ] Test: ApplyResource_NoMethod_FallbackReflection
- [ ] Test: ApplyResource_DeltaCalculation
- [ ] Test: QuickFullHeal → hp = maxhp
- [ ] Test: QuickRestoreEnergy → mana=maxmana + power=maxpower
- [ ] Test: QuickCureInjuries → injury fields = 0 (Task 0.1 결과로 정확한 field name 결정)

### Subtask 1.2: PlayerEditApplier 구현

- [ ] **Step 1.2.1: 8-step pipeline (v0.7.7 ItemEditApplier mirror)**
  - delta = newValue - oldValue
  - try ChangeXxx(delta, false, ...) game-self method
  - read-back 검증
  - silent fail → set_<field>(newValue) reflection
  - RefreshMaxAttriAndSkill

- [ ] **Step 1.2.2: Quick actions**
  ```csharp
  public static bool QuickFullHeal(object player) {
      var maxhp = ReadFloat(player, "maxhp");
      return ApplyResource(player, "hp", maxhp).Success;
  }
  public static bool QuickRestoreEnergy(object player) { /* mana + power */ }
  public static bool QuickCureInjuries(object player) {
      // Task 0.1 결과로 정확한 field name. injury 3종 = 0 + RefreshMaxAttriAndSkill
  }
  ```

- [ ] csproj `<Compile Include>` 추가
- [ ] dotnet test → green
- [ ] commit: `feat(core): v0.7.8 Phase 1 — PlayerEditApplier (Resource stats + Quick actions) + 20+ tests`

---

## Task 2: Phase 2 — HeroSpeAddData × 3 (v0.7.7 mirror)

**Files:**
- (편집기는 PlayerEditorPanel 이 직접 호출 — separate file 불필요)

### Subtask 2.1: 3 wrapper editor 통합

- [ ] PlayerEditorPanel 안 (Task 5) section 3개:
  - "▼ 기본 보정 (baseAddData)" — `HeroSpeAddDataReflector` + `SelectorDialog` + `SpeAddTypeNames` (v0.7.7 100% 재사용)
  - "▼ 임시 buff (heroBuff)" — 동상 + 변경 후 `set_heroBuffDirty(true)` flag set
  - "▼ 합산 (totalAddData) ⚠" — 동상, 단 ⚠ tooltip "RefreshMaxAttriAndSkill 호출 시 derived 재계산"

- [ ] heroBuff edit 후 `heroBuffDirty` set:
  ```csharp
  // After HeroSpeAddDataReflector.TrySet
  TryWriteProperty(player, "heroBuffDirty", true);
  ```

- [ ] totalAddData edit 시 RefreshMaxAttriAndSkill skip flag — 사용자 명시적 trigger 시만:
  ```csharp
  // 별도 ApplyMutation 호출 — refresh 호출 안 함
  ```

- [ ] dotnet test → 신규 case 없음 (v0.7.7 reflector 그대로 사용)
- [ ] commit: `feat(ui): v0.7.8 Phase 2 — HeroSpeAddData × 3 wrapper editor (baseAddData/heroBuff/totalAddData)`

---

## Task 3: Phase 4 — 천부 list editor

**Files:**
- Create: `src/LongYinRoster/Core/HeroTagDataReflector.cs`
- Create: `src/LongYinRoster.Tests/HeroTagDataReflectorTests.cs`
- Create: `src/LongYinRoster/Core/HeroTagNameCache.cs` (lazy init)

### Subtask 3.1: spike 0.2 결과 반영

Task 0.2 결과로 결정:
- [ ] heroTagData 의 List<HeroTagData> wrapper API
- [ ] HeroTagData entry 의 fields (tagID / value / duration / ...)
- [ ] add/remove method 시그니처
- [ ] 한글 라벨 dictionary (cheat dump 또는 game internal)

### Subtask 3.2: HeroTagDataReflector + tests

- [ ] POCO mock 으로 list manipulation 검증 (~8 case)
- [ ] csproj include
- [ ] commit: `feat(core): v0.7.8 Phase 4 — HeroTagDataReflector + tests`

### Subtask 3.3: PlayerEditorPanel 안 천부 섹션 통합

- [ ] DrawHeroTagSection (selector + value field + add/edit/delete row)
- [ ] commit: `feat(ui): v0.7.8 Phase 4 — 천부 list editor section`

---

## Task 4: Phase 5 — 무공 list editor

**Files:**
- Create: `src/LongYinRoster/Core/KungfuSkillEditor.cs`
- Create: `src/LongYinRoster/Core/SkillNameCache.cs`
- Create: `src/LongYinRoster.Tests/KungfuSkillEditorTests.cs`

### Subtask 4.1: spike 0.3 결과 반영

Task 0.3 결과로 결정:
- [ ] single skill remove API (LoseSkill / RemoveSkill / list manipulation)
- [ ] KungfuSkillLvData lv/fightExp/bookExp setter 검증
- [ ] kungfuSkillDataBase iterate 패턴

### Subtask 4.2: SkillNameCache (lazy)

- [ ] `kungfuSkillDataBase` iterate → `Dictionary<int, string>` (skillID → 한글 nameKr)
- [ ] HangulDict.Translate(skill.name) 으로 한글화 (v0.7.5 자산)
- [ ] SelectorDialog 데이터 source 로 사용

### Subtask 4.3: KungfuSkillEditor + tests

- [ ] POCO mock 으로 add/remove + lv 편집 검증 (~10 case)
- [ ] v0.5.2 KungfuListApplier 자산 reuse — `KungfuSkillLvData(skillID)` ctor + `GetSkill(wrapper)` add 패턴 mirror
- [ ] csproj include
- [ ] commit: `feat(core): v0.7.8 Phase 5 — KungfuSkillEditor + SkillNameCache + tests`

### Subtask 4.4: PlayerEditorPanel 안 무공 섹션 통합

- [ ] DrawKungfuSection (selector with 검색 + lv/fightExp/bookExp fields + add/edit/delete row)
- [ ] commit: `feat(ui): v0.7.8 Phase 5 — 무공 list editor section`

---

## Task 5: PlayerEditorPanel skeleton + 통합

**Files:**
- Create: `src/LongYinRoster/UI/PlayerEditorPanel.cs`
- Edit: `src/LongYinRoster/Util/KoreanStrings.cs`

### Subtask 5.1: PlayerEditorPanel 작성

- [ ] **Skeleton** — 480×720 GUI.Window, IMGUI strip-safe pattern (v0.7.7 mirror)
- [ ] **5 섹션** (접이식 toggle)
  - Resource stats (Phase 1)
  - HeroSpeAddData × 3 (Phase 2)
  - 천부 (Phase 4)
  - 무공 (Phase 5)
  - Quick actions button row (Phase 1 안 통합)
- [ ] **Selector** — 단일 SelectorDialog 인스턴스 공유 (각 섹션이 사용)
- [ ] **GetPlayer Func** — ModWindow 가 wire (HeroLocator decouple)
- [ ] commit: `feat(ui): v0.7.8 Task 5 — PlayerEditorPanel skeleton + 5 섹션`

---

## Task 6: ModWindow + ModeSelector + HotkeyMap + SettingsPanel 통합

**Files:**
- Edit: `src/LongYinRoster/Config.cs` (+ 6 ConfigEntry)
- Edit: `src/LongYinRoster/Util/HotkeyMap.cs` (+ PlayerEditorMode)
- Edit: `src/LongYinRoster/UI/ModeSelector.cs` (+ Player mode + 버튼)
- Edit: `src/LongYinRoster/UI/ModWindow.cs` (+ _playerEditorPanel + transition)
- Edit: `src/LongYinRoster/UI/SettingsPanel.cs` (+ Hotkey row + rect 영속화)

### Subtask 6.1: Config + HotkeyMap

- [ ] Config 에 6 신규 ConfigEntry:
  - HotkeyPlayerEditorMode (Alpha4)
  - PlayerEditorPanelX/Y/W/H (200/120/480/720)
  - PlayerEditorPanelOpen (false)
- [ ] HotkeyMap.PlayerEditorModeKey + Numpad + PlayerShortcut() + MainKeyPressedAlone 갱신

### Subtask 6.2: ModeSelector

- [ ] Mode.Player 추가
- [ ] "플레이어 편집 (F11+4)" 버튼 + 높이 240 → 280

### Subtask 6.3: ModWindow

- [ ] _playerEditorPanel 필드 + Awake wire-up (GetPlayer + RefreshAllContainerRows)
- [ ] Update() — F11+4 단축키
- [ ] transition handler — Mode.Player case
- [ ] X 닫기 sync
- [ ] OnGUI() — _playerEditorPanel.OnGUI()
- [ ] OnGUI() — PlayerEditorPanel rect 영속화
- [ ] ShouldBlockMouse — PlayerEditorPanel + Selector 영역

### Subtask 6.4: SettingsPanel

- [ ] Hotkey row "플레이어 편집:" 추가 (4 → 5 hotkey)
- [ ] 충돌 검증 5-key 매트릭스 (RecomputeConflict 갱신)
- [ ] PlayerEditorPanel rect 도 read-only 표시 + reset 가능

- [ ] commit: `feat(ui): v0.7.8 Task 6 — Mode.Player 통합 + Hotkey + SettingsPanel 확장`

---

## Task 7: 인게임 Smoke (~50 시나리오)

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-v0.7.8-smoke-results.md`

### Subtask 7.1: Phase 1 smoke

- [ ] 생명/체력/내력/명예 직접 변경 → toast + UI 갱신
- [ ] maxhp/maxmana/maxpower 변경 → realMaxHp sync (v0.7.7 검증)
- [ ] [전체 회복] / [내력 채움] / [부상 치료] 동작
- [ ] save → reload → 영속

### Subtask 7.2: Phase 2 smoke

- [ ] baseAddData entry add/edit/delete (v0.7.7 mirror)
- [ ] heroBuff entry — heroBuffDirty=true 검증 (UI 즉시 갱신)
- [ ] totalAddData edit ⚠ 경고 + RefreshMaxAttriAndSkill skip 검증

### Subtask 7.3: Phase 4 smoke

- [ ] 기존 천부 entry 표시 (한글 라벨 + 값)
- [ ] 신규 천부 추가 / 기존 제거 / 값 변경
- [ ] save → reload → 영속

### Subtask 7.4: Phase 5 smoke

- [ ] 기존 무공 entry 표시 (한글 무공명 + lv/fightExp/bookExp)
- [ ] 신규 무공 추가 (selector with 검색)
- [ ] 기존 무공 제거 / lv·exp 변경
- [ ] save → reload → 영속

### Subtask 7.5: Strip / Exception 검증

```pwsh
Select-String "...LogOutput.log" -Pattern "Method unstripping failed"
Select-String "...LogOutput.log" -Pattern "PlayerEditorPanel\..*threw"
Select-String "...LogOutput.log" -Pattern "PlayerEditApplier"
```

### Subtask 7.6: 회귀 시나리오

- [ ] v0.7.7 ItemDetailPanel 편집 mode 회귀
- [ ] v0.7.6 SettingsPanel / ContainerPanel 영속화 회귀

### Subtask 7.7: smoke dump 작성

- [ ] iteration fix narrative
- [ ] commit: `docs: v0.7.8 인게임 smoke 결과 — N/N PASS`

---

## Task 8: Release prep

**Files:**
- Edit: `src/LongYinRoster/Plugin.cs` (VERSION 0.7.7 → 0.7.8 + Logger.Info v0.7.8 line)
- Edit: `README.md`
- Edit: `docs/HANDOFF.md`
- Edit: `docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md` (Result append + G3 게이트 진입 명시)

- [ ] **Step 8.1: VERSION bump**
- [ ] **Step 8.2: README.md** — 신규 기능 한 줄
- [ ] **Step 8.3: HANDOFF.md** — release entry / baseline / G3 게이트
- [ ] **Step 8.4: 메타 spec append** — Q1~Q6 결과 + spike 결과 + iteration narrative
- [ ] **Step 8.5: dist zip + GitHub release** (사용자 권한)
- [ ] **Step 8.6: git commit + tag** (사용자 권한)

---

## 예상 commit 시퀀스

| commit | 내용 | tests |
|---|---|---|
| 1 | `spike: v0.7.8 Player editor 3종 검증` | 304 |
| 2 | `feat(core): Phase 1 — PlayerEditApplier + Quick actions + tests` | ~324 (+20) |
| 3 | `feat(core): Phase 4 — HeroTagDataReflector + tests` | ~332 (+8) |
| 4 | `feat(core): Phase 5 — KungfuSkillEditor + SkillNameCache + tests` | ~342 (+10) |
| 5 | `feat(ui): Task 5 — PlayerEditorPanel skeleton + 5 섹션` | ~342 |
| 6 | `feat(ui): Phase 2 — HeroSpeAddData × 3 wrapper editor` | ~342 |
| 7 | `feat(ui): Phase 4 — 천부 list editor section` | ~342 |
| 8 | `feat(ui): Phase 5 — 무공 list editor section` | ~342 |
| 9 | `feat(ui): Task 6 — Mode.Player 통합` | ~342 |
| 10 | `docs: v0.7.8 인게임 smoke 결과` | ~342 |
| 11 | `chore(release): v0.7.8` | ~342 |

총 11 commits + 1 tag.

## 위험 / 변동 요인

- **Task 0.2 spike 결과**: heroTagData schema 가 list 가 아니거나 add/remove 가 game-self method 없으면 Phase 4 재설계
- **Task 0.3 spike 결과**: single skill remove API 부재 시 list 직접 manipulation (cheat 의 Forget skill 패턴 차용)
- **Phase 4/5 smoke 회귀**: scope 큼 — 2~5 iteration fix 가능
- **totalAddData 부작용**: Phase 2 에서 사용자 변경이 derived 재계산으로 사라지면 ⚠ tooltip 강화 또는 read-only 변경

## 참고 자산 / dumps

- v0.7.7 spec/plan/smoke — Hybrid pipeline + HeroSpeAddData editor 패턴
- v0.5.2 KungfuListApplier — 무공 list manipulation
- v0.4 RebuildHeroTagData — 천부 schema 단서
- `dumps/2026-05-05-v075-cheat-feature-reference.md` §3 (CharacterFeature) + §4 (SkillManager) + §5 (StatEditor)
- `C:/Users/deepe/AppData/Local/Temp/longyincheat_decomp/CharacterFeature.cs` — SpeAddTypeNames + tag 한글 매핑 후보
- `C:/Users/deepe/AppData/Local/Temp/longyincheat_decomp/StatEditor.cs` — Quick actions 패턴 + Lock 시스템 (v0.7.8.1 후속)
- `C:/Users/deepe/AppData/Local/Temp/longyincheat_decomp/SkillManager.cs` — kungfuSkillDataBase iterate 패턴

## Spec 통과 검증

본 plan = spec §9 cycle 의 5 Phase 분리 충실. 모든 spec 결정 (Q1~Q6 + 자유 입력) 이 plan 단계로 매핑됨.
