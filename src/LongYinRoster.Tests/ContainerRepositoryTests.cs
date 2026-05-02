using System.IO;
using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerRepositoryTests
{
    private static string MakeTempDir() =>
        Path.Combine(Path.GetTempPath(), "longyin_container_test_" + System.Guid.NewGuid());

    [Fact]
    public void CreateNew_AssignsIncrementingIndex()
    {
        var dir = MakeTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            int i1 = repo.CreateNew("첫번째");
            int i2 = repo.CreateNew("두번째");
            i1.ShouldBe(1);
            i2.ShouldBe(2);
            File.Exists(Path.Combine(dir, "container_01.json")).ShouldBeTrue();
            File.Exists(Path.Combine(dir, "container_02.json")).ShouldBeTrue();
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void List_ReturnsCreatedContainers()
    {
        var dir = MakeTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            repo.CreateNew("A");
            repo.CreateNew("B");
            var list = repo.List();
            list.Count.ShouldBe(2);
            list[0].ContainerName.ShouldBe("A");
            list[1].ContainerName.ShouldBe("B");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Delete_RemovesFile()
    {
        var dir = MakeTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            int idx = repo.CreateNew("X");
            repo.Delete(idx);
            File.Exists(Path.Combine(dir, $"container_{idx:D2}.json")).ShouldBeFalse();
            repo.List().Count.ShouldBe(0);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Rename_UpdatesMetadata()
    {
        var dir = MakeTempDir();
        Directory.CreateDirectory(dir);
        try
        {
            var repo = new ContainerRepository(dir);
            int idx = repo.CreateNew("OldName");
            repo.Rename(idx, "NewName");
            var meta = repo.LoadMetadata(idx);
            meta!.ContainerName.ShouldBe("NewName");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
