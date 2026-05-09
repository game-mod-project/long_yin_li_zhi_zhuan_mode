# LongYinRoster v0.7.10 — 천부 max lock + 속성·무학·기예 editor

**일시**: 2026-05-09
**baseline**: v0.7.8 — 327/327 tests + 사용자 11 iteration 검증 PASS
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §G3 Decision (2026-05-09, GO)

**brainstorm 결과 (2026-05-09)**:
- G3 게이트 = **E** (B + A 조합) — v0.7.8.1 LockedMax + v0.7.10 NPC editor 결합 → ③ 단일 cycle / 1 spec / 2-phase impl / tag = `v0.7.10`
- Q1 = **A** Lock scope = 천부 max 보유수만 (`GetMaxTagNum` Postfix)
- Q2 = **A** Lock 적용 = Player (heroID=0) only
- Q3 = **A** Lock UX = PlayerEditorPanel 천부 섹션 헤더
- Q4 = **β** 2 단계 분리 — v0.7.10 = LockedMax + 속성·무학·기예 editor / v0.7.11 = NPC dropdown (별도 cycle)
- Q5 = **E** 속성·무학·기예 = PlayerEditorPanel secondary tab `[기본 / 속성]`
- Q6 = **B** 편집 mechanism = per-row inline TextField × 2 + 일괄 button
- Q7 = **B** Apply timing = [저장] gated, sanitize 1회
- Q8 = **B** 표시 정보 = 수치/자질값 + buff/effective read-only (자질 grade marker deferred)
- Q9 = **A** Phase 1 LockedMax UX = 체크박스 + TextField + 즉시 적용 (cheat 패턴 100% mirror)

## 0. 한 줄 요약

**v0.7.10 = 천부 max lock + 속성·무학·기예 editor**.
- **Phase 1 LockedMax** — `[HarmonyPostfix] HeroData.GetMaxTagNum` ref __result override (cheat GameplayPatch.cs:84-100 100% mirror). PlayerEditorPanel 천부 섹션 헤더 = `[☐ Lock max] [ 999 ]` 즉시 적용 토글. Player heroID=0 only. ConfigEntry 영속.
- **Phase 2 속성·무학·기예 editor** — PlayerEditorPanel secondary tab `[기본 / 속성]` 추가. [속성] 탭 안에 3 column (속성 6 / 무학 9 / 기예 9), 각 row inline TextField × 2 (수치=base / 자질값=max) + buff/effective read-only. 일괄 button × 3 (`[속성 전체 N]` `[무학 전체 N]` `[기예 전체 N]`). [저장] gated apply → cheat `ChangeAttri/FightSkill/LivingSkill(hero, idx, val)` × N → `RefreshMaxAttriAndSkill()` 1회.

v0.7.8 자산 (PlayerEditorPanel base / Logger.InfoOnce / HeroLocator / Tab 패턴) 90%+ 재사용. v0.7.6 SettingsPanel 의 dirty-tracking + [저장] 패턴 100% mirror. v0.7.11 NPC dropdown 은 별도 cycle (모든 자산이 hero 인자 받도록 generalize).

## 1. 디자인 결정

### 1.1 Phase 매트릭스

| Phase | 섹션 | 필드 / 패치 대상 | Apply 경로 |
|---|---|---|---|
| **Phase 1** | LockedMax (천부) | `HeroData.GetMaxTagNum()` Postfix | `__result = LockedMaxTagNumValue` if `LockMaxTagNum && __instance.heroID == player.heroID` |
| | LockedMax UI | 천부 섹션 헤더 토글 + TextField | ConfigEntry 즉시 write (BepInEx 자동 영속) |
| **Phase 2** | 속성 (6) | `baseAttri[0..5]` / `maxAttri[0..5]` | cheat `ChangeAttri(hero, idx, val)` mirror — `if max < val: max = val` → `hero.ChangeAttri(idx, val - base, false, false)` → fallback `base[idx] = val` |
| | 무학 (9) | `baseFightSkill[0..8]` / `maxFightSkill[0..8]` | cheat `ChangeFightSkill(hero, idx, val)` mirror — 동일 패턴 |
| | 기예 (9) | `baseLivingSkill[0..8]` / `maxLivingSkill[0..8]` | cheat `ChangeLivingSkill(hero, idx, val)` mirror — 동일 패턴 |
| | Stat 재계산 | — | `RefreshMaxAttriAndSkill()` (v0.7.7 검증) [저장] 시 1회 |

