# v0.7.0 — F11 진입 메뉴 재설계 + 컨테이너 기능

**작성일**: 2026-05-02
**Sub-project 위치**: v0.7.0 (v0.6.4 baseline 후속의 첫 신규 기능)
**브레인스토밍 결과**: 통합 path (현 mod 에 신규 기능 추가, 별도 mod 분리 안 함)

---

## 1. 목적 / 한 줄 요약

캐릭터 capture/apply 외 인게임 인벤토리 / 창고와 외부 디스크 컨테이너 사이의 item 이동·복사 기능 추가. F11 진입 시 모드 선택 메뉴 (캐릭터 관리 vs 컨테이너 관리) 도입.

## 2. 후속 sub-project 와 관계

이번 spec 은 **v0.7.0** 에 한정. 후속 sub-project 별 spec → plan → impl cycle:

| Version | 카테고리 | 의존성 |
|---|---|---|
| **v0.7.0** | **메뉴 재설계 + 컨테이너 (이번 spec)** | foundation |
| v0.7.1 | NPC 지원 | 메뉴 의존 |
| v0.7.2 | Slot diff preview | 독립 |
| v0.7.3 | Apply 부분 미리보기 | 독립 |
| v0.7.4+ | 설정 panel (hotkey/정원/창 크기 변경) | 누적 의존 |

## 3. 사용자 요구사항 (확정)

브레인스토밍 Q&A 결과:

1. **F11 진입 방식**: A+C 결합 — 모드 선택 메뉴 표시 + 단축키 조합 (F11+1 / F11+2 직진입). 향후 설정에서 hotkey 변경.
2. **컨테이너 데이터 구조**: 다중 컨테이너 (`container_NN.json` slot 패턴 mirror). 정원 무제한 (설정에서 제한 가능).
3. **이동/복사 의미**: 둘 다 지원. 다중 항목 일괄 처리. 인벤토리 가득 참 시 사용자 알림.
4. **카테고리 필터**: 가로 탭 — 전체 / 장비 / 단약 / 음식 / 비급 / 보물 / 재료 / 말.
5. **필터 동기화**: 글로벌 (3 panel 모두 같은 카테고리 표시).
6. **창 크기**: 800x600 고정 (향후 설정에서 변경).
7. **Item 표시**: 리스트 뷰 (이름 / 강화lv / 무게 / 종류 텍스트만).
8. **착용 장비 처리**: 경고 + 확인 dialog 후 자동 unequip + 이동.

## 4. 아키텍처

### 4.1 신규 파일

```
src/LongYinRoster/UI/
  ModeSelector.cs            ← F11 메뉴 (캐릭터 관리 / 컨테이너 관리)

src/LongYinRoster/Containers/
  ContainerFile.cs           ← JSON schema 직렬화/역직렬화 (slot file mirror)
  ContainerMetadata.cs       ← _meta block (containerName / createdAt / etc.)
  ContainerRepository.cs     ← 디스크 io + 다중 container 관리 (slot repo mirror)
  ContainerPanel.cs          ← 800x600 IMGUI main UI
  ContainerOps.cs            ← 이동/복사/삭제 logic (ItemListApplier.ApplyJsonToObject 재사용)
  ItemCategoryFilter.cs      ← 카테고리 enum + filter 헬퍼

src/LongYinRoster/Util/
  HotkeyMap.cs               ← F11 / F11+1 / F11+2 입력 처리 (향후 settings 의존)
```

### 4.2 기존 파일 변경

```
src/LongYinRoster/UI/ModWindow.cs
  ─→ F11 hotkey 처리 변경 (즉시 ModWindow 띄우는 대신 ModeSelector 거침)
  ─→ 현재 ModWindow IMGUI 는 CharacterPanel 로 rename + 모드 분기

src/LongYinRoster/Plugin.cs
  ─→ ContainerRepository 초기화 + ModeSelector 등록
```

