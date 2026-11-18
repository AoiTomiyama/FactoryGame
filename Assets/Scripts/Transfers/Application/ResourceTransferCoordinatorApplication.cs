using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>搬送一回の予約、演出、確定、取消を順に実行する。</summary>
public sealed class ResourceTransferCoordinatorApplication
{
    public ResourceTransferOperationApplication Operation { get; }
    public ResourceReservationApplication Reservation { get; private set; }
    private UniTaskCompletionSource _reservationWakeup;

    public void WakeForTargetChange() => _reservationWakeup?.TrySetResult();

    public ResourceTransferCoordinatorApplication(ResourceTransferOperationApplication operation,
        ResourceReservationApplication reservation = null)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        if (reservation != null && (operation.CurrentStage != ResourceTransferOperationApplication.Stage.Reserved ||
                                    !ReferenceEquals(reservation.TargetIdentity, operation.Target?.Identity) ||
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
        WakeForTargetChange();
    }

    public async UniTask<ResourceAnimationResultApplication> RunAsync(Func<int> getPortId, Vector3 from, Vector3 to,
        Func<IContainableApplication> getTarget, Func<IContainableApplication, bool> isCurrentTarget,
        ResourceTransitStoreApplication resources, IResourceTransferPresentationApplication presentation,
        Func<float> getDuration, CancellationToken token, Action onAnimating = null)
    {
        try
        {
            if (Reservation == null)
                await WaitForReservation(getTarget, getPortId, token);

            token.ThrowIfCancellationRequested();
            // 接続喪失で現在の予約が外れても、この試行が所有するTokenで終端を判定する。
            var reservation = Reservation;
            var target = reservation.Target;
            if (!target.IsAlive || !isCurrentTarget(target))
                throw new OperationCanceledException();

            if (!resources.TryGet(Operation.ResourceId, out var info) ||
                info.Amount != Operation.Amount || info.Type != Operation.Type)
                throw new InvalidOperationException("搬送記録と資源データが一致しません。");
            if (!Operation.TryMarkAnimating()) throw new OperationCanceledException();
            // 受入待ち中の強化は反映し、移動開始後の時間はこの試行で固定する。
            var seconds = getDuration();
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(getDuration), "Transfer duration must be finite and positive.");
            onAnimating?.Invoke();
            var result = await presentation.Transfer(token, from, to, Operation.ResourceId, seconds);
            token.ThrowIfCancellationRequested();
            if (result != ResourceAnimationResultApplication.Completed)
            {
                // 表示の失敗は資源の消滅ではない。予約を戻し、所有者に再試行を任せる。
                WaitForNewTarget();
                presentation.SetPosition(Operation.ResourceId, from);
                return result;
            }
            if (!target.IsAlive || !isCurrentTarget(target) || !ReferenceEquals(Reservation, reservation) ||
                !ReferenceEquals(Operation.Target?.Identity, reservation.TargetIdentity))
                throw new OperationCanceledException();

            // 次のコンベアや交差セルは、確定の前に表示用IDを引き継ぐ。
            if (target is IResourceReusableApplication reusable) reusable.Reuse(reservation.PortId, Operation.ResourceId);
            // 入金通知から切断・破棄されても、確定済み資源を再試行しない。
            // 予約と搬送記録を閉じてから受け取り側の確定通知へ進む。
            if (!reservation.TryCommit(Operation.TryComplete)) throw new OperationCanceledException();
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

    private async UniTask WaitForReservation(Func<IContainableApplication> getTarget, Func<int> getPortId,
        CancellationToken token)
    {
        while (!Operation.IsTerminal)
        {
            token.ThrowIfCancellationRequested();
            var target = getTarget();
            var port = target == null ? -1 : getPortId();
            if (target != null && TryReserve(target, port)) return;

            var wakeup = new UniTaskCompletionSource();
            void RetryWhenSpaceOpens(int? changedPort)
            {
                if (!changedPort.HasValue || changedPort.Value == port) wakeup.TrySetResult();
            }
            _reservationWakeup = wakeup;
            if (target != null) target.ReceiveSpaceAvailable += RetryWhenSpaceOpens;
            try
            {
                if (ReferenceEquals(target, getTarget()))
                    await wakeup.Task.AttachExternalCancellation(token);
                // 容量の更新や接続通知の途中から次の予約処理へ入り直さない。
                await UniTask.NextFrame(cancellationToken: token);
            }
            finally
            {
                _reservationWakeup = null;
                if (!ReferenceEquals(target, null)) target.ReceiveSpaceAvailable -= RetryWhenSpaceOpens;
            }
        }
        throw new OperationCanceledException(token);
    }

    private bool TryReserve(IContainableApplication target, int portId)
    {
        if (!ResourceReservationApplication.TryCreate(target, portId, Operation.Amount, Operation.Type,
                out var reservation)) return false;
        if ((!ReferenceEquals(Operation.Target?.Identity, reservation.TargetIdentity) && !Operation.TrySetTarget(target)) ||
            !Operation.TryMarkReserved())
        {
            reservation.TryCancel();
            return false;
        }
        Reservation = reservation;
        return true;
    }
}
