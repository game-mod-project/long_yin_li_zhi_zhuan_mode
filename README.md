# LongYin Roster Mod

**龙胤立志传 (LongYinLiZhiZhuan) v1.0.0 f8.2** 의 BepInEx 6 IL2CPP 플러그인.
플레이어 캐릭터(`heroID=0`) 스냅샷을 최대 20슬롯에 저장 / 관리한다.

## 무엇을 할 수 있나 (v0.5.1)

- **캡처** — 인게임에서 `F11` → `[+] 현재 캐릭터 저장` → 빈 슬롯 또는 선택한 슬롯에 현재 플레이어 데이터를 JSON 으로 저장.
- **파일에서 가져오기** *(v0.2)* — `[F] 파일에서` → 게임 자체 SaveSlot 0~10 목록 → 선택한 슬롯의 캐릭터를 mod 슬롯에 import.
- **Apply (slot → game)** *(v0.3 신규)* — `▼ 현재 플레이어로 덮어쓰기` → 슬롯의 캐릭터 본질 (이름·스탯·천부 등) 을 현재 플레이어에 PinpointPatcher 로 덮어쓰기. Apply 직전 자동백업 (슬롯 0).
- **Restore (slot 0 → game)** *(v0.3 신규)* — `↶ 복원` → 자동백업 슬롯 0 → 현재 플레이어로 복귀.
- **슬롯 관리** — 라벨 변경 / 메모 / 삭제. 슬롯 디테일 패널에 캐릭터 이름·전투력·무공·인벤토리 수·금전·천부 표시.
- **같은 슬롯 덮어쓰기** — 차있는 슬롯에 다시 캡처 시도하면 확인 다이얼로그.
- **창 안 입력 차단** *(v0.2)* — 모드 창 영역 안 클릭 / 마우스 휠이 게임으로 propagate 안 됨 (Harmony patch on `Input.GetMouseButton*` / `GetAxis("Mouse ScrollWheel")`).
- **자동 저장 디렉토리** — `BepInEx/plugins/LongYinRoster/Slots/` (cfg 로 변경 가능).

### v0.3 — Apply (slot → game) + Restore (slot 0 → game)

- 슬롯의 캐릭터 본질 (이름, 스탯, 무공, 인벤토리, 천부 등) 을 현재 플레이어에 덮어쓰기
- Apply 직전 자동백업 (슬롯 0) — 실패 시 자동복원
- 보존 필드 (force / location / relations) 는 변경 안 됨 — 사회적 위치 유지
- save → reload 후 캐릭터 정보창 정상 작동 (v0.2 시도 2 의 실패점 통과)
- 지원 필드 매트릭스: `docs/superpowers/specs/2026-04-29-longyin-roster-mod-v0.3-design.md` §7.2

**v0.3 지원 (stat-backup focus)**:
- 명예 / 악명 / HP / Mana / Power / 부상 (외상/내상/중독) / 충성 / 호감 / 자기집 add / 천부 포인트 / 활성 무공 / 스킨 / baseAttri / baseFightSkill / baseLivingSkill / expLivingSkill
- 천부 list (heroTagData)

### v0.4 — 9-카테고리 체크박스 + 정체성 활성화

- **9-카테고리 체크박스 UI** — 스탯 / 명예 / 천부 / 스킨 / 자기집 add / 정체성 / 무공 active / 인벤토리 / 창고 각 카테고리를 슬롯 디테일 패널에서 개별 선택. 토글 즉시 저장 (`_meta.applySelection`)
- **정체성 활성화** — heroName / nickname / age 등 9 필드를 setter direct path 로 Apply (PoC A2 PASS). save → reload 후 정보창 정상 표시 검증 완료
- **부상 / 충성 / 호감 backup 폐기** — v0.3 에서 Apply 시 덮어쓰던 부상 (외상/내상/중독) / 충성 / 호감 을 v0.4 에서 영구 보존 필드로 전환. 의도적 회귀 — 사회적 / 신체적 상태는 슬롯 적용과 무관하게 현재 게임 상태를 유지
- **v0.2/v0.3 슬롯 호환** — 기존 슬롯 파일 무손실. 로드 시 V03Default 자동 적용, 파일 건드리지 않음

### v0.5.1 — 무공 active 활성화

- **무공 active 카테고리 활성화** — slot 의 active 11-슬롯 set 을 현재 플레이어에 정확히 복원. UI 즉시 갱신 + save→reload persistence 검증 완료
- **알고리즘**: v0.5 Phase B Harmony trace 의 game 자체 패턴 mirror — `currentEquipped ∪ equipTargets` 모두 unequip → equipTargets 만 equip. UI cache invalidate trigger + silent fail 회피
- **v0.5 PoC 미해결 issue 모두 해소** — UI cache invalidation + save→reload persistence
- **list 의존성 제한** — slot 의 active skillID 가 현재 player 의 무공 list 에 있어야 적용 (다른 캐릭터의 무공 set 은 적용 불가, v0.6 무공 list sub-project 와 통합 예정)

