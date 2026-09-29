# LongYin Roster Mod v0.7.13 — 아이템 생성기 (Item Generator) 설계

날짜: 2026-05-29
sub-project: v0.7.13 (이벤트 생성 v0.7.14 는 별도 후속)
입력 자산: `docs/superpowers/dumps/2026-05-29-ct-table-portability-analysis.md` (CT 메서드 카탈로그),
`docs/superpowers/dumps/2026-05-05-v075-cheat-feature-reference.md` (ItemGenerator 디컴파일 ref)
baseline: v0.7.12.3 (402 tests PASS)

---

## 1. 한 줄 요약

게임의 아이템 generator 메서드(`GenerateWeapon`/`GenerateBook`/`GenerateMedData`/…)를
Il2CppInterop reflection 으로 **직접 호출**해, 사용자가 7 카테고리의 구체 아이템을
선택·레벨/등급 지정·생성하여 인벤토리에 추가하는 전용 패널(F11 신규 항목)을 추가한다.

**핵심 통찰**: CT 테이블의 `MT.hook.*` 우회장치(shellcode/cmdBuf/메인스레드 후킹)는
CE가 게임 외부 스레드라 필요했던 것. 우리 모드는 in-process(Unity 메인 스레드)라
generator 를 reflection 으로 직접 부르면 된다 — CT는 메서드명/시그니처 자료집.

## 2. 목표 / 비목표

**목표**:
- 7 카테고리(장비[무기/갑옷/투구/신발/장신구/마구] / 비급 / 단약 / 음식 / 재료 / 말 / 보물) 구체 아이템 생성
- 구체 아이템 브라우저 (카테고리 → DB list[한글명+검색] → 선택 → 레벨/등급/수량 → 생성)
- 생성 아이템은 인벤토리에 추가 (게임 generator 가 스탯 완성)

**비목표 (v0.7.13 범위 외)**:
- 이벤트 생성 (v0.7.14)
- 영웅 생성 + 모집 / 자원 치트 / 실시간 토글 (dump §4 보류 목록)
- 창고로 직접 생성 (인벤 → ContainerPanel 로 이동)
- 랜덤 생성 모드 (구체 선택만)

## 3. 아키텍처

```
ItemGeneratorPanel (UI, F11 신규 항목 + 핫키)
   │  카테고리 → 아이템 선택 → 레벨/등급/수량 → [생성]
   ▼
ItemFactory.Generate(category, dbEntry, lv, rare, qty)   (Core)
   │  reflection 으로 게임 generator 직접 호출 (접근 1)
   ▼
gc.GenerateWeapon/Book/MedData/… → ItemData (스탯 완성)
   │
   ▼
player.itemListData.GetItem(item, false) + ItemListApplier.FinalizeNewItemWrapper(item)
   │
   ▼
ToastService + ItemListReflector.GetMaxWeight 무게 경고
```

데이터 소스: `ItemDbCache` 가 카테고리 DB 를 1회 열거 → entry list. 비급은 `SkillNameCache` 위임.

## 4. 신규 컴포넌트 (4개)

| 컴포넌트 | 위치 | 역할 |
|---|---|---|
| `ItemGeneratorPanel` | `UI/` | F11 메뉴 신규 항목 + 핫키. 카테고리 탭 + 장비 secondary 탭 + 검색 + 페이징 list + 레벨/등급/수량 입력 + [생성]. PlayerEditorPanel 무공 list UI 패턴 재사용 |
| `ItemFactory` | `Core/` | 카테고리 → generator 메서드 reflection 매핑 + 호출 + GetItem 추가. 접근 2 fallback |
| `ItemDbCache` | `Core/` | 7 카테고리 DB 열거 → `DbEntry` list (lazy init, thread-safe, SkillNameCache 패턴). 비급은 SkillNameCache 위임 |
| `GameControllerLocator` | `Core/` | `GameController.Instance` reflection 접근 (HeroLocator 패턴 mirror) |

**재사용 자산**: `HeroLocator`(player), `ItemListApplier.FinalizeNewItemWrapper` / `GetItem` 경로,
`CategoryGlyph` / `CategorySecondaryTabs`(탭 라벨), `HangulDict`(한자→한글), `ToastService`,
`ItemListReflector.GetMaxWeight`, `SkillNameCache`(비급), `SelectorDialog`, `Logger.InfoOnce/WarnOnce`.

## 5. 생성 엔진 상세 (`ItemFactory`)

`GameController.Instance`(gc) generator 를 reflection (name + param-count 매칭, 기존 `InvokeMethod`)
으로 호출. **정확한 C# 시그니처는 spike 로 확정** — 아래는 CT + 디컴파일 ref 기반 후보:

| 카테고리 | 게임 메서드 (후보) | param 후보 |
|---|---|---|
| 무기/갑옷/투구/신발/장신구 | `GenerateWeapon`/`Armor`/`Helmet`/`Shoes`/`Decoration` | `(itemLv, 0, 0, bossLv, player)` |
| 마구 | `GenerateHorseArmorData` | `(itemLv, bossLv)` |
| 비급 | `ItemData.SetBookData(skillID, rareLv)` (빈 ItemData type=3) 또는 `gc.GenerateBook` | `(skillID, rareLv)` |
| 단약 | `gc.GenerateMedData` | `(id, bossLv)` |
| 음식 | `gc.GenerateFoodData` | `(id, bossLv)` |
| 재료 | `gc.GenerateMaterial` | `(materialType, itemLv, bossLv)` |
| 말 | `gc.GenerateHorseData` → `horseData.tameRate = 1.0` | `(id, bossLv)` |
| 보물 | `gc.GenerateTreasure` | `(typeIdx, rareLv, bossLv)` |

