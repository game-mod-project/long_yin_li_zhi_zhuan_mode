# LongYinCheat v0.7.5+ 기능 참고 자산

날짜: 2026-05-05
대상: LongYinRoster v0.7.5 이후 추가/업데이트 (무공·아이템 에디터, NPC·플레이어 세부 수정)
관련 분석: `2026-05-05-longyincheat-dll-analysis.md`
디컴파일 결과: `C:/Users/deepe/AppData/Local/Temp/longyincheat_decomp/`

LongYinCheat (v1.4.7, 곰딴지) 의 **즉시 차용 가능 자산** 카탈로그.

## 1. 게임 객체 접근 헬퍼 — `PlayerAccess`

```csharp
PlayerAccess.GDC        // GameDataController.Instance
PlayerAccess.GC         // GameController.Instance
PlayerAccess.World      // GC.worldData
PlayerAccess.Player     // World.Player()  (HeroData)
PlayerAccess.Inventory  // Player.itemListData (ItemListData)
```

**적용 가치**: LongYinRoster 가 동일 헬퍼 채택하면 코드 일관성 유지. v0.7.4 까지 사용자 mod 가 직접 chain 호출했다면 정리 가치 있음.

## 2. 아이템 에디터 / 생성기 — `ItemGenerator`

### 12 카테고리 ItemData 생성

`ItemGenerator.Generate(ItemCategory cat, int itemLv, float bossLv)`:

| Category | 호출 함수 |
|---|---|
| Weapon | `gC.GenerateWeapon(itemLv, 0, 0, bossLv, player)` |
| Armor | `gC.GenerateArmor(itemLv, 0, bossLv, player)` |
| Helmet | `gC.GenerateHelmet(...)` |
| Shoes | `gC.GenerateShoes(...)` |
| Decoration | `gC.GenerateDecoration(...)` |
| Book | `gC.GenerateBook(itemLv, bossLv, -1, null)` |
| Medicine | `gC.GenerateMedData(itemLv, itemLv+5, bossLv)` |
| Food | `gC.GenerateFoodData(itemLv, itemLv+5, bossLv, 0)` |
| Horse | `gC.GenerateHorseData(itemLv, itemLv+3, bossLv)` |
| Material | `gC.GenerateMaterial(0, itemLv, bossLv)` |
| Treasure | `gC.GenerateTreasure(0, itemLv, bossLv)` |
| Random | `gC.GenerateRandomItem(itemLv, bossLv, false, player)` |

```csharp
public enum ItemCategory {
    Weapon, Armor, Helmet, Shoes, Decoration,
    Book, Medicine, Food, Horse, Material, Treasure, Random
}
public static readonly string[] CategoryNames = {
    "무기", "갑옷", "투구", "신발", "장신구",
    "비급", "단약", "음식", "말", "재료", "보물", "랜덤"
};
```

### 인벤토리 추가 헬퍼

```csharp
ItemGenerator.AddToInventory(ItemData)
  // → player.itemListData.GetItem(item, false)
  // item.isNew = true 자동 설정

ItemGenerator.AddCloneToInventory(ItemData original, ItemListData target)
  // → ItemData.Clone() → TryCast<ItemData>() → GetItem
  // IL2CPP wrapper 동일 객체에 다른 wrapper 생성 회피용 패턴

ItemGenerator.AddCloneWithLv(ItemData original, int itemLv, int rareLv, ItemListData target)
  // → Clone → TryCast → itemLv/rareLv 변경 → CountValueAndWeight → InitClonedHorseStats → GetItem

ItemGenerator.InitClonedHorseStats(ItemData)
  // HorseData favorRate/seeRange/stepRate 초기화 (Random)
```

**적용 가치**: v0.7.7 (후보) Item editor 에서 ItemDetailPanel view-only 를 edit-able 로 확장할 때 그대로 차용 가능. `ItemData.Clone()` + `TryCast<ItemData>` + `CountValueAndWeight()` 패턴이 IL2CPP 환경에서 안전.

## 3. NPC 편집 — `CharacterFeature`

### NPC 캐싱 데이터 모델