### 4.3 의존성 그래프

```
F11 입력 → HotkeyMap → ModeSelector
                          ├── 캐릭터 관리 (F11+1) → CharacterPanel (기존 ModWindow 동작)
                          └── 컨테이너 관리 (F11+2) → ContainerPanel
                                                       ├── 인벤토리 view (HeroLocator + ItemListData)
                                                       ├── 창고 view (HeroLocator + selfStorage)
                                                       └── 컨테이너 view (ContainerRepository)

ContainerOps:
  - 이동 (game → container): ItemListData 에서 제거 + ContainerFile 에 추가 + 디스크 저장
  - 복사 (game → container): ContainerFile 에 deep-copy + 디스크 저장 (game 인벤토리 유지)
  - 이동 (container → game): ContainerFile 에서 제거 + game 인벤토리 add (LoseAllItem 안 씀, 직접 GetItem)
  - 복사 (container → game): game 인벤토리 add (컨테이너 유지)
  - 삭제 (container only): ContainerFile 에서 제거 + 디스크 저장
```

## 5. UI 디자인

### 5.1 ModeSelector (F11 메뉴)

```
┌──────────────────────────┐
│  LongYin Roster Mod      │
├──────────────────────────┤
│  [캐릭터 관리 (F11+1)]    │
│  [컨테이너 관리 (F11+2)]  │
│                          │
│  v0.7.0 — F11+0 닫기      │
└──────────────────────────┘
```

- 작은 창 (~280x160). 화면 중앙 또는 마지막 ModWindow 위치 근처.
- 버튼 클릭 또는 단축키로 진입.

### 5.2 ContainerPanel (800x600)

```
┌────────────────────────────────────────────────────────┐
│ [전체|장비|단약|음식|비급|보물|재료|말]  ← 글로벌 카테고리 탭   │
├──────────────────────────┬─────────────────────────────┤
│ 인벤토리 (X / 171개)       │ [컨테이너▼] [신규][이름변경][삭제] │
│ ☐ 절세大剑 (장비/강화10/30kg) │                             │
│ ☐ 麒麟兽    (말/30kg)      │ ☐ 절세布甲 (장비/강화10/24kg) │
│ (스크롤)                   │ ☐ 절세大剑 (장비/강화10/30kg) │
│                          │ (스크롤)                    │
│ [→이동] [→복사]             │                             │
├──────────────────────────┤ [←이동] [←복사] [☓삭제]       │
│ 창고 (X / 217개)           │                             │
│ ☐ 多情飞刀 (비급)          │                             │
│ ...                        │                             │
│ [→이동] [→복사]             │                             │
└──────────────────────────┴─────────────────────────────┘
```

- **좌측 50% (400px)**: 인벤토리 (상 ~250px) + 창고 (하 ~250px) 수직 split
- **우측 50% (400px)**: 컨테이너 panel (드롭다운으로 컨테이너 선택)
- **상단 탭**: 글로벌 카테고리 필터
- **버튼**: 각 panel 안 체크박스 다중 선택 후 이동/복사 (방향 → / ←) + 컨테이너 panel 만 삭제 (☓)
- **컨테이너 관리 버튼**: 신규 (이름 입력 prompt) / 이름변경 / 삭제 (현재 선택된 컨테이너)

### 5.3 Item 리스트 항목

리스트 한 줄 형식:
```
☐ <이름> (<카테고리>/<강화lv>/<무게>kg)
```

- **이름**: ItemData.name (한국어 / 한자 그대로)
- **카테고리**: ItemData.type → 한국어 문자열 (장비/단약/음식/비급/보물/재료/말)
- **강화lv**: ItemData.equipmentData.enhanceLv (장비만, 0 이면 표시 생략)
- **무게**: ItemData.weight (소숫점 1자리)

### 5.4 카테고리 필터 매핑

