# LongYin Roster Mod v0.4 — Design Spec

**일시**: 2026-04-30
**Scope**: 정체성 + 인벤토리 + 창고 활성화 + 체크박스 UI 인프라
**선행 spec**: `2026-04-29-longyin-roster-mod-v0.3-design.md` (PinpointPatcher / Apply / Restore 기반)
**HANDOFF**: `docs/HANDOFF.md` §6.A — v0.4 후보

---

## 1. Context

### 1.1 v0.3 까지의 도달점

- **PinpointPatcher 7-step pipeline** 으로 stat-backup focus Apply / Restore 활성 (smoke C1/C2 PASS)
- 18 SimpleFieldMatrix entry — 명예 / 악명 / HP / Mana / Power / 부상 / 충성 / 호감 / 자기집 add / 천부 포인트 / 스킨 / baseAttri[] / baseFightSkill[] / baseLivingSkill[] / expLivingSkill[]
- heroTagData rebuild + RefreshSelfState/RefreshExternalManagers
- 25/25 unit tests + smoke (save→reload G1/G2/G3 통과)
- v0.3 의 stat-backup 는 **all-or-nothing** — 사용자가 어떤 카테고리만 적용할지 선택 못 함

### 1.2 v0.4 의 동기

사용자 요청 두 가지가 한 release 에 묶임:

1. **Deferred 매트릭스 ⚪ 항목 활성화** (spec §12 의 v0.4 후보)
   - **정체성** (heroName / heroNickName / heroFamilyName / settingName / isFemale / age / nature / talent / generation)
   - **인벤토리** (itemListData.allItem)
   - **창고** (selfStorage.allItem)
   - 무공 list / 외형 은 **v0.5+ 로 deferred** (R&D 비용 + IL2CPP 한계 더 큼)

2. **체크박스 UI** (spec §12 의 v0.5+ 항목 — v0.4 로 앞당김)
   - 사용자가 어느 카테고리를 덮어쓸지 토글
   - v0.3 의 all-or-nothing 폐기

이 둘은 결합도 높음 — 정체성/인벤토리/창고가 활성화되면 사용자는 "전체 캐릭터 복원"을 더 이상 원하지 않을 수 있음 (예: stat 만 백업, 인벤토리는 현재 게임 그대로). 체크박스 인프라가 deferred 매트릭스 활성화의 prerequisite.

### 1.3 v0.4 의 새 접근 — Selection-Aware Pipeline

**핵심 변화**:
- `ApplySelection` POCO 추가 (9 boolean flag)
- `PinpointPatcher.Apply(json, player, selection)` 시그니처 확장
- `SimpleFieldMatrix` entry 에 `Category` 추가 — selection filter 가능
- 신규 step 3 개 추가: SetIdentityFields / SetActiveKungfu / RebuildItemList(활성) / RebuildSelfStorage(활성)
- Plugin 시작 시 1 회 capability 검사 — PoC 풀린 카테고리만 UI checkbox 활성

**의도적 회귀** (v0.3 → v0.4):
- 부상 (외상/내상/중독) / 충성 / 호감 — v0.3 자동 backup 기능 폐기, 영구 보존 카테고리로 이관
- 사유: in-game runtime state 라 backup 시점 의미 모호. force/relations 와 같은 보존 정책

---

## 2. Goals & Non-goals

### 2.1 v0.4 Goals

1. **정체성 활성화** — heroName / heroNickName / age 등 9 필드 game-self 우회로 set (PoC 후 결정)
2. **인벤토리 / 창고 활성화** — itemListData / selfStorage 의 entry-level rebuild (PoC 후 결정)
3. **무공 active 활성화** — `nowActiveSkill` 을 player 보유 wrapper 로 SetNowActiveSkill 호출
4. **체크박스 UI 인프라** — SlotDetailPanel 인라인 9-카테고리 (자기집 add 별도) checkbox grid
5. **ApplySelection 슬롯별 저장** — `_meta.applySelection` 추가, toggle 시 즉시 disk write
6. **Capability 자동 감지** — Plugin 시작 시 1 회. PoC 실패 카테고리는 disabled checkbox + "(v0.5+ 후보)" label
7. **v0.2 / v0.3 슬롯 호환** — 기존 슬롯 자동 V03Default 적용. file 안 건드림
8. **Restore (slot 0) 항상 모두 적용** — 체크박스 무시. ApplySelection.RestoreAll() 사용

### 2.2 v0.4 Non-goals

| # | 항목 | 미루는 사유 |
|---|---|---|
| N1 | 무공 list (kungfuSkills) | KungfuSkillLvData wrapper ctor R&D — v0.5 |
| N2 | 외형 (faceData / partPosture / portraitID) | sprite invalidation + HeroFaceData wrapper R&D — v0.5+ |
| N3 | 부상/충성/호감 backup 옵션화 | v0.6+ 검토 — 사용자가 토글로 활성 가능하게 |
| N4 | Apply preview (dry-run) | v0.6+ |
| N5 | Selection 프리셋 ("전체"/"v0.3 호환"/"정체성만") | v0.5+ |
| N6 | Reverse export (game → 다른 player save) | 별도 모드 |
| N7 | 자동 smoke harness | v0.5 검토 |
| N8 | force / location / relations | §2.2 N4 (v0.3) — 영구 보존 |
| N9 | ConfigEntry 신규 추가 | capability + 슬롯 selection 으로 모든 동작 표현 가능 — 추가 불필요 |

