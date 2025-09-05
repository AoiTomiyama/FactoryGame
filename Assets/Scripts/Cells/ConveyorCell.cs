using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class ConveyorCell : ConnectableCellBase
{
    [SerializeField] private float transferSecond;
    private ResourceTransferInfo? _resourceTransferInfo;
    private bool _isActivate;
    private bool _hasResource;
    private IExportable _backwardCell;
    private IContainable _forwardCell;
    private ConveyorCell _outputConveyor;
    private CancellationTokenSource _cts;

    /// <summary>
    /// 輸送するリソースの情報群
    /// </summary>
    private struct ResourceTransferInfo
    {
        public readonly GameObject ResourcePrefab;
        public readonly int Amount;
        public readonly ResourceType Type;

        public ResourceTransferInfo(GameObject resourcePrefab, int amount, ResourceType type)
        {
            ResourcePrefab = resourcePrefab;
            Amount = amount;
            Type = type;
        }
    }

    public override void InitializeSystem()
    {
        OnConnectionChanged += UpdateTransferTarget;
        base.InitializeSystem();
        _isActivate = true;
        _cts = new();
        StoreResourceAsync(_cts.Token).Forget();
        TakeResourceAsync(_cts.Token).Forget();
        TransferBetweenConveyors(_cts.Token).Forget();
    }

    private void OnDestroy()
    {
        _isActivate = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void UpdateTransferTarget()
    {
        for (var i = 0; i < AdjacentCount; i++)
        {
            var cell = AdjacentCells[i];

            if (cell == null) continue;

            var dir = (cell.transform.position - transform.position).ToCardinalDirection();

            var back = DirectionEnumToVector(Directions.Back);
            var forward = DirectionEnumToVector(Directions.Forward);

            if (dir == back && cell is IExportable exportable)
            {
                _backwardCell = exportable;
            }
            else if (dir == forward && cell is IContainable container)
            {
                _forwardCell = container;
            }
            else if (dir == forward && cell is ConveyorCell conveyor)
            {
                _outputConveyor = conveyor;
            }
        }
    }

    private async UniTask TransferBetweenConveyors(CancellationToken token)
    {
        while (_isActivate && !token.IsCancellationRequested)
        {
            // 前方のセルが存在しない場合、またはリソースを持たない場合は待機
            await UniTask.WaitUntil(() => _outputConveyor != null && _resourceTransferInfo != null,
                cancellationToken: token);

            // リソースの予約
            await UniTask.WaitUntil(() => !_outputConveyor._hasResource, cancellationToken: token);

            // リソースを受け付け可能かを更新
            _hasResource = false;
            _outputConveyor._hasResource = true;

            var tempInfo = _resourceTransferInfo ?? default;
            _resourceTransferInfo = null;

            // 移動アニメーション
            var dir = DirectionEnumToVector(Directions.Forward);
            var padding = Vector3.up * 1.1f;
            var startPos = transform.position + padding;
            var endPos = transform.position + dir + padding;

            // 値の更新
            _outputConveyor._resourceTransferInfo = await Transfer(token, startPos, endPos, tempInfo);
        }
    }

    private async UniTask TakeResourceAsync(CancellationToken token)
    {
        while (_isActivate && !token.IsCancellationRequested)
        {
            // 後方のセルが存在しない場合、またはリソースを既に持っている場合は待機
            await UniTask.WaitUntil(() => _backwardCell != null && _resourceTransferInfo == null,
                cancellationToken: token);

            var amount = 0;
            var type = ResourceType.None;

            // リソースが取れるまで待機
            await UniTask.WaitUntil(() =>
                _backwardCell.ExportableModule.TryExport(out amount, out type), cancellationToken: token);
            _hasResource = true;

            // 移動アニメーション
            var padding = Vector3.up * 1.1f;
            var startPos = _backwardCell.ExportableModule.ExportBeginPos + padding;
            var endPos = transform.position + padding;
            var info = new ResourceTransferInfo(null, amount, type);

            // 取得したリソースを保存
            _resourceTransferInfo = await Transfer(token, startPos, endPos, info);
        }
    }

    private async UniTask StoreResourceAsync(CancellationToken token)
    {
        while (_isActivate && !token.IsCancellationRequested)
        {
            // 前方のセルが存在しない場合、またはリソースを持たない場合は待機
            await UniTask.WaitUntil(() => _forwardCell != null && _resourceTransferInfo != null,
                cancellationToken: token);

            var dir = DirectionEnumToVector(Directions.Forward);
            var amount = _resourceTransferInfo?.Amount ?? 0;
            var type = _resourceTransferInfo?.Type ?? ResourceType.None;
            var prefab = _resourceTransferInfo?.ResourcePrefab;

            // リソースの予約
            var allocated = 0;
            await UniTask.WaitUntil(() =>
            {
                // 値が更新されるまで繰り返す
                allocated = _forwardCell.AllocateStorage(dir, amount, type);
                return allocated > 0;
            }, cancellationToken: token);

            _hasResource = false;
            var tempInfo = _resourceTransferInfo ?? default;
            _resourceTransferInfo = null;

            // 移動アニメーション
            var padding = Vector3.up * 1.1f;
            var startPos = transform.position + padding;
            var endPos = transform.position + dir + padding;

            _ = await Transfer(token, startPos, endPos, tempInfo);
            ResourceItemObjectPool.Instance.Return(type, prefab);

            // 値の更新
            _forwardCell.StoreResource(dir, allocated);
        }
    }

    private async UniTask<ResourceTransferInfo> Transfer(CancellationToken token, Vector3 from, Vector3 to,
        ResourceTransferInfo info)
    {
        var type = info.Type;
        var amount = info.Amount;
        var resourcePrefab = info.ResourcePrefab;

        // リソースのPrefabが存在しない場合、新たに生成
        if (resourcePrefab == null)
        {
            resourcePrefab = ResourceItemObjectPool.Instance.GetPrefab(type);
            resourcePrefab.transform.position = from;

            var textMesh = resourcePrefab.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null)
            {
                // Textが存在する場合、予約量を表示
                textMesh.text = amount.ToString();
            }
        }

        var tween = resourcePrefab.transform
            .DOMove(to, transferSecond)
            .SetEase(Ease.Linear);

        await tween.ToUniTask(cancellationToken: token);
        return new(resourcePrefab, amount, type);
    }

    private void OnDrawGizmos()
    {
        if (_hasResource)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.5f, 0.1f);
        }
    }
}