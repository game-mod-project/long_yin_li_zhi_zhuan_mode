namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — 카테고리/subType → 게임 generator 메서드명 + 호출 메타.
/// 시그니처는 인게임 spike 로 확정. arg 구성은 ItemFactory.BuildArgs 가 담당.
/// </summary>
public sealed class GeneratorSpec
{
    public string MethodName { get; init; } = "";
    public bool TameRateFixup { get; init; }   // 말 = 생성 후 tameRate=1.0
    public bool IsBook { get; init; }          // 비급 = SetBookData 경로 (빈 ItemData type=3)

    public static GeneratorSpec For(ItemGenCategory cat) => cat switch
    {
        ItemGenCategory.Equipment => ForEquipmentSubType(0),
        ItemGenCategory.Medicine  => new GeneratorSpec { MethodName = "GenerateMedData" },
        ItemGenCategory.Food      => new GeneratorSpec { MethodName = "GenerateFoodData" },
        ItemGenCategory.Book      => new GeneratorSpec { MethodName = "GenerateBook", IsBook = true },
        ItemGenCategory.Treasure  => new GeneratorSpec { MethodName = "GenerateTreasure" },
        ItemGenCategory.Material  => new GeneratorSpec { MethodName = "GenerateMaterial" },
        ItemGenCategory.Horse     => new GeneratorSpec { MethodName = "GenerateHorseData", TameRateFixup = true },
        _ => new GeneratorSpec { MethodName = "" },
    };

    public static GeneratorSpec ForEquipmentSubType(int subType) => subType switch
    {
        0 => new GeneratorSpec { MethodName = "GenerateWeapon" },
        1 => new GeneratorSpec { MethodName = "GenerateArmor" },
        2 => new GeneratorSpec { MethodName = "GenerateHelmet" },
        3 => new GeneratorSpec { MethodName = "GenerateShoes" },
        4 => new GeneratorSpec { MethodName = "GenerateDecoration" },
        5 => new GeneratorSpec { MethodName = "GenerateHorseArmorData" },
        _ => new GeneratorSpec { MethodName = "GenerateWeapon" },
    };
}