---

## 3. Architecture

### 3.1 Layered Components (변경/추가)

```
LongYinRoster/
├── Core/
│   ├── ApplySelection.cs                ← 신규 (9 bool POCO + Default/RestoreAll/JSON)
│   ├── PinpointPatcher.cs               ← 변경 (selection 인자 + 신규 step 3개 + capability)
│   ├── SimpleFieldMatrix.cs             ← 변경 (Category enum 추가, 부상/충성/호감 → None)
│   ├── ApplyResult.cs                   ← 변경 (Capability 누적 — startup probe 결과)
│   ├── HeroLocator.cs                   ← 유지
│   ├── IL2CppListOps.cs                 ← 유지 (ItemList rebuild 에서 재사용)
│   ├── PortabilityFilter.cs             ← 유지 (v0.4 신규 카테고리는 strip 안 됨, 변경 없음)
│   └── SerializerService.cs             ← 유지
├── Slots/
│   ├── SlotMetadata.cs                  ← 변경 (ApplySelection field 추가)
│   ├── SlotFile.cs                      ← 변경 (toggle 시 즉시 저장 path)
│   └── ... (나머지 유지)
├── UI/
│   ├── SlotDetailPanel.cs               ← 변경 (체크박스 grid + capability)
│   ├── ModWindow.cs                     ← 변경 (capability cache + DoApply 시 selection 전달)
│   └── ...
└── Util/
    └── KoreanStrings.cs                 ← 변경 (9 카테고리 label + "v0.5+ 후보")
```

### 3.2 Capability Probe (Plugin 시작 시 1 회)

`PinpointPatcher.Probe()` — Plugin.OnEnable 마지막에 호출.
- 각 신규 카테고리 (Identity / ActiveKungfu / ItemList / SelfStorage) 의 우회 path 가 살아있는지 reflection 으로 검사
- 결과: `Capabilities` (POCO) — 9 boolean
- ModWindow 에 cache → SlotDetailPanel 이 disabled checkbox 결정에 사용
- v0.3 검증된 4 카테고리 (Stat / Honor / TalentTag / Skin) 는 항상 true
- 자기집 add 도 v0.3 매트릭스에 있어 항상 true

### 3.3 Pipeline 책임 분리

| Step | 카테고리 selection | Capability gate | Always |
|---|---|---|---|
| 1 SetSimpleFields | Stat / Honor / Skin / SelfHouse (entry.Category) | 항상 true | — |
| 2 SetIdentityFields | Identity | Probe 결과 | — |
| 3 SetActiveKungfu | ActiveKungfu | Probe 결과 | — |
| 4 RebuildItemList | ItemList | Probe 결과 | — |
| 5 RebuildSelfStorage | SelfStorage | Probe 결과 | — |
| 6 RebuildKungfuSkills | (skip) | always false | — |
| 7 RebuildHeroTagData | TalentTag | 항상 true | — |
| 8 RefreshSelfState | (any change) | — | ✓ fatal |
| 9 RefreshExternalManagers | (any change) | — | ✓ best-effort |

---

## 4. ApplySelection Model

### 4.1 POCO 정의

```csharp
public sealed class ApplySelection
{
    public bool Stat        { get; set; } = true;
    public bool Honor       { get; set; } = true;
    public bool TalentTag   { get; set; } = true;
    public bool Skin        { get; set; } = true;
    public bool SelfHouse   { get; set; } = false;  // 별도 카테고리, opt-in
    public bool Identity    { get; set; } = false;  // 신규
    public bool ActiveKungfu{ get; set; } = false;  // 신규
    public bool ItemList    { get; set; } = false;  // 신규
    public bool SelfStorage { get; set; } = false;  // 신규

    public static ApplySelection V03Default() => new();   // 위 default 그대로
    public static ApplySelection RestoreAll() => new()
    {
        Stat=true, Honor=true, TalentTag=true, Skin=true, SelfHouse=true,
        Identity=true, ActiveKungfu=true, ItemList=true, SelfStorage=true
    };

    public bool AnyEnabled() => Stat || Honor || TalentTag || Skin || SelfHouse
                                 || Identity || ActiveKungfu || ItemList || SelfStorage;
}
```

### 4.2 슬롯 JSON Schema (`_meta.applySelection`)

```json
"_meta": {
  ...,
  "applySelection": {
    "stat": true,
    "honor": true,
    "talentTag": true,
    "skin": true,
    "selfHouse": false,
    "identity": false,
    "activeKungfu": false,
    "itemList": false,
    "selfStorage": false
  }
}
```

- v0.4 신규 캡처 시 `V03Default()` 로 함께 저장
- v0.2 / v0.3 슬롯 (없음) → 로드 시 missing 감지 → `V03Default()` 적용. file 안 건드림
- 사용자가 toggle 하면 그 시점에 SlotFile.Write — file 첫 갱신

