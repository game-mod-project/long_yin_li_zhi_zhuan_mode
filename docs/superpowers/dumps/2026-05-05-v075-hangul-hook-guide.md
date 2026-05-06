# v0.7.5 D-4 한글화 hook 가이드

날짜: 2026-05-05
대상: LongYinRoster v0.7.5 D-4 (Item 한글화 단계)
관련 분석: `2026-05-05-hangul-mod-stack-analysis.md`, `2026-05-05-hangul-modpack-bundle-analysis.md`

## 1. 자산 위치 (게임 / 통팩 모두 동일)

```
BepInEx/plugins/Data/patched/
├─ Localization.csv                 ← 메인 사전 (한자;한글, 7041 라인)
├─ Sirius_Metadata.csv              ← 메타 (구분자 @)
├─ Sirius_FamilyName.csv            ← 성씨
├─ Sirius_UIText.csv                ← UI 텍스트
├─ Sirius_Replacer.csv              ← 부분 치환 (Regex 매치)
├─ Sirius_Event.csv / Plot.csv / Tutorial.csv / GameEnd.csv
├─ Sirius_Poetry.csv / PoetryOri.csv / NameTitle.csv / ForceJob.csv
├─ Sirius_Mail.csv / SceneText.csv / CustomKungfu.csv / etc.csv     ← ModFix 추가 자산
└─ UpdateLog_KR.txt
BepInEx/extract/StringLiteral.json  ← IL2CPP literal offset 메타 (Sirius mod 전용)
```

CSV 형식: `key{separator}value` (separator 는 `;` 기본, Metadata 만 `@`). 라인이 `\n`/`\r` 포함 시 escape 된 채로 저장 → 로드 후 `Replace("\\n","\n")` 복원.

## 2. 환경별 사전 접근

### 환경 A: 통팩 단독 (Sirius mod 만)
```csharp
LongYinLiZhiZhuan_Mod.ModPatch.translateData    // patched/Localization.csv
LongYinLiZhiZhuan_Mod.ModPatch.familyName       // Sirius_FamilyName.csv
LongYinLiZhiZhuan_Mod.ModPatch.uiTextData       // Sirius_UIText.csv
LongYinLiZhiZhuan_Mod.ModPatch.replacer         // Sirius_Replacer.csv
LongYinLiZhiZhuan_Mod.ModPatch.etcText          // Sirius_etc.csv
LongYinLiZhiZhuan_Mod.ModPatch.eventText / endingText / plotText / tutorialText
LongYinLiZhiZhuan_Mod.ModPatch.poetryText / poetryOriText / nameTitle / forceJobText
LongYinLiZhiZhuan_Mod.ModPatch.newData / originalData
LongYinLiZhiZhuan_Mod.ModPatch.nameTitleRegex
```

### 환경 B: 통팩 + ModFix (현재 사용자 환경)
```csharp
LongYinModFix.TranslationData.transDict          // ★ 통합 사전 (모든 Sirius_*.csv + Localization 머지)
LongYinModFix.TranslationData.transDictIndex     // char-prefix 인덱스 (longest-match 정렬)
LongYinModFix.TranslationData.translateData      // Localization.csv (UIText/Mail/etc/Scene 머지)
LongYinModFix.TranslationData.replacerCombined   // Replacer + FamilyName 머지
LongYinModFix.TranslationData.replacerRegex      // 통합 정규식
LongYinModFix.TranslationData.nameTitleRegex
LongYinModFix.TranslationData.familyName / mailText / sceneText / etcText / nameTitle / poetryText / ...
```

ModFix 의 `LocalizationFix.LTLocalization_GetText` 는 `LTLocalization.mInstance.textData` 에 **transDict 전체를 직접 inject** (게임 자체 로컬라이즈 dict 에 한글 항목 추가). 즉 게임 코드 내부에서도 자동으로 한글 반환.

## 3. ApplyTranslation 파이프라인 참조 (ModFix `TranslationEngine.cs`)

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

## 4. IMGUI 한자 잔존 이슈

**ModFix 의 자동 변환은 UGUI Text + TMP_Text 컴포넌트에만 작동.** IMGUI (`GUILayout.Label`, `GUI.Label`) 는 후크를 거치지 않으므로 LongYinRoster 의 ContainerPanel/ItemDetailPanel 같은 IMGUI 라벨에 직접 한자가 그려짐.

해결: 표시 직전 **명시적 사전 lookup 호출** 필수.

## 5. 권장 구조 — Hybrid (자체 사전 + ModFix dict reflection fallback)

