using System.Collections.Generic;

namespace LongYinRoster.Core;

/// <summary>
/// PinpointPatcher.SetSimpleFields 가 처리할 simple-value scalar 매트릭스.
///
/// 본 entry list 는 spec §7.2.1 의 "Step 1 SetSimpleFields" mapping 과 1:1.
/// dump 결과 (plan Task 2) 로 채워졌다. 새 필드 추가 / 제거 시 spec §7.2.1 도 동기화.
///
/// SetterStyle:
///   Direct  : InvokeSetter(player, method, newValue)
///   Delta   : InvokeSetter(player, method, newValue - currentValue)
///   None    : 직접 set 경로 없음 — RefreshSelfState 가 derived 로 재계산 기대
/// </summary>
public enum SetterStyle { Direct, Delta, None }

public sealed record SimpleFieldEntry(
    string      Name,
    string      JsonPath,
    string      PropertyName,
    System.Type Type,
    string?     SetterMethod,
    SetterStyle SetterStyle);

public static class SimpleFieldMatrix
{
    /// <summary>
    /// dump 결과로 확정된 매트릭스. plan Task 2 직후 채워진다. 빈 list 면 schema test
    /// 실패 — Task 2 가 완료 안 됐다는 신호.
    ///
    /// spec §7.2.1 Step 1 의 22 entry 와 1:1 (table row 순서 유지).
    /// 특이 케이스:
    /// - `nowActiveSkill`: SetterMethod=null, Style=None — Step 2 후 별도 처리.
    /// - `skinID`: JsonPath/PropertyName 은 skinID 만 표시 (대표). SetSkin 은 multi-arg
    ///   (skinID, skinLv) — Task 7 SetSimpleFields 가 special-case 로 skinLv 도 같이 read.
    /// - `baseAttri[i]` / `baseFightSkill[i]` / `baseLivingSkill[i]` / `expLivingSkill[i]`:
    ///   List 의 각 index 를 ChangeAttri / ChangeFightSkill / ... 의 (i, delta, ...) 로
    ///   호출. JsonPath 는 list-name (Task 7 이 enumerate).
    /// </summary>
    public static readonly IReadOnlyList<SimpleFieldEntry> Entries = new[]
    {
        new SimpleFieldEntry("명예",            "fame",                "fame",                typeof(float), "ChangeFame",                 SetterStyle.Delta),
        new SimpleFieldEntry("악명",            "badFame",             "badFame",             typeof(float), "ChangeBadFame",              SetterStyle.Delta),
        new SimpleFieldEntry("영예 lv",         "hornorLv",            "hornorLv",            typeof(int),   "ChangeHornorLv",             SetterStyle.Delta),
        new SimpleFieldEntry("통치 lv",         "governLv",            "governLv",            typeof(int),   "ChangeGovernLv",             SetterStyle.Delta),
        new SimpleFieldEntry("HP",              "hp",                  "hp",                  typeof(float), "ChangeHp",                   SetterStyle.Delta),
        new SimpleFieldEntry("Mana",            "mana",                "mana",                typeof(float), "ChangeMana",                 SetterStyle.Delta),
        new SimpleFieldEntry("Power",           "power",               "power",               typeof(float), "ChangePower",                SetterStyle.Delta),
        new SimpleFieldEntry("외상",            "externalInjury",      "externalInjury",      typeof(float), "ChangeExternalInjury",       SetterStyle.Delta),
        new SimpleFieldEntry("내상",            "internalInjury",      "internalInjury",      typeof(float), "ChangeInternalInjury",       SetterStyle.Delta),
        new SimpleFieldEntry("중독",            "poisonInjury",        "poisonInjury",        typeof(float), "ChangePoisonInjury",         SetterStyle.Delta),
        new SimpleFieldEntry("충성",            "loyal",               "loyal",               typeof(float), "ChangeLoyal",                SetterStyle.Delta),
        new SimpleFieldEntry("호감",            "favor",               "favor",               typeof(float), "SetFavor",                   SetterStyle.Direct),
        new SimpleFieldEntry("가문 공헌",       "forceContribution",   "forceContribution",   typeof(float), "ChangeForceContribution",    SetterStyle.Delta),
        new SimpleFieldEntry("통치 공헌",       "governContribution",  "governContribution",  typeof(float), "ChangeGovernContribution",   SetterStyle.Delta),
        new SimpleFieldEntry("자기집 add",      "selfHouseTotalAdd",   "selfHouseTotalAdd",   typeof(float), "ChangeSelfHouseTotalAdd",    SetterStyle.Delta),
        new SimpleFieldEntry("천부 포인트",     "heroTagPoint",        "heroTagPoint",        typeof(float), "ChangeTagPoint",             SetterStyle.Delta),
        new SimpleFieldEntry("활성 무공",       "nowActiveSkill",      "nowActiveSkill",      typeof(int),   null,                         SetterStyle.None),
        new SimpleFieldEntry("스킨",            "skinID",              "skinID",              typeof(int),   "SetSkin",                    SetterStyle.Direct),
        new SimpleFieldEntry("baseAttri[i]",    "baseAttri",           "baseAttri",           typeof(float), "ChangeAttri",                SetterStyle.Delta),
        new SimpleFieldEntry("baseFightSkill[i]","baseFightSkill",     "baseFightSkill",      typeof(float), "ChangeFightSkill",           SetterStyle.Delta),
        new SimpleFieldEntry("baseLivingSkill[i]","baseLivingSkill",   "baseLivingSkill",     typeof(float), "ChangeLivingSkill",          SetterStyle.Delta),
        new SimpleFieldEntry("expLivingSkill[i]","expLivingSkill",     "expLivingSkill",      typeof(float), "ChangeLivingSkillExp",       SetterStyle.Delta),
    };
}
