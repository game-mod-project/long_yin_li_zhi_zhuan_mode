# LongYinRoster v0.7.5 — D-4 Item 한글화

**일시**: 2026-05-06
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.2
**baseline**: v0.7.4.1 (commit `3987abe`) — 193/193 tests + 12/12 smoke PASS
**design input**:
- [`dumps/2026-05-05-v075-hangul-hook-guide.md`](../dumps/2026-05-05-v075-hangul-hook-guide.md) (203 LOC) — Hybrid 자체사전+ModFix reflection fallback 패턴
- [`dumps/2026-05-05-hangul-mod-stack-analysis.md`](../dumps/2026-05-05-hangul-mod-stack-analysis.md) — Sirius v0.7.6 + ModFix v3.2.0 stack
- [`dumps/2026-05-05-hangul-modpack-bundle-analysis.md`](../dumps/2026-05-05-hangul-modpack-bundle-analysis.md) — 통팩 단독 vs ModFix 추가 환경 차이

## 0. 한 줄 요약

신규 `HangulDict` static class — Hybrid 한자→한글 사전 (자체 CSV + ModFix `TranslationData.transDict` reflection + `LTLocalization.GetText` fallback). ContainerPanel + ItemDetailPanel 의 IMGUI 라벨에서 한자 노출 제거. UGUI 가 아니라 ModFix 자동 변환이 안 닿는 IMGUI 라벨이 대상.

## 1. 결정 사항

| 항목 | 결정 | 메타 spec / 근거 |
|---|---|---|
| Hook 전략 | **(A) Hybrid** — 자체 사전 우선 + ModFix reflection 보강 + `LTLocalization.GetText` 최후 fallback | 메타 §2.2 default. 통팩 단독 환경 (ModFix 없음) 도 자체 CSV 로 robust 작동 |
| Init timing | **Lazy on first Translate call** | ModFix load 순서 무관 + 한 번만 비용 |
| 변환 적용 시점 | **Eager at row build** (`ItemRow.NameKr`) + **display-time** (curated/raw value) | row build 캐시로 매 frame translate 회피. curated/raw 는 display 직전 |
| 검색 동작 | **Bilingual** — `NameKr` (있으면) OR `NameRaw` 둘 다 substring 매치 | 한국어 검색 + 한자 검색 호환 |
| 정렬 (`SortKey.Name`) | `NameKr ?? NameRaw` 우선 → 한글 자모순 자연 | 한국어 사용자 UX |
| 사전 미스 fallback | 원본 그대로 반환 (no exception, no toast) | 일부 한자 잔존 허용 |
| `ItemCellRenderer` 카테고리 글리프 (装/书/药 등) | **변경 안 함** — 의도된 compact visual ID | scope out |
| 음식 vs 단약 (subType) 분리 | **out-of-scope** — 별도 sub-project | 메타 §2.2 다음 후보 |
| 장비 attriType / littleType 한글 매핑 | **out-of-scope** — v0.7.5 에선 raw 표시 그대로 | 별도 dictionary 작업 |

## 2. 한자 노출 지점 (변경 대상 5곳)

| # | 파일 | 라인 | 현재 | 변경 |
|---|---|---|---|---|
| 1 | `Containers/ContainerRowBuilder.cs` | 38, 102 (Name) / 45, 109 (NameRaw) | `Name = name` (Chinese) | `Name = HangulDict.Translate(name)`, `NameKr = same`, `NameRaw = name` (raw 유지) |
| 2 | `UI/ContainerPanel.cs` | `BuildLabel(r)` | `r.Name` (Chinese) | `r.NameKr ?? r.NameRaw` |
| 3 | `UI/ItemDetailPanel.cs` | 82, 86 | `ItemReflector.GetNameRaw(raw)` (Chinese) | `HangulDict.Translate(GetNameRaw(raw))` |
| 4 | `UI/ItemDetailPanel.cs` | 98 | curated value | `HangulDict.Translate(value)` (label 은 이미 한글) |
| 5 | `UI/ItemDetailPanel.cs` | 110 | raw fname/value | `HangulDict.Translate(value)` (fname 은 영어 필드명, 변경 안 함) |

`ItemCellRenderer` 의 카테고리 글리프 (装/书/药) 와 메뉴 KoreanStrings (이미 한글) 는 변경 안 함.

## 3. 신규 파일

### 3.1 `src/LongYinRoster/Core/HangulDict.cs`

