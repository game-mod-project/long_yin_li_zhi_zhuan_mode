# LongYinRoster v0.7.4.1 — Item 상세 panel 나머지 3 카테고리 curated (말 / 보물 / 재료)

**일시**: 2026-05-05
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.1
**baseline**: v0.7.4 (Item 상세 panel view-only — 장비/비급/단약 curated 완료, 182/182 tests + 인게임 smoke 6/6 PASS)
**sub-project 단위**: 단일 patch (말+보물+재료 한 번 release — 메타 §2.1 결정 게이트 (i) 채택)
**design input**: [`dumps/2026-05-03-v0.7.4-subdata-spike.md`](../dumps/2026-05-03-v0.7.4-subdata-spike.md), [`dumps/2026-05-05-v075-cheat-feature-reference.md`](../dumps/2026-05-05-v075-cheat-feature-reference.md) §2 (LongYinCheat `ItemGenerator.GenerateHorseData/GenerateTreasure/GenerateMaterial` 시그니처 reference)

## 0. 한 줄 요약

`ItemDetailReflector.GetCuratedFields` switch 에 case 4 (보물) / 5 (재료) / 6 (말) 추가. v0.7.4 의 `GetEquipmentDetails / GetBookDetails / GetMedFoodDetails` 패턴 그대로 답습. ItemDetailPanel curated 섹션이 7 카테고리 모두 cover.

## 1. 결정 사항

| 항목 | 결정 | 메타 spec 참조 |
|---|---|---|
| Patch 단위 | **(i) 단일 v0.7.4.1** (말+보물+재료 한 번 release) | §2.1 결정 게이트 |
| Curated 분량 | **(A) Minimal** — 말 12 / 보물 4 / 재료 2 | brainstorm Q2 |
| Code 구조 | v0.7.4 패턴 답습 — switch case 3개 + 3 신규 `GetXxxDetails` private method + `AddBaseAdd` helper | brainstorm Section 1 |
| Test 패턴 | v0.7.4 fake POCO + reflection 검증, ~14 신규 case (총 182 → ~196) | brainstorm Section 2 |
| Smoke | 6 신규 시나리오 (말 1 / 보물 2 / 재료 1 / 회귀 1 / 컨테이너 area 1) | brainstorm Section 2 |

## 2. Code 변경 명세

### 2.1 `src/LongYinRoster/Core/ItemDetailReflector.cs`

**switch 확장**:
```csharp
public static List<(string Label, string Value)> GetCuratedFields(object? item)
{
    int t = ReadInt(item, "type");
    return t switch {
        0 => GetEquipmentDetails(item),
        2 => GetMedFoodDetails(item),
        3 => GetBookDetails(item),
        4 => GetTreasureDetails(item),   // ← 신규
        5 => GetMaterialDetails(item),   // ← 신규
        6 => GetHorseDetails(item),      // ← 신규
        _ => new List<(string, string)>(),
    };
}
```

**`GetHorseDetails` — 12 필드** (착용중 / 4 stat × base+Add 합산 / maxWeightAdd / favorRate / 무게 / 가격):

```csharp
private static List<(string,string)> GetHorseDetails(object item)
{
    var result = new List<(string,string)>();
    var hd = ReadObj(item, "horseData");
    if (hd != null)
    {
        result.Add(("착용중", ReadBool(hd, "equiped") ? "예" : "아니오"));
        AddBaseAdd(result, hd, "속도", "speed", "speedAdd");
        AddBaseAdd(result, hd, "힘", "power", "powerAdd");
        AddBaseAdd(result, hd, "스프린트", "sprint", "sprintAdd");
        AddBaseAdd(result, hd, "인내", "resist", "resistAdd");
        float mwa = ReadFloat(hd, "maxWeightAdd");
        if (mwa > 0f) result.Add(("최대무게 추가", $"+{mwa:F0}"));
        float favor = ReadFloat(hd, "favorRate");
        if (Math.Abs(favor - 1f) > 0.01f) result.Add(("호감 율", $"{favor:F2}"));
    }
    result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
    result.Add(("가격", ReadInt(item, "value").ToString()));
    return result;
}
```

**`GetTreasureDetails` — 4 필드** (완전 감정 / 감정 필요 지식 / 무게 / 가격):

```csharp
private static List<(string,string)> GetTreasureDetails(object item)
{
    var result = new List<(string,string)>();
    var td = ReadObj(item, "treasureData");
    if (td != null)
    {
        result.Add(("완전 감정", ReadBool(td, "fullIdentified") ? "예" : "아니오"));
        float ikn = ReadFloat(td, "identifyKnowledgeNeed");
        if (ikn > 0f) result.Add(("감정 필요 지식", $"{ikn:F0}"));
    }
    result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
    result.Add(("가격", ReadInt(item, "value").ToString()));
    return result;
}
```

