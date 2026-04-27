# LongYin Roster Mod — Design

- **Project**: BepInEx 6 IL2CPP plugin for *龙胤立志传 (Long Yin Li Zhi Zhuan) v1.0.0 f8.2*
- **Plugin GUID**: `com.deepe.longyinroster`
- **MVP version**: `0.1.0`
- **Spec date**: 2026-04-27
- **Author**: deepe + Claude (brainstorming session)

---

## 1. Purpose

캐릭터 스냅샷을 모드의 슬롯 시스템에 저장하고, 다른 시점/세이브의 스냅샷을
지금 플레이 중인 플레이어 캐릭터에 덮어쓸 수 있게 하는 BepInEx 모드.

본질은 우리가 이미 검증한 외부 도구 `merge_player_export.py` + `Hero_PlayerExport.json`
스키마를 게임 안으로 들여와 IMGUI UI로 감싸는 것.

## 2. Scope

### In scope (v0.1)

- **캡처 (저장)** — 두 가지 출처
  1. **라이브**: 현재 플레이 중인 Hero(heroID=0)를 직렬화해 슬롯에 저장
  2. **파일**: 디스크의 `Save/SaveSlotN/Hero` 파일에서 heroID=0 영웅을 추출해 슬롯에 저장
- **덮어쓰기 (적용)** — 슬롯 데이터를 현재 플레이어에게 in-place 적용
- **슬롯 관리** — 20개 슬롯 + 슬롯 0(자동 백업) · 이름변경 · 메모 · 삭제
- **자동 백업** — 덮어쓰기 직전 슬롯 0에 1단계 undo 스냅샷 자동 저장
- **모드 UI** — F11 토글 IMGUI 오버레이, 리스트 + 사이드바 상세 레이아웃
- **로깅** — BepInEx 로거 사용, 레벨 설정 가능
- **한국어 UI** (하드코딩)

### Out of scope (v0.1, deferred to v0.2+)

- NPC 소환 (옛 캐릭터를 별도 NPC로 등장시키기)
- 자동 백업 다단계 undo
- 슬롯 검색/태그/필터/정렬
- 영어/중국어 다국어
- BepInExConfigManager 또는 UniverseLib 통합 UI
- 슬롯 import/export (수동 파일 복사로 대체)
- 모드 자동 업데이트 알림
- 일부 필드만 선택적으로 캡처/적용
- 다른 모드(`LongYinCheat` 프로필 등)와의 자동 호환 변환

## 3. Decisions Recap

| # | 결정 | 이유 |
|---|---|---|
| 1 | MVP = 캡처 + 덮어쓰기 | NPC 소환은 heroID 할당, 소속, 위치 등 더 까다로움 → v0.2 |
| 2 | 캡처 소스 = 파일 + 라이브 둘 다 | 파일은 기존 코드 재사용으로 거의 무료, 라이브는 가치가 큼 |
| 3 | 덮어쓰기 정책 = 캐릭터 이식 (strict-personal) | 문파/위치/관계/AI는 보존, 캐릭터 본질만 교체. `merge_player_export.py --strict-personal`과 동일 |
| 4 | UI 진입 = 핫키 토글 + IMGUI 오버레이 | IL2CPP에서 가장 안정적/단순 |
| 5 | 슬롯 정책 = 고정 20 + 자동이름 + 슬롯당 JSON 1개 + 슬롯 0 자동백업 | 게임 자체 슬롯 시스템과 닮아 친숙 |
| 6 | 레이아웃 = 리스트 + 사이드바 상세 | 정보 밀도, 클릭 단계 짧음, 구현 단순, 향후 NPC 소환 버튼 추가 자리 깔끔 |
| 7 | 파일 캡처 = `Save/SaveSlot*` 자동 스캔만 | 사용자에게 가장 자연스러움. 수동 경로 입력 불필요 |
| 8 | 덮어쓰기 확인 = 자동백업 체크박스(기본 ON) + 단순 확인 | 안전망 + 가벼움 |
| 9 | 다국어 = 한국어만 | 본인 사용 우선, 나중에 확장 |
| 10 | 아키텍처 = 데이터 레이어 (JsonConvert in/out) + 핀포인트 보완 | `merge_player_export.py`를 C#으로 옮긴 것과 사실상 동일 |

## 4. Architecture

### 4.1 Module breakdown