| 탭 | type | subType | 비고 |
|---|---|---|---|
| 전체 | 모두 | 모두 | (default) |
| 장비 | 0 | 모두 | 무기/갑옷/투구/신발/장신구 모두 |
| 단약 | 2 | 0 | medFood subType=0 |
| 음식 | 2 | 1+ | medFood subType ≥ 1 |
| 비급 | 3 | 모두 | book |
| 보물 | 4 | 모두 | treasure |
| 재료 | 5 | 모두 | material |
| 말 | 6 | 모두 | horse + horse armor |

(type=1 은 비점검 — 추후 발견되면 적합한 카테고리에 매핑하거나 "기타" 탭 추가)

## 6. 데이터 schema

### 6.1 컨테이너 file 위치

```
BepInEx/plugins/LongYinRoster/Containers/
  container_01.json
  container_02.json
  ...
```

(Slots 옆에 `Containers/` 폴더 신규)

### 6.2 ContainerFile JSON

```json
{
  "_meta": {
    "schemaVersion": 1,
    "containerIndex": 1,
    "containerName": "용병 장비",
    "userComment": "",
    "createdAt": "2026-05-02T20:00:00+09:00",
    "modVersion": "0.7.0"
  },
  "items": [
    {
      "itemID": 0, "type": 0, "subType": 0, "name": "절세大剑",
      "value": 261888, "itemLv": 5, "rareLv": 5, "weight": 24.0,
      "equipmentData": { "enhanceLv": 10, ... },
      ...
    },
    ...
  ]
}
```

- `items` 배열 = ItemData 구조 그대로 (slot JSON 의 itemListData.allItem 같은 형식)
- subData 풀 보존 (equipmentData / medFoodData / bookData / treasureData / materialData / horseData)
- `_partPostureFloats` 같은 player-level inject 는 미적용 (item-level 만)

## 7. 핵심 동작 시나리오

### 7.1 게임 → 컨테이너 (이동)

1. 사용자가 인벤토리 panel 에서 1+ item 체크 → "→이동" 버튼.
2. 카테고리 필터 적용된 item 만 시각적 표시되지만 체크박스로 명시 선택된 item 만 처리.
3. 착용 중 item 포함 여부 검사 → 포함 시 confirm dialog 표시 → OK 시 UnequipItem 호출.
4. ContainerOps.MoveToContainer:
   - 각 item 을 ContainerFile.items 에 deep-copy add
   - 각 item 을 game's allItem 에서 제거 (LoseAllItem 안 씀, 개별 삭제 — 인벤토리 grid index 보존 위해)
   - ContainerFile 디스크 저장
5. UI 갱신 + 토스트 (예: "5개 항목 이동 완료").

### 7.2 컨테이너 → 게임 (복사)

1. 사용자가 컨테이너 panel 에서 1+ item 체크 → "←복사" 버튼.
2. 인벤토리 가득 참 검사 (171칸 - 현재 occupied):
   - 가득 참 시 처리 가능 N개 만 표시 + 토스트 ("5개 중 3개만 추가 가능 — 인벤토리 가득 참")
3. ContainerOps.CopyToGame:
   - 각 item 을 game's GetItem(wrapper, false) 호출로 추가
   - ContainerFile.items 는 변경 안 함
4. UI 갱신.

### 7.3 컨테이너 항목 삭제

1. 사용자가 컨테이너 panel 에서 1+ item 체크 → "☓삭제" 버튼.
2. confirm dialog (실수 방지) → OK 시 처리.
3. ContainerOps.DeleteFromContainer:
   - 선택된 item 을 ContainerFile.items 에서 제거
   - 디스크 저장
4. UI 갱신.

### 7.4 컨테이너 신규 생성 / 이름 변경 / 삭제