### 4.3 Toggle 시 저장 정책

- SlotDetailPanel 의 `Toggle()` 콜백 → SlotEntry.Meta.ApplySelection 즉시 변경 → SlotRepository.SaveMeta(slotIndex) 호출
- SlotFile 의 atomic .tmp → Replace 패턴 그대로 (v0.2 검증)
- file write 빈도: 사용자가 빠르게 9개 toggle 하면 9 file write — 작은 JSON 이라 OK
- Apply 누를 때 별도 저장 안 함 (이미 toggle 시점에 저장됨)

---

## 5. PinpointPatcher 확장

### 5.1 시그니처 변경

```csharp
public static ApplyResult Apply(string slotPlayerJson, object currentPlayer, ApplySelection selection)
```

기존 호출자 (`ModWindow.DoApply` / `AttemptAutoRestore`) 모두 update.

### 5.2 9-Step Pipeline

```csharp
TryStep("SetSimpleFields",         () => SetSimpleFields(slot, player, selection, res), res);
TryStep("SetIdentityFields",       () => SetIdentityFields(slot, player, selection, res), res);
TryStep("SetActiveKungfu",         () => SetActiveKungfu(slot, player, selection, res), res);
TryStep("RebuildItemList",         () => RebuildItemList(slot, player, selection, res), res);
TryStep("RebuildSelfStorage",      () => RebuildSelfStorage(slot, player, selection, res), res);
TryStep("RebuildKungfuSkills",     () => SkipKungfuSkills(res), res);  // v0.5+
TryStep("RebuildHeroTagData",      () => RebuildHeroTagData(slot, player, selection, res), res);
TryStep("RefreshSelfState",        () => RefreshSelfState(player, res), res, fatal: true);
TryStep("RefreshExternalManagers", () => RefreshExternalManagers(player, res), res);
```

### 5.3 SetSimpleFields 의 selection filter

```csharp
foreach (var entry in SimpleFieldMatrix.Entries)
{
    bool enabled = entry.Category switch
    {
        FieldCategory.Stat       => selection.Stat,
        FieldCategory.Honor      => selection.Honor,
        FieldCategory.Skin       => selection.Skin,
        FieldCategory.SelfHouse  => selection.SelfHouse,
        FieldCategory.TalentPoint=> selection.TalentTag,  // heroTagPoint 는 TalentTag selection 따라감
        FieldCategory.None       => false,                 // 부상/충성/호감 — 영구 보존
        _ => false
    };
    if (!enabled) { res.SkippedFields.Add($"{entry.Name} (selection off)"); continue; }
    // ... 기존 처리
}
```

### 5.4 신규 Step — SetIdentityFields

PoC 결과에 따라 3 우회 path 중 하나 사용:
- 시도 A: `set_heroName(value)` reflection 직접 호출 → 변화 검증
- 시도 B: backing field (`<heroName>k__BackingField` 또는 `_heroName`) 직접 set
- 시도 C: Harmony postfix on setter (별도 helper class — patch target 명시)

`IdentityFieldMatrix` (신규 file) 에 9 entry 매핑 — name / JsonPath / PropertyName / BackingFieldName / Type. PoC 가 어느 path 를 쓰는지 결정 후 코드 구체화.

### 5.5 신규 Step — SetActiveKungfu

```csharp
if (!selection.ActiveKungfu) { res.SkippedFields.Add("activeKungfu (selection off)"); return; }
if (!slot.TryGetProperty("nowActiveSkill", out var idEl)) { res.SkippedFields.Add("activeKungfu — not in slot"); return; }
int targetSkillID = idEl.GetInt32();

// player.kungfuSkills 안에서 같은 skillID 가진 wrapper 찾기
var kungfuList = ReadFieldOrProperty(player, "kungfuSkills");
int n = IL2CppListOps.Count(kungfuList);
object? wrapper = null;
for (int i = 0; i < n; i++)
{
    var entry = IL2CppListOps.Get(kungfuList, i);
    int id = (int)ReadFieldOrProperty(entry, "skillID");  // KungfuSkillLvData.skillID
    if (id == targetSkillID) { wrapper = entry; break; }
}
if (wrapper == null)
{
    res.WarnedFields.Add($"activeKungfu — player 가 skill {targetSkillID} 미보유 (kungfuSkills v0.5+ 후보)");
    return;
}
InvokeMethod(player, "SetNowActiveSkill", new[] { wrapper });
res.AppliedFields.Add($"activeKungfu (skillID={targetSkillID})");
```

### 5.6 신규 Step — RebuildItemList / RebuildSelfStorage

PoC 결과에 따라 3 path 중 하나:
- 시도 A: `ItemData(IntPtr)` IL2CPP wrapper ctor 직접 (Il2CppInterop) — 다른 wrapper 클래스에서 검증된 패턴
- 시도 B: 추가 dump round 의 static factory / nested class 발견
- 시도 C: Harmony patch on game 의 ItemData 생성 path (예: 상점/제작 method)

Clear: `LoseAllItem()` (game-self, 인벤토리 한정 — 창고는 raw `IL2CppListOps.Clear` 가능)

