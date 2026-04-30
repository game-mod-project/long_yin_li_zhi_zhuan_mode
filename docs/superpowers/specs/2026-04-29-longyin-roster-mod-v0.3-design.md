# LongYin Roster Mod v0.3 — Apply Pipeline Design

**작성일**: 2026-04-29
**선행 spec**: `docs/superpowers/specs/2026-04-27-longyin-roster-mod-design.md` (v0.1 base)
**선행 plan**: `docs/superpowers/plans/2026-04-27-longyin-roster-mod-plan.md` (v0.1 ~ v0.2)
**핸드오프**: `docs/HANDOFF.md`
**상태**: Draft — 사용자 승인 전

---

## 1. Context

### 1.1 v0.2 까지의 도달점

v0.1.0 (Live capture + slot management) 과 v0.2.0 (Import from save + input gating) 은 게임 안 검증 통과로 출시 완료. **Capture (game → slot) 흐름은 안정**:
- `HeroLocator.GetPlayer()` 가 `heroID==0` 인 플레이어 영웅 식별
- `SerializerService.Serialize(player)` 가 IL2CPP-bound Newtonsoft 의 단방향 직렬화로 JSON string 생성
- `SlotFile.Write` 가 atomic `.tmp → Replace` 로 디스크 저장
- 21슬롯 (0=자동백업 + 1~20) 관리, FileImport 로 SaveSlot 0~10 의 hero[0] import 까지 가능

남은 미구현은 **Apply (slot → game) 흐름**. v0.2 detail panel 의 `▼ 현재 플레이어로 덮어쓰기` / `복원` 버튼은 `(v0.x 예정)` 라벨로 disabled.

### 1.2 v0.2 가 시도한 두 접근 — 둘 다 실패

#### 시도 1: `JsonSerializer.Populate(reader, target)` (in-place mutation)

- BepInEx 로그: 예외 없이 통과 (`Populate succeeded on HeroData`)
- 진단: pre/post stat snapshot 비교 → **변경 0건** (silent no-op)
- 가설: HeroData 의 setter 들이 IL2CPP 빌드에서 strip 됨 ([Serializable] POCO 가 setter 안 사용, deserialization 콜백만 사용). Newtonsoft reflection 이 strip 된 method 호출을 silent skip.

#### 시도 2: `JsonConvert.DeserializeObject` + `HeroList[0]` reference swap

- 새 HeroData 인스턴스 생성 (Il2CppSystem.Object → wrapper class IntPtr ctor 로 wrap), `HeroList[0] = newHero` reflection setter 작동
- 보존 필드 (force / location / relations) 는 현재 player JSON 에서 머지 후 deserialize
- **결과**: 부분 작동 — 캐릭터 본질 (이름 / 스탯 / 천부) 은 swap. **그러나 broken**:
  - 착용 장비 (`nowEquipment`) 복사 안 됨 — game 의 ItemData 객체와 ID-link 못 함
  - 착용 무공 동상 — `equiped` flag 의 reference 해석 실패
  - 포트레이트 무너짐 — sprite asset reference lazy-load. 새 HeroData 가 그 trigger 못 함
  - 문파 정보 fail — MergePreservedFields 의 검증 부족
  - **save → reload 후 player 정보창 안 열림** — 일부 필드 inconsistent 로 NRE

### 1.3 v0.3 의 새 접근 — PinpointPatcher 패턴

**핵심 통찰**: HeroData 는 [Serializable] POCO 가 아니라 **game-state graph 의 노드**. 단순 JSON round-trip 으로는 reference link 복원 불가.

대신 game 자체가 노출하는 mutator method (`SetX`, `ChangeX`, `AddX`, `RefreshX`) 를 호출해서 필드 단위로 정밀 복사. game 이 자기 method 를 통해서 변경하면 force pool register / item ref-count / stat refresh 같은 side-effect 가 자동 처리됨.

**제약**: 어떤 method 가 실제 노출되어 있는지는 game binary 분석으로만 확인 가능. spec 시점에 모든 필드 매핑 결정 불가 → plan Task 1 의 dump 후 매트릭스 보강.

---

## 2. Goals & Non-goals

### 2.1 v0.3 Goals

- **G1**. Apply (slot → game) 흐름 정상 작동 — **stat / 명예 / 부상 / favor / 천부** 같은 numeric / Change-method 노출 필드의 캡처 → Apply 일관 복귀 (정체성 / 무공 / 인벤토리 / 장비 는 §12 v0.4 후보)
- **G2**. Restore (slot 0 → game) 흐름 — Apply 직후 자동백업 슬롯 0 으로 복귀 가능
- **G3**. **save → reload 후 player 정보창 정상 작동** — v0.2 시도 2 의 정확한 실패점 통과
- **G4**. 지원 필드 매트릭스 명시 — 어떤 필드가 v0.3 에서 Apply 되고, 어떤 필드가 best-effort 인지, 어떤 것이 v0.4 후보인지 사용자 visibility
- **G5**. Apply 실패 시 game state 가 inconsistent 로 안 남음 — 자동복원으로 Apply 직전 상태 복귀
- **G6**. UI: detail panel 의 Apply / Restore 버튼 활성, `(v0.x 예정)` 라벨 제거

### 2.2 v0.3 Non-goals (§12 도 참고)

- **N1**. JSON-based deserialization 재시도 (Populate / DeserializeObject) — 검증된 dead end, 재진입 안 함
- **N2**. `GameDataController.Save/Load` escape hatch — game state 오염 risk (다른 영웅 / 시간 / 위치 영향) 거부
- **N3**. HeroData 의 직접 reflection setter 호출 — Populate 가 silent no-op 인 같은 함정
- **N4**. force / location / relations Apply — `StripForApply` 가 미리 제거. 의미상 보존 필드
- **N5**. 다중 영웅 Apply — heroID==0 (플레이어) 만
- **N6**. Detail panel 의 "마지막 Apply 결과" 섹션 — v0.4
- **N7**. Apply preview (dry-run) — v0.5+
- **N8**. 필드 단위 selective Apply (cherry-pick) — v0.5+
- **N9**. 슬롯 schema 변경 — v0.2 슬롯 그대로 호환 유지
- **N10**. **Collection rebuild (kungfuSkills / itemListData / selfStorage)** — dump 결과 primitive-factory Add method 부재. v0.4 후보 (KungfuSkillLvData / ItemData wrapper ctor 또는 Harmony patch 필요)

---

## 3. Architecture

### 3.1 Layered components

```
UI Layer        : SlotDetailPanel.OnApplyRequested / OnRestoreRequested
                  → ModWindow.RequestApply / RequestRestore (ConfirmDialog)
                  → ModWindow.DoApply / DoRestore (orchestration)

Orchestration  : DoApply
                  - HeroLocator.GetPlayer 검증
                  - Config.AllowApplyToGame 검증
                  - 자동백업 (slot 0)
                  - SlotFile.Read → payload.Player (raw JSON string)
                  - PortabilityFilter.StripForApply (force/location/relations 제거)
                  - PinpointPatcher.Apply(strippedJson, currentPlayer)
                  - try/catch → 실패 시 자동복원 (slot 0 → PinpointPatcher.Apply 다시)
                  - 토스트 + 로그

Patch Layer    : PinpointPatcher.Apply — 7-step pipeline (§4)
                  HeroData self-method + Hero-related manager Refresh API 만 호출
                  ApplyResult 누적 (applied/skipped/warned/errors)

Discovery      : HeroDataDump (plan Task 1, release 전 제거)
                  [F12] 핸들러 → BepInEx 로그에 method 시그니처 dump
                  → docs/HeroData-methods.md (영구 reference)
```

### 3.2 Dependencies & boundaries

- `Core/PinpointPatcher.cs` 는 `Core/HeroLocator.cs` 와 `Slots/`, `UI/` 를 알지 못함. 입력은 (slotPlayerJson, currentPlayer) 두 인자, 출력은 ApplyResult.
- `Core/IL2CppListOps.cs` (재사용 reflection helpers) 는 PinpointPatcher 와 HeroLocator 둘 다 사용. 의존 없는 leaf util.
- `UI/ModWindow.cs` 는 orchestration 책임. PinpointPatcher 의 내부 step 을 알지 못함, ApplyResult 의 카운트만 사용.
- `Slots/`, `Util/PathProvider.cs`, `Util/Logger.cs`, `UI/InputBlockerPatch.cs`, `UI/InputDialog.cs`, `UI/FilePickerDialog.cs`, `UI/ConfirmDialog.cs`, `UI/ToastService.cs`, `UI/SlotListPanel.cs`, `Core/SerializerService.cs`, `Core/HeroLocator.cs`, `Core/PortabilityFilter.cs` — v0.3 에서 코드 변경 없음. (`Util/KoreanStrings.cs` 는 §10.3 의 신규 문자열 추가, `UI/SlotDetailPanel.cs` 는 §10.1 의 버튼 활성, `UI/ModWindow.cs` 는 §10.2 의 흐름 추가)

### 3.3 Relationship to v0.1 spec