**v0.6+ 후보 (현재 미지원)**:
- **인벤토리 / 창고** — sub-data wrapper graph 미해결 (ItemData factory PoC A4 FAIL). v0.6 에서 게임 내부 Add method 추가 dump 필요
- **무공 list** — KungfuSkillLvData wrapper ctor 의 IL2CPP 한계. v0.6 후보
- **외형** — `faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data wrapper graph. v0.6 후보

**사용법**:
1. 모드 창 (F11) → 슬롯 1~20 선택 → 디테일 패널에서 적용할 카테고리 체크박스 선택
2. `▼ 현재 플레이어로 덮어쓰기` 버튼 → 선택된 카테고리만 Apply
3. 자동백업 슬롯 0 → `↶ 복원` 버튼 으로 Apply 직전 상태 복귀

### Releases

| Version | Highlights |
|---|---|
| v0.1.0 | Live capture + slot management |
| v0.2.0 | Import from save + input gating |
| v0.3.0 | Apply (slot → game) + Restore (stat-backup) |
| v0.4.0 | 9-카테고리 체크박스 UI + 정체성 활성화 |
| v0.5.1 | 무공 active 활성화 (UI cache invalidate + persistence) |

## 요구 사항

- 게임: **龙胤立志传 v1.0.0 f8.2** (Steam)
- 모드 로더: **BepInEx 6.0.0-dev** (IL2CPP, .NET 6 runtime)
- 플랫폼: Windows 10/11 64-bit

## 설치

1. [BepInEx 6 IL2CPP](https://builds.bepinex.dev/projects/bepinex_be) 가 게임에 설치되어 있어야 함.
2. 릴리스 zip 의 내용을 게임 루트에 복사. 결과 구조:
   ```
   LongYinLiZhiZhuan/
   └── BepInEx/
       └── plugins/
           └── LongYinRoster/
               ├── LongYinRoster.dll
               └── Slots/                    ← 빈 디렉토리, 슬롯 자동 생성됨
   ```
3. 게임 실행. 첫 실행 시 BepInEx 가 `BepInEx/config/com.deepe.longyinroster.cfg` 자동 생성.

## 사용법

| 단축키 / 버튼 | 동작 |
|---|---|
| `F11` | 모드 창 토글 (cfg 로 변경 가능) |
| `[+] 현재 캐릭터 저장` | 선택한 슬롯에 캡처. 빈 슬롯이면 즉시, 차있으면 확인 |
| 슬롯 N 클릭 | 디테일 패널에 정보 표시 |
| `이름변경` | 슬롯 라벨 변경 |
| `메모` | 슬롯 메모 입력 |
| `×삭제` | 확인 후 슬롯 삭제 |

### 설정 (`com.deepe.longyinroster.cfg`)

| 키 | 기본값 | 설명 |
|---|---|---|
| `ToggleHotkey` | `F11` | 모드 창 토글 키 |
| `PauseGameWhileOpen` | `true` | 창 열린 동안 `Time.timeScale = 0` |
| `SlotDirectory` | `<PluginPath>/Slots` | 슬롯 파일 위치 |
| `MaxSlots` | `20` | 사용자 슬롯 수 (5~50) |
| `LogLevel` | `3` (Info) | 0=Off / 1=Error / 2=Warn / 3=Info / 4=Debug |

## 슬롯 파일 형식

`slot_NN.json` (UTF-8). 구조:

```json
{
  "_meta": {
    "schemaVersion": 1,
    "modVersion": "0.1.0",
    "slotIndex": 1,
    "userLabel": "초한월 단월검 04-28 19:53",
    "userComment": "",
    "captureSource": "live",
    "capturedAt": "2026-04-28T19:53:42.0479372+09:00",
    "summary": { "heroName": "...", "fightScore": 358333.9, "kungfuCount": 130, ... }
  },
  "player": { "heroID": 0, "heroName": "...", ... }   // HeroData JSON 원문
}
```

`player` 부분은 게임 자체 직렬화 형식 (`Save/SaveSlotN/Hero` 안의 `[heroID==0]` 객체) 과 동일.

## 알려진 한계

- **다른 mod 와 입력 충돌** — `PauseGameWhileOpen=true` 가 timeScale=0 으로 게임 input 을 차단하지만, `ModWindow.Toggle` 시점만 적용. 다른 mod 가 매 frame timeScale 을 1로 force 하면 효과 없음. v0.2 에서 Harmony patch 로 input 차단 검토.
- **IMGUI 한국어 폰트** — 게임 자체 폰트가 한국어 지원하므로 별도 처리 불필요. 다른 mod (LongYinLiZhiZhuan_Mod 의 한국어 패치) 와 호환.
- **캡처 후 수동 저장 권장** — 슬롯 파일은 `Slots/` 디렉토리에 영구 저장되지만, 게임 자체 save 는 별도. 캐릭터 진행 자체를 백업하려면 게임 F5 사용.

## 빌드 (개발자용)

```bash
cd Save/_PlayerExport
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
# 결과: BepInEx/plugins/LongYinRoster/LongYinRoster.dll 자동 배포

dotnet test    # 25 tests pass
```

`Directory.Build.props` 의 `GameDir` 환경변수로 다른 게임 위치 지정 가능.

## 핸드오프 / 다음 작업

`docs/HANDOFF.md` — Task 17 검증 결과, IL2CPP 함정 3종 (Newtonsoft type-identity / `Il2CppSystem.Generic.List<T>` IEnumerable / IMGUI strip), 다음 단계 (Task 18+ Apply 흐름).

## 라이선스

MIT (소스코드). 게임 자체 콘텐츠 / 데이터 형식의 권리는 원 개발자 소유.

## 기여

Issues / PR 환영. 새 기능 제안은 먼저 issue 로 논의.
