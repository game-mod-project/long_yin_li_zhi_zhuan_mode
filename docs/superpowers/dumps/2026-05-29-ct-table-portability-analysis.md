# Cheat Engine 테이블 이식 가능성 분석 — `LongYinLiZhiZhuan.CT`

날짜: 2026-05-29
대상 CT: `D:\My Cheat Tables\LongYinLiZhiZhuan.CT` ("龙吟利志传 Multi-Tool v8", 525KB)
분석 목적: CT 테이블 기능 중 LongYinRoster 모드로 이식 가능한 것 식별
사용자 결정: **아이템 생성 + 이벤트 생성** 이식 (이 문서 작성 후 brainstorm → spec → plan → impl)

---

## 0. 핵심 아키텍처 통찰 (가장 중요)

CT 테이블 Lua 코드의 **절반 이상**(`MT.hook.*` — `shellExec` ASM shellcode 주입,
`cmdBuf` 커맨드 버퍼, `installMainThreadHook`, cmd=1/4/5/8 디스패치)은 **순전히
CE가 게임 외부 프로세스/스레드에서 실행되기 때문에** IL2CPP 게임 메서드를 직접
호출할 수 없어서 만든 우회 기계장치다.

게임의 메인 스레드에 hook 을 박고, 커맨드 버퍼에 파라미터를 쓴 뒤 cmd 번호를
설정하면, hook 된 메인 스레드가 그 커맨드를 대신 실행한다:
- `cmd=1` = GetItem (인벤 추가)
- `cmd=4` = doGenEquip (제너레이터 호출, 결과 포인터 반환)
- `cmd=5` = GenerateHeroData + ManagePlayerRecruitHero
- `cmd=8` = runtime_invoke (CreateWorldEvent)

**우리 모드(BepInEx + Il2CppInterop)는 이미 in-process + Unity 메인 스레드에서
실행된다** (OnGUI / Harmony patch / Update). 따라서:

1. **`MT.hook.*` 전체가 불필요** — `GenerateWeapon`, `GenerateBook`,
   `GenerateHeroData`, `CreateWorldEvent` 를 Il2CppInterop reflection 으로 **직접
   호출**하면 된다. shellcode/cmdBuf/스레드 후킹 0줄.
2. CT 의 raw 오프셋(`hero+0x178=hp`)을 우리는 **reflection 필드명**으로 — 게임
   패치에 더 강함.
3. **CT 테이블 = 게임 메서드 카탈로그 + 정확한 시그니처 자료집**. 어려운 부분
   (어느 메서드를 어떤 파라미터로 부르는지)이 cmd 디스패치 주석에 문서화됨.

⚠ **파라미터 순서 주의**: CT 는 ASM 레지스터 매핑(edx/r8/xmm3)으로 파라미터를
넘기므로 cmd 디스패치 코드의 순서가 **C# 시그니처 순서와 다를 수 있다**. 우리는
reflection (이름 + param count) 으로 부르므로 **C# 시그니처**를 따라야 한다 —
기존 `2026-05-05-v075-cheat-feature-reference.md` 의 디컴파일 시그니처가 우선
참조. 인게임 spike 로 1회 검증 필수.

---

## 1. 전체 기능 분류 (CT Multi-Tool v8, 5 탭)

탭: 通用 General / 门派 Sect / 武学 Martial Arts / 物品 Items / 事件 Events

### A. 우리 모드 이미 보유 (이식 불필요)
| CT 기능 | 우리 모드 위치 |
|---|---|
| restoreHP / clearInjury / fame | `PlayerEditApplier` (hp/maxhp/fame + QuickFullHeal) |
| talentPoints / attr·fight·living points | `PlayerEditorPanel` (천부 + 자질) |
| setStatCaps (자질 상한 돌파) | `HeroDataCapBypassPatch` (v0.7.10) |
| talentSlot (`GetMaxTagNum` mov eax,N;ret 패치) | `GetMaxTagNumPatch` Harmony (v0.7.10) |
| maxRarity / 기존 아이템 편집 | Item editor (v0.7.7, rareLv/itemLv 편집) |
| 무공 추가/편집 | `KungfuSkillEditor` + PlayerEditorPanel 무공 list |

### B. 이식 가능 + 高가치 (★★★) — **사용자 선택: 아이템 생성**
### C. 이식 가능 + 신규 (★★) — **사용자 선택: 이벤트 생성**
### D. 이식 가능하나 UX 성격 다름 (실시간 토글, 보류)
battleSpeed / combatExp·livingExp 배율 / enemyOneHP / horseSpeed /
infiniteStamina / dungeonReveal — "스냅샷·편집기" 모델과 다른 실시간 치트. Harmony
or Update 루프로 가능하나 별도 scope. 자원 치트(money/meteorite 등)도 여기 보류.