| v0.1 spec 절 | v0.3 의 처분 |
|---|---|
| §"Apply 흐름" (가설) | **대체** — 본 문서 §4, §5 |
| §"PinpointPatcher" (no-op 정의) | **대체** — 본 문서 §4 |
| §"Slots" / §"Capture" / §"FileImport" / §"UI shell" | **유효** — 변경 없음 |
| §"Edge cases" | **추가** — §8 의 Failure modes 표가 보강 |

---

## 4. PinpointPatcher Pipeline (7-step)

### 4.1 Apply entry point + ApplyResult

```csharp
public static class PinpointPatcher
{
    public static ApplyResult Apply(string slotPlayerJson, object currentPlayer);

    private static void TryStep(string name, Action body, ApplyResult res, bool fatal = false);
    private static void SetSimpleFields(JsonElement slot, object player, ApplyResult res);
    private static void RebuildKungfuSkills(JsonElement slot, object player, ApplyResult res);
    private static void RebuildItemList(JsonElement slot, object player, ApplyResult res);
    private static void RebuildSelfStorage(JsonElement slot, object player, ApplyResult res);
    private static void RebuildHeroTagData(JsonElement slot, object player, ApplyResult res);
    private static void RefreshSelfState(object player, ApplyResult res);
    private static void RefreshExternalManagers(object player, ApplyResult res);
}

public sealed class ApplyResult
{
    public List<string> AppliedFields  { get; } = new();
    public List<string> SkippedFields  { get; } = new();
    public List<string> WarnedFields   { get; } = new();
    public List<Exception> StepErrors  { get; } = new();
    public bool HasFatalError { get; set; }
}
```

`Apply` 의 의사코드:

```csharp
public static ApplyResult Apply(string slotPlayerJson, object currentPlayer)
{
    var res = new ApplyResult();
    using var doc = JsonDocument.Parse(slotPlayerJson);
    var slot = doc.RootElement;

    TryStep("SetSimpleFields",         () => SetSimpleFields(slot, currentPlayer, res), res);
    TryStep("RebuildKungfuSkills",     () => RebuildKungfuSkills(slot, currentPlayer, res), res);
    TryStep("RebuildItemList",         () => RebuildItemList(slot, currentPlayer, res), res);
    TryStep("RebuildSelfStorage",      () => RebuildSelfStorage(slot, currentPlayer, res), res);
    TryStep("RebuildHeroTagData",      () => RebuildHeroTagData(slot, currentPlayer, res), res);
    TryStep("RefreshSelfState",        () => RefreshSelfState(currentPlayer, res),  res, fatal: true);
    TryStep("RefreshExternalManagers", () => RefreshExternalManagers(currentPlayer, res), res);

    return res;
}
```

`TryStep` 의 정책:
- step 의 body 가 throw → `res.StepErrors` 에 기록 + `Logger.Warn`
- `fatal: true` step 의 throw → `res.HasFatalError = true` (자동복원 트리거)
- `fatal: false` step 의 throw → 다음 step 진행 (부분 patch)
- step 안의 개별 field/entry 실패는 step 자체가 catch 후 `res.WarnedFields` 에 기록, step 자체는 throw 안 함

### 4.2 Step 1 — SetSimpleFields

**대상**: simple-value scalars 와 nested-but-not-collection (`baseAttri.*` 같은 sub-object 의 scalar). 예: `heroName`, `nickname`, `age`, `gender`, `fightScore`, `hp`, `maxhp`, `fame`, `heroTagPoint`, `baseAttri.*`, `totalAttri.*`, `金钱` (정확한 필드명은 dump).

**패턴**:
```csharp
foreach (var field in SimpleFieldMatrix)
{
    if (!TryReadJsonValue(slot, field.JsonPath, out var newValue, field.Type)) continue;
    var currentValue = ReadField(player, field.PropertyName);
    if (Equal(currentValue, newValue)) { res.AppliedFields.Add($"{field.Name} (no-op)"); continue; }

    if (field.SetterMethod == null) { res.SkippedFields.Add($"{field.Name} — no setter mapped"); continue; }

    try {
        InvokeSetter(player, field, newValue, currentValue);
        res.AppliedFields.Add(field.Name);
    } catch (Exception ex) {
        res.WarnedFields.Add($"{field.Name} — {ex.GetType().Name}: {ex.Message}");
    }
}
```

`SimpleFieldMatrix` 는 `static readonly` 리스트. 각 entry 의 shape:
- `Name` — 매트릭스/로그 표시용 한국어 이름
- `JsonPath` — slot JSON 안 위치 (`"heroName"`, `"baseAttri.wugong"` 등)
- `PropertyName` — HeroData 의 멤버 (현재값 읽기용 reflection target)
- `Type` — int / string / bool 등
- `SetterMethod` — game-self method 이름 (`"SetHp"` 또는 `"ChangeHp"`)
- `SetterStyle` — `Direct(newValue)` 또는 `Delta(newValue - current)`

**Nested scalar 처리** (`baseAttri.*`, `totalAttri.*` 등 sub-object 의 scalar): 우선 game 이 노출하는 `SetBaseAttriXxx(int)` 같은 직접 method 가 dump 에 있으면 그걸 사용. 없으면 — `RefreshSelfState` 의 `RefreshMaxAttriAndSkill` 이 base → total 재계산을 담당하므로 base 만 set, total 은 derived 로 두고 매트릭스에서 제외 (🟢 derived). base 도 직접 method 없으면 ⚪ → v0.4 후보. 직접 reflection set 은 §2.2 N3 으로 거부.

매트릭스 내용은 plan Task 1 의 dump 후 보강 round 에서 확정.

### 4.3 Step 2~5 — Rebuild collection (Clear=raw, Add=game method)

`HANDOFF §6.2` 의 4개 collection 에 동일 패턴 적용:

```csharp
RebuildKungfuSkills(slot, player, res):
    var il2List = ReadField(player, "kungfuSkills");
    IL2CppListOps.Clear(il2List);  // reflection raw clear
    var slotArr = slot.GetProperty("kungfuSkills");
    for (int i = 0; i < slotArr.GetArrayLength(); i++)
    {
        var entry = slotArr[i];
        var skillId  = entry.GetProperty("id").GetInt32();
        var lv       = entry.GetProperty("lv").GetInt32();
        var exp      = entry.GetProperty("exp").GetInt32();
        var equipped = entry.GetProperty("equiped").GetBoolean();
        try {
            InvokeAddKungfuSkill(player, skillId, lv, exp, equipped);
            res.AppliedFields.Add($"kungfuSkill[{skillId}]");
        } catch (Exception ex) {
            res.WarnedFields.Add($"kungfuSkill[{skillId}] — {ex.Message}");
        }
    }
```

각 collection 의 entry schema (id/lv/exp/equiped 등) 는 `SerializerService.Serialize` 가 게임 객체에서 만들어낸 JSON 의 모양. 즉 v0.1/v0.2 캡처 검증으로 안정적.

`AddXxxMethod` 시그니처는 dump 결과로 결정. 못 찾으면 해당 collection 은 매트릭스의 ⚪ → v0.4 후보.

**Clear 가 raw 인 이유**: game-self `ClearXxx` 가 모든 collection 에 노출되어 있을 보장 없음. raw clear 후 `RefreshSelfState` 가 attri / fightScore 재계산 — derived state 는 step 6 으로 위임.

**dump 결과 (plan Task 2)**: kungfuSkills / itemListData.allItem / selfStorage.allItem 의 primitive-factory Add method 부재 — Step 2~4 는 v0.3 에서 **placeholder skip + Logger.Warn**. 코드 흐름은 유지 (skeleton 호출 + skip 분기) 하지만 실제 collection 변경 안 함. v0.4 에서 KungfuSkillLvData / ItemData wrapper factory 또는 Harmony patch 추가 후 활성. heroTagData (Step 5) 만 dump 에 `AddTag(Int32, Single, String, Boolean, Boolean)` 발견 — 정상 구현.

### 4.4 Step 6 — RefreshSelfState (fatal step)

HeroData self-method 호출. dump 후 확정 — §7.2.1 Step 6 매핑 따름:

```csharp
TryInvoke(player, "RefreshMaxAttriAndSkill");
TryInvoke(player, "RefreshHeroSalaryAndPopulation");
TryInvoke(player, "RecoverState");
```

`HANDOFF §4.4` 의 기존 단서 method (`GetMaxAttri / GetMaxFightSkill / GetMaxLivingSkill / GetMaxFavor / GetFinalTravelSpeed`) 는 dump 로 보면 **read-only Single-반환 getter** — refresh 효과 없음. 본 step 에서 호출 안 함.

**fatal=true 이유**: 이 step 자체가 throw 하면 stat / skill 가 stale 인 채로 game 진행 → save→reload 시 NRE 위험. step 자체 throw 는 자동복원 트리거. 단, 일부 method 가 missing 이라서 individual try/catch 가 warn 으로만 기록되는 건 OK (fatal 아님).

### 4.5 Step 7 — RefreshExternalManagers

dump 결과 (§7.2.1 Step 7) — hero-display 매니저 (HeroIcon/HeroPanel) 미발견. `BigMapController.RefreshBigMapNPC(player)` **단 1 매니저만** 호출:

```csharp
TryInvokeManager("BigMapController", "RefreshBigMapNPC", player, res);
```

