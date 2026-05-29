using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemFactoryArgTests
{
    [Fact]
    public void EquipmentArgs_ItemLvIdBossFloat()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Equipment, subType: 0, id: 7, lv: 5, rare: 5);
        args.Length.ShouldBe(3);
        args[0].ShouldBe(5);          // itemLv
        args[1].ShouldBe(7);          // weaponID/littleType = id
        args[2].ShouldBeOfType<float>(); // bossLv 는 float
    }

    [Fact]
    public void HorseArmorArgs_NoIdPlayer()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Equipment, subType: 5, id: 0, lv: 4, rare: 3);
        args.Length.ShouldBe(2);
        args[0].ShouldBe(4);
        args[1].ShouldBeOfType<float>();
    }

    [Fact]
    public void MedicineArgs_IdBossFloat()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Medicine, subType: 0, id: 12, lv: 5, rare: 5);
        args.Length.ShouldBe(2);
        args[0].ShouldBe(12);
        args[1].ShouldBeOfType<float>();
    }

    [Fact]
    public void MaterialArgs_TypeItemLvBossFloat()
    {
        var args = ItemFactory.BuildArgs(ItemGenCategory.Material, subType: 0, id: 2, lv: 5, rare: 5);
        args.Length.ShouldBe(3);
        args[0].ShouldBe(2);          // materialType = id
        args[1].ShouldBe(5);          // itemLv
        args[2].ShouldBeOfType<float>();
    }
}
