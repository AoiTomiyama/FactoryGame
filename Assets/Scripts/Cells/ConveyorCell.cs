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

    private struct ResourceTransferInfo
    {
        public GameObject ResourcePrefab;
        public int Amount;
        public ResourceType Type;
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
            _hasResource = false;
            _outputConveyor._hasResource = true;
            var tempInfo = _resourceTransferInfo;
            _resourceTransferInfo = null;

            // 移動アニメーション
            var dir = DirectionEnumToVector(Directions.Forward);
            var padding = Vector3.up * 1.1f;
            var endPos = transform.position + dir + padding;

            var tween = tempInfo?.ResourcePrefab.transform
                .DOMove(endPos, transferSecond);

            await tween.ToUniTask(cancellationToken: token);

            // 値の更新
            _outputConveyor._resourceTransferInfo = tempInfo;
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

            var itemObj = ResourceItemObjectPool.Instance.GetPrefab(type);
            itemObj.transform.position = startPos;

            var textMesh = itemObj.GetComponentInChildren<TextMeshPro>();
            if (textMesh != null)
            {
                // Textが存在する場合、予約量を表示
                textMesh.text = amount.ToString();
            }

            var tween = itemObj.transform
                .DOMove(endPos, transferSecond)
                .SetEase(Ease.Linear);

            await tween.ToUniTask(cancellationToken: token);

            // 取得したリソースを保存
            _resourceTransferInfo = new ResourceTransferInfo
            {
                ResourcePrefab = itemObj,
                Amount = amount,
                Type = type
            };
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
                if (allocated > 0)
                {
                    _hasResource = false;
                }

                return allocated > 0;
            }, cancellationToken: token);

            // 移動アニメーション
            var padding = Vector3.up * 1.1f;
            var endPos = transform.position + dir + padding;

            if (prefab != null)
            {
                var tween = prefab.transform
                    .DOMove(endPos, transferSecond)
                    .SetEase(Ease.Linear);

                await tween.ToUniTask(cancellationToken: token);
            }

            ResourceItemObjectPool.Instance.Return(type, prefab);

            // 値の更新
            _forwardCell.StoreResource(dir, allocated);
            _resourceTransferInfo = null;
        }
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