`AuctionController.RefreshOfferMoney` 도 dump 에 발견됐지만 hero refresh 의도가 아니라 경매 시 호출용 — 본 step 에서 호출 안 함. 기타 시각 갱신 (포트레이트 / 영웅 아이콘 / town panel) 은 game frame 의 자연 lazy-load (dirty flag 자동 검사) 에 위임.

**책임 boundary**: Hero 관련 표시 / 조회 매니저만. game-state global manager (TimeManager, FactionManager, BattleManager 등) 는 안 건드림.

### 4.6 TryStep 의 fatal 게이팅

```csharp
private static void TryStep(string name, Action body, ApplyResult res, bool fatal = false)
{
    try { body(); }
    catch (Exception ex)
    {
        Logger.Warn($"PinpointPatcher.{name} threw: {ex.GetType().Name}: {ex.Message}");
        res.StepErrors.Add(ex);
        if (fatal) res.HasFatalError = true;
    }
}
```

상위 (DoApply) 는 `res.HasFatalError` 만 보고 자동복원 결정. 일반 step 의 warn 은 Apply 자체는 success 로 간주.

---

## 5. Orchestration

### 5.1 Apply pipeline

```csharp
ModWindow.RequestApply(int slot)
{
    if (slot == 0) return;  // 슬롯 0 은 자동백업 — Apply 대상 아님
    if (!Repo.HasSlot(slot)) { Toast(KoreanStrings.ToastErrEmptySlot, Error); return; }

    _confirm.Open(
        title: KoreanStrings.ConfirmTitleApply,
        body:  string.Format(KoreanStrings.ConfirmApplyMain,
                             $"슬롯 {slot} · {Repo[slot].Meta.UserLabel}"),
        note:  KoreanStrings.ConfirmApplyPolicy,
        confirmLabel: KoreanStrings.Apply,
        checkboxLabel: KoreanStrings.AutoBackupCheckbox,
        checkboxDefault: Config.AutoBackupBeforeApply.Value,
        onConfirm: doAutoBackup => DoApply(slot, doAutoBackup));
}

ModWindow.DoApply(int slot, bool doAutoBackup)
{
    var player = Core.HeroLocator.GetPlayer();
    if (player == null) { Toast(KoreanStrings.ToastErrNoPlayer, Error); return; }
    if (!Config.AllowApplyToGame.Value) { Toast(KoreanStrings.ToastApplyDisabled, Error); return; }

    if (doAutoBackup)
    {
        try
        {
            var nowJson = Core.SerializerService.Serialize(player);
            Repo.WriteAutoBackup(BuildPayload(nowJson, source: "auto", label: "자동백업"));
        }
        catch (Exception ex)
        {
            Toast(string.Format(KoreanStrings.ToastErrAutoBackup, ex.Message), Error);
            return;
        }
    }

    SlotEntry loaded;
    string stripped;
    try
    {
        loaded   = Slots.SlotFile.Read(Repo.PathFor(slot));
        stripped = Core.PortabilityFilter.StripForApply(loaded.Player);
    }
    catch (Exception ex)
    {
        Toast(string.Format(KoreanStrings.ToastErrSlotRead, slot, ex.Message), Error);
        return;
    }

    Core.ApplyResult res;
    try { res = Core.PinpointPatcher.Apply(stripped, player); }
    catch (Exception ex)
    {
        Logger.Error($"PinpointPatcher.Apply top-level throw: {ex}");
        if (doAutoBackup) AttemptAutoRestore(player);
        Toast(string.Format(
            doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                         : KoreanStrings.ToastErrApplyNoBackup, ex.Message), Error);
        return;
    }

    if (res.HasFatalError)
    {
        if (doAutoBackup) AttemptAutoRestore(player);
        Toast(string.Format(
            doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                         : KoreanStrings.ToastErrApplyNoBackup,
            FirstErrorMessage(res)), Error);
        return;
    }

    Repo.Reload();
    Toast(string.Format(KoreanStrings.ToastApplyOk, slot,
                        res.AppliedFields.Count, res.SkippedFields.Count), Success);
    Logger.Info($"Apply slot={slot} applied={res.AppliedFields.Count} " +
                $"skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
}
```

### 5.2 Restore (slot 0 → game)

```csharp
ModWindow.RequestRestore()
{
    if (!Repo.HasSlot(0)) { Toast(KoreanStrings.ToastErrNoBackup, Error); return; }

    _confirm.Open(
        title: KoreanStrings.ConfirmTitleRestore,
        body:  KoreanStrings.ConfirmRestoreMain,
        note:  KoreanStrings.ConfirmApplyPolicy,
        confirmLabel: KoreanStrings.Restore,
        checkboxLabel: null,                       // 자동백업 옵션 없음
        checkboxDefault: false,
        onConfirm: _ => DoApply(slot: 0, doAutoBackup: false));
}
```

Restore 는 Apply 의 source 만 다를 뿐 같은 코드 path. `doAutoBackup: false` 이유 — 자동복원 자체가 슬롯 0 사용 중이라 다시 백업하면 의미 모호.

### 5.3 AttemptAutoRestore

```csharp
private void AttemptAutoRestore(object player)
{
    try
    {
        var slot0 = Slots.SlotFile.Read(Repo.PathFor(0));
        var stripped = Core.PortabilityFilter.StripForApply(slot0.Player);
        var res = Core.PinpointPatcher.Apply(stripped, player);
        if (res.HasFatalError)
            Logger.Error("Auto-restore also failed — game state may be inconsistent");
        else
            Logger.Info($"Auto-restore OK applied={res.AppliedFields.Count}");
    }
    catch (Exception ex)
    {
        Logger.Error($"Auto-restore threw: {ex}");
    }
}
```

자동복원 자체가 실패하면 추가 토스트 안 띄움 (이미 error 토스트 있음). 사용자가 게임 자체 save→load 로 회복할 수 있음을 README/HANDOFF 에 명시.

---

## 6. Discovery (HeroDataDump)

### 6.1 Why dump first

PinpointPatcher 의 SimpleFieldMatrix / AddXxxMethod / HeroRelatedManagers 는 모두 **HeroData 와 매니저들의 실제 method 시그니처에 의존**. 게임 binary 의 strip 결과는 추측 불가 — 실측 필요.

`Assembly-CSharp.dll` 의 정적 분석은 strip 전 정보. IL2CPP 빌드의 method 가 실제 노출되는지는 **게임 실행 중 reflection enumeration** 으로만 확인.

따라서 plan Task 1 = HeroDataDump. dump 결과 없이 spec 의 매트릭스를 채우면 placeholder 만 양산.

### 6.2 Dump scope

BindingFlags 는 `Public | NonPublic | Instance` — strip 된 setter 의 잔재가 NonPublic 으로 남아 있을 수 있어 함께 enumerate. property / field 도 dump 해서 SimpleFieldMatrix 의 PropertyName 후보 확보.

**(1) HeroData self method + property + field**:
```csharp
const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
var heroType = Core.HeroLocator.GetPlayer()?.GetType();
foreach (var m in heroType.GetMethods(F))
{
    var sig = $"{m.ReturnType.Name} {m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})";
    Logger.Info($"HeroDataDump.self.method: {sig}");
}
foreach (var p in heroType.GetProperties(F))
    Logger.Info($"HeroDataDump.self.prop: {p.PropertyType.Name} {p.Name} {{ get={p.CanRead}, set={p.CanWrite} }}");
foreach (var f in heroType.GetFields(F))
    Logger.Info($"HeroDataDump.self.field: {f.FieldType.Name} {f.Name}");
```

**(2) Hero-related manager 후보**:
```csharp
foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
foreach (var t in SafeGetTypes(asm))
{
    if (!t.Name.EndsWith("Manager") && !t.Name.EndsWith("Controller")) continue;
    foreach (var m in t.GetMethods(F))
    {
        if (!Regex.IsMatch(m.Name, "^(Refresh|Update|OnHero|Rebuild)")) continue;
        if (!m.GetParameters().Any(p => p.ParameterType == heroType ||
                                        p.ParameterType.Name == "HeroData")) continue;
        Logger.Info($"HeroDataDump.mgr: {t.FullName}.{m.Name}({...})");
    }
}
```

### 6.3 임시 코드 — release 전 제거 약속

`Core/HeroDataDump.cs` 와 `[F12]` 핸들러는 plan Task 1 에서 추가, **release 전 (plan 마지막 task) 제거**. release zip 에 포함 안 됨.

### 6.4 Dump 산출물

게임 안 [F12] 누른 후 `BepInEx/LogOutput.log` 에 `HeroDataDump:` prefix 의 method list. 사용자가 추출하여 `docs/HeroData-methods.md` 에 정리. 영구 reference 로 git 에 commit.

---

## 7. Support Field Matrix

### 7.1 Phase 분류

| 기호 | 의미 |
|---|---|
| 🟢 v0.3 지원 | game-self method 검증 완료 + 부수효과 안전 |
| 🟡 v0.3 best-effort | game method 호출하지만 일부 derived state 가 RefreshAll 까지 stale |
| ⚪ v0.4 후보 | dump 했지만 적절한 method 못 찾음 / 검증 부족 |
| ⛔ 영구 비지원 | 의미상 보존 필드 (force/location/relations) |

