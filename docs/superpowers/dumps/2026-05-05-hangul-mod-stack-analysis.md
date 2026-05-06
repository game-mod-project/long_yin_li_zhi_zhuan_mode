# 한글화 mod stack 분석 (Sirius + ModFix)

날짜: 2026-05-05
대상 dll:
- `BepInEx/plugins/LongYinLiZhiZhuan_Mod.dll` (메인 한글화, v0.7.6, Sirius)
- `BepInEx/plugins/LongYinModFix.dll` (보정·후처리, v3.2.0, 곰딴지)

디컴파일 결과:
- `C:/Users/deepe/AppData/Local/Temp/ly_mod_decomp/`
- `C:/Users/deepe/AppData/Local/Temp/ly_modfix_decomp/`

## stack 구조

```
LongYinLiZhiZhuan_Mod (Sirius, 한글화 본체)
   ↓ Plugin Load
   ├─ BepInEx/plugins/Data/{original,patched}/*.csv 로드
   ├─ BepInEx/extract/StringLiteral.json 로드
   ├─ StringLiteralPatcher.ApplyAll() — IL2CPP 정적 string literal 메모리 직접 교체
   └─ Harmony.PatchAll(ModPatch / SearchPatch / CheatPatch)

LongYinModFix (곰딴지, Sirius mod 위에 누적)
   ↓ Plugin Load (Sirius mod 뒤)
   ├─ Sirius mod 의 Harmony patch 일부를 unpatch
   │   ├─ GameDataController.LoadAllGameData (Postfix)
   │   ├─ GameObject.SetActive (Postfix)
   │   ├─ LTLocalization.GetText (Prefix)
   │   ├─ Text.set_text (Pre+Postfix v0.7.5 라인)
   │   └─ GlobalData.GetItemTypeString (Postfix)
   ├─ TranslationData 통합 사전 구축 (transDict)
   ├─ 자체 Harmony 후크 적용 (LocalizationFix / TextSetterPatch / ...)
   ├─ UITranslationScanner (MonoBehaviour, 매 프레임 UI 스캔)
   └─ TextureReplacer (텍스처 교체)
```

## 1. LongYinLiZhiZhuan_Mod (Sirius v0.7.6)

### Plugin Info
- GUID/Name: `LongYinLiZhiZhuan_Mod`
- 버전: 0.7.6
- 진입점: `Loader : BasePlugin` (BepInEx 6 IL2CPP)

### 자산 로드 경로
```
ModPatch.dataPath      = "./BepInEx/plugins/Data/"
ModPatch.originalPath  = dataPath + "original/"
ModPatch.patchedPath   = dataPath + "patched/"
StringLiteralPatcher.stringliteralPath = "./BepInEx/extract/StringLiteral.json"
```

### Static 사전 (Loader.Load 에서 채워짐)
```csharp
LongYinLiZhiZhuan_Mod.ModPatch.translateData    // patched/Localization.csv
LongYinLiZhiZhuan_Mod.ModPatch.familyName       // Sirius_FamilyName.csv
LongYinLiZhiZhuan_Mod.ModPatch.uiTextData       // Sirius_UIText.csv
LongYinLiZhiZhuan_Mod.ModPatch.replacer         // Sirius_Replacer.csv
LongYinLiZhiZhuan_Mod.ModPatch.etcText          // Sirius_etc.csv
LongYinLiZhiZhuan_Mod.ModPatch.eventText        // Sirius_Event.csv
LongYinLiZhiZhuan_Mod.ModPatch.endingText       // Sirius_GameEnd.csv
LongYinLiZhiZhuan_Mod.ModPatch.plotText         // Sirius_Plot.csv
LongYinLiZhiZhuan_Mod.ModPatch.tutorialText     // Sirius_Tutorial.csv
LongYinLiZhiZhuan_Mod.ModPatch.poetryText       // Sirius_Poetry.csv
LongYinLiZhiZhuan_Mod.ModPatch.poetryOriText    // Sirius_PoetryOri.csv
LongYinLiZhiZhuan_Mod.ModPatch.nameTitle        // Sirius_NameTitle.csv
LongYinLiZhiZhuan_Mod.ModPatch.forceJobText     // Sirius_ForceJob.csv
LongYinLiZhiZhuan_Mod.ModPatch.newData          // 동적 추가
LongYinLiZhiZhuan_Mod.ModPatch.originalData     // 백업
LongYinLiZhiZhuan_Mod.ModPatch.nameTitleRegex   // 정규식 (이름 칭호)
LongYinLiZhiZhuan_Mod.StringLiteralPatcher.oriMetaData    // List<StringLiteralEntry> (StringLiteral.json)
LongYinLiZhiZhuan_Mod.StringLiteralPatcher.transMetaData  // Sirius_Metadata.csv (@ 구분자)
```

