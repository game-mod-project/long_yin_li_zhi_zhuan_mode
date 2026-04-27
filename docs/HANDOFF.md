# LongYin Roster Mod — 작업 핸드오프 문서

**일시 중지**: 2026-04-27
**진행 상태**: Task 17 / 25 완료 (commit `ca6d630`)
**저장소**: https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode (`main` 브랜치)
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`

---

## 1. 한 줄 요약

BepInEx 6 IL2CPP 환경에서 플레이어 캐릭터 스냅샷을 20슬롯에 저장/복원하는 모드. **17/25 태스크 완료, 18 태스크부터 게임 안 검증 필요**.

---

## 2. 현재 깃 히스토리 (origin/main 동기화 완료, 17 commits)

```
ca6d630 feat(capture): wire live capture — HeroLocator + SerializerService real impl   ← Task 17
266fca4 feat(ui): SlotListPanel + SlotDetailPanel with selection + action callbacks    ← Task 16
a780d98 feat(ui): ModWindow IMGUI shell with F11 toggle, drag, position persistence    ← Task 15
f04b805 feat(ui): ToastService with 3s auto-dismiss                                    ← Task 14
428f2b0 feat(config): bind BepInEx ConfigEntries                                       ← Task 13
f507e78 feat(core): PinpointPatcher skeleton with debug logging                        ← Task 12
7588833 feat(core): HeroLocator finds heroID=0 player via reflection                   ← Task 11 (rewritten in Task 17)
8b9eebd feat(core): SerializerService stub                                             ← Task 10 (rewritten in Task 17)
dc01499 fix(slots): bump ParseHeader default to 512KB
42c045f feat(slots): SaveFileScanner header parsing                                    ← Task 9
16c38ea feat(slots): SlotRepository with slot 0 auto-backup guard                      ← Task 8
3301432 feat(slots): atomic SlotFile I/O                                               ← Task 7
ea18a73 feat(slots): SlotMetadata + SlotPayload                                        ← Task 6
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
├── .gitignore                 ← .superpowers/ 제외 추가됨
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
    │   │   ├── HeroLocator.cs               ← GameDataController 경로 + Resources scan fallback
    │   │   ├── SerializerService.cs         ← JsonConvert.SerializeObject + JsonSerializer.Populate
    │   │   ├── PortabilityFilter.cs         ← 24+21=45 필드 strict-personal 제거
    │   │   └── PinpointPatcher.cs           ← v0.1 무동작 스켈레톤
    │   ├── Slots/
    │   │   ├── SlotPayload.cs / SlotMetadata.cs / SlotEntry.cs
    │   │   ├── SlotFile.cs                  ← atomic .tmp → File.Replace
    │   │   ├── SlotRepository.cs            ← 21슬롯 (0=자동백업 + 1~20)
    │   │   └── SaveFileScanner.cs           ← Save/SaveSlot0~10 헤더 파싱 (512KB 기본)
    │   ├── UI/
    │   │   ├── ModWindow.cs                 ← MonoBehaviour, F11 토글, IMGUI 메인 창
    │   │   ├── SlotListPanel.cs             ← 왼쪽 21행
    │   │   ├── SlotDetailPanel.cs           ← 오른쪽 상세 + 액션 버튼
    │   │   └── ToastService.cs              ← 3초 자동소멸
    │   └── Util/
    │       ├── Logger.cs                    ← BepInEx 로거 래퍼
    │       ├── KoreanStrings.cs             ← UI 문자열 상수
    │       └── PathProvider.cs              ← <PluginPath> 토큰 + GameSaveDir
    └── LongYinRoster.Tests/                 ← 테스트 (18 tests, all pass)
        ├── LongYinRoster.Tests.csproj
        ├── fixtures/slot3_hero.json         ← 35MB 동결 baseline
        ├── SmokeTests.cs
        ├── PortabilityFilterTests.cs        (4)
        ├── SlotMetadataTests.cs              (1)
        ├── SlotFileTests.cs                  (3)
        ├── SlotRepositoryTests.cs            (7)
        └── SaveFileScannerTests.cs           (2)
