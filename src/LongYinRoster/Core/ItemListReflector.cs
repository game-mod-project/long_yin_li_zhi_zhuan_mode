using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// ItemListData (인벤/창고 wrapper) 의 maxWeight (kg, float) 추출 helper.
/// Spike 결과 (2026-05-03): 갯수 capacity 자체 부재. ItemListData.maxWeight (Single)
/// 한 property 만 노출. 미발견 시 fallbackValue (Config 의 InventoryMaxWeight /
/// StorageMaxWeight) 반환.
/// </summary>
public static class ItemListReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // Spike 확정. 추가 후보 발견 시 array 에 추가.
    private static readonly string[] MAXWEIGHT_NAMES = new[] { "maxWeight" };

    /// <summary>
    /// reflection 으로 itemList wrapper 의 maxWeight (float, kg) 시도. 미발견 시 fallbackValue 반환.
    /// </summary>
    public static float GetMaxWeight(object? itemList, float fallbackValue)
    {
        if (itemList == null) return fallbackValue;
        var t = itemList.GetType();
        foreach (var name in MAXWEIGHT_NAMES)
        {
            var prop = t.GetProperty(name, F);
            if (prop != null && prop.PropertyType == typeof(float))
            {
                try { return (float)prop.GetValue(itemList)!; }
                catch (System.Exception ex) { Logger.Warn($"ItemListReflector.GetMaxWeight prop {name}: {ex.Message}"); }
            }
            var fld = t.GetField(name, F);
            if (fld != null && fld.FieldType == typeof(float))
            {
                try { return (float)fld.GetValue(itemList)!; }
                catch (System.Exception ex) { Logger.Warn($"ItemListReflector.GetMaxWeight fld {name}: {ex.Message}"); }
            }
        }
        return fallbackValue;
    }
}
