# LongYinRoster v0.7.11 — ContainerPanel UX overhaul

**일시**: 2026-05-10
**baseline**: v0.7.10.2 (374 tests + 사용자 smoke PASS)
**메타 로드맵**: [`2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md`](2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §G4 결정 — ContainerPanel UX overhaul

**brainstorm 결과 (2026-05-10)**:
- 카테고리 10개 순차 검토 → 6 카테고리 채택 (1, 2, 3, 4, 5, 9)
- Sub-project 분할 = **β** — v0.7.11 = UX overhaul (sprite 제외) / v0.8 = IL2CPP sprite spike (별도 cycle)
- 사용자 채택 항목: 20+ items (~1500-2000 LOC 추정)
- v0.7.11 원안 (G4 후보 NPC dropdown) → **v0.7.12 이후로 defer** (사용자가 ContainerPanel UX 를 우선시)

## 0. 한 줄 요약

**v0.7.11 = ContainerPanel 전반 UX 개선** — 6 영역 (레이아웃 / 일괄선택 / Undo 안전망 / filter 정밀화 / 컨테이너 관리 safety / panel resize) 의 incremental 개선. **신규 IMGUI 자산 0** (모두 v0.7.6+ 검증된 strip-safe API). v0.7.10.2 baseline 회귀 없이 추가 기능만.

## 1. 디자인 결정 (카테고리별)

### 1.1 Category 1 — 레이아웃/공간 효율 (1A + 1B)

**1B — 인벤/창고 collapse 토글**: 각 list 헤더에 `[▼/▲]` toggle. 접힘 시 라벨 + 무게만 표시 (~28 height), 다른 list 가 영역 모두 차지.

**1A — Split 4-preset 토글**: 좌측 column 헤더에 `[50:50] [70:30] [30:70] [100:0]` cycle button. 인벤/창고 height 비율 조정. ConfigEntry `ContainerSplitPreset` (int, 0~3, default 0=50:50) 영속.

```
좌측 column (390 width)
┌─ 인벤토리 (180개, 50.0/964.0kg) [▼]                ┐
│ row 1                                              │
│ row 2  (split preset 50:50 → 220h, 70:30 → 350h)  │
│ ...                                                │
│ [→이동] [→복사]                                     │
├──────────────────────────────────────────────────┤
│ 창고 (50개, 200.5/300.0kg) [▼]                    │
│ row 1                                             │
│ ...                                               │
│ [→이동] [→복사]                                    │
└──────────────────────────────────────────────────┘
[비율 50:50 ▼]   ← split preset cycle button (column 하단)
```

ConfigEntry:
- `ContainerInventoryCollapsed` (bool, default false)
- `ContainerStorageCollapsed` (bool, default false)
- `ContainerSplitPreset` (int 0~3, default 0)

### 1.2 Category 2 — 선택/일괄 조작 (2A + 2B + 2C + 2H)

**2A — 전체선택/해제/반전 button**: 각 list 헤더 우측에 `[☑] [☐] [↺]` 3 button (32 width each). 현재 visible (search/filter 적용 후) row 대상.

**2B — 선택 카운터 + 무게 합계**: 각 list 라벨 끝에 `(선택: 3 / 180개, 24.5/50.0kg)`. 선택 0 시 `(180개, 50.0kg)` 기본 형식 유지.

**2C — 등급별 일괄 button**: 각 list 헤더 우측에 dropdown `[등급 ≥ ▼]` — 클릭 시 cycle (전체→열악→보통→정량→비전→정극→절세→전체...). 선택 시 그 list 의 visible row 중 등급 ≥ 선택 등급 모두 체크. v0.7.10.1 등급 sort 자산 (SkillNameCache.GetRareLv) + ItemReflector.GetRareLv 재사용.

**2H — 착용중 제외 filter**: 글로벌 toolbar 끝에 `[☐ 착용중 제외]` 토글. ON 시 `equiped == true` row 자동 visible 제외. ConfigEntry `ContainerExcludeEquipped` (bool, default false).

### 1.3 Category 3 — 이동/복사 효율 (3C + 3D + 3G)

**3C — Undo button (단일 stack)**: 글로벌 toolbar 또는 좌/우 column 사이에 `[↶ Undo]` button. 직전 ContainerOp (Move/Copy/Delete) 의 inverse 실행.

**Undo stack 구조**:
```csharp
// src/LongYinRoster/Containers/ContainerOpUndo.cs (신규)
public sealed record OpRecord(
    OpKind Kind,        // Move / Copy / Delete / Clone
    ContainerArea Source,
    ContainerArea Target,
    int? SourceContainerIdx,
    int? TargetContainerIdx,
    List<int> AffectedItemIDs,  // 재구성 위해 보존
    DateTime Timestamp);

public enum OpKind { Move, Copy, Delete, Clone }
```

`_lastOp` (single OpRecord, no deep stack — KISS). `[Undo]` 클릭 시 inverse:
- Move → reverse Move (target → source)
- Copy → Delete on target (복사본 제거)
- Delete → Restore (백업 source 에서 복원 — 별도 백업 stack 필요)
- Clone → Delete cloned container

**Delete Undo limitation**: 컨테이너 file 자체 삭제는 OS-level 라 복원 어려움. **Delete Undo 는 v0.7.11 에서 미지원** (5A confirm dialog 가 대체 안전망). Move/Copy/Clone 만 Undo.

**3D — toast 강화**: 현재 단순 "5개 이동" → 상세 `"5개 이동 (1개 실패: 창고 무게 초과)"`. ContainerOps 의 result 가 이미 success/fail 카운트 + 사유 보유 — toast format 만 강화.

**3G — button 강조 + disabled**: 선택 0 일 때 `[→이동] [→복사]` GUI.enabled = false + 회색. 선택 ≥ 1 시 enabled + 녹색 강조 (GUI.color). 우측 컨테이너 미선택 시 동일 disabled.

### 1.4 Category 4 — 검색/필터 정밀화 (4B + 4E + 4G + 4K)

**4B — 등급 범위 dropdown**: 글로벌 toolbar 에 `[등급 ≥ 절세 ▼]` dropdown. 7 옵션 (전체/열악~절세). filter row 적용. ConfigEntry `ContainerFilterMinRare` (int, default -1 = 전체).

**4E — 착용중 제외 filter**: 2H 와 동일 (자산 공유). filter context 는 row visibility, 2H 의 toggle 은 selection mass operation. 한 ConfigEntry 가 둘 다 영향.

**4G — 무공 type secondary tab**: 현 카테고리 = 비급 (Book) 일 때 secondary tab 9 (전체/내공/경공/.../사술). v0.7.10.1 PlayerEditorPanel 의 `DrawKungfuRareLvTabs` 패턴 mirror. SkillNameCache.GetType + 매칭. **컨테이너 area 의 item 도** 같은 secondary tab 적용.

**4K — filter 결과 카운터**: 글로벌 toolbar 끝에 `(결과: 23 / 180)` 형식. 카테고리 + 등급 범위 + 착용중 제외 + 검색 모두 적용 후 visible row 수.

### 1.5 Category 5 — 컨테이너 관리 (5A + 5B + 5C)

**5A — 삭제 confirm dialog ⚠**: 현재 `[삭제]` 즉시 수행 (line 393-404, **safety critical**). ConfirmDialog 통합:
```
"<컨테이너N> 컨테이너를 삭제하시겠습니까?
 안의 12개 item 도 함께 삭제됩니다.
 [확인] [취소]"
```
기존 `ConfirmDialog` 자산 재사용 (`UI/ConfirmDialog.cs`).

**5B — 컨테이너 정보 표시**: dropdown entry format 변경:
- 현재: `01: 컨테이너이름`
- 신규: `01: 컨테이너이름 (12개, 23.5kg)`

`ContainerMetadata` 에서 item 수 + 무게 합계 lazy-compute. dropdown open 시만 갱신 (full list scan, 캐시).

**5C — 컨테이너 복사 (Clone)**: 우측 column 헤더에 `[복사]` button (45 width) — 신규/이름변경/삭제 사이에. 선택된 컨테이너의 모든 item 을 새 컨테이너로 복제. 이름 자동 = `<원본이름> (복사본)`.

```csharp
// src/LongYinRoster/Containers/ContainerRepository.cs 에 추가
public int Clone(int sourceIdx)
{
    var src = LoadContainer(sourceIdx);
    if (src == null) return -1;
    int newIdx = CreateNew($"{src.Name} (복사본)");
    var dst = LoadContainer(newIdx);
    foreach (var item in src.Items) dst.Items.Add(item);  // 깊은 복사 — JSON serialize/deserialize cycle
    SaveContainer(newIdx, dst);
    return newIdx;
}
```

### 1.6 Category 9 — panel resize (9A + 9D)

**9A — Resize handle (corner drag)**: panel 우하단 corner 영역 (16×16 px) 에 drag handle. v0.7.4 검증된 `Event.current.type == EventType.MouseDown / MouseDrag / MouseUp` + `Event.current.mousePosition` 자산 활용.

**구조**:
```csharp
// ContainerPanel.cs Draw 끝부분에 추가
DrawResizeHandle();

private void DrawResizeHandle()
{
    var handleRect = new Rect(_rect.width - 16, _rect.height - 16, 16, 16);
    GUI.DrawTexture(handleRect, Texture2D.whiteTexture);  // 시각 표시 (회색 사각형)
    
    var e = Event.current;
    if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
    {
        _resizing = true;
        _resizeStart = e.mousePosition;
        _resizeStartSize = new Vector2(_rect.width, _rect.height);
        e.Use();
    }
    else if (_resizing && e.type == EventType.MouseDrag)
    {
        var delta = e.mousePosition - _resizeStart;
        float newW = Mathf.Clamp(_resizeStartSize.x + delta.x, MIN_W, MAX_W);
        float newH = Mathf.Clamp(_resizeStartSize.y + delta.y, MIN_H, MAX_H);
        _rect = new Rect(_rect.x, _rect.y, newW, newH);
        e.Use();
    }
    else if (_resizing && e.type == EventType.MouseUp)
    {
        _resizing = false;
        Config.ContainerPanelW.Value = _rect.width;   // 영속화
        Config.ContainerPanelH.Value = _rect.height;
        e.Use();
    }
}
```

**9D — min/max clamp**: `MIN_W = 600, MAX_W = 1600, MIN_H = 400, MAX_H = 1080` (private const). resize 중 clamp.

## 2. 신규 자산 (LOC 추정)

| File | 변경 | LOC |
|---|---|---|
| `src/LongYinRoster/UI/ContainerPanel.cs` | 수정 — 595→~900 | +~300 |
| `src/LongYinRoster/Containers/ContainerOpUndo.cs` | 신규 — Undo stack + OpRecord | ~100 |
| `src/LongYinRoster/Containers/ContainerRepository.cs` | 수정 — Clone helper | +~30 |
| `src/LongYinRoster/Containers/ContainerOps.cs` | 수정 — Undo metadata 기록 | +~50 |
| `src/LongYinRoster/Containers/ContainerMetadata.cs` | 수정 — ItemCount + Weight cache | +~20 |
| `src/LongYinRoster/Containers/ContainerView.cs` | 수정 — 등급 범위 + 착용중 filter | +~40 |
| `src/LongYinRoster/Config.cs` | 수정 — 5 신규 ConfigEntry (Collapsed × 2 / SplitPreset / FilterMinRare / ExcludeEquipped) | +~30 |
| Tests | 신규 ContainerOpUndoTests + ContainerViewExtraFilterTests + ContainerRepositoryCloneTests | ~200, +~15 tests |

총 신규 LOC ≈ 770 (impl) + 200 (tests).

## 3. 영향 범위 / 호환성

- 기존 v0.7.10.2 사용자 ConfigEntry 변경 없음 (append-only)
- 기존 ContainerPanel 외부 callback (Plugin.cs) 변경 없음 — 4-callback 시그니처 그대로
- 게임 patch 없음
- 다른 mod 와 충돌 없음 (ContainerPanel 은 우리 mod 자산만 사용)
- v0.7.10.x baseline 회귀 risk: ContainerPanel.Draw 의 layout 변경이 큼 — 인게임 smoke 필수

## 4. 작업 phase

각 phase 별 commit 분리:

| Phase | 항목 | tests |
|---|---|---|
| Phase 1 | Cat 5 (5A+5B+5C) — 안전성 우선 | +5 |
| Phase 2 | Cat 1 (1A+1B) — layout 변경 | +0 (UI smoke) |
| Phase 3 | Cat 2 (2A+2B+2C+2H) — 일괄선택 | +5 |
| Phase 4 | Cat 4 (4B+4E+4G+4K) — filter | +5 |
| Phase 5 | Cat 3 (3C+3D+3G) — Undo + toast + 강조 | +5 (Undo logic) |
| Phase 6 | Cat 9 (9A+9D) — resize handle | +0 (인게임 smoke) |
| Phase 7 | release docs + smoke + tag | — |

## 5. Spike list

| # | 검증 항목 | NO-GO fallback |
|---|---|---|
| 1 | `Event.current.type == EventType.MouseDrag` strip-safe (v0.7.4 MouseDown 검증, MouseDrag 미검증) | resize handle 미지원, 9B preset 토글 fallback |
| 2 | `Texture2D.whiteTexture` strip-safe (v0.7.3 검증 OK) | — |
| 3 | `Mathf.Clamp` strip-safe | `Math.Max/Min` 직접 사용 |
| 4 | ContainerOps 의 success/fail 카운트 + 사유 metadata 존재 (3D 위해) | 사유 미보유 시 단순 토스트 fallback |
| 5 | ContainerRepository.Delete 후 Restore 가능성 (Undo 위해 Delete 백업) | Move/Copy/Clone 만 Undo, Delete 미지원 명시 |

## 6. Risk

1. **ContainerPanel.cs 595→~900 LOC** — 단일 file 비대 가능성. 미래 v0.7.12+ 에서 부분 분리 후보 (DrawCategoryTabs / DrawGlobalToolbar / DrawColumns 별도 Helper class)
2. **Undo Move/Copy 의 정확성** — Move 의 inverse = source 로 다시 Move. 그러나 Move 도중 다른 op 가 source/target 변경 시 일치 안 됨. 사용자 시나리오 = 즉시 Undo 만 ($lastOp 만 보존, 단일 stack).
3. **Resize handle 의 기존 GUI.DragWindow 와 충돌** — header drag 와 corner drag 가 다른 영역이라 충돌 없음. 검증 smoke 필수.
4. **Split preset 시 ScrollView 안 ScrollView** — collapse + split 동시 처리 시 layout 깨짐 가능. 인게임 smoke.
5. **5C Clone 의 깊은 복사** — JSON serialize/deserialize cycle 안전. 외부 reference 문제 없음.

## 7. 미반영 / 후속

- **6A 진짜 sprite** — v0.8 별도 cycle. IL2CPP sprite asset spike 필요. cheat IconHelper.cs 316 LOC 참조
- **Cat 7 일괄 편집** — 사용자 skip → 별도 cycle (v0.7.10.x patch 또는 v0.8.1 후보)
- **Cat 8 export/import** — 사용자 skip → 별도 cycle
- **Cat 9C/9E (snap dock / fit-to-content)** — edge case, 별도 patch
- **Cat 4F (보유/미보유)** — 사용자 미채택, 별도 patch
- **NPC dropdown** (원래 v0.7.11 후보) — v0.7.12 또는 G5 게이트로 defer

## 8. 메타 로드맵 G4 Decision

본 spec commit 시점에 메타 로드맵 §G4 Pending → §G4 Decision append:

```
### G4 Decision (2026-05-10)

- v0.7.11 ContainerPanel UX overhaul: GO (사용자 요청, brainstorm 10 카테고리 → 6 채택)
- v0.8 진짜 sprite: GO (β 분할로 별도 cycle, IL2CPP spike 단독 격리)
- v0.7.12 NPC dropdown: DEFER (사용자가 ContainerPanel UX 우선시)
- v0.7.10.x 자질 grade marker: DEFER until G5
- v0.7.9 Slot diff: DEFER until G5

Next sub-project: v0.7.11 ContainerPanel UX overhaul.
```

---

## 9. 작업 순서 (plan 의 phase 분해)

1. Phase 1 spec write & commit (본 단계)
2. Plan write & commit (writing-plans skill)
3. Phase 1 (Cat 5) impl — safety 우선
4. Phase 2 (Cat 1) impl — layout
5. Phase 3 (Cat 2) impl — 일괄선택
6. Phase 4 (Cat 4) impl — filter
7. Phase 5 (Cat 3) impl — Undo + toast + 강조
8. Phase 6 (Cat 9) impl — resize handle
9. 인게임 smoke
10. Release v0.7.11 + handoff + roadmap G4 Decision append
