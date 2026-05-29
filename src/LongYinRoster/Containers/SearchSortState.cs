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

    // 카테고리별 secondary tab 필터 값. -1 = 전체. 비교 대상 필드 (SubType vs KungfuType)
    // 는 ContainerView 에서 ItemCategory context 로 분기 — CategorySecondaryTabs 참고.
    public int     SecondaryFilter    { get; }

    public SearchSortState(string search, SortKey key, bool ascending,
                           int minGradeOrder = -1, bool excludeEquipped = false,
                           int secondaryFilter = -1)
    {
        Search           = search ?? "";
        Key              = key;
        Ascending        = ascending;
        MinGradeOrder    = minGradeOrder;
        ExcludeEquipped  = excludeEquipped;
        SecondaryFilter  = secondaryFilter;
    }

    public SearchSortState WithSearch(string text)         => new(text ?? "", Key, Ascending, MinGradeOrder, ExcludeEquipped, SecondaryFilter);
    public SearchSortState WithKey(SortKey k)              => new(Search, k, Ascending, MinGradeOrder, ExcludeEquipped, SecondaryFilter);
    public SearchSortState ToggleDirection()               => new(Search, Key, !Ascending, MinGradeOrder, ExcludeEquipped, SecondaryFilter);
    public SearchSortState WithMinGradeOrder(int v)        => new(Search, Key, Ascending, v, ExcludeEquipped, SecondaryFilter);
    public SearchSortState WithExcludeEquipped(bool b)     => new(Search, Key, Ascending, MinGradeOrder, b, SecondaryFilter);
    public SearchSortState WithSecondaryFilter(int v)      => new(Search, Key, Ascending, MinGradeOrder, ExcludeEquipped, v);

    public override int GetHashCode()
        => System.HashCode.Combine(Search, Key, Ascending, MinGradeOrder, ExcludeEquipped, SecondaryFilter);

    public override bool Equals(object? obj)
        => obj is SearchSortState s
            && s.Search == Search && s.Key == Key && s.Ascending == Ascending
            && s.MinGradeOrder == MinGradeOrder && s.ExcludeEquipped == ExcludeEquipped
            && s.SecondaryFilter == SecondaryFilter;
}
