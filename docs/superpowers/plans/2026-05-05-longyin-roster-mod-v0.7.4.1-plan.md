# LongYinRoster v0.7.4.1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `ItemDetailReflector.GetCuratedFields` switch 에 case 4 (보물) / 5 (재료) / 6 (말) 추가하여 ItemDetailPanel curated 섹션이 7 카테고리 모두 cover. v0.7.4 패턴 그대로 답습 — 3 신규 `GetXxxDetails` private + `AddBaseAdd` helper.

**Architecture:** v0.7.4 의 `GetEquipmentDetails / GetBookDetails / GetMedFoodDetails` 패턴 답습. 카테고리당 별도 `private static GetXxxDetails(object item)` method 가 sub-data wrapper (treasureData / materialData / horseData) 의 핵심 필드를 한글 라벨 tuple list 로 변환. 말은 base+Add 합산 표시 ("100 (+10)") 위해 `AddBaseAdd` helper 신규. 재료의 nested `extraAddData` 와 보물의 List 들은 raw 섹션 위임.

**Tech Stack:** C# / .NET (BepInEx 6 IL2CPP), xUnit + Shouldly 단위 테스트.

**Spec:** [docs/superpowers/specs/2026-05-05-longyin-roster-mod-v0.7.4.1-design.md](../specs/2026-05-05-longyin-roster-mod-v0.7.4.1-design.md)
**Roadmap:** [docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md](../specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.1

---

## Task 1: 보물 (case 4) curated impl

**Files:**
- Modify: `src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs` — `_Treasure_ReturnsEmpty` test 삭제, 신규 4 case 추가
- Modify: `src/LongYinRoster/Core/ItemDetailReflector.cs` — `GetTreasureDetails` 신규 + switch case 4

- [ ] **Step 1: 기존 `_Treasure_ReturnsEmpty` test 삭제 + 신규 4 case 추가 (TDD red)**

`src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs` 의 100~107 라인 부근 — 기존 두 test 와 fake class 를 다음으로 교체:

```csharp
// ===== Treasure (type=4) — spike: fullIdentified / identifyKnowledgeNeed (List 들은 raw 위임) =====
private sealed class FakeTreasureData
{
    public bool fullIdentified = true;
    public float identifyKnowledgeNeed = 120f;
}
private sealed class FakeTreasureItem
{
    public string name = "紫檀琵琶";
    public int type = 4;
    public int subType = 0;
    public int itemLv = 5;
    public int rareLv = 5;
    public float weight = 12.0f;
    public int value = 80000;
    public FakeTreasureData? treasureData = new();
}

[Fact]
public void GetCuratedFields_Treasure_FullIdentified()
{
    var item = new FakeTreasureItem();
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "완전 감정" && x.Value == "예");
    curated.ShouldContain(x => x.Label == "감정 필요 지식" && x.Value == "120");
    curated.ShouldContain(x => x.Label == "무게" && x.Value == "12.0 kg");
    curated.ShouldContain(x => x.Label == "가격" && x.Value == "80000");
}

[Fact]
public void GetCuratedFields_Treasure_NotIdentified()
{
    var item = new FakeTreasureItem { treasureData = new FakeTreasureData { fullIdentified = false, identifyKnowledgeNeed = 80f } };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "완전 감정" && x.Value == "아니오");
    curated.ShouldContain(x => x.Label == "감정 필요 지식" && x.Value == "80");
}

[Fact]
public void GetCuratedFields_Treasure_IknZero_OmitsRow()
{
    var item = new FakeTreasureItem { treasureData = new FakeTreasureData { fullIdentified = true, identifyKnowledgeNeed = 0f } };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "완전 감정");
    curated.ShouldNotContain(x => x.Label == "감정 필요 지식");
}

[Fact]
public void GetCuratedFields_Treasure_NullWrapper_ReturnsTwoFields()
{
    var item = new FakeTreasureItem { treasureData = null };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.Count.ShouldBe(2);
    curated.ShouldContain(x => x.Label == "무게");
    curated.ShouldContain(x => x.Label == "가격");
}
```

기존 100~108 라인의 다음 두 test 삭제:
```csharp
// 삭제 대상 — 기존 unsupported test (Treasure 가 v0.7.4.1 에서 supported 됨)
private sealed class FakeTreasureItem { public int type = 4; }     // 위 신규 FakeTreasureItem 으로 교체됨
[Fact]
public void GetCuratedFields_Treasure_ReturnsEmpty() { ... }       // 삭제
```

(`FakeMaterialItem` / `_Material_ReturnsEmpty` 는 Task 2 에서 처리 — 지금은 그대로 둠.)

- [ ] **Step 2: Run tests to verify Treasure tests fail**

Run:
```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj --filter "FullyQualifiedName~Curated_Treasure" -v normal
```

Expected: 4 신규 Treasure test FAIL — GetCuratedFields 가 type=4 에 대해 빈 list 반환 (`switch _ => new()`).

- [ ] **Step 3: `GetTreasureDetails` 추가 + switch case 4**

`src/LongYinRoster/Core/ItemDetailReflector.cs` 의 `GetCuratedFields` switch (37~44 라인) 에 case 4 추가:

```csharp
return type switch
{
    0 => GetEquipmentDetails(item),
    2 => GetMedFoodDetails(item),
    3 => GetBookDetails(item),
    4 => GetTreasureDetails(item),   // ← 신규
    _ => new(),
};
```

`GetMedFoodDetails` (94 라인) 직후에 신규 method 추가:

```csharp
private static List<(string, string)> GetTreasureDetails(object item)
{
    var result = new List<(string, string)>();
    var td = ReadFieldOrProperty(item.GetType(), item, "treasureData");
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

- [ ] **Step 4: Run tests to verify Treasure tests pass + 회귀**

```bash
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj --filter "FullyQualifiedName~ItemDetailReflectorCuratedTests" -v normal
```

Expected:
- 신규 4 Treasure test PASS
- 기존 Equipment / Book / MedFood test 모두 PASS (회귀 없음)
- `_Material_ReturnsEmpty` 와 `_NullItem_ReturnsEmpty` 도 PASS (Task 2 에서 갱신)

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Core/ItemDetailReflector.cs src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs
git commit -m "$(cat <<'EOF'
feat(core): v0.7.4.1 — Treasure (type=4) curated 추가

ItemDetailReflector.GetCuratedFields switch 에 case 4 추가. GetTreasureDetails 가 fullIdentified / identifyKnowledgeNeed (>0 일 때만) 표시 + 무게 / 가격 공통. List 필드들 (treasureLv / identifyDifficulty / identified / playerGuessTreasureLv) 은 raw 섹션 위임.

기존 _Treasure_ReturnsEmpty test 4 신규 case 로 교체 (FullIdentified / NotIdentified / IknZero / NullWrapper).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 재료 (case 5) curated impl

**Files:**
- Modify: `src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs` — `_Material_ReturnsEmpty` test 삭제, 신규 1 case
- Modify: `src/LongYinRoster/Core/ItemDetailReflector.cs` — `GetMaterialDetails` 신규 + switch case 5

- [ ] **Step 1: 기존 `_Material_ReturnsEmpty` test 삭제 + 신규 case (TDD red)**

기존 `FakeMaterialItem` (101 라인) 과 `_Material_ReturnsEmpty` test (110~113 라인) 삭제하고 다음으로 교체:

```csharp
// ===== Material (type=5) — extraAddData 는 nested 객체 raw 위임 =====
private sealed class FakeMaterialItem
{
    public string name = "절세食材";
    public int type = 5;
    public int subType = 0;
    public int itemLv = 5;
    public int rareLv = 5;
    public float weight = 6.0f;
    public int value = 12800;
    public object? extraAddData = null;   // raw 위임이라 fake 시그니처만
}

[Fact]
public void GetCuratedFields_Material_Standard_ReturnsTwoFields()
{
    var item = new FakeMaterialItem();
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.Count.ShouldBe(2);
    curated.ShouldContain(x => x.Label == "무게" && x.Value == "6.0 kg");
    curated.ShouldContain(x => x.Label == "가격" && x.Value == "12800");
}
```

- [ ] **Step 2: Run tests to verify Material test fails**

```bash
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj --filter "FullyQualifiedName~Curated_Material" -v normal
```

Expected: `Curated_Material_Standard_ReturnsTwoFields` FAIL — GetCuratedFields 가 type=5 에 대해 빈 list 반환 (`switch _ => new()`), `Count.ShouldBe(2)` fail.

- [ ] **Step 3: `GetMaterialDetails` 추가 + switch case 5**

`src/LongYinRoster/Core/ItemDetailReflector.cs` 의 switch 에 case 5 추가:

```csharp
return type switch
{
    0 => GetEquipmentDetails(item),
    2 => GetMedFoodDetails(item),
    3 => GetBookDetails(item),
    4 => GetTreasureDetails(item),
    5 => GetMaterialDetails(item),   // ← 신규
    _ => new(),
};
```

`GetTreasureDetails` 직후에 신규 method:

```csharp
private static List<(string, string)> GetMaterialDetails(object item)
{
    var result = new List<(string, string)>();
    // materialData.extraAddData (HeroSpeAddData) 는 nested 객체 → raw 위임
    result.Add(("무게", $"{ReadFloat(item, "weight"):F1} kg"));
    result.Add(("가격", ReadInt(item, "value").ToString()));
    return result;
}
```

- [ ] **Step 4: Run tests to verify Material test passes + 회귀**

```bash
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj --filter "FullyQualifiedName~ItemDetailReflectorCuratedTests" -v normal
```

Expected: 모든 Curated test PASS (Equipment / Book / MedFood / Treasure 4 / Material 1 / NullItem).

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Core/ItemDetailReflector.cs src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs
git commit -m "$(cat <<'EOF'
feat(core): v0.7.4.1 — Material (type=5) curated 추가

ItemDetailReflector.GetCuratedFields switch 에 case 5 추가. GetMaterialDetails 는 무게 / 가격만 표시. extraAddData (HeroSpeAddData nested) 는 raw 섹션 위임.

기존 _Material_ReturnsEmpty test 를 _Material_Standard_ReturnsTwoFields 로 교체.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 말 (case 6) curated impl + AddBaseAdd helper

**Files:**
- Modify: `src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs` — FakeHorseData / FakeHorseItem 신규 + 8 신규 case
- Modify: `src/LongYinRoster/Core/ItemDetailReflector.cs` — `GetHorseDetails` + `AddBaseAdd` helper + switch case 6

- [ ] **Step 1: FakeHorseData / FakeHorseItem 신규 + 8 case 추가 (TDD red)**

`ItemDetailReflectorCuratedTests.cs` 의 Material section 직후에 추가:

```csharp
// ===== Horse (type=6) — spike: equiped + 4 stats × (base, Add) + maxWeightAdd + favorRate =====
private sealed class FakeHorseData
{
    public bool equiped = true;
    public float speed = 100f, speedAdd = 0f;
    public float power = 50f, powerAdd = 0f;
    public float sprint = 80f, sprintAdd = 0f;
    public float resist = 70f, resistAdd = 0f;
    public float maxWeightAdd = 0f;
    public float favorRate = 1f;
    // 동적 필드 (raw 위임) — fake 는 시그니처만
    public float nowPower = 100f;
    public float sprintTimeLeft = 0f;
    public float sprintTimeCd = 0f;
}
private sealed class FakeHorseItem
{
    public string name = "神凫马";
    public int type = 6;
    public int subType = 0;
    public int itemLv = 5;
    public int rareLv = 5;
    public float weight = 24.0f;
    public int value = 100000;
    public FakeHorseData? horseData = new();
}

[Fact]
public void GetCuratedFields_Horse_BasicEquipped()
{
    var item = new FakeHorseItem();
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "착용중" && x.Value == "예");
    curated.ShouldContain(x => x.Label == "속도" && x.Value == "100");
    curated.ShouldContain(x => x.Label == "힘" && x.Value == "50");
    curated.ShouldContain(x => x.Label == "스프린트" && x.Value == "80");
    curated.ShouldContain(x => x.Label == "인내" && x.Value == "70");
    curated.ShouldNotContain(x => x.Label == "최대무게 추가");   // maxWeightAdd=0 → 미표시
    curated.ShouldNotContain(x => x.Label == "호감 율");          // favorRate=1 → 미표시
    curated.ShouldContain(x => x.Label == "무게" && x.Value == "24.0 kg");
    curated.ShouldContain(x => x.Label == "가격" && x.Value == "100000");
}