```csharp
public static class HangulDict
{
    private static Dictionary<string, string>? _modfixDict;     // reflection (LongYinModFix)
    private static Dictionary<string, string>? _siriusDict;     // reflection (LongYinLiZhiZhuan_Mod)
    private static Dictionary<string, string>? _selfDict;       // self-loaded CSV
    private static bool _initialized;
    private static readonly object _lock = new();

    public static int LoadedCount => _selfDict?.Count ?? 0;
    public static bool ModFixAvailable => _modfixDict != null;
    public static bool SiriusAvailable => _siriusDict != null;
    public static bool IsInitialized => _initialized;

    public static string Translate(string? cn)
    {
        if (string.IsNullOrEmpty(cn)) return cn ?? "";
        EnsureInitialized();
        // 1. ModFix transDict (가장 풍부)
        if (_modfixDict != null && _modfixDict.TryGetValue(cn, out var v1)) return v1;
        // 2. Sirius mod ModPatch.translateData
        if (_siriusDict != null && _siriusDict.TryGetValue(cn, out var v2)) return v2;
        // 3. 자체 CSV
        if (_selfDict != null && _selfDict.TryGetValue(cn, out var v3)) return v3;
        // 4. LTLocalization.GetText (게임 자체 + ModFix injected)
        try
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("LTLocalization"))
                .FirstOrDefault(x => x != null);
            if (t != null)
            {
                var m = t.GetMethod("GetText", BindingFlags.Static | BindingFlags.Public);
                if (m != null)
                {
                    var r = m.Invoke(null, new object[] { cn }) as string;
                    if (!string.IsNullOrEmpty(r) && r != cn) return r!;
                }
            }
        }
        catch { /* swallow */ }
        return cn;   // raw fallback
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            _initialized = true;
            try { _modfixDict = TryLoadModFix(); } catch { }
            try { _siriusDict = TryLoadSirius(); } catch { }
            try { _selfDict   = LoadSelfCsv(); } catch { }
        }
    }

    private static Dictionary<string,string>? TryLoadModFix()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "LongYinModFix");
        if (asm == null) return null;
        var t = asm.GetType("LongYinModFix.TranslationData");
        if (t == null) return null;
        var f = t.GetField("transDict", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return f?.GetValue(null) as Dictionary<string,string>;
    }

    private static Dictionary<string,string>? TryLoadSirius()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "LongYinLiZhiZhuan_Mod");
        if (asm == null) return null;
        var t = asm.GetType("LongYinLiZhiZhuan_Mod.ModPatch");
        if (t == null) return null;
        var f = t.GetField("translateData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return f?.GetValue(null) as Dictionary<string,string>;
    }

    private static Dictionary<string,string> LoadSelfCsv()
    {
        var dict = new Dictionary<string,string>(8192);
        var basePath = "BepInEx/plugins/Data/patched";
        string[] files = {
            "Localization.csv", "Sirius_UIText.csv", "Sirius_etc.csv",
            "Sirius_Mail.csv", "Sirius_SceneText.csv"
        };
        foreach (var f in files) LoadCsvInto(dict, Path.Combine(basePath, f), ';');
        return dict;
    }

    private static void LoadCsvInto(Dictionary<string,string> dict, string path, char sep)
    {
        if (!File.Exists(path)) return;
        try
        {
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int idx = line.IndexOf(sep);
                if (idx <= 0 || idx >= line.Length - 1) continue;
                var k = line.Substring(0, idx).Replace("\\n", "\n").Replace("\\r", "\r");
                var v = line.Substring(idx + 1).Replace("\\n", "\n").Replace("\\r", "\r");
                if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v) && k != v) dict[k] = v;
            }
        }
        catch { /* swallow */ }
    }
}
```

### 3.2 `src/LongYinRoster.Tests/HangulDictTests.cs`

테스트 패턴 (POCO + reflection 우회):
- `Translate_Empty_ReturnsEmpty`
- `Translate_Null_ReturnsEmpty`
- `Translate_HitInSelfDict_ReturnsKr`
- `Translate_Miss_ReturnsRaw`
- `Translate_AlreadyKorean_ReturnsAsIs`
- `EnsureInitialized_Idempotent`
- `LoadCsvInto_Skips_Blank_And_Malformed_Lines` (private 테스트 — InternalsVisibleTo via reflection)
- `LoadCsvInto_Unescapes_NewlineEscapes`
- `LoadedCount_ReflectsSelfDictSize`

