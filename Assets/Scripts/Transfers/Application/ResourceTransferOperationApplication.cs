using System;

/// <summary>一件の搬送で固定する資源と、接続変更に応じて更新する搬送先・進行段階。</summary>
public sealed class ResourceTransferOperationApplication
{
    public enum Stage
    {
        WaitingForReservation,
        Reserved,
        Animating,
        Completed,
        Cancelled
    }

    public IResourceTransferEndpointApplication Source { get; }
    public IContainableApplication Target { get; private set; }
    public int ResourceId { get; private set; }
    public ResourceTypeDomain Type { get; }
    public int Amount { get; }
    public Stage CurrentStage { get; private set; } = Stage.WaitingForReservation;
    public bool IsTerminal => CurrentStage is Stage.Completed or Stage.Cancelled;

    public ResourceTransferOperationApplication(IResourceTransferEndpointApplication source, IContainableApplication target, int resourceId,
        ResourceTypeDomain type, int amount)
        : this(source,resourceId,type,amount)
    {
        if (target == null || !target.IsAlive || target.Identity == null)
            throw new ArgumentException("搬送元・先と資源の種別・数量を指定してください。");
        Target = target;
    }
    /// <summary>保持資源を復元した直後は、接続先と予約を改めて取得する。</summary>
    public ResourceTransferOperationApplication(IResourceTransferEndpointApplication source, int resourceId,
        ResourceTypeDomain type, int amount)
    {
        if (source == null || !source.IsAlive || source.Identity == null || type == ResourceTypeDomain.None || amount <= 0)
            throw new ArgumentException("Valid transfer source and resource are required.");
        Source = source;
        ResourceId = resourceId;
        Type = type;
        Amount = amount;
    }

    /// <summary>交差セルでは予約後に上流から ID が渡される。</summary>
    public bool TryAttachId(int resourceId)
    {
        // ID の割当方法に依存せず、未設定を示す 0 だけを拒否する。
        if (IsTerminal || ResourceId != 0 || resourceId == 0) return false;
        ResourceId = resourceId;
        return true;
    }

    public bool TryMarkReserved()
    {
        if (CurrentStage != Stage.WaitingForReservation || Target == null || !Target.IsAlive) return false;
        CurrentStage = Stage.Reserved;
        return true;
    }

    public bool TryMarkAnimating()
    {
        if (CurrentStage != Stage.Reserved || ResourceId == 0) return false;
        CurrentStage = Stage.Animating;
        return true;
    }

    /// <summary>接続先を失った場合、同じ資源 ID のまま次の予約を待つ。</summary>
    public bool TryWaitForNewTarget()
    {
        if (IsTerminal) return false;
        Target = null;
        CurrentStage = Stage.WaitingForReservation;
        return true;
    }

    public bool TrySetTarget(IContainableApplication target)
    {
        if (CurrentStage != Stage.WaitingForReservation || target == null || !target.IsAlive || target.Identity == null) return false;
        Target = target;
        return true;
    }

    public bool TryComplete()
    {
        if (CurrentStage != Stage.Animating || Target == null || !Target.IsAlive) return false;
        CurrentStage = Stage.Completed;
        return true;
    }

    public bool TryCancel()
    {
        if (IsTerminal) return false;
        CurrentStage = Stage.Cancelled;
        return true;
    }
}
