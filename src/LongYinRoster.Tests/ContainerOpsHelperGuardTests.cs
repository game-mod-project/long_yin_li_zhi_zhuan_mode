using System.Collections.Generic;
using System.IO;
using LongYinRoster.Containers;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerOpsHelperGuardTests
{
    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lyr-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void GameToContainer_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        // CurrentContainerIndex 초기값 -1 — "미선택"

        var result = helper.GameToContainer(il2List: new object(), indices: new HashSet<int> { 0 }, removeFromGame: false);

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void GameToContainer_EmptyChecks_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        helper.CurrentContainerIndex = repo.CreateNew("test");

        var result = helper.GameToContainer(il2List: new object(), indices: new HashSet<int>(), removeFromGame: false);

        Assert.Equal(0, result.Succeeded);
        Assert.Equal("선택된 항목 없음", result.Reason);
    }

    [Fact]
    public void ContainerToInventory_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);

        var result = helper.ContainerToInventory(player: new object(), indices: new HashSet<int> { 0 }, removeFromContainer: false, maxWeight: 964f);

        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void DeleteFromContainer_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);

        var result = helper.DeleteFromContainer(new HashSet<int> { 0 });

        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void DeleteFromContainer_EmptyChecks_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        helper.CurrentContainerIndex = repo.CreateNew("test");

        var result = helper.DeleteFromContainer(new HashSet<int>());

        Assert.Equal("선택된 항목 없음", result.Reason);
    }

    [Fact]
    public void ContainerToStorage_ContainerNotSelected_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);

        var result = helper.ContainerToStorage(player: new object(), indices: new HashSet<int> { 0 }, removeFromContainer: false, maxWeight: 300f);

        Assert.Equal("컨테이너 미선택", result.Reason);
    }

    [Fact]
    public void ContainerToInventory_EmptyChecks_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        helper.CurrentContainerIndex = repo.CreateNew("test");

        var result = helper.ContainerToInventory(player: new object(), indices: new HashSet<int>(), removeFromContainer: false, maxWeight: 964f);

        Assert.Equal("선택된 항목 없음", result.Reason);
    }

    [Fact]
    public void ContainerToStorage_EmptyChecks_ReturnsReason()
    {
        var dir = MakeTempDir();
        var repo = new ContainerRepository(dir);
        var helper = new ContainerOpsHelper(repo);
        helper.CurrentContainerIndex = repo.CreateNew("test");

        var result = helper.ContainerToStorage(player: new object(), indices: new HashSet<int>(), removeFromContainer: false, maxWeight: 300f);

        Assert.Equal("선택된 항목 없음", result.Reason);
    }

    [Fact]
    public void ContainerRepository_ContainersDir_AutoCreated()
    {
        // ContainerRepository ctor 가 디렉터리 자동 생성하는지 검증
        var parentDir = MakeTempDir();
        var subDir = Path.Combine(parentDir, "Containers-AutoCreate-Test");
        Assert.False(Directory.Exists(subDir));

        var repo = new ContainerRepository(subDir);

        Assert.True(Directory.Exists(subDir));
    }
}