```csharp
// 의사코드
if (!selection.ItemList) { skip; return; }
if (!Capabilities.ItemList) { res.SkippedFields.Add("itemList — PoC 실패"); return; }
InvokeMethod(player, "LoseAllItem", Array.Empty<object>());
foreach (var entry in slot.itemListData.allItem)
{
    var itemData = CreateItemData(entry.itemID, entry.count, ...);  // PoC path
    InvokeMethod(player, "GetItem", new object?[] { itemData, false, false, 0, false });
}
```

### 5.7 SimpleFieldMatrix 변경 — Category enum

```csharp
public enum FieldCategory
{
    None,         // 부상/충성/호감 — 영구 보존, SetSimpleFields 가 자동 skip
    Stat,         // HP/Mana/Power + base stat lists + selfHouse 안 — selfHouse 는 별도
    Honor,        // fame/badFame
    Skin,         // skinID/skinLv
    SelfHouse,    // selfHouseTotalAdd
    TalentPoint,  // heroTagPoint — TalentTag selection 따라감
}

public sealed record SimpleFieldEntry(
    string Name,
    string JsonPath,
    string PropertyName,
    Type Type,
    string? SetterMethod,
    SetterStyle SetterStyle,
    FieldCategory Category);   // ← 추가
```

기존 18 entry 의 Category 매핑 (v0.4 변경):
- 명예 / 악명 → Honor
- HP / Mana / Power → Stat
- **외상 / 내상 / 중독 → None** (v0.3 자동 적용 폐기)
- **충성 / 호감 → None** (v0.3 자동 적용 폐기)
- 자기집 add → SelfHouse
- 천부 포인트 → TalentPoint
- **활성 무공 → entry 제거** (별도 step 으로 이관)
- 스킨 → Skin
- baseAttri[i] / baseFightSkill[i] / baseLivingSkill[i] / expLivingSkill[i] → Stat

→ 17 entry (활성 무공 제거 + 부상/충성/호감 5개 Category=None: 외상/내상/중독/충성/호감)

---

## 6. R&D PoC 전략

### 6.1 정체성 (성공 가능성: 상)

**dump 결과 (v0.3 spec §7.2)**: `set_heroName(String)` 등 property setter 만, game-self method 없음

**PoC 절차**:
1. **시도 A — setter 직접 호출**: reflection `t.GetProperty("heroName").SetValue(player, "신이름")` → 정보창 즉시 변화 + save/reload 검증
2. **시도 A 실패 (silent no-op — Newtonsoft Populate 함정 재현)** → **시도 B — backing field**: `t.GetField("<heroName>k__BackingField") ?? t.GetField("_heroName")` 직접 SetValue
3. **시도 B 실패** → **시도 C — Harmony postfix on `set_heroName`**: caller 가 setter 호출하면 강제 덮어쓰기 (우리는 setter 호출 + Harmony 가 결과 보강)

**판정**: 9 필드 중 일관 path 발견 시 Capabilities.Identity = true. 일부만 풀리면 partial — IdentityFieldMatrix 에 per-field path 명시.

**Fallback**: 모두 실패 → Capabilities.Identity = false, UI disabled.

**PoC 결과 (2026-04-30, commit `69bcb7c` 빌드, in-game F12 with `상정제` heroName)**:
- **시도 A (setter 직접)**: **PASS** — `set='상정제_A' got='상정제_A'`. heroName setter 가 silent no-op 아님 (Newtonsoft Populate 함정과 다름)
- **시도 B (backing field)**: 두 후보 (`<heroName>k__BackingField`, `_heroName`) 모두 미발견. fallback enumerate (`name` 포함 필드) 도 빈 결과 — IL2CPP Il2CppInterop wrapper 가 .NET 측에 backing field 를 노출하지 않는 일반적 패턴
- **결정**: `Capabilities.Identity = true`. `IdentityFieldMatrix` 9 필드 모두 `Path = Setter` 가정
- **잔여 risk**: in-memory PASS 만 검증됨. save→reload 후 setter 변경값이 살아남는지는 Task D15 smoke 항목 E 에서 추가 검증. 만약 reload 후 원본으로 회귀하면 → path = Harmony postfix on `set_heroName` 으로 회귀 (별도 helper class 추가)

### 6.2 무공 active (성공 가능성: 중-상)

**dump 결과**: `SetNowActiveSkill(KungfuSkillLvData)` — wrapper 인자

**PoC**: 슬롯의 `nowActiveSkill` (int) 으로 `player.kungfuSkills[]` 안에서 같은 skill ID 의 wrapper 찾아 호출. player 미보유 skill 이면 warn + skip.

**판정**: 임의 player + 임의 skill ID 로 검증. SetNowActiveSkill 호출 후 game UI 의 active 무공 표시 변화 + save/reload.

**Fallback**: SetNowActiveSkill 자체 호출 실패 → false.