- **신규**: "신규" 버튼 → 이름 입력 prompt (IMGUI text field) → 비어있으면 default 이름 (`container_NN`) → 신규 file 생성 + 컨테이너 드롭다운에 추가.
- **이름 변경**: 드롭다운에서 선택된 컨테이너 → "이름변경" 버튼 → 텍스트 prompt → metadata 갱신 + 디스크 저장.
- **삭제**: 드롭다운 선택 → "삭제" 버튼 → confirm dialog → file 삭제 + 드롭다운에서 제거.

## 8. Error handling

- **컨테이너 file 손상**: JsonDocument 파싱 fail 시 warn 로그 + 토스트 + 빈 컨테이너로 처리 (사용자가 인지 후 재구성).
- **이동/복사 부분 실패**: 실패한 item 만 원본에 남김 + 토스트로 N/M 표시 (예: "5개 중 3개 이동 — 2개 실패 (인벤토리 가득)").
- **게임 진입 안 함**: HeroLocator.GetPlayer() == null 인 상태에선 인벤토리/창고 panel 비활성 (회색 표시 + "게임 진입 후 사용 가능"). 컨테이너 panel 만 활성 (디스크 io 만).
- **컨테이너 fold (외부 도구로 손상된 file)**: schemaVersion 검사 + version mismatch 시 warn.

## 9. 기존 코드 재사용

- **`ItemListApplier.ApplyJsonToObject`** (internal) — item deep-copy
- **`IL2CppListOps`** — game's allItem list 조작
- **`HeroLocator`** — player 접근
- **`SlotRepository`** 패턴 — ContainerRepository 작성 시 mirror
- **`ApplySelection`** — N/A (컨테이너는 selection 무관)

## 10. Out of scope (v0.7.x+)

- Item 상세 정보 패널 (subData 풀 표시 — 현재는 핵심 정보 텍스트만)
- 아이콘 그리드 표시 (게임 Sprite extraction 별도 spike 필요)
- 컨테이너 검색 / 정렬 / sort 옵션
- 설정 panel (hotkey 변경 / 정원 변경 / 창 크기 조정 — v0.7.4+)
- NPC 지원 (v0.7.1)
- Slot diff preview (v0.7.2)
- Apply 부분 미리보기 (v0.7.3)
- Item ID 변경 / 무게 변경 등 직접 편집 기능 (cheat-tier 기능, 추가 검토 필요)

## 11. 검증 / 성공 기준

1. F11 단독 → 모드 메뉴 표시. F11+1 / F11+2 즉시 진입.
2. 컨테이너 신규 / 이름변경 / 삭제 정상 작동.
3. 인벤토리 → 컨테이너 이동: 게임 인벤토리에서 제거 + 컨테이너에 추가 + 디스크 저장 확인.
4. 컨테이너 → 인벤토리 복사: 컨테이너 유지 + 게임 인벤토리에 동일 item 추가 (subData 풀 보존 — 강화lv / 돌파 등).
5. 카테고리 필터 정확히 적용 (장비 탭 → type=0 만 표시).
6. 착용 장비 이동 시 confirm 후 UnequipItem 자동 호출.
7. 인벤토리 가득 참 시 부분 처리 + 토스트 알림.
8. 컨테이너 file 손상 시 graceful fallback.

## 12. 위험 / 미지수

- **type=1 의 미지 카테고리**: probe 또는 동작 후 발견되면 매핑 추가.
- **이동 시 게임 인벤토리 grid index 보존**: ItemListApplier 의 GetItem 사용 시 자동 정렬되므로 nowEquipment.*SaveRecord 와 매칭 issue 가능. v0.6.0 의 identity-based matching 패턴 활용해 영향 최소화.
- **다수 (200+) item 표시 시 IMGUI 성능**: GUILayout.BeginScrollView + ImGui.Selectable 비슷한 패턴으로 가상화 없이 시도. 200~300 row 까지는 frame budget 안 들어갈 것. 500+ 시 가상화 고려.

---

**다음 단계**: 이 spec 검토 후 OK 시 implementation plan 작성 (writing-plans skill).
