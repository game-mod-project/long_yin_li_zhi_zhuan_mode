# LongYinRoster v0.7.8 — Player editor (HeroSpeAddData × 3 + Resource + 천부 + 무공 list)

**일시**: 2026-05-09
**baseline**: v0.7.7 — 304/304 tests + 사용자 5 iteration 검증 PASS
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §G2 Decision (2026-05-09, GO)

**brainstorm 결과 (2026-05-09)**:
- Q1 = **B** (HeroSpeAddData + Resource stats) + 자유 입력 천부/무공 list 추가
- Q2 = **C** (3 wrapper 모두 — baseAddData + totalAddData + heroBuff)
- Q3 = **C** (Hybrid — game-self method 우선 + reflection fallback)
- Q4 = **A** (별도 PlayerEditorPanel + F11+4 단축키)
- Q5 = **B** (Quick actions button row — FullHeal/RestoreEnergy/CureInjuries)
- Q6 = **A** (heroID=0 player 만)
- 자유 입력: **천부 (heroTagData)** + **무공 list (kungfuSkills)** 편집 추가

## 0. 한 줄 요약

신규 `PlayerEditorPanel` (F11+4) — 5 섹션:
1. **Resource stats** (hp/maxhp/power/maxpower/mana/maxmana/fame) — Hybrid pipeline (game-self ChangeXxx 우선 + reflection fallback)
2. **HeroSpeAddData × 3** (baseAddData/totalAddData/heroBuff) — v0.7.7 stat editor 매트릭스 mirror
3. **Quick actions** (전체 회복/내력 채움/부상 치료) — cheat StatEditor 패턴 차용
4. **천부 list editor** (heroTagData add/remove/edit) — 별도 spike 필요
5. **무공 list editor** (kungfuSkills add/remove + lv/fightExp/bookExp 편집) — 별도 spike 필요 + 한글 무공명 dropdown

v0.7.7 자산 (HeroSpeAddDataReflector + SpeAddTypeNames + SelectorDialog + ItemEditApplier.PostMutationRefresh) 90%+ 재사용. 천부/무공 list 는 phase 분리 plan.

## 1. 디자인 결정

### 1.1 Edit scope 매트릭스 (Q1=B + 자유 입력)

| Phase | 섹션 | 필드/Wrapper | Apply 경로 |
|---|---|---|---|
| **Phase 1** | Resource stats | `hp` / `mana` / `power` / `fame` | game-self `ChangeHp(delta)` / `ChangeFame(delta)` 우선, reflection setter fallback |
| | Resource max | `maxhp` / `maxmana` / `maxpower` | game-self `ChangeMaxHp(delta, false)` / 동상 |
| | Stat 재계산 | — | `RefreshMaxAttriAndSkill()` (v0.7.7 검증) 매 mutation 후 |
| **Phase 2** | HeroSpeAddData (3 wrapper) | `baseAddData` (영구 보정) | v0.7.7 HeroSpeAddDataReflector 재사용 |
| | | `heroBuff` (임시 buff) | v0.7.7 mirror + `heroBuffDirty=true` flag set |
| | | `totalAddData` (derived) | v0.7.7 mirror — **⚠ RefreshMaxAttriAndSkill 호출 시 재계산되어 사용자 변경 사라짐** (Risk §7.2) |
| **Phase 3** | Quick actions | `[전체 회복]` | `hp = maxhp` (또는 `ChangeHp(maxhp - hp, ...)`) |
| | | `[내력/체력 채움]` | `mana = maxmana` + `power = maxpower` |
| | | `[부상 치료]` | `externalInjury / internalInjury / poisonInjury = 0` (spike 필요) |
| **Phase 4** | 천부 list (heroTagData) | tag entry add/remove/value | spike 필요 — schema + manipulation API |
| **Phase 5** | 무공 list (kungfuSkills) | skill entry add/remove + lv/fightExp/bookExp | v0.5.2 KungfuListApplier mirror — `KungfuSkillLvData(skillID)` ctor + `GetSkill(wrapper)` add / `LoseAllSkill()` clear (single remove 미검증, spike 필요) |

