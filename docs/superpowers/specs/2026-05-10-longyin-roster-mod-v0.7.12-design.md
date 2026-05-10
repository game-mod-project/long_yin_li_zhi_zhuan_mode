# LongYinRoster v0.7.12 — Cat 3 deferred (Undo + toast 강화)

**일시**: 2026-05-10
**baseline**: v0.7.11 (394 tests + ContainerPanel UX overhaul)
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §G5 — Cat 3 deferred 진입

**brainstorm 결과 (2026-05-10)**:
- Q1 = **A** Full Undo 양방향 (모든 6 op + Delete)
- Q2 = **A** Single op stack (마지막 1개만)
- Q3 = **A** Toast 표준화 (모든 op format 일관 + Reason)
- Q4 = **A** Toolbar [↶ Undo] button (can-undo 시 enabled)

## 0. 한 줄 요약

**v0.7.11 release 직후 deferred 였던 Cat 3C (Undo) + 3D (toast 강화) 구현**. ContainerOps callback 패턴에 OpRecord snapshot 추가, ModWindow.Do* 메서드가 record on success, ContainerPanel toolbar 의 [↶ Undo] button 이 ModWindow 의 PerformUndo handler 호출. Single op stack — 직전 1 op 만 reversible.

## 1. 디자인

### 1.1 OpRecord 구조 (`Containers/ContainerOpUndo.cs` 신규)

```csharp
public enum OpKind {
    GameToContainerMove,    // 인벤/창고 → 컨테이너 (Move)
    GameToContainerCopy,    // 인벤/창고 → 컨테이너 (Copy)
    ContainerToInvMove,     // 컨테이너 → 인벤 (Move)
    ContainerToInvCopy,
    ContainerToStoMove,     // 컨테이너 → 창고 (Move)
    ContainerToStoCopy,
    ContainerDelete,        // 컨테이너 안 item 삭제
}

public sealed class OpRecord
{
    public OpKind Kind;
    public int    ContainerIdx;
    public string ContainerJsonBefore;     // 항상 — Container 측 restore source
    public string AddedItemsJson;          // Move undo 용 — Source 측에 다시 add 할 item
    public string GameSourceField;         // "itemListData" / "selfStorage" / "" — Game 측 source 필드명
    public int    AddedCountToGame;        // Copy undo 용 — Game 측에 추가된 item count (마지막 N 제거)
    public string Description;             // 사용자 표시용 (e.g., "이동 5개 from 창고")
    public DateTime Timestamp;
}

public static class ContainerOpUndo
{
    private static OpRecord? _last;
    public static void Record(OpRecord op) => _last = op;
    public static OpRecord? Pop() { var t = _last; _last = null; return t; }
    public static OpRecord? Peek() => _last;
    public static bool CanUndo => _last != null;
    public static void Clear() => _last = null;
}
```

### 1.2 ModWindow.Do* 메서드 — snapshot + record

기존 `DoGameToContainer / DoContainerToInventory / DoContainerToStorage / DoDeleteContainerItems` 가 직접 ContainerOpsHelper 호출. 본 v0.7.12 변경:

**Pre-op**: `ContainerJsonBefore = _repo.LoadItemsJson(idx)` (always)
**Post-op (success)**:
```csharp
ContainerOpUndo.Record(new OpRecord {
    Kind = OpKind.GameToContainerMove,
    ContainerIdx = idx,
    ContainerJsonBefore = jsonBefore,
    AddedItemsJson = extracted,           // game→container Move 의 경우 재 add 용
    GameSourceField = "itemListData",
    AddedCountToGame = 0,                  // Copy 만 ≥1
    Description = $"이동 {r.Succeeded}개 from 인벤토리",
    Timestamp = DateTime.Now,
});
```

### 1.3 PerformUndo handler (ModWindow 신규)

```csharp
private void PerformUndo()
{
    var op = ContainerOpUndo.Pop();
    if (op == null) { _containerPanel.Toast("Undo 가능한 작업 없음"); return; }
    try
    {
        // 1. Container restore (always)
        if (!string.IsNullOrEmpty(op.ContainerJsonBefore))
            _repo.SaveItemsJson(op.ContainerIdx, op.ContainerJsonBefore);

        // 2. Game side restore — Kind 별 분기
        switch (op.Kind)
        {
            case OpKind.GameToContainerMove:
                // game 에서 제거된 item 들을 다시 add
                var sourceList = ResolveGameList(op.GameSourceField);
                if (sourceList != null && !string.IsNullOrEmpty(op.AddedItemsJson))
                    ContainerOps.AddItemsJsonToGame(player, op.AddedItemsJson, /*maxWeight=*/9999f, allowOvercap: true, op.GameSourceField);
                break;

            case OpKind.GameToContainerCopy:
            case OpKind.ContainerDelete:
                // Container 만 변경 — Game 측 unchanged. 추가 작업 없음.
                break;

            case OpKind.ContainerToInvMove:
            case OpKind.ContainerToInvCopy:
            case OpKind.ContainerToStoMove:
            case OpKind.ContainerToStoCopy:
                // game 의 마지막 N 개 (방금 추가된 item) 제거
                if (op.AddedCountToGame > 0)
                {
                    var targetList = ResolveGameList(op.GameSourceField);  // here SourceField stores target
                    if (targetList != null)
                    {
                        int count = ContainerOps.GetGameListCount(targetList);
                        var lastIndices = new HashSet<int>();
                        for (int i = count - op.AddedCountToGame; i < count; i++) lastIndices.Add(i);
                        ContainerOps.RemoveGameItems(targetList, lastIndices);
                    }
                }
                break;
        }
        _containerPanel.Toast($"↶ Undo: {op.Description}");
        _containerPanel.RefreshContainerData();
    }
    catch (Exception ex)
    {
        _containerPanel.Toast($"Undo 실패: {ex.Message}");
    }
}
```

