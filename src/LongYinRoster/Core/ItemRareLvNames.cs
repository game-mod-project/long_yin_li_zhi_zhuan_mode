using System.Collections.Generic;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.7 — 등급 (itemLv) / 품질 (rareLv) 한글 라벨 매핑.
/// LongYinCheat (`CheatPanels.cs:80-82`):
///   EquipLvNames = ["열악","보통","우수","정량","완벽","절세"]   → itemLv 0~5 (등급)
///   QualityNames = ["잔품","하품","중품","상품","진품","극품"]   → rareLv 0~5 (품질)
/// ItemReflector.cs:20 의 매핑 (itemLv=등급, rareLv=품질) 과 일치.
/// SelectorDialog 의 (int Value, string Label) 형식으로 노출.
/// </summary>
public static class ItemRareLvNames
{
    /// <summary>등급 (itemLv) — game 의 EquipLv enum 한글.</summary>
    public static readonly string[] EquipLvNames =
    {
        "열악", "보통", "우수", "정량", "완벽", "절세",
    };

    /// <summary>품질 (rareLv) — game 의 Quality enum 한글.</summary>
    public static readonly string[] QualityNames =
    {
        "잔품", "하품", "중품", "상품", "진품", "극품",
    };

    /// <summary>등급 selector 용 list — value=0~5, label=EquipLvNames[N].</summary>
    public static IReadOnlyList<(int Value, string Label)> EquipLvOptions()
    {
        var list = new List<(int, string)>();
        for (int i = 0; i < EquipLvNames.Length; i++)
            list.Add((i, EquipLvNames[i]));
        return list;
    }

    /// <summary>품질 selector 용 list — value=0~5, label=QualityNames[N].</summary>
    public static IReadOnlyList<(int Value, string Label)> QualityOptions()
    {
        var list = new List<(int, string)>();
        for (int i = 0; i < QualityNames.Length; i++)
            list.Add((i, QualityNames[i]));
        return list;
    }

    /// <summary>itemLv → 한글 (등급). 범위 밖이면 "기타(N)".</summary>
    public static string GetEquipLv(int idx) =>
        (idx >= 0 && idx < EquipLvNames.Length) ? EquipLvNames[idx] : $"기타({idx})";

    /// <summary>rareLv → 한글 (품질). 범위 밖이면 "기타(N)".</summary>
    public static string GetQuality(int idx) =>
        (idx >= 0 && idx < QualityNames.Length) ? QualityNames[idx] : $"기타({idx})";
}