각 Phase 별 commit 분리 — 각 commit 단위로 빌드/test/smoke 가능.

### 1.2 Phase 1 LockedMax — `HeroData.GetMaxTagNum` Postfix (cheat 100% mirror)

```csharp
// src/LongYinRoster/Patches/GetMaxTagNumPatch.cs (신규)
namespace LongYinRoster.Patches;

[HarmonyPatch(typeof(HeroData), "GetMaxTagNum")]
internal static class GetMaxTagNumPatch
{
    [HarmonyPostfix]
    internal static void Postfix(HeroData __instance, ref int __result)
    {
        try
        {
            if (!Plugin.Config.LockMaxTagNum.Value) return;
            int v = Plugin.Config.LockedMaxTagNumValue.Value;
            if (v <= 0) return;
            var player = HeroLocator.GetPlayer();
            if (player == null) return;
            if (__instance.heroID == player.heroID)
                __result = v;
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("GetMaxTagNumPatch", $"Postfix threw: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
```

**ConfigEntry** (BepInEx — Plugin.cs Config 클래스에 추가):
- `LockMaxTagNum` (bool, default false) — toggle
- `LockedMaxTagNumValue` (int, default 999, range [1, 999999]) — override 값

**UI** (PlayerEditorPanel 의 천부 섹션 헤더 — 기존 `천부 (N/M)` 라인 옆):
```
천부 (5/30)  [☐ Lock max] [ 999 ___ ]
─────────────────────────────────────
[+ 추가 …] (기존 v0.7.8 자산)
```
- 체크박스 toggle 또는 TextField 변경 시 → ConfigEntry 즉시 set (BepInEx 가 자동 file write)
- TextField 비숫자 → 무시 (Logger.WarnOnce + dirty 미적용)
- TextField 범위 외 → clamp [1, 999999]

### 1.3 Phase 2 — 속성·무학·기예 editor

#### 1.3.1 Tab 구조 (Q5=E)

PlayerEditorPanel 헤더에 secondary tab 추가:
```
+ 플레이어 편집 ────────────── [X] +
| {player.heroName}              |
| [ 기본 ]  [ 속성 ]              |  ← 신규 secondary tab
| ─────────────────────────────  |
| (탭 내용)                       |
```

- **[기본] 탭** = v0.7.8 의 6 섹션 (Resource / SpeAddData × 3 / 천부 / 무공 / Breakthrough) 그대로 보존
- **[속성] 탭** = 신규 — 속성 6 / 무학 9 / 기예 9 = 3 column

탭 state = `PlayerEditorPanel._activeTab` (enum {Basic, Attri}, default Basic). 영속화 안 함 (session-only).

#### 1.3.2 [속성] 탭 layout (3-column)

```
┌── 속성 ──────────────┐ ┌── 무학 ──────────────┐ ┌── 기예 ──────────────┐
│ 근력 [199] / [999] +246 → 445 │ 내공 [155] / [999] +151 → 306 │ 의술 [455] / [999] +15 → 470 │
│ 민첩 [165] / [999] +230 → 395 │ 경공 [155] / [999] +104 → 259 │ 독술 [255] / [999] +2  → 257 │
│ ...                  │ ...                  │ ...                  │
│ [전체 999]           │ [전체 999]           │ [전체 999]           │
└──────────────────────┘ └──────────────────────┘ └──────────────────────┘
                          [저장]  [되돌리기]
```

- 720 width 분할: column 240 each (라벨 56 + base input 56 + `/` + max input 56 + `+buff` 36 + `→ effective` 36)
- row height 24, 9 rows max → column height 약 240. 스크롤 미필요 (속성 6 짧음, 무학/기예 9 충분)
- 일괄 button = column 하단 `[전체 N]` (TextField + 적용 button) → buffer 의 maxValue 만 일괄 set
- [저장] / [되돌리기] = 탭 하단 공통

#### 1.3.3 편집 mechanism (Q6=B)