**PoC 결과 (2026-04-30, commit `001bc55` 빌드, in-game F12)**:
- player: 무공 1 개 보유 (skillID=9 lv=3, current nowActiveSkill=0)
- 호출: `SetNowActiveSkill(wrapper)` 결과: `nowActiveSkill=3 FAIL — value not changed`
- **핵심 발견**: SetNowActiveSkill 가 nowActiveSkill 를 wrapper.skillID (9) 가 아니라 wrapper.lv (3) 로 set. 우리 가정 (nowActiveSkill == 무공 ID) 과 game 의 실제 mapping (nowActiveSkill == 무공 lv 또는 비-ID semantic) 이 다름
- 두 번째 시도에서 current=3 으로 시작 (원복 logic 이 currentID=0 일 때 skip 한 사이드 이펙트) — game state 일부 변경됨 (재시작으로 회복 필요)
- Wrapper entry property 이름: `skillID` 가 작동 (fallback chain 첫 번째)
- **결정**: `Capabilities.ActiveKungfu = false`. 무공 active 카테고리는 v0.5+ 로 deferred. v0.5 에서 추가 dump round 또는 game source 분석으로 nowActiveSkill 의 정확한 semantic 결정 필요. `SetNowActiveSkill` 외에 다른 setter / activator method 후보 enumerate 도 검토
- UI 영향: SlotDetailPanel 의 "무공 active" checkbox 는 disabled + "(v0.5+ 후보)" suffix

### 6.3 인벤토리 / 창고 (성공 가능성: 하 — 30~50%)

**dump 결과**: `ItemData` wrapper ctor / static factory 미발견. `LoseAllItem()` 만 clear 가능. `GetItem(ItemData,...)` overload 들 — wrapper 인자

**PoC 절차**:
1. **시도 A — 추가 dump round**: ItemData / ItemListData 의 다른 namespace / nested class / static factory 후보 enumerate. `Il2CppInterop` wrapper ctor (`ItemData(IntPtr)`) 패턴 검증.
2. **시도 A 실패** → **시도 B — IntPtr ctor 직접**: `typeof(ItemData).GetConstructor(new[] { typeof(IntPtr) }).Invoke(new[] { someValidPtr })` — 다른 wrapper 클래스에서 검증된 IL2CPP 패턴.
3. **시도 B 실패** → **시도 C — Harmony patch hijack**: game 의 어느 method (상점 구매 / 제작 / 던전 보상) 에 ItemData 생성 path 가 있는지 확인 → 그 method 를 reflection 으로 호출하여 새 ItemData 얻고 hijack.

**Clear**:
- 인벤토리: `LoseAllItem()` (game-self)
- 창고: dump 에 동등 method 없음 → `IL2CppListOps.Clear(selfStorage.allItem)` raw fallback

**판정**: itemID=10001 + count=1 추가 → 인벤토리 표시 변화 + 사용 가능 + save/reload 후에도 살아있음.

**Fallback**: 모두 실패 → Capabilities.ItemList = false, Capabilities.SelfStorage = false. UI 두 체크박스 모두 disabled (둘 다 ItemData 한계 공유). 인벤토리/창고는 묶음 처리.

**PoC 결과 (2026-04-30, commit `caa36e1` 빌드, in-game F12 with player allItem count=2)**:
- **ItemData type 발견**: top-level namespace, `ItemData`
- **Constructors enumerate** (2 개):
  - `ItemData(ItemType _type)` — game-self ctor (ItemType enum 인자)
  - `ItemData(IntPtr pointer)` — Il2CppInterop wrapper ctor → IntPtr ctor exists 확인
- **Static factory**: **미발견** (`ItemData.Create(id, count)` 같은 후보 0 개)
- **기존 ItemData 구조** (player.itemListData.allItem[0] dump):
  - 약 20 properties: `itemID=0` (의외 — uniqueness 결여), `type=Equip`, `subType=0`, `name=보통手甲`, `checkName=""`, `describe=""`, `value=680`, `itemLv=1`, `rareLv=3`, `weight=8`, `isNew=True`, `poisonNum=0`, `poisonNumDetected=False`, `setName=""`
  - **sub-data wrappers**: `equipmentData=EquipmentData` (Equip 타입이라 채워짐), `medFoodData=`, `bookData=`, `treasureData=`, `materialData=`, `horseData=` (모두 빈 값 — 다른 type 일 때 채워질 것)
  - `ObjectClass`, `Pointer`, `WasCollected` — Il2CppInterop wrapper infrastructure
- **결정**: `Capabilities.ItemList = false`, `Capabilities.SelfStorage = false`. **v0.5+ 로 deferred**. 사유:
  1. **Static factory 미발견** — `ItemData.Create(id, count)` 같은 깔끔한 path 없음
  2. **`itemID = 0`** — slot JSON 의 itemID 만으로는 item 정체성 복원 안 됨 (uniqueness 결여)
  3. **Sub-data wrapper graph** — equipmentData / medFoodData / bookData / treasureData / materialData / horseData 가 별도 wrapper 클래스. `ItemData(ItemType)` ctor 만 호출하면 sub-data 비어있음 → item 의 실제 의미 결정 안 됨. 각 sub-data 별 wrapper ctor 추가 R&D 필요
  4. v0.4 scope 초과 — sub-data graph reconstruction 은 별도 phase
