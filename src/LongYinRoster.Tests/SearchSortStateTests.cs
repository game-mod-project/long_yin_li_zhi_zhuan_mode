using Xunit;
using LongYinRoster.Containers;

namespace LongYinRoster.Tests;

public class SearchSortStateTests
{
    [Fact]
    public void Default_state_has_empty_search_category_key_and_ascending()
    {
        var s = SearchSortState.Default;
        Assert.Equal("", s.Search);
        Assert.Equal(SortKey.Category, s.Key);
        Assert.True(s.Ascending);
    }

    [Fact]
    public void WithSearch_returns_new_instance_with_updated_text_only()
    {
        var s = SearchSortState.Default;
        var s2 = s.WithSearch("檢");
        Assert.Equal("", s.Search);     // 원본 불변
        Assert.Equal("檢", s2.Search);
        Assert.Equal(s.Key, s2.Key);
        Assert.Equal(s.Ascending, s2.Ascending);
    }

    [Fact]
    public void WithKey_and_ToggleDirection_compose_correctly()
    {
        var s = SearchSortState.Default
            .WithKey(SortKey.Grade)
            .ToggleDirection();
        Assert.Equal(SortKey.Grade, s.Key);
        Assert.False(s.Ascending);
    }
}
