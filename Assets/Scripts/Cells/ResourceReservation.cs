using System.Threading;
using UnityEngine;

/// <summary>成功した一件の全量予約を識別し、確定か取消を一度だけ実行する。</summary>
public sealed class ResourceReservation
{
    public enum Result { Pending, Committed, Cancelled }

    private static long _nextId;
    private readonly IContainable _target;

    public long Id { get; }
    public CellBase TargetCell { get; }
    public Vector3Int Direction { get; }
    public ResourceType Type { get; }
    public int Amount { get; }
    public Result CurrentResult { get; private set; } = Result.Pending;

    private ResourceReservation(IContainable target, CellBase targetCell, Vector3Int direction,
        int amount, ResourceType type)
    {
        Id = Interlocked.Increment(ref _nextId);
        _target = target;
        TargetCell = targetCell;
        Direction = direction;
        Amount = amount;
        Type = type;
    }

    public static bool TryCreate(IContainable target, Vector3Int direction, int amount,
        ResourceType type, out ResourceReservation reservation)
    {
        reservation = null;
        if (target is not CellBase cell || cell == null || amount <= 0 || type == ResourceType.None ||
            !target.AllocateStorage(direction, amount, type)) return false;

        reservation = new ResourceReservation(target, cell, direction, amount, type);
        return true;
    }

    public bool TryCommit()
    {
        if (CurrentResult != Result.Pending || TargetCell == null) return false;
        // 再入で同じ予約を二度確定しないよう、受け取り側の呼び出しより先に閉じる。
        CurrentResult = Result.Committed;
        _target.StoreResource(Direction, Amount);
        return true;
    }

    public bool TryCancel()
    {
        if (CurrentResult != Result.Pending) return false;
        CurrentResult = Result.Cancelled;
        if (TargetCell != null) _target.CancelStorage(Direction, Amount, Type);
        return true;
    }
}