각 Phase 별 commit 분리 — 각 commit 단위로 빌드/test/smoke 가능.

### 1.2 Apply pipeline (Q3=C Hybrid + Q5=B Quick actions)

```
Resource stat (e.g. hp = 1000):
  oldValue = player.hp
  delta = newValue - oldValue
  ┌─ try ChangeHp(delta, false, false, false, false)  ← game-self method
  └─ read-back → match? PASS
  └─ silent fail → fallback set_hp(value) reflection setter
  → RefreshMaxAttriAndSkill() (v0.7.7 검증)
```

HeroSpeAddData mutation 은 v0.7.7 PostMutationRefresh 패턴 그대로:
```
TrySet(addData, type, value)
→ ItemEditApplier.PostMutationRefresh(player, player) 호출 (item 자리에 player)
  ├─ TryInvokeCountValueAndWeight(player) — player 에는 method 없으나 try/catch 안전
  └─ TryInvokeRefreshSelfState (= RefreshMaxAttriAndSkill) — v0.7.7 검증
```

PostMutationRefresh 의 시그니처 약간 조정 필요 — 첫 인자 `item` 이 player 일 때 CountValueAndWeight skip. 또는 `RefreshMaxAttriAndSkill` 만 분리한 helper 추가.

### 1.3 UI placement — PlayerEditorPanel (Q4=A)

신규 IMGUI window — `PlayerEditorPanel` (480×720, ItemDetailPanel 보다 길게).

**ModeSelector.Mode** 확장:
```csharp
public enum Mode { None, Character, Container, Settings, Player }   // Player 추가
```

**HotkeyMap 확장**:
- `Hotkey.PlayerEditorMode` 신규 ConfigEntry (default `Alpha4`)
- `HotkeyMap.PlayerShortcut()` 신규
- ModeSelector 에 "플레이어 편집 (F11+4)" 버튼 추가

**Window layout**:
```
+ 플레이어 편집 ────────── [X] +
| {player.heroName} (heroID=0)  |
| {age}세 / {fightScore} 전투력 |
| ▼ 자원 / 최대값                |
|   생명: [1234] / [9999] [수정] |
|   체력: [5000] / [9999] [수정] |
|   내력: [3000] / [9999] [수정] |
|   명예: [12345]      [수정]    |
|   [전체 회복] [채움] [치료]      |
| ▼ 기본 보정 (baseAddData)      |
|   (v0.7.7 stat editor mirror)  |
| ▼ 임시 buff (heroBuff)         |
|   (동상)                      |
| ▼ 합산 (totalAddData) ⚠       |
|   (read-only 표시 + 편집 시 경고) |
| ▼ 천부 (heroTagData)           |
|   (Phase 4 — list editor)      |
| ▼ 무공 (kungfuSkills)          |
|   (Phase 5 — list editor)      |
+ ─────────────────────────────+
```

각 섹션 접이식 (▶ ↔ ▼ 토글). 사용자 frequent 섹션만 펼침.

### 1.4 Quick actions (Q5=B)

button row 1줄 (resource 섹션 안):

| 버튼 | 동작 | game-self method 또는 reflection |
|---|---|---|
| `[전체 회복]` | `hp = maxhp` | reflection setter (간단) 또는 `ChangeHp(maxhp - hp, ...)` |
| `[내력/체력 채움]` | `mana = maxmana` + `power = maxpower` | reflection setters |
| `[부상 치료]` | `externalInjury / internalInjury / poisonInjury = 0` | reflection setters (필드명은 spike 필요) |

각 버튼 클릭 시 RefreshMaxAttriAndSkill 호출 + 토스트.

### 1.5 천부 list editor (자유 입력)

