# LongYin Roster Mod — 작업 핸드오프 문서

**일시 중지**: 2026-05-06
**진행 상태**: **v0.7.5.2 release** — Cell 24×24 정사각형 + 한자 → 48×24 가로 직사각형 + 한글 라벨 (장비/단약/음식/비급/보물/재료/말). cell 내부 강화/착 마커 제거 (row text 정보 유지 — redundant). 카테고리 직관성 향상. 216/216 tests PASS (CategoryGlyphTests 갱신) + 인게임 smoke 11/11 PASS (3 iteration fix: label width / height / marker 제거).
**저장소**: https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode (`main` 브랜치)
**프로젝트 루트**: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`
**Releases**:
- [v0.1.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.1.0) — Live capture + slot management
- [v0.2.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.2.0) — Import from save + input gating
- [v0.3.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.3.0) — Apply (stat-backup) + Restore + save/reload 안전성
- [v0.4.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.4.0) — 9-카테고리 체크박스 UI + 정체성 활성화 + 부상/충성/호감 영구 보존 회귀
- (v0.5.0 — release 안 함 — 양쪽 PoC FAIL, dumps/2026-05-01-* 참고)
- [v0.5.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.1) — 무공 active 활성화 (kungfuSkills.equiped + game 패턴 11-swap + UI cache invalidate + save persistence)
- [v0.5.2](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.2) — 무공 list 활성화 (LoseAllSkill clear + ctor(int) wrapper + GetSkill add 2-pass + SlotFile JSON 직렬화 fix)
- [v0.5.3](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.3) — 인벤토리 (ItemList) Replace 활성화 (LoseAllItem + ItemData(ItemType) ctor + GetItem add + Probe cache invalidate)
- [v0.5.4](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.4) — 인벤토리 subData 풀 복원 (filter fix + generic JSON→IL2CPP wrapper deep-copy with Dictionary handling)
- [v0.5.5](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.5.5) — 창고 (selfStorage) Replace 활성화 (직접 list manipulation + ItemListApplier deep-copy 재사용)
- [v0.6.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.0) — 장비 슬롯 (무기 / 갑옷 / 투구 / 신발 / 장신구×2 / 말 / 마구) Replace 활성화 (EquipItem + EquipHorse game-self method + identity-based matching)
- [v0.6.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.1) — 외형 (faceData / skinColorDark / voicePitch) 활성화 + 모든 카테고리 동시 Apply 시 stat override 회귀 fix (SetSimpleFields → RefreshSelfState 이후 이동)
- [v0.6.2](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.2) — 무공 돌파속성 (extraAddData / speEquipData / speUseData / equipUseSpeAddValue / damageUseSpeAddValue / belongHeroID 등) 풀 복원 + Stat snapshot/restore (Stat unchecked 시 부수효과 보호)
- [v0.6.3](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.3) — 2D nested list (treasure.playerGuessTreasureLv 등 List<List<int>>) 풀 복원. ApplyJsonArray 가 nested element type 의 inner list 인스턴스 신규 생성 후 recurse.
- [v0.6.4](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.6.4) — partPosture (체형 등 외형 sub-data) 복원. SerializerService 가 player.partPosture.partPosture (List<float>) 의 값을 `_partPostureFloats` 배열로 JSON inject + AppearanceApplier 가 reflection clear+add. JsonConvert 가 IL2CPP wrapper 제외하는 issue 우회.
- [v0.7.0](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.0) — F11 진입 메뉴 (캐릭터 관리 / 컨테이너 관리) + 컨테이너 기능 (인벤토리/창고 ↔ 외부 디스크 컨테이너 이동·복사·삭제). 통합 UI overhaul (커스텀 thicker 헤더 + 흰색 bold 제목 + 일관된 X 닫기 버튼 + 일관된 transparency).
- [v0.7.0.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.0.1) — Hotfix: 천부 휴식 회귀 (ManageTagTime + ClearAllTempTag Harmony Prefix/Postfix + ApplyInProgress flag) / ContainerPanel 높이 600→760 + ScrollView / DrawToast IL2CPP strip 우회 (FlexibleSpace 제거 + ToastService.Push 위임 + try/catch).
- [v0.7.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.1) — 컨테이너 UX 1차: destination 명시 (대칭 mirror 4-callback) / capacity 표시 무게 기반 (N개, X.X / MAX kg) + 인벤 over-cap ⚠ 초과 마커 / 가드 (인벤 over-weight 허용+속도페널티 안내, 창고 hard cap+거절 toast). Spike: ItemListData.maxWeight (Single) reflection 우선, 미발견 시 BepInEx Config InventoryMaxWeight/StorageMaxWeight (float, default 964/300) fallback.
- [v0.7.2](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.2) — D-3 컨테이너 검색·정렬: 글로벌 toolbar (검색 box + 카테고리/이름/등급/품질 4 sort key + ▲/▼) 1줄 + 3-area cache. Item itemLv/rareLv (등급/품질) reflection. Row text 등급 6단계 색상 (열악 회색 → 절세 빨강). 인게임 smoke 통과 + 2 bug fix (color JSON path itemLv 우선 / dropdown lazy load).
- [v0.7.3](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.3) — D-2 컨테이너 Item 시각 표시 풍부화 (placeholder cell): row 마다 24×24 cell prefix (등급 배경 6단계 + 우상단 품질 마름모 6단계 + 중앙 카테고리 한자 装/书/药/食/宝/材/马 + 우하단 강화 `+N` + 좌하단 착용 `착`). 신규 `CategoryGlyph` + `ItemCellRenderer` (Draw + GradeColor/QualityColor 단일 source). v0.7.2 검색·정렬 자산 100% 보존. IL2CPP IMGUI strip 회귀 2회 (Box 류 + GUILayoutUtility.GetLastRect) 발견 → `GUILayoutUtility.GetRect` 1-call 로 fallback. 진짜 game sprite 도입은 v0.8+ 별도 sub-project 후보.
- [v0.7.4](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.4) — D-1 Item 상세 panel (view-only, hybrid curated+raw): ContainerPanel row 좌측 cell 클릭 single-focus (cyan 외곽선) + toolbar `ⓘ 상세` 버튼으로 별도 ItemDetailPanel window. **Curated 섹션** = 카테고리별 한글 라벨 (장비: 강화/착용/특수 강화/무게 경감/무게/가격, 비급: 무공 ID/무게/가격, 단약·음식: 강화/추가 보정/무게/가격) — 보물·재료·말 은 후속 v0.7.4.x patch. **Raw fields 섹션** (접이식) = 모든 reflection 필드 dump (IL2CPP meta 필터). **컨테이너 area** (외부 디스크) 는 JSON path 라 ItemDetailPanel 데이터 미지원 — focus outline 만 표시. **Cell vs Toggle 분리**: cell 클릭 = single-select + focus, toggle 라벨 클릭 = multi-check (이동·복사 워크플로우, focus 해제). ContainerPanel X 닫기 시 ItemDetailPanel 도 sync close. 신규 `ItemDetailReflector` (GetRawFields + GetCuratedFields with sub-data wrapper unwrap) + `ItemDetailPanel` window. v0.7.2 검색·정렬 / v0.7.3 cell renderer 자산 100% 보존. 182/182 tests PASS + 인게임 smoke 6/6 PASS (4-iteration UX fix: ref equality / SetFocus call / single-select / Toggle clear + container focus). Sub-data wrapper inventory dump: `docs/superpowers/dumps/2026-05-03-v0.7.4-subdata-spike.md`. Item editor (수정 기능) 는 v0.7.7 후보 별도 sub-project.
- [v0.7.4.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.4.1) — Item 상세 panel 나머지 3 카테고리 curated (말 / 보물 / 재료). 7 카테고리 cover. 193 tests + smoke 12/12.
- [v0.7.5.2](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.5.2) — Cell 24×24 정사각형 + 한자 → 48×24 가로 직사각형 + 한글 라벨 (장비/단약/음식/비급/보물/재료/말). cell 내부 강화/착 마커 제거. 216 tests + smoke 11/11.
- [v0.7.5.1](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.5.1) — HangulDict stage 4 ModFix TranslationEngine fallback. 합성어 부분 한글화 (절세长矛 → 절세장검 등). 216/216 tests PASS, 인게임 smoke PASS.
- [v0.7.5](https://github.com/game-mod-project/long_yin_li_zhi_zhuan_mode/releases/tag/v0.7.5) — Item 한글화 — Hybrid 4단계 사전 (ModFix reflection > Sirius > 자체 CSV > LTLocalization). ContainerPanel/ItemDetailPanel 한자 노출 제거. bilingual 검색 + Korean 정렬. 212 tests + smoke 14/14.

## v0.7.1 Known Limitations
- **무공 list만 단독 Apply 시 active 장착 정보 손실** (의도된 동작 — 무공 active 도 같이 체크 권장).
- **Stat 미체크 시 일부 max/derived 값 부정확** (game update loop 한계 — best-effort).
- **컨테이너 → 게임 인벤토리 가득 참 시 부분 처리**: 처리 가능 갯수만 추가, 실패 항목은 컨테이너에 남김 + 토스트 알림.
- **컨테이너 Item 상세 / 아이콘 그리드 / 검색·정렬 / Item 한글화** 는 v0.7.2 / v0.7.3 / v0.7.4 / v0.7.5 (D-1/2/3/4) sub-project 에서 처리.
- **천부 휴식 회귀 fix 의 잠재 wipe path**: 현재 검증된 시나리오는 휴식 1회 cycle. 추가 wipe path (set_heroTagData 직접 reassign 등) 발견 시 RestKeepHeroTagPatch 에 동일 패턴 추가.

---

## 1. 한 줄 요약

BepInEx 6 IL2CPP 환경에서 플레이어 캐릭터 스냅샷을 20슬롯에 저장 / 관리하는 모드 + 컨테이너 (인벤/창고 ↔ 외부 디스크) 관리.
**현재 main baseline = v0.7.5.2** (Cell 가로 직사각형 + 한글 라벨 — 장비/단약/음식/비급/보물/재료/말. cell 내부 강화/착 마커 제거. 카테고리 직관성 향상. 216/216 tests PASS + 인게임 smoke 11/11 PASS).

**다음 세션 후속 sub-project**:
- ✅ ~~v0.7.4.x patch: 나머지 3 카테고리 curated — 말 / 보물 / 재료~~ (v0.7.4.1 release 완료, 2026-05-05)
- ✅ ~~v0.7.5 D-4 Item 한글화~~ (v0.7.5 release 완료, 2026-05-06) — Hybrid 4단계 사전. ContainerPanel + ItemDetailPanel 한자 노출 제거. bilingual 검색 + Korean 정렬.
- ✅ ~~v0.7.5.1 hotfix~~ (v0.7.5.1 release 완료, 2026-05-06) — HangulDict stage 4 ModFix TranslationEngine fallback. 합성어 부분 한글화 (절세长矛 → 절세장검 등). 216/216 tests + 인게임 smoke PASS.
- ✅ ~~v0.7.5.2 (cell 모양 + 한글 라벨)~~ (v0.7.5.2 release 완료, 2026-05-06) — Cell 24×24 정사각형 + 한자 → 48×24 가로 직사각형 + 한글 라벨. cell 내부 강화/착 마커 제거. 216 tests + smoke 11/11 PASS.
- **v0.7.6 (후속 sub-project): 설정 panel** — hotkey 변경 / 컨테이너 정원 / 창 크기 / 검색·정렬 영속화 옵션. 자체 IMGUI panel vs sinai BepInExConfigManager 위임 vs Hybrid 결정 게이트.
- **v0.7.7 (후보)**: Item editor — ItemDetailPanel 의 view-only 필드를 edit-able 로 확장 (강화 lv 직접 변경, equipUseSpeAddValue 같은 sub-data 직접 수정). game-self method 우선 + reflection setter fallback. v0.7.4 의 ItemFieldExtractor 자산 baseline
- v0.7.8: Apply 부분 미리보기 — 선택한 카테고리 적용 시 전후 비교
- v0.7.9: Slot diff preview — Apply 전 어떤 필드가 바뀔지 미리보기 (스탯/장비/무공 차이 시각화)
- v0.7.10: NPC 지원 — 캐릭터 선택 + apply target 확장 (heroID=0 외 다른 캐릭터)
- **v0.8 (후보)**: 진짜 game sprite 도입 — `ItemCellRenderer` 의 placeholder block 만 sprite blit 으로 교체. IL2CPP sprite asset 접근 + IMGUI texture caching challenge. v0.7.3 의 cell 구조가 baseline

각 sub-project 는 별도 brainstorming → spec → plan → impl cycle. 진입점은 ModeSelector 메뉴에 항목 추가.

**v0.5 PoC dual-track (외형 + 무공 active) — 양쪽 FAIL → release 안 함**:
- 외형 (G1 FAIL): `portraitID` field 부재. 진짜 외형 = `faceData / partPosture` sub-data wrapper graph (v0.4 ItemData 와 동일 패턴) → v0.6 통합 작업으로 deferred.
- active (G3 보수적 FAIL): Method path 발견 + read-back PASS, 그러나 게임 UI 미반영 + save→reload persistence 미검증 → v0.4 외형 패턴과 동일 (cache invalidate 별도 필요) → v0.6 통합 작업으로 deferred.

**v0.5 PoC 의 결정적 발견** (v0.6 production 자산):
- `kungfuSkills[i].equiped` 가 active 의 source-of-truth (NOT `nowActiveSkill` — v0.4 A3 FAIL 의 진짜 원인)
- `KungfuSkillLvData.skillID` 가 진짜 ID 필드 (NOT `kungfuID`)
- `HeroData.EquipSkill / UnequipSkill (KungfuSkillLvData wrapper, bool=true)` — active set/unset path
- 외형은 `HeroData.faceData (HeroFaceData)` + `partPosture (PartPostureData)` sub-data graph
- `HeroIconDirty / heroIconDirtyCount` (HeroData), `skillIconDirty / maxManaChanged` (KungfuSkillLvData) — UI refresh trigger 후보
- 자세한 evidence: `docs/superpowers/dumps/2026-05-01-*` 5 개 markdown

**v0.5 main 영향**: 양쪽 FAIL → release tag / dist zip / VERSION bump 안 함. **Foundation 변경은 보존** (`Capabilities.Appearance` flag, `FieldCategory.Appearance` enum, `ApplySelection.Appearance` flag, `KoreanStrings.Cat_Appearance`) — v0.6 production 의 prerequisite.

---

## 2. 현재 깃 히스토리 (v0.4 시점 발췌 — v0.5+ commit 은 `git log` 참조)

```
2d4b24e chore(release): remove HeroDataDumpV04 + [F12] handler (D16)             ← v0.4 release prep
a127206 docs: D15 smoke v0.4 PASS — 모든 항목 통과 (천부 fix eaf2938 후 17/17)
eaf2938 fix(core): RebuildHeroTagData JSON schema 정정 — heroTagData 가 Array
10069fd fix(ui): WindowH default 480→560 + toast on selection save failure
8f3edf1 feat(ui): v0.4 SlotDetailPanel 9-카테고리 체크박스 grid + 즉시 저장 wiring
c99d709 feat(strings): v0.4 — 9 카테고리 label + disabled suffix
4e60687 feat(core): selection-aware PinpointPatcher 9-step + Probe + ModWindow wiring
02e349e feat(core): IdentityFieldMatrix (Setter) + ItemDataFactory (v0.4 stub) per PoC results
e9894c7 feat(slots): SlotRepository.UpdateApplySelection — toggle 즉시 저장 path
748eaae feat(slots): _meta.applySelection schema + read/write + legacy fallback test
30626c9 feat(core): SimpleFieldMatrix Category enum + v0.4 17 entry
4a9be77 feat(core): v0.4 Capabilities POCO
256bfc5 feat(core): v0.4 ApplySelection POCO + JSON helpers + 4 tests
7d57fea poc: v0.4 ItemData PoC FAIL — defer to v0.5+
c83e808 poc: v0.4 ActiveKungfu PoC FAIL — defer to v0.5+
4887f01 poc: v0.4 Identity PoC PASS — setter direct (in-memory)
(이전) chore(release): v0.3.0 — VERSION bump + README/HANDOFF update             ← v0.3.0 tag
6929201 chore(release): remove HeroDataDump temp tool + [F12] handler            ← Task 21
ca194bb feat(ui): activate Apply/Restore buttons; remove temp smoke handlers     ← Task 18
853aa8f feat(ui): ModWindow.RequestApply / DoApply / AttemptAutoRestore wired    ← Task 16
6c89076 feat(core): RefreshSelfState step 6 (fatal) + smoke [F11+R]              ← Task 12
c20c237 feat(core): RebuildHeroTagData step 5 + smoke [F11+T]                    ← Task 11
a747996 fix(core): SimpleFieldMatrix 22→18 + Apply cache guard + doc 보강        ← Task 7-fix
470bbbc feat(core): SetSimpleFields step 1 + special-cases + smoke handler       ← Task 7
829cd7e docs: HeroData method dump + spec/plan refined per dump                  ← Task 2
13ed023 docs: v0.3 spec + plan — PinpointPatcher Apply pipeline design           ← Task 1
4ec9db5 docs: handoff bumped past v0.2.0 with v0.3 Apply replan
8c89fe4 docs: README — bump for v0.2 capabilities                                ← v0.2.0 tag
2286366 feat(slots+ui): import-from-game-save flow + scroll-wheel block          ← Task 21
17140aa fix(ui): Harmony-patch mouse input through mod window region             ← S0+S1
473763d docs: add README and prepare v0.1.0 release packaging                    ← v0.1.0 tag
b3e300d feat(ui): drop v0.1 Apply path, add slot edit handlers, polish window UX ← C-4 + D
6ba31eb docs: handoff update — Task 17 verified, IL2CPP traps documented
8d115d4 refactor(slots+ui): System.Text.Json migration + same-slot overwrite
59e3be2 fix(core): HeroLocator IL2CPP-compatible iteration
601602c docs: handoff document — work paused after Task 17
ca6d630 feat(capture): wire live capture — HeroLocator + SerializerService real impl   ← Task 17
266fca4 feat(ui): SlotListPanel + SlotDetailPanel with selection + action callbacks    ← Task 16
a780d98 feat(ui): ModWindow IMGUI shell with F11 toggle, drag, position persistence    ← Task 15
f04b805 feat(ui): ToastService with 3s auto-dismiss                                    ← Task 14
428f2b0 feat(config): bind BepInEx ConfigEntries                                       ← Task 13
f507e78 feat(core): PinpointPatcher skeleton with debug logging                        ← Task 12
7588833 feat(core): HeroLocator finds heroID=0 player via reflection                   ← Task 11 (rewritten in 59e3be2)
8b9eebd feat(core): SerializerService stub                                             ← Task 10 (rewritten/trimmed in b3e300d)
dc01499 fix(slots): bump ParseHeader default to 512KB
42c045f feat(slots): SaveFileScanner header parsing                                    ← Task 9 (rewritten in 2286366)
16c38ea feat(slots): SlotRepository with slot 0 auto-backup guard                      ← Task 8
3301432 feat(slots): atomic SlotFile I/O                                               ← Task 7 (rewritten in 8d115d4)
ea18a73 feat(slots): SlotMetadata + SlotPayload                                        ← Task 6 (rewritten in 8d115d4)
850fc55 feat(core): PortabilityFilter strips faction + runtime fields                  ← Task 5 (rewritten in b3e300d via System.Text.Json)
87d9b55 feat(util): KoreanStrings + PathProvider                                       ← Task 4
71614ca test: test project skeleton with frozen Hero fixture (35MB)                    ← Task 3
573738e refactor(plugin): Log → Logger; gate deploy target to Release
9b8e85e feat(plugin): bootstrap BepInEx plugin loading and logging                     ← Task 2
e45b164 fix(build): allow GameDir env-var override
bb17569 chore: initialize solution and shared build props                              ← Task 1
```

**Tags**: `v0.1.0` (at `473763d`), `v0.2.0` (at `8c89fe4`), `v0.3.0` (pending — Task 23).
**Branch**: v0.3 (21+ commits ahead of main).

---

## 3. 프로젝트 구조 (현재 디스크 상태)

```
_PlayerExport/
├── README.md                  ← 사용자 가이드 (v0.2 기능 + v0.3 deferred 명시)
├── LongYinRoster.sln
├── Directory.Build.props      ← GameDir env-var fallback
├── .gitignore                 ← .superpowers/ + .omc/ + dist/ 제외
├── docs/
│   ├── HANDOFF.md             ← 이 파일
│   └── superpowers/
│       ├── specs/2026-04-27-longyin-roster-mod-design.md   (~600줄)
│       └── plans/2026-04-27-longyin-roster-mod-plan.md     (~3000줄)
├── dist/                      ← gitignore. v0.1.0 / v0.2.0 zip + 폴더 구조
└── src/
    ├── LongYinRoster/                       ← 메인 플러그인
    │   ├── LongYinRoster.csproj             ← 0Harmony / Il2CppInterop / Newtonsoft 등 reference
    │   ├── Plugin.cs                        ← BepInPlugin entry + Harmony.PatchAll
    │   ├── Config.cs                        ← PauseGameWhileOpen default true
    │   ├── Core/
    │   │   ├── HeroLocator.cs               ← reflection Count + indexer (IL2CPP-compatible)
    │   │   ├── SerializerService.cs         ← Serialize 만 유지 (Populate / DeserializeHero 제거됨)
    │   │   ├── PortabilityFilter.cs         ← System.Text.Json 으로 재작성. 24+21=45 필드 제거
    │   │   └── PinpointPatcher.cs           ← v0.2 까지 무동작 스켈레톤. v0.3 의 Apply 핵심
    │   ├── Slots/
    │   │   ├── SlotPayload.cs               ← Player = raw JSON string
    │   │   ├── SlotMetadata.cs              ← System.Text.Json 으로 메타 추출
    │   │   ├── SlotEntry.cs
    │   │   ├── SlotFile.cs                  ← System.Text.Json Read/Write + atomic .tmp→Replace
    │   │   ├── SlotRepository.cs            ← 21슬롯 (0=자동백업 + 1~20)
    │   │   └── SaveFileScanner.cs           ← System.Text.Json. LoadHero0 → raw JSON string
    │   ├── UI/
    │   │   ├── ModWindow.cs                 ← MonoBehaviour, F11, _instance + ShouldBlockMouse
    │   │   ├── SlotListPanel.cs             ← 왼쪽 21행 + [+] / [F]
    │   │   ├── SlotDetailPanel.cs           ← 오른쪽 상세. Apply/Restore 버튼 disabled (v0.3 예정)
    │   │   ├── ConfirmDialog.cs             ← 재사용 modal — IL2CPP-safe IMGUI 패턴 정립
    │   │   ├── InputDialog.cs               ← 텍스트 입력 modal (Rename/Comment)
    │   │   ├── FilePickerDialog.cs          ← SaveSlot 0~10 list + 클릭 import
    │   │   ├── DialogStyle.cs               ← 재사용 0.85α 검정 overlay (불투명도 보강)
    │   │   ├── InputBlockerPatch.cs         ← Harmony Prefix on Input.GetMouseButton* / GetAxis
    │   │   └── ToastService.cs              ← 3초 자동소멸
    │   └── Util/
    │       ├── Logger.cs                    ← BepInEx 로거 래퍼
    │       ├── KoreanStrings.cs             ← UI 문자열 상수 (Apply / Capture overwrite / Input dialogs)
    │       └── PathProvider.cs              ← <PluginPath> 토큰 + GameSaveDir
    └── LongYinRoster.Tests/                 ← 테스트 (18 tests, all pass)
        ├── LongYinRoster.Tests.csproj
        ├── fixtures/slot3_hero.json         ← 35MB 동결 baseline
        ├── SmokeTests.cs
        ├── PortabilityFilterTests.cs        (4)
        ├── SlotMetadataTests.cs              (1)  ← string 입력
        ├── SlotFileTests.cs                  (3)  ← string Player + JsonDocument 검증
        ├── SlotRepositoryTests.cs            (7)
        └── SaveFileScannerTests.cs           (2)
