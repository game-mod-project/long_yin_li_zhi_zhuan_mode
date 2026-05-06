# LongYinCheat.dll 분석 (v1.4.7)

날짜: 2026-05-05
경로: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinCheat.dll`
디컴파일 결과: `C:/Users/deepe/AppData/Local/Temp/longyincheat_decomp/` (30,214 LOC, 39 파일)

## 기본 정보

| 항목 | 값 |
|------|------|
| Plugin GUID | `com.gomdanji.longyincheat` |
| 이름 | LongYin InGame Cheat |
| 버전 | 1.4.7 |
| 저자 | 곰딴지 (창 제목 "치트 메뉴 [F10] v1.4.7 by 곰딴지") |
| 크기 | 406 KB |
| 빌드 | PE32 .NET assembly, 83 타입, .NET 6 / Roslyn |
| 베이스 | BepInEx 6 IL2CPP + Harmony 2.10.2 + Il2CppInterop 1.5.1 |
| 언어 | 한국어 UI 전용 |
| 의존성 | `BepInDependency` (디컴파일 attribute decode 실패 — 추정: LongYinLiZhiZhuan_Mod 또는 LongYinModFix) |

## 아키텍처

```
CheatPlugin (BasePlugin)              ← BepInEx 진입점, ~80개 ConfigEntry
 ├─ CheatKeyHandler (MonoBehaviour)   ← Update/OnGUI 후크
 │   ├─ F10 → CheatUI.Toggle
 │   ├─ Ctrl+Backspace → 기타 탭 리셋
 │   ├─ Ctrl+R → AuctionFeature.RefreshAuction
 │   └─ Ctrl+T → TreasureMapFeature.RevealTreasureMap
 ├─ CheatUI (6352 LOC IMGUI)          ← 14 탭 윈도우
 ├─ CheatPanels (5653 LOC)            ← 6개 별도 패널 (아이템/무학/속성/장비/태그/NPC편집)
 └─ PlayerAccess                      ← GameDataController/GameController/HeroData 헬퍼
