using LongYinRoster.Containers;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>v0.7.12 Cat 3C — ContainerOpUndo 의 Record/Pop/CanUndo 검증.</summary>
public class ContainerOpUndoTests : System.IDisposable
{
    public ContainerOpUndoTests()  { ContainerOpUndo.Clear(); }
    public void Dispose()          { ContainerOpUndo.Clear(); }

    [Fact]
    public void Empty_CannotUndo()
    {
        ContainerOpUndo.CanUndo.ShouldBeFalse();
        ContainerOpUndo.Pop().ShouldBeNull();
    }

    [Fact]
    public void Record_ThenCanUndo()
    {
        var op = new OpRecord { Kind = OpKind.GameToContainerMove, ContainerIdx = 1 };
        ContainerOpUndo.Record(op);
        ContainerOpUndo.CanUndo.ShouldBeTrue();
        ContainerOpUndo.Peek().ShouldNotBeNull();
    }

    [Fact]
    public void Pop_ReturnsAndClears()
    {
        var op = new OpRecord { Kind = OpKind.ContainerDelete, ContainerIdx = 2, Description = "test" };
        ContainerOpUndo.Record(op);
        var popped = ContainerOpUndo.Pop();
        popped.ShouldNotBeNull();
        popped!.Kind.ShouldBe(OpKind.ContainerDelete);
        popped.ContainerIdx.ShouldBe(2);
        popped.Description.ShouldBe("test");
        ContainerOpUndo.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void Record_OverwritesPrevious()
    {
        // single-stack 동작 — 새 op 가 _last 를 덮어씀
        ContainerOpUndo.Record(new OpRecord { Kind = OpKind.GameToContainerMove, ContainerIdx = 1 });
        ContainerOpUndo.Record(new OpRecord { Kind = OpKind.ContainerToInvMove,  ContainerIdx = 2 });
        var op = ContainerOpUndo.Peek();
        op.ShouldNotBeNull();
        op!.Kind.ShouldBe(OpKind.ContainerToInvMove);
        op.ContainerIdx.ShouldBe(2);
    }

    [Fact]
    public void OpRecord_FieldsPreserved()
    {
        var op = new OpRecord
        {
            Kind                = OpKind.ContainerToStoCopy,
            ContainerIdx        = 5,
            ContainerJsonBefore = "[{\"itemID\":1}]",
            AddedItemsJson      = "[{\"itemID\":2}]",
            GameSourceField     = "selfStorage",
            AddedCountToGame    = 3,
            Description         = "복사 3개 to 창고",
        };
        ContainerOpUndo.Record(op);
        var popped = ContainerOpUndo.Pop();
        popped!.Kind.ShouldBe(OpKind.ContainerToStoCopy);
        popped.ContainerIdx.ShouldBe(5);
        popped.ContainerJsonBefore.ShouldBe("[{\"itemID\":1}]");
        popped.AddedItemsJson.ShouldBe("[{\"itemID\":2}]");
        popped.GameSourceField.ShouldBe("selfStorage");
        popped.AddedCountToGame.ShouldBe(3);
        popped.Description.ShouldBe("복사 3개 to 창고");
    }
}
