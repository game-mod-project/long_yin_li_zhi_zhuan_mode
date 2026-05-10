namespace LongYinRoster.Containers;

public enum ItemCategory
{
    All       = -1,
    Equipment = 0,   // type=0
    Medicine  = 1,   // type=1 (pills) OR type=2 subType≥1 (medicinal wine)
    Food      = 2,   // type=2 subType=0
    Book      = 3,   // type=3
    Treasure  = 4,   // type=4
    Material  = 5,   // type=5
    Horse     = 6,   // type=6
    Other     = 99,  // 미분류
}

public static class ItemCategoryFilter
{
    /// <summary>
    /// v0.7.11.1 fix: 카테고리 매핑 swap 수정.
    /// 게임 schema:
    ///   type=1 → 단약 pill (보혈단/통락단/황련환/지렬산 등) — 이전 "Other/기타" 로 잘못 분류
    ///   type=2 subType=0 → 음식 (통돼지구이/사군자탕/팔진회/고기만두 등) — 이전 "Medicine/단약" 으로 swap
    ///   type=2 subType≥1 → 단약 약주 (용뇌주/두강주 등) — 이전 "Food/음식" 으로 swap
    /// </summary>
    public static ItemCategory Classify(int type, int subType) => type switch
    {
        0 => ItemCategory.Equipment,
        1 => ItemCategory.Medicine,                                                // 단약 pill
        2 => subType == 0 ? ItemCategory.Food : ItemCategory.Medicine,             // 음식 / 약주
        3 => ItemCategory.Book,
        4 => ItemCategory.Treasure,
        5 => ItemCategory.Material,
        6 => ItemCategory.Horse,
        _ => ItemCategory.Other,
    };

    public static bool Matches(ItemCategory filter, int type, int subType)
    {
        if (filter == ItemCategory.All) return true;
        return Classify(type, subType) == filter;
    }

    public static string KoreanLabel(ItemCategory c) => c switch
    {
        ItemCategory.All       => "전체",
        ItemCategory.Equipment => "장비",
        ItemCategory.Medicine  => "단약",
        ItemCategory.Food      => "음식",
        ItemCategory.Book      => "비급",
        ItemCategory.Treasure  => "보물",
        ItemCategory.Material  => "재료",
        ItemCategory.Horse     => "말",
        ItemCategory.Other     => "기타",
        _ => "?",
    };

    /// <summary>v0.7.6 — Config string ↔ enum parsing. invalid 값 → All fallback.</summary>
    public static ItemCategory ParseOrDefault(string s) => s switch
    {
        "All"       => ItemCategory.All,
        "Equipment" => ItemCategory.Equipment,
        "Medicine"  => ItemCategory.Medicine,
        "Food"      => ItemCategory.Food,
        "Book"      => ItemCategory.Book,
        "Treasure"  => ItemCategory.Treasure,
        "Material"  => ItemCategory.Material,
        "Horse"     => ItemCategory.Horse,
        "Other"     => ItemCategory.Other,
        _           => ItemCategory.All,
    };
}