- **v0.5 R&D 시작점**: `ItemData(ItemType)` ctor + EquipmentData / MedFoodData / BookData wrapper enumeration. JSON property setter 함정 (itemID=0 등) 검증. 또는 Harmony hijack on game's item-creation path (상점 / 제작 / 보상 method)
- **UI 영향**: SlotDetailPanel 의 "인벤토리" / "창고" 체크박스 둘 다 disabled + "(v0.5+ 후보)" suffix

### 6.4 PoC 실패 시 일관 처리

- `Capabilities.X = false` 가 ground truth
- ApplySelection.X = true 라도 step 이 `res.SkippedFields.Add("X — PoC failed")` no-op
- UI 의 disabled checkbox 가 사용자에게 보이는 정보
- 토스트 매핑: 사용자가 disabled checkbox 클릭 시 ToastService 가 "PoC 실패 — v0.5+ 후보" 표시 (또는 클릭 자체가 GUI.enabled=false 로 차단되어 토스트 불필요)

---

## 7. UI 변경

### 7.1 SlotDetailPanel — 체크박스 Grid

```
┌─ 슬롯 03 · 김무사 (호남무사) ─────────────────┐
│  캡처     2026-04-29 14:20                  │
│  출처     라이브                              │
│  세이브   SaveSlot 03                        │
│  전투력   12,345                             │
│  무공     7 (Lv10 2)                         │
│  인벤토리 23 / 창고 87                        │
│  금전     45,200 냥                          │
│  천부     5 개                                │
│                                              │
│  ─── Apply 항목 ───                          │
│  ☑ 스탯       ☑ 명예      ☑ 천부            │
│  ☑ 스킨       ☐ 자기집 add ☐ 정체성         │
│  ☐ 무공 active ☐ 인벤토리  ☐ 창고            │
│                                              │
│  [▼ 현재 플레이어로 덮어쓰기]                 │
│  [Rename] [Comment] [Delete]                 │
└──────────────────────────────────────────────┘
```

**Layout**: 3 컬럼 x 3 행 grid (9 카테고리). IMGUI `GUILayout.BeginHorizontal()` x 3 + `Toggle` 9 개. GUIStyle 인자 없는 default skin 사용 (HANDOFF §4.3 IL2CPP IMGUI strip 회피).

**Disabled checkbox** (Capability false):
- `GUI.enabled = false` 로 그려서 회색 처리
- label 옆에 "(v0.5+ 후보)" 추가 — 정체성 / 무공active / 인벤토리 / 창고 4 개 후보

**panel 높이 변경**: 현재 약 480px → 540~560px (~60px 증가). ModWindow 의 height 도 동기 update. position 영속 영향 없음 (height 만 증가, position 키 동일).

### 7.2 ModWindow — Capability cache + DoApply 시 selection 전달

```csharp
public sealed class ModWindow : MonoBehaviour
{
    public Capabilities Capabilities { get; private set; }  // ← 신규

    void OnEnable()
    {
        // ... 기존
        Capabilities = PinpointPatcher.Probe();
        Logger.Info($"Capabilities: Identity={Capabilities.Identity} " +
                    $"ActiveKungfu={Capabilities.ActiveKungfu} " +
                    $"ItemList={Capabilities.ItemList} SelfStorage={Capabilities.SelfStorage}");
    }

    public void DoApply(int slotIndex)
    {
        var slot = _repo.Get(slotIndex);
        var selection = slot.Meta.ApplySelection;
        var res = PinpointPatcher.Apply(slot.PlayerJson, _player, selection);
        // ... 기존 토스트 / autobackup / autorestore 로직
    }

    public void DoRestore()
    {
        var slot = _repo.Get(0);
        var selection = ApplySelection.RestoreAll();   // 체크박스 무시
        var res = PinpointPatcher.Apply(slot.PlayerJson, _player, selection);
        // ...
    }
}
```

### 7.3 Slot 0 (자동백업) detail panel

체크박스 노출 안 함. `[↶ Apply 직전 상태로 복원]` 버튼만. `entry.Index == 0` 분기 그대로 유지.

### 7.4 KoreanStrings 신규

```csharp
public const string Cat_Stat        = "스탯";
public const string Cat_Honor       = "명예";
public const string Cat_TalentTag   = "천부";
public const string Cat_Skin        = "스킨";
public const string Cat_SelfHouse   = "자기집 add";
public const string Cat_Identity    = "정체성";
public const string Cat_ActiveKungfu= "무공 active";
public const string Cat_ItemList    = "인벤토리";
public const string Cat_SelfStorage = "창고";
public const string Cat_DisabledSuffix = "(v0.5+ 후보)";
public const string ApplySectionHeader = "─── Apply 항목 ───";
```

---

## 8. Migration & Compatibility

### 8.1 슬롯 JSON

- **v0.2 / v0.3 슬롯**: `_meta.applySelection` field 부재 → 로드 시 `V03Default()` 적용. file 안 건드림 (read-only migration)
- **v0.4 신규 슬롯**: 캡처 시 `V03Default()` 함께 저장
- 사용자가 toggle 하는 시점에만 file 갱신 — 첫 sync 보장

### 8.2 SlotMetadata 변경

