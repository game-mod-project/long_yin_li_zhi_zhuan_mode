# LongYinRoster v0.7.7 — Item editor (ItemDetailPanel view-only → edit-able)

**일시**: 2026-05-08
**baseline**: v0.7.6 — 238/238 tests + 인게임 smoke 28/28 PASS
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.4 v0.7.7 1-pager + G1 Decision (2026-05-08, GO)

**brainstorm 결과 (2026-05-08)**:
- Q1 = **A** (단일 Phase) — 7 카테고리 × ~30 curated 필드 한 번에. Phase 분리 비용 > 가치 (매트릭스 작음).
- Q2 = **C** (Hybrid) — simple field 는 reflection setter, complex sub-data 변경 (skillID 같은 identity 영향) 은 `Clone + AddCloneWithLv` mirror 의 regenerate. read-back 검증 후 silent fail 시 regenerate fallback.
- Q3 = **D** (Aggressive sanitization) — Range check + `ItemData.CountValueAndWeight` (weight 재계산) + SaveDataSanitizer pattern (NaN/Infinity) + equipped 시 `RefreshSelfState` (player stat 재계산).
- Q4 = **A** (ItemDetailPanel 안 [편집 mode] 토글) — view-only 가 default, 명시적 mode 진입.
- Q5 = **B** (Disclaimer + 입력 range hardcoded) — `ItemEditField` 매트릭스. 자동 백업 안 함 (사용자 명시 capture 권장).
- Q6 = **A** (인벤·창고만 edit) — 외부 컨테이너 JSON path 는 view-only 유지. 사용자 워크플로우 = "컨테이너 → 인벤 이동 → 편집".

## 0. 한 줄 요약

신규 `ItemEditApplier` (Hybrid 적용 + Q3 sanitize pipeline) + `ItemEditField` (range 매트릭스 ~17 필드). `ItemDetailPanel` 에 [편집 mode] 토글 추가 — 활성 시 curated 라벨 옆에 textfield/dropdown/checkbox + [적용] 버튼. 인벤·창고 area 만. raw 섹션은 read-only 유지.

## 1. 디자인 결정 (brainstorm 전개)

### 1.1 Edit-able 필드 매트릭스 (Q1=A 단일 Phase)

| 카테고리 | 필드 | 타입 | Range | 적용 경로 (Q2 Hybrid) |
|---|---|---|---|---|
| **Common (모두 적용)** | `rareLv` | int | 0~5 | reflection setter |
| | `itemLv` | int | 0~5 | reflection setter |
| | `value` | int | 0~9999999 | reflection setter (`SaveDataSanitizer` clamp) |
| **Equipment (type=0)** | `equipmentData.enhanceLv` | int | 0~9 | reflection |
| | `equipmentData.speEnhanceLv` | int | 0~9 | reflection |
| | `equipmentData.speWeightLv` | int | 0~9 | reflection |
| **Book (type=3)** | `bookData.skillID` | int | game-defined (KungfuSkillDataBase iterate 후 검증) | regenerate fallback (identity-impact 큼) |
| **Med/Food (type=2)** | `medFoodData.enhanceLv` | int | 0~9 | reflection |
| | `medFoodData.randomSpeAddValue` | int | 0~999 | reflection |
| **Treasure (type=4)** | `treasureData.fullIdentified` | bool | true/false | reflection |
| | `treasureData.identifyKnowledgeNeed` | float | 0~9999 | reflection |
| **Horse (type=6)** | `horseData.speedAdd` | float | 0~9999 | reflection |
| | `horseData.powerAdd` | float | 0~9999 | reflection |
| | `horseData.sprintAdd` | float | 0~9999 | reflection |
| | `horseData.resistAdd` | float | 0~9999 | reflection |
| | `horseData.maxWeightAdd` | float | 0~99999 | reflection |
| | `horseData.favorRate` | float | 0.01~9.99 | reflection |
| **Material (type=5)** | (skip) | - | - | curated 거의 비어있음 — common 3개 (rareLv/itemLv/value) 만 |

