namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.3 D-2 — type/subType → 카테고리 한자 1자. ItemCellRenderer 의 placeholder cell
/// 중앙 글자에 사용. 장비 subType 세분 (무기/갑옷/투구/신발/장신구) 은 v0.7.4 D-1 시점 정밀화.
/// </summary>
public static class CategoryGlyph
{
    public static string For(int type, int subType) => type switch
    {
        0 => "装",                              // Equipment
        2 => subType == 0 ? "药" : "食",        // Medicine / Food
        3 => "书",                              // Book
        4 => "宝",                              // Treasure
        5 => "材",                              // Material
        6 => "马",                              // Horse
        _ => "·",                               // Other (type=1 등 미분류)
    };
}
