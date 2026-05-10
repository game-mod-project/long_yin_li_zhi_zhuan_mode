namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.3 D-2 — type/subType → 카테고리 한글 라벨 (1-2자). ItemCellRenderer 의 가로 직사각형
/// (48×24, v0.7.5.2 부터) cell 가운데 표시.
/// v0.7.5.2 변경: 한자 1자 (装/书/药/食/宝/材/马) → 한글 라벨 (장비/단약/음식/비급/보물/재료/말).
/// </summary>
public static class CategoryGlyph
{
    // v0.7.11.2 fix (재수정): 사용자 인게임 분류와 일치.
    //   type=1 → 단약 pill (보혈단/통락단/황련환 등)
    //   type=2 → 음식 전체 (food + wines, subType 무관). 인게임 "음식" 탭 = wine 포함.
    public static string For(int type, int subType) => type switch
    {
        0 => "장비",                              // Equipment
        1 => "단약",                              // Medicine pill only
        2 => "음식",                              // Food (food + wines, subType 무관)
        3 => "비급",                              // Book
        4 => "보물",                              // Treasure
        5 => "재료",                              // Material
        6 => "말",                                // Horse
        _ => "기타",                              // 미분류
    };
}