총 **17 distinct edit field** (common 3 + 카테고리 14). Range 매트릭스 = `ItemEditField` POCO + readonly Dictionary.

### 1.2 적용 pipeline (Q2 Hybrid + Q3 Aggressive)

```
사용자 textfield 입력 → [적용] 버튼 클릭
  │
  ▼
1. ItemEditField.ParseAndValidate(string, out value)
     └─ parse 실패 → toast "잘못된 입력"
     └─ range 밖 → toast "범위: <min>~<max>"
  │
  ▼
2. SaveDataSanitizer.Sanitize(value, max, fallback)
     └─ NaN/Infinity → fallback (cheat dump §9 패턴 차용)
  │
  ▼
3. Reflection setter 시도
     └─ FieldInfo.SetValue OR PropertyInfo.SetValue
     └─ try/catch — IL2CPP NRE 방어
  │
  ▼
4. Read-back 검증
     └─ FieldInfo.GetValue 후 입력 값과 비교
     └─ 일치: PASS → step 6
     └─ 불일치 (silent fail) → step 5 fallback
  │
  ▼
5. Regenerate fallback (skillID 같은 identity-impact 필드)
     └─ ItemData.Clone() + TryCast<ItemData>() (cheat §2 패턴)
     └─ 새 객체에 reflection setter
     └─ ItemListData 의 list 에서 swap (기존 idx 위치)
     └─ 실패 시 toast "변경 실패: <field>"
  │
  ▼
6. ItemData.CountValueAndWeight() 호출
     └─ weight + value 자동 재계산 (game-self method)
     └─ weight 변경 시 ItemListData.weight (총합) 도 재계산
  │
  ▼
7. equipped 검사 (equipmentData.equiped == true 또는 horseData.equiped == true)
     └─ true → HeroData.RefreshSelfState() 호출 (player stat 재계산)
  │
  ▼
8. UI refresh
     └─ ContainerRowBuilder.FromGameAllItem 재호출 (ContainerPanel 의 row 갱신)
     └─ ItemDetailPanel 의 curated/raw 섹션 재렌더 (다음 frame OnGUI 자동)
     └─ Toast "✔ {field} = {value} 적용"
```

**Read-back 검증 (Step 4)**: HeroData 의 v0.2 strip 교훈 mirror — Newtonsoft Populate silent no-op. ItemData 의 sub-data setter 도 IL2CPP 빌드에서 strip 가능성 — read-back 으로 detect.

### 1.3 UI placement — [편집 mode] 토글 (Q4=A)

ItemDetailPanel 헤더 우측에 **[편집] 토글 버튼** (active=cyan, ContainerPanel 의 ⓘ 토글 패턴 mirror):

```
+ 아이템 상세 ─────── [편집] [X] +
| 헤더 (item name + cell) ────  |
| ▼ Curated                    |
|   강화: +5         [편집중: [3 ] [적용]] |
|   특수 강화: +2    [편집중: [2 ] [적용]] |
|   ...                        |
| ▼ Raw fields (접이식)         |
|   (read-only 유지 — 변경 안 함)|
+ ─────────────────────────────+
```

- **Mode = view (default)**: 현재 v0.7.4 동작 그대로
- **Mode = edit**: curated 섹션 라벨 옆에 textfield (또는 dropdown / checkbox) + 행마다 [적용] 버튼
- **Toggle 클릭 (cyan ↔ default)** = mode 전환. edit mode 는 컨테이너 area = 인벤·창고 일 때만 활성화. **컨테이너 area 의 외부 컨테이너 (Q6=A) 시 토글 disabled** + tooltip "외부 컨테이너는 편집 안 됨"
- **Disclaimer (mode=edit 시 panel 상단 1줄, Q5=B)**: "⚠ 편집한 값은 게임 save 후 영속. Apply/Restore 흐름과 별개"

### 1.4 적용 단위 = 행별 [적용] 버튼 (immediate vs batched)