```csharp
public sealed class SlotMetadata
{
    // ... 기존
    public ApplySelection ApplySelection { get; set; } = ApplySelection.V03Default();
}
```

JSON read 시 missing field → C# default initializer (V03Default) 발화. 별도 migration 코드 불필요.

### 8.3 ConfigEntry

신규 추가 없음. `AllowApplyToGame` (v0.3) 유지.

### 8.4 Release 절차

1. PoC 결과로 spec §6 update (어느 path 가 실제 풀렸는지 명시)
2. README / HANDOFF / spec §12 v0.4 항목 → "활성" 으로 이동, v0.5+ 항목 list update
3. `dist/LongYinRoster_v0.4.0/` zip
4. git tag `v0.4.0`, push
5. GitHub release with notes (체크박스 UI + 정체성/인벤토리/창고 활성, 부상/충성/호감 폐기 명시)

---

## 9. Testing

### 9.1 Unit Tests (신규)

| Test | 검증 |
|---|---|
| `ApplySelectionTests.JsonRoundTrip` | 9 필드 serialize/deserialize 동일성 |
| `ApplySelectionTests.V03Default` | 4 카테고리 on, 5 카테고리 off |
| `ApplySelectionTests.RestoreAll` | 9 카테고리 모두 on |
| `ApplySelectionTests.MissingFieldFallback` | v0.3 슬롯 (필드 없음) → V03Default 적용 |
| `SimpleFieldMatrixTests.CategoryAssignment` | 17 entry, 부상/충성/호감 = None |
| `SimpleFieldMatrixTests.NoActiveKungfuEntry` | 활성 무공 entry 제거됐는지 |
| `SlotMetadataTests.ApplySelectionField` | meta JSON 에 applySelection field |

기존 테스트는 호환성 유지 (signature 변경된 PinpointPatcher.Apply 호출자 update 만).

### 9.2 Smoke Checklist (게임 안 검증)

- **A. 체크박스 default 검증**: 신규 슬롯 → V03Default (스탯/명예/천부/스킨 on, 나머지 off)
- **B. Toggle 시 즉시 저장**: 정체성 체크 → 슬롯 file 의 applySelection.identity=true 확인 (mod 닫기 → 재오픈 후 같은 상태)
- **C. v0.3 호환 Apply**: V03Default 그대로 → v0.3 와 동일 결과 (단 부상/충성/호감 안 건드림)
- **D. 부분 Apply — 스탯만**: 다른 8 카테고리 off → 스탯만 변경, 정체성/인벤토리 그대로
- **E. 정체성 PoC**: 정체성 on + Apply → 이름 변화 → save/reload 후 정보창 표시
- **F. 인벤토리 PoC**: 인벤토리 on + Apply → 새 인벤토리 entry 사용 가능 (장비 / 소비 등)
- **G. 무공 active PoC**: 무공 active on + Apply → 활성 무공 표시 변화. player 미보유 skill 이면 warn 토스트
- **H. Restore 항상 모두 적용**: slot 0 [↶ 복원] → 체크박스 무관 9 카테고리 적용
- **I. PoC 실패 disabled UI**: capability false 카테고리는 회색 + "(v0.5+ 후보)"
- **J. v0.2 / v0.3 슬롯 호환**: 기존 슬롯 로드 → V03Default 자동 적용 → file 안 건드림

### 9.3 회귀 게이트

- v0.3 의 G1/G2/G3 (save→reload 후 정보창 정상) — 9 카테고리 어떤 조합 후에도 통과
- v0.3 의 25/25 unit tests — 신규 17 entry 매핑 + Category 추가 후에도 통과 (test count 신규 7 추가 → ~32)

---

## 10. Out of Scope (v0.5+)

- **무공 list (kungfuSkills)** — KungfuSkillLvData wrapper ctor 발견 또는 Harmony patch
- **외형 (faceData / partPosture / portraitID)** — HeroFaceData wrapper + sprite invalidation
- **부상 / 충성 / 호감 backup 옵션화** — v0.6+ 사용자가 명시 토글 시 활성
- **Apply preview (dry-run)** — selection 적용 시뮬레이션 + 변경 예정 list 표시
- **Selection 프리셋** — "전체" / "v0.3 호환" / "정체성만" 같은 단축 버튼 (v0.5+)
- **Disabled checkbox 클릭 시 토스트** — "PoC 실패 — v0.5+ 후보" 안내 (현재는 GUI.enabled=false 로만 차단)
- **Selection 리셋 버튼** — 슬롯별 default 복귀
- **자동 smoke harness** — IL2CPP 안 deterministic 게임 상태 변경
- **Reverse export** (game → 다른 player save) — 별도 모드
- **§7.2 매트릭스의 나머지 ⚪ 항목 활성화** — chaos/evil/armor/medResist/heroStrengthLv/AI/buff/mission/log/cd 등 30+ 필드. 각자 IL2CPP 한계 다름.

---

## 11. Open Questions (PoC 후 확정)