```
LongYinRosterMod (DLL)
│
├── Plugin.cs                    BepInEx BasePlugin 진입점
│                                · ConfigEntry: HotKey, SlotDir, AutoBackupOnApply
│                                · GameObject 생성 + UIController 등록
│
├── Core/
│   ├── HeroLocator.cs           ▶ heroID=0 Hero 객체 찾기
│   ├── SerializerService.cs     ▶ JsonConvert.SerializeObject / PopulateObject
│   ├── PortabilityFilter.cs     ▶ 문파/위치/관계/AI 필드 제거 (strict-personal)
│   └── PinpointPatcher.cs       ▶ Populate 후 부수효과 처리 (1차: 빈 메서드+로깅)
│
├── Slots/
│   ├── SlotFile.cs              ▶ slot_NN.json 파일 1개의 read/write (atomic)
│   ├── SlotRepository.cs        ▶ 21슬롯(0~20) 컬렉션
│   ├── SlotMetadata.cs          ▶ 카드/리스트 표시용 요약
│   └── SaveFileScanner.cs       ▶ Save/SaveSlot0~10 헤더 파싱
│
├── UI/
│   ├── ModWindow.cs             ▶ OnGUI 메인 창. 핫키 토글
│   ├── SlotListPanel.cs         ▶ 왼쪽 슬롯 21행
│   ├── SlotDetailPanel.cs       ▶ 오른쪽 상세 + 액션 버튼
│   ├── ConfirmDialog.cs         ▶ 덮어쓰기 확인 (자동백업 체크박스)
│   └── FilePickerDialog.cs      ▶ "파일에서" → SaveSlot 자동 스캔 목록
│
└── Util/
    ├── Logger.cs                ▶ BepInEx 로거 래퍼 ([LongYinRoster] 접두사)
    └── KoreanStrings.cs         ▶ 모든 UI 문자열 상수
```

**경계 원칙**:
- `Core/` 만 게임 객체와 직접 닿음 — IL2CPP 의존성을 이 층에 격리
- `Slots/` 는 디스크 I/O와 메타데이터만
- `UI/` 는 IMGUI 그리기 + 입력만 — Core/Slots에 작업 위임

### 4.2 Plugin manifest

```csharp
[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("LongYinLiZhiZhuan.exe")]
public sealed class Plugin : BasePlugin
{
    public const string GUID    = "com.deepe.longyinroster";
    public const string NAME    = "LongYin Roster Mod";
    public const string VERSION = "0.1.0";

    public override void Load() { /* ... */ }
}
```

### 4.3 Dependencies

- `BepInEx.Unity.IL2CPP` (게임에 이미 설치됨)
- `Newtonsoft.Json` ← `BepInEx/interop/Newtonsoft.Json.dll` (게임 내장 인터롭)
- 추가 NuGet 의존성 **없음** — `BepInExConfigManager`, `UniverseLib` 등도 v0.1에서는 사용 안 함

### 4.4 Output layout

```
BepInEx/plugins/LongYinRoster/
├── LongYinRoster.dll
└── Slots/
    ├── slot_00.json   ← 자동 백업 (덮어쓰기 직전)
    ├── slot_01.json   ← 사용자
    ├── slot_02.json
    ├── ...
    └── slot_20.json
```

## 5. Data Flow

### 5.1 라이브 캡처 (현재 플레이어 → 슬롯)

```
[+] 현재 캐릭터 저장
  → HeroLocator.GetPlayer()                  heroID=0 Hero 객체
  → SerializerService.Serialize(hero)        JsonConvert.SerializeObject
  → SlotRepository.WriteToFreeSlot(json, meta)
  → slot_NN.json 작성 + UI 새로고침
```

### 5.2 파일 캡처 (디스크 SaveSlot → 슬롯)

```
[F] 파일에서
  → SaveFileScanner.ListAvailable()          Save/SaveSlot0~10/Info+Hero 헤더 파싱
                                             (35MB 풀 파싱 안 함)
  → 사용자가 한 줄 선택
  → SaveFileScanner.LoadSlot(slotN)          그제서야 풀 파싱, heroID=0 추출
  → SlotRepository.WriteToFreeSlot(json, meta)
  → slot_NN.json 작성 + UI 새로고침
```

### 5.3 덮어쓰기 (슬롯 → 현재 플레이어)