**`GetMaterialDetails` — 2 필드** (무게 / 가격 — extraAddData 는 raw 위임):

```csharp
private static List<(string,string)> GetMaterialDetails(object item)
{
    var result = new List<(string,string)>();
    // materialData.extraAddData (HeroSpeAddData) 는 nested 객체 → raw 위임
    result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
    result.Add(("가격", ReadInt(item, "value").ToString()));
    return result;
}
```

**신규 helper `AddBaseAdd`** (말 4 stat 의 base+Add 합산 표시. private static, 다른 카테고리 미사용):

```csharp
// "100 (+10)"  또는 Add=0 시  "100"
private static void AddBaseAdd(List<(string,string)> result, object obj, string label, string baseField, string addField)
{
    float baseVal = ReadFloat(obj, baseField);
    float addVal = ReadFloat(obj, addField);
    string val = addVal > 0f ? $"{baseVal:F0} (+{addVal:F0})" : $"{baseVal:F0}";
    result.Add((label, val));
}
```

**기존 helper 재사용**: `ReadObj` / `ReadBool` / `ReadFloat` / `ReadInt` (v0.7.4 에 이미 존재 — 변경 없음).

### 2.2 LOC 추정

- impl 추가: ~80 LOC (3 method + 1 helper + switch 3 case)
- test 추가: ~150 LOC (~14 case + 3 fake POCO)

## 3. Test 변경 명세

### 3.1 신규 fake POCO (in `ItemDetailReflectorCuratedTests.cs` 또는 분리 파일)

```csharp
internal class FakeHorseData
{
    public bool equiped;
    public float speed, speedAdd;
    public float power, powerAdd;
    public float sprint, sprintAdd;
    public float resist, resistAdd;
    public float maxWeightAdd;
    public float favorRate = 1f;
    // 동적 필드 (raw 위임) — fake 는 시그니처만:
    public float nowPower, sprintTimeLeft, sprintTimeCd;
}

internal class FakeTreasureData
{
    public bool fullIdentified;
    public float identifyKnowledgeNeed;
}

internal class FakeMaterialData
{
    // curated 필드 없음 — extraAddData 는 raw 위임이라 fake 시그니처만 (placeholder)
    public object? extraAddData;
}
```

`FakeItemData` 확장 — 기존 `equipmentData / bookData / medFoodData` 옆에 `horseData / treasureData / materialData` 3 wrapper field 추가.

### 3.2 신규 test 케이스 (Theory + Fact 혼합, ~14)

**말 (`Curated_Horse_*`, 7~8 case)**:
- `Curated_Horse_BasicEquipped` — equiped=true, base 4 stat, Add=0 → "착용중: 예" + 4 bare values + 무게/가격
- `Curated_Horse_WithAdd` — speedAdd=10 → "속도: 100 (+10)" 합산 형식
- `Curated_Horse_NotEquipped` — equiped=false → "착용중: 아니오"
- `Curated_Horse_FavorRateNonDefault` — favorRate=1.5 → "호감 율: 1.50" 추가
- `Curated_Horse_FavorRateDefault` — favorRate=1.0 → 행 미표시
- `Curated_Horse_MaxWeightAdd` — maxWeightAdd=20 → "최대무게 추가: +20" 추가
- `Curated_Horse_MaxWeightAdd_Zero` — maxWeightAdd=0 → 행 미표시
- `Curated_Horse_NullWrapper` — horseData=null → curated = 무게/가격 2행

**보물 (`Curated_Treasure_*`, 4 case)**:
- `Curated_Treasure_FullIdentified` — fullIdentified=true, ikn=120 → "완전 감정: 예" + "감정 필요 지식: 120" + 무게/가격
- `Curated_Treasure_NotIdentified` — fullIdentified=false → "완전 감정: 아니오"
- `Curated_Treasure_IknZero` — ikn=0 → 행 미표시
- `Curated_Treasure_NullWrapper` — treasureData=null → curated = 무게/가격 2행

**재료 (`Curated_Material_*`, 1 case)**:
- `Curated_Material_Standard` — type=5 → curated = 무게/가격 2행 (extraAddData 영향 없음)

**unknown type (`Curated_UnknownType_*`, 2 case)**:
- `Curated_TypeOne_ReturnsEmpty` — type=1 → empty
- `Curated_TypeNegative_ReturnsEmpty` — type=-1 → empty

### 3.3 회귀

- v0.7.4 의 `Curated_Equipment_* / Curated_Book_* / Curated_MedFood_*` 모두 PASS 유지 (switch case 추가만 한 변경 — 기존 case 동작 변경 없음)
- 다른 unit test (`ContainerPanelFormatTests`, `ItemCellRendererHelperTests`, `ContainerPanelFocusTests` 등) 도 PASS

