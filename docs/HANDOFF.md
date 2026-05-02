# LongYin Roster Mod — 작업 핸드오프 문서

**일시 중지**: 2026-05-02
**진행 상태**: **v0.7.0 release** — F11 진입 메뉴 (캐릭터 관리 / 컨테이너 관리) + 컨테이너 기능 (인벤토리 / 창고 ↔ 외부 디스크 컨테이너 이동·복사·삭제 + 카테고리 필터). 통합 UI overhaul (커스텀 헤더 + 일관된 X 닫기 버튼 + transparency 통일).
**저장소**: https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode (`main` 브랜치)
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`
**Releases**:
- [v0.1.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.1.0) — Live capture + slot management
- [v0.2.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.2.0) — Import from save + input gating
- [v0.3.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.3.0) — Apply (stat-backup) + Restore + save/reload 안전성
- [v0.4.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.4.0) — 9-카테고리 체크박스 UI + 정체성 활성화 + 부상/충성/호감 영구 보존 회귀
- (v0.5.0 — release 안 함 — 양쪽 PoC FAIL, dumps/2026-05-01-* 참고)
- [v0.5.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.1) — 무공 active 활성화 (kungfuSkills.equiped + game 패턴 11-swap + UI cache invalidate + save persistence)
- [v0.5.2](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.2) — 무공 list 활성화 (LoseAllSkill clear + ctor(int) wrapper + GetSkill add 2-pass + SlotFile JSON 직렬화 fix)
- [v0.5.3](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.3) — 인벤토리 (ItemList) Replace 활성화 (LoseAllItem + ItemData(ItemType) ctor + GetItem add + Probe cache invalidate)
- [v0.5.4](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.4) — 인벤토리 subData 풀 복원 (filter fix + generic JSON→IL2CPP wrapper deep-copy with Dictionary handling)
- [v0.5.5](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.5) — 창고 (selfStorage) Replace 활성화 (직접 list manipulation + ItemListApplier deep-copy 재사용)
- [v0.6.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.0) — 장비 슬롯 (무기 / 갑옷 / 투구 / 신발 / 장신구×2 / 말 / 마구) Replace 활성화 (EquipItem + EquipHorse game-self method + identity-based matching)
- [v0.6.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.1) — 외형 (faceData / skinColorDark / voicePitch) 활성화 + 모든 카테고리 동시 Apply 시 stat override 회귀 fix (SetSimpleFields → RefreshSelfState 이후 이동)
- [v0.6.2](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.2) — 무공 돌파속성 (extraAddData / speEquipData / speUseData / equipUseSpeAddValue / damageUseSpeAddValue / belongHeroID 등) 풀 복원 + Stat snapshot/restore (Stat unchecked 시 부수효과 보호)
- [v0.6.3](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.3) — 2D nested list (treasure.playerGuessTreasureLv 등 List<List<int>>) 풀 복원. ApplyJsonArray 가 nested element type 의 inner list 인스턴스 신규 생성 후 recurse.
- [v0.6.4](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.4) — partPosture (체형 등 외형 sub-data) 복원. SerializerService 가 player.partPosture.partPosture (List<float>) 의 값을 `_partPostureFloats` 배열로 JSON inject + AppearanceApplier 가 reflection clear+add. JsonConvert 가 IL2CPP wrapper 제외하는 issue 우회.
- [v0.7.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.0) — F11 진입 메뉴 (캐릭터 관리 / 컨테이너 관리) + 컨테이너 기능 (인벤토리/창고 ↔ 외부 디스크 컨테이너 이동·복사·삭제). 통합 UI overhaul (커스텀 thicker 헤더 + 흰색 bold 제목 + 일관된 X 닫기 버튼 + 일관된 transparency).

## v0.7.0 Known Limitations
- **무공 list만 단독 Apply 시 active 장착 정보 손실** (의도된 동작 — 무공 active 도 같이 체크 권장).
- **Stat 미체크 시 일부 max/derived 값 부정확** (game update loop 한계 — best-effort).
- **컨테이너 → 게임 인벤토리 가득 참 시 부분 처리**: 처리 가능 갯수만 추가, 실패 항목은 컨테이너에 남김 + 토스트 알림.
- **Item 상세 정보 / 아이콘 그리드 / 정렬 / 검색 / 설정 panel** 은 v0.7.x+ 후속 (NPC 지원 / Slot diff / Apply preview 와 함께 후속 sub-project).

---

## 1. 한 줄 요약

BepInEx 6 IL2CPP 환경에서 플레이어 캐릭터 스냅샷을 20슬롯에 저장 / 관리하는 모드.
**현재 main baseline = v0.7.0** (F11 진입 메뉴 + 캐릭터 관리 (13-카테고리 체크박스) + 컨테이너 관리 (인벤토리/창고 ↔ 외부 디스크)).

**다음 세션 후속 sub-project** (모두 v0.7.0 의 ModeSelector 진입점 활용):
- v0.7.1: NPC 지원 — 캐릭터 선택 + apply target 확장 (heroID=0 외 다른 캐릭터)
- v0.7.2: Slot diff preview — Apply 전 어떤 필드가 바뀔지 미리보기 (스탯/장비/무공 차이 시각화)
- v0.7.3: Apply 부분 미리보기 — 선택한 카테고리 적용 시 전후 비교
- v0.7.4: 컨테이너 UX 개선 — Item 상세 정보 panel / 아이콘 그리드 / 검색·정렬
- v0.7.5+: 설정 panel — hotkey 변경 / 컨테이너 정원 / 창 크기 조정

각 sub-project 는 별도 brainstorming → spec → plan → impl cycle. 진입점은 ModeSelector 메뉴에 항목 추가.

**v0.5 PoC dual-track (외형 + 무공 active) — 양쪽 FAIL → release 안 함**:
- 외형 (G1 FAIL): `portraitID` field 부재. 진짜 외형 = `faceData / partPosture` sub-data wrapper graph (v0.4 ItemData 와 동일 패턴) → v0.6 통합 작업으로 deferred.
- active (G3 보수적 FAIL): Method path 발견 + read-back PASS, 그러나 게임 UI 미반영 + save→reload persistence 미검증 → v0.4 외형 패턴과 동일 (cache invalidate 별도 필요) → v0.6 통합 작업으로 deferred.

**v0.5 PoC 의 결정적 발견** (v0.6 production 자산):
- `kungfuSkills[i].equiped` 가 active 의 source-of-truth (NOT `nowActiveSkill` — v0.4 A3 FAIL 의 진짜 원인)
- `KungfuSkillLvData.skillID` 가 진짜 ID 필드 (NOT `kungfuID`)
- `HeroData.EquipSkill / UnequipSkill (KungfuSkillLvData wrapper, bool=true)` — active set/unset path
- 외형은 `HeroData.faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data graph
- `HeroIconDirty / heroIconDirtyCount` (HeroData), `skillIconDirty / maxManaChanged` (KungfuSkillLvData) — UI refresh trigger 후보
- 자세한 evidence: `docs/superpowers/dumps/2026-05-01-*` 5 개 markdown

