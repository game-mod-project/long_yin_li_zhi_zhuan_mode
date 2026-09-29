using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LongYinRoster.Core;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.13.1 — 게임 v1.1.0f5 에서 GameDataController.*DataBase 가 List&lt;T&gt; → Dictionary&lt;int,T&gt; 로 바뀜.
/// 인덱스 순회(i &lt; Count, get_Item(i))가 Dictionary 에서는 '키 조회'로 오동작 → 키 누락 시 KeyNotFound,
/// Count 범위 밖 키(커스텀 무공 1200~2188)는 도달 불가. Il2Cpp Dictionary 래퍼도 .NET Dictionary 와 같은
/// 멤버 모양(Count / GetEnumerator / MoveNext / Current / Key / Value)이므로 표준 .NET 컬렉션으로 검증
/// (IL2CppListOpsTests 와 동일 전략 — 실제 IL2CPP 컬렉션은 smoke 로).
/// </summary>
public class IL2CppListOpsEntriesTests
{
    [Fact]
    public void IsDictionary_IsFalse_ForStandardList()
    {
        var list = new List<string> { "a" };
        IL2CppListOps.IsDictionary(list).Should().BeFalse();
    }

    [Fact]
    public void IsDictionary_IsTrue_ForIntKeyedDictionary()
    {
        var dict = new Dictionary<int, string> { [1] = "a" };
        IL2CppListOps.IsDictionary(dict).Should().BeTrue();
    }

    [Fact]
    public void Entries_YieldsIndexAndItem_InOrder_ForStandardList()
    {
        var list = new List<string> { "a", "b", "c" };

        var entries = IL2CppListOps.Entries(list).Select(kv => (kv.Key, kv.Value)).ToList();

        entries.Should().Equal((0, "a"), (1, "b"), (2, "c"));
    }

    [Fact]
    public void Entries_YieldsKeyAndValue_ForSparseIntKeyedDictionary()
    {
        // 키 5 / 1200 — Count(=2) 범위 밖 키가 있고, 0·1 은 비어 있음.
        // 구 코드(i < Count → get_Item(i)) 였다면 get_Item(0) 에서 KeyNotFound.
        var dict = new Dictionary<int, string> { [5] = "x", [1200] = "y" };

        var entries = IL2CppListOps.Entries(dict).Select(kv => (kv.Key, kv.Value)).ToList();

        entries.Should().BeEquivalentTo(new[] { (5, (object?)"x"), (1200, (object?)"y") });
    }

    [Fact]
    public void Entries_IsEmpty_ForEmptyDictionary()
    {
        var dict = new Dictionary<int, string>();
        IL2CppListOps.Entries(dict).Should().BeEmpty();
    }

    [Fact]
    public void Entries_Throws_ForNull()
    {
        var act = () => IL2CppListOps.Entries(null!).ToList();
        act.Should().Throw<ArgumentNullException>();
    }
}
