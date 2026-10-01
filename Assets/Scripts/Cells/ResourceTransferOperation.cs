using System;

/// <summary>一件の搬送で固定する資源と、接続変更に応じて更新する搬送先・進行段階。</summary>
public sealed class ResourceTransferOperation
{
    public enum Stage
    {
        WaitingForReservation,
        Reserved,
        Animating,
        Completed,
        Cancelled
    }

    public CellBase Source { get; }
    public CellBase Target { get; private set; }
    public int ResourceId { get; private set; }
    public ResourceType Type { get; }
    public int Amount { get; }
    public Stage CurrentStage { get; private set; } = Stage.WaitingForReservation;
    public bool IsTerminal => CurrentStage is Stage.Completed or Stage.Cancelled;

    public ResourceTransferOperation(CellBase source, CellBase target, int resourceId,
        ResourceType type, int amount)
    {
        if (source == null || target == null || type == ResourceType.None || amount <= 0)
            throw new ArgumentException("搬送元・先と資源の種別・数量を指定してください。");
        Source = source;
        Target = target;
        ResourceId = resourceId;
        Type = type;
        Amount = amount;
    }

    /// <summary>交差セルでは予約後に上流から ID が渡される。</summary>
    public bool TryAttachId(int resourceId)
    {
        // Unity の GetInstanceID は負数も返す。未設定を示す 0 だけを拒否する。
        if (IsTerminal || ResourceId != 0 || resourceId == 0) return false;
        ResourceId = resourceId;
        return true;
    }

    public bool TryMarkReserved()
    {
        if (CurrentStage != Stage.WaitingForReservation || Target == null) return false;
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

    public bool TrySetTarget(CellBase target)
    {
        if (CurrentStage != Stage.WaitingForReservation || target == null) return false;
        Target = target;
        return true;
    }

    public bool TryComplete()
    {
        if (CurrentStage != Stage.Animating || Target == null) return false;
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