### Harmony 후크 (`ModPatch.cs`)

| 후크 대상 | 패치 종류 | 용도 |
|---|---|---|
| `GameDataController.LoadAllGameData` | Postfix (×2) | 게임 데이터 로드 후 한자 → 한글 일괄 변환 |
| `LTLocalization.GetText` | Prefix | 로컬라이즈 키 lookup intercept |
| `Text.set_text` (UnityEngine.UI) | Pre/Postfix (v0.7.5 라인) | 모든 UGUI Text 표시 시점 변환 |
| `TextAsset.ToString` | Postfix | 텍스트 자산 읽기 시 변환 |
| `GlobalData.StringReplace` | Prefix | 게임 자체 치환 함수 가로채기 |
| `GlobalData.GetItemTypeString` (×2) | Postfix | 아이템 타입명 |
| `ItemData.Name` | Postfix | 아이템 이름 (디스플레이용) |
| `HeroData.HeroFamilyName` | Postfix | 캐릭터 성씨 |
| `GameController.GetHeroName` | Postfix | 캐릭터 이름 |
| `GameController.GetNewMail` | Postfix | 메일 |
| `MissionData.GetTriggerTargetDescribe` (Pre+Post) | | 미션 설명 |
| `QuickDetail.ShowBookQuickDetail` (Pre+Post) | | 비급 상세 |
| `RandomEventController/PlotController/GameResultController/TutorialController/WorldEventController/WorldPlotEventController/MissionDataController.Awake` | Postfix | 화면 진입 시 일괄 적용 |
| `PlotController.ShowPlot / ShowSinglePlot / GenerateAskPeotryPlot / AnswerMidAutumnFestivalQuestion` | Postfix | 시구·줄거리 |
| `GameObject.SetActive` | Postfix | 활성화 시점 (UI 갱신) |
| `ReadBookController.Awake` | Postfix | 비급 읽기 |
| `StaffMenuController.ShowStaffMenu / ForceDetailController.ShowForceDetail / HeroIconController.OnClick` | Postfix | 메뉴 진입 시 |
| `Application.Quit / QuickTravelUIController.HideQuickTravelUI` | Prefix | 보조 |

### StringLiteralPatcher 동작
GameAssembly.dll 베이스 + StringLiteral.json offset 으로 IL2CPP 정적 string literal 메모리 **직접 교체**:
```csharp
IntPtr ptr = gameAssemblyBase + offset;
IntPtr il2cppStr = IL2CPP.ManagedStringToIl2Cpp(translation);
il2cpp_gchandle_new(il2cppStr, false);  // GC 고정
Marshal.WriteIntPtr(ptr, il2cppStr);
```
ApplyAll 단발성 호출. 게임 빌드별로 offset 이 달라지므로 빌드 mismatch 시 무효 또는 잘못된 영역 덮어쓰기.

### CheatPatch 와 SearchPatch
- **CheatPatch**: `BepInEx/config/cheat` 파일이 존재하면 `bCheatEnabled = true`. 활성화 시 25+ 배율/플래그 ConfigEntry (ReadBookExpRatio, StudyInternal*, HorseMaxWeight*, Favor*, Storage*, Spe*, Summon*, Explorer* 등) 로드.
- **SearchPatch**: Ctrl+F 대지도 검색·텔레포트 IMGUI 윈도우.

### Util 헬퍼
```csharp
LongYinLiZhiZhuan_Mod.Util.regexCheckCN              // [一-龥]
LongYinLiZhiZhuan_Mod.Util.LoadTextFile(path, sep)   // CSV 로드
LongYinLiZhiZhuan_Mod.Util.ApplyTranslation(key, ref bApplied)
LongYinLiZhiZhuan_Mod.Util.ApplySegmentTranslation(dict, text, ref bApplied)
LongYinLiZhiZhuan_Mod.Util.ApplyNameTitle(text)
LongYinLiZhiZhuan_Mod.Util.TranslateFromDictionary(dict, text)
LongYinLiZhiZhuan_Mod.Util.CorrectPostpositions(text, ref bApplied)
LongYinLiZhiZhuan_Mod.Util.ApplyEventDataTranslation(eventData, funcName)
LongYinLiZhiZhuan_Mod.Util.Crc32Hex(input)
```

## 2. LongYinModFix (곰딴지 v3.2.0)

### Plugin Info
- GUID: `com.gomdanji.longyinmodfix`
- 버전: 3.2.0
- 진입점: `Plugin : BasePlugin`
- 의존성: `BepInDependency` (Sirius mod)
- 같은 저자가 LongYinCheat 도 만듦 (`com.gomdanji.longyincheat`)

