namespace LongYinRoster.Containers;

/// <summary>
/// v0.7.2 — ContainerPanel 정렬 키. spec §3.3 (사용자 확정 4 키).
/// </summary>
public enum SortKey
{
    Category = 0,
    Name     = 1,
    Grade    = 2,
    Quality  = 3,
}

/// <summary>v0.7.6 — Config string ↔ enum parsing.</summary>
public static class SortKeyParser
{
    public static SortKey ParseOrDefault(string s) => s switch
    {
        "Category" => SortKey.Category,
        "Name"     => SortKey.Name,
        "Grade"    => SortKey.Grade,
        "Quality"  => SortKey.Quality,
        _          => SortKey.Category,
    };
}