### E. 불필요
`MT.hook.*` 전체(shellcode/cmdBuf/스레드 후킹), 연결/진단 — in-process라 무의미.

---

## 2. 아이템 생성 — 게임 메서드 카탈로그 (이식 대상 ①)

모든 generator 는 `GameController` (이하 gc) 인스턴스 메서드. 결과 `ItemData` 를
`player.itemListData.GetItem(item, false)` 로 추가 (우리 `ItemListApplier` /
`ContainerOps` 가 이미 쓰는 경로 — `FinalizeNewItemWrapper` 도 적용 가능).

### 2.1 장비 family (`MT.hook.equipDBs`)
| 카테고리 | gc 메서드 | params | DB 오프셋 (GDC 기준) |
|---|---|---|---|
| 무기 | `GenerateWeapon` | 4 | `gdc+0xF0` |
| 갑옷 | `GenerateArmor` | 4 | `gdc+0xF8` |
| 투구 | `GenerateHelmet` | 4 | `gdc+0x100` |
| 신발 | `GenerateShoes` | 4 | `gdc+0x108` |
| 장신구 | `GenerateDecoration` | 4 | (static 6종: 향낭/옥선/반지/옥패/요대/면구) |
| 마구 | `GenerateHorseArmorData` | 2 | (static 1종: 안구) |

- CT cmd=4 register 순서: `edx=bossLv, r8d=dbIndex, xmm3=qualityRate`.
- **C# 시그니처 (디컴파일 ref)**: `gC.GenerateWeapon(itemLv, 0, 0, bossLv, player)`
  → reflection 호출 시 이쪽 우선. param count 5 일 수 있음 — spike 확인.
- `MT.items.addEquipment` 대안: DB 템플릿에서 ItemData 필드 직접 복사 +
  EquipmentData clone + 인벤 최고 장비의 baseAddData/extraAddData 포인터 복사
  (스탯 생성 회피). 우리는 generator 직접 호출이 더 깔끔 — 이 복사 방식은 fallback.

### 2.2 비급 (Book)
- `ItemData.SetBookData(skillID, rareLv)` — 2 params. 빈 ItemData(type=3) 생성 후
  호출. 또는 `gc.GenerateBook(itemLv, bossLv, -1, null)`.
- skillID → 무공 매핑은 우리 `SkillNameCache` (134+ entry, type 0~8) 이미 보유.

### 2.3 소비/재료/탈것
| 종류 | gc 메서드 | params | 비고 |
|---|---|---|---|
| 단약 | `GenerateMedData(id, bossLv)` | 2 | id = medDB index |
| 음식 | `GenerateFoodData(id, bossLv)` | 2 | id = foodDB index |
| 재료 | `GenerateMaterial(materialType, itemLv, bossLv)` | 3 | |
| 말 | `GenerateHorseData(id, bossLv)` | 2 | 생성 후 `horseData.tameRate(+0x3C)=1.0` (100% 길들임) |
| 보물 | `GenerateTreasure(typeIdx, rareLv, bossLv)` | 3 | |

### 2.4 추가 후 처리
1. `player.itemListData.GetItem(newItem, false)` — 인벤 추가 (game-self, cache 정상).
2. `FinalizeNewItemWrapper(newItem)` 권장 — `isNew=true` + `CountValueAndWeight()`
   (v0.7.12.2/3 새 캐릭터 save cleanup 회피와 동일 이유).
3. 말은 `tameRate=1.0` 추가 set.

---

## 3. 이벤트 생성 — `WorldEventController` (이식 대상 ②)

### 3.1 경로
- 싱글톤: `WorldEventController.Instance` (CT: klass+0xB8 static → instance).
  우리는 `WorldEventController.Instance` reflection 직접 접근 (HeroLocator 패턴).
- 템플릿 DB: `wecInst.dbList (+0x18)` = `List<EventData>`. index 로 템플릿 선택.
- 호출: **`wecInst.CreateWorldEvent(EventData template)`** (1-param 버전).
  1-param 버전이 영역 선택 + clone + 등록까지 모두 처리. CT 는 runtime_invoke 로
  부르지만 우리는 reflection 직접 invoke.

### 3.2 난이도 처리 (선택)
- 생성 전: `template.difficultyRate 객체(+0x50).field(+0x68) = difficulty` set →
  게임이 올바른 색상 아이콘 생성.
