namespace LongYinRoster.Core;

/// <summary>v0.7.13 — 아이템 생성 카테고리 (게임 ItemType 매핑). CategoryGlyph.For 와 일관.</summary>
public enum ItemGenCategory
{
    Equipment = 0,  // 장비 (subType 0~4: 무기/갑옷/투구/신발/장신구, 5: 마구)
    Medicine  = 1,  // 단약
    Food      = 2,  // 음식
    Book      = 3,  // 비급
    Treasure  = 4,  // 보물
    Material  = 5,  // 재료
    Horse     = 6,  // 말
}

public static class ItemGenCategoryNames
{
    public static string Korean(ItemGenCategory c) => c switch
    {
        ItemGenCategory.Equipment => "장비",
        ItemGenCategory.Medicine  => "단약",
        ItemGenCategory.Food      => "음식",
        ItemGenCategory.Book      => "비급",
        ItemGenCategory.Treasure  => "보물",
        ItemGenCategory.Material  => "재료",
        ItemGenCategory.Horse     => "말",
        _ => "기타",
    };
}
