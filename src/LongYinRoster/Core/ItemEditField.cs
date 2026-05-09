using System.Collections.Generic;
using System.Globalization;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.7 — Item editor field 종류. textfield/dropdown/checkbox UI 분기 결정.
/// </summary>
public enum ItemEditFieldKind
{
    Int,
    Float,
    Bool,
}

/// <summary>
/// v0.7.7 — 단일 edit-able 필드 메타. Path 는 dot-구분 reflection path
/// (예: "rareLv" 또는 "equipmentData.enhanceLv"). KrLabel 은 ItemDetailReflector
/// 의 curated 라벨과 동일 (라벨 매칭으로 UI 행 결정).
/// </summary>
public sealed class ItemEditField
{
    public string Path { get; }
    public string KrLabel { get; }
    public ItemEditFieldKind Kind { get; }
    public float Min { get; }
    public float Max { get; }

    public ItemEditField(string path, string label, ItemEditFieldKind kind, float min, float max)
    {
        Path = path;
        KrLabel = label;
        Kind = kind;
        Min = min;
        Max = max;
    }

    /// <summary>
    /// 사용자 textfield 입력 → typed value. 실패 시 ok=false + error 메시지 (KoreanStrings 의 format string 인자로 사용).
    /// </summary>
    public bool TryParse(string input, out object value, out string error)
    {
        error = "";
        value = Kind switch
        {
            ItemEditFieldKind.Int   => 0,
            ItemEditFieldKind.Float => 0f,
            _                       => false,
        };
        if (string.IsNullOrEmpty(input))
        {
            error = "빈 입력";
            return false;
        }
        switch (Kind)
        {
            case ItemEditFieldKind.Int:
                if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                {
                    error = "정수 형식 아님";
                    return false;
                }
                if (i < Min || i > Max)
                {
                    error = $"범위: {Min:F0}~{Max:F0}";
                    return false;
                }
                value = i;
                return true;

            case ItemEditFieldKind.Float:
                if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                {
                    error = "소수 형식 아님";
                    return false;
                }
                if (f < Min || f > Max)
                {
                    error = $"범위: {Min:F2}~{Max:F2}";
                    return false;
                }
                value = f;
                return true;

            case ItemEditFieldKind.Bool:
                var s = input.Trim().ToLowerInvariant();
                if (s == "true" || s == "1" || s == "yes" || s == "예" || s == "y")
                {
                    value = true;
                    return true;
                }
                if (s == "false" || s == "0" || s == "no" || s == "아니오" || s == "n")
                {
                    value = false;
                    return true;
                }
                error = "true/false 또는 1/0";
                return false;
        }
        error = "Unknown kind";
        return false;
    }
}

/// <summary>
/// v0.7.7 — 7 카테고리 × 17 distinct edit field 매트릭스. Common 3 (rareLv/itemLv/value) 는
/// 모든 카테고리 공통. ForCategory(type) 가 해당 카테고리의 List 반환.
/// </summary>
public static class ItemEditFieldMatrix
{
    // v0.7.7 fix — ItemReflector.cs:20 주석 = itemLv=등급, rareLv=품질 (game 의미).
    // 이전 매트릭스가 swap 돼있었음 — selector 도입과 함께 정정.
    public static readonly IReadOnlyList<ItemEditField> Common = new[]
    {
        new ItemEditField("itemLv", "등급", ItemEditFieldKind.Int, 0, 5),
        new ItemEditField("rareLv", "품질", ItemEditFieldKind.Int, 0, 5),
        new ItemEditField("value",  "가격", ItemEditFieldKind.Int, 0, 9999999),
    };

    // v0.7.7 사용자 피드백 — enhanceLv / speEnhanceLv 단순 수치 수정은 게임 로직 상 무의미 (강화 시스템은 stat 추가 메커니즘).
    // 대신 baseAddData / extraAddData entry 편집기 (HeroSpeAddDataEditor) 가 의미 있는 대체 — Layer 5 patch.
    public static readonly IReadOnlyList<ItemEditField> Equipment = Concat(Common, new[]
    {
        new ItemEditField("equipmentData.speWeightLv", "무게 경감", ItemEditFieldKind.Int, 0, 9),
    });

    public static readonly IReadOnlyList<ItemEditField> MedFood = Concat(Common, new[]
    {
        new ItemEditField("medFoodData.enhanceLv",         "강화",      ItemEditFieldKind.Int, 0, 9),
        new ItemEditField("medFoodData.randomSpeAddValue", "추가 보정", ItemEditFieldKind.Int, 0, 999),
    });

    public static readonly IReadOnlyList<ItemEditField> Book = Concat(Common, new[]
    {
        new ItemEditField("bookData.skillID", "무공 ID", ItemEditFieldKind.Int, 0, 99999),
    });

    public static readonly IReadOnlyList<ItemEditField> Treasure = Concat(Common, new[]
    {
        new ItemEditField("treasureData.fullIdentified",        "완전 감정",      ItemEditFieldKind.Bool,  0, 1),
        new ItemEditField("treasureData.identifyKnowledgeNeed", "감정 필요 지식", ItemEditFieldKind.Float, 0, 9999),
    });

    public static readonly IReadOnlyList<ItemEditField> Horse = Concat(Common, new[]
    {
        new ItemEditField("horseData.speedAdd",     "속도(+Add)",     ItemEditFieldKind.Float, 0,    9999),
        new ItemEditField("horseData.powerAdd",     "힘(+Add)",       ItemEditFieldKind.Float, 0,    9999),
        new ItemEditField("horseData.sprintAdd",    "스프린트(+Add)", ItemEditFieldKind.Float, 0,    9999),
        new ItemEditField("horseData.resistAdd",    "인내(+Add)",     ItemEditFieldKind.Float, 0,    9999),
        new ItemEditField("horseData.maxWeightAdd", "최대무게 추가",  ItemEditFieldKind.Float, 0,    99999),
        new ItemEditField("horseData.favorRate",    "호감 율",        ItemEditFieldKind.Float, 0.01f, 9.99f),
    });

    public static readonly IReadOnlyList<ItemEditField> Material = Common;

    public static IReadOnlyList<ItemEditField> ForCategory(int type) => type switch
    {
        0 => Equipment,
        2 => MedFood,
        3 => Book,
        4 => Treasure,
        5 => Material,
        6 => Horse,
        _ => System.Array.Empty<ItemEditField>(),
    };

    private static ItemEditField[] Concat(IReadOnlyList<ItemEditField> a, ItemEditField[] b)
    {
        var result = new ItemEditField[a.Count + b.Length];
        for (int i = 0; i < a.Count; i++) result[i] = a[i];
        b.CopyTo(result, a.Count);
        return result;
    }
}
