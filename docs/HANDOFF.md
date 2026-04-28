# LongYin Roster Mod — 작업 핸드오프 문서

**일시 중지**: 2026-04-28
**진행 상태**: Task 17 라이브 캡처 게임 안 검증 완료 + 같은 슬롯 덮어쓰기 흐름 추가 (commits `59e3be2`, `8d115d4`)
**저장소**: https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode (`main` 브랜치)
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`

---

## 1. 한 줄 요약

BepInEx 6 IL2CPP 환경에서 플레이어 캐릭터 스냅샷을 20슬롯에 저장/복원하는 모드.
**라이브 캡처 + 같은 슬롯 덮어쓰기 검증 완료. Task 18 (apply, slot → game) 부터 게임 안 검증 필요**.

---

## 2. 현재 깃 히스토리 (origin/main 동기화 직후 +2 commit)

```
8d115d4 refactor(slots+ui): System.Text.Json migration + same-slot overwrite   ← NEW
59e3be2 fix(core): HeroLocator IL2CPP-compatible iteration                     ← NEW
601602c docs: handoff document — work paused after Task 17                     ← previous handoff
ca6d630 feat(capture): wire live capture — HeroLocator + SerializerService real impl   ← Task 17
266fca4 feat(ui): SlotListPanel + SlotDetailPanel with selection + action callbacks    ← Task 16
a780d98 feat(ui): ModWindow IMGUI shell with F11 toggle, drag, position persistence    ← Task 15
f04b805 feat(ui): ToastService with 3s auto-dismiss                                    ← Task 14
428f2b0 feat(config): bind BepInEx ConfigEntries                                       ← Task 13
f507e78 feat(core): PinpointPatcher skeleton with debug logging                        ← Task 12
7588833 feat(core): HeroLocator finds heroID=0 player via reflection                   ← Task 11 (rewritten in Task 17, then again in 59e3be2)
8b9eebd feat(core): SerializerService stub                                             ← Task 10 (rewritten in Task 17)
dc01499 fix(slots): bump ParseHeader default to 512KB
42c045f feat(slots): SaveFileScanner header parsing                                    ← Task 9
16c38ea feat(slots): SlotRepository with slot 0 auto-backup guard                      ← Task 8
3301432 feat(slots): atomic SlotFile I/O                                               ← Task 7 (Read/Write rewritten via System.Text.Json in 8d115d4)
ea18a73 feat(slots): SlotMetadata + SlotPayload                                        ← Task 6 (rewritten in 8d115d4)
850fc55 feat(core): PortabilityFilter strips faction + runtime fields                  ← Task 5
87d9b55 feat(util): KoreanStrings + PathProvider                                       ← Task 4
71614ca test: test project skeleton with frozen Hero fixture (35MB)                    ← Task 3
573738e refactor(plugin): Log → Logger; gate deploy target to Release
9b8e85e feat(plugin): bootstrap BepInEx plugin loading and logging                     ← Task 2
e45b164 fix(build): allow GameDir env-var override
bb17569 chore: initialize solution and shared build props                              ← Task 1
```

---

## 3. 프로젝트 구조 (현재 디스크 상태)

```
_PlayerExport/
├── LongYinRoster.sln
├── Directory.Build.props      ← GameDir env-var fallback
├── .gitignore                 ← .superpowers/ + .omc/ 제외
├── docs/
│   ├── HANDOFF.md             ← 이 파일
│   └── superpowers/
│       ├── specs/2026-04-27-longyin-roster-mod-design.md   (~600줄)
│       └── plans/2026-04-27-longyin-roster-mod-plan.md     (~3000줄)
└── src/
    ├── LongYinRoster/                       ← 메인 플러그인
    │   ├── LongYinRoster.csproj
    │   ├── Plugin.cs                        ← BepInPlugin entry
    │   ├── Config.cs                        ← ConfigEntries
    │   ├── Core/
    │   │   ├── HeroLocator.cs               ← reflection Count + indexer (IL2CPP-compatible)
    │   │   ├── SerializerService.cs         ← JsonConvert.SerializeObject + JsonSerializer.Populate
    │   │   ├── PortabilityFilter.cs         ← 24+21=45 필드 strict-personal 제거
    │   │   └── PinpointPatcher.cs           ← v0.1 무동작 스켈레톤
    │   ├── Slots/
    │   │   ├── SlotPayload.cs               ← Player = raw JSON string
    │   │   ├── SlotMetadata.cs              ← System.Text.Json 으로 메타 추출
    │   │   ├── SlotEntry.cs
    │   │   ├── SlotFile.cs                  ← System.Text.Json Read/Write + atomic .tmp→Replace
    │   │   ├── SlotRepository.cs            ← 21슬롯 (0=자동백업 + 1~20)
    │   │   └── SaveFileScanner.cs           ← Save/SaveSlot0~10 헤더 파싱 (512KB 기본)
    │   ├── UI/
    │   │   ├── ModWindow.cs                 ← MonoBehaviour, F11 토글, RequestCapture/DoCapture 분리
    │   │   ├── SlotListPanel.cs             ← 왼쪽 21행
    │   │   ├── SlotDetailPanel.cs           ← 오른쪽 상세 + 액션 버튼
    │   │   ├── ConfirmDialog.cs             ← 재사용 modal (IL2CPP-safe IMGUI)   ← NEW
    │   │   └── ToastService.cs              ← 3초 자동소멸
    │   └── Util/
    │       ├── Logger.cs                    ← BepInEx 로거 래퍼
    │       ├── KoreanStrings.cs             ← UI 문자열 상수 (capture-overwrite 추가)
    │       └── PathProvider.cs              ← <PluginPath> 토큰 + GameSaveDir
    └── LongYinRoster.Tests/                 ← 테스트 (18 tests, all pass)
        ├── LongYinRoster.Tests.csproj
        ├── fixtures/slot3_hero.json         ← 35MB 동결 baseline
        ├── SmokeTests.cs
        ├── PortabilityFilterTests.cs        (4)
        ├── SlotMetadataTests.cs              (1)  ← string 입력으로 변경
        ├── SlotFileTests.cs                  (3)  ← string Player + JsonDocument 검증
        ├── SlotRepositoryTests.cs            (7)
        └── SaveFileScannerTests.cs           (2)
