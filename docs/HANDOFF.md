# LongYin Roster Mod — 작업 핸드오프 문서

**일시 중지**: 2026-04-28
**진행 상태**: **v0.2.0 출시 완료** (tag `v0.2.0`, commits `17140aa` `2286366` `8c89fe4`).
다음 세션은 **v0.3 의 핵심 — Apply (slot → game) 흐름 PinpointPatcher 패턴 재설계**.
**저장소**: https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode (`main` 브랜치)
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`
**Releases**:
- [v0.1.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.1.0) — Live capture + slot management
- [v0.2.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.2.0) — Import from save + input gating

---

## 1. 한 줄 요약

BepInEx 6 IL2CPP 환경에서 플레이어 캐릭터 스냅샷을 20슬롯에 저장 / 관리하는 모드.
**Capture / FileImport / Slot 관리 / Input gating 모두 검증 완료. v0.3 의 Apply (slot → game)
흐름은 IL2CPP-bound `JsonSerializer.Populate` 가 silent no-op 이라는 한계 + `HeroList`
swap 시 reference 필드 (장비/무공/포트레이트/문파) link 깨지는 문제 때문에 v0.2 에서 제외.
PinpointPatcher 패턴 (게임 자체 setter method 호출) 으로 재설계 필요**.

---

## 2. 현재 깃 히스토리 (origin/main 동기화 직후)

```
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

**Tags**: `v0.1.0` (at `473763d`), `v0.2.0` (at `8c89fe4`).

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

### ✅ 게임 안에서 검증 완료 (v0.1.0 + v0.2.0)
- BepInEx 가 우리 플러그인 정상 로드 (`Loaded LongYin Roster Mod v0.1.0`)
- F11 핫키, 창 드래그, 위치 영속, 한글 텍스트 정상
- 18 unit tests all pass
- **라이브 캡처**: `[+]` → 슬롯 1 에 503KB JSON + 토스트 (Task 17)
- **Slot list / Slot detail panel**: 갱신 + 캐릭터 정보 정상 표시
- **같은 슬롯 덮어쓰기**: ConfirmDialog → 취소/덮어쓰기 동작
- **슬롯 Rename / Comment / Delete**: InputDialog / ConfirmDialog 통합 (Task 18.5 → C-4 + D)
- **FileImport** (v0.2): `[F] 파일에서` → SaveSlot list → import → `_meta.captureSource = "file"` (Task 21)
- **Mouse / Wheel input gating** (v0.2): 모드 창 / 다이얼로그 영역 안 클릭 / 스크롤 차단

### ❌ Apply (slot → game) 시도 결과 — v0.2 에서 폐기 / v0.3 재설계
- **Populate**: silent no-op (HeroData setter strip 의심)
- **HeroList swap**: 부분 작동, 그러나 reference 필드 (장비/무공/포트레이트/문파) 깨짐 + save→reload 후 정보창 NRE
- **Restore from slot 0**: Apply 와 같은 경로라 동상 미지원
- 디테일 패널의 `▼ 현재 플레이어로 덮어쓰기` / `복원` 버튼은 `(v0.2 예정)` → `(v0.3 예정)` 라벨로 disabled 표시

### ⚠ 부분 작동 / 알려진 한계
- **`PauseGameWhileOpen = true`** — 캐릭터/NPC/시간은 멈추지만 일부 UI 트랜지션은 통과 (S0). Mouse Harmony patch 로 보완 (S1).
- **HeroData setter reflection** — 일반적인 Newtonsoft / reflection-driven property set 가 silent no-op. v0.3 의 핵심 한계.

---

## 6. 다음 세션 시작 시 — v0.3 Plan

### 6.1 첫 작업: HeroData method dump

게임 안에서 reflection 으로 `HeroData` 클래스의 모든 public method (instance + static) 를 enumerate 하고 BepInEx 로그에 dump. v0.3 Apply 의 PinpointPatcher 가 호출할 method 후보 식별이 첫 단계.

**위치**: 임시로 `ModWindow.Awake` 또는 별도 `[F12]` 핸들러로 추가:
```csharp
var heroType = Core.HeroLocator.GetPlayer()?.GetType();
if (heroType != null) {
    foreach (var m in heroType.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
        Logger.Info($"HeroData.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}) -> {m.ReturnType.Name}");
    }
}
```

dump 결과를 `docs/HeroData-methods.md` 에 저장 → 어떤 method 가 어떤 simple-value field 를 set 하는지 매핑.

### 6.2 PinpointPatcher 본격 구현

**제안 파일 구조**:
```
Core/PinpointPatcher.cs
  - public static void Apply(string slotPlayerJson, object currentPlayer)
    1. Parse slot JSON (System.Text.Json) → JsonElement
    2. SetSimpleFields(player, slotJson, ExcludedFields) — 각 simple-value field
    3. RebuildKungfuSkills(player, slotJson)              — list 재구축
    4. RebuildItemList(player, slotJson)                  — itemListData.allItem
    5. RebuildSelfStorage(player, slotJson)               — selfStorage.allItem
    6. RebuildHeroTagData(player, slotJson)               — 천부
    7. RefreshAll(player) — game-internal RefreshXxx 호출
```

각 SetX / RebuildX 는 game-self method 호출. 일부 method 는 strip 됐을 수 있으므로 catch 가드 + Logger.Warn.

### 6.3 Apply 흐름 재와이어

`b3e300d` 에서 제거한 Apply 코드를 PinpointPatcher 사용 버전으로 다시 작성:
- `ModWindow._detail.OnApplyRequested = RequestApply`
- `RequestApply` — ConfirmDialog (이미 있음)
- `DoApply`:
  - 자동백업 슬롯 0 (이미 있음)
  - SlotFile.Read → payload.Player (raw JSON)
  - PortabilityFilter.StripForApply — 보존 필드 제거 (이미 있음)
  - **`Core.PinpointPatcher.Apply(stripped, player)`** — 새 함수
  - Repo.Reload + 토스트
- `SlotDetailPanel.cs` — `(v0.2 예정)` 텍스트 제거, ApplyBtn 정상 활성

### 6.4 Restore from slot 0

Apply 와 같은 PinpointPatcher 호출. `SlotDetailPanel.OnRestoreRequested = RequestRestore`.

### 6.5 v0.3 검증

- 캡처 → 명확한 변경 (무공 학습, hp 감소 등) → Apply → 변경 전 상태로 복귀 확인
- 자동백업 슬롯 0 에서 Restore → Apply 직전 상태로 복귀 확인
- Save → Reload → 정보창 정상 (이전 v0.2 시도에서 깨졌던 부분)
- 장비 / 포트레이트 / 문파 정상

### 6.6 v0.3 release

- README 업데이트 (Apply / Restore 활성)
- HANDOFF 갱신
- tag v0.3.0 + GitHub release + zip

---

## 7. 다음 세션을 위한 컨텍스트 압축본

**다음 세션 첫 메시지에 붙여넣을 요약**:

> LongYin Roster Mod v0.2.0 출시 완료
> ([release](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.2.0)).
> 프로젝트 루트는
> `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`. 핸드오프
> 문서: `docs/HANDOFF.md`, 플랜: `docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md`.
>
> 다음 단계 — **v0.3 의 핵심: Apply (slot → game) 흐름 PinpointPatcher 패턴 재설계**.
> 이전 두 접근 (`JsonSerializer.Populate`, `HeroList` swap) 모두 실패. HANDOFF §4.4
> 참고. 첫 작업은 HeroData method dump (§6.1) — game-self setter / mutator 식별 후
> field 별 매핑.

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
