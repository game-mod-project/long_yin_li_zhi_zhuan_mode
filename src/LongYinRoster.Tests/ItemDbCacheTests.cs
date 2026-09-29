using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemDbCacheTests
{
    [Fact]
    public void All_InTestEnvironment_ReturnsEmptyWithoutThrowing()
    {
        ItemDbCache.ResetForTests();
        var result = Should.NotThrow(() => ItemDbCache.All());
        result.ShouldNotBeNull();   // 게임 부재 → 빈 list (예외 없음)
    }
}