[Fact]
public void GetCuratedFields_Horse_WithAdd_ShowsCombined()
{
    var item = new FakeHorseItem
    {
        horseData = new FakeHorseData { speed = 100f, speedAdd = 10f, power = 50f, powerAdd = 5f }
    };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "속도" && x.Value == "100 (+10)");
    curated.ShouldContain(x => x.Label == "힘" && x.Value == "50 (+5)");
}

[Fact]
public void GetCuratedFields_Horse_NotEquipped()
{
    var item = new FakeHorseItem { horseData = new FakeHorseData { equiped = false } };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "착용중" && x.Value == "아니오");
}

[Fact]
public void GetCuratedFields_Horse_FavorRateNonDefault_ShowsRow()
{
    var item = new FakeHorseItem { horseData = new FakeHorseData { favorRate = 1.5f } };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "호감 율" && x.Value == "1.50");
}

[Fact]
public void GetCuratedFields_Horse_FavorRateDefault_OmitsRow()
{
    var item = new FakeHorseItem { horseData = new FakeHorseData { favorRate = 1f } };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldNotContain(x => x.Label == "호감 율");
}

[Fact]
public void GetCuratedFields_Horse_MaxWeightAdd_ShowsRow()
{
    var item = new FakeHorseItem { horseData = new FakeHorseData { maxWeightAdd = 20f } };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldContain(x => x.Label == "최대무게 추가" && x.Value == "+20");
}