**Spike 필요** (v0.7.8 Task 0.2):
- `heroTagData` field/property 의 type — `Il2CppSystem.Collections.Generic.List<HeroTagData>` 추정 (v0.4 Apply pipeline 의 RebuildHeroTagData 참고)
- `HeroTagData` 의 schema — tagID (int) + value (float) + duration (?) + 기타?
- add/remove API:
  - 추가: `player.AddTag(tagID, value, ...)` 같은 method 추정 — spike 로 method dump
  - 제거: list 직접 manipulation 또는 `player.RemoveTag(tagID)` method 추정

**UI** (LongYinCheat NpcEditPanel 의 Tag tab 패턴 mirror):
```
== 천부 (heroTagData) ==
  {tagName1} (id={tagID}): [{value}] [수정] [삭제]
  {tagName2} ...
  추가: [천부 selector ▼] [값] [추가]
```

**한글 매핑**: tag 도 SpeAddTypeNames 같은 정적 dict 가 cheat 에 있을 수 있음 (`CharacterFeature.cs` 추가 검색).

### 1.6 무공 list editor (자유 입력)

v0.5.2 KungfuListApplier 검증 자산:
- `LoseAllSkill()` — 전체 clear
- `KungfuSkillLvData(int skillID)` ctor — wrapper 생성
- `player.GetSkill(KungfuSkillLvData wrapper)` — add (2-pass retry 로 silent fail 회피)

**Single skill add/remove spike 필요** (v0.7.8 Task 0.3):
- `player.LoseSkill(skillID)` 또는 `player.LoseSkill(KungfuSkillLvData)` method 존재? (cheat SkillManager 검색)
- `KungfuSkillLvData.level / fightExp / bookExp` setter — Property setter 가능성 (v0.7.7 기준)

**Cheat SkillManager 자산**:
- `GetAllSkills()` — `GameDataController.kungfuSkillDataBase` 전체 list cache
- `TranslationHelper.Translate(skill.name)` — 한글화

**UI**:
```
== 무공 (kungfuSkills) ==
  {skillName1} (id={skillID}): lv={lv} / fightExp={fightExp} / bookExp={bookExp}
    [수정] [삭제]
  ...
  추가: [무공 selector ▼ (검색)] [추가]   (lv/exp 는 추가 후 수정 row 에서)
```

**한글 무공명 dropdown** = SelectorDialog 재사용 — 검색 box 가 핵심 (수십~수백 무공). cheat 의 `GetAllSkills()` cache 패턴 차용.

## 2. 변경 파일

### 2.1 신규 파일

#### 2.1.1 `src/LongYinRoster/Core/PlayerEditApplier.cs` (~250 LOC)
Resource stat Hybrid pipeline (Phase 1). game-self method 우선, reflection fallback, RefreshMaxAttriAndSkill 후처리.

```csharp
public static class PlayerEditApplier
{
    public static PlayerEditResult ApplyResource(object player, string fieldName, float newValue)
    {
        // 1. read oldValue (Property)
        // 2. try ChangeXxx(delta, ...) game-self method
        // 3. read-back 검증
        // 4. fallback: set_<fieldName>(newValue) reflection
        // 5. RefreshMaxAttriAndSkill
    }
    public static bool QuickFullHeal(object player) { /* hp = maxhp */ }
    public static bool QuickRestoreEnergy(object player) { /* mana/power 채움 */ }
    public static bool QuickCureInjuries(object player) { /* externalInjury 등 0 */ }
}
```

#### 2.1.2 `src/LongYinRoster/Core/HeroTagDataReflector.cs` (~150 LOC, Phase 4)
heroTagData list 의 entry read/write/add/remove. v0.7.7 HeroSpeAddDataReflector 패턴 mirror.

#### 2.1.3 `src/LongYinRoster/Core/KungfuSkillEditor.cs` (~200 LOC, Phase 5)
kungfuSkills list 의 add (single skill) / remove / lv·fightExp·bookExp 편집. v0.5.2 KungfuListApplier 자산 + 단일 skill manipulation 추가.

#### 2.1.4 `src/LongYinRoster/Core/SkillNameCache.cs` (~80 LOC, Phase 5)
`GameDataController.kungfuSkillDataBase` iterate → `(int skillID, string nameKr)` cache. lazy init. SelectorDialog 의 dropdown 데이터 source.

