namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — 검색·정렬 상태. immutable. cache invalidate 의 source-of-truth.
/// 세션 휘발 (저장 안 함). v0.7.6 영속화 시 직렬화 합류.
/// </summary>
public sealed class SearchSortState
{
    public static readonly SearchSortState Default = new("", SortKey.Category, true, -1, false, -1);

    public string  Search             { get; }
    public SortKey Key                { get; }
    public bool    Ascending          { get; }

    // v0.7.11 Cat 4B/4E — 등급 범위 + 착용중 제외 filter
    public int     MinGradeOrder      { get; }   // -1 = 전체, 0~5 = 등급 (열악/보통/정량/비전/정극/절세)
    public bool    ExcludeEquipped    { get; }
    // v0.7.11 Cat 4G — 무공 secondary tab (item.SubType 매칭). -1 = 전체, 0~8 = 9 무공 type (내공/.../사술).
    // 카테고리 = Book 일 때만 ContainerPanel 이 secondary tab 표시. ApplyView 는 unconditional 적용
    // (다른 카테고리 row 는 SubType 가 다른 의미라 필터 결과 0 일 수 있어 사용자 혼란 방지하려면
    // ContainerPanel 이 Book 외에서 -1 reset 책임 — 본 클래스는 단순 filter only).
    public int     KungfuTypeFilter   { get; }

    public SearchSortState(string search, SortKey key, bool ascending,
                           int minGradeOrder = -1, bool excludeEquipped = false,
                           int kungfuTypeFilter = -1)
    {
        Search           = search ?? "";
        Key              = key;
        Ascending        = ascending;
        MinGradeOrder    = minGradeOrder;
        ExcludeEquipped  = excludeEquipped;
        KungfuTypeFilter = kungfuTypeFilter;
    }

    public SearchSortState WithSearch(string text)         => new(text ?? "", Key, Ascending, MinGradeOrder, ExcludeEquipped, KungfuTypeFilter);
    public SearchSortState WithKey(SortKey k)              => new(Search, k, Ascending, MinGradeOrder, ExcludeEquipped, KungfuTypeFilter);
    public SearchSortState ToggleDirection()               => new(Search, Key, !Ascending, MinGradeOrder, ExcludeEquipped, KungfuTypeFilter);
    public SearchSortState WithMinGradeOrder(int v)        => new(Search, Key, Ascending, v, ExcludeEquipped, KungfuTypeFilter);
    public SearchSortState WithExcludeEquipped(bool b)     => new(Search, Key, Ascending, MinGradeOrder, b, KungfuTypeFilter);
    public SearchSortState WithKungfuTypeFilter(int v)     => new(Search, Key, Ascending, MinGradeOrder, ExcludeEquipped, v);

    public override int GetHashCode()
        => System.HashCode.Combine(Search, Key, Ascending, MinGradeOrder, ExcludeEquipped, KungfuTypeFilter);

    public override bool Equals(object? obj)
        => obj is SearchSortState s
            && s.Search == Search && s.Key == Key && s.Ascending == Ascending
            && s.MinGradeOrder == MinGradeOrder && s.ExcludeEquipped == ExcludeEquipped
            && s.KungfuTypeFilter == KungfuTypeFilter;
}