**Buffer 구조** (`AttriTabPanel._buffer`):
```csharp
private record AttriRow(int Index, string Label, float OriginalBase, float OriginalMax,
                       string BaseInput, string MaxInput);
private List<AttriRow> _attriRows;       // 6
private List<AttriRow> _fightSkillRows;  // 9
private List<AttriRow> _livingSkillRows; // 9
```

- 탭 활성화 시 `LoadFromHero(hero)` — base/max read → originals 저장 + Input 초기화
- TextField 수정 시 buffer 업데이트만 (game state 미변경)
- dirty = (BaseInput != OriginalBase.ToString() OR MaxInput != OriginalMax.ToString()) for any row

**[저장] 클릭 흐름**:
1. dirty rows 수집 (3 그룹 통합)
2. 각 row 마다 parse → clamp [0, 999999] → `CharacterAttriEditor.Change*(hero, idx, val)` 호출
3. **마지막 1회만** `TryInvokeRefreshMaxAttriAndSkill(hero)` (v0.7.7 검증된 helper 재사용)
4. read-back 검증: 다시 base/max 읽어서 buffer 와 비교 → 성공/실패 카운트
5. toast: `"속성 N 항목 적용됨"` 또는 `"성공 N / 실패 K"`
6. `LoadFromHero(hero)` 다시 호출 → buffer 초기화 + dirty 해제

**[되돌리기] 클릭**:
- `LoadFromHero(hero)` 만 호출. buffer originals 로 복원.

**[전체 N] 일괄 button** (column 하단):
- TextField 의 N parse → 그 column 의 모든 row 의 MaxInput = N 으로 set (BaseInput 미수정)
- buffer 업데이트만 (즉시 commit 아님). [저장] 으로 일괄 적용.

### 1.4 신규 자산 (LOC 추정)

| File | 역할 | LOC |
|---|---|---|
| `src/LongYinRoster/Patches/GetMaxTagNumPatch.cs` | Phase 1 Postfix | ~30 |
| `src/LongYinRoster/Core/HeroAttriReflector.cs` | baseAttri/maxAttri/baseFightSkill/maxFightSkill/baseLivingSkill/maxLivingSkill read + heroBuff lookup | ~150 |
| `src/LongYinRoster/Core/CharacterAttriEditor.cs` | cheat ChangeAttri/FightSkill/LivingSkill mirror — `Change(hero, axis, idx, val)` + sanitize wrapper | ~120 |
| `src/LongYinRoster/UI/AttriTabPanel.cs` | [속성] 탭 IMGUI — 3-column row drawing + buffer + [저장]/[되돌리기]/일괄 | ~250 |
| `src/LongYinRoster/Util/AttriLabels.cs` | 24 한글 라벨 (속성 6 / 무학 9 / 기예 9) — hardcoded fallback + LTLocalization 우선 | ~60 |
| `PlayerEditorPanel.cs` 수정 | secondary tab 추가 + 천부 섹션 헤더 lock 토글 | +~80 |
| `Plugin.cs` Config 수정 | LockMaxTagNum + LockedMaxTagNumValue ConfigEntry 추가 | +~10 |
| Tests | CharacterAttriEditorTests + GetMaxTagNumPatchTests + AttriTabPanelBufferTests | ~250, +20~25 tests |

총 신규 LOC ≈ 950 (impl) + 250 (tests). v0.7.8 (1500 LOC impl + 320 tests) 보다 작음.

### 1.5 Hangul label source (속성/무학/기예)

| Source | 비고 |
|---|---|
| 자체 hardcoded 24 entry (`AttriLabels`) | 한글 fixed — 근력/민첩/지력/의지/체질/경맥 + 내공/경공/절기/권장/검법/도법/장병/기문/사술 + 의술/독술/학식/언변/채벌/목식/단조/제약/요리 |
| `HangulDict.Translate(label)` (v0.7.5 자산) | 게임 한글 라벨 일치 시 사용. 미일치 시 hardcoded fallback |
| LTLocalization | 미사용 (HangulDict stage 4 가 이미 cover) |

→ 결정: hardcoded 24 entry primary, HangulDict optional (사용자 통팩 환경 대응). v0.7.5 의 4-stage fallback 자산 재사용.

## 2. 적용 시 절차 (사용자 시나리오)

### 2.1 Phase 1 LockedMax 사용 흐름