- **(A) 행별 [적용]** (선택) — 각 textfield 옆 [적용] 버튼. 개별 필드 변경 즉시. 사용자 명확.
- **(B) panel 일괄 [모두 적용]** — 모든 textfield 한꺼번에 적용. Buffer 패턴 (v0.7.6 SettingsPanel mirror).
- **결정**: (A) — 사용자가 어느 필드를 변경했는지 즉시 보임. 부작용 (RefreshSelfState 호출) 도 필드 단위로 격리. v0.7.6 SettingsPanel 의 buffer 는 hotkey/rect 처럼 묶음 변경이 자연스러운 케이스, 본 sub-project 는 필드별 독립.

### 1.5 Book.skillID UI — dropdown vs textfield

- **(A) Textfield + 검증** — ID 직접 입력. 검증 = `GameDataController.kungfuSkillDataBase` iterate 후 존재 여부.
- **(B) Searchable dropdown** — KungfuSkillData list scrollable + 검색 box. 한글화된 name 으로 표시. 구현 비용 높음.
- **(C) Textfield + dropdown 보조** — textfield 가 default, "📋 무공 list" 버튼 클릭 시 modal dropdown.
- **결정**: (A) — 첫 release 단순화. 사용자가 ID 모르면 raw 섹션의 `bookData.skillID` 값 보고 직접 입력. (B)/(C) 는 G2 후속 patch 후보 (v0.7.7.1).

### 1.6 자동 백업 정책 (Q5=B)

자동 백업 **안 함** — 사용자 명시 capture (`F11 → [+]`) 권장. 이유:
- v0.3 의 자동 백업은 Apply (player swap) 전제. 본 sub-project = item-level mutation, player swap 없음.
- 자동 백업 의 storage 비용 (slot 0 매번 overwrite 부담) 큼.
- Disclaimer 1줄 + 사용자 책임 분리.

## 2. 변경 파일

### 2.1 신규 파일

#### 2.1.1 `src/LongYinRoster/Core/ItemEditField.cs` (~150 LOC)

```csharp
namespace LongYinRoster.Core;

public enum ItemEditFieldKind { Int, Float, Bool }

public sealed class ItemEditField
{
    public string Path { get; }            // "rareLv" 또는 "equipmentData.enhanceLv" — dot 구분
    public string KrLabel { get; }
    public ItemEditFieldKind Kind { get; }
    public float Min { get; }
    public float Max { get; }

    public ItemEditField(string path, string label, ItemEditFieldKind kind, float min, float max)
    { Path = path; KrLabel = label; Kind = kind; Min = min; Max = max; }

    public bool TryParse(string input, out object value, out string error) { /* int/float/bool parse + range */ }
}

public static class ItemEditFieldMatrix
{
    // 카테고리 type → 적용 가능 필드 list
    public static IReadOnlyList<ItemEditField> ForCategory(int type) => type switch
    {
        0 => Equipment, 2 => MedFood, 3 => Book,
        4 => Treasure, 5 => Material, 6 => Horse,
        _ => System.Array.Empty<ItemEditField>(),
    };

    public static readonly IReadOnlyList<ItemEditField> Common = new[]
    {
        new ItemEditField("rareLv", "등급", ItemEditFieldKind.Int, 0, 5),
        new ItemEditField("itemLv", "품질", ItemEditFieldKind.Int, 0, 5),
        new ItemEditField("value",  "가격", ItemEditFieldKind.Int, 0, 9999999),
    };

    public static readonly IReadOnlyList<ItemEditField> Equipment = new[]
    {
        Common[0], Common[1], Common[2],
        new ItemEditField("equipmentData.enhanceLv",    "강화",     ItemEditFieldKind.Int, 0, 9),
        new ItemEditField("equipmentData.speEnhanceLv", "특수 강화", ItemEditFieldKind.Int, 0, 9),
        new ItemEditField("equipmentData.speWeightLv",  "무게 경감", ItemEditFieldKind.Int, 0, 9),
    };
    // ... MedFood / Book / Treasure / Horse / Material 동상
}
```

