using System;
using System.Collections.Generic;
using System.Linq;

namespace LongYinRoster.Core;

/// <summary>v0.7.13 — 생성 가능 아이템 DB entry (1회 열거 캐시 단위).</summary>
public sealed class ItemGenEntry
{
    public int Id { get; init; }                  // skillID(비급) 또는 DB index/itemID
    public string NameRaw { get; init; } = "";
    public string? NameKr { get; init; }
    public ItemGenCategory Category { get; init; }
    public int SubType { get; init; }
    public int RareLvDefault { get; init; }

    public string Display => string.IsNullOrEmpty(NameKr) ? NameRaw : NameKr!;
}

/// <summary>v0.7.13 — 순수 필터/페이징 (ItemGeneratorPanel 이 사용, 테스트 가능).</summary>
public static class ItemGenFilter
{
    public static List<ItemGenEntry> Apply(
        IEnumerable<ItemGenEntry> entries, ItemGenCategory category, int secondary, string? search)
    {
        IEnumerable<ItemGenEntry> q = entries.Where(e => e.Category == category);
        if (secondary >= 0) q = q.Where(e => e.SubType == secondary);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            q = q.Where(e => (e.NameKr != null && e.NameKr.Contains(s, StringComparison.OrdinalIgnoreCase))
                          || e.NameRaw.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        return q.ToList();
    }

    public static (List<ItemGenEntry> Slice, int TotalPages) Page(
        IReadOnlyList<ItemGenEntry> filtered, int page, int pageSize)
    {
        int total = (filtered.Count + pageSize - 1) / pageSize;
        if (total == 0) total = 1;
        if (page >= total) page = total - 1;
        if (page < 0) page = 0;
        int start = page * pageSize;
        int end = Math.Min(start + pageSize, filtered.Count);
        var slice = new List<ItemGenEntry>();
        for (int i = start; i < end; i++) slice.Add(filtered[i]);
        return (slice, total);
    }
}
