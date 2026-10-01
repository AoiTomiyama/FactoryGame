using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CrossingCell : ConnectableCellBase, IContainable, IResourceReusable
{
    private readonly Dictionary<Vector3Int, (IContainable containable, int id)> _adjacentContainers = new();
    private readonly Dictionary<Vector3Int, PendingTransfer> _pending = new();
    private CancellationTokenSource _cts;
    private bool _isDisconnected;

    private sealed class PendingTransfer
    {
        public IContainable Target;
        public CellBase TargetCell;
        public int Amount;
        public ResourceType Type;
        public int Id;
        public bool Reserved;
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
            transfer.ActiveCts?.Cancel();
            if (transfer.Reserved && transfer.TargetCell != null)
            {
                transfer.Reserved = false;
                transfer.Target.CancelStorage(pair.Key, transfer.Amount, transfer.Type);
            }
            if (transfer.Id != 0) ResourceItemObjectPool.Instance.DisposeId(transfer.Id);
        }
        _cts?.Dispose();
        _cts = null;
    }

    private void OnConnectionUpdated(Vector3Int dir, CellBase cell)
    {
        if (cell is IContainable container)
            _adjacentContainers[dir] = (container, 0);
    }

    private void OnConnectionLost(CellBase cell)
    {
        var directions = new List<Vector3Int>();
        foreach (var pair in _adjacentContainers)
        {
            if (ReferenceEquals(pair.Value.containable, cell)) directions.Add(pair.Key);
        }

        foreach (var dir in directions)
        {
            _adjacentContainers.Remove(dir);
            if (!_pending.TryGetValue(dir, out var transfer) || transfer.TargetCell != cell) continue;
            // 削除される受け取り先の予約は使えない。資源は交差セルに保持する。
            transfer.Reserved = false;
            transfer.Target = null;
            transfer.TargetCell = null;
            transfer.ActiveCts?.Cancel();
        }
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (_isDisconnected || amount <= 0 || resourceType == ResourceType.None || _pending.ContainsKey(dir))
            return false;
        if (!_adjacentContainers.TryGetValue(dir, out var adjacent) ||
            adjacent.containable is not CellBase targetCell || targetCell == null ||
            !adjacent.containable.AllocateStorage(dir, amount, resourceType)) return false;

        _pending[dir] = new PendingTransfer
        {
            Target = adjacent.containable,
            TargetCell = targetCell,
            Amount = amount,
            Type = resourceType,
            Reserved = true
        };
        return true;
    }

    public void CancelStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (!_pending.TryGetValue(dir, out var transfer) || transfer.Amount != amount ||
            transfer.Type != resourceType) return;
        _pending.Remove(dir);
        var reserved = transfer.Reserved;
        transfer.Reserved = false;
        transfer.ActiveCts?.Cancel();
        if (reserved && transfer.TargetCell != null)
            transfer.Target.CancelStorage(dir, amount, resourceType);
        if (transfer.Id != 0) ResourceItemObjectPool.Instance.DisposeId(transfer.Id);
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (_cts == null || !_pending.TryGetValue(dir, out var transfer) ||
            transfer.Amount != amount || transfer.Id == 0 || transfer.Started) return;
        transfer.Started = true;
        StoreResourceAsync(dir, transfer, _cts.Token).Forget();
    }

    private async UniTask StoreResourceAsync(Vector3Int dir, PendingTransfer transfer, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var activeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                transfer.ActiveCts = activeCts;
                try
                {
                    if (!transfer.Reserved)
                    {
                        await UniTask.WaitUntil(() =>
                        {
                            if (!_adjacentContainers.TryGetValue(dir, out var adjacent) ||
                                adjacent.containable is not CellBase cell || cell == null) return false;
                            if (!adjacent.containable.AllocateStorage(dir, transfer.Amount, transfer.Type))
                                return false;
                            transfer.Target = adjacent.containable;
                            transfer.TargetCell = cell;
                            transfer.Reserved = true;
                            return true;
                        }, cancellationToken: activeCts.Token);
                    }

                    activeCts.Token.ThrowIfCancellationRequested();
                    var info = ResourceItemObjectPool.Instance.TakeResourceDataById(transfer.Id);
                    if (info.amount != transfer.Amount || info.type != transfer.Type)
                        throw new InvalidOperationException("交差セルの予約量と資源データが一致しません。");

                    var startPos = transform.position + Vector3.up * 1.1f;
                    await ResourceItemObjectPool.Instance.Transfer(activeCts.Token, startPos,
                        transform.position + dir + Vector3.up * 1.1f, transfer.Id);
                    activeCts.Token.ThrowIfCancellationRequested();
                    if (transfer.TargetCell == null) continue;

                    if (transfer.Target is IResourceReusable reusable)
                        reusable.Reuse(dir, transfer.Id);
                    transfer.Target.StoreResource(dir, transfer.Amount);
                    transfer.Reserved = false;
                    _pending.Remove(dir);
                    if (transfer.Target is not IResourceReusable)
                        ResourceItemObjectPool.Instance.DisposeId(transfer.Id);
                    return;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // 接続先だけが失われた。次の接続までIDを保持する。
                    ResourceItemObjectPool.Instance.SetPosition(transfer.Id,
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
                if (transfer.Reserved && transfer.TargetCell != null)
                    transfer.Target.CancelStorage(dir, transfer.Amount, transfer.Type);
                if (transfer.Id != 0) ResourceItemObjectPool.Instance.DisposeId(transfer.Id);
                _pending.Remove(dir);
            }
        }
    }

    public void Reuse(Vector3Int dir, int id)
    {
        if (!_pending.TryGetValue(dir, out var transfer) || transfer.Id != 0) return;
        transfer.Id = id;
        if (_adjacentContainers.TryGetValue(dir, out var adjacent))
            _adjacentContainers[dir] = (adjacent.containable, id);
    }
}