### 1.4 ContainerPanel — [↶ Undo] button + callback

ContainerPanel 의 글로벌 toolbar 끝 (착용중 제외 toggle 다음, 결과 카운터 앞) 에 button 추가:

```csharp
GUILayout.Space(8);
GUI.enabled = ContainerOpUndo.CanUndo;
var undoColor = GUI.color;
if (GUI.enabled) GUI.color = new Color(1.0f, 0.9f, 0.5f);  // 노랑 강조 (warning-ish)
if (GUILayout.Button("↶ Undo", GUILayout.Width(80)))
{
    OnUndoRequested?.Invoke();    // ModWindow 가 wire-up
}
GUI.color = undoColor;
GUI.enabled = true;
```

신규 callback `Action? OnUndoRequested` (ContainerPanel public field). ModWindow 에서:
```csharp
_containerPanel.OnUndoRequested = PerformUndo;
```

### 1.5 Toast 표준화 (3D)

기존 ModWindow 의 Toast 호출 site (총 ~6 개) 를 일관 format 으로 통일:

```csharp
private void ToastResult(string opLabel, ContainerOpsHelper.Result r)
{
    var kind = r.Failed > 0 ? ToastKind.Warning : ToastKind.Info;
    string msg = $"{opLabel}: {r.Succeeded}개 성공";
    if (r.Failed > 0) msg += $" / {r.Failed}개 실패";
    if (!string.IsNullOrEmpty(r.Reason)) msg += $" — {r.Reason}";
    if (r.OverCapWeight > 0) msg += $" (over-cap {r.OverCapWeight:F1}kg)";
    ToastService.Push(msg, kind);
}
```

기존 분산된 Toast 호출 site 를 `ToastResult("이동", r)` 같은 단일 method 로 통일.

## 2. 신규 자산 (LOC 추정)

| File | 변경 | LOC |
|---|---|---|
| `src/LongYinRoster/Containers/ContainerOpUndo.cs` | 신규 — OpRecord + OpKind + ContainerOpUndo static | ~80 |
| `src/LongYinRoster/Containers/ContainerOps.cs` | 수정 — `GetGameListCount(il2List)` helper 추가 | +~20 |
| `src/LongYinRoster/UI/ModWindow.cs` | 수정 — Do* 메서드에 snapshot + record. PerformUndo. ResolveGameList. ToastResult helper | +~150 |
| `src/LongYinRoster/UI/ContainerPanel.cs` | 수정 — OnUndoRequested callback + toolbar [↶ Undo] button | +~30 |
| `src/LongYinRoster.Tests/ContainerOpUndoTests.cs` | 신규 — Record/Pop/CanUndo 검증 + OpRecord 직렬화 검증 | ~50, +5 tests |

총 신규 LOC ≈ 280 + 50 tests.

## 3. Risk

1. **Move undo 의 game 측 add** — `AddItemsJsonToGame` 의 maxWeight = 9999f (인위적 무한대) — Move 직전 상태 복원이라 무게 cap 검증 skip. 만약 game state 가 변경되어 cap 도 달라졌으면 incorrect. 사용자 시나리오 = 즉시 Undo (single stack) 라 가정 → 영향 작음.
2. **Copy undo 의 마지막 N 제거** — `count - addedCount ~ count - 1` indices 가 정말 방금 추가된 item 인지 보장 어려움. 사용자가 즉시 Undo 안 하고 다른 op 후 Undo 시 mismatch 가능. 단일 stack 으로 mitigated (다른 op 시 _last 가 새 OpRecord 로 덮어씀).
3. **PerformUndo 도중 game state 가 외부 mod 또는 게임 자체에 의해 변경**: race condition. 영향 작음 (사용자가 즉시 Undo 안 누르는 경우 stale).
4. **Toast 강화의 KoreanStrings 의존** — 일부 기존 toast 가 KoreanStrings 의 `Toast{Inv,Sto}{Ok,Partial,Full,Overcap}` format 사용. 표준화 후에도 호환 유지 (기존 string 그대로 활용 또는 명시적 변경).

## 4. Phase 작업 순서

| Phase | 항목 |
|---|---|
| 1 | ContainerOpUndo 신규 + OpRecord + tests |
| 2 | ContainerOps.GetGameListCount helper |
| 3 | ModWindow.ToastResult helper + 기존 Toast 호출 통합 |
| 4 | ModWindow.Do* 메서드 snapshot + Record 통합 |
| 5 | ModWindow.PerformUndo + ResolveGameList |
| 6 | ContainerPanel.OnUndoRequested + toolbar [↶ Undo] button |
| 7 | 인게임 smoke + release |

## 5. 미반영

- **Deep stack** — 사용자가 multiple Undo 원하면 v0.7.13+ 후속
- **Redo** — Q4 미언급, defer
- **Container Delete (file)** — v0.7.11 5A confirm dialog 가 대체 안전망 — Undo 미지원 유지

---

**spec END.**
