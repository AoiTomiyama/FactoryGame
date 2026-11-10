using System;
using System.Threading;

/// <summary>成功した一件の全量予約を識別し、確定か取消を一度だけ実行する。</summary>
public sealed class ResourceReservationApplication
{
    public enum Result { Pending, Committed, Cancelled }

    private static long _nextId;
    private readonly IContainableApplication _target;

    public long Id { get; }
    public IContainableApplication Target => _target;
    public object TargetIdentity { get; }
    public int PortId { get; }
    public ResourceTypeDomain Type { get; }
    public int Amount { get; }
    public Result CurrentResult { get; private set; } = Result.Pending;

    private ResourceReservationApplication(IContainableApplication target, int portId,
        int amount, ResourceTypeDomain type)
    {
        Id = Interlocked.Increment(ref _nextId);
        _target = target;
        TargetIdentity = target.Identity;
        PortId = portId;
        Amount = amount;
        Type = type;
    }

    public static bool TryCreate(IContainableApplication target, int portId, int amount,
        ResourceTypeDomain type, out ResourceReservationApplication reservation)
    {
        reservation = null;
        if (target == null || !target.IsAlive || target.Identity == null || amount <= 0 || type == ResourceTypeDomain.None ||
            !target.AllocateStorage(portId, amount, type)) return false;

        reservation = new ResourceReservationApplication(target, portId, amount, type);
        return true;
    }

    public bool TryCommit() => TryCommit(null);

    internal bool TryCommit(Func<bool> completeOperation)
    {
        if (CurrentResult != Result.Pending || !_target.IsAlive) return false;
        if (completeOperation != null && !completeOperation()) return false;
        // 再入で同じ予約を二度確定しないよう、受け取り側の呼び出しより先に閉じる。
        CurrentResult = Result.Committed;
        _target.StoreResource(PortId, Amount);
        return true;
    }

    public bool TryCancel()
    {
        if (CurrentResult != Result.Pending) return false;
        CurrentResult = Result.Cancelled;
        if (_target.IsAlive) _target.CancelStorage(PortId, Amount, Type);
        return true;
    }
}