[Fact]
public void GetCuratedFields_Horse_MaxWeightAddZero_OmitsRow()
{
    var item = new FakeHorseItem { horseData = new FakeHorseData { maxWeightAdd = 0f } };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.ShouldNotContain(x => x.Label == "최대무게 추가");
}

[Fact]
public void GetCuratedFields_Horse_NullWrapper_ReturnsTwoFields()
{
    var item = new FakeHorseItem { horseData = null };
    var curated = ItemDetailReflector.GetCuratedFields(item);
    curated.Count.ShouldBe(2);
    curated.ShouldContain(x => x.Label == "무게");
    curated.ShouldContain(x => x.Label == "가격");
}
```

- [ ] **Step 2: Run tests to verify Horse tests fail**

```bash
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj --filter "FullyQualifiedName~Curated_Horse" -v normal
```

Expected: 8 신규 Horse test FAIL — type=6 에 대해 switch `_ => new()` 반환.

- [ ] **Step 3: `AddBaseAdd` helper + `GetHorseDetails` + switch case 6**

`src/LongYinRoster/Core/ItemDetailReflector.cs` 의 switch 에 case 6 추가:

```csharp
return type switch
{
    0 => GetEquipmentDetails(item),
    2 => GetMedFoodDetails(item),
    3 => GetBookDetails(item),
    4 => GetTreasureDetails(item),
    5 => GetMaterialDetails(item),
    6 => GetHorseDetails(item),   // ← 신규
    _ => new(),
};
```

`GetMaterialDetails` 직후에 두 신규 method 추가:

```csharp
private static List<(string, string)> GetHorseDetails(object item)
{
    var result = new List<(string, string)>();
    var hd = ReadFieldOrProperty(item.GetType(), item, "horseData");
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

// 말 4 stat 의 base + Add 합산 표시 — Add=0 시 bare value, Add>0 시 "{base} (+{add})"
private static void AddBaseAdd(List<(string, string)> result, object obj, string label, string baseField, string addField)
{
    float baseVal = ReadFloat(obj, baseField);
    float addVal = ReadFloat(obj, addField);
    string val = addVal > 0f ? $"{baseVal:F0} (+{addVal:F0})" : $"{baseVal:F0}";
    result.Add((label, val));
}
```

⚠ `Math.Abs` 호출 위해 `using System;` 이 file 상단에 이미 있음 (1 라인) — 추가 import 불필요.

- [ ] **Step 4: Run tests to verify Horse tests pass + 전체 회귀**

```bash
dotnet test src/LongYinRoster.Tests/LongYinRoster.Tests.csproj -v normal
```

Expected:
- 8 신규 Horse test PASS
- Treasure 4 + Material 1 + Equipment 1 + Book 1 + MedFood 1 + NullItem 1 = 16 Curated test 모두 PASS
- 다른 모든 unit test (`ContainerPanelFormatTests`, `ItemCellRendererHelperTests`, `ContainerPanelFocusTests`, `ItemDetailReflectorRawFieldsTests` 등) PASS — 회귀 없음
- 총 test 갯수: v0.7.4 의 182 + Treasure 4 + Material 1 + Horse 8 - 기존 unsupported 2 (Treasure_ReturnsEmpty + Material_ReturnsEmpty) = **193**

- [ ] **Step 5: Commit**

```bash
git add src/LongYinRoster/Core/ItemDetailReflector.cs src/LongYinRoster.Tests/ItemDetailReflectorCuratedTests.cs
git commit -m "$(cat <<'EOF'
feat(core): v0.7.4.1 — Horse (type=6) curated 추가 + AddBaseAdd helper

ItemDetailReflector.GetCuratedFields switch 에 case 6 추가. GetHorseDetails 가 12 필드 표시 — 착용중 / 4 stat (base+Add 합산) / maxWeightAdd (>0 일 때만) / favorRate (≠1.0 일 때만) / 무게 / 가격. 신규 AddBaseAdd helper 가 "100 (+10)" 또는 "100" 형식으로 합산.

동적 필드 (nowPower / sprintTimeLeft / sprintTimeCd) 는 raw 섹션 위임.

8 신규 Horse case (BasicEquipped / WithAdd / NotEquipped / FavorRateNonDefault / FavorRateDefault / MaxWeightAdd / MaxWeightAddZero / NullWrapper).

총 test 182 → 193.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: 인게임 smoke (12/12)

**Files:**
- Build artifact: dist zip 또는 BepInEx plugins 폴더 직접 deploy (기존 빌드 절차 답습)
- Create: `docs/superpowers/dumps/2026-05-05-v0.7.4.1-smoke-results.md`

- [ ] **Step 1: 빌드 + 게임 deploy**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
```

Expected: 0 warnings, 0 errors. dll path 로그 확인.

게임의 `BepInEx/plugins/LongYinRoster/` 디렉토리에 빌드 결과 dll 복사 (기존 v0.7.4 deploy 절차와 동일 — `tools/deploy.ps1` 또는 직접 copy).

- [ ] **Step 2: 게임 실행 + 6 신규 시나리오 smoke**

게임 실행 후 F11 → 컨테이너 관리 → 인벤토리 또는 창고에서 다음 6 시나리오 실행:

| # | 시나리오 | 기대 결과 |
|---|---|---|
| S1 | 인벤에 말 1개 보유 → cell 클릭 (focus) → toolbar `ⓘ 상세` | curated: 착용중 (예/아니오) / 속도/힘/스프린트/인내 (Add 있으면 합산 형식) / maxWeightAdd 또는 favorRate (조건부) / 무게 / 가격. raw 섹션에 nowPower / sprintTimeLeft / sprintTimeCd 동적 필드 |
| S2 | 보물 1개 (fullIdentified=true) → ⓘ | "완전 감정: 예" / "감정 필요 지식: N" / 무게 / 가격. raw 섹션에 List 들 (treasureLv [count=N] 등) |
| S3 | 보물 1개 (fullIdentified=false) → ⓘ | "완전 감정: 아니오" + (ikn=0 이면 행 미표시) |
| S4 | 재료 1개 → ⓘ | curated 2행 (무게 / 가격) + raw 섹션에 extraAddData 객체 type 표시 |
| S5 | 회귀: v0.7.4 장비/비급/단약 1개씩 ⓘ | curated 표시 변동 없음 (장비 강화/착용/특수강화/무게경감 / 비급 무공ID / 단약 강화/추가보정 — 모두 v0.7.4 결과 그대로) |
| S6 | 컨테이너 area item ⓘ | v0.7.4 와 동일 — 미지원 (focus outline 만, JSON path 데이터). ItemDetailPanel 은 빈 curated + raw 섹션 |

각 시나리오마다 결과 (PASS / FAIL + 스크린샷 또는 로그 발췌) 기록.

- [ ] **Step 3: Smoke dump 작성**

`docs/superpowers/dumps/2026-05-05-v0.7.4.1-smoke-results.md` 신규 파일:

```markdown
# v0.7.4.1 인게임 smoke 결과

**일시**: 2026-05-05
**baseline**: v0.7.4 release (commit 16b41f0)
**변경**: ItemDetailReflector switch case 4/5/6 추가
**환경**: 게임 v1.0.0f8.2 + BepInEx 6 IL2CPP + 통팩 한글모드 + ModFix v3.2.0 + LongYinCheat v1.4.7

## 결과: N/12 PASS

### 신규 6 시나리오

| # | 시나리오 | 결과 | 비고 |
|---|---|---|---|
| S1 | 말 ⓘ | PASS / FAIL | (실제 표시 행 발췌) |
| S2 | 보물 (fullIdentified=true) ⓘ | PASS / FAIL | |
| S3 | 보물 (fullIdentified=false) ⓘ | PASS / FAIL | |
| S4 | 재료 ⓘ | PASS / FAIL | |
| S5 | 회귀 (장비/비급/단약) | PASS / FAIL | 3 카테고리 모두 변동 없음 |
| S6 | 컨테이너 area | PASS / FAIL | v0.7.4 와 동일 미지원 |

### 회귀 6 시나리오 (v0.7.4 smoke 의 6/6)

(v0.7.4 smoke 결과와 동일 형식으로 6 항목 기록)

## 발견된 이슈

(없으면 "없음")

## 다음 단계

- v0.7.4.1 release tag + GitHub release (Task 5)
- HANDOFF.md / 메타 spec 갱신
```

- [ ] **Step 4: Commit smoke dump**

```bash
git add docs/superpowers/dumps/2026-05-05-v0.7.4.1-smoke-results.md
git commit -m "$(cat <<'EOF'
docs(smoke): v0.7.4.1 인게임 smoke 12/12 결과 — 말 / 보물 / 재료 curated + 회귀

신규 6 (말 / 보물 fullIdentified true·false / 재료 / 회귀 / 컨테이너 area) + v0.7.4 smoke 6 회귀 = 12/12.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

⚠ smoke 가 FAIL 한 항목이 있으면 release 진행 금지 — 이전 task 로 돌아가서 fix.

---

## Task 5: Release v0.7.4.1

**Files:**
- Modify: `VERSION` (root, 또는 csproj 안 — Step 1 에서 위치 검색)
- Modify: `CHANGELOG.md` (root)
- Modify: `docs/HANDOFF.md`
- Modify: `docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`

- [ ] **Step 1: VERSION bump 위치 확인 + bump**

```bash
cd "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/Save/_PlayerExport"
ls VERSION 2>/dev/null && echo "root VERSION 존재" || echo "root VERSION 없음"
grep -r "0.7.4" --include="*.csproj" --include="VERSION" --include="*.props" 2>&1 | head -10
```

VERSION 파일이 있으면 `0.7.4` → `0.7.4.1` 로 변경. csproj 의 `<Version>` 또는 `<VersionPrefix>` 가 있으면 그것도 변경.

- [ ] **Step 2: CHANGELOG entry 추가**

`CHANGELOG.md` 의 가장 위 (또는 [Unreleased] 섹션 다음) 에 추가:

```markdown
## [v0.7.4.1] - 2026-05-05

### Added
- ItemDetailPanel curated 섹션 — 보물 / 재료 / 말 카테고리 추가 (기존 장비 / 비급 / 단약 + 신규 3 = 7 카테고리 cover).
- 말 stat 표시 — 속도 / 힘 / 스프린트 / 인내 base+Add 합산 형식 ("100 (+10)"), 착용중, 최대무게 추가 (>0 일 때), 호감 율 (≠1.0 일 때).
- 보물 표시 — 완전 감정 (예/아니오), 감정 필요 지식 (>0 일 때).
- 재료 표시 — 무게 / 가격 (extraAddData nested 객체는 raw 섹션 위임).

### Tests
- ~14 신규 unit test (Horse 8 + Treasure 4 + Material 1 + 일부 negative — 기존 unsupported 2 삭제). 총 182 → 193.

### Out-of-scope
- Nested 객체 (HeroSpeAddData / ChangeHeroStateData / EquipPoisonData) 의 1-depth 표시 → v0.7.7 Item editor 와 함께 처리.
- 음식 vs 단약 분리 / 장비 attriType 한글 매핑 → v0.7.5 한글화 sub-project.
- 컨테이너 area (외부 디스크) curated → JSON path 데이터 미지원 (v0.7.4 와 동일).
```

- [ ] **Step 3: HANDOFF.md 갱신**

`docs/HANDOFF.md`:
- 라인 4 의 "**진행 상태**: **v0.7.4 release**" → "**v0.7.4.1 release**"
- 메인 baseline 을 v0.7.4.1 로 갱신 + 1 줄 요약 추가 ("말 / 보물 / 재료 curated 추가, 7 카테고리 cover, 193 tests + smoke 12/12")
- "다음 sub-project" 의 v0.7.4.x 항목에 ✅ 표시 + cursor → "v0.7.5 D-4 Item 한글화"

- [ ] **Step 4: 메타 로드맵 spec 갱신**

`docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`:
- §2.1 v0.7.4.x patch 의 "확정 sub-project" → "✅ 완료 (v0.7.4.1, 2026-05-05)"
- 본 plan + spec + smoke 의 link 추가

§6 다음 액션:
- 1. 현재 cursor → v0.7.5 D-4 한글화 sub-project brainstorm 진입

- [ ] **Step 5: Commit + tag**

```bash
git add VERSION CHANGELOG.md docs/HANDOFF.md docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md
# (csproj 도 변경됐으면 추가)
git commit -m "$(cat <<'EOF'
chore(release): v0.7.4.1 — Item 상세 panel 나머지 3 카테고리 curated (말 / 보물 / 재료)

ItemDetailReflector.GetCuratedFields 가 7 카테고리 모두 cover. Test 182 → 193. Smoke 12/12 PASS.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
git tag v0.7.4.1
git push origin main
git push origin v0.7.4.1
```

- [ ] **Step 6: GitHub release + dist zip**

기존 v0.7.4 release 와 동일 절차 (HANDOFF.md 의 release 섹션 참조). dist zip = `dist/LongYinRoster-v0.7.4.1.zip` (BepInEx/plugins/LongYinRoster/ 구조).

```bash
gh release create v0.7.4.1 dist/LongYinRoster-v0.7.4.1.zip \
  --title "v0.7.4.1 — Item 상세 panel 7 카테고리 cover" \
  --notes "ItemDetailPanel curated 섹션이 말 / 보물 / 재료 추가로 7 카테고리 모두 cover. 기존 장비 / 비급 / 단약 회귀 없음. Test 182 → 193, smoke 12/12.

상세: docs/superpowers/specs/2026-05-05-longyin-roster-mod-v0.7.4.1-design.md"
```

---

## Self-Review

### Spec coverage check

| Spec section | 구현 task |
|---|---|
| §1 결정 사항 (i, A, code 구조, test 패턴, smoke) | Task 1~5 |
| §2.1 switch 확장 + 3 GetXxxDetails + AddBaseAdd | Task 1 (Treasure) / Task 2 (Material) / Task 3 (Horse + AddBaseAdd) |
| §2.2 LOC 추정 (impl ~80 / test ~150) | Task 1~3 (실제 LOC 는 변동 가능) |
| §3.1 신규 fake POCO 3개 | Task 1 (FakeTreasureData/Item) / Task 2 (FakeMaterialItem) / Task 3 (FakeHorseData/Item) |
| §3.2 신규 test 케이스 ~14 | Task 1 (4) / Task 2 (1) / Task 3 (8) = 13 신규 + 기존 negative 유지 |
| §3.3 회귀 — 기존 test PASS 유지 | Task 3 Step 4 의 전체 test run 으로 검증 |
| §4 smoke 12/12 (신규 6 + 회귀 6) | Task 4 |
| §5 cycle (1~6) | Task 1~3 (impl) + Task 4 (smoke) + Task 5 (release) |
| §6 out-of-scope | impl 안 함 (그대로 두는 게 검증) |
| §7 risk | Task 4 smoke 가 spike → release 검증 |

### Placeholder scan
- ✅ "TBD" / "TODO" 없음
- ✅ "implement later" / "fill in details" 없음
- ✅ 모든 step 에 actual code / actual command
- ✅ 모든 method signature spec 과 일치 (GetTreasureDetails / GetMaterialDetails / GetHorseDetails / AddBaseAdd)

### Type consistency
- ✅ `ReadFieldOrProperty / ReadBool / ReadFloat / ReadInt` — v0.7.4 기존 helper, 시그니처 변경 없음
- ✅ `AddBaseAdd(List<(string,string)> result, object obj, string label, string baseField, string addField)` — Task 3 에서 정의, 사용 일관
- ✅ Switch case 4/5/6 → GetTreasureDetails/Material/Horse — 일관
- ✅ Fake POCO field 명 = spike dump §1.1~§1.4 에 명시된 게임 필드명과 일치 (treasureData / fullIdentified / identifyKnowledgeNeed / horseData / equiped / speed / speedAdd / etc.)

---

## 빠른 참조 — Test 갯수 추적

| 시점 | Curated test | 전체 test |
|---|---:|---:|
| v0.7.4 baseline | 6 (Equipment + Book + MedFood + Treasure_Empty + Material_Empty + NullItem) | 182 |
| Task 1 후 (Treasure) | 6 - 1 + 4 = 9 | 185 |
| Task 2 후 (Material) | 9 - 1 + 1 = 9 | 185 |
| Task 3 후 (Horse) | 9 + 8 = 17 | 193 |

⚠ 전체 test 갯수는 추정 — Task 3 Step 4 의 실제 결과로 확정.
