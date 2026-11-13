using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CrossingCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IResourceReusableApplication
{
    // 独立した数量能力は持たず、購入レベルだけを保持して中継中の数量を維持する。
    private readonly Dictionary<Vector3Int, IContainableApplication> _adjacentContainers = new();
    private readonly Dictionary<Vector3Int, PendingTransfer> _pending = new();
    private CancellationTokenSource _cts;
    private bool _isDisconnected;
    private SaveGameApplication.Slot[] _restoredBatches;
    public SaveGameApplication.Slot[] CaptureBatches(Action<int> registerOwner)
    {
        var values = new List<SaveGameApplication.Slot>();
        foreach (var pair in _pending)
        {
            var transfer = pair.Value; var operation = transfer.Operation;
            if (!transfer.Started || operation.CurrentStage == ResourceTransferOperationApplication.Stage.Completed) continue;
            if (!ResourceItemObjectPoolAdapter.Instance.Resources.TryGet(operation.ResourceId,out var data) || data.Type != operation.Type || data.Amount != operation.Amount)
                throw new InvalidOperationException("Crossing owns a missing resource batch.");
            registerOwner(operation.ResourceId);
            var port = Ports.Single(p => -PortDirection(p.Id) == pair.Key && (p.Use & CellPortUseDomain.Input) != 0);
            values.Add(SaveGameApplication.SlotValue(port.Id,operation.Type,operation.Amount));
        }
        return values.OrderBy(v => v.port).ToArray();
    }
    public void RestoreBatches(SaveGameApplication.Slot[] batches)
    {
        if (_cts != null || _pending.Count != 0) throw new InvalidOperationException("Restore requires fresh crossing.");
        _restoredBatches = batches;
    }

    private sealed class PendingTransfer
    {
        public ResourceTransferCoordinatorApplication Coordinator;
        public ResourceTransferOperationApplication Operation => Coordinator.Operation;
        public ResourceReservationApplication Reservation => Coordinator.Reservation;
        public bool Started;
        public CancellationTokenSource ActiveCts;
        public ResourceItemObjectPoolAdapter ResourceOwner;
    }

    public override void InitializeSystem()
    {
        _cts = new();
        OnGetConnectedCell += OnConnectionUpdated;
        OnLostConnectedCell += OnConnectionLost;
        OnDisconnected += Shutdown;
        base.InitializeSystem();
        if (_restoredBatches != null)
        {
            foreach (var slot in _restoredBatches)
            {
                var type = SaveKeysApplication.Resource(slot.resource);
                var pool = ResourceItemObjectPoolAdapter.Instance;
                var id = pool.CreateIdFromResourceData(type,slot.amount);
                var direction = -PortDirection(slot.port);
                var transfer = new PendingTransfer {Started = true,ResourceOwner = pool,
                    Coordinator = new ResourceTransferCoordinatorApplication(new ResourceTransferOperationApplication(this,id,type,slot.amount))};
                _pending.Add(direction,transfer);
                pool.SetPosition(id,transform.position + Vector3.up * 1.1f);
                StoreResourceAsync(direction,transfer,_cts.Token).Forget();
            }
            _restoredBatches = null;
        }
    }

    protected override void OnDestroy() { base.OnDestroy(); Shutdown(); }

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
            if (operation.ResourceId != 0 && transfer.ResourceOwner != null) transfer.ResourceOwner.DisposeId(operation.ResourceId);
        }
        _cts?.Dispose();
        _cts = null;
    }

    private void OnConnectionUpdated(Connection connection)
    {
        var dir = connection.Direction;
        var cell = connection.Neighbor;
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
            if (!_pending.TryGetValue(dir, out var transfer) || !ReferenceEquals(transfer.Operation.Target?.Identity, cell)) continue;
            // 受け取り先を失った予約だけを取り消し、資源IDは交差セルに保持する。
            transfer.Coordinator.WaitForNewTarget();
            transfer.ActiveCts?.Cancel();
        }
    }

    public bool AllocateStorage(int portId, int amount, ResourceTypeDomain resourceType)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return false;
        var dir = -PortDirection(portId);
        if (_isDisconnected || amount <= 0 || resourceType == ResourceTypeDomain.None || _pending.ContainsKey(dir))
            return false;
        if (!_adjacentContainers.TryGetValue(dir, out var adjacent) ||
            !ResourceReservationApplication.TryCreate(adjacent, TargetPortId(adjacent.Identity as CellBaseAdapter, dir), amount, resourceType, out var reservation)) return false;

        var operation = new ResourceTransferOperationApplication(this, adjacent, 0, resourceType, amount);
        operation.TryMarkReserved();
        _pending[dir] = new PendingTransfer
        {
            Coordinator = new ResourceTransferCoordinatorApplication(operation, reservation)
        };
        return true;
    }

    public void CancelStorage(int portId, int amount, ResourceTypeDomain resourceType)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return;
        var dir = -PortDirection(portId);
        if (!_pending.TryGetValue(dir, out var transfer)) return;
        var operation = transfer.Operation;
        if (operation.Amount != amount || operation.Type != resourceType) return;
        if (operation.IsTerminal) return;
        _pending.Remove(dir);
        transfer.ActiveCts?.Cancel();
        transfer.Coordinator.Cancel();
        if (operation.ResourceId != 0 && transfer.ResourceOwner != null) transfer.ResourceOwner.DisposeId(operation.ResourceId);
    }

    public void StoreResource(int portId, int amount)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return;
        var dir = -PortDirection(portId);
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
            await UniTask.Yield(PlayerLoopTiming.Update,token);
            while (!token.IsCancellationRequested)
            {
                using var activeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                transfer.ActiveCts = activeCts;
                try
                {
                    var startPos = transform.position + Vector3.up * 1.1f;
                    var result = await transfer.Coordinator.RunAsync(
                        () => _adjacentContainers.TryGetValue(dir, out var portTarget) ? TargetPortId(portTarget.Identity as CellBaseAdapter, dir) : -1, startPos,
                        transform.position + dir + Vector3.up * 1.1f,
                        () => _adjacentContainers.TryGetValue(dir, out var adjacent) ? adjacent : null,
                        endpoint => _adjacentContainers.TryGetValue(dir, out var adjacent) &&
                                ReferenceEquals(adjacent.Identity, endpoint.Identity), ResourceItemObjectPoolAdapter.Instance.Resources,
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
                    if (operation.ResourceId != 0 && transfer.ResourceOwner != null)
                        transfer.ResourceOwner.DisposeId(operation.ResourceId);
                }
                _pending.Remove(dir);
            }
        }
    }

    public void Reuse(int portId, int id)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return;
        var dir = -PortDirection(portId);
        if (!_pending.TryGetValue(dir, out var transfer)) return;
        if (transfer.Operation.TryAttachId(id)) transfer.ResourceOwner = ResourceItemObjectPoolAdapter.Instance;
    }
}
