using System;
using System.Reflection;

namespace LongYinRoster.Core;

/// <summary>
/// Il2CppSystem.Collections.Generic.List&lt;T&gt; 가 .NET IEnumerable 을 구현하지 않아
/// foreach 가 안 되는 환경 대응. reflection 으로 Count property, Item indexer (또는
/// get_Item(int) method), Clear method 를 호출. 표준 .NET List&lt;T&gt; 도 동일 모양이므로
/// 단위 테스트 가능 (실제 IL2CPP list 는 smoke check 로 검증).
/// </summary>
public static class IL2CppListOps
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static int Count(object il2List)
    {
        if (il2List == null) throw new ArgumentNullException(nameof(il2List));
        var prop = il2List.GetType().GetProperty("Count", F)
            ?? throw new InvalidOperationException(
                $"IL2CppListOps.Count: type {il2List.GetType().FullName} has no Count property");
        return Convert.ToInt32(prop.GetValue(il2List));
    }

    public static object? Get(object il2List, int index)
    {
        if (il2List == null) throw new ArgumentNullException(nameof(il2List));
        var t = il2List.GetType();
        var itemProp = t.GetProperty("Item", F);
        if (itemProp != null) return itemProp.GetValue(il2List, new object[] { index });
        var getItem = t.GetMethod("get_Item", F, null, new[] { typeof(int) }, null);
        if (getItem != null) return getItem.Invoke(il2List, new object[] { index });
        throw new InvalidOperationException(
            $"IL2CppListOps.Get: type {t.FullName} has no Item indexer / get_Item(int)");
    }

    public static void Clear(object il2List)
    {
        if (il2List == null) throw new ArgumentNullException(nameof(il2List));
        var t = il2List.GetType();
        var clear = t.GetMethod("Clear", F, null, Type.EmptyTypes, null)
            ?? t.GetMethod("clear", F, null, Type.EmptyTypes, null);
        if (clear == null)
            throw new InvalidOperationException(
                $"IL2CppListOps.Clear: type {t.FullName} has no Clear() method");
        clear.Invoke(il2List, null);
    }

    public static void Add(object il2List, object item)
    {
        if (il2List == null) throw new ArgumentNullException(nameof(il2List));
        var t = il2List.GetType();
        // Try Add(object) — IL2CPP lists expose typed Add(T) so we search by name only.
        var add = t.GetMethod("Add", F, null, new[] { typeof(object) }, null);
        if (add == null)
        {
            // Typed T parameter: find any single-param method named Add.
            foreach (var m in t.GetMethods(F))
            {
                if (m.Name == "Add")
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 1) { add = m; break; }
                }
            }
        }
        if (add == null)
            throw new InvalidOperationException(
                $"IL2CppListOps.Add: type {t.FullName} has no Add(T) method");
        add.Invoke(il2List, new[] { item });
    }
}