#### 2.1.5 `src/LongYinRoster/Core/HeroTagNameCache.cs` (~80 LOC, Phase 4)
LongYinCheat `CharacterFeature.cs` 또는 game 의 tagID → 한글 라벨 dump (추가 디컴파일 필요).

#### 2.1.6 `src/LongYinRoster/UI/PlayerEditorPanel.cs` (~400 LOC)
신규 IMGUI window. 5 섹션 (resource / 3 stat editor / 천부 / 무공). 각 섹션 접이식. Quick actions button row.

#### 2.1.7 신규 test 파일들
- `PlayerEditApplierTests.cs` — POCO mock + Hybrid pipeline 검증
- `HeroTagDataReflectorTests.cs` — POCO mock list manipulation
- `KungfuSkillEditorTests.cs` — POCO mock skill list

### 2.2 변경 파일

#### 2.2.1 `src/LongYinRoster/Config.cs`
- `HotkeyPlayerEditorMode` 신규 ConfigEntry (default Alpha4)
- `PlayerEditorPanelX/Y/W/H` 4 신규 ConfigEntry (default 200/120/480/720)
- `PlayerEditorPanelOpen` 신규 (default false)

#### 2.2.2 `src/LongYinRoster/Util/HotkeyMap.cs`
- `PlayerEditorModeKey` + Numpad 신규 정적 필드
- `Bind()` 안 sync
- `PlayerShortcut()` 신규
- `MainKeyPressedAlone` 갱신 (Player Numpad 도 검사)

#### 2.2.3 `src/LongYinRoster/UI/ModeSelector.cs`
- `Mode.Player` 추가
- "플레이어 편집 (F11+4)" 버튼 추가
- _windowRect 높이 240 → 280

#### 2.2.4 `src/LongYinRoster/UI/ModWindow.cs`
- `_playerEditorPanel` 필드
- Awake wire-up (HeroLocator.GetPlayer 콜백 + RefreshAllContainerRows 콜백 — 일부 변경 시 인벤 갱신 필요)
- Update() — F11+4 단축키
- transition handler — Settings/Player case
- ShouldBlockMouse — PlayerEditorPanel + selector 영역
- OnGUI — `_playerEditorPanel.OnGUI()`

#### 2.2.5 `src/LongYinRoster/UI/SettingsPanel.cs`
- Hotkey row "플레이어 편집:" 추가 (4 → 5 hotkey)
- 충돌 검증 5-key 매트릭스로 확장
- ContainerPanel rect 외에 PlayerEditorPanel rect 도 영속화 (ItemDetailPanel mirror)

#### 2.2.6 `src/LongYinRoster/Util/KoreanStrings.cs`
Player editor 라벨 모음.

## 3. UI Layout 상세 (PlayerEditorPanel 480×720)

[§1.3 의 mockup 참조]

각 섹션 접이식 토글 (▶ closed / ▼ open). default 펼친 상태 = resource + baseAddData. heroBuff/totalAddData/천부/무공 = closed default.

## 4. Strip-safe IMGUI 검증

**v0.7.7 까지 검증된 패턴만** 사용 — 신규 IMGUI API 없음:
- GUI.Window / GUILayout.Button / TextField / Toggle / BeginScrollView / Space
- Event.current.KeyDown (v0.7.6) — Selector 검색 box 자동 포함
- GUI.enabled (v0.7.6)

추가 spike 불필요.

## 5. Test 변경

| Test 파일 (신규) | 예상 case |
|---|---|
| `PlayerEditApplierTests` | Resource Hybrid pipeline (delta math + setter fallback) + Quick actions — 20+ |
| `HeroTagDataReflectorTests` | POCO list mock add/remove/edit — 8+ |
| `KungfuSkillEditorTests` | POCO mock skill add/remove + lv 편집 — 10+ |
| 기존 304 → **~340 (+36)** |

