# LongYinRoster v0.7.5 Implementation Plan — D-4 Item 한글화

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ContainerPanel + ItemDetailPanel IMGUI 라벨에서 한자 노출 제거. 신규 `HangulDict` (Hybrid 자체사전 + ModFix reflection fallback) + `ItemRow.NameKr` field + display-time 변환.

**Architecture:** Lazy-init `HangulDict` static class — 4단계 fallback (ModFix transDict → Sirius translateData → 자체 CSV → LTLocalization.GetText). ContainerRowBuilder 가 row 생성 시 한 번 translate 후 `NameKr` 캐시. ContainerView 가 bilingual 검색 (Korean OR Chinese substring) + Korean-aware 정렬. ItemDetailPanel display 직전 변환 (header / curated value / raw value).

**Tech Stack:** C# / .NET (BepInEx 6 IL2CPP), reflection 우회 ModFix/Sirius assembly probe, xUnit + Shouldly 단위 테스트.

**Spec:** [docs/superpowers/specs/2026-05-06-longyin-roster-mod-v0.7.5-design.md](../specs/2026-05-06-longyin-roster-mod-v0.7.5-design.md)
**Roadmap:** [docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md](../specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.2

---

## Task 1: HangulDict (신규 file + TDD)

**Files:**
- Create: `src/LongYinRoster/Core/HangulDict.cs`
- Create: `src/LongYinRoster.Tests/HangulDictTests.cs`

- [ ] **Step 1: 신규 test file 작성 (TDD red)**

`src/LongYinRoster.Tests/HangulDictTests.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class HangulDictTests
{
    public HangulDictTests() => HangulDict.ResetForTests();

    [Fact]
    public void Translate_Null_ReturnsEmpty()
    {
        HangulDict.Translate(null).ShouldBe("");
    }

    [Fact]
    public void Translate_Empty_ReturnsEmpty()
    {
        HangulDict.Translate("").ShouldBe("");
    }

    [Fact]
    public void Translate_Miss_ReturnsRaw()
    {
        HangulDict.Translate("不存在的词").ShouldBe("不存在的词");
    }

    [Fact]
    public void Translate_AlreadyKorean_ReturnsAsIs()
    {
        HangulDict.Translate("한글").ShouldBe("한글");
    }

    [Fact]
    public void Translate_HitInSelfDict_ReturnsKr()
    {
        var fake = new Dictionary<string, string> { { "测试", "테스트" } };
        HangulDict.SetSelfDictForTests(fake);
        HangulDict.Translate("测试").ShouldBe("테스트");
    }

    [Fact]
    public void Translate_HitInModFixDict_PreferredOverSelf()
    {
        var modfix = new Dictionary<string, string> { { "测试", "모드픽스" } };
        var self   = new Dictionary<string, string> { { "测试", "자체" } };
        HangulDict.SetModFixDictForTests(modfix);
        HangulDict.SetSelfDictForTests(self);
        HangulDict.Translate("测试").ShouldBe("모드픽스");
    }

    [Fact]
    public void Translate_HitInSiriusDict_PreferredOverSelf()
    {
        var sirius = new Dictionary<string, string> { { "测试", "시리우스" } };
        var self   = new Dictionary<string, string> { { "测试", "자체" } };
        HangulDict.SetSiriusDictForTests(sirius);
        HangulDict.SetSelfDictForTests(self);
        HangulDict.Translate("测试").ShouldBe("시리우스");
    }

    [Fact]
    public void Translate_ModFixWins_OverSirius()
    {
        var modfix = new Dictionary<string, string> { { "测试", "모드픽스" } };
        var sirius = new Dictionary<string, string> { { "测试", "시리우스" } };
        HangulDict.SetModFixDictForTests(modfix);
        HangulDict.SetSiriusDictForTests(sirius);
        HangulDict.Translate("测试").ShouldBe("모드픽스");
    }

    [Fact]
    public void EnsureInitialized_Idempotent()
    {
        HangulDict.EnsureInitialized();
        bool first = HangulDict.IsInitialized;
        HangulDict.EnsureInitialized();
        bool second = HangulDict.IsInitialized;
        first.ShouldBeTrue();
        second.ShouldBeTrue();
    }

    [Fact]
    public void LoadCsvLines_Skips_Blank_Lines()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "", "  ", "测试;테스트", "\t" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict.Count.ShouldBe(1);
        dict["测试"].ShouldBe("테스트");
    }

    [Fact]
    public void LoadCsvLines_Skips_NoSeparatorLines()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "noSeparatorHere", "测试;테스트", "trailingSep;" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict.Count.ShouldBe(1);
        dict["测试"].ShouldBe("테스트");
    }

    [Fact]
    public void LoadCsvLines_Unescapes_NewlineEscapes()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { @"测试\n第二行;테스트\n둘째줄" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict["测试\n第二行"].ShouldBe("테스트\n둘째줄");
    }

    [Fact]
    public void LoadCsvLines_Skips_KeyEqualsValue()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "same;same", "测试;테스트" };
        HangulDict.LoadCsvLinesForTests(lines, ';', dict);
        dict.Count.ShouldBe(1);
    }

    [Fact]
    public void LoadCsvLines_AtSeparator_Works()
    {
        var dict = new Dictionary<string,string>();
        var lines = new[] { "key@value", "测试@테스트" };
        HangulDict.LoadCsvLinesForTests(lines, '@', dict);
        dict["key"].ShouldBe("value");
        dict["测试"].ShouldBe("테스트");
    }

    [Fact]
    public void LoadedCount_ReflectsSelfDictSize()
    {
        var fake = new Dictionary<string, string> { { "a", "A" }, { "b", "B" } };
        HangulDict.SetSelfDictForTests(fake);
        HangulDict.LoadedCount.ShouldBe(2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail (red)**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj --filter "FullyQualifiedName~HangulDictTests" -v normal
```

Expected: All 14 fail with compile error (HangulDict 클래스 미존재).

- [ ] **Step 3: HangulDict.cs 작성 (green)**

`src/LongYinRoster/Core/HangulDict.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.5 D-4 — 한자 → 한글 사전. Hybrid 4단계 fallback:
///   1. LongYinModFix.TranslationData.transDict (가장 풍부, ModFix 통합 사전)
///   2. LongYinLiZhiZhuan_Mod.ModPatch.translateData (Sirius mod 본체)
///   3. 자체 CSV (BepInEx/plugins/Data/patched/Localization.csv 등 5개)
///   4. LTLocalization.GetText (게임 자체 + ModFix injected)
/// 사전 미스 시 원본 한자 그대로 반환 (no exception, no log).
///
/// Lazy init on first Translate call. Thread-safe via lock on init only —
/// Translate 자체는 lock-free (dict read).
///
/// 테스트 헬퍼 (`SetSelfDictForTests` / `SetModFixDictForTests` / `SetSiriusDictForTests` / `ResetForTests` /
/// `LoadCsvLinesForTests`) 는 internal — InternalsVisibleTo("LongYinRoster.Tests") 로 접근.
/// </summary>
public static class HangulDict
{
    private static Dictionary<string, string>? _modfixDict;
    private static Dictionary<string, string>? _siriusDict;
    private static Dictionary<string, string>? _selfDict;
    private static bool _initialized;
    private static readonly object _lock = new();

    public static int LoadedCount => _selfDict?.Count ?? 0;
    public static bool ModFixAvailable => _modfixDict != null;
    public static bool SiriusAvailable => _siriusDict != null;
    public static bool IsInitialized => _initialized;

    public static string Translate(string? cn)
    {
        if (string.IsNullOrEmpty(cn)) return "";
        EnsureInitialized();
        try
        {
            if (_modfixDict != null && _modfixDict.TryGetValue(cn, out var v1)) return v1;
        } catch { /* race */ }
        try
        {
            if (_siriusDict != null && _siriusDict.TryGetValue(cn, out var v2)) return v2;
        } catch { /* race */ }
        if (_selfDict != null && _selfDict.TryGetValue(cn, out var v3)) return v3;
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
        return cn;
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
            try { _selfDict   = LoadSelfCsv();   } catch { _selfDict = new Dictionary<string,string>(); }
        }
    }

    private static Dictionary<string, string>? TryLoadModFix()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "LongYinModFix");
        if (asm == null) return null;
        var t = asm.GetType("LongYinModFix.TranslationData");
        if (t == null) return null;
        var f = t.GetField("transDict",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return f?.GetValue(null) as Dictionary<string, string>;
    }

    private static Dictionary<string, string>? TryLoadSirius()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "LongYinLiZhiZhuan_Mod");
        if (asm == null) return null;
        var t = asm.GetType("LongYinLiZhiZhuan_Mod.ModPatch");
        if (t == null) return null;
        var f = t.GetField("translateData",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return f?.GetValue(null) as Dictionary<string, string>;
    }

    private static Dictionary<string, string> LoadSelfCsv()
    {
        var dict = new Dictionary<string, string>(8192);
        var basePath = Path.Combine("BepInEx", "plugins", "Data", "patched");
        string[] files = {
            "Localization.csv", "Sirius_UIText.csv", "Sirius_etc.csv",
            "Sirius_Mail.csv", "Sirius_SceneText.csv"
        };
        foreach (var f in files)
        {
            var path = Path.Combine(basePath, f);
            if (!File.Exists(path)) continue;
            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                LoadCsvLines(lines, ';', dict);
            }
            catch { /* swallow per-file */ }
        }
        return dict;
    }

    private static void LoadCsvLines(IEnumerable<string> lines, char sep, Dictionary<string, string> dict)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            int idx = line.IndexOf(sep);
            if (idx <= 0 || idx >= line.Length - 1) continue;
            var k = line.Substring(0, idx).Replace("\\n", "\n").Replace("\\r", "\r");
            var v = line.Substring(idx + 1).Replace("\\n", "\n").Replace("\\r", "\r");
            if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v) && k != v) dict[k] = v;
        }
    }

    // ===== Test helpers (internal) =====
    internal static void ResetForTests()
    {
        lock (_lock)
        {
            _modfixDict = null;
            _siriusDict = null;
            _selfDict = null;
            _initialized = false;
        }
    }

    internal static void SetSelfDictForTests(Dictionary<string, string> dict)
    {
        lock (_lock) { _selfDict = dict; _initialized = true; }
    }

    internal static void SetModFixDictForTests(Dictionary<string, string> dict)
    {
        lock (_lock) { _modfixDict = dict; _initialized = true; }
    }

    internal static void SetSiriusDictForTests(Dictionary<string, string> dict)
    {
        lock (_lock) { _siriusDict = dict; _initialized = true; }
    }

    internal static void LoadCsvLinesForTests(IEnumerable<string> lines, char sep, Dictionary<string, string> dict)
        => LoadCsvLines(lines, sep, dict);
}
```

- [ ] **Step 4: Run tests to verify they pass (green) + 회귀**

```bash
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
```

Expected:
- 14 신규 HangulDictTests PASS
- 기존 193 PASS (회귀 없음)
- 총 207 PASS

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Core/HangulDict.cs src/LongYinRoster.Tests/HangulDictTests.cs
git commit -m "$(cat <<'EOF'
feat(core): v0.7.5 — HangulDict 한자→한글 사전 (Hybrid 4단계 fallback)

신규 HangulDict static class — ModFix transDict reflection > Sirius translateData reflection > 자체 CSV (5개 파일) > LTLocalization.GetText > raw fallback. Lazy init on first Translate call. Thread-safe init via lock, lock-free dict read.

자체 CSV 자산: BepInEx/plugins/Data/patched/{Localization,Sirius_UIText,Sirius_etc,Sirius_Mail,Sirius_SceneText}.csv (구분자 ;). escape \\n / \\r 복원.

14 신규 HangulDictTests — null/empty, miss, hit (각 dict 우선순위), idempotent init, CSV 로더 edge cases (blank/no-sep/escape/key=value/at-sep). Internal test helpers via InternalsVisibleTo. 193 → 207.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: ItemRow.NameKr field + ContainerRowBuilder Translate

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs` — `ItemRow` 에 `NameKr` field 추가
- Modify: `src/LongYinRoster/Containers/ContainerRowBuilder.cs` — `FromJsonArray` + `FromGameAllItem` 모두 NameKr 할당
- Modify (or create): `src/LongYinRoster.Tests/ContainerRowBuilderTests.cs` (있으면 갱신, 없으면 신규)

- [ ] **Step 1: ItemRow.NameKr field 추가**

`src/LongYinRoster/UI/ContainerPanel.cs` — `ItemRow` 정의 위치 찾기 (struct 또는 class). 기존 `Name` / `NameRaw` field 옆에 추가:

```csharp
public string? NameKr;   // v0.7.5 — translated display name (null/empty = NameRaw fallback)
```

- [ ] **Step 2: ContainerRowBuilder Translate 호출**

`src/LongYinRoster/Containers/ContainerRowBuilder.cs`:

`FromJsonArray` (around line 35-48) — `list.Add(new ContainerPanel.ItemRow {...})` 안에서 `NameRaw = name,` 옆에 추가:
```csharp
NameKr       = LongYinRoster.Core.HangulDict.Translate(name),
```

`FromGameAllItem` (around line 99-113) — 동일:
```csharp
NameKr       = LongYinRoster.Core.HangulDict.Translate(name),
```

- [ ] **Step 3: ContainerRowBuilder test (있으면 갱신, 없으면 짧게 신규)**

먼저 기존 test 파일 확인:
```bash
ls src/LongYinRoster.Tests/ContainerRowBuilderTests.cs 2>/dev/null && echo "exists" || echo "missing"
```

기존 파일 있으면 — `NameKr` field assertion 추가 (`NameKr.ShouldBe(name)` 또는 fake dict 주입 후 변환 확인).

기존 파일 없으면 — Step 4 의 회귀 테스트가 NameKr 변경을 cover 하는지 확인. 별도 ContainerRowBuilder 테스트 신규 추가는 불필요 (HangulDict 단위 테스트 + 인게임 smoke 가 cover).

- [ ] **Step 4: Build + 전체 test 실행 (회귀)**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
```

Expected:
- Build: 0 warnings, 0 errors
- 207 PASS (Task 1 의 14 + 기존 193, 회귀 없음)

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs src/LongYinRoster/Containers/ContainerRowBuilder.cs
# ContainerRowBuilderTests.cs 가 변경되었으면 추가
git commit -m "$(cat <<'EOF'
feat(containers): v0.7.5 — ItemRow.NameKr 필드 + ContainerRowBuilder Translate 호출

ItemRow 에 NameKr field 추가 (string?, null = NameRaw fallback). ContainerRowBuilder.FromJsonArray + FromGameAllItem 둘 다 row 생성 시 HangulDict.Translate(name) 호출하여 NameKr 캐시. NameRaw 는 raw 한자 그대로 유지 (검색 호환).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: ContainerView bilingual search + Korean-aware sort

**Files:**
- Modify: `src/LongYinRoster/Containers/ContainerView.cs`
- Modify (or create): `src/LongYinRoster.Tests/ContainerViewTests.cs`

- [ ] **Step 1: ContainerView 검색 + 정렬 변경**

`src/LongYinRoster/Containers/ContainerView.cs`:

검색 (line 29-33):
```csharp
if (!string.IsNullOrEmpty(state.Search))
{
    string needle = state.Search;
    q = q.Where(r =>
        ((r.NameKr  ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
     || ((r.NameRaw ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0));
}
```

정렬 (line 35-42, `SortKey.Name` 만):
```csharp
SortKey.Name => q.OrderBy(r => r.NameKr ?? r.NameRaw ?? "").ThenBy(r => r.Index),
```

- [ ] **Step 2: ContainerView 테스트 신규 (또는 갱신)**

먼저 확인:
```bash
ls src/LongYinRoster.Tests/ContainerViewTests.cs 2>/dev/null && echo "exists" || echo "missing"
```

**없으면 신규 작성** (`src/LongYinRoster.Tests/ContainerViewTests.cs`):

```csharp
using System.Collections.Generic;
using LongYinRoster.Containers;
using LongYinRoster.UI;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerViewTests
{
    private static List<ContainerPanel.ItemRow> Sample()
    {
        return new List<ContainerPanel.ItemRow>
        {
            new() { Index = 0, Name = "九阳神功", NameRaw = "九阳神功", NameKr = "구양신공", Type = 3, GradeOrder = 5, QualityOrder = 5, CategoryKey = "003.000" },
            new() { Index = 1, Name = "古今图书", NameRaw = "古今图书", NameKr = "고금도서", Type = 3, GradeOrder = 4, QualityOrder = 5, CategoryKey = "003.000" },
            new() { Index = 2, Name = "九转还魂丹", NameRaw = "九转还魂丹", NameKr = null, Type = 2, GradeOrder = 5, QualityOrder = 5, CategoryKey = "002.000" }, // NameKr null = miss
        };
    }

    [Fact]
    public void Search_KoreanKeyword_MatchesViaNameKr()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default with { Search = "구양" };
        var result = view.ApplyView(Sample(), state);
        result.Count.ShouldBe(1);
        result[0].NameRaw.ShouldBe("九阳神功");
    }

    [Fact]
    public void Search_ChineseKeyword_MatchesViaNameRaw()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default with { Search = "九阳" };
        var result = view.ApplyView(Sample(), state);
        result.Count.ShouldBe(1);
        result[0].NameKr.ShouldBe("구양신공");
    }

    [Fact]
    public void Search_NameKrNull_FallsBackToNameRaw()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default with { Search = "九转" };
        var result = view.ApplyView(Sample(), state);
        result.Count.ShouldBe(1);
        result[0].Index.ShouldBe(2);
    }

    [Fact]
    public void Sort_NameKey_PrefersNameKr()
    {
        var view = new ContainerView();
        var state = SearchSortState.Default with { Key = SortKey.Name, Ascending = true };
        var result = view.ApplyView(Sample(), state);
        // 한글 자모순: "고금도서" < "구양신공" < "九转还魂丹" (NameKr null → NameRaw 후순위)
        result[0].NameKr.ShouldBe("고금도서");
        result[1].NameKr.ShouldBe("구양신공");
    }
}
```

⚠ `SearchSortState.Default with { Search = "..." }` 형태가 record/struct with-expression 을 요구. SearchSortState 가 record/struct 가 아니면 다른 방식 — 먼저 `SearchSortState.cs` 확인하고 적절히 조정.

기존 ContainerViewTests 가 있으면 — 위 4 case 추가, 기존 회귀 보존.

- [ ] **Step 3: 빌드 + 테스트**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
```

Expected:
- 신규 ContainerView 4 PASS
- 기존 207 PASS
- 총 211 PASS

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/Containers/ContainerView.cs src/LongYinRoster.Tests/ContainerViewTests.cs
git commit -m "$(cat <<'EOF'
feat(containers): v0.7.5 — ContainerView bilingual 검색 + Korean-aware 정렬

ContainerView.ApplyView 검색이 NameKr OR NameRaw 둘 다 substring 매치 (한글 키워드 + 한자 키워드 호환). SortKey.Name 정렬은 NameKr ?? NameRaw 우선 → 한국어 자모순 자연스러운 정렬.

4 신규 ContainerViewTests — Korean keyword / Chinese keyword / NameKr null fallback / Korean sort.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: ContainerPanel BuildLabel + ItemDetailPanel display 변환

**Files:**
- Modify: `src/LongYinRoster/UI/ContainerPanel.cs` — `BuildLabel` 가 NameKr 우선
- Modify: `src/LongYinRoster/UI/ItemDetailPanel.cs` — header / curated value / raw value 모두 `HangulDict.Translate` wrap

- [ ] **Step 1: ContainerPanel.BuildLabel 변경**

`src/LongYinRoster/UI/ContainerPanel.cs` — `BuildLabel(ItemRow r)` 메서드 찾기. 현재 `r.Name` 또는 `r.NameRaw` 사용 중일 텐데, `r.NameKr ?? r.NameRaw ?? ""` 로 변경.

(BuildLabel 의 정확한 현재 구현은 file 을 직접 확인하여 변경 — name 출처만 교체, format 유지)

- [ ] **Step 2: ItemDetailPanel display-time Translate**

`src/LongYinRoster/UI/ItemDetailPanel.cs`:

Line 82 변경:
```csharp
string name = LongYinRoster.Core.HangulDict.Translate(ItemReflector.GetNameRaw(raw));
```

Line 98 변경:
```csharp
GUILayout.Label($"  {label}: {LongYinRoster.Core.HangulDict.Translate(value)}");
```

Line 110 변경:
```csharp
GUILayout.Label($"  {fname}: {LongYinRoster.Core.HangulDict.Translate(value)}");
```

(fname 은 영어 reflection 필드명이라 변경 안 함. value 만 wrap.)

`using LongYinRoster.Core;` 가 file 상단에 없으면 추가.

- [ ] **Step 3: Build + 전체 test**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
```

Expected:
- Build: 0 warnings, 0 errors
- 211 PASS (회귀 없음 — UI 변경은 단위 테스트 미커버, 회귀는 smoke 단계)

- [ ] **Step 4: Commit**

```bash
git add src/LongYinRoster/UI/ContainerPanel.cs src/LongYinRoster/UI/ItemDetailPanel.cs
git commit -m "$(cat <<'EOF'
feat(ui): v0.7.5 — ContainerPanel.BuildLabel + ItemDetailPanel display-time 한자→한글

ContainerPanel.BuildLabel 가 NameKr ?? NameRaw 사용 (Toggle 라벨 한글 우선).

ItemDetailPanel — header (line 82), curated value (line 98), raw value (line 110) 모두 HangulDict.Translate wrap. label (curated 한글) 과 fname (영어 reflection 필드) 은 변경 없음.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Plugin.cs 진단 로그

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs`

- [ ] **Step 1: Load() 끝에 진단 로그 한 줄 추가**

`src/LongYinRoster/Plugin.cs` `Load()` 메서드 끝부분 (Logger.Info(...) 다음 또는 마지막 줄):

```csharp
Logger.Info("[v0.7.5] HangulDict: lazy init on first Translate() call");
```

선택: 게임 첫 frame Update 에서 `HangulDict.EnsureInitialized()` 명시 호출 하여 ModFix dict reload 시점을 보장. 단 lazy 자체로 충분하므로 명시 호출은 생략해도 OK — 첫 row build 가 자연스럽게 trigger.

- [ ] **Step 2: Build + test**

```bash
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj
```

Expected:
- Build: 0 warnings, 0 errors
- 211 PASS

- [ ] **Step 3: Commit**

```bash
git add src/LongYinRoster/Plugin.cs
git commit -m "$(cat <<'EOF'
feat(plugin): v0.7.5 — HangulDict 로딩 진단 로그

Plugin.Load 끝에 한 줄 — lazy init 안내. 실제 사전 로딩은 첫 Translate 호출 시점에 발생.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: 인게임 smoke 14/14 (manual)

**Files:**
- Build artifact: `dotnet build -c Release` 가 자동 deploy (DeployToBepInEx target)
- Create: `docs/superpowers/dumps/2026-05-06-v0.7.5-smoke-results.md`

- [ ] **Step 1: 빌드 + 게임 deploy**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

DeployToBepInEx target 이 `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 자동 복사.

- [ ] **Step 2: 게임 실행 + 8 신규 시나리오**

| # | 시나리오 | 절차 | 기대 |
|---|---|---|---|
| S1 | 통팩+ModFix 환경 인벤 | 게임 실행 (현재 mod stack) → F11 → 컨테이너 → 인벤 | item 이름 한글 표시 |
| S2 | 통팩 단독 (ModFix 임시 비활성) | LongYinModFix.dll 잠시 다른 폴더로 옮기고 게임 재시작 → 인벤 | 한글 표시 (자체 CSV fallback) |
| S3 | 인벤 검색 — 한글 | 검색창에 "검" / "단" 등 한글 키워드 | 한글 매치 항목 필터 |
| S4 | 인벤 검색 — 한자 | "刀" / "剑" 등 한자 키워드 | 한자 매치 항목 필터 |
| S5 | 인벤 정렬 SortKey.Name | 이름 정렬 토글 | 한국어 자모 순 표시 |
| S6 | ItemDetailPanel header | 임의 item ⓘ | 한글 이름 표시 |
| S7 | ItemDetailPanel curated value | 임의 item ⓘ → curated 섹션 | 가능한 한자 값 한글 변환 |
| S8 | ItemDetailPanel raw fields | 임의 item ⓘ → raw 펼침 | 가능한 한자 값 한글 변환, 미스는 한자 그대로 |

(S2 종료 후 ModFix dll 원위치 복원 + 재시작)

- [ ] **Step 3: 회귀 6 (v0.7.4.1 smoke)**

| # | 시나리오 | 결과 |
|---|---|---|
| R1 | 말 ⓘ — curated 12행 | PASS / FAIL |
| R2 | 보물 (fullIdentified=true) ⓘ | |
| R3 | 보물 (fullIdentified=false) ⓘ | |
| R4 | 재료 ⓘ | |
| R5 | 장비/비급/단약 ⓘ | |
| R6 | 컨테이너 area item ⓘ — 미지원 표시 | |

- [ ] **Step 4: smoke dump 작성**

`docs/superpowers/dumps/2026-05-06-v0.7.5-smoke-results.md` 신규 — 14/14 결과 표.

- [ ] **Step 5: smoke commit**

```bash
git add docs/superpowers/dumps/2026-05-06-v0.7.5-smoke-results.md
git commit -m "$(cat <<'EOF'
docs(smoke): v0.7.5 인게임 smoke 14/14 결과 — 한자→한글 변환 + 회귀

신규 8 (통팩+ModFix / 통팩 단독 / 한글 검색 / 한자 검색 / Korean 정렬 / Detail header / Detail curated / Detail raw) + v0.7.4.1 회귀 6 = 14/14 PASS.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

⚠ smoke FAIL 항목 있으면 release 진행 금지 — 이전 task 로 되돌아가서 fix.

---

## Task 7: Release v0.7.5

**Files:**
- Modify: `src/LongYinRoster/Plugin.cs` — VERSION
- Modify: `README.md`
- Modify: `docs/HANDOFF.md`
- Modify: `docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`
- Build: `dist/LongYinRoster-v0.7.5.zip`

- [ ] **Step 1: VERSION bump**

`Plugin.cs:17`:
```csharp
public const string VERSION = "0.7.4.1";   // → "0.7.5"
```

- [ ] **Step 2: README.md update**

새 `### v0.7.5 — Item 한글화` section ABOVE v0.7.4.1 + version table row:

```markdown
### v0.7.5 — Item 한글화

ContainerPanel + ItemDetailPanel IMGUI 라벨에서 한자 노출 제거. 신규 `HangulDict` static class 가 4단계 사전 fallback 제공:

1. **LongYinModFix** (`TranslationData.transDict`) — 통팩 + ModFix 환경
2. **LongYinLiZhiZhuan_Mod** (`ModPatch.translateData`) — Sirius 본체
3. **자체 CSV** (`BepInEx/plugins/Data/patched/{Localization,Sirius_UIText,Sirius_etc,Sirius_Mail,Sirius_SceneText}.csv`) — ModFix 미설치 환경 robust
4. **`LTLocalization.GetText`** — 게임 자체 사전 (ModFix injected 항목 포함)

`ItemRow.NameKr` field 신규. 검색은 bilingual (한글 OR 한자), 정렬은 한국어 자모순.

~211 unit tests PASS (HangulDict 14 + ContainerView 4 신규). 인게임 smoke 14/14 PASS (신규 8 + 회귀 6).
```

version table row:
```markdown
| v0.7.5 | Item 한글화 — Hybrid 사전 (ModFix reflection + 자체 CSV + LTLocalization fallback). ContainerPanel/ItemDetailPanel 한자 노출 제거. bilingual 검색 + Korean 정렬. |
```

- [ ] **Step 3: HANDOFF.md update**

- 진행 상태 → "v0.7.5 release"
- Releases 리스트에 v0.7.5 entry 추가
- "다음 sub-project" cursor → v0.7.6 설정 panel

- [ ] **Step 4: 메타 spec §2.2 ✅**

`docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md` §2.2 — heading 에 ✅ 마킹 + Result 섹션:

```markdown
### 2.2 v0.7.5 D-4 Item 한글화 ✅ 완료 (v0.7.5, 2026-05-06)

[기존 표 유지]

**Result** (2026-05-06):
- Release: [v0.7.5](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.5)
- Spec: [2026-05-06-longyin-roster-mod-v0.7.5-design.md](2026-05-06-longyin-roster-mod-v0.7.5-design.md)
- Plan: [2026-05-06-longyin-roster-mod-v0.7.5-plan.md](../plans/2026-05-06-longyin-roster-mod-v0.7.5-plan.md)
- Smoke: [2026-05-06-v0.7.5-smoke-results.md](../dumps/2026-05-06-v0.7.5-smoke-results.md)
- Tests: 193 → ~211 PASS, 인게임 smoke 14/14 PASS.
- Hook 전략: (A) Hybrid — ModFix reflection > Sirius reflection > 자체 CSV > LTLocalization.
```

- [ ] **Step 5: Commit + tag + push**

```bash
git add src/LongYinRoster/Plugin.cs README.md docs/HANDOFF.md docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md
git commit -m "$(cat <<'EOF'
chore(release): v0.7.5 — Item 한글화 (ContainerPanel + ItemDetailPanel)

신규 HangulDict — 4단계 사전 fallback (ModFix transDict reflection > Sirius translateData > 자체 CSV 5개 > LTLocalization.GetText). ItemRow.NameKr field 신규, bilingual 검색 + Korean 정렬. ItemDetailPanel header / curated value / raw value 한자→한글 변환.

VERSION 0.7.4.1 → 0.7.5. ~211 unit tests PASS, smoke 14/14 PASS.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
git tag v0.7.5
git push origin main
git push origin v0.7.5
```

- [ ] **Step 6: dist zip + GitHub release**

```bash
# dist zip — v0.7.4.1 패턴 답습 (BepInEx/plugins/LongYinRoster/LongYinRoster.dll)
# PowerShell 또는 Compress-Archive 사용
```

PowerShell:
```powershell
$root = "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
$stage = "$root/dist/_stage_v075"
$dll = "$root/src/LongYinRoster/bin/Release/LongYinRoster.dll"
$zipPath = "$root/dist/LongYinRoster-v0.7.5.zip"
New-Item -ItemType Directory -Force -Path "$stage/BepInEx/plugins/LongYinRoster" | Out-Null
Copy-Item $dll "$stage/BepInEx/plugins/LongYinRoster/" -Force
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$stage/BepInEx" -DestinationPath $zipPath -Force
Remove-Item -Recurse -Force $stage
```

GitHub release:
```bash
gh release create v0.7.5 dist/LongYinRoster-v0.7.5.zip \
  --title "v0.7.5 — Item 한글화 (ContainerPanel + ItemDetailPanel)" \
  --notes "$(cat <<'EOF'
ContainerPanel + ItemDetailPanel IMGUI 라벨에서 한자 노출 제거.

## 변경

- 신규 `HangulDict` 한자→한글 사전 (Hybrid 4단계 fallback)
  1. ModFix `TranslationData.transDict` reflection
  2. Sirius `ModPatch.translateData` reflection
  3. 자체 CSV (`BepInEx/plugins/Data/patched/Localization.csv` 등 5개)
  4. `LTLocalization.GetText` (게임 자체)
- `ItemRow.NameKr` 신규 field — row 생성 시 한 번 translate 후 캐시
- ContainerView — bilingual 검색 (한글 OR 한자), Korean-aware 정렬
- ItemDetailPanel — header / curated value / raw value 한자→한글

## 호환성

- **통팩+ModFix**: ModFix `transDict` 우선 (가장 풍부)
- **통팩 단독**: Sirius `translateData` 또는 자체 CSV 로 robust
- **사전 미스**: 한자 원본 그대로 표시 (no exception)

## 검증

- ~211 unit tests PASS (HangulDict 14 + ContainerView 4 신규)
- 인게임 smoke 14/14 PASS (신규 8 + 회귀 6)

## 다음

v0.7.6 설정 panel — hotkey / 컨테이너 정원 / 창 크기 / 검색·정렬 영속화

상세: [docs/superpowers/specs/2026-05-06-longyin-roster-mod-v0.7.5-design.md](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/blob/main/docs/superpowers/specs/2026-05-06-longyin-roster-mod-v0.7.5-design.md)
EOF
)"
```

---

## Self-Review

### Spec coverage
| Spec section | Task |
|---|---|
| §3.1 HangulDict 신규 | T1 |
| §3.2 HangulDictTests 신규 | T1 |
| §4.1 ItemRow.NameKr | T2 |
| §4.2 ContainerRowBuilder Translate | T2 |
| §4.3 ContainerView bilingual + Korean sort | T3 |
| §4.4 ItemDetailPanel display 변환 | T4 |
| §4.5 Plugin.cs 진단 로그 | T5 |
| §6 smoke 14/14 | T6 |
| §7 release | T7 |
| §8 out-of-scope | impl 안 함 (그대로) |
| §9 risk | T1 try/catch + T6 smoke 가 검증 |

### Placeholder scan
- ✅ TBD/TODO 없음
- ✅ 모든 step 에 actual code/command
- ✅ 모든 method signature spec 과 일치 (HangulDict.Translate / EnsureInitialized / TryLoadModFix / TryLoadSirius / LoadSelfCsv / LoadCsvLines / 4 internal test helpers)

### Type consistency
- ✅ `Dictionary<string,string>` 일관 (모든 dict)
- ✅ `ItemRow.NameKr` (string?, nullable) — caller 가 `??` fallback
- ✅ Sort: `r.NameKr ?? r.NameRaw ?? ""` 일관
- ✅ Search: 두 필드 둘 다 substring 매치

---

## Test 갯수 추적

| 시점 | 신규 | 누적 | 전체 |
|---|---:|---:|---:|
| baseline (v0.7.4.1) | — | — | 193 |
| Task 1 (HangulDict) | +14 | 14 | 207 |
| Task 2 (Row 통합) | +0 | 14 | 207 |
| Task 3 (ContainerView) | +4 | 18 | 211 |
| Task 4 (UI 변환) | +0 | 18 | 211 |
| Task 5 (Plugin 로그) | +0 | 18 | 211 |
| Task 6 (smoke) | — | — | 211 |
| Task 7 (release) | — | — | 211 |

⚠ ContainerView record/struct with-expression 호환성 — Step 3 Step 2 시 SearchSortState 타입 확인 후 적절히 조정.
