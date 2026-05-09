namespace LongYinRoster.Containers;

public enum ItemCategory
{
    All       = -1,
    Equipment = 0,   // type=0
    Medicine  = 1,   // type=2 subType=0
    Food      = 2,   // type=2 subType≥1
    Book      = 3,   // type=3
    Treasure  = 4,   // type=4
    Material  = 5,   // type=5
    Horse     = 6,   // type=6
    Other     = 99,  // type=1 등 미분류
}

public static class ItemCategoryFilter
{
    public static ItemCategory Classify(int type, int subType) => type switch
    {
        0 => ItemCategory.Equipment,
        2 => subType == 0 ? ItemCategory.Medicine : ItemCategory.Food,
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
