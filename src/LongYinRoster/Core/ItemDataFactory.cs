using System;

namespace LongYinRoster.Core;

/// <summary>
/// PoC Task A4 결과 (commit 7d57fea) — FAIL.
///
/// ItemData wrapper construction 우회 path 3개 후보 모두 v0.4 에서 미사용:
///   1. IntPtr ctor (Il2CppInterop wrapper) — 발견되었지만 valid IntPtr 확보 불가
///   2. Static factory (e.g., ItemData.Create(int id, int count)) — dump 에 미발견
///   3. Harmony hijack — 미시도 (sub-data graph unsolved)
///
/// **결정**: v0.4 에서 IsAvailable = false. RebuildItemList / RebuildSelfStorage 가
/// `if (!Capabilities.ItemList) return;` 로 short-circuit 하므로 Create 는 호출 안 됨.
/// v0.5+ 에서 sub-data wrapper enumeration (EquipmentData / MedFoodData 등) +
/// path 결정 후 Create 본문 구현.
/// </summary>
public static class ItemDataFactory
{
    public static bool IsAvailable => false;

    public static object Create(int itemID, int count)
    {
        throw new InvalidOperationException(
            "ItemDataFactory.Create unavailable in v0.4 — sub-data wrapper graph unsolved (Task A4 PoC FAIL). " +
            "v0.5+ work. Callers should check IsAvailable first or rely on Capabilities gate.");
    }
}