```
[▼ 덮어쓰기]
  → ConfirmDialog (자동백업 체크박스 ON 기본)
  → [확인]
  → [자동백업 ON 시] Serialize(player) → SlotRepository.Write(slot=0, ...)
                     실패 시 즉시 abort (덮어쓰기 안 함)
  → SlotFile.Read(slot_NN.json) → fullJson
  → PortabilityFilter.StripForApply(fullJson) → filteredJson
                     (문파 24개 + 휘발성 18개 = 42개 키 제거)
  → SerializerService.Populate(filteredJson, player)   in-place 변경
  → PinpointPatcher.RefreshAfterApply(player)          캐시/아이콘 갱신
                                                       (v0.1: 무동작 + 디버그 로그.
                                                        통합 테스트에서 미반영 항목 발견 시 추가)
  → 완료 토스트
```

### 5.4 핵심 관찰

- 흐름 5.1 과 5.3 의 직렬화 경로가 대칭 — 같은 `JsonConvert` 사용
- 5.2 의 헤더 파싱 트릭으로 UI 응답성 확보 (~50ms)
- 모든 흐름이 게임의 SaveManager 거치지 않음 — 다음 게임 자체 저장 시에 디스크 반영
- 파일 캡처는 게임이 그 슬롯을 로드 중이 아닐 때 가장 신뢰 가능 — UI에 ⚠ 마크 표시

## 6. Slot File Schema

`Save/_PlayerExport/Hero_PlayerExport.json` 으로 검증한 스키마 그대로 재사용.

### 6.1 `slot_NN.json` 구조

```jsonc
{
  "_meta": {
    "schemaVersion": 1,
    "modVersion": "0.1.0",
    "slotIndex": 1,
    "userLabel": "초한월 단월검 8.5.3",
    "userComment": "",
    "captureSource": "live",                  // "live" | "file"
    "captureSourceDetail": "SaveSlot3",
    "capturedAt": "2026-04-27T19:45:00",
    "gameSaveVersion": "1.0.0 f8.2",
    "gameSaveDetail": "8년 5월 3일 / 仙霞派 掌門",
    "summary": {
      "heroName": "초한월",
      "heroNickName": "단월검",
      "isFemale": true,
      "age": 18,
      "generation": 1,
      "fightScore": 353738.0,
      "kungfuCount": 130,
      "kungfuMaxLvCount": 117,
      "itemCount": 156,
      "storageCount": 217,
      "money": 98179248,
      "talentCount": 16
    }
  },
  "player": {
    /* heroID=0 Hero 객체의 153개 키 풀 스냅샷 */
  }
}
```

### 6.2 설계 포인트

- **`player`는 풀 스냅샷** — strict-personal 필터는 *적용 시점*에서 적용. 같은 슬롯이 v0.2 NPC 소환에도 그대로 쓸 수 있게.
- **`_meta.summary`는 캐시** — UI 표시용. 35MB 파일 다시 파싱 회피.
- **`schemaVersion: 1`** — 미래 포맷 변경 시 마이그레이터 진입점. v1만 읽고 다른 버전은 빨간 슬롯으로 표시.
- **외부 호환성**: `_meta`만 떼면 우리 기존 `Hero_PlayerExport.json` 과 동일. 외부 도구와 양방향 호환.
- **인코딩**: UTF-8 (BOM 없음), Unix LF.
- **압축 안 함** — 슬롯당 ~500KB, 21슬롯 합 ~10MB.

### 6.3 파일 잠금 / 원자성

- 모든 쓰기는 `slot_NN.json.tmp` → `File.Replace` 패턴.
- 동시 작업은 `SemaphoreSlim(1,1)` 으로 직렬화.

### 6.4 빈 슬롯

- 빈 슬롯 = 파일 없음 (placeholder JSON 안 만듦).
- `SlotRepository.LoadAll()`이 21경로 시도, 없는 것은 `Empty(slotIndex)` 메타로 채움.

### 6.5 슬롯 0 (자동백업) 특별 취급

- 사용자가 직접 캡처/덮어쓰기/이름변경 못함 (UI에서 disabled).
- "복원" 버튼만 활성 — 직전 덮어쓰기 결과를 되돌림.
- 새 자동백업이 쓰이면 기존 슬롯 0 덮어씀 (1단계 undo만, v0.1 한정).

## 7. UI