ModFix/Sirius reflection path 는 통합 테스트 어려움 (실제 mod 가 로드되어야 함) — 단위 테스트는 자체 사전 + fallback 파트만 커버. ModFix/Sirius 실제 path 는 인게임 smoke 에서 검증.

LoadCsvInto / LoadSelfCsv 가 private 라 테스트하기 위해 옵션:
1. `internal` 로 변경 + `InternalsVisibleTo("LongYinRoster.Tests")` 활용 (이미 csproj 에 설정됨)
2. 별도 `LoadCsvLines(IEnumerable<string> lines, char sep, Dictionary<string,string> dict)` 헬퍼 추출

**채택**: (2) 헬퍼 추출 — 테스트 가능 + 단순.

## 4. 변경 파일

### 4.1 `src/LongYinRoster/UI/ContainerPanel.cs`

`ItemRow` struct/class 에 `NameKr` field 추가:
```csharp
public string? NameKr;   // v0.7.5 — translated display name. null 이면 NameRaw 사용
```

`BuildLabel(r)` — `r.Name` 대신 `r.NameKr ?? r.NameRaw` 사용 (정확한 형식은 기존 코드 유지하되 name 출처만 교체).

### 4.2 `src/LongYinRoster/Containers/ContainerRowBuilder.cs`

Both `FromJsonArray` and `FromGameAllItem` — row 생성 시 `NameKr = HangulDict.Translate(name)` 할당:
```csharp
list.Add(new ContainerPanel.ItemRow
{
    ...
    Name         = name,                                // v0.7.5 — raw 유지 (회귀 안전)
    NameRaw      = name,
    NameKr       = HangulDict.Translate(name),          // v0.7.5 — 신규 (한자 미스 시 raw 동일)
    ...
});
```

### 4.3 `src/LongYinRoster/Containers/ContainerView.cs`

검색 — bilingual:
```csharp
q = q.Where(r =>
    ((r.NameKr  ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
 || ((r.NameRaw ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0));
```

정렬 (`SortKey.Name`) — Korean-aware:
```csharp
SortKey.Name => q.OrderBy(r => r.NameKr ?? r.NameRaw ?? "").ThenBy(r => r.Index),
```

### 4.4 `src/LongYinRoster/UI/ItemDetailPanel.cs`

```csharp
// line 82 변경
string name = HangulDict.Translate(ItemReflector.GetNameRaw(raw));
// line 98 변경
GUILayout.Label($"  {label}: {HangulDict.Translate(value)}");
// line 110 변경
GUILayout.Label($"  {fname}: {HangulDict.Translate(value)}");
```

### 4.5 `src/LongYinRoster/Plugin.cs`

`Load()` 끝에 — 진단 정보 1줄 (실제 dict 로딩은 lazy):
```csharp
// v0.7.5 — HangulDict 진단 로그 (lazy init 이라 첫 호출 시 실제 로딩)
Logger.Info("[v0.7.5] HangulDict: lazy init on first Translate() call");
```

## 5. Test 변경 명세

### 5.1 신규 unit tests (`HangulDictTests.cs`, ~15 case)

위 §3.2 항목들 모두. fake 자체 dict 주입을 위해 internal/test 헬퍼 또는 정적 dict 의 `Reset` API 추가.

`HangulDict.ResetForTests()` internal method — 테스트마다 깨끗한 상태.

### 5.2 기존 ContainerView 테스트 갱신 (있으면)

`ContainerViewTests` 가 있으면 — bilingual 검색 / Korean sort 테스트 케이스 추가. 없으면 신규 작성.

### 5.3 ContainerRowBuilder 테스트 갱신

`ContainerRowBuilderTests` 가 있으면 — `NameKr` field assertion 추가. fake `HangulDict` 동작 확인.

### 5.4 회귀

기존 unit tests (193) PASS 유지.

총합: **193 → ~210** (HangulDict 15 + ContainerView 2-3 + ContainerRowBuilder 1-2).

## 6. 인게임 Smoke

### 신규 8 시나리오

