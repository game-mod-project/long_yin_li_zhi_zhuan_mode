using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class GeneratorSpecTests
{
    [Fact]
    public void AllCategories_HaveSpec()
    {
        foreach (ItemGenCategory cat in System.Enum.GetValues(typeof(ItemGenCategory)))
        {
            var spec = GeneratorSpec.For(cat);
            spec.ShouldNotBeNull();
            spec.MethodName.ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public void Weapon_MapsToGenerateWeapon()
    {
        GeneratorSpec.For(ItemGenCategory.Equipment).MethodName.ShouldBe("GenerateWeapon");
    }

    [Fact]
    public void Horse_RequiresTameRateFixup()
    {
        GeneratorSpec.For(ItemGenCategory.Horse).TameRateFixup.ShouldBeTrue();
    }

    [Fact]
    public void EquipmentSubType_SelectsCorrectMethod()
    {
        GeneratorSpec.ForEquipmentSubType(0).MethodName.ShouldBe("GenerateWeapon");
        GeneratorSpec.ForEquipmentSubType(1).MethodName.ShouldBe("GenerateArmor");
        GeneratorSpec.ForEquipmentSubType(4).MethodName.ShouldBe("GenerateDecoration");
    }
}