1. F11 → "플레이어 편집" → PlayerEditorPanel open
2. [기본] 탭 의 천부 섹션 헤더 = `[☐ Lock max] [ 30 ]` (default value = current GetMaxTagNum 또는 999)
3. 사용자가 체크 + 값 999 입력 → ConfigEntry 즉시 write (영속화)
4. 게임 안에서 천부 추가 시 GetMaxTagNum 호출 → Postfix override → __result = 999 → 게임이 999 까지 추가 허용
5. uncheck 또는 panel 닫기 → 다음 GetMaxTagNum 호출 시 Postfix early-exit → 원래 limit 복귀

### 2.2 Phase 2 속성·무학·기예 편집 흐름

1. F11 → 플레이어 편집 → [속성] 탭
2. 24 row 의 inline TextField 수정 (예: 근력 base 199 → 999, 자질값 999 → 9999)
3. dirty rows 자동 추적 — [저장] 버튼 enabled
4. [저장] 클릭 → CharacterAttriEditor.Change* × N → RefreshMaxAttriAndSkill 1회 → toast `"속성 N 항목 적용됨"`
5. read-back 으로 buffer 갱신 → dirty 해제
6. 또는 [되돌리기] → buffer originals 로 복원, dirty 해제
7. 또는 [전체 999] 일괄 → 그 column 의 모든 row 의 MaxInput = 999 set → [저장] 으로 일괄 commit

## 3. UI placement — PlayerEditorPanel secondary tab

### 3.1 ModWindow / ModeSelector 변경 없음

- v0.7.8 의 F11+4 단축키 그대로
- ModeSelector.Mode = {None, Character, Container, Settings, Player} 그대로 (Player 가 v0.7.10 에서도 PlayerEditorPanel)

### 3.2 PlayerEditorPanel 변경

**탭 헤더 추가** (Window 의 첫 row):
```csharp
GUILayout.BeginHorizontal();
if (DrawTabButton("기본", _activeTab == Tab.Basic)) _activeTab = Tab.Basic;
if (DrawTabButton("속성", _activeTab == Tab.Attri)) _activeTab = Tab.Attri;
GUILayout.EndHorizontal();
```

**탭 분기**:
```csharp
switch (_activeTab)
{
    case Tab.Basic:
        DrawBasicTab();  // v0.7.8 기존 6 섹션
        break;
    case Tab.Attri:
        _attriTabPanel.Draw(player);  // 신규
        break;
}
```

**천부 섹션 헤더 변경** (기존 `천부 (N/M)` 옆):
```csharp
GUILayout.Label($"천부 ({n}/{m})", GUILayout.Width(120));
bool newLock = GUILayout.Toggle(Plugin.Config.LockMaxTagNum.Value, "Lock max", GUILayout.Width(80));
if (newLock != Plugin.Config.LockMaxTagNum.Value)
    Plugin.Config.LockMaxTagNum.Value = newLock;  // BepInEx 자동 file write
string newValStr = GUILayout.TextField(_lockValueBuffer, 6, GUILayout.Width(64));
if (newValStr != _lockValueBuffer)
{
    _lockValueBuffer = newValStr;
    if (int.TryParse(newValStr, out int v) && v >= 1 && v <= 999999)
        Plugin.Config.LockedMaxTagNumValue.Value = v;
}
```

### 3.3 panel 크기

- v0.7.8 의 720×720 그대로
- secondary tab 추가 시 헤더 +24 height → 본문 -24 (scroll 안에서 흡수)
- [속성] 탭 layout 720 width 분할: column 240 each, row 24 height × 9 row + 헤더 28 + 일괄 button 28 = 약 280 height. 720 안에 충분.

## 4. 테스트 전략

### 4.1 Unit tests (~20-25 추가)

| Test class | 검증 항목 | 추정 갯수 |
|---|---|---|
| `CharacterAttriEditorTests` | 클램프 [0, 999999] / dirty 추적 / 일괄 set / 비숫자 무시 / sanitize 1회 | ~10 |
| `HeroAttriReflectorTests` | reflection probe (mock HeroData fixture) / index 6/9/9 verify / heroBuff lookup | ~6 |
| `GetMaxTagNumPatchTests` | toggle off skip / on override / heroID mismatch skip / value=0 skip / null player guard | ~5 |
| `AttriTabPanelBufferTests` | buffer load from hero / dirty 추적 / 일괄 button / [되돌리기] reset | ~4 |