```

**테스트**: `dotnet test` → **18/18 PASS**.
**빌드**: `dotnet build -c Release` → 자동으로 `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 배포.

---

## 4. 가장 중요한 발견 (다음 세션 작업자 필독)

### 4.1 IL2CPP-bound Newtonsoft.Json API gap

`BepInEx/interop/Newtonsoft.Json.dll` 은 표준 NuGet 버전과 **다른 표면**을 가진다:

| 표준 Newtonsoft | IL2CPP 버전 | 우리 대응 |
|---|---|---|
| `JsonConvert.SerializeObject(obj, settings)` | ❌ 1+settings 오버로드 없음 | `SerializeObject(obj, type, settings)` 또는 `(Il2CppSystem.Object)` 단일 인자 |
| `JsonConvert.PopulateObject(json, obj, settings)` | ❌ **부재** | `JsonSerializer.Create().Populate(JsonTextReader(...), il2target)` |
| `JsonSerializerSettings.NullValueHandling` setter | ❌ 읽기전용 | 사용 안 함 |
| `JsonSerializerSettings.FloatFormatHandling` | ❌ 부재 | 사용 안 함 |
| `JObject` foreach (C# duck-typed) | ❌ enumerator 호환 안 됨 | `for (int i = 0; i < arr.Count; i++)` 또는 IList<JToken> 캐스트 |
| `JsonTextReader` ctor | takes `Il2CppSystem.IO.TextReader` | `new Il2CppSystem.IO.StringReader(json)` |

`SerializerService.cs` 안에 이 패턴 다 적용돼 있음.

### 4.2 게임 내부 구조 (Assembly-CSharp.dll 조사 결과)

- **플레이어 영웅 타입**: `HeroData` (전역 namespace, [Serializable])
- **싱글톤 매니저**: `GameDataController.Instance` (MonoBehaviour)
  - `.gameSaveData.HeroList` → `List<HeroData>` (899개 영웅 컬렉션)
  - `.Save(int saveID)` / `.Load(int saveID)` — 게임 자체 저장/로드 메서드
  - **백업 비상로**: 위 두 메서드 호출하면 게임이 직접 디스크에 저장. SerializerService.Populate 가 막히면 이걸 escape hatch 로 사용 가능.
- **Hero 파일 형식**: JSON array of HeroData (TypeNameHandling=None, default Newtonsoft)
- **HeroData 콜백**: `OnSerializingMethod`, `OnDeserializedMethod` ([OnSerializing]/[OnDeserialized] 표시) — 우리 직렬화 경로에서 자동 발화

---

## 5. 검증된 것 / 검증 안 된 것

### ✅ 게임 안에서 검증 완료 (Task 14 직후 1차 확인 + Task 15 직후 2차 확인)
- BepInEx 가 우리 플러그인 정상 로드 (`Loaded LongYin Roster Mod v0.1.0`)
- BepInEx config 파일 자동 생성 (`com.deepe.longyinroster.cfg`)
- F11 핫키로 IMGUI 창 토글 작동
- 한글 텍스트 정상 렌더링 (Task 22 폰트 fix 불필요)
- 창 드래그 + 위치 영속 OK
- ModWindow.Awake 에서 SlotRepository 초기화 OK (BepInEx 로그에 "ModWindow Awake (slots dir: ...)")
- 18 unit tests all pass

### ⚠️ 작성됐지만 게임 안 검증 안 됨 (Task 17 추가 코드)
- `HeroLocator.GetPlayer()` 가 실제로 `HeroData` 반환하는지 — **첫 검증 필요**
- `SerializerService.Serialize(player)` 가 35MB Hero 파일 비슷한 JSON 생성하는지
- `ModWindow.CaptureCurrent()` 흐름이 슬롯 파일 디스크에 만드는지
- Slot detail panel 이 캡처된 슬롯 정보 제대로 표시하는지

### ❌ 아직 코드 자체가 없음 (Task 18+)
- 덮어쓰기 (apply) 흐름 — `SerializerService.Populate` 통합 호출
- 자동백업 트랜잭션
- 슬롯 0 복원
- 파일에서 캡처 (`SaveFileScanner.LoadHero0` 통합)
- 슬롯 이름변경 / 메모 / 삭제 UI 와이어링
- README / 릴리스 패키징

---

## 6. 다음 세션 시작 시 먼저 할 일 (우선순위 순)

### 6.1 라이브 캡처 통합 스모크 (반드시 먼저)

게임을 닫은 상태에서:
```bash
cd E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
# Build succeeded. 0 Warnings. → DLL 자동 배포
```

게임 실행 → 저장 슬롯 로드 → F11 → `[+] 현재 캐릭터 저장` 클릭.

**기대 결과**:
- 토스트: `✔ 슬롯 1에 캡처되었습니다.`
- `BepInEx/plugins/LongYinRoster/Slots/slot_01.json` 생성됨, ~500KB
- 슬롯 1 클릭 → 사이드바에 캐릭터 이름, fightScore, 무공/인벤토리 수 표시

**가능한 실패 모드와 디버그 경로**:

| 증상 | 의미 | 수정 방향 |
|---|---|---|
| 토스트: `✘ 플레이어를 찾을 수 없습니다` | HeroLocator 가 HeroData 못 찾음 | BepInEx 로그에 "HeroLocator manager path: ..." 또는 "HeroData type not found" 메시지. GameDataController 또는 HeroData 타입 이름 다를 수 있음 — 다른 후보로 재시도 |
| 토스트: `✘ 캡처 실패: ... InvalidOperationException` | SerializerService.Serialize 가 거부 | hero 가 Il2CppSystem.Object 가 아님. HeroLocator 가 잘못된 타입 반환 |
| 토스트: `✘ 캡처 실패: ... ` 다른 예외 | JsonConvert.SerializeObject 호출 실패 | BepInEx 로그 stack trace 확인. 게임 자체 SaveManager 경로(`GameDataController.Save`)로 우회 검토 |
| 슬롯에는 저장됐지만 fightScore=0 등 이상값 | 직렬화는 되었으나 일부 프로퍼티 누락 | 슬롯 파일 직접 열어 비교. 게임 자체 Save/SaveSlot0/Hero 와 diff |

### 6.2 Task 18 — 적용(덮어쓰기) 흐름

플랜 문서 `docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md` 의 Task 18 절 참고. 핵심:
- ConfirmDialog 추가 (`UI/ConfirmDialog.cs`)
- ModWindow.RequestApply / DoApply 메서드 추가
- 자동백업 트랜잭션 (Repo.WriteAutoBackup → Repo.PathFor(slot) → Read → StripForApply → Populate)
- `SerializerService.Populate` 가 IL2CPP 환경에서 실제로 작동하는지 — **이 task 의 핵심 위험점**
- 만약 Populate 실패하면 escape hatch: 슬롯 데이터를 임시 파일로 저장 후 `GameDataController.Load(saveID)` 호출

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

> LongYin Roster Mod 빌드 중. Task 17/25 완료, GitHub `main` 동기화됨 (`game-mod-project/long_yin_li_zhi_zhuan_mode`). 프로젝트 루트는 `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`. 핸드오프 문서: `docs/HANDOFF.md`, 플랜: `docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md`.
>
> 다음 단계: 라이브 캡처 게임 안 1차 검증 → Task 18 (apply flow) 진입. SerializerService.Populate 가 IL2CPP 환경에서 작동하는지가 가장 큰 미지수. 핸드오프 §6.1 의 "기대 결과 / 가능한 실패 모드" 표 참고.

---

## 8. 빠른 명령어 모음

```bash
# 게임 닫고 빌드
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release

# 테스트
DOTNET_CLI_UI_LANGUAGE=en dotnet test

# 게임 닫혔는지 확인
tasklist | findstr LongYinLiZhiZhuan

# 깃 풀 (다른 머신에서 작업 시)
git pull origin main

# BepInEx 로그 추적
tail -f "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"

# 슬롯 디렉터리 확인
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/"
```