| # | 시나리오 | 기대 |
|---|---|---|
| S1 | 통팩+ModFix 환경 인벤 toggle 라벨 | item 이름 한글 표시 |
| S2 | 통팩 단독 (ModFix 임시 비활성) 인벤 | item 이름 한글 표시 (자체 CSV fallback) |
| S3 | 인벤 검색 — 한글 키워드 | 한글 매치 항목 필터 |
| S4 | 인벤 검색 — 한자 키워드 | 한자 매치 항목 필터 (회귀 호환) |
| S5 | 인벤 정렬 SortKey.Name | 한국어 자모 순 |
| S6 | ItemDetailPanel header | 한글 이름 표시 |
| S7 | ItemDetailPanel curated value | 가능한 한자 값 한글 변환 |
| S8 | ItemDetailPanel raw fields | 가능한 한자 값 한글 변환, 미스는 한자 그대로 |

### 회귀 6

v0.7.4.1 의 6/6 smoke 그대로 유지 + 7 카테고리 ItemDetailPanel curated 표시.

총 14/14.

Smoke dump: `docs/superpowers/dumps/2026-05-06-v0.7.5-smoke-results.md`

## 7. Release & Cycle 정합 (메타 §5.1)

| 단계 | 산출물 |
|---|---|
| 1. brainstorm | 본 spec (commit 후 종료) |
| 2. spec | 본 파일 |
| 3. plan | `docs/superpowers/plans/2026-05-06-longyin-roster-mod-v0.7.5-plan.md` |
| 4. impl | `src/LongYinRoster/Core/HangulDict.cs` (신규) + `Plugin.cs` / `ContainerRowBuilder.cs` / `ContainerView.cs` / `ContainerPanel.cs` / `ItemDetailPanel.cs` 수정 + 테스트 |
| 5. smoke | `dumps/2026-05-06-v0.7.5-smoke-results.md` (14/14) |
| 6. release | VERSION 0.7.4.1 → 0.7.5 + README + HANDOFF + 메타 spec §2.2 ✅ + tag v0.7.5 + GitHub release |

### handoff 갱신
`HANDOFF.md` baseline → v0.7.5, 다음 sub-project = v0.7.6 설정 panel.

### 메타 spec 갱신
§2.2 "확정 sub-project" → "✅ 완료 (v0.7.5, 2026-05-06)" + Result 섹션.

## 8. Out-of-scope

- **음식 vs 단약 분리** — 별도 sub-project (또는 v0.7.6 설정 panel 시점에 ItemCategoryFilter 와 함께)
- **장비 attriType / littleType 한글 매핑** — v0.7.5 raw 그대로 표시. 별도 dictionary 작업 필요
- **`ItemCellRenderer` 카테고리 글리프** — 의도된 compact visual ID, 변경 안 함
- **사전 reload** — runtime 동안 사전 갱신 안 함 (CSV 파일 변경 시 재시작 필요)
- **ContainerRepository / ContainerOps 의 toast 메시지** — 이미 한국어 (변경 안 함)

## 9. Risk

- **ModFix `transDict` field signature 변경** — `LongYinModFix v3.2.0` 가 다른 버전으로 update 되면 reflection field name 다를 수 있음. fallback (자체 CSV + LTLocalization) 로 흡수.
- **자체 CSV 파일 부재** — Sirius mod 미설치 시 `BepInEx/plugins/Data/patched/Localization.csv` 가 없을 수도. `File.Exists` 체크 + try/catch 로 silent fallback.
- **lock 경합** — `EnsureInitialized` 의 `lock(_lock)` 은 첫 호출 한 번만 fire. Translate 자체는 lock-free (dict read-only).
- **dict mutation race** — ModFix/Sirius 의 dict 가 plugin load 중 mutate 될 가능성. `Dictionary.TryGetValue` 는 race 시 예외 가능 → try/catch 로 감싸기 (HangulDict.Translate 의 try/catch).
- **culture 의존 sorting** — `OrderBy(r => r.NameKr)` 가 default culture 사용. xUnit 환경에서 ko-KR 자모순 결정성 — invariant culture 강제 필요 시 추가 조정.

## 10. 참고 자산

- 메타 로드맵 §2.2: 본 sub-project 의 1-pager
- `dumps/2026-05-05-v075-hangul-hook-guide.md`: §1~9 의 모든 design input (특히 §5 Hybrid 권장 구조 는 본 spec 과 1:1)
- `dumps/2026-05-05-hangul-mod-stack-analysis.md`: ModFix `TranslationData.transDict` 와 Sirius `ModPatch.translateData` 위치
- `dumps/2026-05-05-hangul-modpack-bundle-analysis.md`: 통팩 단독 환경 구조 (자체 CSV fallback 의 근거)