#### 2.1.2 `src/LongYinRoster/Core/ItemEditApplier.cs` (~200 LOC)

```csharp
namespace LongYinRoster.Core;

public sealed class ItemEditResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }   // null 이면 PASS
    public string? Method { get; init; }  // "reflection" / "regenerate" / null
    public bool TriggeredRefreshSelfState { get; init; }
}

public static class ItemEditApplier
{
    /// <summary>
    /// item 의 path 에 value 를 적용. Hybrid (reflection 우선 + regenerate fallback) +
    /// Q3 Aggressive sanitize (CountValueAndWeight + SaveDataSanitizer + RefreshSelfState).
    /// </summary>
    public static ItemEditResult Apply(object item, ItemEditField field, object value, object? player)
    {
        // 1. SaveDataSanitizer
        // 2. Reflection setter
        // 3. Read-back 검증
        // 4. Silent fail → regenerate fallback (sub-data wrapper Clone)
        // 5. ItemData.CountValueAndWeight()
        // 6. equipped 검사 → RefreshSelfState
    }

    private static bool ApplyReflection(object item, string path, object value) { /* dot path resolve + setter */ }
    private static bool ApplyRegenerate(object item, string path, object value) { /* Clone + setter + list swap */ }
    private static object SanitizeValue(object value, ItemEditField field) { /* NaN/Infinity */ }
    private static bool IsEquipped(object item) { /* equipmentData.equiped OR horseData.equiped */ }
}
```

#### 2.1.3 `src/LongYinRoster.Tests/ItemEditFieldTests.cs`

```csharp
public class ItemEditFieldTests
{
    [Theory]
    [InlineData("3", true, 3)]
    [InlineData("-1", false, 0)]   // range 밖
    [InlineData("foo", false, 0)]  // parse 실패
    public void TryParse_Int_ValidatesRange(string input, bool ok, int expected) { ... }

    [Fact]
    public void Matrix_Equipment_HasEnhanceLv() { ... }

    [Fact]
    public void Matrix_Material_OnlyCommon() { ... }
}
```

#### 2.1.4 `src/LongYinRoster.Tests/ItemEditApplierTests.cs`

```csharp
public class ItemEditApplierTests
{
    // POCO mock — IL2CPP 의존성 없이 reflection setter 검증
    private sealed class FakeItem { public int rareLv; public int itemLv; public int value; public FakeEquipment? equipmentData; }
    private sealed class FakeEquipment { public int enhanceLv; public bool equiped; }

    [Fact]
    public void Apply_TopLevelInt_SetterPasses() { ... }

    [Fact]
    public void Apply_NestedSubData_SetterPasses() { ... }

    [Fact]
    public void Apply_RangeViolation_ReturnsError() { ... }

    [Fact]
    public void Apply_NaN_SanitizedToFallback() { ... }

    [Fact]
    public void Apply_InvalidPath_ReturnsError() { ... }
    // CountValueAndWeight / RefreshSelfState 호출은 IL2CPP 의존 → 인게임 smoke 만
}
```

### 2.2 변경 파일

#### 2.2.1 `src/LongYinRoster/UI/ItemDetailPanel.cs`

- [편집 mode] 토글 추가 (헤더 우측, [X] 옆)
- mode=edit 시 curated 행마다 textfield/dropdown/checkbox + [적용] 버튼
- raw 섹션은 mode 무관 read-only 유지
- 외부 컨테이너 area 시 토글 disabled

#### 2.2.2 `src/LongYinRoster/Util/KoreanStrings.cs`

```csharp
public const string EditModeBtn = "편집";
public const string EditApplyBtn = "적용";
public const string EditDisclaimer = "⚠ 편집한 값은 게임 save 후 영속. Apply/Restore 흐름과 별개";
public const string EditModeContainerOnly = "외부 컨테이너는 편집 안 됨 (인벤/창고만)";
public const string EditFieldRangeError = "{0} 범위: {1}~{2}";
public const string EditFieldParseError = "{0}: 잘못된 입력";
public const string EditApplyOk = "✔ {0} = {1} 적용";
public const string EditApplyFailed = "✘ 변경 실패: {0}";
```