```csharp
public struct NpcInfo {
    public int HeroId;
    public HeroData Hero;
    public string FullName;
    public string ForceName;
    public float Favor;
    public bool IsDead;
}

public class FavorDiagInfo {
    public float RawFavor;
    public float DisplayFavor;
    public float MaxFavor;
    public float NatureFavorRate = -1f;
    public float HornorAddFavorRate = -1f;
    public int HornorLv = -1;
    public bool IsFriend, IsBrother, IsHater, HasLover, IsPrelover, HasTeacher;
    public string FavorLvText = "";
    public int PlayTestMaxFavor = -1;
    public List<string> FavorLvTexts;
}

public struct TagInfo {
    public int TagId;
    public string Name;
    public string Category;
    public int Value;
    public bool IsOwned;
}

public struct AttribEntry {
    public int TypeId;
    public string Name;
    public float Value;
}
```

### API
```csharp
CharacterFeature.GetCachedNpcs() / RefreshNpcCache()
CharacterFeature.ClampResource(float)  // MAX_RESOURCE = 9999999f
CharacterFeature.GetAttriName(int) / GetFightSkillName(int) / GetLivingSkillName(int)
```

### 한글 이름 lookup 테이블 (재사용 가치 ★★★)

```csharp
private static readonly string[] DefaultAttriNames = {
    "체질", "근골", "내식", "신법", "의지", "매력"
};
private static readonly string[] DefaultFightSkillNames = {
    "도법", "검법", "권장", "장병", "기문", "사술", "경공", "절기", "내공"
};
private static readonly string[] DefaultLivingSkillNames = {
    "약학", "의술", "독학", "요리", "제련", "제작", "채집", "사냥", "낚시"
};

private static readonly Dictionary<int, string> SpeAddTypeNames = new Dictionary<int, string> {
    { 0, "근력" }, { 1, "민첩" }, { 2, "지력" }, { 3, "의지" }, { 4, "체질" },
    { 5, "경맥" }, { 6, "내공" }, { 7, "경공" }, { 8, "절기" }, { 9, "권장" },
    { 10, "검법" }, { 11, "도법" }, { 12, "장병" }, { 13, "기문" }, { 14, "사술" },
    { 15, "내공위력" }, { 16, "경공위력" }, { 17, "절기위력" }, { 18, "권장위력" },
    { 19, "검법위력" }, { 20, "도법위력" }, { 21, "장병위력" }, { 22, "기문위력" },
    { 23, "사술위력" }, { 24, "의술" }, { 25, "독술" }, { 26, "학식" }, { 27, "언변" },
    { 28, "벌채" }, { 29, "목식" }, { 30, "단조" }, { 31, "연약" }, { 32, "요리" },
    { 33, "근력잠재" }, { 34, "민첩잠재" }, { 35, "지력잠재" }, { 36, "의지잠재" },
    { 37, "체질잠재" }, { 38, "경맥잠재" }, { 39, "내공잠재" }, { 40, "경공잠재" },
    { 41, "절기잠재" }, { 42, "권장잠재" }, { 43, "검법잠재" }, { 44, "도법잠재" },
    { 45, "장병잠재" }, { 46, "기문잠재" }, { 47, "사술잠재" }, { 48, "의술잠재" },
    { 49, "독술잠재" }, { 50, "학식잠재" }, { 51, "언변잠재" }, { 52, "벌채잠재" },
    { 53, "목식잠재" }, { 54, "단조잠재" },
    // ... 60+ 종 (단약/음식 잠재 등 추가)
};
```

**적용 가치**: v0.7.5 한글화 작업 시 이 정적 테이블을 그대로 import 하면 attribute/skill 한글명 lookup 자동화 (ModFix 사전에 의존 안 함). 폐기 위험 없음 (내장 테이블).

## 4. 무공 (Skill) 에디터 — `SkillManager`

### 3가지 데이터 모델

```csharp
public struct SkillInfo {
    public int SkillID;
    public string Name;
    public int RareLv;
    public string TypeName;
    public string Describe;
    public string IconName;
    public int ForceID;
    public string ForceName;
}

public struct EquippedSkillInfo {
    public string SlotName;
    public KungfuSkillLvData Data;
    public string Name;
    public int Level;
    public float FightExp;
    public float BookExp;
    public float Power;
    public float MaxPower;
    public bool CanUpgrade;
    public string IconName;
    public int RareLv;
    public string TypeName;
    public int ForceID;
    public string ForceName;
}

public struct UnlearnedBookInfo {
    public ItemData Book;
    public int SkillID;
    public string Name;
    public int SkillRareLv;
    public int BookRareLv;
    public string TypeName;
    public string IconName;
    public int ForceID;
    public string ForceName;
}
```