`SkillNameCache` / `HeroTagNameCache` 는 game runtime 의존 → 인게임 smoke 만.

## 6. 인게임 Smoke (~50 시나리오)

### 6.1 Phase 1 — Resource stats
- 생명/체력/내력/명예 직접 변경 → toast + UI 갱신 (HeroDetailPanel)
- maxhp/maxmana/maxpower 변경 → realMaxHp 도 sync
- 변경 후 save → reload → 영속

### 6.2 Phase 2 — HeroSpeAddData
- baseAddData entry add/edit/delete (v0.7.7 패턴 mirror, player level)
- heroBuff entry — heroBuffDirty=true 검증 (UI 즉시 갱신)
- totalAddData edit 시 RefreshMaxAttriAndSkill 후 사용자 변경 사라지는지 (Risk §7.2)

### 6.3 Phase 3 — Quick actions
- [전체 회복] → hp = maxhp
- [내력 채움] → mana = maxmana + power = maxpower
- [부상 치료] → 부상 stat = 0

### 6.4 Phase 4 — 천부 list editor
- 기존 천부 entry 표시 (한글 라벨 + 값)
- 신규 천부 추가 (selector + 값)
- 기존 천부 제거
- save → reload → 영속

### 6.5 Phase 5 — 무공 list editor
- 기존 무공 entry 표시 (한글 무공명 + lv/fightExp/bookExp)
- 신규 무공 추가 (selector with 검색)
- 기존 무공 제거
- lv/fightExp/bookExp 변경 후 RefreshMaxAttriAndSkill
- save → reload → 영속

### 6.6 회귀 (v0.7.7 baseline 304/304 + 사용자 검증)
- ItemDetailPanel 편집 mode + stat editor 정상 동작
- ContainerPanel / SettingsPanel 회귀 없음

## 7. Risk

### 7.1 totalAddData 직접 편집 시 derived 재계산 (HIGH)

**우려**: Q2=C 채택. totalAddData 는 base + buff + 무공 + 장비 의 합산 derived. 직접 편집 후 RefreshMaxAttriAndSkill 호출 시 다시 계산되어 사용자 변경 사라질 가능성.

**완화**: UI 에 ⚠ 경고 + tooltip "이 섹션 편집은 derived 라 다음 stat 갱신 시 사라질 수 있음". 사용자에게 명시적 informed choice. 또는 totalAddData 편집 후 RefreshMaxAttriAndSkill 호출 skip (다른 mutation 의 자동 호출과 분리 — flag 로 제어).

### 7.2 부상 stat 필드명 미spike (MEDIUM)

**우려**: `externalInjury / internalInjury / poisonInjury` 가 추정. game 의 정확한 field name 확인 필요.

**완화**: Phase 3 시작 시 임시 spike (F8 같은 trigger) — HeroData 의 *Injury* 패턴 method/field grep dump.

### 7.3 천부 list schema 미spike (HIGH)

**우려**: heroTagData 는 List<HeroTagData> 추정이지만 schema 미확인. tag 의 (id/value/duration/...) 구조 미상.

**완화**: Phase 4 시작 시 spike — heroTagData type + 첫 entry 의 fields/properties dump. v0.4 의 RebuildHeroTagData 코드 참조.

### 7.4 무공 single remove API 미검증 (MEDIUM)

**우려**: v0.5.2 의 LoseAllSkill 만 검증. single skill remove method 부재 시 list 직접 manipulation 필요.

**완화**: Phase 5 시작 시 spike — `Lose*` / `Remove*` method 패턴 dump. 부재 시 cheat SkillManager 의 패턴 차용 (Forget skill).

### 7.5 무공명 한글화 cache 의 lazy init 비용 (LOW)

**우려**: GameDataController.kungfuSkillDataBase iterate 는 무공 수십~수백 개. dropdown open 시 lazy init.

**완화**: Cache singleton + lazy. 첫 dropdown open 시만 비용. 이후 O(1).

### 7.6 PlayerEditorPanel 영속화 cfg 마이그레이션 (LOW)