```csharp
public static class HangulDict {
    static Dictionary<string,string> _modfixDict;     // optional, reflection
    static Dictionary<string,string> _selfDict;       // 자체 로드
    static bool _initialized;

    public static string Translate(string cn) {
        if (string.IsNullOrEmpty(cn)) return cn;
        if (!_initialized) Init();

        // 1. ModFix transDict (있으면 가장 풍부)
        if (_modfixDict != null && _modfixDict.TryGetValue(cn, out var v1)) return v1;

        // 2. 자체 사전 (Sirius mod 자산 직접 로드)
        if (_selfDict != null && _selfDict.TryGetValue(cn, out var v2)) return v2;

        // 3. fallback: LTLocalization.GetText (게임 자체 dict, ModFix 가 inject 한 항목 포함)
        try {
            var r = LTLocalization.GetText(cn);
            if (r != cn) return r;
        } catch { }

        return cn;  // raw — 사전 미스
    }

    public static void Init() {
        if (_initialized) return;
        _initialized = true;

        // 자체 사전 로드 (Sirius mod 가 깔려있지 않은 경우 대비)
        _selfDict = new Dictionary<string,string>();
        LoadCsv(_selfDict, "BepInEx/plugins/Data/patched/Localization.csv", ';');
        LoadCsv(_selfDict, "BepInEx/plugins/Data/patched/Sirius_UIText.csv", ';');
        LoadCsv(_selfDict, "BepInEx/plugins/Data/patched/Sirius_etc.csv", ';');
        LoadCsv(_selfDict, "BepInEx/plugins/Data/patched/Sirius_Mail.csv", ';');     // ModFix 자산
        LoadCsv(_selfDict, "BepInEx/plugins/Data/patched/Sirius_SceneText.csv", ';'); // ModFix 자산

        // ModFix dict reflection (있으면 우선)
        try {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "LongYinModFix");
            if (asm != null) {
                var t = asm.GetType("LongYinModFix.TranslationData");
                _modfixDict = (Dictionary<string,string>)t.GetField("transDict").GetValue(null);
            }
        } catch { }

        // ModFix 가 없으면 Sirius mod 사전 시도
        if (_modfixDict == null) {
            try {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "LongYinLiZhiZhuan_Mod");
                if (asm != null) {
                    var t = asm.GetType("LongYinLiZhiZhuan_Mod.ModPatch");
                    _modfixDict = (Dictionary<string,string>)t.GetField("translateData").GetValue(null);
                }
            } catch { }
        }
    }

    static void LoadCsv(Dictionary<string,string> dict, string path, char sep) {
        if (!File.Exists(path)) return;
        try {
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int idx = line.IndexOf(sep);
                if (idx <= 0 || idx >= line.Length - 1) continue;
                var k = line.Substring(0, idx).Replace("\\n","\n").Replace("\\r","\r");
                var v = line.Substring(idx + 1).Replace("\\n","\n").Replace("\\r","\r");
                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v) && k != v)
                    dict[k] = v;
            }
        } catch { }
    }
}
```

## 6. Init 호출 시점

- BepInEx Plugin Load 의 가장 마지막 단계 또는 첫 frame Update 에서 호출
- ModFix `TranslationData.transDict` 가 채워진 뒤 (ModFix 가 자기 Load 끝나기 전에 우리 mod 가 호출하면 reflection 실패)
- 안전장치: Translate 호출 시 `_initialized` 가드 + 빈 dict 일 때 raw 반환

## 7. ItemDetailPanel 적용 패턴

```csharp
// Before
GUILayout.Label(item.cnName);  // → 한자 그대로

// After
GUILayout.Label(HangulDict.Translate(item.cnName));  // → 한글 또는 raw fallback
```

curated 필드의 라벨도 동일하게 감싸기:
```csharp
DrawField("강화", item.enhanceLv);                          // 라벨은 이미 한글 (LongYinCheat 의 정적 테이블 참조 가능)
DrawField("착용 부위", HangulDict.Translate(item.equipPos)); // 값은 변환
```

## 8. 추가 고려사항

### v0.7.5 캐릭터 정보 한글화 (선택적 확장)
LongYinRoster 가 캐릭터 이름/속성/무공명도 표시한다면 `CharacterFeature.cs` 의 정적 한글 테이블 차용:
```csharp
DefaultAttriNames[6]       = 체질/근골/내식/신법/의지/매력
DefaultFightSkillNames[9]  = 도법/검법/권장/장병/기문/사술/경공/절기/내공
DefaultLivingSkillNames[9] = 약학/의술/독학/요리/제련/제작/채집/사냥/낚시
SpeAddTypeNames[60+]       = 천공 보정 60+ 종 (근력/민첩/지력/.../학식잠재/언변잠재/...)
```
이 정적 테이블은 사전 lookup 없이 ID → 한글 즉시 변환 가능 → 성능·robust 양면에서 유리.

### 폰트
한자 ID 가 한글 폰트에 없는 경우 □ 또는 깨짐 표시. ModFix 와 LongYinCheat 의 `FontManager` 는 동적 폰트 생성 + 텍스처 재구축 콜백을 처리. LongYinRoster 가 IMGUI 에서 한글 표시할 때 폰트 미설정 시 일부 한글이 안 보일 수 있음 — Unity 기본 폰트로 fallback 되는지 in-game 검증 필수.

### 캐싱
사전 lookup 결과는 매 frame 호출되면 부담. ContainerPanel rendering 시 `item.cnName → 한글` 매핑을 row 단위로 캐싱 (cell renderer 자산 활용).

## 9. 디버깅 체크리스트

1. **사전 미스 진단**: ModFix 의 `Dump All GetText Keys` ConfigEntry 활성화 → `getTextKeyDump` 에서 누락 키 확인
2. **IMGUI 라벨 확인**: 한자 잔존 시 `Translate(cn)` 호출 누락 의심
3. **Reflection 실패 진단**: ModFix Plugin Load 후에 Init 호출되는지 확인 (BepInExDependency 또는 첫 frame 지연 사용)
4. **자체 사전 fallback 검증**: ModFix 강제 비활성화 상태에서 게임 실행 시 자체 사전만으로 한글 표시되는지 확인
5. **버전 mismatch 영향**: 통팩 1.0.1f3 ↔ 게임 1.0.0f8.2 환경에서 StringLiteral.json 패치는 무효일 수 있음. CSV 사전 lookup 은 영향 없음.
