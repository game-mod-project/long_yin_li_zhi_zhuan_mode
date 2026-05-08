# LongYinRoster v0.7.5.1 — HangulDict ModFix TranslationEngine fallback (hotfix)

**일시**: 2026-05-06
**baseline**: v0.7.5 (commit `dbd1905`) — 212/212 tests + 14/14 smoke PASS
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §5.5 patch 컨벤션
**trigger**: 인게임 스크린샷 (사용자 보고) — "절세长矛" / "보통长戟" / "절세重甲" 등 합성어가 부분 한글 prefix + 한자 잔존. 등급 prefix 만 사전에 있고 종류 명사 (长矛 / 重甲 / 斗笠 등) 는 정확 키 매치 안 됨.

## 0. 한 줄 요약

`HangulDict.Translate` 의 4단계 fallback 사이에 **ModFix `TranslationEngine.Translate(string)` reflection 호출** 을 5단계로 추가. ModFix 의 replacer regex + placeholder lookup pipeline 을 직접 활용해서 합성 한자 부분 치환 cover.

## 1. 변경

### 현재 (v0.7.5)

```
Translate(cn) →
  1. _modfixDict.TryGetValue(cn)        — exact dict
  2. _siriusDict.TryGetValue(cn)         — exact dict
  3. _selfDict.TryGetValue(cn)           — exact CSV dict
  4. LTLocalization.GetText(cn)          — game self
  5. return cn (raw)
```

`exact key lookup` 만이라 "절세长矛" 같은 합성어는 사전 미스 → raw 반환.

### v0.7.5.1

```
Translate(cn) →
  1. _modfixDict.TryGetValue(cn)
  2. _siriusDict.TryGetValue(cn)
  3. _selfDict.TryGetValue(cn)
  4. ★ ModFix TranslationEngine.Translate(cn)  — 신규 (replacer regex + placeholder + char-prefix index)
  5. LTLocalization.GetText(cn)
  6. return cn (raw)
```

ModFix `TranslationEngine.Translate(string)` 는 fuller pipeline 을 가짐 (dumps/2026-05-05-hangul-mod-stack-analysis.md §1.4):
- exact dict lookup
- TryPlaceholderLookup
- baseReplacerRegex (longest-match)
- transDictIndex (char-prefix 인덱스)
- 날짜 패턴
- 후처리 (DirectionTextHelper / PostpositionHelper 는 caller 가 안 함, Translate 만)

→ "절세长矛" → ModFix replacer 로 "长矛" → "장모" 부분 치환 → "절세장모" 결과 가능.

ModFix 미설치 환경 (통팩 단독) — `_modfixEngineFn == null` 이라 stage 4 skip, 기존 behavior 유지.

## 2. Code 변경 — `src/LongYinRoster/Core/HangulDict.cs`

신규 `_modfixEngineFn` 필드:
```csharp
private static Func<string, string?>? _modfixEngineFn;
```

신규 helper `TryLoadModFixEngineFn`:
```csharp
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
```

`EnsureInitialized` 에 추가:
```csharp
try { _modfixEngineFn = TryLoadModFixEngineFn(); } catch { }
```

`Translate` 의 stage 4 (LTLocalization 직전) 에 stage 4 추가:
```csharp
// Stage 4: ModFix TranslationEngine.Translate (replacer regex + placeholder + char-prefix)
if (_modfixEngineFn != null)
{
    try
    {
        var r = _modfixEngineFn(cn);
        if (!string.IsNullOrEmpty(r) && r != cn) return r!;
    }
    catch { /* swallow */ }
}
// Stage 5: LTLocalization (기존 4단계)
try { ... LTLocalization 호출 ... } catch { }
```

신규 internal 테스트 헬퍼:
```csharp
internal static void SetModFixEngineFnForTests(Func<string, string?>? fn)
{
    lock (_lock) { _modfixEngineFn = fn; _initialized = true; }
}
```

`ResetForTests` 에도 `_modfixEngineFn = null;` 추가.

`ModFixAvailable` 의미 그대로 (`_modfixDict != null`) — engine fn 은 별개 신호, 추가 property 불필요 (사용자에게 공개 안 함).

## 3. Test 변경 — `src/LongYinRoster.Tests/HangulDictTests.cs`