### 7.1 메인 창 동작

- 활성화: F11 (config 변경 가능). 토글식.
- 크기/위치: 720×480, 우측 기본. 사용자 드래그/리사이즈 위치는 config에 영속.
- 입력 흡수: 모드 창 위 마우스만 게임 입력 차단 (`Event.current.Use()`).
- 일시정지 옵션: `PauseGameWhileOpen=false` 기본. 켜면 `Time.timeScale=0`.

### 7.2 레이아웃

```
┌───────────────────────────────────────────────────────────────┐
│ Roster Mod  ·  F11 to close                          v0.1     │
├──────────────────────────┬────────────────────────────────────┤
│ [+] 현재 캐릭터 저장      │ 슬롯 01 · 초한월 (단월검)          │
│ [F] 파일에서             │                                    │
├──────────────────────────┤ 캡처: 2026-04-27 23:52             │
│ 00 · 자동백업 (없음)     │ 세이브 시점: 8년 5월 3일            │
│ ▶ 01 · 초한월 [8.5.3]    │ 전투력: 353,738                     │
│   02 · 단월검4 [12.3.8]  │ 무공: 130 (Lv10 117)                │
│   03 · 검선시도 [6.1.21] │ 인벤토리: 156 / 창고 217            │
│   04 · (비어있음)        │ 금전: 98,179,248냥                  │
│   ...                    │ 천부: 16개                         │
│   20 · (비어있음)        │ ───────────────────────────────    │
│                          │ [▼ 현재 플레이어로 덮어쓰기]        │
│                          │ [이름변경]  [메모]  [×삭제]         │
└──────────────────────────┴────────────────────────────────────┘
```

### 7.3 상태 머신

```
HIDDEN ──[F11]──▶ MAIN ──┬─[슬롯 클릭]──▶ MAIN (selectedSlot 변경)
                         ├─[+ 저장]────▶ INLINE_BUSY ──▶ MAIN
                         ├─[F 파일에서]─▶ FILE_PICKER ──▶ MAIN
                         ├─[덮어쓰기]──▶ CONFIRM_DIALOG ──▶ MAIN
                         └─[삭제]──────▶ DELETE_CONFIRM ──▶ MAIN

MAIN ──[F11/×닫기]──▶ HIDDEN
```

### 7.4 서브 다이얼로그

**ConfirmDialog (덮어쓰기)**
- 슬롯 이름 + "캐릭터 본질만 교체" 안내
- ☑ 슬롯 00에 자동 저장 (default ON)
- [취소] / [덮어쓰기]

**FilePickerDialog**
- `Save/SaveSlot0~10/` 11줄 목록
- 행 형식: `SaveSlot3 · 초한월 1세대 · 8.5.3 · fight 354K  ⚠현재 로드 중`
- 행 클릭 → "이 슬롯에서 가져오기" 활성화 → 다음 빈 모드 슬롯에 캡처
- [취소]

**DeleteConfirm**
- "슬롯 NN을 삭제합니다. 되돌릴 수 없습니다."
- [취소] / [삭제]

### 7.5 토스트

- 우측 하단, 3초 자동 소멸.
- 예시:
  - `✔ 슬롯 03에 캡처되었습니다.`
  - `✔ 슬롯 01의 데이터로 덮어썼습니다. 슬롯 00에 직전 상태 자동저장.`
  - `✘ 캡처 실패: HeroLocator could not find player. (자세한 내용: BepInEx 로그)`

### 7.6 빈 상태 / 비활성화

- 슬롯 0개 → 사이드바에 "왼쪽 [+] 버튼으로 첫 캐릭터를 저장하세요"
- 메인 메뉴 (`HeroLocator.IsInGame()=false`) → 모든 액션 disabled, "게임에 입장한 뒤 사용 가능합니다" 안내

### 7.7 IMGUI 구현 메모

- `OnGUI()` 안에서 모든 그리기. `Update()`에서는 핫키 감지만.
- 슬롯 메타 캐시: `LoadAll()`은 모드 창 첫 열림 + 변경 시에만 호출. OnGUI는 캐시만 읽음.
- **한글 폰트 리스크**: BepInEx 6 IL2CPP 환경에서 IMGUI 기본 폰트가 한글 미지원일 수 있음. 게임 내장 한글 폰트(`TextMeshPro` 에셋) 또는 별도 `arial.ttf` 류를 `GUI.skin.font`로 주입. 첫 빌드에서 확인.