### 4.2 인게임 smoke (Phase 1 + Phase 2)

**Phase 1 LockedMax**:
1. F11 → 플레이어 편집 → 천부 섹션 헤더 lock 체크 + 값 999
2. ConfigEntry write 검증 (`BepInEx/config/com.deepe.longyinroster.cfg` open)
3. 게임 안에서 천부 추가 → 999 까지 추가 가능 검증
4. uncheck → 다음 추가 시 원래 limit 복귀 검증

**Phase 2 속성·무학·기예**:
1. F11 → 플레이어 편집 → [속성] 탭
2. 속성 6 / 무학 9 / 기예 9 row 표시 + buff/effective 표시 검증
3. 근력 base 199 → 999 입력 → [저장] → read-back 확인 (base = 999, max ≥ 999)
4. 무학 일괄 [전체 9999] → [저장] → 9 row 모두 max = 9999 검증
5. RefreshMaxAttriAndSkill 호출 후 maxhp/maxpower 변화 검증 (속성 → derived stat 영향)
6. [되돌리기] → originals 복원
7. 비숫자 입력 → 무시 + WarnOnce
8. 회귀 검증: [기본] 탭 의 v0.7.8 6 섹션 모두 작동 (Resource / SpeAddData / 천부 / 무공 / Breakthrough)

### 4.3 Strip-safe 검증 (v0.7.6 자산 정렬)

- `GUILayout.TextField(string, int, params)` ✓ (v0.7.6 검증)
- `GUILayout.Toggle(bool, string, params)` ✓ (v0.7.6 검증)
- `GUILayout.Button(string, params)` ✓ (v0.7.6 검증)
- `GUILayout.BeginHorizontal/EndHorizontal/BeginVertical/EndVertical` ✓ (v0.7.6 검증)
- `GUILayout.Label(string, params)` ✓
- `GUI.color` getter/setter ✓ (column 헤더 색상)
- `GUI.enabled` getter/setter ✓ (v0.7.6 [저장] disabled UX)

→ **신규 IMGUI API 도입 0**. v0.7.6 strip-safe set 안에서만 작동.

## 5. 영속화

| 항목 | 위치 | 영속? |
|---|---|---|
| `LockMaxTagNum` (bool) | BepInEx Config (`com.deepe.longyinroster.cfg`) | ✅ 자동 |
| `LockedMaxTagNumValue` (int) | 동상 | ✅ 자동 |
| 활성 탭 (`_activeTab`) | session memory | ❌ 매 panel open 시 [기본] 으로 시작 |
| 속성/무학/기예 buffer | `AttriTabPanel._buffer` | ❌ session-only — panel 재open 시 hero 에서 read |
| 일괄 [전체 N] TextField 의 N | `AttriTabPanel._bulkInputs` | ❌ session-only |

[저장] 후의 game state 변경은 BepInEx config 와 무관 — game 의 save 흐름에 의존 (사용자가 게임 안에서 save 하면 maintained, 안 하면 reload 시 사라짐).

## 6. Spike list (impl 진입 전 검증 필수)

Plan 단계에서 spike commit 으로 검증. NO-GO 시 fallback 명시.

| # | 검증 항목 | 위치 / 방법 | NO-GO fallback |
|---|---|---|---|
| 1 | `HeroData.GetMaxTagNum()` 존재 + return int | `mcp__plugin_oh-my-claudecode_t__lsp_workspace_symbols` 또는 reflection probe | LockedMax 폐기 (Phase 1 NO-GO, v0.7.10 = Phase 2 만) |
| 2 | `HeroData.baseAttri[i] (Property indexer)` + `Count = 6` + `ChangeAttri(int, float, bool, bool)` | reflection probe | reflection direct set fallback (cheat 패턴 mirror) |
| 3 | `HeroData.baseFightSkill / maxFightSkill / ChangeFightSkill` 동일 + `Count = 9` | 동상 | 동상 |
| 4 | `HeroData.baseLivingSkill / maxLivingSkill / ChangeLivingSkill` 동일 + `Count = 9` | 동상 | 동상 |
| 5 | heroBuff[i] index 가 baseAttri[i] index 와 매칭 | cheat reference 분석 + runtime probe | mismatch 시 effective display 빼기 (수치/자질값 input 만) |
| 6 | LTLocalization or HangulDict 의 6+9+9 라벨 한글 일치 | runtime check (HangulDict.Translate 호출) | hardcoded 24 entry fallback (`AttriLabels`) |
| 7 | `RefreshMaxAttriAndSkill` 호출 후 자원 stat 의 변경 (maxhp 변화 등) — 사용자 의도 반영 | 인게임 smoke step 5 | side-effect 명시 (toast 안에 "max 자원 재계산됨") |