### 핵심 API

- `GetAllSkills()` — `GameDataController.kungfuSkillDataBase` 전체 캐싱 (count 변경 시 재생성)
- 무공명 자동 한글화: `TranslationHelper.Translate(skill.name)` 호출
- `belongForceID` 로 문파 분류
- `KungfuSkillData.GetSkillIcon()` — 아이콘 이름 추출

### 등급 한글 라벨 (CheatPanels.cs 에서 정의)

```csharp
EquipLvNames    = { "열악", "보통", "우수", "정량", "완벽", "절세" };  // 장비
QualityNames    = { "잔품", "하품", "중품", "상품", "진품", "극품" };  // 단약
SkillLvNames    = { "기초", "진급", "상승", "비전", "정극", "절세" };  // 무공
BookRareNames   = { "잔본", "방본", "선본", "고본", "진본", "완본" };  // 비급
RareColors      = 6단계 등급 색상 (회색/녹색/파랑/보라/금색/빨강)
```

**적용 가치**: v0.7.5+ 무공 에디터 만들 때 데이터 추출 로직 통째 차용 가능. 단 `KungfuSkillLvData` 의 level/fightExp/bookExp/power 직접 수정은 영구 변경이라 백업 필수.

## 5. 플레이어 스탯 에디터 — `StatEditor`

### 잠금 시스템 (매 frame `EnforceLocks()`)

```csharp
StatEditor.LockHp / LockPower / LockMana / LockWeight  // bool
StatEditor.LockedMaxHp / LockedMaxPower / LockedMaxMana / LockedMaxWeight  // float
StatEditor.AllowExternalGrowth  // bool — 잠금된 값보다 큰 값은 유지
```

### API

```csharp
StatEditor.FullHeal(player)              // hp = maxhp
StatEditor.RestoreEnergy(player)         // power/mana 복구
StatEditor.CureInjuries(player)          // externalInjury/internalInjury/poisonInjury = 0

StatEditor.SetMaxHp/Power/Mana(player, string input)
  // 1. float.TryParse
  // 2. CharacterFeature.ClampResource (9999999 cap)
  // 3. ChangeMaxHp(delta, false) → RefreshMaxAttriAndSkill()
  // 4. 실패 시 realMaxHp/maxhp 직접 set (fallback)
  // 5. hp = maxhp 보충
  // 6. LockedMax* 저장 + Lock* = true

StatEditor.EnforceLocks()
  // 매 프레임 lock 값 적용 (다른 시스템이 변경해도 복원)
  // AllowExternalGrowth 시: realMaxHp = max(LockedMaxHp, 현재값)

StatEditor.FixWeightText(inv)
  // GameObject.Find("Canvas/HeroDetailPanel/Item/Weight") 캐싱
  // weight > maxWeight 시 "<color=#B40000>{w}/{m}</color>" 적색 표시
```

**핵심 노하우**: `realMaxHp` ↔ `maxhp` **두 필드를 모두 동기화** 해야 시스템 일관성 유지. `ChangeMaxHp(delta, false)` 가 정상 경로, fallback 으로 직접 대입.

**적용 가치**: v0.7.5+ 플레이어 세부 정보 edit-able 패널 만들 때 차용.

## 6. NPC 편집 패널 (CheatPanels) — UI 구조 참고

`CheatPanels.cs` (5653 LOC) 6개 IMGUI 윈도우. **NPC 편집** (`DrawNpcEditPanel`, line 4190) 4 탭:

