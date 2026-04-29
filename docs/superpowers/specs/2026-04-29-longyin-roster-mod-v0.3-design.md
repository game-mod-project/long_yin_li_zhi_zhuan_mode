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

- **G1**. Apply (slot → game) 흐름 정상 작동 — 캡처 직후 의도적 변경 후 Apply 하면 캡처 시점 상태로 복귀
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

### 4.4 Step 6 — RefreshSelfState (fatal step)

HeroData self-method 호출. `HANDOFF §4.4` 의 단서 method + dump 추가:

```csharp
TryInvoke(player, "RefreshMaxAttriAndSkill");
TryInvoke(player, "GetMaxAttri");
TryInvoke(player, "GetMaxFightSkill");
TryInvoke(player, "GetMaxLivingSkill");
TryInvoke(player, "GetMaxFavor");
TryInvoke(player, "GetFinalTravelSpeed");
// dump 결과로 추가될 RefreshXxx / RecalcXxx
```

**fatal=true 이유**: 이 step 자체가 throw 하면 stat / skill 가 stale 인 채로 game 진행 → save→reload 시 NRE 위험. step 자체 throw 는 자동복원 트리거. 단, 일부 method 가 missing 이라서 individual try/catch 가 warn 으로만 기록되는 건 OK (fatal 아님).

### 4.5 Step 7 — RefreshExternalManagers

dump 결과로 식별된 Hero-관련 매니저들의 `RefreshHero(player)` / `UpdateHero(player)` / `OnHeroChanged(player)` 호출:

```csharp
foreach (var mgr in HeroRelatedManagers)
    TryInvoke(mgr.Instance, mgr.Method, player);
```

`HeroRelatedManagers` 는 `(매니저타입, 메서드명, 인자스타일)` 의 리스트. `static readonly` + dump 후 보강.

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

### 7.2 카테고리별 매트릭스 (spec 시점 가설, dump 후 보강)

| 카테고리 | 필드 (예) | 가설 | 메모 |
|---|---|---|---|
| 정체성 | heroName, nickname, gender, age | 🟢 / 🟡 | dump 결과로 확정 |
| 외형 | bodyData, faceData, portraitID | 🟡 / ⚪ | sprite reference 는 lazy load — RefreshExternalManagers 의존 |
| 스탯 base | baseAttri.* | 🟢 / 🟡 | RefreshMaxAttriAndSkill 으로 totalAttri 재계산 기대 |
| 스탯 derived | totalAttri, fightScore, hp, maxhp | 🟢 (refresh 후 자동) | 직접 set 안 하고 refresh 가 계산 |
| 무공 | kungfuSkills (id, lv, exp, equipped) | 🟡 | step 2 — Clear=raw + AddKungfuSkillMethod |
| 인벤토리 | itemListData.allItem | 🟡 | step 3 |
| 창고 | selfStorage.allItem | 🟡 | step 4 |
| 천부 | heroTagData, heroTagPoint | 🟡 | step 5 |
| 명예/금전 | fame, 금전 (정확한 필드명은 dump) | 🟢 / 🟡 | simple value |
| 보존 | force / location / relations | ⛔ | StripForApply 가 미리 제거 |
| Misc 시스템 | 호감도(favor), 명성(reputation) | ⚪ → 🟡 | dump 후 결정 |

### 7.3 Dump 후 spec 보강 절차

plan Task 1 종료 후:
1. `docs/HeroData-methods.md` 작성
2. 본 문서의 §7.2 매트릭스 → 가설 → dump-evidenced 로 변경하는 commit `docs(spec): v0.3 support matrix refined per dump`
3. 매트릭스 변경 내용을 사용자에게 요약 제시 → "보강 매트릭스 OK" 승인
4. plan Task 2~ (PinpointPatcher 본 구현) 진입

이 보강 round 가 spec 의 두 번째 commit. 매트릭스가 실측 기반으로 재구성되면서 일부 필드는 ⚪ → v0.4, 일부는 가설 🟢 → 🟡 (또는 그 반대) 로 이동.

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
- [ ] B1. SetSimpleFields: heroName / fame / fightScore 변경 → capture → 변경 되돌림 → Apply → game UI 에 원복 표시
- [ ] B2. RebuildKungfuSkills: 무공 학습 → capture → 무공 reset → Apply → 무공 list 동일
- [ ] B3. RebuildItemList: 아이템 추가 → capture → 아이템 삭제 → Apply → 인벤토리 동일
- [ ] B4. RebuildSelfStorage: 동상 (창고)
- [ ] B5. RebuildHeroTagData: 천부 변경 → capture → reset → Apply → 천부 동일
- [ ] B6. RefreshSelfState: B1~B5 후 stat (fightScore, maxhp) 가 stale 가 아닌 게임 계산값
- [ ] B7. RefreshExternalManagers: B1~B5 후 포트레이트 / 영웅 아이콘 / town panel 정상 표시

**Phase C — 통합**
- [ ] C1. 슬롯 1 캡처 → 의도적 큰 변경 → Apply → 종합 일치
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

§2.2 의 N1~N9 + 다음:

- **§7 매트릭스의 ⚪ 항목 승격** — dump 후에도 method 못 찾은 필드. v0.4 에서 다른 namespace / nested manager 추가 dump 또는 Harmony patch 우회.
- **Sprite / portrait 명시적 reload** — RefreshExternalManagers 가 잡길 기대하지만 못 잡으면 v0.4 에서 sprite manager API 명시 추가.
- **Detail panel 의 "마지막 Apply 결과" 섹션** — 토스트 + 로그 외 visibility, v0.4.
- **Apply preview (dry-run)** — PinpointPatcher 의 dry-run mode 분기. v0.5+.
- **필드 단위 selective Apply (cherry-pick)** — SlotDetailPanel 의 체크박스 매트릭스. v0.5+.
- **자동 smoke harness** — IL2CPP 안 deterministic 게임 상태 변경. v0.4 검토.
- **Reverse export (game → 다른 player save)** — 별도 모드로 분리 권장.

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