## 7. Risk + 대응

### 7.1 IMGUI strip risk

Phase 2 의 TextField 48 + Toggle 1 + Button 6 = 55 IMGUI call. 모두 v0.7.6 검증된 strip-safe API 만 사용. 신규 도입 0.

### 7.2 RefreshMaxAttriAndSkill 비용

[저장] 시 1회만 호출. v0.7.7/v0.7.8 검증. 즉시 commit pattern 안 쓰는 이유.

### 7.3 Postfix 호출 빈도

`HeroData.GetMaxTagNum` 가 게임 내부 매 frame 호출되면 Postfix 도 매 frame. early-exit (`if (!Lock) return`) 비용 무시 가능. cheat 도 같은 패턴 — 검증된 안정성.

### 7.4 사용자 실수 시 game state 깨짐

- 자질값 너무 큰 값 → maxhp/maxpower 계산 overflow 가능
- 부분 mitigation: clamp [0, 999999] 으로 제한
- 추가 mitigation: 슬롯 0 자동백업 (v0.3+ 기존 자산) — 사용자가 [복원] 으로 회복 가능
- 명시적 mitigation: [저장] 직전 confirm dialog 미도입 (UX 무거움) — clamp + 자동백업 으로 충분 추정

### 7.5 자질 grade marker (신/하 등) 미구현

- screenshot 의 신/하 marker 미표시
- v0.7.10.1 patch 또는 v0.7.11 NPC editor cycle 안에서 추가 (derivation rule spike 또는 별도 field 발견 후)
- 사용자 영향: 수치 직접 보고 grade 추정 가능. v0.7.10 cycle 에서는 기능적 영향 없음

### 7.6 baseAttri 가 game-self 의 dynamic recalculation 을 받는 경우

- 일부 게임은 base 가 buff/equipment/level 의 derived value
- cheat 의 `hero.ChangeAttri(idx, delta, false, false)` 가 game-self method 라 안전 추정 (cheat 가 작동 검증)
- fallback `base[idx] = val` 직접 set 도 cheat 패턴 mirror

## 8. v0.7.11 NPC dropdown (deferred — 별도 cycle)

v0.7.10 의 모든 자산이 hero 인자 받도록 generalize:
- `HeroAttriReflector.Get*(hero, idx)` — 이미 hero 인자
- `CharacterAttriEditor.Change*(hero, idx, val)` — 이미 hero 인자
- `PlayerEditorPanel._currentHero` 필드 추가 (default = HeroLocator.GetPlayer())
- Phase 1 LockedMax 의 Postfix 는 player heroID 만 검사 → NPC 에 적용 안 함 (의도된 mental model 분리)
- v0.7.11 진입 시 PlayerEditorPanel header 에 heroID dropdown 추가 + SelectorDialog 2단계 탭 (force/문파 + name search)

v0.7.11 별도 brainstorm cycle. v0.7.10 에서는 PlayerEditorPanel 이 player only.

## 9. 빌드 / 배포 / 영향

- 빌드: 신규 file 5개 + 기존 file 2개 수정
- 영향: BepInEx Plugin 의 `Awake` 에 1 Harmony patch 추가 (`GetMaxTagNumPatch`), Config 의 ConfigEntry 2개 추가
- 호환성: 기존 v0.7.8 사용자 설정 (sort/filter/last/rect/window/hotkey 4) 변경 없음 — append-only

## 10. 작업 순서 (plan 의 phase 분해)

