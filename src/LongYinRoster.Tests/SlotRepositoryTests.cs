using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LongYinRoster.Slots;
using Xunit;

namespace LongYinRoster.Tests;

public class SlotRepositoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        $"LongYinRoster.Repo.{Guid.NewGuid():N}");

    public SlotRepositoryTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private SlotRepository Repo() => new(_dir, maxUserSlots: 20);

    private static SlotPayload P(int idx) => new()
    {
        Meta = new SlotPayloadMeta(
            1, "0.1.0", idx, $"slot{idx}", "", "live", "",
            DateTime.UtcNow, "1.0.0 f8.2", "",
            new SlotMetadata("h", "", false, 18, 1, 0, 0, 0, 0, 0, 0, 0)),
        Player = @"{""heroID"":0}",
    };

    [Fact]
    public void Empty_Dir_Yields_21_Empty_Entries()
    {
        var repo = Repo();
        repo.All.Count.Should().Be(21);
        repo.All.All(e => e.IsEmpty).Should().BeTrue();
    }

    [Fact]
    public void Write_Then_Read_Single_Slot()
    {
        var repo = Repo();
        repo.Write(3, P(3));
        repo.Reload();

        repo.All[3].IsEmpty.Should().BeFalse();
        repo.All[3].Meta!.UserLabel.Should().Be("slot3");
    }

    [Fact]
    public void AllocateNextFree_Skips_Slot_0_And_Returns_Lowest_Free_User_Slot()
    {
        var repo = Repo();
        repo.Write(1, P(1));
        repo.Write(2, P(2));
        repo.WriteAutoBackup(P(0));
        repo.Reload();

        repo.AllocateNextFree().Should().Be(3);
    }

    [Fact]
    public void AllocateNextFree_Returns_Negative_When_All_User_Slots_Full()
    {
        var repo = Repo();
        for (int i = 1; i <= 20; i++) repo.Write(i, P(i));
        repo.Reload();

        repo.AllocateNextFree().Should().BeLessThan(0);
    }

    [Fact]
    public void Delete_Removes_File_And_Marks_Empty()
    {
        var repo = Repo();
        repo.Write(5, P(5));
        repo.Delete(5);
        repo.Reload();

        repo.All[5].IsEmpty.Should().BeTrue();
        File.Exists(Path.Combine(_dir, "slot_05.json")).Should().BeFalse();
    }

    [Fact]
    public void Slot0_Direct_User_Write_Not_Allowed_Via_Public_Write_Method()
    {
        var repo = Repo();
        var act = () => repo.Write(0, P(0));
        act.Should().Throw<InvalidOperationException>().WithMessage("*slot 0*");
    }

    [Fact]
    public void WriteAutoBackup_Allows_Slot_0()
    {
        var repo = Repo();
        repo.WriteAutoBackup(P(0));
        repo.Reload();
        repo.All[0].IsEmpty.Should().BeFalse();
    }
}