**v0.5 main 영향**: 양쪽 FAIL → release tag / dist zip / VERSION bump 안 함. **Foundation 변경은 보존** (`Capabilities.Appearance` flag, `FieldCategory.Appearance` enum, `ApplySelection.Appearance` flag, `KoreanStrings.Cat_Appearance`) — v0.6 production 의 prerequisite.

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

### 6.A v0.5 PoC report — 양쪽 FAIL (2026-05-01)

v0.5 PoC dual-track 결과 양쪽 FAIL — release 안 함. PoC artifact 와 결정적 발견 보존:

#### 6.A.1 외형 PoC — G1 FAIL
- 가설: `portraitID` (int) + `gender` (int) setter direct + sprite cache invalidate
- 결과: HeroData 에 `portraitID / gender` field 자체가 부재. `gender` 는 `isFemale` (bool, v0.4 Identity 가 처리). 진짜 외형 = `faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data wrapper graph
- v0.4 PoC A4 (ItemData) 와 동일 미해결 패턴
- 후보 zero-arg refresh method 13 개 모두 not found
- evidence: `docs/superpowers/dumps/2026-05-01-portrait-poc-result.md`

#### 6.A.2 active PoC — G3 보수적 FAIL
- Phase A (save-diff) ✅: `kungfuSkills[i].equiped` 가 source-of-truth (NOT `nowActiveSkill` — v0.4 A3 FAIL 의 진짜 원인)
- Phase B (Harmony trace) ✅: `HeroData.EquipSkill / UnequipSkill (KungfuSkillLvData wrapper, bool=true)` method path 발견. 11-슬롯 array swap 패턴 (게임 자체 호출 시 11회 Unequip + 11회 Equip)
- Phase C (in-memory) 🟡: read-back 데이터 layer 변경 PASS, 그러나 게임 UI 미반영 + save→reload persistence 미검증 — v0.4 외형 PoC 와 동일 패턴 (cache invalidate 별도 필요)
- evidence: `docs/superpowers/dumps/2026-05-01-active-kungfu-{diff,trace,poc-result}.md`

#### 6.A.3 v0.5 결정적 발견 — v0.6 production 자산
- KungfuSkillLvData wrapper shape: `equiped (bool)`, `skillID (int)` ← **진짜 ID 필드**, `lv`, `fightExp`, `bookExp`, `belongHeroID`, `speEquipData / speUseData / extraAddData (HeroSpeAddData)`, `cdTimeLeft / activeTimeLeft / power / battleDamageCount`, `skillIconDirty / maxManaChanged` flags
- HeroData 외형 영역: `faceData / partPosture / skinID / skinLv / defaultSkinID / setSkinID / playerSetSkin / skinColorDark / HeroIconDirty / heroIconDirtyCount`
- 외형 method 후보: `SetSkin(int, int)`, `SetSkeletonGraphicFaceSlot(SkeletonGraphic, int, int)`, `SetSkeletonGraphicSkinColor(SkeletonGraphic)`
- 통합 report: `docs/superpowers/dumps/2026-05-01-v0.5-poc-report.md`

### 6.B v0.6 — 통합 작업 후보 (sub-data wrapper graph + UI cache invalidation)

v0.5 양쪽 FAIL + v0.4 PoC A4 (ItemData) 의 패턴이 모두 동일: **IL2CPP sub-data wrapper graph + game UI refresh path 미해결**. v0.6 에서 통합 해결 권장:

#### 6.B.1 sub-data wrapper graph 통합
- **외형**: `HeroFaceData` + `PartPostureData` wrapper 처리
- **인벤토리**: `ItemData[]` (itemListData.allItem, 171 entries) wrapper graph
- **창고**: `ItemData[]` (selfStorage.allItem, 217 entries) 동상
- 공통 challenge: IL2CPP wrapper 의 ctor / factory / Add method 발견

#### 6.B.2 active full integration (v0.5 발견 활용)
- v0.5 의 method path (`EquipSkill / UnequipSkill`) + skillID 매칭 production code
- UI refresh path 발견 — `KungfuSkillLvData.skillIconDirty / maxManaChanged` flag toggle, 또는 game-self `RefreshFightSkillUI / RefreshKungfuPanel` 류 method
- save→reload persistence 검증

#### 6.B.3 무공 list (kungfuSkills)
- v0.5 발견된 wrapper shape (skillID, lv 등) 활용
- ctor / factory / Add method 발견
- v0.4 PoC A1 의 KungfuSkillLvData wrapper IL2CPP 한계

#### 6.B.4 UI cache invalidation 일반화
- 외형 / active 둘 다 동일 challenge — game UI 의 sprite/widget cache invalidate trigger 발견
- 후보: `HeroIconDirty` flag, `RefreshSelfState` (이미 v0.3 사용), Harmony trace round 2 (Equip → 후속 cascading method)

### 6.C maintenance 모드

v0.4.0 main baseline 유지. 게임 패치 (v1.0.0 f8.3+) 시 재검증 + breakage fix. v0.6 통합 작업은 별도 spec → plan → implementation 사이클로.

### 6.D 다음 세션 첫 작업

선택지:
1. **v0.6 spec 작성** — sub-data wrapper graph + UI cache invalidation 통합 (큰 scope, 새 brainstorm 권장)
2. **maintenance 모드** — 게임 패치 대응 대기
3. **v0.5 foundation 일부 revert 결정** — Capabilities.Appearance / FieldCategory.Appearance 를 v0.6 에서 활용 vs 일시 제거 (현재 false default 로 무해, 보존 권장)

---

## 7. 다음 세션을 위한 컨텍스트 압축본

**다음 세션 첫 메시지에 붙여넣을 요약**:

> LongYin Roster Mod — **main baseline = v0.4.0**. **v0.5 PoC 양쪽 FAIL — release 안 함** (2026-05-01).
> 프로젝트 루트: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`.
> 핸드오프: `docs/HANDOFF.md`. v0.5 spec: `docs/superpowers/specs/2026-05-01-longyin-roster-mod-v0.5-design.md`.
> v0.5 PoC artifact: `docs/superpowers/dumps/2026-05-01-*` (5 markdowns).
>
> **v0.5 결과**: 외형 G1 FAIL (`portraitID` 부재, sub-data graph), active G3 보수적 FAIL (method path 발견 + read-back PASS but UI 미반영 + save→reload 미검증). 양쪽 모두 v0.4 외형/ItemData 와 동일 패턴 — **IL2CPP sub-data wrapper graph + UI cache invalidate** 미해결.
>
> **v0.5 결정적 발견** (v0.6 자산):
> - `kungfuSkills[i].equiped` 가 active source (NOT `nowActiveSkill`)
> - `KungfuSkillLvData.skillID` 가 진짜 ID 필드 (NOT `kungfuID`)
> - `HeroData.EquipSkill / UnequipSkill (wrapper, bool=true)` method path
> - 외형 = `faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data wrapper graph
> - UI refresh trigger 후보: `HeroIconDirty / heroIconDirtyCount / skillIconDirty / maxManaChanged`
>
> **v0.5 main 상태**: foundation (Capabilities.Appearance / FieldCategory.Appearance / ApplySelection.Appearance / KoreanStrings.Cat_Appearance) 유지. Probe 코드 cleanup 완료. 45/45 tests PASS.
>
> **다음 단계 후보**:
> - **v0.6 spec** — sub-data wrapper graph + UI cache invalidation 통합 작업 (외형 + active full + 인벤토리 + 무공 list). 새 brainstorm 권장.
> - **maintenance 모드** — 게임 패치 (v1.0.0 f8.3+) 대응 대기.

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
