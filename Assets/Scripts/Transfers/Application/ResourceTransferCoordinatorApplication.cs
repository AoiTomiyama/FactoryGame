using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>搬送一回の予約、演出、確定、取消を順に実行する。</summary>
public sealed class ResourceTransferCoordinatorApplication
{
    public ResourceTransferOperationApplication Operation { get; }
    public ResourceReservationApplication Reservation { get; private set; }

    public ResourceTransferCoordinatorApplication(ResourceTransferOperationApplication operation,
        ResourceReservationApplication reservation = null)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        if (reservation != null && (operation.CurrentStage != ResourceTransferOperationApplication.Stage.Reserved ||
                                    reservation.TargetCell != operation.Target ||
                                    reservation.Amount != operation.Amount ||
                                    reservation.Type != operation.Type))
            throw new ArgumentException("搬送記録と予約が一致しません。", nameof(reservation));
        Reservation = reservation;
    }

    /// <summary>接続先を失った予約だけを取り消し、同じ資源IDで次の接続を待つ。</summary>
    public void WaitForNewTarget()
    {
        if (Operation.IsTerminal) return;
        Reservation?.TryCancel();
        Reservation = null;
        Operation.TryWaitForNewTarget();
    }

    public void Cancel()
    {
        if (!Operation.TryCancel()) return;
        Reservation?.TryCancel();
    }

    public async UniTask<ResourceAnimationResultApplication> RunAsync(Vector3Int direction, Vector3 from, Vector3 to,
        Func<IContainableApplication> getTarget, Func<CellBaseAdapter, bool> isCurrentTarget,
        ResourceTransitStoreApplication resources, IResourceTransferPresentationApplication presentation,
        CancellationToken token, Action onAnimating = null)
    {
        try
        {
            if (Reservation == null)
                await UniTask.WaitUntil(() => TryReserve(getTarget(), direction), cancellationToken: token);

            token.ThrowIfCancellationRequested();
            var targetCell = Reservation.TargetCell;
            if (targetCell == null || !isCurrentTarget(targetCell))
                throw new OperationCanceledException();

            if (!resources.TryGet(Operation.ResourceId, out var info) ||
                info.Amount != Operation.Amount || info.Type != Operation.Type)
                throw new InvalidOperationException("搬送記録と資源データが一致しません。");
            if (!Operation.TryMarkAnimating()) throw new OperationCanceledException();
            onAnimating?.Invoke();
            var result = await presentation.Transfer(token, from, to, Operation.ResourceId);
            token.ThrowIfCancellationRequested();
            if (result != ResourceAnimationResultApplication.Completed)
            {
                // 表示の失敗は資源の消滅ではない。予約を戻し、所有者に再試行を任せる。
                WaitForNewTarget();
                presentation.SetPosition(Operation.ResourceId, from);
                return result;
            }
            if (targetCell == null || !isCurrentTarget(targetCell) ||
                Operation.Target != targetCell || Reservation?.TargetCell != targetCell)
                throw new OperationCanceledException();

            // 次のコンベアや交差セルは、確定の前に表示用IDを引き継ぐ。
            var target = (IContainableApplication)targetCell;
            if (target is IResourceReusableApplication reusable) reusable.Reuse(direction, Operation.ResourceId);
            if (!Reservation.TryCommit()) throw new OperationCanceledException();
            if (!Operation.TryComplete()) throw new InvalidOperationException("搬送の完了状態へ進めません。");
            if (target is not IResourceReusableApplication)
            {
                resources.Remove(Operation.ResourceId);
                presentation.ReleaseVisual(Operation.ResourceId);
            }
            return ResourceAnimationResultApplication.Completed;
        }
        finally
        {
            // 接続喪失時は WaitForNewTarget が旧予約を閉じる。その他の失敗は試行を終える。
            if (Operation.CurrentStage != ResourceTransferOperationApplication.Stage.Completed &&
                Operation.CurrentStage != ResourceTransferOperationApplication.Stage.WaitingForReservation)
                Cancel();
        }
    }

    private bool TryReserve(IContainableApplication target, Vector3Int direction)
    {
        if (target is not CellBaseAdapter cell || cell == null ||
            !ResourceReservationApplication.TryCreate(target, direction, Operation.Amount, Operation.Type,
                out var reservation)) return false;
        if ((Operation.Target != cell && !Operation.TrySetTarget(cell)) || !Operation.TryMarkReserved())
        {
            reservation.TryCancel();
            return false;
        }
        Reservation = reservation;
        return true;
    }
}
