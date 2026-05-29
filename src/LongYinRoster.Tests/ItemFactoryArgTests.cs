using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemFactoryArgTests
{
    [Fact]
    public void EquipmentArgs_LevelRarityShape()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Equipment, subType: 0, id: 0, lv: 5, rare: 5);
        args.Length.ShouldBeGreaterThanOrEqualTo(2);
        args[0].ShouldBe(5);   // itemLv
    }

    [Fact]
    public void BookArgs_SkillIdAndRare()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Book, subType: 0, id: 287, lv: 0, rare: 5);
        args[0].ShouldBe(287); // skillID
        args[1].ShouldBe(5);   // rareLv
    }

    [Fact]
    public void HorseArgs_IdAndBossLv()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Horse, subType: 0, id: 3, lv: 5, rare: 5);
        args[0].ShouldBe(3);   // id
    }
}