## 8. Configuration

`BepInEx/config/com.deepe.longyinroster.cfg`:

```ini
[General]
ToggleHotkey = F11
PauseGameWhileOpen = false
SlotDirectory = <PluginPath>/Slots

# 사용자가 쓸 수 있는 슬롯 개수 (1~MaxSlots).
# 슬롯 0(자동백업)은 이 카운트에 포함되지 않음. 총 파일 = MaxSlots + 1.
# 기본 20, 50까지 허용.
MaxSlots = 20

[UI]
WindowX = 1100
WindowY = 100
WindowW = 720
WindowH = 480

[Behavior]
AutoBackupBeforeApply = true
RunPinpointPatchOnApply = true

[Logging]
LogLevel = 3   # 0=Off, 1=Error, 2=Warn, 3=Info, 4=Debug
```

## 9. Public Module Signatures

```csharp
// Core/HeroLocator.cs
public static class HeroLocator
{
    public static Hero? GetPlayer();   // heroID=0 Hero 또는 null
    public static bool   IsInGame();
}

// Core/SerializerService.cs
public static class SerializerService
{
    public static string Serialize(Hero hero);
    public static void   Populate(string json, Hero target);
    public static T?     DeserializeFromFile<T>(string path);
}

// Core/PortabilityFilter.cs
public static class PortabilityFilter
{
    public static string StripForApply(string fullPlayerJson);
    public static IReadOnlyList<string> ExcludedFields { get; }
}

// Core/PinpointPatcher.cs
public static class PinpointPatcher
{
    public static void RefreshAfterApply(Hero player);
}

// Slots/SlotRepository.cs
public sealed class SlotRepository
{
    public IReadOnlyList<SlotEntry> All { get; }
    public SlotEntry  Read(int index);
    public void       Write(int index, SlotPayload payload);
    public void       Delete(int index);
    public void       Rename(int index, string newLabel);
    public void       UpdateComment(int index, string newComment);
    public int        AllocateNextFree();
    public void       Reload();
}

public readonly record struct SlotEntry(
    int Index, bool IsEmpty, SlotMetadata? Meta, string FilePath);

// Slots/SaveFileScanner.cs
public static class SaveFileScanner
{
    public sealed record SaveSlotInfo(
        int    SlotIndex,
        bool   Exists,
        bool   IsCurrentlyLoaded,
        string SaveDetail,
        DateTime SaveTime,
        string HeroName,
        string HeroNickName,
        float  FightScore);

    public static List<SaveSlotInfo> ListAvailable();
    public static string LoadHeroJson(int saveSlotIndex);
}
```

## 10. Edge Cases & Risks

### 10.1 사용자 행동 / 게임 상태

| # | 상황 | 대응 |
|---|---|---|
| E1 | 메인 메뉴에서 모드 창 열기 (플레이어 없음) | `IsInGame()=false` → 액션 disabled + 안내 |
| E2 | 전투 중 덮어쓰기 시도 | 경고 다이얼로그 + 진행 가능. v0.2에 강제 차단 옵션 |
| E3 | 자동저장 진행 중 덮어쓰기 | SaveManager 상태 폴링 가능하면 잠금. 안 되면 "저장 직후에 다시 시도" 안내 |
| E4 | 빈 슬롯에 덮어쓰기 시도 | UI 단에서 비활성화. API 호출도 NoOp + Error 토스트 |
| E5 | 슬롯 0(자동백업) 직접 캡처/덮어쓰기 | UI disabled. API는 `InvalidOperationException` |

### 10.2 직렬화 / 역직렬화 (안 1의 핵심 위험점)

| # | 리스크 | 대응 |
|---|---|---|
| S1 | 순환 참조 (Friends/Haters → 다른 Hero) | 게임 디스크 형태와 동일하게 heroID 정수만 보관. `JsonSerializerSettings`를 게임 파일과 일치 |
| S2 | PopulateObject가 IL2CPP 컬렉션 못 채움 | 첫 빌드 검증. 막히는 컬렉션 (의심: `kungfuSkills`, `itemListData.allItem`) 은 `PinpointPatcher`에서 수동 매핑 추가 |
| S3 | 다형 필드 (`aiStuffTarget` 등) | `TypeNameHandling.Auto`. v0.1 에서는 `aiStuffTarget`이 휘발성으로 어차피 제거됨 |
| S4 | enum 직렬화 형식 | `IntegerEnumConverter` 명시 |
| S5 | float 정밀도 | round-trip ("R") 명시 |
| S6 | null vs 빈 컬렉션 | Populate 전에 `target.kungfuSkills ??= new()` 보정 |

