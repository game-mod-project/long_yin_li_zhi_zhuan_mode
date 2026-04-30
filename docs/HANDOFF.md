# LongYin Roster Mod — 작업 핸드오프 문서

**일시 중지**: 2026-04-30
**진행 상태**: **v0.4.0 출시 완료** (selection-aware Apply / Restore + 정체성 활성화 + 9-카테고리 체크박스 UI).
**저장소**: https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode (`main` 브랜치)
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`
**Releases**:
- [v0.1.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.1.0) — Live capture + slot management
- [v0.2.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.2.0) — Import from save + input gating
- [v0.3.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.3.0) — Apply (stat-backup) + Restore + save/reload 안전성
- [v0.4.0] — 9-카테고리 체크박스 UI + 정체성 활성화 + 부상/충성/호감 영구 보존 회귀

---

## 1. 한 줄 요약

BepInEx 6 IL2CPP 환경에서 플레이어 캐릭터 스냅샷을 20슬롯에 저장 / 관리하는 모드.
**v0.4.0 출시 완료**: v0.3 의 PinpointPatcher Apply / Restore 에 더해
**9-카테고리 체크박스 UI** (슬롯별 selection 즉시 저장) + **정체성 활성화** (heroName / nickname / age 등
9 필드 setter direct — PoC A2 PASS) + **부상/충성/호감 영구 보존 회귀** (v0.3 의 덮어쓰기 폐기).

**Scope (v0.4)**: v0.3 stat-backup 전체 + 정체성 9 필드. 카테고리 단위 선택 Apply.
부상 (외상/내상/중독) / 충성 / 호감 은 보존 필드로 전환 — 현재 게임 상태 유지.
save → reload 후 정보창 정상 작동 (smoke D15 PASS, 천부 17/17 포함).

**v0.5+ 후보**: 무공 active (PoC A3 FAIL — semantic mismatch) / 인벤토리 / 창고 (PoC A4 FAIL —
sub-data wrapper graph 미해결) / 무공 list / 외형. spec §12 deferred list 참고.

---

## 2. 현재 깃 히스토리 (main HEAD — v0.4 완료 후)

```
2d4b24e chore(release): remove HeroDataDumpV04 + [F12] handler (D16)             ← v0.4 release prep
a127206 docs: D15 smoke v0.4 PASS — 모든 항목 통과 (천부 fix eaf2938 후 17/17)
eaf2938 fix(core): RebuildHeroTagData JSON schema 정정 — heroTagData 가 Array
10069fd fix(ui): WindowH default 480→560 + toast on selection save failure
8f3edf1 feat(ui): v0.4 SlotDetailPanel 9-카테고리 체크박스 grid + 즉시 저장 wiring
c99d709 feat(strings): v0.4 — 9 카테고리 label + disabled suffix
4e60687 feat(core): selection-aware PinpointPatcher 9-step + Probe + ModWindow wiring
02e349e feat(core): IdentityFieldMatrix (Setter) + ItemDataFactory (v0.4 stub) per PoC results
e9894c7 feat(slots): SlotRepository.UpdateApplySelection — toggle 즉시 저장 path
748eaae feat(slots): _meta.applySelection schema + read/write + legacy fallback test
30626c9 feat(core): SimpleFieldMatrix Category enum + v0.4 17 entry
4a9be77 feat(core): v0.4 Capabilities POCO
256bfc5 feat(core): v0.4 ApplySelection POCO + JSON helpers + 4 tests
7d57fea poc: v0.4 ItemData PoC FAIL — defer to v0.5+
c83e808 poc: v0.4 ActiveKungfu PoC FAIL — defer to v0.5+
4887f01 poc: v0.4 Identity PoC PASS — setter direct (in-memory)
(이전) chore(release): v0.3.0 — VERSION bump + README/HANDOFF update             ← v0.3.0 tag
6929201 chore(release): remove HeroDataDump temp tool + [F12] handler            ← Task 21
ca194bb feat(ui): activate Apply/Restore buttons; remove temp smoke handlers     ← Task 18
853aa8f feat(ui): ModWindow.RequestApply / DoApply / AttemptAutoRestore wired    ← Task 16
6c89076 feat(core): RefreshSelfState step 6 (fatal) + smoke [F11+R]              ← Task 12
c20c237 feat(core): RebuildHeroTagData step 5 + smoke [F11+T]                    ← Task 11
a747996 fix(core): SimpleFieldMatrix 22→18 + Apply cache guard + doc 보강        ← Task 7-fix
470bbbc feat(core): SetSimpleFields step 1 + special-cases + smoke handler       ← Task 7
829cd7e docs: HeroData method dump + spec/plan refined per dump                  ← Task 2
13ed023 docs: v0.3 spec + plan — PinpointPatcher Apply pipeline design           ← Task 1
4ec9db5 docs: handoff bumped past v0.2.0 with v0.3 Apply replan
8c89fe4 docs: README — bump for v0.2 capabilities                                ← v0.2.0 tag
2286366 feat(slots+ui): import-from-game-save flow + scroll-wheel block          ← Task 21
17140aa fix(ui): Harmony-patch mouse input through mod window region             ← S0+S1
473763d docs: add README and prepare v0.1.0 release packaging                    ← v0.1.0 tag
b3e300d feat(ui): drop v0.1 Apply path, add slot edit handlers, polish window UX ← C-4 + D
6ba31eb docs: handoff update — Task 17 verified, IL2CPP traps documented
8d115d4 refactor(slots+ui): System.Text.Json migration + same-slot overwrite
59e3be2 fix(core): HeroLocator IL2CPP-compatible iteration
601602c docs: handoff document — work paused after Task 17
ca6d630 feat(capture): wire live capture — HeroLocator + SerializerService real impl   ← Task 17
266fca4 feat(ui): SlotListPanel + SlotDetailPanel with selection + action callbacks    ← Task 16
a780d98 feat(ui): ModWindow IMGUI shell with F11 toggle, drag, position persistence    ← Task 15
f04b805 feat(ui): ToastService with 3s auto-dismiss                                    ← Task 14
428f2b0 feat(config): bind BepInEx ConfigEntries                                       ← Task 13
f507e78 feat(core): PinpointPatcher skeleton with debug logging                        ← Task 12
7588833 feat(core): HeroLocator finds heroID=0 player via reflection                   ← Task 11 (rewritten in 59e3be2)
8b9eebd feat(core): SerializerService stub                                             ← Task 10 (rewritten/trimmed in b3e300d)
dc01499 fix(slots): bump ParseHeader default to 512KB
42c045f feat(slots): SaveFileScanner header parsing                                    ← Task 9 (rewritten in 2286366)
16c38ea feat(slots): SlotRepository with slot 0 auto-backup guard                      ← Task 8
3301432 feat(slots): atomic SlotFile I/O                                               ← Task 7 (rewritten in 8d115d4)
ea18a73 feat(slots): SlotMetadata + SlotPayload                                        ← Task 6 (rewritten in 8d115d4)
850fc55 feat(core): PortabilityFilter strips faction + runtime fields                  ← Task 5 (rewritten in b3e300d via System.Text.Json)
87d9b55 feat(util): KoreanStrings + PathProvider                                       ← Task 4
71614ca test: test project skeleton with frozen Hero fixture (35MB)                    ← Task 3
573738e refactor(plugin): Log → Logger; gate deploy target to Release
9b8e85e feat(plugin): bootstrap BepInEx plugin loading and logging                     ← Task 2
e45b164 fix(build): allow GameDir env-var override
bb17569 chore: initialize solution and shared build props                              ← Task 1
```

**Tags**: `v0.1.0` (at `473763d`), `v0.2.0` (at `8c89fe4`), `v0.3.0` (pending — Task 23).
**Branch**: v0.3 (21+ commits ahead of main).

---

## 3. 프로젝트 구조 (현재 디스크 상태)

```
_PlayerExport/
├── README.md                  ← 사용자 가이드 (v0.2 기능 + v0.3 deferred 명시)
├── LongYinRoster.sln
├── Directory.Build.props      ← GameDir env-var fallback
├── .gitignore                 ← .superpowers/ + .omc/ + dist/ 제외
├── docs/
│   ├── HANDOFF.md             ← 이 파일
│   └── superpowers/
│       ├── specs/2026-04-27-longyin-roster-mod-design.md   (~600줄)
│       └── plans/2026-04-27-longyin-roster-mod-plan.md     (~3000줄)
├── dist/                      ← gitignore. v0.1.0 / v0.2.0 zip + 폴더 구조
└── src/
    ├── LongYinRoster/                       ← 메인 플러그인
    │   ├── LongYinRoster.csproj             ← 0Harmony / Il2CppInterop / Newtonsoft 등 reference
    │   ├── Plugin.cs                        ← BepInPlugin entry + Harmony.PatchAll
    │   ├── Config.cs                        ← PauseGameWhileOpen default true
    │   ├── Core/
    │   │   ├── HeroLocator.cs               ← reflection Count + indexer (IL2CPP-compatible)
    │   │   ├── SerializerService.cs         ← Serialize 만 유지 (Populate / DeserializeHero 제거됨)
    │   │   ├── PortabilityFilter.cs         ← System.Text.Json 으로 재작성. 24+21=45 필드 제거
    │   │   └── PinpointPatcher.cs           ← v0.2 까지 무동작 스켈레톤. v0.3 의 Apply 핵심
    │   ├── Slots/
    │   │   ├── SlotPayload.cs               ← Player = raw JSON string
    │   │   ├── SlotMetadata.cs              ← System.Text.Json 으로 메타 추출
    │   │   ├── SlotEntry.cs
    │   │   ├── SlotFile.cs                  ← System.Text.Json Read/Write + atomic .tmp→Replace
    │   │   ├── SlotRepository.cs            ← 21슬롯 (0=자동백업 + 1~20)
    │   │   └── SaveFileScanner.cs           ← System.Text.Json. LoadHero0 → raw JSON string
    │   ├── UI/
    │   │   ├── ModWindow.cs                 ← MonoBehaviour, F11, _instance + ShouldBlockMouse
    │   │   ├── SlotListPanel.cs             ← 왼쪽 21행 + [+] / [F]
    │   │   ├── SlotDetailPanel.cs           ← 오른쪽 상세. Apply/Restore 버튼 disabled (v0.3 예정)
    │   │   ├── ConfirmDialog.cs             ← 재사용 modal — IL2CPP-safe IMGUI 패턴 정립
    │   │   ├── InputDialog.cs               ← 텍스트 입력 modal (Rename/Comment)
    │   │   ├── FilePickerDialog.cs          ← SaveSlot 0~10 list + 클릭 import
    │   │   ├── DialogStyle.cs               ← 재사용 0.85α 검정 overlay (불투명도 보강)
    │   │   ├── InputBlockerPatch.cs         ← Harmony Prefix on Input.GetMouseButton* / GetAxis
    │   │   └── ToastService.cs              ← 3초 자동소멸
    │   └── Util/
    │       ├── Logger.cs                    ← BepInEx 로거 래퍼
    │       ├── KoreanStrings.cs             ← UI 문자열 상수 (Apply / Capture overwrite / Input dialogs)
    │       └── PathProvider.cs              ← <PluginPath> 토큰 + GameSaveDir
    └── LongYinRoster.Tests/                 ← 테스트 (18 tests, all pass)
        ├── LongYinRoster.Tests.csproj
        ├── fixtures/slot3_hero.json         ← 35MB 동결 baseline
        ├── SmokeTests.cs
        ├── PortabilityFilterTests.cs        (4)
        ├── SlotMetadataTests.cs              (1)  ← string 입력
        ├── SlotFileTests.cs                  (3)  ← string Player + JsonDocument 검증
        ├── SlotRepositoryTests.cs            (7)
        └── SaveFileScannerTests.cs           (2)
