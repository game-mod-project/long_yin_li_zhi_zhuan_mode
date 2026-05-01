# v0.5 외형 PoC — Phase 2 결과 (2026-05-01) — **FAIL**

## Outcome
**G1 판정: FAIL** — 가설 부적합. 외형 카테고리 v0.6 deferred.

## 1차 시도 (commit `e494c48`)

`portraitID` + `gender` setter direct + 8 후보 refresh method 호출.

```
field/property 'portraitID' not found
field/property 'gender' not found
method 'set_portraitID' not found
... (모든 후보 not found)
```

**결과**: HeroData 에 `portraitID`, `gender` field 자체가 부재. 가설 자체가 게임 구조와 맞지 않음.

## 2차 시도 — Discovery pivot (commit `a404703`)

Probe 가 자가 enumerate — `portrait|face|avatar|head|icon|pic|outfit|cloth|hair|skin|appearance|partposture|body` 패턴 매칭 field/property/method dump.

### HeroData 의 외형 관련 발견

| 필드 / method | 타입 | 의미 |
|---|---|---|
| `faceData` | `HeroFaceData` (wrapper) | 얼굴 sub-data graph |
| `partPosture` | `PartPostureData` (wrapper) | 자세 sub-data |
| `skinID`, `skinLv` | int, int | 현재 의상 (이미 v0.3 Cat_Skin) |
| `defaultSkinID` | int | 기본 의상 |
| `setSkinID`, `setSkinLv`, `playerSetSkin` | int/int/bool | 의상 설정 상태 |
| `skinColorDark` | float | 피부색 어두움 |
| `HeroIconDirty` | bool | 아이콘 refresh dirty flag |
| `heroIconDirtyCount` | int | 아이콘 dirty 카운터 |
| `bodyGuard` | bool | 호위 (의미 미상) |
| `changeSkinCd` | int | 의상 변경 cooldown |

### 발견된 method (signature)

- `set_*` setters for all fields above
- `SetSkeletonGraphicSkinColor(SkeletonGraphic)` — 1-arg
- `SetSkeletonGraphicFaceSlot(SkeletonGraphic, Int32, Int32)` — 3-arg
- `SetSkin(Int32, Int32)` — 2-arg (skinID + skinLv 통합 setter)
- `SetSkeletonSkinColor(SkeletonAnimation)` — 1-arg
- `SetSkeletonFaceSlot(SkeletonAnimation, Int32)` — 2-arg

### 후보 zero-arg refresh method

후보 13 개 모두 not found:
`RefreshPortrait`, `ReloadPortrait`, `UpdatePortrait`, `RefreshFaceData`, `RefreshFace`, `RefreshAvatar`, `RefreshSprite`, `RefreshSelfState`, `RefreshIcon`, `ReloadIcon`, `RefreshHead`, `RefreshOutfit`, `RefreshHair`.

> 참고: `RefreshSelfState` 는 v0.3/v0.4 의 PinpointPatcher 가 사용 — 다만 HeroData 의 method 가 아니라 다른 위치 (또는 game-internal). HeroData 자체에는 없음.

## FAIL 결정 사유

1. **`portraitID` 라는 단일 field 자체가 존재 안 함** — 가설이 게임 구조와 안 맞음.
2. **진짜 "외형 변경" = `faceData` 또는 `partPosture` sub-data wrapper** — v0.4 PoC A4 (ItemData) 와 같은 sub-data graph 문제. v0.5 scope 외.
3. **`skinID` (의상) 는 이미 v0.3 Cat_Skin 에서 처리 중** — Appearance 별도 카테고리로 분리할 새 가치 없음.
4. **`HeroIconDirty` flag** 흥미롭지만 외형 자체 변경 없이 refresh 만으로는 의미 없음.
5. **Discovery 결과의 zero-arg refresh method 0** — 외형 sprite refresh 는 game-internal 또는 SkeletonGraphic 인자 (UI 컴포넌트) 필요. HeroData 자체의 zero-arg path 없음.

## v0.6 후보 evidence

- 외형 = `faceData` (HeroFaceData wrapper) + `partPosture` (PartPostureData wrapper) sub-data graph
- v0.4 PoC A4 (ItemData) 와 통합 해결 — sub-data wrapper graph 문제
- 의상 확장 (defaultSkinID / skinColorDark / setSkinID 등) 은 별도 카테고리 가치 적음
- `SetSkin(Int32, Int32)` method 활용한 skinID + skinLv 통합 setter — Cat_Skin 의 v0.6 개선 후보

## v0.5 scope 영향

- `Capabilities.Appearance` flag — `false` 유지 (Plugin runtime 에서)
- `SimpleFieldMatrix` 에 portraitID/gender entry 추가 안 함 (T9 skip)
- `PortraitRefresh.cs` / `PortraitRefreshTests.cs` 신규 안 함 (T8 / T11 skip)
- `PinpointPatcher` 외형 hook 추가 안 함 (T10 skip)
- `SlotDetailPanel` 외형 disabled label 유지 (T19 부분 적용)

## 다음 단계

active PoC (T12) 로 진행. 외형 결과 따라 OR-gate:
- active PASS → v0.5.0 release (active only)
- active FAIL → 양쪽 FAIL → maintenance + PoC report (T27 alternate)
