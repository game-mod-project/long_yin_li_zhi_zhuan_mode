namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — 검색·정렬 상태. immutable. cache invalidate 의 source-of-truth.
/// 세션 휘발 (저장 안 함). v0.7.6 영속화 시 직렬화 합류.
/// </summary>
public sealed class SearchSortState
{
    public static readonly SearchSortState Default = new("", SortKey.Category, true);

    public string  Search    { get; }
    public SortKey Key       { get; }
    public bool    Ascending { get; }

    public SearchSortState(string search, SortKey key, bool ascending)
    {
        Search    = search ?? "";
        Key       = key;
        Ascending = ascending;
    }

    public SearchSortState WithSearch(string text)  => new(text ?? "", Key, Ascending);
    public SearchSortState WithKey(SortKey k)       => new(Search, k, Ascending);
    public SearchSortState ToggleDirection()        => new(Search, Key, !Ascending);

    public override int GetHashCode()
        => System.HashCode.Combine(Search, Key, Ascending);

    public override bool Equals(object? obj)
        => obj is SearchSortState s
            && s.Search == Search && s.Key == Key && s.Ascending == Ascending;
}
