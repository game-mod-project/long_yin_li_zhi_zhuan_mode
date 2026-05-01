using System.Collections.Generic;

namespace LongYinRoster.Core;

public enum SetterStyle { Direct, Delta, None }

/// <summary>
/// v0.4 — entry 의 selection 분류. None 은 "영구 보존" (부상/충성/호감).
/// Stat / Honor / Skin / SelfHouse 는 ApplySelection 의 동명 flag 따라 selection.
/// TalentPoint 는 ApplySelection.TalentTag 와 묶여 selection (heroTagPoint 가 천부 카테고리 안).
/// </summary>
public enum FieldCategory
{
    None,         // 부상/충성/호감 — Apply 안 함, 영구 보존
    Stat,         // hp/mana/power + base stat lists
    Honor,        // fame/badFame
    Skin,         // skinID
    SelfHouse,    // selfHouseTotalAdd
    TalentPoint,  // heroTagPoint
    Appearance,   // v0.5 — portraitID + gender (외형 PoC PASS 시 entry 추가)
}

public sealed record SimpleFieldEntry(
    string         Name,
    string         JsonPath,
    string         PropertyName,
    System.Type    Type,
    string?        SetterMethod,
    SetterStyle    SetterStyle,
    FieldCategory  Category);

/// <summary>
/// v0.4: 17 entry. v0.3 18 entry 에서 활성 무공 (nowActiveSkill) 제거 — 별도 step (SetActiveKungfu) 로 이관.
/// 부상/충성/호감 5 entry 의 Category=None — Apply 안 함 (v0.3 backup 폐기, 영구 보존 정책).
///
/// PinpointPatcher.SetSimpleFields 가 entry.Category 와 ApplySelection 비교하여 selection filter.
/// SetterStyle:
///   Direct  : InvokeSetter(player, method, newValue)
///   Delta   : InvokeSetter(player, method, newValue - currentValue)
///   None    : 직접 set 경로 없음 — RefreshSelfState 가 derived 로 재계산 기대
/// </summary>
public static class SimpleFieldMatrix
{
    public static readonly IReadOnlyList<SimpleFieldEntry> Entries = new[]
    {
        new SimpleFieldEntry("명예",              "fame",                "fame",                typeof(float), "ChangeFame",                 SetterStyle.Delta,  FieldCategory.Honor),
        new SimpleFieldEntry("악명",              "badFame",             "badFame",             typeof(float), "ChangeBadFame",              SetterStyle.Delta,  FieldCategory.Honor),
        new SimpleFieldEntry("HP",                "hp",                  "hp",                  typeof(float), "ChangeHp",                   SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("Mana",              "mana",                "mana",                typeof(float), "ChangeMana",                 SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("Power",             "power",               "power",               typeof(float), "ChangePower",                SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("외상",              "externalInjury",      "externalInjury",      typeof(float), "ChangeExternalInjury",       SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("내상",              "internalInjury",      "internalInjury",      typeof(float), "ChangeInternalInjury",       SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("중독",              "poisonInjury",        "poisonInjury",        typeof(float), "ChangePoisonInjury",         SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("충성",              "loyal",               "loyal",               typeof(float), "ChangeLoyal",                SetterStyle.Delta,  FieldCategory.None),
        new SimpleFieldEntry("호감",              "favor",               "favor",               typeof(float), "SetFavor",                   SetterStyle.Direct, FieldCategory.None),
        new SimpleFieldEntry("자기집 add",        "selfHouseTotalAdd",   "selfHouseTotalAdd",   typeof(float), "ChangeSelfHouseTotalAdd",    SetterStyle.Delta,  FieldCategory.SelfHouse),
        new SimpleFieldEntry("천부 포인트",       "heroTagPoint",        "heroTagPoint",        typeof(float), "ChangeTagPoint",             SetterStyle.Delta,  FieldCategory.TalentPoint),
        new SimpleFieldEntry("스킨",              "skinID",              "skinID",              typeof(int),   "SetSkin",                    SetterStyle.Direct, FieldCategory.Skin),
        new SimpleFieldEntry("baseAttri[i]",      "baseAttri",           "baseAttri",           typeof(float), "ChangeAttri",                SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("baseFightSkill[i]", "baseFightSkill",      "baseFightSkill",      typeof(float), "ChangeFightSkill",           SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("baseLivingSkill[i]","baseLivingSkill",     "baseLivingSkill",     typeof(float), "ChangeLivingSkill",          SetterStyle.Delta,  FieldCategory.Stat),
        new SimpleFieldEntry("expLivingSkill[i]", "expLivingSkill",      "expLivingSkill",      typeof(float), "ChangeLivingSkillExp",       SetterStyle.Delta,  FieldCategory.Stat),
    };
}