### 10.3 파일 시스템 / 동시성

| # | 리스크 | 대응 |
|---|---|---|
| F1 | 쓰기 중 크래시 | `.tmp` → `File.Replace` 원자 패턴 |
| F2 | 디스크 부족 / 권한 실패 | 예외 잡아 Error 토스트, 슬롯 상태 변경 없음 |
| F3 | 사용자 빠른 더블 클릭 | 작업 중 버튼 비활성 + `SemaphoreSlim(1,1)` 직렬화 |
| F4 | 외부 도구가 같은 파일 동시 수정 | v0.1: 신경 안 씀. v0.2 에 mtime 감지 후 reload |

### 10.4 게임 업데이트 호환성

| # | 리스크 | 대응 |
|---|---|---|
| G1 | Hero에 새 필드 추가 | 안 1은 자동 흡수 |
| G2 | 기존 필드 타입 변경 | Newtonsoft 기본 변환 시도, 실패 시 그 필드 건너뛰고 경고 로그 |
| G3 | 슬롯 파일이 구 게임버전에서 캡처됨 | `_meta.gameSaveVersion` 비교 → 다르면 경고 다이얼로그 |
| G4 | 모드 자체 schemaVersion 변경 | 하드체크. 미래 마이그레이터로 처리 |

### 10.5 자동백업 트랜잭션 안전장치

```
1. snapshot = Serialize(player)
2. WriteAtomic(slot=0, snapshot)   ← 실패 시 여기서 throw → abort
3. apply slot_NN
4. (성공 토스트)
```

자동백업 실패 시 덮어쓰기 자체를 abort. 자동백업 OFF 인 경우는 1, 2 건너뜀.

## 11. Testing Strategy

### 11.1 자동화 단위 테스트 (게임 없이)

별도 `LongYinRoster.Tests` 프로젝트 — IL2CPP 의존성 없는 순수 로직만.

| 테스트 대상 | 검증 |
|---|---|
| `PortabilityFilter.StripForApply` | 실제 SaveSlot3 Hero JSON 으로 round-trip; 제외 필드 모두 빠지고 보존 필드 그대로 |
| `PortabilityFilter.ExcludedFields` | 24개 문파 + 18개 휘발성 = 42개 명시 |
| `SlotFile.WriteAtomic / Read` | 임시 디렉터리 round-trip, `.tmp` 잔존 없음 |
| `SlotMetadata.FromPlayerJson` | summary 필드 모두 채워짐 |
| `SaveFileScanner.ParseHeader` | 처음 4KB 만으로 `heroName`, `fightScore` 추출. graceful 처리 |
| `SlotRepository.AllocateNextFree` | 슬롯 0,1,3,5 차 있을 때 결과 = 2 (슬롯 0 건너뛰는지) |
| schema version mismatch | `schemaVersion: 99` → `UnsupportedVersion` 예외 |

**검증 데이터**: `Save/SaveSlot3/Hero` 사본을 `tests/fixtures/slot3_hero.json` 으로 동결.

### 11.2 통합 스모크 체크리스트 (실제 게임 1회)

**준비**: 게임 폴더 통째로 백업 + 별도 SaveSlot10 사용해 영향 격리.

