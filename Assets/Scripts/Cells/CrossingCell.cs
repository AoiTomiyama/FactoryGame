using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CrossingCell : ConnectableCellBase, IContainable, IResourceReusable
{
    private readonly Dictionary<Vector3Int, IContainable> _adjacentContainers = new();
    private readonly Dictionary<Vector3Int, PendingTransfer> _pending = new();
    private CancellationTokenSource _cts;
    private bool _isDisconnected;

    private sealed class PendingTransfer
    {
        public ResourceTransferOperation Operation;
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
            var wasReserved = operation.CurrentStage is ResourceTransferOperation.Stage.Reserved or
                ResourceTransferOperation.Stage.Animating;
            if (!operation.TryCancel()) continue;
            if (wasReserved && operation.Target != null)
            {
                ((IContainable)operation.Target).CancelStorage(pair.Key, operation.Amount, operation.Type);
            }
            if (operation.ResourceId != 0) ResourceItemObjectPool.Instance.DisposeId(operation.ResourceId);
        }
        _cts?.Dispose();
        _cts = null;
    }

    private void OnConnectionUpdated(Vector3Int dir, CellBase cell)
    {
        if (cell is IContainable container)
            _adjacentContainers[dir] = container;
    }

    private void OnConnectionLost(CellBase cell)
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
            // 削除される受け取り先の予約は使えない。資源は交差セルに保持する。
            transfer.Operation.TryWaitForNewTarget();
            transfer.ActiveCts?.Cancel();
        }
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (_isDisconnected || amount <= 0 || resourceType == ResourceType.None || _pending.ContainsKey(dir))
            return false;
        if (!_adjacentContainers.TryGetValue(dir, out var adjacent) ||
            adjacent is not CellBase targetCell || targetCell == null ||
            !adjacent.AllocateStorage(dir, amount, resourceType)) return false;

        var operation = new ResourceTransferOperation(this, targetCell, 0, resourceType, amount);
        operation.TryMarkReserved();
        _pending[dir] = new PendingTransfer { Operation = operation };
        return true;
    }

    public void CancelStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (!_pending.TryGetValue(dir, out var transfer)) return;
        var operation = transfer.Operation;
        if (operation.Amount != amount || operation.Type != resourceType) return;
        var wasReserved = operation.CurrentStage is ResourceTransferOperation.Stage.Reserved or
            ResourceTransferOperation.Stage.Animating;
        if (!operation.TryCancel()) return;
        _pending.Remove(dir);
        transfer.ActiveCts?.Cancel();
        if (wasReserved && operation.Target != null)
            ((IContainable)operation.Target).CancelStorage(dir, amount, resourceType);
        if (operation.ResourceId != 0) ResourceItemObjectPool.Instance.DisposeId(operation.ResourceId);
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
                    if (operation.CurrentStage == ResourceTransferOperation.Stage.WaitingForReservation)
                    {
                        await UniTask.WaitUntil(() =>
                        {
                            if (!_adjacentContainers.TryGetValue(dir, out var adjacent) ||
                                adjacent is not CellBase cell || cell == null) return false;
                            if (!adjacent.AllocateStorage(dir, operation.Amount, operation.Type))
                                return false;
                            if (!operation.TrySetTarget(cell) || !operation.TryMarkReserved())
                            {
                                adjacent.CancelStorage(dir, operation.Amount, operation.Type);
                                return false;
                            }
                            return true;
                        }, cancellationToken: activeCts.Token);
                    }

                    activeCts.Token.ThrowIfCancellationRequested();
                    var info = ResourceItemObjectPool.Instance.TakeResourceDataById(operation.ResourceId);
                    if (info.amount != operation.Amount || info.type != operation.Type)
                        throw new InvalidOperationException("交差セルの予約量と資源データが一致しません。");

                    operation.TryMarkAnimating();
                    var startPos = transform.position + Vector3.up * 1.1f;
                    await ResourceItemObjectPool.Instance.Transfer(activeCts.Token, startPos,
                        transform.position + dir + Vector3.up * 1.1f, operation.ResourceId);
                    activeCts.Token.ThrowIfCancellationRequested();
                    if (operation.Target == null) continue;

                    var target = (IContainable)operation.Target;
                    if (target is IResourceReusable reusable)
                        reusable.Reuse(dir, operation.ResourceId);
                    target.StoreResource(dir, operation.Amount);
                    if (!operation.TryComplete()) return;
                    _pending.Remove(dir);
                    if (target is not IResourceReusable)
                        ResourceItemObjectPool.Instance.DisposeId(operation.ResourceId);
                    return;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // 接続先だけが失われた。次の接続までIDを保持する。
                    ResourceItemObjectPool.Instance.SetPosition(operation.ResourceId,
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
                var wasReserved = operation.CurrentStage is ResourceTransferOperation.Stage.Reserved or
                    ResourceTransferOperation.Stage.Animating;
                if (operation.TryCancel())
                {
                    if (wasReserved && operation.Target != null)
                        ((IContainable)operation.Target).CancelStorage(dir, operation.Amount, operation.Type);
                    if (operation.ResourceId != 0)
                        ResourceItemObjectPool.Instance.DisposeId(operation.ResourceId);
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
