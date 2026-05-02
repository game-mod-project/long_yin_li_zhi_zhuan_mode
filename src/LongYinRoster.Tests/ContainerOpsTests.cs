using System.Collections.Generic;
using System.Text.Json;
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerOpsTests
{
    [Fact]
    public void AppendItemsJson_AddsToExistingArray()
    {
        string existing = @"[{""itemID"":1,""name"":""A""}]";
        string toAdd    = @"[{""itemID"":2,""name"":""B""},{""itemID"":3,""name"":""C""}]";
        var result = ContainerOps.AppendItemsJson(existing, toAdd);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public void AppendItemsJson_HandlesEmptyExisting()
    {
        var result = ContainerOps.AppendItemsJson("[]", @"[{""itemID"":1}]");
        JsonDocument.Parse(result).RootElement.GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public void RemoveItemsByIndex_PreservesNonSelected()
    {
        string items = @"[{""itemID"":1},{""itemID"":2},{""itemID"":3}]";
        var indices = new HashSet<int> { 0, 2 };
        var result = ContainerOps.RemoveItemsByIndex(items, indices);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(1);
        doc.RootElement[0].GetProperty("itemID").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void ExtractItemsByIndex_ReturnsSelectedOnly()
    {
        string items = @"[{""itemID"":1},{""itemID"":2},{""itemID"":3}]";
        var indices = new HashSet<int> { 1, 2 };
        var result = ContainerOps.ExtractItemsByIndex(items, indices);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetArrayLength().ShouldBe(2);
        doc.RootElement[0].GetProperty("itemID").GetInt32().ShouldBe(2);
        doc.RootElement[1].GetProperty("itemID").GetInt32().ShouldBe(3);
    }
}