#### 2.2.3 `src/LongYinRoster/UI/ContainerPanel.cs`

ContainerPanel 의 cell 클릭 → focus 갱신 시 ItemDetailPanel 의 edit mode 자동 reset (안전 — 다른 item 으로 focus 변경 시 미반영 textfield 폐기). v0.7.4 D-1 의 stale focus 패턴 mirror.

## 3. UI Layout (ItemDetailPanel — edit mode 활성)

```
+ 아이템 상세 ─── [편집✓] [X] +
| {item name (한글)}        |
| {category cell — visual}  |
| ⚠ 편집한 값은 게임 save 후... |
| ▼ Curated (편집 가능)       |
|   등급:        [3   ] [적용] |
|   품질:        [4   ] [적용] |
|   가격:        [1500] [적용] |
|   강화:        [+5  ] [적용] |
|   특수 강화:    [+2  ] [적용] |
|   무게 경감:    [+1  ] [적용] |
|   무게: 12.5 kg (재계산됨)    |
| ▼ Raw fields (read-only)    |
|   ▶ {expandable}           |
+ ─────────────────────────+
```

- **무게/value 표시 (read-only)**: CountValueAndWeight 후 자동 재계산되므로 표시만
- **Bool 필드** (treasure.fullIdentified): textfield 대신 [✓ 예 / ✗ 아니오] 토글
- **Float 필드** (horse.favorRate): textfield 0.01 단위 (TryParse 시 decimal)

## 4. Strip-safe IMGUI 검증

신규 IMGUI API 사용 안 함 — v0.7.6 까지 검증된 패턴만:
- `GUILayout.TextField(string, params)` (검증)
- `GUILayout.Toggle(bool, string, params)` (검증)
- `GUI.enabled = bool` (v0.7.6 검증)
- `Event.current.*` (v0.7.6 검증)

새 IMGUI API 도입 없음 → 별도 spike 불필요.

## 5. Test 변경

| Test | 신규 case |
|---|---|
| `ItemEditFieldTests` | TryParse Int/Float/Bool 각 valid/invalid/range — ~12 case |
| `ItemEditApplierTests` | reflection setter / nested path / range error / NaN sanitize / invalid path — ~8 case |
| 기존 238 → **~258 (+20)** |

CountValueAndWeight / RefreshSelfState 호출은 IL2CPP 의존 → 인게임 smoke 만.

## 6. 인게임 Smoke (예상 ~30 시나리오)

### 6.1 신규 시나리오