3 신규 case:

```csharp
[Fact]
public void Translate_ModFixEngineFn_HandlesCompositeKey_AfterDictMiss()
{
    // 모든 dict 가 miss 인데 engine fn 이 합성어 한글화
    HangulDict.SetModFixEngineFnForTests(cn => cn == "절세长矛" ? "절세장모" : null);
    HangulDict.Translate("절세长矛").ShouldBe("절세장모");
}

[Fact]
public void Translate_ModFixEngineFn_NullResult_FallsThroughToRaw()
{
    HangulDict.SetModFixEngineFnForTests(_ => null);
    HangulDict.Translate("미스").ShouldBe("미스");   // raw return
}

[Fact]
public void Translate_ModFixEngineFn_SameAsInput_FallsThroughToRaw()
{
    // engine fn 이 입력 그대로 반환 (변환 안 됨) → raw fallback
    HangulDict.SetModFixEngineFnForTests(cn => cn);
    HangulDict.Translate("미스").ShouldBe("미스");
}

[Fact]
public void Translate_ModFixEngineFn_AfterAllDictsMiss_ButDictHitWins()
{
    // dict hit 이면 engine fn 호출 안 함 (stage 1-3 우선)
    HangulDict.SetModFixDictForTests(new Dictionary<string,string> { { "测试", "딕셔너리" } });
    HangulDict.SetModFixEngineFnForTests(_ => "엔진");
    HangulDict.Translate("测试").ShouldBe("딕셔너리");
}
```

Total: 19 → 23 HangulDictTests. 전체 212 → 216.

## 4. Smoke

스크린샷 재현 시나리오 — 인벤토리 row 라벨에서 다음 합성어 한글화 확인:
- 절세长矛 → 절세장모 (또는 ModFix 사전이 매핑한 단어)
- 보통长戟 → 보통장극
- 절세重甲 → 절세중갑
- 절세布甲 → 절세포갑
- 우수头冠 → 우수두관
- 절세斗笠 → 절세두립
- 절세布鞋 → 절세포혜

(정확한 한글은 ModFix Sirius_Replacer.csv 의 매핑에 따라 다름)

회귀: 기존 14/14 smoke PASS 유지 (전부 한글이거나 부분 한글 prefix 가 이미 잘 되던 row 들).

총 14 회귀 + 1 신규 (합성어 cover) = 15/15.

## 5. Release & Cycle

Single-cycle hotfix:
1. spec (본 파일) — compact
2. impl (HangulDict.cs + HangulDictTests.cs)
3. smoke
4. release v0.7.5.1

명명: `v0.7.5.1` (메타 §5.5 patch 컨벤션). VERSION bump only — README v0.7.5 section 갱신 (한 줄 추가) + HANDOFF + 메타 spec §2.2 Result 에 patch 추가 link.

## 6. Out-of-scope

- ModFix `TextSetterPatch.TMP_Text_Setter` / `Text_Setter` reflection (UGUI 자동 변환은 ModFix 가 이미 처리, IMGUI 만 우리 책임)
- 자체 replacer regex 구현 (option B) — ModFix 미설치 환경 cover 강화 가능하지만 v0.7.5.1 scope out (별도 후속)
- char-prefix partial match (option C) — over-engineering

## 7. Risk

- **TranslationEngine.Translate 시그니처 변경** — ModFix v3.2.0 → v4.x 등 update 시 method 부재 가능. `TryLoadModFixEngineFn` 가 null 반환 → silent skip, 기존 4단계 fallback 으로 회귀 (안전).
- **Performance** — 매 row 변환 시 dict 미스면 reflection invoke. 200 row × 1 invoke = 200 method calls/frame. 단 ItemRow.NameKr 가 row build 시 1회만 translate (eager cache, T2) — IMGUI 매 frame 호출 안 됨. ItemDetailPanel 의 ~20 raw fields 만 매 frame 호출 — 부담 미미.
- **ModFix engine 의 부작용** — `TranslationEngine.Translate` 가 `dumpGetTextKeys` 활성 시 internal dict 에 미번역 key 누적. v0.7.5.1 호출도 ModFix 의 dump 에 기여 — 의도된 동작이라 OK.
