# 2026-05-05 외부 mod 분석 — 인덱스

다섯 개 분석 문서를 동일 dumps 폴더에 보관. 각 문서는 자기 완결적이며, 이 인덱스는 navigation 용.

## 분석 대상

| dll | 분석 문서 | 핵심 |
|---|---|---|
| `LongYinCheat.dll` (v1.4.7, 곰딴지) | [`2026-05-05-longyincheat-dll-analysis.md`](2026-05-05-longyincheat-dll-analysis.md) | F10 치트 메뉴, 14 탭 + 6 패널, 11종 Harmony patch, 22종 Features |
| `BepInExConfigManager.Il2Cpp.{Patcher,CoreCLR}.dll` (v1.3.0, sinai) | [`2026-05-05-bepinexconfigmanager-analysis.md`](2026-05-05-bepinexconfigmanager-analysis.md) | F5 BepInEx config UI 편집 도구. **한글화 mod 가 아님**. |
| `LongYinLiZhiZhuan_Mod.dll` (v0.7.6, Sirius) + `LongYinModFix.dll` (v3.2.0, 곰딴지) | [`2026-05-05-hangul-mod-stack-analysis.md`](2026-05-05-hangul-mod-stack-analysis.md) | 한글화 stack — Sirius 본체 + 곰딴지 보정. Harmony 후크 + StringLiteralPatcher + TranslationEngine |
| 한글모드 통팩 (`E:\LongYinLiZhiZhuan_한글모드통팩\`) | [`2026-05-05-hangul-modpack-bundle-analysis.md`](2026-05-05-hangul-modpack-bundle-analysis.md) | Sirius mod + sinai ConfigManager + UniverseLib + Data 자산 번들. 게임 v1.0.0f8.2 vs 통팩 v1.0.1f3 mismatch 위험 |

## v0.7.5+ 작업 가이드

| 가이드 | 활용처 |
|---|---|
| [`2026-05-05-v075-hangul-hook-guide.md`](2026-05-05-v075-hangul-hook-guide.md) | v0.7.5 D-4 한글 사전 hook 구현 — Hybrid 자체 사전 + ModFix dict reflection fallback 패턴 |
| [`2026-05-05-v075-cheat-feature-reference.md`](2026-05-05-v075-cheat-feature-reference.md) | v0.7.5+ 무공/아이템 에디터, NPC/플레이어 세부 수정 — LongYinCheat 의 즉시 차용 가능 자산 카탈로그 |

## 핵심 정정 사항

사용자 메모리 또는 인지에 다음 사항 정정 필요:

1. **`BepInExConfigManager.Il2Cpp.Patcher.dll` 은 한글화 mod 가 아님** — sinai 의 BepInEx config UI 도구. 한글 통팩에 함께 동봉되어 있어 오인 가능.
2. **진짜 한글화 stack** = `LongYinLiZhiZhuan_Mod.dll` (Sirius v0.7.6, 본체) + `LongYinModFix.dll` (곰딴지 v3.2.0, 보정·후처리).
3. **통팩에 LongYinModFix 와 LongYinCheat 는 포함되지 않음** — 사용자가 게임 설치본에 별도 추가한 것.
4. **버전 mismatch**: 통팩은 게임 v1.0.1f3 용, 사용자 게임은 v1.0.0f8.2. StringLiteralPatcher 의 IL2CPP literal offset 패치는 빌드 종속이라 무효 또는 잘못된 영역 덮어쓰기 위험. CSV 사전 lookup 은 영향 없음.

## 디컴파일 결과 캐시

| 분석 대상 | Windows 경로 |
|---|---|
| LongYinCheat | `C:\Users\deepe\AppData\Local\Temp\longyincheat_decomp\` |
| BepInExConfigManager Patcher | `C:\Users\deepe\AppData\Local\Temp\configmgr_patcher_decomp\` |
| BepInExConfigManager Plugin | `C:\Users\deepe\AppData\Local\Temp\configmgr_plugin_decomp\` |
| Sirius mod (LongYinLiZhiZhuan_Mod) | `C:\Users\deepe\AppData\Local\Temp\ly_mod_decomp\` |
| ModFix (LongYinModFix) | `C:\Users\deepe\AppData\Local\Temp\ly_modfix_decomp\` |

ilspycmd 8.2.0 (`C:\Users\deepe\.dotnet\tools\ilspycmd.exe -p -o <out> <dll>`).