| 메서드 | 라인 | 역할 |
|---|---|---|
| `DrawNpcBasicTab(npc)` | 4268 | 기본 정보 (이름/성씨/나이/소속) |
| `DrawNpcStatTab(npc)` | 4828 | 스탯 |
| `DrawNpcIndividualStats(hero, stats, maxStats, inputs, talentInputs, count, talentCap, getName, setValue, setMax)` | 4863 | 6 속성 / 9 무공 / 9 생활기예 + 잠재 cap (재사용 가능 헬퍼) |
| `DrawNpcItemTab(npc)` | 4995 | 인벤토리 |
| `DrawNpcSkillTab(npc)` | 5114 | 무공 |
| `DrawNpcEquipSlot(slot, items)` | 5337 | 장비 슬롯 (장신구 4슬롯 list) |
| `DrawNpcSingleEquip(slot, item)` | 5361 | 단일 장비 |
| `DrawNpcEquipRow(slot, item)` | 5380 | 행 |

### 재사용 가능한 GUI 헬퍼

| 메서드 | 라인 | 용도 |
|---|---|---|
| `DrawForceFilterWrapped(forceNames, ref selected, maxWidth)` | 932 | 문파 필터 (랩 wrap) |
| `DrawForceDropdown(forceNames, ref selected)` | 2236 | 문파 드롭다운 + 검색 |
| `DrawNpcStatRow(label, ref input, onApply)` | 5414 | label + textfield + Apply 버튼 행 |
| `DrawHorseStatField(label, key, getter, setter)` | 3890 | float 필드 편집기 |
| `DrawHorsePercentField(label, key, getter, setter)` | 3913 | % 필드 편집기 |
| `DrawSpeAddDataEditor(HeroSpeAddData, prefix)` | 3936 | 천공 보정 60+ 종 batch editor |
| `DrawPanelResizeHandle(ref Rect, windowId)` | 5537 | 패널 우하단 드래그 리사이즈 |
| `DrawEquipItemRow(item, inv)` | 3240 | 장비 행 |
| `DrawEditableEquipList(slot, items)` | 3461 | 장신구처럼 list-based 슬롯 |
| `DrawEditableSingleEquip(slot, item)` | 3442 | 무기처럼 단일 슬롯 |

## 7. IMGUI 윈도우 구조 (`CheatPanels.DrawAllPanels`)

```csharp
// windowId 와 toggle 상태 분리
public static bool ShowItemGenPanel, ShowSkillPanel, ShowAttribPanel, ShowEquipPanel, ShowTagPanel, ShowNpcEditPanel;
private static Rect _itemGenRect, _skillRect, _attribRect, _equipRect, _tagRect, _npcEditRect;

// 윈도우 호출
_itemGenRect = GUI.Window(98770, _itemGenRect, _itemGenFunc, new GUIContent("아이템 생성"), windowStyle);
_skillRect   = GUI.Window(98771, _skillRect, _skillFunc, new GUIContent("무학 패널"), windowStyle);
_attribRect  = GUI.Window(98773, _attribRect, _attribFunc, new GUIContent("속성 편집"), windowStyle);
_equipRect   = GUI.Window(98772, _equipRect, _equipFunc, new GUIContent("장비 관리"), windowStyle);
_tagRect     = GUI.Window(98774, _tagRect, _tagFunc, new GUIContent("천부(태그) 관리"), windowStyle);
_npcEditRect = GUI.Window(98775, _npcEditRect, _npcEditFunc, new GUIContent("NPC 편집"), windowStyle);
```

영속화: `BepInEx/config/LongYinCheat_panels.cfg` 와 `_panelSizeInputs` Dictionary<string,string>.

**적용 가치**: LongYinRoster 의 ContainerPanel + ItemDetailPanel 패턴과 거의 1:1. 패널 영속화 + 멀티 윈도우 + 리사이즈 핸들 패턴 그대로 차용 가능.

## 8. ItemData 카테고리 분기 (CheatPanels)

각 아이템 종류별 별도 생성 패널:
- `DrawItemGenPanel` (1112) — 메인 진입
- `DrawItemDbBrowser` (1209) — 게임 ItemDB 검색·미리보기 (이미지 캐싱)
- `DrawDecorationGenerator` (1385) — 장신구
- `DrawHorseArmorGenerator` (1534) — 말·안장
- `DrawTreasureGenerator` (1656) — 보물
- `DrawMaterialGenerator` (1776) — 재료

**적용 가치**: v0.7.4.x patch (말/보물/재료 curated) 작성 시 카테고리별 필드 추출 로직 참고.

