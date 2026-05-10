namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.3 D-2 — type/subType → 카테고리 한글 라벨 (1-2자). ItemCellRenderer 의 가로 직사각형
/// (48×24, v0.7.5.2 부터) cell 가운데 표시.
/// v0.7.5.2 변경: 한자 1자 (装/书/药/食/宝/材/马) → 한글 라벨 (장비/단약/음식/비급/보물/재료/말).
/// </summary>
public static class CategoryGlyph
{
    // v0.7.11.1 fix: 카테고리 매핑 swap 수정 (ItemCategoryFilter 와 일관 유지).
    //   type=1 → 단약 pill (보혈단/통락단/황련환 등) — 이전 "기타" 로 잘못 분류
    //   type=2 subType=0 → 음식 (통돼지구이/사군자탕 등) — 이전 "단약" swap
    //   type=2 subType≥1 → 단약 약주 (용뇌주/두강주 등) — 이전 "음식" swap
    public static string For(int type, int subType) => type switch
    {
        0 => "장비",                              // Equipment
        1 => "단약",                              // Medicine pill
        2 => subType == 0 ? "음식" : "단약",      // Food / Medicine wine
        3 => "비급",                              // Book
        4 => "보물",                              // Treasure
        5 => "재료",                              // Material
        6 => "말",                                // Horse
        _ => "기타",                              // 미분류
    };
}
