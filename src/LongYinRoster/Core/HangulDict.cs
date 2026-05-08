using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.5 D-4 — 한자 → 한글 사전. Hybrid 5단계 fallback (v0.7.5.1 hotfix 로 stage 4 추가):
///   1. LongYinModFix.TranslationData.transDict (가장 풍부, ModFix 통합 사전 — exact key)
///   2. LongYinLiZhiZhuan_Mod.ModPatch.translateData (Sirius mod 본체 — exact key)
///   3. 자체 CSV (BepInEx/plugins/Data/patched/Localization.csv 등 5개 — exact key)
///   4. ★ ModFix TranslationEngine.Translate(string) (replacer regex + placeholder + char-prefix index)
///   5. LTLocalization.GetText (게임 자체 + ModFix injected — exact key)
/// 사전 미스 시 원본 한자 그대로 반환 (no exception, no log).
///
/// Stage 4 (v0.7.5.1) 가 합성어 부분 치환 cover — "절세长矛" 같은 등급+종류 합성어를
/// ModFix 의 baseReplacerRegex (longest-match) 로 부분 한글화. ModFix 미설치 환경에선
/// _modfixEngineFn null 이라 stage 4 skip — 기존 4단계 fallback 유지.
///
/// Lazy init on first Translate call. Thread-safe via lock on init only —
/// Translate 자체는 lock-free (dict read).
///
/// 테스트 헬퍼 (`SetSelfDictForTests` / `SetModFixDictForTests` / `SetSiriusDictForTests` /
/// `SetModFixEngineFnForTests` / `ResetForTests` / `LoadCsvLinesForTests`) 는 internal —
/// 테스트 프로젝트가 partial-source `&lt;Compile Include&gt;` 으로 직접 컴파일.
/// </summary>
public static class HangulDict
{
    private static Dictionary<string, string>? _modfixDict;
    private static Dictionary<string, string>? _siriusDict;
    private static Dictionary<string, string>? _selfDict;
    // v0.7.5.1 — ModFix TranslationEngine.Translate(string) reflection wrapper.
    // null = ModFix 미설치 또는 method 시그니처 mismatch. stage 4 skip.
    private static Func<string, string?>? _modfixEngineFn;
    // volatile — DCL 의 happens-before 보장 (특히 IL2CPP ARM target).
    private static volatile bool _initialized;
    private static readonly object _lock = new();

    public static int LoadedCount => _selfDict?.Count ?? 0;
    public static bool ModFixAvailable => _modfixDict != null;
    public static bool SiriusAvailable => _siriusDict != null;
    public static bool ModFixEngineAvailable => _modfixEngineFn != null;
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
        // v0.7.5.1 — Stage 4: ModFix TranslationEngine.Translate (replacer regex + placeholder + char-prefix index)
        if (_modfixEngineFn != null)
        {
            try
            {
                var r = _modfixEngineFn(cn);
                if (!string.IsNullOrEmpty(r) && r != cn) return r!;
            }
            catch { /* swallow — engine 내부 throw 시 stage 5 fallback */ }
        }
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
            try { _modfixDict     = TryLoadModFix();         } catch { }
            try { _siriusDict     = TryLoadSirius();         } catch { }
            try { _selfDict       = LoadSelfCsv();           } catch { _selfDict = new Dictionary<string,string>(); }
            try { _modfixEngineFn = TryLoadModFixEngineFn(); } catch { }
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

    // v0.7.5.1 — ModFix TranslationEngine.Translate(string) — internal class, public static method.
    // ModFix Sirius_Replacer.csv 기반 baseReplacerRegex (longest-match) + placeholder + char-prefix
    // 으로 합성어 부분 한글화 cover. dict 미스 시 입력 그대로 반환.
    private static Func<string, string?>? TryLoadModFixEngineFn()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "LongYinModFix");
        if (asm == null) return null;
        var t = asm.GetType("LongYinModFix.TranslationEngine");
        if (t == null) return null;
        var m = t.GetMethod("Translate",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(string) }, null);
        if (m == null) return null;
        return cn =>
        {
            try { return m.Invoke(null, new object[] { cn }) as string; }
            catch { return null; }
        };
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
            _modfixEngineFn = null;
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

    // v0.7.5.1 — ModFix engine fn 직접 주입 (production 은 reflection wrapper).
    internal static void SetModFixEngineFnForTests(Func<string, string?>? fn)
    {
        lock (_lock) { _modfixEngineFn = fn; _initialized = true; }
    }

    internal static void LoadCsvLinesForTests(IEnumerable<string> lines, char sep, Dictionary<string, string> dict)
        => LoadCsvLines(lines, sep, dict);
}