```
A. 활성화
[ ] 게임 시작 → BepInEx 로그에 "[LongYinRoster] Loaded v0.1.0"
[ ] 메인 메뉴 F11 → 모드 창, 액션 버튼 disabled
[ ] 게임 입장 후 F11 → 캡처 버튼 활성화

B. 라이브 캡처
[ ] [+ 현재 캐릭터 저장] → 50ms 안 토스트, 슬롯 01에 메타 표시
[ ] 슬롯 01 클릭 → 사이드바 fightScore/무공/인벤토리 표시
[ ] BepInEx/plugins/LongYinRoster/Slots/slot_01.json 존재, ~500KB

C. 파일 캡처
[ ] [F 파일에서] → 11줄 목록, 캐릭터 이름과 fightScore 표시
[ ] SaveSlot3 행 → 슬롯 02에 캡처, ⚠ 마크 확인
[ ] 빈 SaveSlot 회색

D. 덮어쓰기 (가장 중요)
[ ] 슬롯 01 [덮어쓰기] → 다이얼로그, 자동백업 ON
[ ] 확인 → 토스트, 슬롯 00에 직전 백업 생성
[ ] 캐릭터 상태창 → fightScore/무공/인벤토리/천부 모두 슬롯 데이터와 일치
[ ] 위치/문파/친구 변경 없음 (strict-personal 검증)
[ ] 게임 자체 저장 → 재시작 → 영속 확인

E. 자동백업 복원
[ ] 슬롯 00 [복원] → 직전 상태 복귀
[ ] 한 번만 가능 (다시 누르면 NoOp)

F. 슬롯 관리
[ ] 슬롯 03 [이름변경] → 디스크 + UI 반영
[ ] 슬롯 03 [메모] → 저장
[ ] 슬롯 03 [삭제] → 확인 → slot_03.json 삭제, UI는 (비어있음)

G. 에러 시나리오
[ ] 슬롯 디렉터리 권한 제거 → 캡처 시도 → Error 토스트
[ ] schemaVersion=99 가짜 슬롯 → 빨간 슬롯 표시
[ ] 다른 SaveSlot 게임에 옛 슬롯 적용 → 게임버전 다르면 경고

H. UI/UX
[ ] F11 토글 반복 → 메모리 안정
[ ] 모드 창 영역에서 게임 입력 차단
[ ] 슬롯 가득 + 캡처 → "빈 슬롯이 없습니다" 토스트
[ ] 한국어 텍스트 정상 렌더링 (□ 글리프 없음)
```

### 11.3 베이스라인 회귀 데이터 (D 단계용 기대값)

```
fightScore    : 353,738
무공 Lv10 수  : 117 / 130
인벤토리      : 156 개
창고          : 217 개
금전          : 98,179,248 냥
천부재능      : 16 개
belongForceID : 변경 없음 (이전 값 보존)
atAreaID      : 변경 없음
Friends/Haters: 변경 없음
```

### 11.4 릴리스 게이트 (v0.1.0)

다음 모두 통과해야 출시:

1. ✅ 단위 테스트 100% 통과
2. ✅ 스모크 체크리스트 A~H 모두 통과
3. ✅ 베이스라인 슬롯 적용 → 게임 자체 저장 → 재시작 → 영속 OK
4. ✅ BepInEx 로그에 unhandled exception 없음
5. ✅ 기존 모드(`LongYinCheat`, `TeammateManagerMod` 등)와 동시 사용 시 충돌 없음

### 11.5 디버깅 도구 (개발 한정)

- `LogLevel=4` 시 PortabilityFilter 제거 필드 목록, PopulateObject 키 수, IL2CPP 예외 모두 기록
- 모드 창 우상단 작은 ⚙ 버튼 → "마지막 작업 로그를 클립보드로" → 이슈 제보 양식

## 12. Open Questions / Future Work

- v0.2 NPC 소환 — heroID 할당 정책, 소속 결정, 위치 선정, 적/우호 설정
- 자동백업 다단계 undo (현재 1단계만)
- 파일 캡처 시 "캐릭터 선택" — heroID=0 외 다른 영웅도 선택 가능하게
- 다른 모드(`LongYinCheat` 프로필)와의 변환 도구
- 다국어 (en, zh-CN)

## 13. Glossary

- **strict-personal** — 우리 분석에서 정의한 캐릭터 이식 정책. 문파/직책/기여도/봉록 24필드 + 위치/AI/팀/임무/관계 18필드 = 42필드 제거하고 캐릭터 본질(스탯·스킬·아이템·외형·천부)만 적용.
- **Hero** — 게임의 영웅 객체. 153개 키. heroID=0이 플레이어.
- **풀 스냅샷** — Hero 객체 153키 모두 포함된 JSON.
- **슬롯 0** — 자동백업 전용. 사용자가 직접 쓸 수 없음.
- **PinpointPatcher** — Populate 후 부수효과(아이콘 갱신, 캐시 무효화 등) 처리하는 모듈. v0.1에서는 빈 메서드 + 로깅, 발견되는 문제마다 추가.
