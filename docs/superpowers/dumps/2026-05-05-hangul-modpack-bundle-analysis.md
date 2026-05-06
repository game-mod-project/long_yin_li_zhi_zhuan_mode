# 한글모드 통팩 분석 (`E:\LongYinLiZhiZhuan_한글모드통팩`)

날짜: 2026-05-05
경로: `E:\LongYinLiZhiZhuan_한글모드통팩\`

## 통팩 = "한글모드 + sinai ConfigManager + UniverseLib + 번역 자산" 번들

### 루트 구성
```
.doorstop_version
BepInEx/                ← 핵심
changelog.txt           ← BepInEx v6.0.0-pre.2 changelog (한글모드 changelog 아님)
doorstop_config.ini
dotnet/
winhttp.dll             ← BepInEx Doorstop
```

### BepInEx 디렉토리
```
BepInEx/
├─ config/
│   └─ BepInEx.cfg
├─ core/
├─ extract/
│   └─ StringLiteral.json       ← Sirius IL2CPP literal 메타
├─ interop/
├─ patchers/
│   └─ BepInExConfigManager.Il2Cpp.Patcher.dll  (sinai, 6,144 B)
├─ plugins/
│   ├─ BepInExConfigManager.Il2Cpp.CoreCLR.dll  (sinai, 54,272 B)
│   ├─ LongYinLiZhiZhuan_Mod.dll                (Sirius v0.7.6, 84,480 B)
│   ├─ UniverseLib.BIE.IL2CPP.Interop.dll       (2,882,048 B)
│   ├─ version.ini                              ("1.0.1f3")
│   └─ Data/                                    ← 번역 자산
│       ├─ original/                            (35 파일, 원본 백업)
│       └─ patched/                             (36 파일, 한글화 적용본)
└─ unity-libs/
```

## 주요 파일 동일성 (통팩 vs 게임 설치본)

| 컴포넌트 | 통팩 | 게임 설치본 | 일치 |
|----------|------|-----------|------|
| `LongYinLiZhiZhuan_Mod.dll` (v0.7.6) | 84,480 B (Apr 28 22:12) md5 `88638e...` | 83,456 B (Apr 22 21:53) md5 `7cc891...` | ❌ 다른 빌드 |
| `BepInExConfigManager.Il2Cpp.CoreCLR.dll` | 54,272 B md5 `3432286a...` | 동일 md5 | ✅ |
| `BepInExConfigManager.Il2Cpp.Patcher.dll` | 6,144 B | 동일 | ✅ |
| `UniverseLib.BIE.IL2CPP.Interop.dll` | 2,882,048 B | (interop/ 또는 부재) | — |
| `LongYinModFix.dll` | **없음** | 있음 (v3.2.0 곰딴지) | — |
| `LongYinCheat.dll` | **없음** | 있음 (v1.4.7 곰딴지) | — |

## 핵심 결론

1. **통팩 = Sirius 한글모드 단독 배포본** + sinai ConfigManager 도구. `LongYinModFix.dll` (곰딴지 v3.2.0) 와 `LongYinCheat.dll` 은 통팩에 **포함되지 않음** — 사용자가 별도로 추가한 것.

2. **`BepInExConfigManager.Il2Cpp.Patcher.dll` 은 통팩에 동봉되어 있지만 한글화 mod 가 아님**. sinai 의 BepInEx config UI 도구로, 한글모드의 BepInEx config 값을 게임 안에서 GUI 로 편집할 수 있게 해주는 보조 도구. 한글 통팩에 함께 패키징된 이유로 오해 가능 — 하지만 한글화 본체는 `LongYinLiZhiZhuan_Mod.dll`.

3. **`changelog.txt` 는 한글모드 changelog 가 아니라 BepInEx 본체 v6.0.0-pre.2 의 47-change changelog**. ManlyMarco/aldelaro5 커밋. 한글모드 변경 이력은 들어있지 않음.

## ⚠️ 게임 버전 불일치 위험

- 통팩 `version.ini` = **`1.0.1f3`**
- 게임 폴더 = **`v1.0.0f8.2`** (`E:/Games/龙胤立志传.v1.0.0f8.2/`)

**StringLiteralPatcher 는 `GameAssembly.dll 베이스 + StringLiteral.json offset` 으로 IL2CPP 정적 문자열 메모리에 직접 쓰기**를 하기 때문에 빌드 offset 이 1바이트라도 어긋나면 잘못된 주소를 덮어쓰거나 fail-silent 합니다. 통팩이 1.0.1f3 용으로 빌드된 `StringLiteral.json` 을 1.0.0f8.2 게임에 적용하면:
- ✅ Harmony patch (LoadAllGameData, GetText 등) 는 메소드 시그니처 매칭이라 작동
- ✅ CSV 사전 lookup 도 작동
- ❌ **StringLiteral.json offset 패치는 무효 / 잘못된 영역 덮어쓰기 가능** → 일부 한자 잔존 또는 random crash

게임 설치본의 `LongYinLiZhiZhuan_Mod.dll` 이 다른 빌드라는 점은 **통팩 적용 후 사용자가 mod 만 별도 업데이트했거나, 통팩 적용 전 게임 빌드용으로 다운그레이드했을 가능성**입니다.

## Data 자산 — patched 36개 / original 35개

```
patched (한글화 적용본)                  original (원본 백업)
─────────────────────────────────────   ─────────────────────────
AchievementData.csv                     AchievementData.csv
ArmorData.csv                           AreaData.csv          ←patched 없음
BuildingData.csv                        ArmorData.csv
FoodData.csv                            BookTypeIconData.csv  ←patched 없음
ForceData.csv                           BuildingData.csv
GlobalData.txt          ←original 없음  FoodData.csv
HeroNatureTalkText.csv                  ForceData.csv
HeroSpeTalkText.csv                     ForceSpeAddDataBase.csv ←patched 없음
HeroTagData.csv                         HeroNatureTalkText.csv
HorseData.csv                           HeroSpeTalkText.csv
InnData.csv                             HeroTagData.csv
KungFuData.csv                          HorseData.csv
Localization.csv  (사전 본체)           InnData.csv
LoveableSpeHero.csv                     KungFuData.csv
MedData.csv                             Localization.csv
NameData.csv                            Localization_new.csv  ←patched 없음
PlotData.csv                            LoveableSpeHero.csv
PoetryData.csv                          MartialClubData.csv   ←patched 없음
Sirius_Event.csv         ←Sirius 시리즈 MedData.csv
Sirius_FamilyName.csv     (한글화 전용  NameData.csv
Sirius_ForceJob.csv        후처리 사전) OriginalText.csv      ←patched 없음
Sirius_GameEnd.csv                      PlotData.csv
Sirius_Metadata.csv                     PoetryData.csv
Sirius_NameTitle.csv                    ResourcePointData.csv ←patched 없음
Sirius_Plot.csv                         ResourcePointTypeData.csv ←patched 없음
Sirius_Poetry.csv                       Sirius_UIText.csv
Sirius_PoetryOri.csv                    SkinDataBase.csv
Sirius_Replacer.csv                     SpeAddDataBase.csv    ←대소문자
Sirius_Tutorial.csv                     SpeHeroData.csv       ←patched 없음
Sirius_UIText.csv                       SpeHeroFaceData.csv   ←patched 없음
Sirius_etc.csv                          SummonData.csv        ←patched 없음
SkinDataBase.csv                        SummonKungFuData.csv
SummonKungFuData.csv                    TechDataBase.csv
TechDataBase.csv                        TipsData.csv
TipsData.csv                            WeaponData.csv
WeaponData.csv
speAddDataBase.csv
```

- **patched 전용**: `GlobalData.txt`, `Sirius_*` 13개 — Sirius (한글모더) 가 추가한 보조 사전
- **original 전용**: `AreaData/BookTypeIconData/ForceSpeAddDataBase/Localization_new/MartialClubData/OriginalText/ResourcePointData/ResourcePointTypeData/SpeHeroData/SpeHeroFaceData/SummonData` — 한글화 안 됨 (또는 1.0.1f3 에서 게임 데이터 구조 변경으로 추가된 파일을 통팩이 처리 안 함)
- **`speAddDataBase.csv` (소문자) vs `SpeAddDataBase.csv` (대문자)** — 대소문자 차이. 게임 빌드 변경 흔적

## v0.7.5 D-4 한글화 작업에 대한 영향

- **통팩 사용자가 다수**: 사용자 mod 의 한글화 hook 은 `LongYinLiZhiZhuan_Mod.dll` v0.7.6 단독 stack (ModFix 없음) 도 지원해야 함
- **사전 hook 대상**: 통팩 단독에서는 `ModPatch.translateData / familyName / uiTextData / replacer / etcText / eventText / endingText / plotText / tutorialText / poetryText / nameTitle / forceJobText` 13개 `Dictionary<string,string>` (모두 `LongYinLiZhiZhuan_Mod.ModPatch` static field). ModFix 가 추가로 깔린 환경에서는 `LongYinModFix.TranslationData.transDict` (통합 사전) 가 더 풍부.
- **자체 사전 + Sirius_*.csv 직접 로드 권장**: load 순서 무관 + ModFix 유무 무관 + 통팩 단독 / 통팩+ModFix / 게임 설치본의 더 오래된 mod 빌드 모두 동일하게 작동.
