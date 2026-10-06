using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CrossingCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IResourceReusableApplication
{
    private readonly Dictionary<Vector3Int, IContainableApplication> _adjacentContainers = new();
    private readonly Dictionary<Vector3Int, PendingTransfer> _pending = new();
    private CancellationTokenSource _cts;
    private bool _isDisconnected;

    private sealed class PendingTransfer
    {
        public ResourceTransferCoordinatorApplication Coordinator;
        public ResourceTransferOperationApplication Operation => Coordinator.Operation;
        public ResourceReservationApplication Reservation => Coordinator.Reservation;
        public bool Started;
        public CancellationTokenSource ActiveCts;
    }

    public override void InitializeSystem()
    {
        _cts = new();
        OnGetConnectedCell += OnConnectionUpdated;
        OnLostConnectedCell += OnConnectionLost;
        OnDisconnected += Shutdown;
        base.InitializeSystem();
    }

    private void OnDestroy() => Shutdown();

    private void Shutdown()
    {
        if (_isDisconnected) return;
        _isDisconnected = true;
        // 取消コールバックが即時に再開しても辞書の列挙が崩れないよう、先に所有権を外す。
        var pending = new List<KeyValuePair<Vector3Int, PendingTransfer>>(_pending);
        _pending.Clear();
        _cts?.Cancel();
        foreach (var pair in pending)
        {
            var transfer = pair.Value;
            var operation = transfer.Operation;
            transfer.ActiveCts?.Cancel();
            if (operation.CurrentStage == ResourceTransferOperationApplication.Stage.Completed) continue;
            transfer.Coordinator.Cancel();
            // 取消通知が先に非同期処理へ届いても、ID の所有者である交差セルが返却する。
            if (operation.ResourceId != 0) ResourceItemObjectPoolAdapter.Instance.DisposeId(operation.ResourceId);
        }
        _cts?.Dispose();
        _cts = null;
    }

    private void OnConnectionUpdated(Vector3Int dir, CellBaseAdapter cell)
    {
        if (cell is IContainableApplication container)
            _adjacentContainers[dir] = container;
    }

    private void OnConnectionLost(CellBaseAdapter cell)
    {
        var directions = new List<Vector3Int>();
        foreach (var pair in _adjacentContainers)
        {
            if (ReferenceEquals(pair.Value, cell)) directions.Add(pair.Key);
        }

        foreach (var dir in directions)
        {
            _adjacentContainers.Remove(dir);
            if (!_pending.TryGetValue(dir, out var transfer) || transfer.Operation.Target != cell) continue;
            // 受け取り先を失った予約だけを取り消し、資源IDは交差セルに保持する。
            transfer.Coordinator.WaitForNewTarget();
            transfer.ActiveCts?.Cancel();
        }
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (_isDisconnected || amount <= 0 || resourceType == ResourceTypeDomain.None || _pending.ContainsKey(dir))
            return false;
        if (!_adjacentContainers.TryGetValue(dir, out var adjacent) ||
            adjacent is not CellBaseAdapter targetCell || targetCell == null ||
            !ResourceReservationApplication.TryCreate(adjacent, dir, amount, resourceType, out var reservation)) return false;

        var operation = new ResourceTransferOperationApplication(this, targetCell, 0, resourceType, amount);
        operation.TryMarkReserved();
        _pending[dir] = new PendingTransfer
        {
            Coordinator = new ResourceTransferCoordinatorApplication(operation, reservation)
        };
        return true;
    }

    public void CancelStorage(Vector3Int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!_pending.TryGetValue(dir, out var transfer)) return;
        var operation = transfer.Operation;
        if (operation.Amount != amount || operation.Type != resourceType) return;
        if (operation.IsTerminal) return;
        _pending.Remove(dir);
        transfer.ActiveCts?.Cancel();
        transfer.Coordinator.Cancel();
        if (operation.ResourceId != 0) ResourceItemObjectPoolAdapter.Instance.DisposeId(operation.ResourceId);
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (_cts == null || !_pending.TryGetValue(dir, out var transfer) ||
            transfer.Operation.Amount != amount || transfer.Operation.ResourceId == 0 || transfer.Started) return;
        transfer.Started = true;
        StoreResourceAsync(dir, transfer, _cts.Token).Forget();
    }

    private async UniTask StoreResourceAsync(Vector3Int dir, PendingTransfer transfer, CancellationToken token)
    {
        var operation = transfer.Operation;
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var activeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                transfer.ActiveCts = activeCts;
                try
                {
                    var startPos = transform.position + Vector3.up * 1.1f;
                    var result = await transfer.Coordinator.RunAsync(dir, startPos,
                        transform.position + dir + Vector3.up * 1.1f,
                        () => _adjacentContainers.TryGetValue(dir, out var adjacent) ? adjacent : null,
                        cell => _adjacentContainers.TryGetValue(dir, out var adjacent) &&
                                ReferenceEquals(adjacent, cell), ResourceItemObjectPoolAdapter.Instance.Resources,
                        ResourceItemObjectPoolAdapter.Instance, activeCts.Token);
                    if (result != ResourceAnimationResultApplication.Completed)
                    {
                        await UniTask.Delay(100, cancellationToken: token);
                        continue;
                    }
                    _pending.Remove(dir);
                    return;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    if (operation.CurrentStage == ResourceTransferOperationApplication.Stage.Cancelled) return;
                    if (operation.CurrentStage != ResourceTransferOperationApplication.Stage.WaitingForReservation)
                        throw;
                    // 接続先だけが失われた。次の接続までIDを保持する。
                    ResourceItemObjectPoolAdapter.Instance.SetPosition(operation.ResourceId,
                        transform.position + Vector3.up * 1.1f);
                }
                finally
                {
                    if (transfer.ActiveCts == activeCts) transfer.ActiveCts = null;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 交差セルの削除による終了。
        }
        finally
        {
            if (_pending.TryGetValue(dir, out var current) && current == transfer)
            {
                if (operation.CurrentStage != ResourceTransferOperationApplication.Stage.Completed)
                {
                    transfer.Coordinator.Cancel();
                    if (operation.ResourceId != 0)
                        ResourceItemObjectPoolAdapter.Instance.DisposeId(operation.ResourceId);
                }
                _pending.Remove(dir);
            }
        }
    }

    public void Reuse(Vector3Int dir, int id)
    {
        if (!_pending.TryGetValue(dir, out var transfer)) return;
        transfer.Operation.TryAttachId(id);
    }
}