신규 ConfigEntry 추가 — 기존 cfg 자동 추가. 영향 없음.

### 7.7 RefreshMaxAttriAndSkill 부작용 (MEDIUM, v0.7.7 알려진 위험)

v0.7.7 에서 이미 검증된 부작용 — equipped item edit 시 player stat 재계산. v0.7.8 의 player edit 도 같은 method 호출. 사용자 변경 의도 외 derived 변경 (예: max 값 변경 시 hp 보충) 가능. cheat StatEditor 의 LockedMax 패턴은 v0.7.8.1 후속 후보.

## 8. Out-of-scope

- **NPC 지원** (Q6=A) — v0.7.10 후속
- **Lock 시스템** (cheat-style EnforceLocks 매 frame) — v0.7.8.1 후속 후보
- **Apply pipeline 확장** — v0.7.8 의 player editor 는 in-memory mutation 만. Apply/Restore 흐름 무관 (v0.4~v0.7 자산 그대로)
- **무공 의 sub-data** (extraAddData / speEquipData / speUseData / equipUseSpeAddValue) — KungfuSkillLvData 안의 nested HeroSpeAddData. v0.7.9 후속 후보
- **천부 의 nested data** — heroTagData entry 의 sub-data graph (있다면). spike 결과로 결정
- **HeroData 의 다른 필드** — 외형 (faceData/partPosture, v0.6.4 자산), 정체성 (heroName/nickname, v0.4 자산), 보존 필드 (force/location/relations) — 현 sub-project OOS
- **Apply 미리보기 / Slot diff** — v0.7.9 후보

## 9. Cycle 계획

Plan §5.1 의 6단계 + Phase 분리:

```
1. brainstorm = 본 spec
2. plan = phase 별 task 분리
   Task 0.1 — spike: 부상 stat 필드명
   Task 0.2 — spike: heroTagData schema
   Task 0.3 — spike: kungfuSkills single skill remove + lv setter
   Task 1 — Phase 1 Resource + Quick actions
   Task 2 — Phase 2 HeroSpeAddData × 3 (v0.7.7 mirror)
   Task 3 — Phase 4 천부 list editor
   Task 4 — Phase 5 무공 list editor
   Task 5 — PlayerEditorPanel skeleton + integration
   Task 6 — ModWindow + ModeSelector + HotkeyMap + SettingsPanel 통합
   Task 7 — Smoke per phase + 종합
   Task 8 — Release prep
3. impl = phase 별 commit (~10 commits)
4. smoke = phase 별 + 종합 ~50 시나리오
5. release = v0.7.8 tag + GitHub release
6. handoff = HANDOFF.md / 메타 §추가 + G3 게이트
```

## 10. 명명 / 호환성

- 버전: `v0.7.8` (확정 sub-project, G2 GO)
- spec slug: `longyin-roster-mod-v0.7.8-design`
- plan slug: `longyin-roster-mod-v0.7.8-plan`
- smoke dump: `2026-05-XX-v0.7.8-smoke-results.md`
- 사용자 cfg 마이그레이션: 신규 ConfigEntry 자동 생성. 영향 없음.
- 슬롯 schema 영향: 없음 — Apply/Restore 흐름 무관, 별개 mutation
- v0.7.7 자산 보존 100% — ItemDetailPanel / SelectorDialog / HeroSpeAddDataReflector / SpeAddTypeNames / ItemEditApplier / ItemEditField / ItemRareLvNames 모두 그대로

## 11. 다음 단계 (G3 게이트)

v0.7.8 release 직후 G3 게이트:
- v0.8 진짜 sprite — IL2CPP sprite spike → GO/DEFER/NO-GO
- v0.7.8.1 hotfix candidates — Lock 시스템 / 무공 sub-data
- v0.7.9 Slot diff / v0.7.10 NPC 지원 — 평가
- maintenance — trigger 시 활성

본 spec 통과 → plan 작성 → impl (8 task / phase 별 commit) → smoke → release → G3.