```

**테스트**: `dotnet test` → **18/18 PASS**.
**빌드**: `dotnet build -c Release` → 자동으로 `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 배포.

---

## 4. 가장 중요한 발견 (다음 세션 작업자 필독)

### 4.1 IL2CPP-bound Newtonsoft.Json API gap

`BepInEx/interop/Newtonsoft.Json.dll` 은 표준 NuGet 버전과 **다른 표면 + 다른 type identity**.

| 표준 Newtonsoft | IL2CPP 버전 | 우리 대응 |
|---|---|---|
| `JsonConvert.SerializeObject(obj, settings)` | ❌ 1+settings 오버로드 없음 | `(Il2CppSystem.Object)` 단일 인자 |
| `JsonConvert.PopulateObject(json, obj, settings)` | ❌ **부재** | (해당 없음 — Populate 자체 deprecated) |
| `JsonSerializer.Populate(JsonReader, Object)` | ⚠ **silent no-op** in IL2CPP | **사용 금지**. v0.3 에서 PinpointPatcher 로 우회 |
| `JsonConvert.DeserializeObject(string, Type)` | ⚠ `Il2CppSystem.Type` 받음 | `Il2CppType.From(systemType)` 변환 후 호출. 결과는 `Il2CppSystem.Object`, wrapper class 의 `IntPtr` ctor 로 다시 감싸야 |
| `JArray` cast (`as JArray` / `(IList<JToken>)`) | ❌ **silently null** — type identity 충돌 | **System.Text.Json 으로 우회** (모든 슬롯/파일 경로) |
| `(JObject)root["player"]` cast | ❌ ArgumentNullException 또는 silently null | 동상 |
| `JObject` foreach (C# duck-typed) | ❌ enumerator 호환 안 됨 | `for (int i = 0; i < arr.Count; i++)` |
| `JsonTextReader` ctor | takes `Il2CppSystem.IO.TextReader` | `new Il2CppSystem.IO.StringReader(json)` |

**현재 코드베이스의 정책**: IL2CPP-bound Newtonsoft 는 **`SerializerService.Serialize` 한 곳에서만** 사용 (game 객체 → JSON string, 1방향). 그 결과 string 은 **System.Text.Json** 으로만 traverse. SlotPayload.Player 도 raw string. JObject / JArray 는 코드베이스 어디에서도 더 이상 instance 로 다루지 않음.

### 4.2 IL2CPP-bound `Il2CppSystem.Collections.Generic.List<T>` 는 .NET IEnumerable 미구현

`HeroList` 가 `Il2CppSystem.Collections.Generic.List<HeroData>` 로 노출되는데 우리 .NET 의 `System.Collections.IEnumerable` 을 구현하지 않는다. `is IEnumerable` 검사 → false → foreach 불가.

**대응**: reflection 으로 `Count` property + `Item` indexer (또는 `get_Item(int)`) 호출. HeroLocator 의 핵심 패턴.

또한 `BindingFlags.Public | BindingFlags.Static` 만 사용하면 generic singleton base class 의 `Instance` property 가 derived wrapper class 에서 안 보임. **`BindingFlags.FlattenHierarchy` 필수**. IL2CPP wrapper 가 backing field 만 노출하는 경우도 있어 **property → field fallback** + 흔한 별명(`instance`, `_instance`, `s_Instance`, `s_instance`) 시도.

### 4.3 IL2CPP IMGUI strip — 매 frame `Method unstripping failed`

이 게임의 IL2CPP 빌드는 다음 IMGUI API 들을 strip:

- `GUILayout.FlexibleSpace()`
- `new GUIStyle(GUIStyle other)` ctor
- `GUILayout.Label(string, GUIStyle, GUILayoutOption[])` (그리고 다른 GUIStyle 받는 overload들)
- 기타 GUIStyle 인자 받는 IMGUI overload 다수

**대응**: `GUILayout.Space(int)`, `GUILayout.Button(string, GUILayoutOption[])`, `GUILayout.Label(string)` 같은 **default skin + 단순 overload** 만 사용. 새 dialog 추가 시 ConfirmDialog / InputDialog / FilePickerDialog 의 패턴 그대로 따름:
- `GUI.enabled = true` 강제 (Draw 진입 + DrawWindow callback 시작)
- `GUILayout.Space(N)` 명시값으로 정렬 (FlexibleSpace 회피)
- try/catch 가드 (logging 폭주 방지 + 새 strip 발견 시 진단)

### 4.4 Apply (slot → game) 의 깊은 IL2CPP 한계 (v0.3 의 핵심 도전)

**v0.2 가 시도한 두 접근 모두 실패**:

#### 시도 1 — `JsonSerializer.Populate(reader, target)` (in-place mutation)
- BepInEx 로그: `Populate succeeded on HeroData` (예외 없이 통과)
- 진단: `pre/post` stat snapshot 비교 → **변경 0건** (silent no-op)
- 가설: HeroData 의 setter (`set_X`) 들이 IL2CPP 빌드에서 strip 됨 (게임 자체가 setter 안 사용, [Serializable] 콜백만 사용). Newtonsoft reflection 이 strip 된 method 를 호출하려 하면 unstrip 또는 silent skip.

#### 시도 2 — `JsonConvert.DeserializeObject` + `HeroList[0]` reference swap
- 새 HeroData 인스턴스 생성 (`Il2CppSystem.Object` → wrapper class IntPtr ctor 로 wrap)
- `HeroList[0] = newHero` reflection setter 호출 → 작동
- 보존 필드 (force / location / relations) 는 현재 player JSON 에서 머지 후 deserialize
- **결과**: 부분 작동 — 캐릭터 본질 (이름 / 스탯 / 천부) 은 swap 됨
- **그러나 broken**:
  - 착용 장비 (`nowEquipment`) 복사 안됨 — game 의 ItemData 객체와 ID-link 못 함
  - 착용 무공 동상 — `equiped` flag 의 reference 해석 실패
  - 포트레이트 무너짐 — sprite asset reference 가 lazy-load. 새 HeroData 가 그 trigger 못 함
  - 문파 정보 보존 fail — MergePreservedFields 의 검증 부족
  - **save → reload 후 player 정보창 안 열림** — 일부 필드 inconsistent 로 NRE

#### v0.3 의 새 접근 — PinpointPatcher 패턴

**핵심 통찰**: HeroData 는 [Serializable] POCO 가 아니라 game-state graph 의 노드. 단순 JSON round-trip 으로는 reference link 복원 불가.

**제안 흐름** (`PinpointPatcher.RefreshAfterApply` 의 본질):
1. 슬롯 JSON 에서 simple-value 필드들 (heroName, age, fightScore, hp, maxhp, fame, heroTagPoint, baseAttri, totalAttri 등) 만 추출
2. 각 필드별로 **game 자체 method** (`SetX`, `ChangeX`, `AddX`, `RefreshX`) 호출
   - 예: hp 변경 → `player.hp = N` (setter strip) 대신 `player.ChangeHp(N - currentHp)` 또는 비슷한 game-internal method
3. ID-list 필드 (kungfuSkills, itemListData.allItem, selfStorage.allItem, heroTagData) 는 **slot 의 ID 들로 재구축**:
   - 현재 player 의 list clear (game-internal method)
   - slot JSON 의 각 entry 마다 `player.AddKungfuSkill(skillID, lv, ...)` 같은 method 호출
4. 보존 필드 (force / location / relations) 는 **건드리지 않음** — 현재 player 객체에 있는 그대로 남김
5. UI / cache invalidate — game-internal `RefreshXxx` method 호출

**가장 큰 미지수**: HeroData 의 game-self method 가 어떤 시그니처인지. Reverse-engineering 필요. LongYin InGame Cheat 의 Harmony patch list (BepInEx 로그) 에 일부 단서:
- `HeroData.RefreshMaxAttriAndSkill`
- `HeroData.GetMaxAttri / GetMaxFightSkill / GetMaxLivingSkill`
- `HeroData.AddSkillFightExp / AddSkillBookExp / BattleChangeSkillFightExp`
- `HeroData.ChangeLivingSkillExp / ChangeFavor / GetMaxFavor`
- `HeroData.GetFinalTravelSpeed`

이 method 들 외에도 더 있을 것. v0.3 시작 시 `Assembly-CSharp.dll` 의 HeroData 클래스 method 전체 목록 dump 하는 것이 첫 단계.

### 4.5 Time.timeScale = 0 만으로는 mouse input 차단 안 됨

**확인된 사실** (S0/S1 검증):
- `Time.timeScale = 0` 은 캐릭터 / NPC / 시간 진행은 멈추지만 **마우스 driven UI / 마을 건물 클릭은 정상 작동** (그 핸들러들이 timeScale 무시).
- 진짜 차단하려면 **Harmony Prefix on `UnityEngine.Input.GetMouseButton{Down,Up,}` + `Input.GetAxis("Mouse ScrollWheel")`** — `ModWindow.ShouldBlockMouse` 가 true 일 때 `__result = false / 0` 반환 + skip-original.

**LongYin InGame Cheat 도 같은 method patch** 함. Harmony 는 multiple prefix 를 모두 호출하고 어느 prefix 가 false 반환하면 short-circuit. priority 설정 불필요.

### 4.6 게임 내부 구조 (Assembly-CSharp.dll 조사 + 검증 결과)

- **플레이어 영웅 타입**: `HeroData` (전역 namespace, [Serializable])
- **싱글톤 매니저**: `GameDataController.Instance` — reflection 정상 접근 ✓
  - `.gameSaveData.HeroList` → `Il2CppSystem.Collections.Generic.List<HeroData>` (899개 영웅)
  - `.Save(int saveID)` / `.Load(int saveID)` — escape hatch 후보. v0.3 Apply 시도 시 Populate / swap 둘 다 실패하면 마지막 fallback (단점: 전체 game state reload, 다른 영웅 + 시간 + 위치 다 영향)
- **플레이어 식별**: `HeroList[0]` 의 `heroID == 0` (검증 완료)
- **HeroData 콜백**: `OnSerializingMethod`, `OnDeserializedMethod` — 우리 직렬화 경로에서 자동 발화

---

## 5. 검증된 것 / 검증 안 된 것

### ✅ 게임 안에서 검증 완료 (v0.1.0 + v0.2.0 + v0.3.0 + v0.4.0)
- BepInEx 가 우리 플러그인 정상 로드 (`Loaded LongYin Roster Mod v0.3.0` / v0.4.0)
- F11 핫키, 창 드래그, 위치 영속, 한글 텍스트 정상
- **40 unit tests all pass** (v0.4 추가: ApplySelection / Capabilities / IdentityFieldMatrix / legacy 호환)
- **라이브 캡처**: `[+]` → 슬롯 1 에 503KB JSON + 토스트
- **Slot list / Slot detail panel**: 갱신 + 캐릭터 정보 정상 표시
- **같은 슬롯 덮어쓰기**: ConfirmDialog → 취소/덮어쓰기 동작
- **슬롯 Rename / Comment / Delete**: InputDialog / ConfirmDialog 통합
- **FileImport** (v0.2): `[F] 파일에서` → SaveSlot list → import → `_meta.captureSource = "file"`
- **Mouse / Wheel input gating** (v0.2): 모드 창 / 다이얼로그 영역 안 클릭 / 스크롤 차단
- **Apply (slot → game)** (v0.3): `▼ 현재 플레이어로 덮어쓰기` → SimpleFieldMatrix +
  heroTagData rebuild + RefreshSelfState/RefreshExternalManagers 7-step pipeline (smoke C1 PASS)
- **Restore (slot 0 → game)** (v0.3): `↶ 복원` → Apply 직전 상태 복귀 (smoke C2 PASS)
- **자동백업** (v0.3): Apply 직전 슬롯 0 자동백업 + 실패 시 자동복원 (AttemptAutoRestore)
- **save → reload 후 정보창 정상** (v0.3): G1/G2/G3 통과 (v0.2 시도 2 의 NRE 실패점 통과)
- **보존 필드** (v0.3): force / location / relations 변경 안 됨 — 사회적 위치 유지
- **9-카테고리 체크박스 default 표시** (v0.4): 슬롯 선택 시 9개 체크박스 인라인 표시 (smoke D15 PASS)
- **Toggle 즉시 저장** (v0.4): 체크박스 토글 → `_meta.applySelection` 즉시 파일 저장
- **정체성 Apply** (v0.4): heroName / nickname / age 등 9 필드 setter direct Apply → save → reload PASS
- **천부 17/17** (v0.4): heroTagData JSON schema 정정 후 천부 17/17 Apply 정상 (eaf2938 fix)
- **Restore / RestoreAll** (v0.4): 선택 카테고리 Restore + 전체 Restore 정상 동작
- **disabled UI** (v0.4): 미지원 카테고리 (무공 active / 인벤토리 / 창고) UI 비활성화 표시
- **legacy 호환** (v0.4): v0.2/v0.3 슬롯 파일 무손실 — V03Default 자동 적용, 파일 건드리지 않음

### ⚪ v0.5+ 후보 (현재 미지원, deferred)
- **무공 active** — wrapper.lv vs nowActiveSkill ID semantic mismatch (PoC A3 FAIL). v0.5+ 에서 재조사
- **인벤토리 / 창고** — sub-data wrapper graph 미해결 (PoC A4 FAIL). v0.5+ 에서 게임 내부 Add method 추가 dump 필요
- **무공 list** — KungfuSkillLvData wrapper ctor 의 IL2CPP 한계. v0.5+ 후보
- **외형** (faceData / portraitID 등) — sprite reference lazy-load. v0.5+ 후보
- spec §12 v0.4 진행 상태 + deferred list 참고

### ⚠ 알려진 한계
- **`PauseGameWhileOpen = true`** — 캐릭터/NPC/시간은 멈추지만 일부 UI 트랜지션은 통과. Mouse
  Harmony patch 로 보완.
- **HeroData setter reflection** — 일반적인 Newtonsoft / reflection-driven property set 가
  silent no-op. v0.3/v0.4 는 game-self method / setter direct 로 우회.
- **무공 active / 인벤토리 / 창고** — v0.4 에서 UI 는 존재하지만 Apply 시 건너뜀 (disabled). v0.5+ 후보.

---

## 6. 다음 세션 — v0.5+ 후보 또는 maintenance 모드

v0.4.0 출시 완료. 9-카테고리 체크박스 UI + 정체성 활성화 + 부상/충성/호감 영구 보존 회귀 검증 완료.
다음 세션은 다음 중 하나:

### 6.A v0.5 — 미해결 PoC 항목 재도전

각 항목은 v0.4 PoC 에서 실패 원인이 확인된 상태. 추가 dump / 우회 전략 필요:

#### 6.A.1 무공 list (kungfuSkills)
- 현재: `KungfuSkillLvData` wrapper ctor 의 IL2CPP 한계
- 접근: game 자체 `AddKungfuSkill` 시그니처 deeper dump. IL2CppListOps.Add ctor 후보 enumerate

#### 6.A.2 무공 active semantic 재조사
- 현재: PoC A3 FAIL — wrapper.lv vs nowActiveSkill ID mismatch (semantic mismatch)
- 접근: nowActiveSkill 의 실제 ID 매핑 방식 재조사. HeroDataDump round 2 필요

#### 6.A.3 인벤토리 / 창고 (itemListData / selfStorage)
- 현재: PoC A4 FAIL — sub-data wrapper graph 미해결
- 접근: game 자체 `GetItem` / `AddItem` method 후보 enumerate. ItemData wrapper ctor 경로 재시도

#### 6.A.4 외형 (faceData / portraitID)
- 현재: sprite reference lazy-load
- 접근: game-self `RefreshPortrait()` 또는 sprite cache invalidate method 탐색

### 6.B maintenance 모드

v0.4.0 GitHub release 후 모드 maintenance 모드. 게임 패치 (v1.0.0 f8.3+) 시 재검증 + breakage fix.

### 6.C 첫 작업

**v0.4.0 release packaging (D18) 가 먼저**:
- VERSION 파일 → `0.4.0`
- `dist/LongYinRoster_v0.4.0.zip` 생성
- `git tag v0.4.0` + push
- `gh release create v0.4.0 ...` + 게임-load verify (사용자 게이트)

---

## 7. 다음 세션을 위한 컨텍스트 압축본

**다음 세션 첫 메시지에 붙여넣을 요약**:

> LongYin Roster Mod **v0.4.0 출시 완료** (9-카테고리 체크박스 UI + 정체성 활성화 + 부상/충성/호감 영구 보존 회귀).
> 프로젝트 루트:
> `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`. 핸드오프 문서:
> `docs/HANDOFF.md`, spec: `docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md`.
>
> v0.4 scope: v0.3 stat-backup + **정체성 9 필드 (setter direct)** + **카테고리 단위 선택 Apply**
> (`_meta.applySelection` 슬롯별 저장). 부상/충성/호감 보존 필드 전환. smoke D15 PASS, 40/40 tests PASS.
>
> **다음 단계 후보**:
> - **D18** (release packaging): VERSION → 0.4.0 + dist zip + `git tag v0.4.0` + GitHub release.
>   사용자 게이트 = `gh release create` + 게임-load verify.
> - **v0.5** (deferred 재도전): 무공 list / 외형 / 인벤토리 sub-data wrapper graph /
>   무공 active semantic 재조사. spec §12 v0.4 진행 상태 + HANDOFF §6.A 참고.
> - **maintenance 모드**: v0.4.0 GitHub release 후 게임 패치 대응.

---

## 8. 빠른 명령어 모음

```bash
# 게임 닫고 빌드
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release

# 테스트
DOTNET_CLI_UI_LANGUAGE=en dotnet test

# 게임 닫혔는지 확인
tasklist | grep -i LongYinLiZhiZhuan

# 깃 풀 (다른 머신에서 작업 시)
git pull origin main

# BepInEx 로그 클리어 + 추적 (검증 사이클)
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
grep -n "HeroLocator\|toast\|Capture\|slot \|HeroData\." "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"

# 슬롯 디렉터리 확인 + 깨끗하게 시작
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/"
rm -f "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/"slot_*.json

# 슬롯 메타 빠른 확인
python -c "import json; d=json.load(open('E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/slot_01.json', encoding='utf-8-sig')); print(json.dumps(d['_meta']['summary'], ensure_ascii=False, indent=2))"

# v0.x release 패키징 (PowerShell)
# Compress-Archive -Path "dist/LongYinRoster_v0.x.0/*" -DestinationPath "dist/LongYinRoster_v0.x.0.zip" -Force

# GitHub release (gh CLI)
# gh release create v0.x.0 dist/LongYinRoster_v0.x.0.zip --title "..." --notes-file release-notes.md
```