- 생성 후: 새 이벤트(eventList 의 마지막)의 `+0x64 = difficulty` 로 정확값 override.
- 생성 전후 origRate 백업/복원.

### 3.3 하드코딩된 월드 이벤트 templateIdx (CT v6 기준)
| idx | 이름 | idx | 이름 |
|---|---|---|---|
| 13 | 异草奇花 Rare Herb | 18 | 千里名驹 Famous Horse |
| 14 | 仙木灵果 Spirit Fruit | 19 | 武学奇才 Martial Prodigy |
| 15 | 失落宝藏 Lost Treasure | 20 | 世外高人 Hidden Master |
| 16 | 神兵现世 Divine Weapon | 21 | 皇家宝库 Royal Treasury |
| 17 | 失传秘籍 Lost Manual | 22 | 神秘石碑 Mysterious Stele |

- 별도 "대회(tournament)" 이벤트 타입도 있음 (search 기반, type="tournament").
- **권장**: 하드코딩 대신 `wecInst.dbList` 를 런타임 순회해 이름+idx 빌드 (우리
  SkillNameCache/ForceNameCache 패턴). 이름은 `EventData.name(+0x10)` 한자 →
  HangulDict 변환 가능.

---

## 4. (참고) 향후 후보 — 보류된 기능들

### 4.1 영웅 생성 + 모집 (★★, 사용자 미선택 — 향후)
- `gc.GenerateHeroData(...)` (6-param / 9-param 두 오버로드) → `HeroData`.
- `gc.ManagePlayerRecruitHero(hero, ...)` — 플레이어 문파로 모집.
- `gc.RandomGenerateNPCSkill(hero)` / `RandomGenerateNPCItem(hero)` — 무공/장비 채움.
- `gc.WorldAddNewHero(forceID=-1, heroForceLv, outSideForce=false)` — 무소속 방랑자.
- 4-메서드 체인이라 spike 필요. UI: force(문파) + level + sex + age + loyalty + stats.

### 4.2 자원 치트 (★★, 보류)
| 항목 | 위치 (CT raw offset) | reflection 후보 |
|---|---|---|
| 돈(银两) | `itemListData+0x18` (int) | `itemListData.money` |
| 운철(陨铁) | `worldData+0x228` (int) | `worldData.meteorite` |
| 문파공헌(贡献) | `hero+0x1C0` (float) | `hero.sectContrib` |
| 전 문파 공헌 | `forceData+0x170` (float) 순회 | |
| maxNpcFavor | HerosList 순회 `npc+0x124=100` | `hero.favor` |
| maxFactionAffinity | forceData favor list 순회 | |

### 4.3 실시간 토글 (★, 보류 — 별도 "live cheat" 모드)
battleSpeed(`worldData+0x1D0`, 50ms 타이머) / combatExp(tag222 buffData dict
key 176·177) / livingExp(tag243 key 178) / enemyOneHP(BattleController) /
horseSpeed / infiniteStamina / dungeonReveal.

---

## 5. 우리 모드 기존 자산 (이식 시 재사용)
- `HeroLocator` — GameDataController.Instance / 싱글톤 reflection 접근 패턴.
  → `GameController.Instance` / `WorldEventController.Instance` 도 동일 패턴.
- `ItemListApplier.FinalizeNewItemWrapper` (v0.7.12.2/3) — 생성 아이템 추가 후 처리.
- `ItemListApplier` / `ContainerOps` — `GetItem` 인벤 추가 경로.
- `SkillNameCache` (무공 type 0~8) / `ForceNameCache` (문파) — Book 생성 + 영웅 force.
- `HangulDict` — 생성 아이템/이벤트 한자명 → 한글.
- `SelectorDialog` (2단계 탭 + 검색) — 카테고리/항목 선택 UI.
- `ItemGenerator.Generate(ItemCategory, itemLv, bossLv)` 매핑 — 기존
  `2026-05-05-v075-cheat-feature-reference.md` §2 에 12 카테고리 정리됨.

## 6. 다음 단계
1. ✅ 이 dump 작성.
2. brainstorm — 아이템 생성 + 이벤트 생성 sub-project 의 UX/scope/진입점.
   - 어디에 둘지: F11 신규 메뉴 항목 vs PlayerEditorPanel 탭 vs ContainerPanel.
   - generator 메서드 reflection 호출 spike (param count / 시그니처 확정).
   - 카테고리·등급·레벨 입력 UI (CT Items 탭 참조: dropdown + level + rarity + 생성 버튼).
3. spec → plan → impl → 인게임 smoke.