```

**테스트**: `dotnet test` → **18/18 PASS**.
**빌드**: `dotnet build -c Release` → 자동으로 `BepInEx/plugins/LongYinRoster/LongYinRoster.dll` 배포.

---

## 4. 가장 중요한 발견 (다음 세션 작업자 필독)

### 4.1 IL2CPP-bound Newtonsoft.Json API gap

`BepInEx/interop/Newtonsoft.Json.dll` 은 표준 NuGet 버전과 **다른 표면 + 다른 type identity**.

| 표준 Newtonsoft | IL2CPP 버전 | 우리 대응 |
|---|---|---|
| `JsonConvert.SerializeObject(obj, settings)` | ❌ 1+settings 오버로드 없음 | `(Il2CppSystem.Object)` 단일 인자 |
| `JsonConvert.PopulateObject(json, obj, settings)` | ❌ **부재** | (해당 없음 — Populate 자체 deprecated) |
| `JsonSerializer.Populate(JsonReader, Object)` | ⚠ **silent no-op** in IL2CPP | **사용 금지**. v0.3 에서 PinpointPatcher 로 우회 |
| `JsonConvert.DeserializeObject(string, Type)` | ⚠ `Il2CppSystem.Type` 받음 | `Il2CppType.From(systemType)` 변환 후 호출. 결과는 `Il2CppSystem.Object`, wrapper class 의 `IntPtr` ctor 로 다시 감싸야 |
| `JArray` cast (`as JArray` / `(IList<JToken>)`) | ❌ **silently null** — type identity 충돌 | **System.Text.Json 으로 우회** (모든 슬롯/파일 경로) |
| `(JObject)root["player"]` cast | ❌ ArgumentNullException 또는 silently null | 동상 |
| `JObject` foreach (C# duck-typed) | ❌ enumerator 호환 안 됨 | `for (int i = 0; i < arr.Count; i++)` |
| `JsonTextReader` ctor | takes `Il2CppSystem.IO.TextReader` | `new Il2CppSystem.IO.StringReader(json)` |

**현재 코드베이스의 정책**: IL2CPP-bound Newtonsoft 는 **`SerializerService.Serialize` 한 곳에서만** 사용 (game 객체 → JSON string, 1방향). 그 결과 string 은 **System.Text.Json** 으로만 traverse. SlotPayload.Player 도 raw string. JObject / JArray 는 코드베이스 어디에서도 더 이상 instance 로 다루지 않음.

### 4.2 IL2CPP-bound `Il2CppSystem.Collections.Generic.List<T>` 는 .NET IEnumerable 미구현

`HeroList` 가 `Il2CppSystem.Collections.Generic.List<HeroData>` 로 노출되는데 우리 .NET 의 `System.Collections.IEnumerable` 을 구현하지 않는다. `is IEnumerable` 검사 → false → foreach 불가.

**대응**: reflection 으로 `Count` property + `Item` indexer (또는 `get_Item(int)`) 호출. HeroLocator 의 핵심 패턴.

또한 `BindingFlags.Public | BindingFlags.Static` 만 사용하면 generic singleton base class 의 `Instance` property 가 derived wrapper class 에서 안 보임. **`BindingFlags.FlattenHierarchy` 필수**. IL2CPP wrapper 가 backing field 만 노출하는 경우도 있어 **property → field fallback** + 흔한 별명(`instance`, `_instance`, `s_Instance`, `s_instance`) 시도.

### 4.3 IL2CPP IMGUI strip — 매 frame `Method unstripping failed`

이 게임의 IL2CPP 빌드는 다음 IMGUI API 들을 strip:

- `GUILayout.FlexibleSpace()`
- `new GUIStyle(GUIStyle other)` ctor
- `GUILayout.Label(string, GUIStyle, GUILayoutOption[])` (그리고 다른 GUIStyle 받는 overload들)
- `GUILayout.Box(string, params GUILayoutOption[])` — v0.7.3 발견 → `GUILayout.Label("", options)` + `GUI.DrawTexture(rect, Texture2D.whiteTexture)` 조합으로 대체
- `GUI.Box(Rect, string)` — v0.7.3 발견 → `GUI.DrawTexture(rect, Texture2D.whiteTexture)` + `GUI.color` 로 대체
- `GUILayoutUtility.GetLastRect()` — v0.7.3 발견 → **`GUILayoutUtility.GetRect(w, h, options)`** 1-call API 로 대체
- 기타 GUIStyle 인자 받는 IMGUI overload 다수

**대응**: `GUILayout.Space(int)`, `GUILayout.Button(string, GUILayoutOption[])`, `GUILayout.Label(string)` 같은 **default skin + 단순 overload** 만 사용. 새 dialog 추가 시 ConfirmDialog / InputDialog / FilePickerDialog 의 패턴 그대로 따름:
- `GUI.enabled = true` 강제 (Draw 진입 + DrawWindow callback 시작)
- `GUILayout.Space(N)` 명시값으로 정렬 (FlexibleSpace 회피)
- try/catch 가드 (logging 폭주 방지 + 새 strip 발견 시 진단)
- IMGUI 패턴 추가 시 **v0.7.x production code 의 정확한 사용처 grep 검증** 필수 — v0.7.3 plan 의 "GetLastRect 검증됨" 주장이 실제로는 v0.7.2 코드에 미사용이었음 (회귀 발견 후 plan/spec 학습)
- v0.7.3 시점 strip-safe IMGUI patterns 전체 목록: `docs/superpowers/dumps/2026-05-03-v0.7.3-smoke-results.md` §자산 (검증된 method 20+ + 폐기 method 5)

### 4.4 Apply (slot → game) 의 깊은 IL2CPP 한계 (v0.3 의 핵심 도전)

**v0.2 가 시도한 두 접근 모두 실패**:

#### 시도 1 — `JsonSerializer.Populate(reader, target)` (in-place mutation)
- BepInEx 로그: `Populate succeeded on HeroData` (예외 없이 통과)
- 진단: `pre/post` stat snapshot 비교 → **변경 0건** (silent no-op)
- 가설: HeroData 의 setter (`set_X`) 들이 IL2CPP 빌드에서 strip 됨 (게임 자체가 setter 안 사용, [Serializable] 콜백만 사용). Newtonsoft reflection 이 strip 된 method 를 호출하려 하면 unstrip 또는 silent skip.

#### 시도 2 — `JsonConvert.DeserializeObject` + `HeroList[0]` reference swap
- 새 HeroData 인스턴스 생성 (`Il2CppSystem.Object` → wrapper class IntPtr ctor 로 wrap)
- `HeroList[0] = newHero` reflection setter 호출 → 작동
- 보존 필드 (force / location / relations) 는 현재 player JSON 에서 머지 후 deserialize
- **결과**: 부분 작동 — 캐릭터 본질 (이름 / 스탯 / 천부) 은 swap 됨
- **그러나 broken**:
  - 착용 장비 (`nowEquipment`) 복사 안됨 — game 의 ItemData 객체와 ID-link 못 함
  - 착용 무공 동상 — `equiped` flag 의 reference 해석 실패
  - 포트레이트 무너짐 — sprite asset reference 가 lazy-load. 새 HeroData 가 그 trigger 못 함
  - 문파 정보 보존 fail — MergePreservedFields 의 검증 부족
  - **save → reload 후 player 정보창 안 열림** — 일부 필드 inconsistent 로 NRE

#### v0.3 의 새 접근 — PinpointPatcher 패턴

**핵심 통찰**: HeroData 는 [Serializable] POCO 가 아니라 game-state graph 의 노드. 단순 JSON round-trip 으로는 reference link 복원 불가.

**제안 흐름** (`PinpointPatcher.RefreshAfterApply` 의 본질):
1. 슬롯 JSON 에서 simple-value 필드들 (heroName, age, fightScore, hp, maxhp, fame, heroTagPoint, baseAttri, totalAttri 등) 만 추출
2. 각 필드별로 **game 자체 method** (`SetX`, `ChangeX`, `AddX`, `RefreshX`) 호출
   - 예: hp 변경 → `player.hp = N` (setter strip) 대신 `player.ChangeHp(N - currentHp)` 또는 비슷한 game-internal method
3. ID-list 필드 (kungfuSkills, itemListData.allItem, selfStorage.allItem, heroTagData) 는 **slot 의 ID 들로 재구축**:
   - 현재 player 의 list clear (game-internal method)
   - slot JSON 의 각 entry 마다 `player.AddKungfuSkill(skillID, lv, ...)` 같은 method 호출
4. 보존 필드 (force / location / relations) 는 **건드리지 않음** — 현재 player 객체에 있는 그대로 남김
5. UI / cache invalidate — game-internal `RefreshXxx` method 호출

**가장 큰 미지수**: HeroData 의 game-self method 가 어떤 시그니처인지. Reverse-engineering 필요. LongYin InGame Cheat 의 Harmony patch list (BepInEx 로그) 에 일부 단서:
- `HeroData.RefreshMaxAttriAndSkill`
- `HeroData.GetMaxAttri / GetMaxFightSkill / GetMaxLivingSkill`
- `HeroData.AddSkillFightExp / AddSkillBookExp / BattleChangeSkillFightExp`
- `HeroData.ChangeLivingSkillExp / ChangeFavor / GetMaxFavor`
- `HeroData.GetFinalTravelSpeed`

이 method 들 외에도 더 있을 것. v0.3 시작 시 `Assembly-CSharp.dll` 의 HeroData 클래스 method 전체 목록 dump 하는 것이 첫 단계.

### 4.5 Time.timeScale = 0 만으로는 mouse input 차단 안 됨

**확인된 사실** (S0/S1 검증):
- `Time.timeScale = 0` 은 캐릭터 / NPC / 시간 진행은 멈추지만 **마우스 driven UI / 마을 건물 클릭은 정상 작동** (그 핸들러들이 timeScale 무시).
- 진짜 차단하려면 **Harmony Prefix on `UnityEngine.Input.GetMouseButton{Down,Up,}` + `Input.GetAxis("Mouse ScrollWheel")`** — `ModWindow.ShouldBlockMouse` 가 true 일 때 `__result = false / 0` 반환 + skip-original.

**LongYin InGame Cheat 도 같은 method patch** 함. Harmony 는 multiple prefix 를 모두 호출하고 어느 prefix 가 false 반환하면 short-circuit. priority 설정 불필요.

### 4.6 게임 내부 구조 (Assembly-CSharp.dll 조사 + 검증 결과)

- **플레이어 영웅 타입**: `HeroData` (전역 namespace, [Serializable])
- **싱글톤 매니저**: `GameDataController.Instance` — reflection 정상 접근 ✓
  - `.gameSaveData.HeroList` → `Il2CppSystem.Collections.Generic.List<HeroData>` (899개 영웅)
  - `.Save(int saveID)` / `.Load(int saveID)` — escape hatch 후보. v0.3 Apply 시도 시 Populate / swap 둘 다 실패하면 마지막 fallback (단점: 전체 game state reload, 다른 영웅 + 시간 + 위치 다 영향)
- **플레이어 식별**: `HeroList[0]` 의 `heroID == 0` (검증 완료)
- **HeroData 콜백**: `OnSerializingMethod`, `OnDeserializedMethod` — 우리 직렬화 경로에서 자동 발화

---

## 5. 검증된 것 / 검증 안 된 것

### ✅ 게임 안에서 검증 완료 (v0.1.0 + v0.2.0 + v0.3.0 + v0.4.0)
- BepInEx 가 우리 플러그인 정상 로드 (`Loaded LongYin Roster Mod v0.3.0` / v0.4.0)
- F11 핫키, 창 드래그, 위치 영속, 한글 텍스트 정상
- **40 unit tests all pass** (v0.4 추가: ApplySelection / Capabilities / IdentityFieldMatrix / legacy 호환)
- **라이브 캡처**: `[+]` → 슬롯 1 에 503KB JSON + 토스트
- **Slot list / Slot detail panel**: 갱신 + 캐릭터 정보 정상 표시
- **같은 슬롯 덮어쓰기**: ConfirmDialog → 취소/덮어쓰기 동작
- **슬롯 Rename / Comment / Delete**: InputDialog / ConfirmDialog 통합
- **FileImport** (v0.2): `[F] 파일에서` → SaveSlot list → import → `_meta.captureSource = "file"`
- **Mouse / Wheel input gating** (v0.2): 모드 창 / 다이얼로그 영역 안 클릭 / 스크롤 차단
- **Apply (slot → game)** (v0.3): `▼ 현재 플레이어로 덮어쓰기` → SimpleFieldMatrix +
  heroTagData rebuild + RefreshSelfState/RefreshExternalManagers 7-step pipeline (smoke C1 PASS)
- **Restore (slot 0 → game)** (v0.3): `↶ 복원` → Apply 직전 상태 복귀 (smoke C2 PASS)
- **자동백업** (v0.3): Apply 직전 슬롯 0 자동백업 + 실패 시 자동복원 (AttemptAutoRestore)
- **save → reload 후 정보창 정상** (v0.3): G1/G2/G3 통과 (v0.2 시도 2 의 NRE 실패점 통과)
- **보존 필드** (v0.3): force / location / relations 변경 안 됨 — 사회적 위치 유지
- **9-카테고리 체크박스 default 표시** (v0.4): 슬롯 선택 시 9개 체크박스 인라인 표시 (smoke D15 PASS)
- **Toggle 즉시 저장** (v0.4): 체크박스 토글 → `_meta.applySelection` 즉시 파일 저장
- **정체성 Apply** (v0.4): heroName / nickname / age 등 9 필드 setter direct Apply → save → reload PASS
- **천부 17/17** (v0.4): heroTagData JSON schema 정정 후 천부 17/17 Apply 정상 (eaf2938 fix)
- **Restore / RestoreAll** (v0.4): 선택 카테고리 Restore + 전체 Restore 정상 동작
- **disabled UI** (v0.4): 미지원 카테고리 (무공 active / 인벤토리 / 창고) UI 비활성화 표시
- **legacy 호환** (v0.4): v0.2/v0.3 슬롯 파일 무손실 — V03Default 자동 적용, 파일 건드리지 않음

### ⚪ v0.5+ 후보 (현재 미지원, deferred)
- **무공 active** — wrapper.lv vs nowActiveSkill ID semantic mismatch (PoC A3 FAIL). v0.5+ 에서 재조사
- **인벤토리 / 창고** — sub-data wrapper graph 미해결 (PoC A4 FAIL). v0.5+ 에서 게임 내부 Add method 추가 dump 필요
- **무공 list** — KungfuSkillLvData wrapper ctor 의 IL2CPP 한계. v0.5+ 후보
- **외형** (faceData / portraitID 등) — sprite reference lazy-load. v0.5+ 후보
- spec §12 v0.4 진행 상태 + deferred list 참고

### ⚠ 알려진 한계
- **`PauseGameWhileOpen = true`** — 캐릭터/NPC/시간은 멈추지만 일부 UI 트랜지션은 통과. Mouse
  Harmony patch 로 보완.
- **HeroData setter reflection** — 일반적인 Newtonsoft / reflection-driven property set 가
  silent no-op. v0.3/v0.4 는 game-self method / setter direct 로 우회.
- **무공 active / 인벤토리 / 창고** — v0.5.1 / v0.5.3 / v0.5.5 부터 활성화 완료. 무공 list 는 v0.5.2.
- **컨테이너 ↔ 외부 디스크 이동/복사/관리** — v0.7.0 부터. v0.7.1 에서 destination 별 분리 + 무게 표시 + 가드.

---

## 6. 다음 세션 — v0.7.4+ sub-project 또는 maintenance 모드

v0.7.3 출시 완료 (2026-05-03). D-2 컨테이너 Item 시각 표시 풍부화 (placeholder cell) 인게임 smoke 5/5 PASS + 166/166 unit tests PASS.

### 6.A v0.7.1 release 핵심 발견 (다음 세션 자산)

- **ItemListData capacity = `maxWeight` (Single/float, kg)** — 갯수 capacity 자체 부재. spike 로 확정 (`docs/superpowers/dumps/2026-05-03-v0.7.1-capacity-spike.md`).
- **사용자 게임 시스템 사실**: 인벤은 maxWeight 초과 허용 (속도 페널티), 창고는 hard cap.
- **`ItemListReflector.GetMaxWeight(itemList, fallback)`** helper 가 reflection 우선 + Config fallback 패턴. 다른 sub-data property 도 같은 패턴 적용 가능.
- **ContainerPanel layout pattern**: 좌측 vertical (인벤/창고 split with → 버튼) + 우측 (컨테이너 + ← 4 버튼 mirror) — v0.7.2~v0.7.5 D-1/D-2/D-3/D-4 의 표시 영역 base.
- **smoke 도중 발견된 새 통증 (D-4 한글화)**: ContainerPanel 의 item 이름이 baseline 중국어 그대로. 사용자가 한글 패치 mod 사용 중인데 본 모드는 그 i18n 사전 미경유.

### 6.B 다음 세션 첫 작업 후보

**컨테이너 D 계열** (작업 순서 D→C→A→B 사용자 채택):
1. ✅ **v0.7.2 D-3 검색·정렬** (release 완료)
2. ✅ **v0.7.3 D-2 Item 시각 표시 풍부화** (release 완료) — D-2 scope 가 "정사각형 sprite grid" → "List 풍부화 (24×24 placeholder cell prefix)" 로 재정의. row 마다 cell prefix 로 등급 배경 + 품질 마름모 + 카테고리 한자 + 강화 `+N` + 착용 `착` 시각 표시. 진짜 game sprite 는 v0.8+ 별도 후보. IL2CPP IMGUI strip 회귀 2회 (Box 류 + GUILayoutUtility.GetLastRect) 발견 → `GUILayoutUtility.GetRect` 1-call API 로 fallback (자세한 회귀 학습 + strip-safe IMGUI patterns 목록: `docs/superpowers/dumps/2026-05-03-v0.7.3-smoke-results.md`).
3. ✅ **v0.7.4 D-1 Item 상세 panel** (release 완료 — 본 commit) — view-only, hybrid curated+raw. ContainerPanel cell 클릭 single-focus (cyan 외곽선) + toolbar `ⓘ 상세` 버튼 → 별도 ItemDetailPanel window. **Curated 섹션** = 카테고리별 한글 라벨 (장비/비급/단약 우선 cover, 보물·재료·말 후속 patch). **Raw fields 섹션** (접이식) = 모든 reflection 필드 dump (IL2CPP meta 필터). **컨테이너 area** (외부 디스크) 는 JSON path 라 ItemDetailPanel 데이터 미지원 — focus outline 만 표시. **Cell vs Toggle 분리**: cell 클릭 = single-select + focus, toggle 라벨 = multi-check (이동·복사 워크플로우, focus 해제). 4-iteration smoke fix narrative + sub-data wrapper inventory: `docs/superpowers/dumps/2026-05-03-v0.7.4-smoke-results.md` + `docs/superpowers/dumps/2026-05-03-v0.7.4-subdata-spike.md`.
4. **v0.7.4.x patch (후보)** — 나머지 3 카테고리 curated — 말 우선 (HorseItemData 의 fightSpeed/normalSpeed/maxHp 등), 그 다음 보물 / 재료. v0.7.4 의 `ItemFieldExtractor.GetCuratedFields` 의 카테고리별 switch 만 확장하면 됨 (단순 patch — 별도 sub-project 안 함).
5. **v0.7.5 D-4 Item 한글화** — 한글 패치 mod 사전 hook 또는 자체 itemID→한글 사전. 사용자가 이미 한글 패치 mod 사용 중 (인게임 "절세大劍/정량长劍" 같은 grade prefix + 한자 합성 표시 확인됨). ContainerPanel + ItemDetailPanel item 이름이 현재 중국어 한자 노출. 사전 hook 가능성 우선 조사.

**캐릭터 관리 계열** (D 끝나면):
6. v0.7.6 설정 panel — hotkey / 컨테이너 정원 / 창 크기 / 검색·정렬 영속화 옵션
7. **v0.7.7 (후보) Item editor** — ItemDetailPanel 의 view-only 필드를 edit-able 로 확장 (강화 lv 직접 변경, equipUseSpeAddValue 같은 sub-data 직접 수정). game-self method 우선 + reflection setter fallback. v0.7.4 의 `ItemFieldExtractor` 자산 baseline. 별도 brainstorming → spec → plan → impl cycle.
8. v0.7.8 Apply 부분 미리보기
9. v0.7.9 Slot diff preview
10. v0.7.10 NPC 지원

**v0.8 (후보) — 진짜 game sprite 도입**:
- v0.7.3 의 `ItemCellRenderer` 의 placeholder block 만 game sprite blit 으로 교체 (cell 구조 + GradeColor/QualityColor/CategoryGlyph 자산 그대로 사용)
- challenge: IL2CPP sprite asset 접근 (`item.iconID` → Sprite/Texture2D resolution + lazy-load), IMGUI texture caching, sprite atlas 핸들링
- v0.7.2 spike 의 ItemData sub-data wrapper presence (`equipmentData` / `bookData` / etc.) + iconID field reflection 결과 활용 (v0.7.4 spike 가 이 inventory 를 한 번 더 확정)
- 별도 brainstorming → spec → plan → impl cycle

각 sub-project 는 별도 brainstorming → spec → plan → impl cycle.

---

## 7. 다음 세션을 위한 컨텍스트 압축본

**다음 세션 첫 메시지에 붙여넣을 요약**:

> LongYin Roster Mod — **main baseline = v0.7.4** (2026-05-03 release). D 계열 세 번째 sub-project (D-1 Item 상세 panel — view-only, hybrid curated+raw) 완료. 182/182 unit tests + 인게임 smoke 6/6 PASS (4-iteration UX fix).
> 프로젝트 루트: `E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport/`.
> 핸드오프: `docs/HANDOFF.md`. v0.7.4 spec: `docs/superpowers/specs/2026-05-03-longyin-roster-mod-v0.7.4-design.md`. plan: `docs/superpowers/plans/2026-05-03-longyin-roster-mod-v0.7.4-plan.md`.
> v0.7.4 smoke: `docs/superpowers/dumps/2026-05-03-v0.7.4-smoke-results.md`. v0.7.4 sub-data spike: `docs/superpowers/dumps/2026-05-03-v0.7.4-subdata-spike.md`.
>
> **v0.7.4 결과**: ContainerPanel row 좌측 cell 클릭 → single-focus (cyan 외곽선, area 별 단일 selection). toolbar `ⓘ 상세` 버튼 → 별도 ItemDetailPanel window 가 열려서 selected item 의 상세 정보 표시. 신규 `ItemFieldExtractor.GetRawFields(item)` (모든 reflection 필드 dump, IL2CPP meta 필터) + `GetCuratedFields(item)` (카테고리별 한글 라벨 + sub-data wrapper unwrap — 장비/비급/단약 우선 cover). 신규 `ItemDetailPanel` (window + curated 섹션 + raw 접이식 섹션). ContainerPanel X 닫기 시 ItemDetailPanel sync close. v0.7.2 검색·정렬 / v0.7.3 cell renderer 자산 100% 보존.
>
> **v0.7.4 핵심 UX 결정** (smoke 4-iteration fix):
> - **Cell 클릭** = single-select + focus (해당 row 만 체크 + cyan 외곽선)
> - **Toggle 라벨 클릭** = multi-check (이동·복사 워크플로우, focus 해제)
> - 1차: ref equality FAIL → IL2CPP wrapper 의 same-pointer 비교 issue, identity-based fallback 추가
> - 2차: SetFocus call 누락 → cell 클릭 핸들러 명시 호출
> - 3차: single-select UX 회귀 → cell 클릭이 다른 row 의 check 까지 끄도록 명시 동작
> - 4차: container area focus + Toggle 클릭 시 focus clear
>
> **v0.7.4 scope cover**:
> - 장비 (EquipmentData): 강화 / 착용 / 특수 강화 / 무게 경감 / 무게 / 가격
> - 비급 (BookData): 무공 ID / 무게 / 가격
> - 단약·음식 (UseItemData): 강화 / 추가 보정 / 무게 / 가격
> - **미지원**: 보물 / 재료 / 말 → v0.7.4.x patch 후보 (말 우선 — HorseItemData)
> - **컨테이너 area** (외부 디스크) JSON path 라 데이터 미지원 — focus outline 만 표시
>
> **다음 단계 후보**:
> - **v0.7.4.x patch** — 나머지 3 카테고리 curated 추가 (말 우선). `ItemFieldExtractor.GetCuratedFields` switch 확장만 (단순 patch)
> - **v0.7.5 D-4 Item 한글화** — 한글 패치 mod 사전 hook 또는 자체 사전. ContainerPanel + ItemDetailPanel item 이름 모두 한자 노출
> - **v0.7.6 설정 panel** — hotkey / 컨테이너 정원 / 창 크기 / 검색·정렬 영속화 옵션
> - **v0.7.7 (후보) Item editor** — ItemDetailPanel view-only 필드를 edit-able 로 확장. v0.7.4 `ItemFieldExtractor` 자산 baseline
> - **v0.8 (후보) 진짜 sprite 도입** — `ItemCellRenderer` placeholder block 만 sprite blit 으로 교체. IL2CPP sprite asset 접근 + IMGUI texture caching
> - **maintenance 모드** — 게임 patch 대응 대기

---

## 8. 빠른 명령어 모음

```bash
# 게임 닫고 빌드
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release

# 테스트
DOTNET_CLI_UI_LANGUAGE=en dotnet test

# 게임 닫혔는지 확인
tasklist | grep -i LongYinLiZhiZhuan

# 깃 풀 (다른 머신에서 작업 시)
git pull origin main

# BepInEx 로그 클리어 + 추적 (검증 사이클)
> "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
grep -n "HeroLocator\|toast\|Capture\|slot \|HeroData\." "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"

# 슬롯 디렉터리 확인 + 깨끗하게 시작
ls "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/"
rm -f "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/"slot_*.json

# 슬롯 메타 빠른 확인
python -c "import json; d=json.load(open('E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/plugins/LongYinRoster/Slots/slot_01.json', encoding='utf-8-sig')); print(json.dumps(d['_meta']['summary'], ensure_ascii=False, indent=2))"

# v0.x release 패키징 (PowerShell)
# Compress-Archive -Path "dist/LongYinRoster_v0.x.0/*" -DestinationPath "dist/LongYinRoster_v0.x.0.zip" -Force

# GitHub release (gh CLI)
# gh release create v0.x.0 dist/LongYinRoster_v0.x.0.zip --title "..." --notes-file release-notes.md
```