### 자산 로드 (Plugin.Load)
Sirius 와 동일한 `Data/patched/` 디렉토리에서 추가 CSV 머지:
```
patchedPath/Localization.csv
patchedPath/Sirius_FamilyName.csv
patchedPath/Sirius_UIText.csv
patchedPath/Sirius_Replacer.csv
patchedPath/Sirius_Event.csv / GameEnd.csv / Plot.csv / Tutorial.csv / Poetry.csv / PoetryOri.csv / NameTitle.csv
patchedPath/Sirius_Mail.csv          ← ModFix 추가
patchedPath/Sirius_etc.csv
patchedPath/Sirius_SceneText.csv     ← ModFix 추가
patchedPath/Sirius_CustomKungfu.csv  ← ModFix 추가
patchedPath/Sirius_Metadata.csv      (구분자 @)
patchedPath/Sirius_ForceJob.csv
patchedPath/UpdateLog_KR.txt
```

### Static 사전 (`TranslationData.cs`)
```csharp
LongYinModFix.TranslationData.translateData      // Localization.csv (UIText/Mail/etc/Scene 머지)
LongYinModFix.TranslationData.uiTextData / familyName / replacer
LongYinModFix.TranslationData.eventText / endingText / plotText / tutorialText
LongYinModFix.TranslationData.poetryText / poetryOriText / nameTitle / mailText / etcText / sceneText
LongYinModFix.TranslationData.newData / originalData
LongYinModFix.TranslationData.nameTitleRegex / baseReplacerRegex / replacerRegex
LongYinModFix.TranslationData.replacerCombined   // Replacer + FamilyName 머지
LongYinModFix.TranslationData.transDict          // ★ 통합 사전 (모든 Sirius_*.csv + Localization 머지)
LongYinModFix.TranslationData.replacerDict
LongYinModFix.TranslationData.transDictIndex     // char-prefix 인덱스 (longest-match 정렬)
LongYinModFix.TranslationData.dumpGetTextKeys    // bool, debug
LongYinModFix.TranslationData.getTextKeyDump     // GetText 키 덤프
LongYinModFix.TranslationData.updateLogKR        // UpdateLog_KR.txt 전체 텍스트
```

`BuildTransDictIndex()`: transDict 의 key 를 첫 char 별로 그룹핑하고 길이 내림차순 정렬 → longest-match-first 룩업 가능.

### Sirius mod 의 patch unpatch (`Plugin.RemoveOriginalPatches`)
ModFix 가 Load 직후 다음 Sirius mod patch 를 모두 제거:
- `GameDataController.LoadAllGameData` Postfix → `LoadDataFix.Postfix` 로 대체
- `GameObject.SetActive` Postfix
- `LTLocalization.GetText` Prefix → `LocalizationFix` 로 대체
- `UnityEngine.UI.Text.set_text` Pre/Postfix (v0.7.5 라인) → `TextSetterPatch` 로 대체
- `GlobalData.GetItemTypeString` Postfix

이렇게 함으로써 Sirius mod 의 일부 동작을 ModFix 의 더 정교한 버전으로 갈아끼움.

### Harmony 후크 (ModFix 자체)

| 후크 대상 | 패치 | 용도 |
|---|---|---|
| `LTLocalization.GetText` | Prefix (`LocalizationFix`) | 핵심 lookup (TranslationEngine.ApplyTranslation → DirectionTextHelper → PostpositionHelper) + textData 일괄 inject |
| `UnityEngine.UI.Text.text` setter | Prefix (`TextSetterPatch`) | UGUI 텍스트 |
| `TMP_Text.text` setter | Prefix (`TextSetterPatch`) | TextMeshPro |
| `Dropdown.Show` | Postfix (`DropdownShowPatch`) | 드롭다운 표시 시 옵션 번역 |
| `GameObject.SetActive` | Postfix (`SetActivePatch`) | 활성화 시점 |
| `MissionData.GetTriggerTargetDescribe` (Pre+Post) | `MiscFixes` | 미션 |
| `GlobalData.StringReplace` | Prefix | |
| `GlobalData.GetItemTypeString` (Postfix ×2) | | |
| `GameController.GetHeroName` | Postfix | |
| `HeroData.HeroFamilyName` | Postfix | |
| `ItemData.Name` | Postfix | |
| `QuickDetail.ShowBookQuickDetail` (Pre+Post) | | |
| `RandomEventController/PlotController/GameResultController/TutorialController/WorldEventController/WorldPlotEventController/MissionDataController.Awake` | Postfix (`AwakePatches`) | |
| `PlotController.GenerateAskPeotryPlot / AnswerMidAutumnFestivalQuestion` | Postfix | |
| `SaveLoadMenuController.RefreshSlot` | Postfix | 세이브 슬롯 표시 |
| `GameController.GameStartTeleportPlayer` | Postfix (`SaveDataTranslator.GameStartPatch`) | 게임 시작 시 세이브 데이터 한자 → 한글 번역 |
| `GameController.GetNewMail` | Postfix (`MailFilterCompat`) | 메일 호환 |
| `Application.Quit / QuickTravelUIController.HideQuickTravelUI` | Prefix | 보조 |
| `InfoMenuController.ShowInfoMenu / GameTitleController.ShowStartInfoMenu` | Postfix (`UpdateLogPatch`) | UpdateLog_KR.txt 표시 |

