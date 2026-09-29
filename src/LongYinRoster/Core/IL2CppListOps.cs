using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Dictionary 모양(Keys 프로퍼티 보유)인지 판별. v0.7.13.1 — 게임 v1.1.0f5 에서
    /// GameDataController.*DataBase 가 List&lt;T&gt; → Dictionary&lt;int,T&gt; 로 바뀌어,
    /// Count/get_Item(int) 인덱스 순회가 '키 조회'로 오동작하는 것을 분기하기 위한 판별.
    /// </summary>
    public static bool IsDictionary(object il2Coll)
    {
        if (il2Coll == null) throw new ArgumentNullException(nameof(il2Coll));
        return il2Coll.GetType().GetProperty("Keys", F) != null;
    }

    /// <summary>
    /// (key, value) 열거. List → (index, item). Dictionary&lt;int,T&gt; → (key, value).
    /// Dictionary 는 GetEnumerator()/MoveNext()/Current → Key/Value 를 reflection 으로 호출 —
    /// Il2Cpp 래퍼(Dictionary`2 / Enumerator / KeyValuePair`2)와 .NET Dictionary 가 같은 멤버 이름을 가진다.
    /// 키가 Count 범위 밖(예: 커스텀 무공 ID 1200~2188)이거나 중간이 비어도 전부 도달.
    /// </summary>
    public static IEnumerable<KeyValuePair<int, object?>> Entries(object il2Coll)
    {
        if (il2Coll == null) throw new ArgumentNullException(nameof(il2Coll));
        if (!IsDictionary(il2Coll))
        {
            int n = Count(il2Coll);
            for (int i = 0; i < n; i++)
                yield return new KeyValuePair<int, object?>(i, Get(il2Coll, i));
            yield break;
        }

        var t = il2Coll.GetType();
        var getEnumerator = t.GetMethod("GetEnumerator", F, null, Type.EmptyTypes, null)
            ?? throw new InvalidOperationException(
                $"IL2CppListOps.Entries: type {t.FullName} has no GetEnumerator()");
        var enumerator = getEnumerator.Invoke(il2Coll, null)
            ?? throw new InvalidOperationException(
                $"IL2CppListOps.Entries: {t.FullName}.GetEnumerator() returned null");
        var et = enumerator.GetType();
        var moveNext = et.GetMethod("MoveNext", F, null, Type.EmptyTypes, null)
            ?? throw new InvalidOperationException(
                $"IL2CppListOps.Entries: enumerator {et.FullName} has no MoveNext()");
        var current = et.GetProperty("Current", F)
            ?? throw new InvalidOperationException(
                $"IL2CppListOps.Entries: enumerator {et.FullName} has no Current");

        // .NET Dictionary 의 Enumerator 는 struct — boxed 인스턴스에 Invoke 하면 box 내부가 갱신되므로
        // 같은 boxed 객체를 계속 사용해야 한다. Il2Cpp 래퍼는 class 라 무관.
        PropertyInfo? keyProp = null, valueProp = null;
        while ((bool)moveNext.Invoke(enumerator, null)!)
        {
            var kv = current.GetValue(enumerator);
            if (kv == null) continue;
            var kvt = kv.GetType();
            keyProp ??= kvt.GetProperty("Key", F)
                ?? throw new InvalidOperationException($"IL2CppListOps.Entries: {kvt.FullName} has no Key");
            valueProp ??= kvt.GetProperty("Value", F)
                ?? throw new InvalidOperationException($"IL2CppListOps.Entries: {kvt.FullName} has no Value");
            yield return new KeyValuePair<int, object?>(Convert.ToInt32(keyProp.GetValue(kv)), valueProp.GetValue(kv));
        }
    }
}