| Phase | # | 시나리오 |
|---|---|---|
| Mode toggle | E1 | ItemDetailPanel [편집] 토글 → cyan + curated 라벨 옆 textfield 표시 |
| Mode toggle | E2 | 외부 컨테이너 area focus 시 [편집] 토글 disabled + tooltip |
| Common 필드 | E3 | rareLv 변경 → toast + ContainerPanel row 색상 변경 (등급 색상 sync) |
| Common 필드 | E4 | itemLv 변경 → cell 우상단 마름모 색 변경 |
| Common 필드 | E5 | value 변경 → curated 가격 라벨 갱신 |
| Equipment | E6 | enhanceLv 변경 → cell `+N` marker 갱신 |
| Equipment | E7 | speEnhanceLv 변경 → curated "특수 강화" 갱신 |
| Equipment | E8 | speWeightLv 변경 → curated "무게 경감" 갱신 + weight 자동 재계산 |
| Equipment | E9 | equipped item enhanceLv 변경 → player stat (HP/공격력) 변경 (RefreshSelfState 호출) |
| Book | E10 | skillID 변경 (existing skillID) → reflection 통과 (curated "무공 ID" 갱신) |
| Book | E11 | skillID 변경 (silent fail trigger 시) → regenerate fallback PASS |
| Med/Food | E12 | enhanceLv 변경 |
| Med/Food | E13 | randomSpeAddValue 변경 |
| Treasure | E14 | fullIdentified 토글 → curated "완전 감정" 갱신 |
| Treasure | E15 | identifyKnowledgeNeed 변경 |
| Horse | E16 | speedAdd 변경 → curated "속도" base + Add 합산 표시 |
| Horse | E17 | maxWeightAdd 변경 + 무게 합산 영향 검증 |
| Horse | E18 | favorRate 변경 (0.01 단위 float) |
| Range error | E19 | enhanceLv = 99 입력 → toast "범위: 0~9" + textfield reset |
| Range error | E20 | value = -1 → toast "범위: 0~9999999" |
| Parse error | E21 | enhanceLv = "abc" → toast "잘못된 입력" |
| Save / reload | E22 | edit 후 게임 save → reload → 변경 값 영속 |
| Material | E23 | Material item edit (common 3 필드만) |
| Equipment unequipped | E24 | unequipped item edit → RefreshSelfState 호출 안 함 (성능) |
| ContainerPanel sync | E25 | edit 후 ContainerPanel row 라벨 (한글화 + 등급 색상) 자동 갱신 |
| Focus 변경 | E26 | edit mode 활성 + 다른 item cell 클릭 → textfield 미반영 buffer 폐기 + edit mode 유지 |
| Container area | E27 | 외부 컨테이너 item view-only (toggle disabled) — 회귀 검증 |
| Strip 회귀 | E28 | BepInEx LogOutput grep — 0 건 |

### 6.2 회귀 시나리오 (기존)

- v0.7.6 smoke 28 — 모두 PASS 기대 (특히 ItemDetailPanel 의 view-only 동작 회귀 + ContainerPanel 자동 영속화)

총 ~58 시나리오 (28 신규 + ~30 회귀).

## 7. Risk

### 7.1 ItemData reflection setter strip 위험 (HIGH)

**우려**: HeroData v0.2 시도 1 의 silent no-op 교훈. ItemData 의 sub-data setter 도 IL2CPP strip 가능성. cheat dump 의 `AddCloneWithLv` 패턴이 reflection setter 직접 호출하는 것 같지만 검증 필요.

**Spike 1 (impl 첫 단계)**: 인벤 첫 item 의 `equipmentData.enhanceLv = 5` reflection setter 호출 → read-back 검증 → strip 발견 시 즉시 fallback (regenerate via Clone) 활성화.

**Read-back 검증** (Step 4) 가 본 위험의 detection 메커니즘 — strip 시 Pipeline Step 5 의 regenerate fallback 자동 활성.

### 7.2 RefreshSelfState 부작용 (MEDIUM)

**우려**: equipped item 변경 시 RefreshSelfState 호출 → HP/Power/Mana derived 값 재계산. 사용자가 의도한 변경 외 다른 stat 도 변경될 수 있음.

**완화**: RefreshSelfState 는 player.maxhp 같은 base 값 만들지만 외부 효과 (천부, 무공 active 등) 도 반영. 사용자가 enhanceLv 만 바꿨는데 HP 가 10 늘면 의도한 결과. 부작용 발견 시 Q3 fallback (D → C: RefreshSelfState 생략) 으로 hotfix 가능.

### 7.3 Sub-data wrapper IL2CPP identity (MEDIUM)

**우려**: v0.7.4 D-1 spike 결과 — sub-data wrapper (`equipmentData` 등) 가 lazy-instance 또는 same-pointer-different-wrapper 가능. reflection setter 가 wrong wrapper 에 적용 가능.

**완화**: setter 직후 read-back 으로 mismatch 검출 (Step 4). 또한 `item.equipmentData = newWrapper` 같은 wrapper 자체 swap 안 함 — 내부 필드만 변경.

### 7.4 ItemData.Clone() 의 IL2CPP wrapper 안전성 (MEDIUM)

