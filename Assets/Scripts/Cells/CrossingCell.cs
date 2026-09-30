using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CrossingCell : ConnectableCellBase, IContainable, IResourceReusable
{
    private readonly Dictionary<Vector3Int, (IContainable containable, int id)> _adjacentContainers = new();
    private CancellationTokenSource _cts;

    public override void InitializeSystem()
    {
        _cts = new();
        OnGetConnectedCell += OnConnectionUpdated;
        OnDisconnected += () =>
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        };
        base.InitializeSystem();
    }

    private void OnConnectionUpdated(Vector3Int dir, CellBase cell)
    {
        if (cell is IContainable container)
        {
            _adjacentContainers[dir] = (container, 0);
        }
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (amount <= 0 || resourceType == ResourceType.None) return false;
        // 指定された方向にコンテナが存在しない場合は予約失敗
        if (_adjacentContainers.TryGetValue(dir, out var container) &&
            container.containable.AllocateStorage(dir, amount, resourceType))
        {
            return true;
        }

        return false;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        StoreResourceAsync(dir, amount, _cts.Token).Forget();
    }

    private async UniTask StoreResourceAsync(Vector3Int dir, int amount, CancellationToken token)
    {
        var targetCell = _adjacentContainers[dir].containable;

        await UniTask.WaitUntil(() => _adjacentContainers[dir].id != 0, cancellationToken: token);
        var id = _adjacentContainers[dir].id;

        var info = ResourceItemObjectPool.Instance.TakeResourceDataById(id);
        // 予約した量と表示オブジェクトの量が異なる搬送を確定しない。
        if (amount <= 0 || info.amount != amount)
        {
            Debug.LogError($"交差セルの搬送量が一致しません: 予約量 {amount}, 資源量 {info.amount}");
            return;
        }

        // 移動アニメーション
        var padding = Vector3.up * 1.1f;
        var startPos = transform.position + padding;
        var endPos = transform.position + dir + padding;

        await ResourceItemObjectPool.Instance.Transfer(token, startPos, endPos, id);


        if (targetCell is IResourceReusable reusable)
        {
            reusable.Reuse(dir, id);
        }
        else
        {
            ResourceItemObjectPool.Instance.DisposeId(id);
        }

        targetCell.StoreResource(dir, amount);
    }

    public void Reuse(Vector3Int dir, int id)
    {
        if (_adjacentContainers.TryGetValue(dir, out var container))
        {
            _adjacentContainers[dir] = (container.containable, id);
        }
    }
}
