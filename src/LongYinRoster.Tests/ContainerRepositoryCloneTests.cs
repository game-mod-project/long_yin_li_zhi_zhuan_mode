using System;
using System.IO;
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.7.11 Cat 5C — ContainerRepository.Clone 검증.</summary>
public sealed class ContainerRepositoryCloneTests : IDisposable
{
    private readonly string _tmpDir;

    public ContainerRepositoryCloneTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), $"longyin-clone-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmpDir, recursive: true); } catch { }
    }

    [Fact]
    public void Clone_CreatesNewContainer_WithCopySuffixedName()
    {
        var repo = new ContainerRepository(_tmpDir);
        int srcIdx = repo.CreateNew("원본");
        int cloneIdx = repo.Clone(srcIdx);

        cloneIdx.ShouldBeGreaterThan(srcIdx);
        var meta = repo.LoadMetadata(cloneIdx);
        meta.ShouldNotBeNull();
        meta!.ContainerName.ShouldBe("원본 (복사본)");
    }

    [Fact]
    public void Clone_DeepCopiesItemsJson()
    {
        var repo = new ContainerRepository(_tmpDir);
        int srcIdx = repo.CreateNew("원본");
        // 직접 itemsJson 작성 — schema = items array of {itemID, weight, ...}
        repo.SaveItemsJson(srcIdx, "[{\"itemID\":1,\"weight\":5},{\"itemID\":2,\"weight\":3}]");

        int cloneIdx = repo.Clone(srcIdx);
        cloneIdx.ShouldBeGreaterThan(srcIdx);

        string cloneItems = repo.LoadItemsJson(cloneIdx);
        cloneItems.ShouldContain("\"itemID\":1");
        cloneItems.ShouldContain("\"itemID\":2");

        // 원본 itemsJson 변경 시 clone 영향 없음 (JSON string deep copy)
        repo.SaveItemsJson(srcIdx, "[]");
        string cloneItemsAfter = repo.LoadItemsJson(cloneIdx);
        cloneItemsAfter.ShouldContain("\"itemID\":1");
    }

    [Fact]
    public void Clone_NonExistentSource_ReturnsMinusOne()
    {
        var repo = new ContainerRepository(_tmpDir);
        int result = repo.Clone(9999);
        result.ShouldBe(-1);
    }

    [Fact]
    public void Clone_PreservesItemCount_OnList()
    {
        var repo = new ContainerRepository(_tmpDir);
        int srcIdx = repo.CreateNew("원본");
        repo.SaveItemsJson(srcIdx, "[{\"itemID\":1,\"weight\":5}]");
        int cloneIdx = repo.Clone(srcIdx);

        var list = repo.List();
        var cloneMeta = list.Find(m => m.ContainerIndex == cloneIdx);
        cloneMeta.ShouldNotBeNull();
        cloneMeta!.ItemCount.ShouldBe(1);
        cloneMeta.TotalWeight.ShouldBe(5f);
    }
}