**우려**: regenerate fallback 의 `Clone + TryCast<ItemData>` 패턴이 cheat mod 검증됨이지만 사용자 mod 환경에서 다를 수 있음.

**완화**: Clone 후 read-back 으로 새 객체의 path 값 검증. 일치 시 list swap. 불일치 시 toast "변경 실패" + 원본 유지.

### 7.5 Book.skillID 변경 시 무공 list reference 망가뜨림 (MEDIUM)

**우려**: bookData.skillID = N 변경 시 N 이 GameDataController.kungfuSkillDataBase 에 없으면 무공 list 안에 invalid reference 발생.

**완화**: Step 1 의 ParseAndValidate 가 skillID 의 존재 여부 검증 — `kungfuSkillDataBase` iterate 후 미발견 시 toast "존재하지 않는 무공 ID" + 적용 거부.

### 7.6 사용자 입력 negative effect (LOW)

**우려**: 사용자가 enhanceLv = 9 같은 max 값 적용 시 game 의 다른 시스템 (combat formula 등) 이 가정한 range 초과로 부작용.

**완화**: Range hardcoded matrix (Q5=B) 가 game 자체 max 값 mirror — game 이 허용하는 범위 안. 안전.

## 8. Out-of-scope

- Common 3 필드 외 다른 ItemData 직접 필드 (예: `iconID`, `subType`) — v0.7.7 OOS
- Sub-data wrapper 의 nested 객체 (equipmentData.extraAddData / damageUseSpeAddValue 같은 60+ 종 HeroSpeAddData) — v0.7.8 후보
- Material 의 nested wrapper edit — v0.7.4.1 에서 curated 거의 비어있어서 본 sub-project 도 common 3 만
- Multi-select edit (여러 item 일괄) — 사용자 워크플로우 단순화 위해 OOS
- Undo / Redo — OOS (게임 save 가 영속화 trigger)
- Dropdown for Book.skillID — v0.7.7.1 후보
- 외부 컨테이너 (JSON path) edit — Q6=A 결정
- Auto backup before edit — Q5=B 결정 (사용자 명시 capture 권장)

## 9. Cycle 계획

per 메타 §5.1:
1. **brainstorm** = 본 spec (사용자 review + 승인 게이트)
2. **plan** = layer-by-layer (Spike 1 reflection setter strip 검증 → Layer 1 ItemEditField + ItemEditApplier + tests → Layer 2 ItemDetailPanel edit mode UI → Layer 3 ContainerPanel sync → Layer 4 smoke ~28)
3. **impl** = layer 별 단위 commit
4. **smoke** = 인게임 ~28 시나리오
5. **release** = v0.7.7 tag + GitHub release
6. **handoff** = HANDOFF.md / 메타 §2.4 Result + G2 게이트 진입

## 10. 명명 / 호환성

- 버전: `v0.7.7` (확정 sub-project, G1 GO 결정)
- spec slug: `longyin-roster-mod-v0.7.7-design`
- plan slug: `longyin-roster-mod-v0.7.7-plan`
- smoke dump: `2026-05-XX-v0.7.7-smoke-results.md`
- 사용자 cfg 영향: 신규 ConfigEntry 없음 — Hotkey/Container 섹션 그대로
- 슬롯 schema 영향: 없음 — Apply/Restore 흐름 무관, 별개 mutation
- 기존 v0.7.6 의 SettingsPanel / 영속화 패턴 그대로 유지

## 11. 다음 단계 (G2 진입 준비)

v0.7.7 release 직후 G2 결정 게이트 (메타 §3.2):
- v0.8 (후보) 진짜 sprite — IL2CPP sprite asset spike → GO/DEFER/NO-GO
- maintenance — game patch / 통팩 release / BepInEx 변경 trigger
- 신규 후보 (v0.7.8 Apply 미리보기 / v0.7.9 Slot diff / v0.7.10 NPC 지원) 평가

본 spec 통과 → plan 작성 → impl → smoke → release → G2.
