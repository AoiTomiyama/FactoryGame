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
        StoreResourceAsync(dir, _cts.Token).Forget();
    }

    private async UniTask StoreResourceAsync(Vector3Int dir, CancellationToken token)
    {
        var targetCell = _adjacentContainers[dir].containable;

        await UniTask.WaitUntil(() => _adjacentContainers[dir].id != 0, cancellationToken: token);
        var id = _adjacentContainers[dir].id;

        var info = ResourceItemObjectPool.Instance.TakeResourceDataById(id);

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

        targetCell.StoreResource(dir, info.amount);
    }

    public void Reuse(Vector3Int dir, int id)
    {
        if (_adjacentContainers.TryGetValue(dir, out var container))
        {
            _adjacentContainers[dir] = (container.containable, id);
        }
    }
}