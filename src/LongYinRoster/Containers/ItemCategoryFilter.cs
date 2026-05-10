namespace LongYinRoster.Containers;

public enum ItemCategory
{
    All       = -1,
    Equipment = 0,   // type=0
    Medicine  = 1,   // type=1 only (pills)
    Food      = 2,   // type=2 (all subTypes — regular food + wines)
    Book      = 3,   // type=3
    Treasure  = 4,   // type=4
    Material  = 5,   // type=5
    Horse     = 6,   // type=6
    Other     = 99,  // 미분류
}

public static class ItemCategoryFilter
{
    /// <summary>
    /// v0.7.11.2 fix (재수정): 사용자 인게임 분류와 일치.
    ///   type=1 → 단약 pill (보혈단/통락단/황련환/형방환/지혈산 등). v0.7.11.1 에서 추가됨.
    ///   type=2 → **음식 전체** — regular food (통돼지구이/사군자탕/팔진회) + wines
    ///            (원숭이술/두강주/용뇌주/죽엽청/화조주). 인게임 stat panel 의 "음식" 탭이
    ///            wine 도 포함. v0.7.11.1 에서 subType 분기 (subType≥1 → 단약) 잘못 도입 → 제거.
    /// </summary>
    public static ItemCategory Classify(int type, int subType) => type switch
    {
        0 => ItemCategory.Equipment,
        1 => ItemCategory.Medicine,        // 단약 pill only
        2 => ItemCategory.Food,            // 음식 (food + wines, subType 무관)
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