**호출 패턴**:
1. generator 호출 → `ItemData` 반환
2. rareLv/itemLv 가 generator 인자에 반영 안 되면 reflection setter 보정 + `CountValueAndWeight()`
3. 말이면 `horseData.tameRate = 1.0`
4. `player.itemListData.GetItem(item, false)` + `FinalizeNewItemWrapper(item)`
5. 수량(qty)만큼 반복

**입력 단순화**: UI 의 "레벨" = `itemLv` = `bossLv` 동일 적용 (스탯 스케일 단순화). "등급/품질" = `rareLv`. 수량 1~99 (기본 1).

**Fallback (접근 2)**: generator resolve 실패 시 DB 템플릿 복제 경로 (CT `addEquipment` 방식 —
템플릿 필드 복사 + EquipmentData clone + 인벤 최고 장비 스탯 포인터 복사). 카테고리별 enable
플래그로 부분 운영. v0.7.13 에서는 generator 우선, fallback 은 미해결 카테고리만.

## 6. DB 열거 (`ItemDbCache`)

`GameDataController.Instance` / `GameController` 의 카테고리별 DB list 를 1회 순회 →
`DbEntry { Index, Id, NameRaw, NameKr, SubType, ItemLvDefault, RareLvDefault }`.
비급은 `SkillNameCache`(134+ entry, type 0~8) 위임. 한글명 `HangulDict.Translate`. lazy init + thread-safe.

**미지수 (spike 확정 대상)**:
- 각 DB 의 GDC 접근자 (CT raw offset: 무기 `gdc+0xF0` / 갑옷 `0xF8` / 투구 `0x100` / 신발 `0x108`
  → 우리는 **필드/property 이름 reflection 우선**, 미발견 시 offset fallback)
- 단약/음식/재료/보물/말 DB 접근자 이름
- 장신구/마구는 generator 가 static 항목 (CT: 향낭/옥선/반지/옥패/요대/면구 6종, 안구 1종) — DB index 가 아닌 sub-type 일 수 있음

## 7. 에러 처리

- generator/DB resolve 실패 → 해당 카테고리 탭 **disable** + `Logger.InfoOnce` (폭주 방지)
- player/gc null → toast "게임 로드 후 시도"
- 인벤 무게 초과 → 생성 허용하되 `ItemListReflector.GetMaxWeight` 기반 ⚠ 경고 toast
  (인벤은 over-weight 허용 — v0.7.1 게임 사실)
- 모든 reflection 호출 try/catch + `WarnOnce`
- IMGUI strip-safe: 기존 검증된 패턴만 (default skin + GUILayout.Space/Button/Label, GUILayoutUtility.GetRect 1-call)

## 8. 테스트 전략

기존 xUnit + Shouldly, `player=null` 테스트 모드 패턴 (reflection 호출은 게임 타입 부재로 test 회피):
- `ItemFactory` 카테고리 → 메서드명 매핑 테이블 (POCO, 매핑/param 구성 검증)
- `ItemDbCache` entry 파싱/필터/검색 (mock entry list)
- 한글명 fallback (`NameKr ?? NameRaw`)
- UI: 카테고리 탭 / secondary 탭 / 페이징 인덱스 계산 / 검색 필터
- 목표: 402 → ~420 tests

## 9. 구현 단계 (plan 에서 상세화)

1. **spike (최우선)** — `GameControllerLocator` + generator 1종(무기) + DB 1종 열거 인게임 검증.
   시그니처/오프셋/접근자 확정 후 나머지 6 카테고리 확장. (미지수 해소 게이트)
2. `ItemDbCache` — 7 카테고리 DB 열거 + 비급 SkillNameCache 위임
3. `ItemFactory` — 8 generator 매핑 + GetItem + FinalizeNewItemWrapper + 말 tameRate
4. `ItemGeneratorPanel` — UI (카테고리/secondary 탭 + 검색 + 페이징 list + 입력 + 생성) + F11 메뉴 wiring + 핫키 (Config + SettingsPanel rebind 통합)
5. 빌드 + 테스트(~420) + 인게임 smoke (7 카테고리 각 1종 생성 → 인벤 확인 → 세이브·로드 유지)

## 10. 위험 / 완화

| 위험 | 완화 |
|---|---|
| generator 시그니처가 후보와 다름 | spike 가 1단계 게이트. 미해결 카테고리는 fallback 또는 disable + 후속 patch |
| 장신구/마구 static 항목 구조 불명 | spike 에서 sub-type 기반인지 확인. 불명 시 v0.7.13.x 로 defer |
| 생성 아이템이 save 후 사라짐 (v0.7.12.2 회귀와 동일) | `FinalizeNewItemWrapper` 적용으로 선제 차단 |
| IMGUI strip 회귀 | 기존 strip-safe 패턴만, plan-time grep 검증 (MEMORY.md 규칙) |
| 패널 복잡도 | PlayerEditorPanel 무공 list 패턴 그대로 — 신규 IMGUI 0 목표 |
