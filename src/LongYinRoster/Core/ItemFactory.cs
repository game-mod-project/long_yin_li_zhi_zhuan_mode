using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.13 — 게임 generator reflection 호출 → ItemData → GetItem 인벤 추가.
/// BuildArgs 는 순수(테스트), Generate 는 게임 의존(smoke).
/// arg 구성은 인게임 spike 확정 시그니처로 조정 가능.
/// </summary>
public static class ItemFactory
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public sealed class Result
    {
        public bool Ok { get; set; }
        public int Created { get; set; }
        public string? Reason { get; set; }
        public string? ItemName { get; set; }
    }

    /// <summary>generator 메서드 인자 배열 (this/player 제외). 인게임 확정 시그니처 반영.</summary>
    public static object[] BuildArgs(ItemGenCategory cat, int subType, int id, int lv, int rare)
    {
        float bossF = lv;  // bossLv 는 Single — 반드시 float
        return cat switch
        {
            // 장비: subType 5(마구)=HorseArmor(lv, bossLv) player 없음 / 그 외=(itemLv, id, bossLv)+player(호출부 append)
            ItemGenCategory.Equipment => subType == 5
                ? new object[] { lv, bossF }
                : new object[] { lv, id, bossF },
            ItemGenCategory.Medicine => new object[] { id, bossF },
            ItemGenCategory.Food     => new object[] { id, bossF },
            ItemGenCategory.Material => new object[] { id, lv, bossF },   // (materialType=id, itemLv, bossLv)
            ItemGenCategory.Horse    => new object[] { id, bossF },
            ItemGenCategory.Treasure => new object[] { id, lv, bossF },   // (treasureType=id, itemLv, bossLv)
            ItemGenCategory.Book     => new object[] { lv, bossF, -1 },   // GenerateBook(skillLv, bossLv, forceID=-1)
            _ => new object[] { lv, bossF },
        };
    }

    public static Result Generate(object? player, ItemGenCategory cat, int subType, int id, int lv, int rare, int qty)
    {
        var res = new Result();
        if (player == null) { res.Reason = "player null (게임 로드 후 시도)"; return res; }
        var gc = GameControllerLocator.GetGameController();
        if (gc == null) { res.Reason = "GameController null"; return res; }

        var spec = cat == ItemGenCategory.Equipment ? GeneratorSpec.ForEquipmentSubType(subType) : GeneratorSpec.For(cat);

        int created = 0;
        string? lastName = null;
        for (int q = 0; q < Math.Max(1, qty); q++)
        {
            try
            {
                object? item;
                if (cat == ItemGenCategory.Book)
                {
                    // GenerateBook 으로 Book ItemData 생성 후 특정 skill 로 override (spec.MethodName == "GenerateBook")
                    item = InvokeMethodReturning(gc, spec.MethodName, new object[] { lv, (float)lv, -1 });
                    if (item == null) { res.Reason = "GenerateBook 반환 null"; break; }
                    TryInvokeFlexible(item, "SetBookData", new object[] { id, rare });
                }
                else
                {
                    var args = BuildArgs(cat, subType, id, lv, rare);
                    bool appendPlayer = cat == ItemGenCategory.Equipment && subType != 5;
                    object[] callArgs = appendPlayer ? Append(args, player) : args;
                    item = InvokeMethodReturning(gc, spec.MethodName, callArgs);
                    if (item == null) { res.Reason = $"{spec.MethodName} 반환 null"; break; }
                    if (lv > 0) TrySetInt(item, "itemLv", lv);      // generator 가 itemLv 안 받는 카테고리(med/food 등) 보정
                    if (rare > 0) TrySetInt(item, "rareLv", rare);   // generator 가 rareLv 안 받음 → post-set
                }

                if (spec.TameRateFixup) TrySetHorseTame(item);
                ItemListApplier.FinalizeNewItemWrapper(item);
                AddToInventory(player, item);
                lastName = TryGetName(item);
                created++;
            }
            catch (Exception ex)
            {
                Logger.WarnOnce("ItemFactory", $"Generate {cat}/{spec.MethodName}: {ex.GetType().Name}: {ex.Message}");
                res.Reason = $"{spec.MethodName}: {ex.Message}";
                break;
            }
        }
        res.Created = created;
        res.Ok = created > 0;
        res.ItemName = lastName;
        return res;
    }

    private static void TrySetInt(object obj, string name, int value)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null && p.CanWrite) { p.SetValue(obj, value); return; }
            var f = t.GetField(name, F);
            if (f != null) f.SetValue(obj, value);
        }
        catch { }
    }

    // SetBookData 시그니처 불확실 → 가능한 인자 조합 시도 (있으면 호출, 없으면 무시)
    private static void TryInvokeFlexible(object obj, string methodName, object[] preferredArgs)
    {
        try
        {
            var best = FindMethod(obj.GetType(), methodName, preferredArgs);
            if (best != null) { best.Invoke(obj, FillArgs(best, preferredArgs)); return; }
            // fallback: 첫 번째 인자만 (skillID) 받는 overload 시도
            if (preferredArgs.Length > 1)
            {
                var one = FindMethod(obj.GetType(), methodName, new[] { preferredArgs[0] });
                if (one != null) one.Invoke(obj, FillArgs(one, new[] { preferredArgs[0] }));
            }
        }
        catch { }
    }

    private static object[] Append(object[] arr, object tail)
    {
        var r = new object[arr.Length + 1];
        Array.Copy(arr, r, arr.Length);
        r[arr.Length] = tail;
        return r;
    }

    private static void AddToInventory(object player, object item)
    {
        // HeroData.GetItem(ItemData, bool) — ItemListApplier 와 동일 경로 (player 메서드)
        InvokeMethodVoid(player, "GetItem", new object[] { item, false });
    }

    private static void TrySetHorseTame(object item)
    {
        var hd = item.GetType().GetProperty("horseData", F)?.GetValue(item)
                 ?? item.GetType().GetField("horseData", F)?.GetValue(item);
        if (hd == null) return;
        var p = hd.GetType().GetProperty("tameRate", F);
        if (p != null && p.CanWrite) { p.SetValue(hd, 1.0f); return; }
        var f = hd.GetType().GetField("tameRate", F);
        if (f != null) f.SetValue(hd, 1.0f);
    }

    private static string? TryGetName(object item)
    {
        try
        {
            var p = item.GetType().GetProperty("name", F);
            string raw = p?.GetValue(item)?.ToString() ?? "";
            return string.IsNullOrEmpty(raw) ? null : HangulDict.Translate(raw);
        }
        catch { return null; }
    }

    private static object? InvokeMethodReturning(object obj, string methodName, object[] args)
    {
        var best = FindMethod(obj.GetType(), methodName, args);
        if (best == null) throw new MissingMethodException(obj.GetType().FullName, methodName);
        return best.Invoke(obj, FillArgs(best, args));
    }

    private static void InvokeMethodVoid(object obj, string methodName, object[] args)
    {
        var best = FindMethod(obj.GetType(), methodName, args);
        if (best == null) throw new MissingMethodException(obj.GetType().FullName, methodName);
        best.Invoke(obj, FillArgs(best, args));
    }

    private static MethodInfo? FindMethod(Type t, string methodName, object[] args)
    {
        MethodInfo? best = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != methodName) continue;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) continue;
            bool ok = true;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
            }
            if (!ok) continue;
            if (best == null || ps.Length < best.GetParameters().Length) best = m;
        }
        return best;
    }

    private static object?[] FillArgs(MethodInfo m, object[] args)
    {
        var ps = m.GetParameters();
        var full = new object?[ps.Length];
        for (int i = 0; i < ps.Length; i++)
            full[i] = i < args.Length ? args[i]
                : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
        return full;
    }
}
