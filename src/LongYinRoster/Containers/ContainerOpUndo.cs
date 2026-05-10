using System;

namespace LongYinRoster.Containers;

/// <summary>v0.7.12 — Cat 3C Undo stack 의 op 종류.</summary>
public enum OpKind
{
    GameToContainerMove,    // 인벤/창고 → 컨테이너 (Move)
    GameToContainerCopy,    // 인벤/창고 → 컨테이너 (Copy)
    ContainerToInvMove,     // 컨테이너 → 인벤 (Move)
    ContainerToInvCopy,     // 컨테이너 → 인벤 (Copy)
    ContainerToStoMove,     // 컨테이너 → 창고 (Move)
    ContainerToStoCopy,     // 컨테이너 → 창고 (Copy)
    ContainerDelete,        // 컨테이너 안 item 삭제
}

/// <summary>
/// v0.7.12 — Cat 3C Undo 의 단일 op 메타데이터.
/// ContainerJsonBefore = 항상 저장 (container side restore source).
/// AddedItemsJson + GameSourceField = Move undo 시 game 측 re-add 용.
/// AddedCountToGame = Container→Game ops (Copy 또는 Move) 후 game 의 마지막 N 개 제거 용.
/// </summary>
public sealed class OpRecord
{
    public OpKind   Kind                { get; init; }
    public int      ContainerIdx        { get; init; }
    public string   ContainerJsonBefore { get; init; } = "[]";
    public string   AddedItemsJson      { get; init; } = "";
    public string   GameSourceField     { get; init; } = "";
    public int      AddedCountToGame    { get; init; }
    public string   Description         { get; init; } = "";
    public DateTime Timestamp           { get; init; } = DateTime.Now;
}

/// <summary>
/// v0.7.12 Cat 3C — single-op Undo stack. ModWindow 의 Do* 메서드가 success 시 Record(),
/// ContainerPanel 의 [↶ Undo] button 이 ModWindow.PerformUndo 통해 Pop().
/// 단일 슬롯 — 새 op 가 이전 _last 를 덮어씀. mutex / thread-safety 미보장 (Unity single-thread).
/// </summary>
public static class ContainerOpUndo
{
    private static OpRecord? _last;

    public static void Record(OpRecord op) => _last = op;

    public static OpRecord? Pop()
    {
        var t = _last;
        _last = null;
        return t;
    }

    public static OpRecord? Peek() => _last;

    public static bool CanUndo => _last != null;

    /// <summary>tests / 강제 reset 용.</summary>
    public static void Clear() => _last = null;
}