### 7.2 카테고리별 매트릭스 (dump-evidenced, plan Task 2 보강 완료)

dump 산출물 = `docs/HeroData-methods.md` (759 methods, 177 properties, 2 fields, 2 managers).
Decision criteria: § 7.2 의 "🟢 / 🟡 / ⚪ / ⛔" 분류는 dump 에서 game-self method 가 발견됐는지로만 결정. property setter (`set_xxx`) 만 있는 경우는 §2.2 N3 정책에 따라 ⚪.

| 카테고리 | 필드 | 분류 | dump 근거 |
|---|---|---|---|
| 정체성 | `heroName` (String) | ⚪ | `set_heroName(String)` 만 (property setter — N3 거부). game method 없음 |
| 정체성 | `heroNickName` (String) | ⚪ | `set_heroNickName(String)` 만 |
| 정체성 | `heroFamilyName` (String) | ⚪ | `set_heroFamilyName(String)` 만 |
| 정체성 | `settingName` (String) | ⚪ | `set_settingName(String)` 만 |
| 정체성 | `isFemale` (Boolean) | ⚪ | `set_isFemale(Boolean)` 만 |
| 정체성 | `age` (Int32) | ⚪ | `set_age(Int32)` 만. game method 없음 |
| 정체성 | `nature` (Int32) | ⚪ | `set_nature(Int32)` 만 |
| 정체성 | `talent` (Int32) | ⚪ | `set_talent(Int32)` 만 |
| 정체성 | `generation` (Int32) | ⚪ | `set_generation(Int32)` 만 |
| 외형 | `faceData` (HeroFaceData) | ⚪ | `set_faceData(HeroFaceData)` 만. `LoadFaceCode(String)` / `RandomFaceData(Boolean)` 는 reset/random 용 |
| 외형 | `partPosture` (PartPostureData) | ⚪ | `set_partPosture(PartPostureData)` 만 |
| 외형 | `skinID` / `skinLv` / `setSkinID` / `setSkinLv` (Int32) | 🟢 | **`SetSkin(Int32, Int32)`** game method — Direct |
| 외형 | `defaultSkinID` (Int32) | ⚪ | `ResetDefaultSkin()` 은 reset, 직접 set 아님 |
| 외형 | `skinColorDark` / `voicePitch` (Single) | ⚪ | property setter 만 |
| 스탯 base | `baseAttri` (List`1) | 🟢 (Delta) | **`ChangeAttri(Int32, Single, Boolean, Boolean)`** — Delta. base 만 set, total 은 §6 derived |
| 스탯 base | `baseFightSkill` (List`1) | 🟢 (Delta) | **`ChangeFightSkill(Int32, Single, Boolean, Boolean)`** — Delta |
| 스탯 base | `baseLivingSkill` (List`1) | 🟢 (Delta) | **`ChangeLivingSkill(Int32, Single, Boolean, Boolean)`** + **`ChangeLivingSkillExp(Int32, Single, Boolean)`** — Delta |
| 스탯 base | `expLivingSkill` (List`1) | 🟡 | `ChangeLivingSkillExp` 가 자동 갱신 (best-effort) |
| 스탯 max | `maxAttri` (List`1) | 🟢 (Delta) | **`ChangeMaxAttri(Int32, Int32, Boolean)`** — Delta. step 6 의 `RefreshMaxAttriAndSkill` 가 base 로부터 재계산하므로 보통 derived |
| 스탯 max | `maxFightSkill` / `maxLivingSkill` (List`1) | 🟢 derived | `ChangeMaxFightSkill` / `ChangeMaxLivingSkill` 존재하지만 step 6 이 재계산 — 직접 set 불필요 |
| 스탯 max | `maxhp` / `maxMana` / `maxPower` (Single) | 🟢 (Delta) | **`ChangeMaxHp` / `ChangeMaxMana` / `ChangeMaxPower`** — Delta. 단 보통 step 6 derived |
| 스탯 derived | `totalAttri` / `totalFightSkill` / `totalLivingSkill` (List`1) | 🟢 derived | step 6 의 `RefreshMaxAttriAndSkill` 가 base + buff 로 재계산. 직접 set 안 함 |
| 스탯 derived | `fightScore` (Single) | 🟢 derived | `GetFightScore(Boolean)` 가 계산 method, set 없음. step 6 후 game 이 계산 |
| 스탯 derived | `realMaxHp` / `realMaxMana` / `realMaxPower` (Single) | 🟢 derived | step 6 derived |
| 스탯 current | `hp` (Single) | 🟢 (Delta) | **`ChangeHp(Single, Boolean, Boolean, Boolean, Boolean)`** — Delta |
| 스탯 current | `mana` (Single) | 🟢 (Delta) | **`ChangeMana(Single, Boolean, Boolean, Boolean)`** — Delta |
| 스탯 current | `power` (Single) | 🟢 (Delta) | **`ChangePower(Single, Boolean)`** — Delta |
| 부상 | `externalInjury` / `internalInjury` / `poisonInjury` (Single) | 🟢 (Delta) | **`ChangeExternalInjury` / `ChangeInternalInjury` / `ChangePoisonInjury`** — Delta |
| 무공 | `kungfuSkills` (List`1, 4 KungfuSkillLvData entries) | ⚪ | dump 에 `AddKungfuSkill(id, lv, exp, equipped)` 같은 신규 추가 method **없음**. `EquipSkill(KungfuSkillLvData, Boolean)` / `LoseSkill(KungfuSkillLvData)` / `UpgradeSkill(KungfuSkillLvData)` 는 기존 객체 조작. **신규 ID 로 무공 추가 method 미발견** |
| 무공 (참고) | `attackSkills` / `attackSkillSaveRecord` | ⚪ | property setter 만 (List 단위 set 은 reference swap — N3 거부) |
| 무공 (참고) | `dodgeSkill` / `internalSkill` / `uniqueSkill` (KungfuSkillLvData) | ⚪ | property setter 만 |
| 무공 active | `nowActiveSkill` (Int32) | 🟢 | **`SetNowActiveSkill(KungfuSkillLvData)`** — Direct |
| 무공 max | `skillMaxPracticeExpData` (List`1) | 🟡 | **`AddSkillMaxPracticeExp(SkillMaxPracticeExpData)`** — entry 단위 add. Clear method 미발견 → raw clear + add |
| 인벤토리 | `itemListData.allItem` | ⚪ | dump 에 `AddItem(int, int)` 또는 `AddItem(ItemData)` **없음**. `GetItem(ItemData, Boolean, Boolean, Int32, Boolean)` 등 3 overloads 가 존재하지만 모두 ItemData 객체 인자 — 슬롯 JSON 의 primitive (id, count) 로부터 ItemData 객체 구성 method 미발견. `LoseAllItem()` 은 clear 가능 |
| 창고 | `selfStorage.allItem` | ⚪ | 동상. `GetItem(ItemListData, ...)` overload 가 있지만 ItemListData 가 인자 — 슬롯 JSON 으로 부터 직접 구성 못 함 |
| 천부 | `heroTagData` (List`1) | 🟡 | **`AddTag(Int32, Single, String, Boolean, Boolean)`** — entry 단위 add. **`ClearAllTempTag()`** 는 temp 만 clear. `RemoveTag(Int32, Boolean)` / `RemoveTag(String, Boolean)` 로 개별 제거 가능 → raw clear + add |
| 천부 | `heroTagPoint` (Single) | 🟢 (Delta) | **`ChangeTagPoint(Single, Boolean)`** — Delta |
| 명예 | `fame` (Single) | 🟢 (Delta) | **`ChangeFame(Single, Boolean)`** — Delta |
| 명예 | `badFame` (Single) | 🟢 (Delta) | **`ChangeBadFame(Single, Boolean, HeroData, Boolean)`** — Delta |
| 명예 | `hornorLv` (Int32) | ⛔ | `ChangeHornorLv(Int32)` 존재하지만 force-related — `PortabilityFilter._faction` 가 strip (§2.2 N4). SimpleFieldMatrix 에서 제거 |
| 명예 | `governLv` (Int32) | ⛔ | `ChangeGovernLv(Int32)` 존재하지만 force-related — `PortabilityFilter._faction` 가 strip (§2.2 N4). SimpleFieldMatrix 에서 제거 |
| 충성/호감 | `favor` (Single) | 🟢 | **`SetFavor(Single, Boolean)`** — Direct (또는 `ChangeFavor(...)` Delta) |
| 충성/호감 | `loyal` (Single) | 🟢 (Delta) | **`ChangeLoyal(Single, Boolean)`** — Delta. `ResetLoyal()` 도 사용 가능 |
| 충성/호감 | `chaos` / `evil` / `armor` (Single) | ⚪ | property setter 만 |
| 충성/호감 | `medResist` (Single) | ⚪ | property setter 만. `GetMedResist()` 는 read-only |
| 공헌 | `forceContribution` / `governContribution` (Single) | ⛔ | `ChangeForceContribution(Single, Boolean, Int32)` / `ChangeGovernContribution(Single, Boolean)` 존재하지만 force-related — `PortabilityFilter._faction` 가 strip (§2.2 N4). SimpleFieldMatrix 에서 제거 |
| 공헌 | `lastFightContribution` / `lastMonthContribution` / `lastYearContribution` / `thisMonthContribution` / `thisYearContribution` (Single) | ⚪ | property setter 만. `ClearContributionRecord()` 만 reset. **공헌 이력은 history-like 이므로 Apply 시점 의미 모호** — v0.4 검토 |
| 시스템 | `salary` (Int32) / `population` (Int32) | 🟢 derived | `RefreshHeroSalaryAndPopulation()` 가 재계산 — step 6 으로 위임 |
| 시스템 | `selfHouseTotalAdd` (Single) | 🟢 (Delta) | **`ChangeSelfHouseTotalAdd(Single)`** — Delta |
| 시스템 | `heroStrengthLv` (Single) | ⚪ | property setter 만 |
| 시스템 | `heroForceLv` (Int32) | ⛔ | `SetHeroForceLv(Int32)` / `ChangeHeroForceLv(Int32, Boolean)` 존재하나 — force 변경은 §2.2 N4 (보존 필드) 영역. PortabilityFilter 가 strip |
| 시스템 | `heroID` (Int32) | ⛔ | identity field — Apply 가 같은 player (heroID==0) 대상이라 변경 무의미 |
| 시스템 | `summonID` / `summonLv` / `summonMoveRange` / `summonControlable` / `summonSourceHero` / `isSummon` | ⚪ | property setter 만. summon 시스템 별도 — v0.4 |
| 상태 boolean | `dead` / `hide` / `inHill` / `inMountain` / `inPrison` / `inSafeArea` / `inTeam` / `inWater` / `rest` / `bodyGuard` / `equipLock` / `skillLock` 외 다수 | ⚪ | property setter 만. transient runtime state — 일부는 **§4.4 보존 필드** 카테고리 검토 (location/in* — `StripForApply` 와 중복). 다른 boolean (`equipLock`, `skillLock`) 은 ⚪ |
| 상태 cd | `changeSkinCd` / `forceJobCD` / `selfCureTime` / `playerBecomeTeacherTime` (Int32) | ⚪ | property setter 만. timer-like — Apply 시점 의미 모호 |
| 장비 | `nowEquipment` (HeroEquipmentData) | ⚪ | property setter 만. `EquipItem(ItemData, Boolean, Boolean)` 존재하지만 ItemData 인자 — JSON primitive 부터 구성 못 함 (인벤토리와 동일 한계) |
| 장비 | `horse` / `horseArmor` (ItemData) + `horseSaveRecord` / `horseArmorSaveRecord` (Int32) | ⚪ | `EquipHorse(ItemData, Boolean)` / `UnequipHorse(...)` 는 ItemData 인자 — 인벤토리와 동일 한계 |
| 관계 (보존) | `Lover` / `Teacher` / `Brothers` / `Friends` / `Haters` / `PreLovers` / `Relatives` / `Students` | ⛔ | §2.2 N4 — `StripForApply` 가 미리 제거. game method 는 존재 (`SetLover`, `AddBrother`, `AddFriend`, `AddHater`, `AddPrelover`, `AddStudent` 등) 지만 v0.3 적용 안 함 |
| 관계 (보존) | `belongForceID` / `servantForceID` / `branchLeaderAreaID` / `forceJobID` / `forceJobType` / `skillForceID` (Int32) | ⛔ | §2.2 N4 |
| 위치 (보존) | `atAreaID` / `bigMapPos` / `forceMission` | ⛔ | §2.2 N4 |
| Mission/Log | `missions` / `recordLog` / `missionNumCount` / `plotNumCount` (List/Int32) | ⚪ | property setter 만. `AddLog(String)` 만 entry 단위 add — record 단위 reconstruct 어려움 |
| AI | `heroAIData` / `heroAIDataArriveTargetRecord` / `heroAISettingData` / `dailyAIManaged` / `autoSetting` / `playerInteractionTimeData` | ⚪ | `SetHeroAIData(HeroAIData)` 존재. `ResetAI()` / `ResetAutoSetting()` 도 가능. **runtime AI state 라 Apply 시점 의미 모호** |
| Buff | `baseAddData` / `totalAddData` / `heroBuff` (HeroSpeAddData) | ⚪ | property setter 만. `AddBuff(Int32, Single)` 가 있지만 buff entry 단위 — buff list reconstruct 미지원 |
| Misc | `goodKungfuSkillName` / `hobby` / `kungfuSkillFocus` / `livingSkillFocus` / `skillCount` / `teamMates` (List`1) | ⚪ | property setter 만 (List reference swap — N3) |
| Dirty flag | `heroBuffDirty` / `heroDetailDirty` / `HeroIconDirty` / `heroIconDirtyCount` | ⚪ | property setter 만. step 6 의 refresh 가 자동 토글 |
| 보존 (PortabilityFilter) | force / location / relations 일체 | ⛔ | §2.2 N4. `StripForApply` 가 미리 제거 |

**요약 카운트** (HeroData self property + 관련 field 단위 — 매트릭스 row 수):
- 🟢 / 🟢 derived: **23 row** (직접 patch 가능 + step 6 derived)
- 🟡 best-effort: **3 row** (heroTagData, expLivingSkill, skillMaxPracticeExpData)
- ⚪ v0.4 후보: **30+ row** (대부분 property setter only — N3 거부)
- ⛔ 영구 비지원: relations / force / location / heroForceLv / heroID 묶음

### 7.2.1 method 매핑 (dump 후 확정, PinpointPatcher 의 호출 contract)

PinpointPatcher 의 각 step 이 호출하는 game-self method 매핑. 이 sub-section 은 plan Task 4 (SimpleFieldMatrix.cs) / Task 5~9 (각 Rebuild step) / Task 10 (RefreshSelfState) / Task 11 (RefreshExternalManagers) 의 코드 생성 contract.

#### Step 1 — SetSimpleFields

`SimpleFieldEntry` 형식: `Name | JsonPath | PropertyName | Type | SetterMethod | Style`

dump 에서 game-self setter / changer 가 발견된 simple-value 필드만 entry 로 등록. 미발견은 ⚪.

| Name | JsonPath | PropertyName | Type | SetterMethod | Style |
|---|---|---|---|---|---|
| 명예 | `fame` | `fame` | Single | `ChangeFame` | Delta `(new - cur, false)` |
| 악명 | `badFame` | `badFame` | Single | `ChangeBadFame` | Delta `(new - cur, false, null, false)` |
| HP | `hp` | `hp` | Single | `ChangeHp` | Delta `(new - cur, false, false, false, false)` |
| Mana | `mana` | `mana` | Single | `ChangeMana` | Delta `(new - cur, false, false, false)` |
| Power | `power` | `power` | Single | `ChangePower` | Delta `(new - cur, false)` |
| 외상 | `externalInjury` | `externalInjury` | Single | `ChangeExternalInjury` | Delta `(new - cur, false, false, false)` |
| 내상 | `internalInjury` | `internalInjury` | Single | `ChangeInternalInjury` | Delta `(new - cur, false, false, false)` |
| 중독 | `poisonInjury` | `poisonInjury` | Single | `ChangePoisonInjury` | Delta `(new - cur, false, false, false)` |
| 충성 | `loyal` | `loyal` | Single | `ChangeLoyal` | Delta `(new - cur, false)` |
| 호감 | `favor` | `favor` | Single | `SetFavor` | Direct `(new, false)` |
| 자기집 add | `selfHouseTotalAdd` | `selfHouseTotalAdd` | Single | `ChangeSelfHouseTotalAdd` | Delta `(new - cur)` |
| 천부 포인트 | `heroTagPoint` | `heroTagPoint` | Single | `ChangeTagPoint` | Delta `(new - cur, false)` |
| 활성 무공 | `nowActiveSkill` | `nowActiveSkill` | Int32 | (⚪) | `SetNowActiveSkill` 가 `KungfuSkillLvData` 인자 받음 — Step 2 후 별도 처리 |
| 스킨 | `skinID` + `skinLv` | `skinID` / `skinLv` | Int32 | `SetSkin` | Direct `(skinID, skinLv)` (multi-arg) |
| baseAttri[i] | `baseAttri[i].x` | `baseAttri` (List indexer) | Single | `ChangeAttri` | Delta `(i, new - cur, false, false)` |
| baseFightSkill[i] | `baseFightSkill[i].x` | `baseFightSkill` (List indexer) | Single | `ChangeFightSkill` | Delta `(i, new - cur, false, false)` |
| baseLivingSkill[i] | `baseLivingSkill[i].x` | `baseLivingSkill` (List indexer) | Single | `ChangeLivingSkill` | Delta `(i, new - cur, false, false)` |
| expLivingSkill[i] | `expLivingSkill[i].x` | `expLivingSkill` (List indexer) | Single | `ChangeLivingSkillExp` | Delta `(i, new - cur, false)` |

**미매핑 (⚪) — Step 1 에서 skip + log warn**:
- `heroName`, `heroNickName`, `heroFamilyName`, `settingName`, `isFemale`, `age`, `nature`, `talent`, `generation`, `voicePitch`, `chaos`, `evil`, `armor`, `medResist`, `heroStrengthLv`, `summonID/Lv`, `defaultSkinID`, `skinColorDark`, AI / Buff / Mission / Log / Cd / Dirty 일체.

이들은 v0.4 후보 (Open Q1 갱신 — 매트릭스의 Misc property setter only 필드는 game method 추가 enumerate 또는 Harmony patch 우회로 풀어야).

**hornorLv / governLv / forceContribution / governContribution 제외 사유**:
이 4 필드는 dump 매핑 시 game-self setter (`ChangeHornorLv` 등) 발견됐지만,
`PortabilityFilter._faction` 이 force-related 로 strip — spec §2.2 N4 의 force-preserve
정책. 따라서 SimpleFieldMatrix 에서 제거 (matrix 가 strip 된 필드를 read 시 "not in
slot JSON" skip 발생). v0.4 에서 사용자 옵션 (force-state 도 backup) 검토 가능.
22 dump-evidenced row → 18 entry (Task 7-fix 에서 정정).

#### Step 2 — RebuildKungfuSkills

**상태**: ⚪ — dump 에 적합한 add method 없음.

```text
Clear: IL2CppListOps.Clear(player.kungfuSkills)   // raw reflection
Add:   (미발견) — JSON entry (id, lv, exp, equipped) 으로 KungfuSkillLvData 객체 구성 method 가 dump 에 없음
       대안 후보: EquipSkill(existing KungfuSkillLvData, bool) — 이미 player.kungfuSkills 에 들어있는 객체에만 작동
```

**결정**: Step 2 는 v0.3 에서 **best-effort**:
- raw clear 만 수행 → step 6 의 refresh 가 빈 list 로부터 stat 재계산 → **fightScore 가 0 이 되는 부작용**
- 또는 step 2 자체 skip 하고 캐릭터의 무공은 Apply 직전 게임 상태 유지

**선택**: Step 2 자체 skip + matrix 에 `kungfuSkills ⚪` 명시. v0.4 에서 dump 보강 (다른 namespace / static factory) 또는 Harmony patch 로 KungfuSkillLvData 직접 생성. 본 spec 의 §4.3 step 2 의사코드는 그대로 유지하되, plan Task 5 의 구현은 placeholder (`res.SkippedFields.Add("kungfuSkills — no AddKungfuSkill in dump")`) 로 둔다.

#### Step 3 — RebuildItemList

**상태**: ⚪ — 동상.

```text
Clear: IL2CppListOps.Clear(player.itemListData.allItem)  // raw reflection (또는 LoseAllItem())
Add:   (미발견) — JSON entry (itemID, count, ...) 으로 ItemData 객체 구성 method 없음
       GetItem(ItemData, Boolean, Boolean, Int32, Boolean) 등 overload 가 ItemData 인자 받음 (이미 객체 필요)
```

**결정**: Step 3 도 best-effort skip. v0.4 에서 ItemData factory dump 또는 Harmony patch.

#### Step 4 — RebuildSelfStorage

**상태**: ⚪ — Step 3 동일.

```text
Clear: IL2CppListOps.Clear(player.selfStorage.allItem)
Add:   (미발견)
```

#### Step 5 — RebuildHeroTagData

**상태**: 🟡 best-effort.

```text
heroTagPoint: ChangeTagPoint(new - cur, false)   // step 1 에서 처리
heroTagData.Clear: 
    - ClearAllTempTag()  // temp 만 clear (game-self)
    - 그 후 raw enumerate + RemoveTag(int, false) 또는 RemoveTag(string, false) 호출 (각 entry id/name 으로)
heroTagData.Add: AddTag(tagID, point, source, true, false)  // 5-arg overload
```

이 step 은 dump 의 `AddTag` / `RemoveTag` / `ClearAllTempTag` / `RemoveAllDebuff` 조합으로 reconstructable. 단 `AddTag` 의 마지막 두 boolean 인자 의미 미상 (dump 는 시그니처만 노출 — 호출 시 default `true, false` 가정).

#### Step 6 — RefreshSelfState (fatal)

dump 에서 발견된 호출 가능 method 들 (no-arg refresh):

```text
RefreshMaxAttriAndSkill()              ✓ no-arg, 핵심 — base attri/skill 로부터 maxAttri/maxFightSkill/maxLivingSkill 재계산
RefreshHeroSalaryAndPopulation()       ✓ no-arg, salary/population derived 재계산
RecoverState()                          ✓ no-arg, 일반 state recover (의미는 미상 — 안전하게 호출)
ResetHeroSkillID()                      ⚪ skill ID reset — Apply 시점 의미 모호, skip
```

dump 의 `GetMaxAttri(Int32)` / `GetMaxFightSkill(Int32)` / `GetMaxLivingSkill(Int32)` / `GetMaxFavor(Single)` / `GetFinalTravelSpeed()` 는 **read-only getter** (반환값 Single). Apply step 으로는 의미 없음 — **step 6 에서 호출 안 함** (HANDOFF §4.4 의 "단서 method" 가설은 read-only 라는 신규 evidence 로 정정).

**Step 6 호출 list (확정)**:
1. `RefreshMaxAttriAndSkill()` (no-arg)
2. `RefreshHeroSalaryAndPopulation()` (no-arg)
3. `RecoverState()` (no-arg)

세 method 모두 fatal step body 안에서 individual try/catch — 한 method missing 도 fatal 처리 안 함, step body 의 throw 만 fatal.

#### Step 7 — RefreshExternalManagers

dump 에서 발견된 manager 후보 2 개:

```text
AuctionController.RefreshOfferMoney(Single, HeroData)    ⚪ 경매 시 호출, hero 갱신 의미 아님 — skip
BigMapController.RefreshBigMapNPC(HeroData)              🟢 큰 지도 NPC 표시 갱신 — 호출
```

**결정 (책임 축소)**:
- Step 7 은 `BigMapController.RefreshBigMapNPC(player)` **단 1개** 호출.
- 그 외 hero icon / portrait / panel 매니저는 **자기 frame update 의 lazy refresh** 에 의존 — game frame 이 자동으로 dirty flag 검사하므로 spec §3.1 의 PinpointPatcher 책임 boundary 안에서 추가 노력 안 함.
- v0.4 에서 추가 dump (HeroData 인자 없는 매니저 — `RefreshAll(int heroID)` 또는 dirty flag 기반) 로 보강.

### 7.3 Dump 후 spec 보강 절차 (완료)

본 §7.2 / §7.2.1 가 plan Task 2 의 산출물. 다음 commit 이 spec 의 두 번째 commit:

```text
docs: HeroData method dump + spec v0.3 support matrix refined per dump
```

매트릭스 보강 결과:
- ⚪ v0.4 후보가 30+ rows — **v0.3 Apply 의 가치는 stat / 명예 / 부상 / favor / 천부 등 numeric simple field 에 집중**, 정체성 / 외형 / 무공 / 인벤토리는 v0.4 로 deferred.
- 🟢 row (23) 만 해도 stat-snapshot 캡처 / 복원 시나리오는 의미 있게 동작 — 사용자가 "캐릭터 능력 백업" 용도로 쓸 수 있음.
- ⚪ 가 너무 많아 보이지만 그 중 다수는 transient runtime / dirty / cd 같은 Apply 의미 모호 필드 — 실제 Apply 가치 있는 필드 (heroName, kungfuSkills, items) 만 좁히면 v0.4 작업 범위가 명확.

사용자 review → "보강 매트릭스 OK" 후 plan Task 3 (ApplyResult POCO + tests) 진입.

---

## 8. Error Handling

### 8.1 Failure modes

| 시점 | 실패 유형 | 대응 |
|---|---|---|
| `HeroLocator.GetPlayer()` null | 게임 진입 전 | 토스트 "게임 안 진입 후 시도", 변경 0 |
| `Config.AllowApplyToGame=false` | dump phase / 의도적 비활성 | 토스트 "Apply disabled in config", 변경 0 |
| `WriteAutoBackup` throws | 슬롯 0 디스크 I/O 실패 | 토스트 "자동백업 실패: {ex}", **Apply 진행 안 함** |
| `SlotFile.Read` throws | 슬롯 파일 손상 | 토스트 "슬롯 N 읽기 실패: {ex}", 자동백업했으면 game state 는 안 변경됨 (자동복원 불필요) |
| `StripForApply` throws | JSON 파싱 실패 | 동상 |
| `PinpointPatcher.Apply` top-level throw | 예상 못한 예외 | 자동백업했으면 자동복원 + 토스트 "Apply 실패, 자동복원됨". 자동백업 안 했으면 토스트 "Apply 실패, 자동백업 비활성 — 수동 복구" |
| `ApplyResult.HasFatalError` (step 6 실패) | RefreshSelfState 실패 — stat stale 위험 | 동상 |
| step 1~5 의 일부 field warn/skip | 부분 patch (정상 case) | success 토스트 + 카운트 ("Apply 됨: 23개, 미지원: 4개"), `Logger.Info` 에 상세 |
| auto-restore 자체 throw | catastrophic | 추가 토스트 안 띄움 (이미 error 토스트), `Logger.Error` 만 |

### 8.2 토스트 매핑

| Apply 결과 | doAutoBackup | 자동복원 시도 결과 | 토스트 (1회) | 추가 로그 |
|---|---|---|---|---|
| 정상 (warn 포함) | — | (시도 안 함) | `ToastApplyOk` | Info |
| top-level throw | true | OK | `ToastErrApplyAutoRestored` | Error (apply) + Info (restore) |
| top-level throw | true | fail | `ToastErrApplyAutoRestored` | Error (apply) + Error (restore) |
| top-level throw | false | (시도 안 함) | `ToastErrApplyNoBackup` | Error |
| HasFatalError | true | OK | `ToastErrApplyAutoRestored` | Warn (step) + Info (restore) |
| HasFatalError | true | fail | `ToastErrApplyAutoRestored` | Warn (step) + Error (restore) |
| HasFatalError | false | (시도 안 함) | `ToastErrApplyNoBackup` | Warn (step) |

**원칙**: 한 Apply 호출당 토스트는 정확히 1개. 자동복원의 성패는 사용자에게 명시적 토스트로 전달하지 않고 (§8.1 의 "auto-restore 실패는 추가 토스트 안 띄움" 정책 일관) BepInEx 로그로만. 사용자가 추가 디버깅 필요하면 로그 확인.

### 8.3 KoreanStrings 신규/변경

```csharp
// 신규 / 변경
public const string ApplyBtn                     = "▼ 현재 플레이어로 덮어쓰기";    // (v0.x 예정) 제거
public const string RestoreBtn                   = "↶ 자동백업으로 복원";            // (v0.x 예정) 제거
public const string ConfirmTitleRestore          = "↶ 자동백업 복원 확인";
public const string ConfirmRestoreMain           = "Apply 직전 상태로 되돌립니다.";
public const string Restore                      = "복원";
public const string AutoBackupCheckbox           = "Apply 직전 자동백업 (슬롯 0)";

public const string ToastApplyOk                 = "✓ 슬롯 {0} 적용됨 ({1}개 필드, {2}개 미지원)";
public const string ToastErrNoPlayer             = "✘ 게임 안 진입 후 시도";
public const string ToastApplyDisabled           = "✘ Apply 가 설정에서 비활성됨";
public const string ToastErrAutoBackup           = "✘ 자동백업 실패: {0}";
public const string ToastErrSlotRead             = "✘ 슬롯 {0} 읽기 실패: {1}";
public const string ToastErrApplyAutoRestored    = "✘ 적용 실패: {0}. 자동복원 시도됨 (로그 확인)";
public const string ToastErrApplyNoBackup        = "✘ 적용 실패: {0}. 자동백업 비활성 — 수동 복구";
public const string ToastErrEmptySlot            = "✘ 슬롯이 비어 있습니다";
public const string ToastErrNoBackup             = "✘ 자동백업이 없습니다";
```

기존 `ToastErrApply` 는 위의 세분화된 메시지로 대체.

### 8.4 Logger 정책

- **Info**: Apply 의 시작/종료, 카운트 요약, dump 결과, auto-restore 결과
- **Debug**: 각 field/entry 의 applied/skipped/warned 상세 (운영 시 noise 가능 — `BepInEx.cfg` 의 LogLevel 로 제어)
- **Warn**: step 의 throw, individual method missing
- **Error**: top-level throw, auto-restore 실패, fatal step

---

## 9. ConfigEntries

| Key | Default | 의미 | 변경 |
|---|---|---|---|
| `AllowApplyToGame` | `true` | Apply 자체 kill switch. dump phase 에서 false 권장 | **신규** |
| `AutoBackupBeforeApply` | `true` | DoApply 시 슬롯 0 자동백업 + 실패 시 자동복원 | 유지 |
| `RunPinpointPatchOnApply` | — | v0.1 의 잔재. PinpointPatcher 가 Apply 자체이므로 의미 없음 | **제거** |
| 기타 (PauseGameWhileOpen, 위치 영속 등) | — | v0.2 그대로 | 유지 |

---

## 10. UI Changes

### 10.1 SlotDetailPanel.cs

- 슬롯 1~20 선택 시: `ApplyBtn` 정상 활성, `(v0.x 예정)` 라벨 제거. 클릭 → `OnApplyRequested(slot)`.
- 슬롯 0 선택 시: `RestoreBtn` 정상 활성, `(v0.x 예정)` 라벨 제거. 클릭 → `OnRestoreRequested()`.
- 슬롯이 비어 있을 때: 두 버튼 모두 disable. 클릭 자체가 안 되므로 별도 안내 불필요 (§4.3 IL2CPP IMGUI strip 영향으로 tooltip 미사용 — v0.2 패턴 유지)
- `OnApplyRequested` / `OnRestoreRequested` 콜백 위임은 v0.2 의 `OnRenameRequested` / `OnCommentRequested` 패턴 그대로

### 10.2 ModWindow.cs

- v0.2 의 `b3e300d` 에서 제거된 `RequestApply` / `DoApply` 코드를 PinpointPatcher 호출 버전으로 다시 추가
- 신규: `RequestRestore`, `AttemptAutoRestore` (private)
- `_detail.OnApplyRequested = RequestApply`, `_detail.OnRestoreRequested = RequestRestore` 와이어링
- `Awake`/`Start` 의 키 핸들러: `[F12]` → `Core.HeroDataDump.DumpToLog()` (plan Task 1, release 전 제거)

### 10.3 KoreanStrings 정리

§8.3 의 신규/변경 적용. 기존 `ToastErrApply` 사용처는 세분화된 메시지로 대체.

---

## 11. Testing

### 11.1 신규 unit tests (PinpointPatcher framework)

`LongYinRoster.Tests/PinpointPatcherTests.cs` (신규):

| 테스트 | 검증 |
|---|---|
| `ApplyResult_StartsEmpty` | 새 ApplyResult 의 모든 list / fatal 이 비어 있음 |
| `ApplyResult_TracksAppliedSkippedWarned` | 카운트 + 명시 동작 |
| `IL2CppListOps_ClearsStandardList` | `List<int>` 같은 .NET list reflection clear (가짜 IL2CPP — type 이름만 다름) |
| `IL2CppListOps_CountReturnsItemCount` | 동상 |
| `IL2CppListOps_GetReturnsItemAt` | 동상 |
| `SimpleFieldMatrix_Schema_Frozen` | matrix entry 의 (jsonName, propertyName, setterName) shape 가 expected schema 따름. dump 결과 변경 회귀 감지 |

→ 기존 18 + 신규 6 = **24/24 PASS**

### 11.2 Smoke checklist (게임 안 검증)

`docs/superpowers/specs/2026-04-29-v0.3-smoke.md` 에 체크리스트 + 결과 기록.

**Phase A — Dump 검증** (plan Task 1 직후)
- [ ] A1. `[F12]` 누르면 `BepInEx/LogOutput.log` 에 `HeroDataDump:` prefix 의 method list 출력
- [ ] A2. dump 에 `RefreshMaxAttriAndSkill`, `GetMaxAttri` 등 §1 단서 method 가 포함됨
- [ ] A3. manager candidate dump 에 `RefreshHero` / `UpdateHero` / `OnHeroChanged` 같은 method 가진 매니저 1+ 발견

**Phase B — PinpointPatcher 단계별 검증** (각 step 구현 후)
- [ ] B1. SetSimpleFields: fame / hp / favor / 천부 포인트 변경 → capture → 변경 되돌림 → Apply → game UI 에 원복 표시 (heroName 등 정체성은 ⚪ — skip)
- [ ] B2. RebuildKungfuSkills: **v0.4 후보 — collection ⚪. v0.3 placeholder skip 만 검증** (Apply 결과 `SkippedFields` 에 `kungfuSkills — collection rebuild deferred to v0.4` 한 row 출력 확인)
- [ ] B3. RebuildItemList: **v0.4 후보 — collection ⚪. v0.3 placeholder skip 만 검증** (`itemListData.allItem — collection rebuild deferred to v0.4` 한 row 확인)
- [ ] B4. RebuildSelfStorage: **v0.4 후보 — collection ⚪. v0.3 placeholder skip 만 검증** (`selfStorage.allItem — collection rebuild deferred to v0.4` 한 row 확인)
- [ ] B5. RebuildHeroTagData: 🟡 — 천부 변경 → capture → reset → Apply → 천부 동일 (`AddTag` 5-arg)
- [ ] B6. RefreshSelfState: 3 method (`RefreshMaxAttriAndSkill` / `RefreshHeroSalaryAndPopulation` / `RecoverState`) 모두 호출 됨 + B1 후 stat 갱신
- [ ] B7. RefreshExternalManagers: `BigMapController.RefreshBigMapNPC` 1 호출 — 지도 NPC 표시 갱신

**Phase C — 통합**
- [ ] C1. 슬롯 1 캡처 → **stat 위주 변경** (fame, hp, fightScore, baseAttri 같은 numeric) → Apply → 종합 일치. 무공 / 인벤토리 변경은 v0.4 검증 — Apply 후에도 변경 안 됨 이 정상 (skipped 카운트 ≥3)
- [ ] C2. **save → reload → 정보창 정상** (v0.2 시도 2 의 실패점)
- [ ] C3. 보존 필드 검증 — Apply 후 force/location/relations 가 Apply 직전 값 유지
- [ ] C4. Restore (slot 0): B1 직후 Apply 실수 가정 → Restore → B1 직전 상태 복귀
- [ ] C5. 자동복원 트리거 — 의도적으로 PinpointPatcher 안 throw 하도록 일시 코드 → Apply → 자동복원 → game state 가 Apply 직전
- [ ] C6. Config kill switch — `AllowApplyToGame=false` → Apply 클릭 → 토스트 disable + game state 변경 0
- [ ] C7. 미지원 필드 토스트 — Apply 결과 토스트에 "X개 미지원" 카운트 + 로그에 field name

**Phase D — Edge**
- [ ] D1. slot 비어있을 때 Apply 버튼 — disable 또는 토스트
- [ ] D2. game 진입 전 Apply — 토스트 "게임 안 진입 후 시도", 변경 0
- [ ] D3. 슬롯 1~20 의 Apply 후 슬롯 0 의 자동백업이 직전 상태 보존 — 슬롯 0 캐릭터명 검증

### 11.3 회귀 게이트

v0.3 release 직전 (plan 마지막 task):
- `dotnet test` → 24/24 pass
- Phase A/B/C/D smoke checklist 전부 [x]
- BepInEx 로그에 unhandled exception 0 (smoke 동안)
- `Core/HeroDataDump.cs` 와 `[F12]` 핸들러 제거 확인

---

## 12. Out of Scope (v0.4+)

§2.2 의 N1~N10 + 다음:

- **§7.2 매트릭스의 ⚪ 항목 활성화** — 무공 / 장비 / 인벤토리 / 정체성 (heroName, age 등) 의 primitive-factory 또는 Harmony 우회 추가. dump 추가 round 필요. KungfuSkillLvData / ItemData wrapper ctor 후보 enumerate 또는 Harmony patch 로 game 의 GetItem(ItemData,...) 같은 wrapper-인자 method 를 primitive-factory 로 변환.
- **§7 매트릭스의 ⚪ 항목 승격** — dump 후에도 method 못 찾은 필드. v0.4 에서 다른 namespace / nested manager 추가 dump 또는 Harmony patch 우회.
- **Sprite / portrait 명시적 reload** — RefreshExternalManagers 가 잡길 기대하지만 못 잡으면 v0.4 에서 sprite manager API 명시 추가.
- **Detail panel 의 "마지막 Apply 결과" 섹션** — 토스트 + 로그 외 visibility, v0.4.
- **Apply preview (dry-run)** — PinpointPatcher 의 dry-run mode 분기. v0.5+.
- **필드 단위 selective Apply (cherry-pick)** — SlotDetailPanel 의 체크박스 매트릭스. v0.5+.
- **자동 smoke harness** — IL2CPP 안 deterministic 게임 상태 변경. v0.4 검토.
- **Reverse export (game → 다른 player save)** — 별도 모드로 분리 권장.

### v0.4 진행 상태

v0.4.0 출시 완료 (2026-04-30). 위 항목들의 실제 처리 결과:

| §12 항목 | v0.4 결과 |
|---|---|
| §7.2 매트릭스 ⚪ — **정체성** (heroName / nickname / age 등) | **v0.4 에서 활성화** — setter direct path (commit eaf2938 chain). PoC A2 PASS. save → reload PASS |
| §7.2 매트릭스 ⚪ — **무공 active** | **v0.5+ 로 재deferred** — PoC A3 FAIL. wrapper.lv vs nowActiveSkill ID semantic mismatch |
| §7.2 매트릭스 ⚪ — **인벤토리 / 창고** | **v0.5+ 로 재deferred** — PoC A4 FAIL. sub-data wrapper graph 미해결 |
| §7.2 매트릭스 ⚪ — **무공 list** | **v0.5+ 후보** — KungfuSkillLvData wrapper ctor IL2CPP 한계. v0.4 에서 미도전 |
| Detail panel 의 "마지막 Apply 결과" 섹션 | v0.4 에서 다루지 않음. v0.5+ 유지 |
| **필드 단위 selective Apply** (체크박스 매트릭스) | **v0.4 에서 카테고리 단위로 활성화** — 9-카테고리 체크박스 (스탯/명예/천부/스킨/자기집 add/정체성/무공 active/인벤토리/창고). 슬롯별 `_meta.applySelection` 즉시 저장. 필드 단위 cherry-pick 은 여전히 v0.5+ 후보 |
| Sprite / portrait 명시적 reload | v0.4 에서 다루지 않음. v0.5+ 유지 |
| 자동 smoke harness | v0.4 에서 미구현. v0.5+ 검토 |

---

## 13. Open Questions (dump 결과 후 확정)

- **Q1**. SimpleFieldMatrix 의 정확한 method 매핑 — 어느 필드가 `SetX`, 어느 필드가 `ChangeX(delta)`, 어느 필드가 `SetXxx(value, ...)` 다인자 시그니처
- **Q2**. collection 의 AddXxx method 시그니처 — `AddKungfuSkill(id, lv, exp, equipped)` 가 단일 시그니처인지 overload 가 있는지
- **Q3**. RefreshExternalManagers 의 매니저 list — 어떤 manager 가 RefreshHero(player) 같은 API 노출하는지
- **Q4**. `금전` (금전 시스템) 이 HeroData 안 필드인지 GameDataController 의 별도 필드인지 — 후자라면 매트릭스에서 제외 또는 보존 필드 분류 검토

위 질문들의 답은 plan Task 1 (HeroDataDump) 의 산출물 + plan Task 2 (매트릭스 보강) 에서 확정.

---

## 14. Migration & Compatibility

### 14.1 슬롯 schema

v0.2 슬롯 (raw JSON Player + System.Text.Json `_meta`) 그대로 유지. v0.3 가 새 필드 추가 안 함, 기존 사용자의 슬롯 디렉터리 무손실.

### 14.2 ConfigEntry migration

기존 `RunPinpointPatchOnApply` 키는 BepInEx config 에서 ignore (자동 정리되거나 무시됨 — 우리 코드는 더 이상 읽지 않음). 신규 `AllowApplyToGame` 은 default `true` 라 기존 사용자가 즉시 Apply 사용 가능.

### 14.3 Release 절차

1. plan 마지막 task 완료 후 README / HANDOFF 갱신
2. `dist/LongYinRoster_v0.3.0/` 폴더 + zip
3. git tag `v0.3.0`, push
4. GitHub release with release notes
5. HANDOFF "다음 세션" 영역을 v0.4 로 update (또는 "v0.3 출시 완료, 후속 없음" 으로 닫기)

---

## Appendix A — Decision Log (이 brainstorm 의 5 결정)

| # | 결정 | 선택 | 근거 |
|---|---|---|---|
| 1 | Spec 작성 시점 | (B) framework + 가설, plan Task 1 = dump | 한 세션에 spec/plan, dump 후 보강 round 로 evidence-based 정정 |
| 2 | Apply 실패 fallback | (C)+(A) escape hatch 거부 + 매트릭스 명시 + 슬롯 0 자동복원 | game state 오염 risk 거부, expectation management 명시 |
| 3 | PinpointPatcher 책임 범위 | (B) HeroData self + Hero-related manager Refresh API | (A) 는 sprite lazy load 못 잡음, (C) 는 god class 위험 |
| 4 | Restore vs Apply 관계 | (A) 동일 코드 path, source 만 다름 | 자동백업 시점이 Apply 직전이라 보존 필드 차이 거의 없음, 두 path 유지비 회피 |
| 5 | Collection 처리 패턴 | (B) Clear=raw reflection, Add=game method | (A) 는 ClearXxx 못 찾을 위험, (C) 는 §1.2 시도 2 재현. (B) 는 Add 의 side-effect 살아 있음 |

---

## Appendix B — Files to Add / Modify / Remove

### Add
- `src/LongYinRoster/Core/PinpointPatcher.cs` (재작성, no-op 폐기)
- `src/LongYinRoster/Core/HeroDataDump.cs` (임시, release 전 제거)
- `src/LongYinRoster/Core/IL2CppListOps.cs` (재사용 reflection helpers)
- `src/LongYinRoster/Core/ApplyResult.cs` (POCO)
- `src/LongYinRoster/Core/SimpleFieldMatrix.cs` (static readonly 매트릭스)
- `src/LongYinRoster.Tests/PinpointPatcherTests.cs` (6 tests)
- `docs/HeroData-methods.md` (dump 산출물)
- `docs/superpowers/specs/2026-04-29-v0.3-smoke.md` (smoke checklist)

### Modify
- `src/LongYinRoster/UI/ModWindow.cs` (RequestApply / DoApply / RequestRestore / AttemptAutoRestore 추가)
- `src/LongYinRoster/UI/SlotDetailPanel.cs` (Apply / Restore 버튼 활성)
- `src/LongYinRoster/Util/KoreanStrings.cs` (§8.3 신규)
- `src/LongYinRoster/Config.cs` (AllowApplyToGame 신규, RunPinpointPatchOnApply 제거)

### Remove (release 전)
- `src/LongYinRoster/Core/HeroDataDump.cs`
- `ModWindow` 의 `[F12]` 핸들러