```

**테스트**: `dotnet test` → **18/18 PASS**.
**빌드**: `dotnet build -c Release` → 자동으로 `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 배포.

---

## 4. 가장 중요한 발견 (다음 세션 작업자 필독)

### 4.1 IL2CPP-bound Newtonsoft.Json API gap

`BepInEx/interop/Newtonsoft.Json.dll` 은 표준 NuGet 버전과 **다른 표면 + 다른 type identity** 를 가진다.

| 표준 Newtonsoft | IL2CPP 버전 | 우리 대응 |
|---|---|---|
| `JsonConvert.SerializeObject(obj, settings)` | ❌ 1+settings 오버로드 없음 | `SerializeObject(obj, type, settings)` 또는 `(Il2CppSystem.Object)` 단일 인자 |
| `JsonConvert.PopulateObject(json, obj, settings)` | ❌ **부재** | `JsonSerializer.Create().Populate(JsonTextReader(...), il2target)` |
| `JsonSerializerSettings.NullValueHandling` setter | ❌ 읽기전용 | 사용 안 함 |
| `JsonSerializerSettings.FloatFormatHandling` | ❌ 부재 | 사용 안 함 |
| `JArray` cast (`as JArray` / `(IList<JToken>)`) | ❌ **silently null** — type identity 충돌 | **System.Text.Json 으로 우회** (Slots/, SlotMetadata) |
| `(JObject)root["player"]` cast (Read 경로) | ❌ ArgumentNullException 또는 silently null | 동상 (SlotFile Read) |
| `JObject` foreach (C# duck-typed) | ❌ enumerator 호환 안 됨 | `for (int i = 0; i < arr.Count; i++)` |
| `JsonTextReader` ctor | takes `Il2CppSystem.IO.TextReader` | `new Il2CppSystem.IO.StringReader(json)` |

**SerializerService** 안의 IL2CPP-bound Newtonsoft 사용은 게임 객체 직렬화 1방향만 (Serialize(il2obj) → JSON 문자열). **그 결과 JSON 은 우리 코드 안에서 표준 .NET 라이브러리(System.Text.Json)로만 다룬다**. IL2CPP-bound JObject/JArray 는 코드베이스 어디에서도 더 이상 traverse 하지 않음. SlotPayload.Player 도 raw JSON string.

### 4.2 IL2CPP-bound `Il2CppSystem.Collections.Generic.List<T>` 는 .NET IEnumerable 미구현

`HeroList` (`List<HeroData>`) 가 게임 안에서 `Il2CppSystem.Collections.Generic.List<HeroData>` 로 노출되는데 우리 .NET 의 `System.Collections.IEnumerable` 을 구현하지 않는다. `is IEnumerable` 검사 → false → foreach 불가.

**대응**: reflection 으로 `Count` property + `Item` indexer (또는 `get_Item(int)` method) 호출. HeroLocator 의 핵심 패턴.

또한 `BindingFlags.Public | BindingFlags.Static` 만 사용하면 generic singleton base class 의 `Instance` property 가 derived wrapper class 에서 안 보임. **`BindingFlags.FlattenHierarchy` 필수**. 그리고 IL2CPP wrapper 가 backing field 만 노출하는 경우도 있어 **property → field fallback** + 흔한 별명(`instance`, `_instance`, `s_Instance`, `s_instance`) 시도.

### 4.3 IL2CPP IMGUI strip — 매 frame `Method unstripping failed`

이 게임의 IL2CPP 빌드는 다음 IMGUI API 들을 strip (런타임 미사용 판단):

- `GUILayout.FlexibleSpace()`
- `new GUIStyle(GUIStyle other)` ctor
- `GUILayout.Label(string, GUIStyle, GUILayoutOption[])` overload (그리고 다른 GUIStyle 받는 overload들)

이 method 호출 시 **Il2CppInterop 가 unstripping 실패 → NotSupportedException 매 프레임 throw → IMGUI callback abort → 그 element 이후 모두 누락**.

**대응**: `GUILayout.Space(int)`, `GUILayout.Button(string, GUILayoutOption[])`, `GUILayout.Label(string)` 같이 **default skin + 단순 overload** 만 사용. ConfirmDialog 가 이 패턴 적용. 추가로 callback 시작 시 `GUI.enabled = true` 강제 + try/catch 가드 (logging 폭주 방지 + 새 strip 발견 시 진단 가능).

### 4.4 게임 내부 구조 (Assembly-CSharp.dll 조사 + 게임 안 검증 결과)

- **플레이어 영웅 타입**: `HeroData` (전역 namespace, [Serializable])
- **싱글톤 매니저**: `GameDataController.Instance` — reflection 정상 접근 ✓
  - `.gameSaveData.HeroList` → `Il2CppSystem.Collections.Generic.List<HeroData>` (899개 영웅)
  - `.Save(int saveID)` / `.Load(int saveID)` — escape hatch 후보 (Task 18 fallback)
- **플레이어 식별**: `HeroList[0]` 의 `heroID == 0` (검증 완료 — `matched heroID=0 at index 0`)
- **HeroData 콜백**: `OnSerializingMethod`, `OnDeserializedMethod` — 우리 직렬화 경로에서 자동 발화

---

## 5. 검증된 것 / 검증 안 된 것

### ✅ 게임 안에서 검증 완료
- BepInEx 가 우리 플러그인 정상 로드 (`Loaded LongYin Roster Mod v0.1.0`)
- F11 핫키, 창 드래그, 위치 영속, 한글 텍스트 정상
- 18 unit tests all pass
- **Task 17 라이브 캡처**: `[+]` → 슬롯 1 에 503KB JSON 디스크 저장 + 토스트
- **Slot list / Slot detail panel**: 캡처 후 즉시 갱신, 캐릭터명 / 전투력 358K / 무공 130 (Lv10 119) / 인벤토리 171·창고 217 / 금전 98M냥 / 천부 17 모두 정상 표시
- **같은 슬롯 덮어쓰기**: ConfirmDialog 표시 → 취소 시 변경 없음, 덮어쓰기 시 selected 슬롯 갱신

### ❌ 아직 코드 자체가 없음 (Task 18+)
- `▼ 현재 플레이어로 덮어쓰기` 버튼 (slot → game, Apply) — `OnApplyRequested` 핸들러 미와이어
- 자동백업 트랜잭션 (Apply 직전 슬롯 0 에 현 캐릭터 저장)
- 슬롯 0 복원 (자동백업에서 게임으로 되돌리기)
- 파일에서 캡처 (`SaveFileScanner.LoadHero0` 통합)
- 슬롯 이름변경 / 메모 / 삭제 UI 와이어링 (디테일 패널 핸들러)
- README / 릴리스 패키징

---

## 6. 다음 세션 시작 시 먼저 할 일 (우선순위 순)

### 6.1 Task 18 — 적용(Apply, slot → game) 흐름

플랜 문서 `docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md` Task 18 절 참고. 핵심 단계:

1. **`ModWindow._detail.OnApplyRequested = RequestApply` 와이어링**
2. `RequestApply(int slot)` — ConfirmDialog 표시 (`KoreanStrings.ConfirmTitleApply`, `ConfirmApplyMain`, `ConfirmApplyPolicy`, `Apply`/`Cancel`)
3. 승락 시 `DoApply(slot)`:
   - `Repo.WriteAutoBackup(currentPayload)` — 슬롯 0 에 현 캐릭터 자동백업
   - `Repo.PathFor(slot)` → `SlotFile.Read(path)` → `payload.Player` (raw JSON)
   - `StripForApply(playerJson)` — 슬롯 데이터에서 보존 필드(force, location, relations) 제외
   - `SerializerService.Populate(playerJson, currentHeroDataObject)` — **이 task 의 핵심 위험점**
4. **`SerializerService.Populate` 가 IL2CPP 환경에서 작동하는지** — Newtonsoft `JsonSerializer.Create().Populate(reader, il2target)` 가 JsonReader 의 IL2CPP 어셈블리 격차를 넘어 실제 deserialize 하는지 미검증
5. **만약 Populate 실패하면 escape hatch**: 슬롯 데이터를 임시 Hero 파일로 저장 → `GameDataController.Load(saveID)` 호출하여 게임 자체 직렬화 경로로 우회

### 6.2 Task 18.5 — 디테일 패널 미구현 핸들러

`SlotDetailPanel` 의 4개 액션 모두 핸들러 미와이어:
- `OnRenameRequested` — 입력 다이얼로그 (UI/InputDialog.cs 신규?) → `Repo.Rename(slot, newLabel)`
- `OnCommentRequested` — 동상 → `Repo.UpdateComment(slot, newComment)`
- `OnDeleteRequested` — ConfirmDialog (이미 있음) → `Repo.Delete(slot)` + `Repo.Reload()`
- `OnRestoreRequested` (슬롯 0 전용) — Apply 와 동일 흐름 (자동백업 슬롯에서 복원)

### 6.3 Task 19~22

플랜대로 진행. 각각 ~15분 분량 코드 + 1번 스모크.

### 6.4 Task 23 — 풀 스모크 체크리스트

플랜 §11.2 의 A~H 목록 모두 게임에서 실행 + 결과를 `docs/superpowers/specs/smoke-tests-2026-04-27.md` 에 기록.

### 6.5 Task 24 — Pinpoint 핀픽스

스모크 23 에서 발견되는 적용 후 미반영 항목들을 `PinpointPatcher.RefreshAfterApply` 에 추가.

### 6.6 Task 25 — 릴리스 패키징

README.md 작성, zip 패키징, git tag v0.1.0.

---

## 7. 다음 세션을 위한 컨텍스트 압축본

**다음 세션 첫 메시지에 붙여넣을 요약**:

> LongYin Roster Mod 빌드 중. Task 17 라이브 캡처 + 같은 슬롯 덮어쓰기까지 게임 안 검증 완료
> (commits 59e3be2, 8d115d4). 프로젝트 루트는
> `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`. 핸드오프
> 문서: `docs/HANDOFF.md`, 플랜: `docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md`.
>
> 다음 단계: Task 18 (apply, slot → game). SerializerService.Populate 가 IL2CPP 환경에서
> 작동하는지가 가장 큰 미지수. 핸드오프 §4 (IL2CPP 함정 3종) + §6.1 의 escape hatch
> (`GameDataController.Load`) 참고.

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
grep -n "HeroLocator\|toast\|Capture\|slot " "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"

# 슬롯 디렉터리 확인 + 깨끗하게 시작
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/"
rm -f "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/"slot_*.json

# 슬롯 메타 빠른 확인
python -c "import json; d=json.load(open('E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/slot_01.json', encoding='utf-8-sig')); print(json.dumps(d['_meta']['summary'], ensure_ascii=False, indent=2))"
```