### LocalizationFix.LTLocalization_GetText 동작
```csharp
[HarmonyPrefix]
[HarmonyPatch(typeof(LTLocalization), "GetText")]
static bool LTLocalization_GetText(string key, ref string __result) {
    if (!_textDataInjected) TryInjectTextData();
    string text = TranslationEngine.ApplyTranslation(key, ref bApplied);
    if (bApplied) text = DirectionTextHelper.Replace(text);
    __result = PostpositionHelper.CorrectPostpositions(text, ref bApplied2);
    return !(bApplied || bApplied2);  // 번역됐으면 원본 호출 skip
}
```

`TryInjectTextData()`: `LTLocalization.mInstance.textData` 에 `transDict` 전체를 직접 inject (게임 자체 로컬라이즈 dict 에 한글 항목 추가). 이로써 게임 코드 내부에서도 자동으로 한글 반환.

### TranslationEngine.ApplyTranslation 파이프라인
```
input string
  ├─ regexCheckCN ([一-龥]) 매치 없으면 return as-is (이미 한글)
  ├─ \n/\r 포함 시 escape 후 translateData lookup
  ├─ translateData[key] 정확 매치 → 반환
  ├─ TryPlaceholderLookup (한자 + ASCII 혼합)
  ├─ baseReplacerRegex 적용 (replacer 사전, longest-match 우선)
  ├─ 다시 한자 없으면 반환
  ├─ TryPlaceholderLookup 재시도
  ├─ regexDate 패턴 ("第N年M月D日") → "N년 M월 D일"
  └─ 일부 잔존 시 transDictIndex 로 char-prefix lookup
```

후처리:
- `DirectionTextHelper.Replace` (방위 한자: 内/外/東/西/南/北/上/下/中/左/右/前/後)
- `PostpositionHelper.CorrectPostpositions` (조사 `은(는)/이(가)/을(를)/와(과)` 한국어 받침 검사 후 자동 결정 — `(c - 44032) % 28 > 0` 으로 받침 유무 판정)

### TextSetterPatch (UGUI/TMP)
```csharp
[HarmonyPrefix]
public static void TMP_Text_Setter(ref string value) {
    if (string.IsNullOrEmpty(value)) return;
    value = DirectionTextHelper.Replace(value);
    if (TranslationEngine.ContainsChinese(value))
        value = TranslationEngine.Translate(value);
}
// Text_Setter 도 동일 시그니처
```
**중요**: IMGUI (`GUILayout.Label` 등) 는 이 후크를 **거치지 않음** — UGUI/TMP Text 컴포넌트만 자동 변환. LongYinRoster 의 IMGUI ContainerPanel/ItemDetailPanel 은 명시적 변환 필요.

### UITranslationScanner (MonoBehaviour)
- `LateUpdate` 매 프레임 (느려지면 2초 간격)
- Scene 변경 감지 → 즉시 재스캔
- `Object.FindObjectsOfType<Text>()` 로 모든 UGUI Text 찾아서 번역 적용
- Dropdown 별도 (4초 간격)
- 5회 연속 변경 없으면 ScanInterval 1s → 2s 로 슬로우다운

## 한글 잔존 디버깅 팁

1. `Verbose Translation Log` ConfigEntry 활성화 → `[PreReplacer]/[PostReplacer]` 로그
2. `Dump All GetText Keys` ConfigEntry 활성화 → `getTextKeyDump` 사전에 미번역 키 누적
3. IMGUI 라벨인 경우 자동 변환 안 됨 → 명시적 `LTLocalization.GetText(cn)` 또는 자체 사전 lookup 필요

## v0.7.5 D-4 작업에 대한 영향

- **사전 hook 대상**: ModFix 가 깔린 환경에서는 `LongYinModFix.TranslationData.transDict` 가 가장 풍부 (모든 Sirius_*.csv 머지). Sirius 단독 환경에서는 `LongYinLiZhiZhuan_Mod.ModPatch.translateData` + 13개 보조 사전 개별 lookup.
- **IMGUI 한자 잔존 이슈**: ModFix 의 `TextSetterPatch` 는 UGUI/TMP 만 적용 → LongYinRoster IMGUI 패널은 자체 사전 lookup 필수.
- **권장**: 자체 사전 + ModFix dict reflection fallback hybrid 구조 (별도 가이드 문서 참조).