1. **Phase 1 spike** — HeroData.GetMaxTagNum 존재 검증 + cheat 패턴 mirror 가능 verify
2. **Phase 1 impl** — GetMaxTagNumPatch + ConfigEntry 추가 + PlayerEditorPanel 천부 섹션 헤더 토글
3. **Phase 1 tests + smoke** — 5 unit tests + 인게임 smoke (lock 토글 + 값 변경 + 천부 추가 limit 검증)
4. **Phase 2 spike** — baseAttri/maxAttri/ChangeAttri × 3 axis + Count + heroBuff index 검증
5. **Phase 2 impl** — HeroAttriReflector + CharacterAttriEditor + AttriLabels + AttriTabPanel + PlayerEditorPanel tab 헤더
6. **Phase 2 tests** — ~20 unit tests
7. **Phase 2 smoke** — 인게임 smoke (24 row × 2 + 일괄 + RefreshMaxAttriAndSkill + 회귀 v0.7.8 6 섹션)
8. **release** — VERSION bump + CHANGELOG + git tag v0.7.10 + GitHub release + roadmap meta spec G3 Decision append + HANDOFF 갱신

## 11. 의존성

- v0.7.8 의 PlayerEditorPanel base — 100% 보존 (탭 분기 추가)
- v0.7.7 의 RefreshMaxAttriAndSkill helper (`PlayerEditApplier.TryInvokeRefreshMaxAttriAndSkill`) — Phase 2 sanitize 에서 재사용
- v0.7.6 의 dirty + [저장] gated 패턴 — Phase 2 buffer 에 100% mirror
- v0.7.6 의 ConfigEntry write 즉시 영속 패턴 — Phase 1 Lock 토글에 100% mirror
- v0.7.5 의 HangulDict — 라벨 fallback (optional)
- v0.7.3+ 검증된 strip-safe IMGUI patterns — Phase 2 의 모든 IMGUI 호출이 이 set 안에서

## 12. 미반영 / 후속 sub-project 후보

- **자질 grade marker (신/하 등)** — derivation rule 또는 별도 field 미확인. v0.7.10.1 patch 또는 v0.7.11 NPC editor 안에서 추가
- **NPC dropdown (heroID switch)** — v0.7.11 별도 cycle
- **무공 자질 (kungfuSkills 의 자질값)** — 168 무공 각각의 자질? 별도 field 미확인. 추후 sub-project
- **천부 카테고리별 max** (sameMeaning 그룹 max) — Q1 = A 로 GetMaxTagNum (전체 max) 만 cover. 카테고리별 lock 미지원
- **Lock 시스템 확장** — Resource stat (hp/power/mana/weight) lock = cheat StatEditor LockedMax 매 frame 패턴 — Q1 에서 deferred (B/C/D 후보)
- **v0.8 진짜 sprite** — G3 Decision 에서 DEFER until G4 (v0.7.11 또는 그 이후 게이트)

---

**Decision Gate (G3) — append 후 commit**

본 spec 작성 + commit 시점에 메타 로드맵 [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §Decision Log 의 G3 Pending → G3 Decision 으로 변경:

```
### G3 Decision (2026-05-09)

v0.7.8 release 직후 G3 게이트 평가:
- **v0.7.8.1 LockedMax + v0.7.10 NPC editor 결합**: **GO** (사용자 ③ 채택, 단일 cycle, tag = v0.7.10).
  Q4 = β 로 NPC dropdown 은 v0.7.11 분리, v0.7.10 = LockedMax + 속성·무학·기예 editor.
  rationale = v0.7.8 자산 기억 신선할 때 가장 큰 새 feature (속성/무학/기예) 흡수.
- **v0.8 진짜 sprite**: **DEFER until G4** → IL2CPP sprite spike 비용 별도, v0.7.10/v0.7.11 후 재평가.
- **v0.7.9 Slot diff preview**: **DEFER until G4** → Apply pipeline 변경 cycle, NPC editor 후 자연스러운 후속.
- **maintenance**: **WAIT** → trigger 미발견.

Next sub-project: **v0.7.10** (천부 max lock + 속성·무학·기예 editor) brainstorm cycle 시작 (메타 §5.3 후보 sub-project = 5~10 질문 cycle, 본 spec 의 §brainstorm 결과 9 Q 진행 완료).
```

---

**spec END.** Plan 단계에서 phase 별 task 분해 + spike 검증 commit + impl commit + tests + smoke + release 7-step 작성.
