namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.3 D-2 — type/subType → 카테고리 한글 라벨 (1-2자). ItemCellRenderer 의 가로 직사각형
/// (48×24, v0.7.5.2 부터) cell 가운데 표시.
/// v0.7.5.2 변경: 한자 1자 (装/书/药/食/宝/材/马) → 한글 라벨 (장비/단약/음식/비급/보물/재료/말).
/// </summary>
public static class CategoryGlyph
{
    public static string For(int type, int subType) => type switch
    {
        0 => "장비",                              // Equipment
        2 => subType == 0 ? "단약" : "음식",      // Medicine / Food
        3 => "비급",                              // Book
        4 => "보물",                              // Treasure
        5 => "재료",                              // Material
        6 => "말",                                // Horse
        _ => "기타",                              // Other (type=1 등 미분류)
    };
}