| # | 질문 | PoC 단계 |
|---|---|---|
| Q1 | 정체성 9 필드의 일관 우회 path — setter / backing field / Harmony 중 어느 것이 풀림? 일부만 풀리면 per-field 매핑? | **답: 시도 A (Setter 직접) PASS** — Plan Task A2 PoC (commit `69bcb7c`). Backing field 미발견. §6.1 PoC 결과 참조. save→reload risk 는 D15 smoke E |
| Q2 | nowActiveSkill 의 SetNowActiveSkill 가 wrapper reference 받을 때 — kungfuSkills 안의 같은 skillID 가진 wrapper 가 항상 1개? 중복 / 빈 list 처리? | **답: Capability FAIL (commit `001bc55` PoC)** — SetNowActiveSkill 이 wrapper.skillID 가 아니라 wrapper.lv 를 nowActiveSkill 에 set. mapping 가설 틀림. v0.5+ 로 deferred. §6.2 PoC 결과 참조 |
| Q3 | ItemData wrapper ctor / factory — IntPtr 직접 / static factory / Harmony hijack 중 어느 것이 풀림? | **답 (commit `caa36e1` PoC)**: `ItemData(ItemType _type)` game-self ctor + `ItemData(IntPtr)` Il2CppInterop ctor 존재. Static factory 미발견. 그러나 sub-data graph (equipmentData / medFoodData 등) reconstruction 미해결로 v0.5+ deferred. §6.3 PoC 결과 |
| Q4 | LoseAllItem() 의 부수효과 — 장비 자동 해제? 퀘스트 아이템 보호? | v0.5+ deferred (Q3 와 묶음 — ItemList 자체 deferred 이므로 LoseAllItem 호출 안 함) |
| Q5 | selfStorage.allItem 의 clear 방법 — IL2CppListOps.Clear raw 가 안전한가? game-self method 없음 | v0.5+ deferred (SelfStorage 자체 deferred) |

---

## 12. Decision Log

| # | 결정 | 선택 | 근거 |
|---|---|---|---|
| 1 | v0.4 scope | (B + 인벤토리/창고) 정체성 + 인벤토리/창고 + 체크박스 UI | R&D risk 분산 — 무공/외형은 v0.5+ 로 |
| 2 | 체크박스 granularity | 카테고리 단위 (9개) | UI 단순 + 사용자 결정 부담 적음 |
| 3 | 체크박스 위치 | SlotDetailPanel 인라인 | 항상 보임 + 1-step Apply |
| 4 | 비노출 카테고리 처리 | 영구 보존 (v0.3 의 부상/충성/호감 backup 폐기) | runtime state 라 backup 시점 의미 모호 |
| 5 | Default + 저장 | V03Default + 슬롯별 (`_meta.applySelection`) | v0.3 호환 + 슬롯마다 다른 의도 가능 |
| 6 | 자기집 add 카테고리 | 별도, default OFF | 신규 분리 → opt-in |
| 7 | 자기집 add 의 v0.3 호환 회귀 | OFF (사용자 명시 opt-in) | 신규 분리 의미 살림 |
| 8 | Restore 의 selection | 무시, 항상 모두 (RestoreAll) | "직전 상태로 정확 복귀" 의미 보존 |
| 9 | Step 6/7 Refresh | 항상 호출 (selection 무관) | derived state 일관성 |
| 10 | R&D fallback (PoC 실패) | Disabled checkbox + "(v0.5+ 후보)" label | UI 가 ground truth, release 진행 무관 |
| 11 | Capability 검사 | Plugin 시작 시 1 회 + cache | startup 1 회로 충분, frame loop 영향 없음 |
| 12 | Toggle 저장 | 즉시 disk write | 단순, 작은 JSON 이라 성능 OK |

---

## Appendix A — Files to Add / Modify / Remove

### Add
- `src/LongYinRoster/Core/ApplySelection.cs` (POCO + Default/RestoreAll/JSON)
- `src/LongYinRoster/Core/Capabilities.cs` (POCO — Probe 결과)
- `src/LongYinRoster/Core/IdentityFieldMatrix.cs` (9 필드 매핑 — PoC 후 path 명시)
- `src/LongYinRoster.Tests/ApplySelectionTests.cs` (4 tests)

### Modify
- `src/LongYinRoster/Core/PinpointPatcher.cs` (시그니처 + 신규 step 4 개 + Probe 추가)
- `src/LongYinRoster/Core/SimpleFieldMatrix.cs` (Category enum + 17 entry, 활성 무공 제거)
- `src/LongYinRoster/Slots/SlotMetadata.cs` (ApplySelection field)
- `src/LongYinRoster/Slots/SlotFile.cs` (toggle 시 즉시 저장 path 노출)
- `src/LongYinRoster/UI/SlotDetailPanel.cs` (체크박스 grid + Capability)
- `src/LongYinRoster/UI/ModWindow.cs` (Probe + cache + DoApply 시 selection 전달)
- `src/LongYinRoster/Util/KoreanStrings.cs` (9 카테고리 label + disabled suffix)
- `src/LongYinRoster.Tests/SimpleFieldMatrixTests.cs` (Category 검증)
- `src/LongYinRoster.Tests/SlotMetadataTests.cs` (applySelection field round-trip)

### Remove
- 없음 (v0.3 components 모두 유지, 변경만)