총합: **182 → ~196**.

## 4. 인게임 Smoke (release 직전, 12/12)

| # | 시나리오 | 기대 |
|---|---|---|
| 1 | 인벤에 말 1개 → ContainerPanel cell 클릭 → ⓘ | curated 행 표시 — 착용중 / 속도/힘/스프린트/인내 (Add 있으면 합산) / maxWeightAdd 또는 favorRate 또는 둘 다 없음 / 무게 / 가격 |
| 2 | 보물 1개 (fullIdentified=true) → ⓘ | "완전 감정: 예" + "감정 필요 지식: N" + 무게/가격 |
| 3 | 보물 1개 (fullIdentified=false) → ⓘ | "완전 감정: 아니오" |
| 4 | 재료 1개 → ⓘ | curated 2행 (무게/가격) + raw 섹션 펼치면 extraAddData 객체 type 표시 |
| 5 | 회귀 — v0.7.4 장비/비급/단약 ⓘ | curated 표시 변동 없음 (각 카테고리 1 sample 검증) |
| 6 | 컨테이너 area item ⓘ | v0.7.4 와 동일 — 미지원 (focus outline 만, JSON path 데이터) |

신규 6 + 기존 6 회귀 = **12/12 smoke**.

Smoke dump 위치: `docs/superpowers/dumps/2026-05-05-v0.7.4.1-smoke-results.md`

## 5. Release & Cycle 정합 (메타 §5.1)

| Cycle 단계 | 산출물 |
|---|---|
| 1. brainstorm | 본 spec (commit 후 종료) |
| 2. spec | 본 파일 — git commit |
| 3. plan | `writing-plans` skill 호출 → `docs/superpowers/plans/2026-05-05-longyin-roster-mod-v0.7.4.1-plan.md` |
| 4. impl | `src/LongYinRoster/Core/ItemDetailReflector.cs` (~80 LOC 추가) + `src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs` (~150 LOC 추가) |
| 5. smoke | `docs/superpowers/dumps/2026-05-05-v0.7.4.1-smoke-results.md` (12/12) |
| 6. release | VERSION 0.7.4.1 + CHANGELOG entry + git tag v0.7.4.1 + GitHub release + dist zip |

### handoff 갱신 (release 후)

`docs/HANDOFF.md`:
- "현재 main baseline = v0.7.4" → "v0.7.4.1"
- "다음 sub-project" 의 v0.7.4.x 항목 ✅ 표시 + cursor → v0.7.5 D-4 한글화

### 메타 spec 갱신 (release 후)

`docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md` §2.1 — 상태 "확정 sub-project" → "✅ 완료 (v0.7.4.1, YYYY-MM-DD)" + release tag link + smoke dump 경로.

## 6. Out-of-scope (v0.7.4.x 미포함, 후속 sub-project)

- **HeroSpeAddData / ChangeHeroStateData / EquipPoisonData 같은 nested 객체의 1-depth curated 표시** — v0.7.7 Item editor 와 함께 처리 (nested editor 와 자연스러운 묶음)
- **음식 vs 단약 (subType=0 vs ≥1) 분리** — 별도 sub-project 또는 v0.7.5 한글화 시점에 ItemCategoryFilter 와 함께
- **장비 attriType / littleType 한글 매핑** — 별도 dictionary 작업, v0.7.5 한글화에 포함 가능
- **컨테이너 area (외부 디스크) curated 표시** — JSON path 데이터 미지원 (v0.7.4 와 동일 한계)
- **보물 List 들 (treasureLv / identifyDifficulty / identified / playerGuessTreasureLv)** — raw 섹션이 count 형식으로 이미 표시
- **말 동적 필드 (nowPower / sprintTimeLeft / sprintTimeCd)** — raw 섹션이 매 frame reflection 으로 표시

## 7. Risk

- **HorseData / TreasureData / MaterialData wrapper field 명 변동** — 게임 patch 시 영향. spike 가 v1.0.0f8.2 (현재 게임 빌드) 기준이라 maintenance 모드 진입 시 ItemDetailReflector.GetRawFields dump 로 재검증
- **base+Add 합산 표시의 culture 문제** — `:F0` 사용으로 culture-invariant 보장 (xUnit 환경에서 `,` vs `.` 영향 없음)
- **fake POCO ↔ IL2CPP 객체 시그니처 불일치** — v0.7.4 에서 검증된 ReadObj/Bool/Float/Int 패턴이 양쪽 모두 reflection path 동일하게 작동 (FieldInfo.GetValue 가 type-agnostic)
