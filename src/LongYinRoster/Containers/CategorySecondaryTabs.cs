using System;
using System.Collections.Generic;

namespace LongYinRoster.Containers;

/// <summary>
/// 카테고리별 secondary tab 정의. 각 tuple (라벨, 필터 값).
///
/// subType 매핑은 실제 save data (Hero_PlayerExport.json) 샘플로 검증:
/// - 장비(0): 0=무기(大剑·钩爪) / 1=갑옷(布甲·轻甲) / 2=투구(斗笠) / 3=신발(短靴) / 4=장신구(香囊·반지)
/// - 음식(2): 0=음식(汤·乳猪) / 1=술(龙脑酒)
/// - 재료(5): 0=목재(木材) / 1=광석(矿料) / 2=약재(药引) / 3=식재(食材)
///
/// 비급(3) 은 item.subType 가 무공 type 과 매핑되지 않음 → ItemRow.KungfuType
/// (bookData.skillID → SkillNameCache.GetType) 사용. ContainerView 가 카테고리로 분기.
///
/// 단약(1) / 보물(4) / 말(6) / 기타(99) = secondary tab 없음 (game 데이터상 subType 분류 무의미).
/// </summary>
public static class CategorySecondaryTabs
{
    public static IReadOnlyList<(string Label, int Value)> ForCategory(ItemCategory cat) => cat switch
    {
        ItemCategory.Equipment => EquipmentTabs,
        ItemCategory.Food      => FoodTabs,
        ItemCategory.Book      => BookTabs,
        ItemCategory.Material  => MaterialTabs,
        _                      => Array.Empty<(string, int)>(),
    };

    private static readonly (string, int)[] EquipmentTabs =
    {
        ("무기",   0),
        ("갑옷",   1),
        ("투구",   2),
        ("신발",   3),
        ("장신구", 4),
    };

    private static readonly (string, int)[] FoodTabs =
    {
        ("음식", 0),
        ("술",   1),
    };

    /// <summary>비급 secondary tab — 9 무공 type (SkillNameCache.TypeNames 순서).</summary>
    private static readonly (string, int)[] BookTabs =
    {
        ("내공", 0), ("경공", 1), ("절기", 2), ("권장", 3),
        ("검법", 4), ("도법", 5), ("장병", 6), ("기문", 7), ("사술", 8),
    };

    private static readonly (string, int)[] MaterialTabs =
    {
        ("목재", 0),
        ("광석", 1),
        ("약재", 2),
        ("식재", 3),
    };
}
