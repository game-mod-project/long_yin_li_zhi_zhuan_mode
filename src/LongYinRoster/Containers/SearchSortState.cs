namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — 검색·정렬 상태. immutable. cache invalidate 의 source-of-truth.
/// 세션 휘발 (저장 안 함). v0.7.6 영속화 시 직렬화 합류.
/// </summary>
public sealed class SearchSortState
{
    public static readonly SearchSortState Default = new("", SortKey.Category, true, -1, false);

    public string  Search          { get; }
    public SortKey Key             { get; }
    public bool    Ascending       { get; }

    // v0.7.11 Cat 4B/4E — 등급 범위 + 착용중 제외 filter
    public int     MinGradeOrder   { get; }   // -1 = 전체, 0~5 = 등급 (열악/보통/정량/비전/정극/절세)
    public bool    ExcludeEquipped { get; }

    public SearchSortState(string search, SortKey key, bool ascending,
                           int minGradeOrder = -1, bool excludeEquipped = false)
    {
        Search          = search ?? "";
        Key             = key;
        Ascending       = ascending;
        MinGradeOrder   = minGradeOrder;
        ExcludeEquipped = excludeEquipped;
    }

    public SearchSortState WithSearch(string text)         => new(text ?? "", Key, Ascending, MinGradeOrder, ExcludeEquipped);
    public SearchSortState WithKey(SortKey k)              => new(Search, k, Ascending, MinGradeOrder, ExcludeEquipped);
    public SearchSortState ToggleDirection()               => new(Search, Key, !Ascending, MinGradeOrder, ExcludeEquipped);
    public SearchSortState WithMinGradeOrder(int v)        => new(Search, Key, Ascending, v, ExcludeEquipped);
    public SearchSortState WithExcludeEquipped(bool b)     => new(Search, Key, Ascending, MinGradeOrder, b);

    public override int GetHashCode()
        => System.HashCode.Combine(Search, Key, Ascending, MinGradeOrder, ExcludeEquipped);

    public override bool Equals(object? obj)
        => obj is SearchSortState s
            && s.Search == Search && s.Key == Key && s.Ascending == Ascending
            && s.MinGradeOrder == MinGradeOrder && s.ExcludeEquipped == ExcludeEquipped;
}