```

**14 탭**: 기본 / 스탯 / 수치 / 아이템 / 무학 / 전투 / 문파 / NPC / 경매 / 리롤 / 사택 / 외부모드 / 기타 / 설정

## Harmony 패치 (Patches/, 11종)

| 패치 | 역할 |
|------|------|
| **CombatPatch** | GodMode (`BattleController.ManageDamage` Prefix), OneHitKill, 데미지 멀티플라이어 (입힘/받음), 전투 보상 모드, 무한 마나/기력/행동/이동, 노쿨다운 |
| **GameplayPatch** | 호감도 감소 차단, 최대 호감도/태그수 override, 이동속도 배율, 즉시 탐사 종료, 무한 탐사 걸음수, 안개 제거 + 시야 범위, 대지도 텔레포트, 비밀지도 핫키, 감별 정답 자동 표시, 외부창고 용량 override (기본 24000), 실외 창고 백업/복원 |
| **MultiplierPatch** | 호감도/전투EXP/생활EXP 배율 + Player/Both/None scope, 돌파점수율, 제련 성공률, 속성 999 캡 해제 (`UncapMaxAttri/FightSkill/LivingSkill`), HorseData see-range/step-rate override |
| **InputBlockPatch** | UI 열렸을 때 `GameController.Update`, `BigMapController.Update`, `Input.GetKey*`, `GlobalData.GetKey*`, 마우스 버튼/축, `UICamera.*` 모두 차단 — 단 KeyCode 8/114/116/291/305/306 (Backspace, R, T, F10, Ctrl) 만 살림 |
| **CityHouseExtensionPatch** | 사택 빌드 메뉴 확장/순서 변경 |
| **DiscipleRuleUnlockPatch** | 제자 규칙 잠금 해제 |
| **MusicKungfuToQimenPatch** | 음악 무공을 기문 무공으로 분류 변환 |
| **TextRenderingPatch** | UI 텍스트 자간/베이스라인/라인높이 조정 + 건물명 복원 (1057 LOC) |
| **UpdateLogPatch** | 한글 업데이트 로그 주입 |
| **LetterSpacing / MultiplierScope** | 보조 enum / 헬퍼 |

## Features (Features/, 22종)

| 모듈 | 역할 |
|------|------|
| **ItemGenerator** | 12 카테고리 아이템 생성 (무기/갑옷/투구/신발/장신구/비급/단약/음식/말/재료/보물/랜덤) — `GameController.Generate*` 직접 호출 |
| **CharacterFeature** (2069 LOC) | NPC 캐싱, 호감도/태그/대화 정보, 속성/무공명 lookup |
| **SkillManager** (1202 LOC) | 무공 학습/장착/제거, 비급 학습 (소비 옵션), 무공 레벨 조정 |
| **StatEditor** | 스탯 편집 + `EnforceLocks()` 매 프레임 잠금 |
| **BattleFeature** | 즉시 승리, 아군 전체 회복, 전투 스킵 토글 |
| **AuctionFeature** | 경매 새로고침 (Ctrl+R) |
| **TreasureMapFeature** | 보물지도 마스킹 해제 (Ctrl+T) |
| **CraftReroll / SpePoisonReroll** | 제작/독 결과 리롤 (드래그 가능 인게임 버튼) |
| **InGameRerollButtons** | 6종 리롤 버튼 (제작-무기/약/음식/돌파/독채집/독합성) 위치 좌표 영속화 |
| **StorageCapacity** | 외부 창고 용량 override |
| **CheatProfiles** | 모든 멀티플라이어/플래그를 프로필로 저장/로드 |
| **SaveDataSanitizer** | NaN/Infinity/초과값 자동 보정 (자원 9999999, 스탯 99999, 비율 9999 클램프) |
| **AdvancedModSync** | 별도 모드 `LongYinLiZhiZhuan_Advanced` 의 BreakThrough/MaxRatio 필드를 reflection 으로 동기화 |
| **TranslationHelper / TranslationReport** | `Localization.csv` 기반 한자→한글 번역, 13종 방위 한자 자동 매핑 (内/外/東/西/南/北/上/下/中/左/右/前/後), 누락 리포트 |
| **FontManager** (4560 LOC) | 동적 폰트 생성, IL2CPP 텍스처 재구축 콜백, 자동 적용 |
| **IconHelper / IdentifyInfo / TreasureMapInfo** | UI 보조 |
| **FontOverrideLabelPatch / UILabelOnEnablePatch / UILabelOnStartPatch / ItemFeature** | 보조 패치 |

## 영속화

- **BepInEx config**: 약 50+ ConfigEntry — `CfgGodMode`, `CfgOneHitKill`, ..., `CfgRerollPos*X/Y` 위치 좌표, `CfgCityHouseEnabled/Order` (직렬화 문자열)
- **`BepInEx/config/LongYinCheat_ui.cfg`** — UI 상태 (창 크기, 알파, 폰트 배율)
- **`BepInEx/config/LongYinCheat_panels.cfg`** — 패널 상태
- **CheatProfiles** — 멀티플라이어/플래그 프로필

## 보안·동작 특이사항

1. **InputBlock 광범위** — UI 열렸을 때 `UICamera.Update/ProcessMouse/ProcessTouches/LateUpdate` 가 *항상* 차단 (`return true` 의 prefix → 항상 false 반환). 즉 UICamera 는 **상시** 비활성화 상태. 기존 NGUI 입력 처리를 죽이고 IMGUI 단일 처리로 가는 구조.
2. **EventSystem.enabled = false** — UI 열림 시 Unity EventSystem 도 비활성화
3. **Il2CppObjectBase.Pointer 키 dictionary** — `MultiplierPatch._seeRangeOverrides`/`_stepRateOverrides` — IL2CPP wrapper 동일 객체에 다른 wrapper 가 생기는 이슈 회피용 pointer 기반 lookup (사용자 mod 의 동일 패턴과 일치)

## 사용자 mod (LongYinRoster) 와의 비교 관점

- **양립성**: LongYinCheat 의 InputBlock 은 `CheatUI.IsOpen` 가드라서 다른 IMGUI mod 가 자체 토글로 작동할 때만 입력 차단. LongYinRoster 의 IMGUI ContainerPanel 도 동일 패턴이면 충돌 없음.
- **번역 충돌 가능성**: `TextRenderingPatch.RestoreBuildingNames`, `TranslationHelper`, `FontManager` 가 광범위 텍스트 후처리. v0.7.5 한글화 작업 시 동일 라벨 후크하면 우선순위/순서 충돌 검토 필요.
- **공통 패턴 확인**: `PlayerAccess` (`GameDataController.Instance` → `GC.worldData` → `Player()` → `itemListData`) — 사용자 mod 의 동일 접근 경로와 1:1 일치. 이 helper 형태를 LongYinRoster 도 차용 가능.
- **ItemGenerator.Generate** — `gC.GenerateBook/GenerateMedData/GenerateHorseData/GenerateMaterial/GenerateTreasure` 시그니처가 v0.7.4.x patch (말/보물/재료 curated) 작성 시 참고 가능.