## 9. SaveDataSanitizer — 데이터 무결성 보호

```csharp
SaveDataSanitizer.SanitizeWorld() returns SanitizeResult
  // - WorldData null 체크
  // - 모든 Force.resourceStore/resourceStoreMax 클램프
  // - Hero attribStat/fightSkillStat/livingSkillStat NaN/Infinity/over-max 보정
  // - 최대값: 자원 9999999, 스탯 99999, 비율 9999

SaveDataSanitizer.IsValid(value, max)  // private
SaveDataSanitizer.Sanitize(value, max, fallback = 0f)  // 클램프 + NaN/Infinity → fallback

public struct SanitizeResult {
    public int TotalChecked;
    public int TotalFixed;
    public string Summary;
}
```

**적용 가치**: 무공/아이템/스탯 에디터 도입 시 **edit 직후 sanitize 호출** 패턴 권장. 사용자가 잘못된 값 입력해도 게임 크래시 방지. v0.7.7 Item editor 에서 필수 차용.

## 10. BattleFeature — 즉시 효과 (참고)

```csharp
BattleFeature.WinBattle()      // 전투 즉시 승리 (BattleController 조작)
BattleFeature.HealAllAllies()  // 아군 전체 회복
BattleFeature.GetBattleSkipField() / GetModBattleSkip / SetModBattleSkip
```

**적용 가치**: NPC 편집 패널에서 "이 NPC 만 즉시 회복" 같은 quick action 만들 때 패턴 참고.

## 11. CheatProfiles — 설정 영속화 패턴

```csharp
CheatProfiles.SaveCurrentProfile()
CheatProfiles.LoadProfile(string name)
CheatProfiles.ProfileData {
    // 모든 멀티플라이어/플래그 직렬화
}
```

**적용 가치**: v0.7.6 설정 panel 작업 시 hotkey/창 크기/검색·정렬 영속화 옵션을 프로필 단위로 묶을 때 참고.

## 12. IconHelper — UI 아이콘 (v0.8 sprite 작업 참고)

`IconHelper.cs` (316 LOC) — IL2CPP 환경에서 sprite asset → IMGUI texture 변환. v0.8 (후보) 진짜 sprite 도입 작업 시 핵심 참고. (필요 시 별도 deep-dive.)

## v0.7.5+ 작업 순서 권장 매트릭스

| 단계 | 차용 자산 | 우선순위 |
|---|---|---|
| **D-4 한글화** | ModFix `TranslationData.transDict` reflection + 자체 사전 + `LTLocalization.GetText` fallback | ★★★ |
| | `CharacterFeature` 의 `DefaultAttri/FightSkill/LivingSkillNames + SpeAddTypeNames` 정적 한글 테이블 | ★★★ |
| **v0.7.4.x patch** | `ItemFieldExtractor.GetCuratedFields` 확장 — `HorseData/MaterialData/TreasureData` 필드 추출 + `CategoryNames[12]` 한글 라벨 | ★★ |
| **v0.7.6 설정 panel** | `CheatPanels._panelSizeInputs` + `LongYinCheat_panels.cfg` 영속화 패턴 + `CheatProfiles` | ★★ |
| **v0.7.7 Item editor** | `ItemGenerator.AddCloneWithLv` + `SaveDataSanitizer` + `ItemData.CountValueAndWeight` + `EquipLvNames/QualityNames/SkillLvNames/BookRareNames` 라벨 | ★★ |
| **NPC 편집 / 플레이어 세부** | `StatEditor.EnforceLocks` + `realMaxHp/maxhp` 동기화 + `DrawNpcStatRow` GUI 헬퍼 + `DrawSpeAddDataEditor` (천공 60+) + `DrawNpcIndividualStats` 함수 시그니처 | ★★ |
| **무공 에디터** | `SkillManager.SkillInfo / EquippedSkillInfo / UnlearnedBookInfo` 데이터 모델 + `KungfuSkillLvData.level/fightExp` 수정 + `RareColors[6]` 색상 | ★ |
| **v0.8 sprite** | `IconHelper` (별도 deep-dive 필요) + `FontManager` 동적 폰트 패턴 | ★ |
