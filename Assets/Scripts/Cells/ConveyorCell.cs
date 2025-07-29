using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class ConveyorCell : ConnectableCellBase
{
    [SerializeField] private float transferSecond;
    private int _resourceAmount;
    private bool _isActivate;
    private bool _isTransferring;
    private ResourceType _resourceType;
    private IExportable _backwardCell;
    private IContainable _forwardCell;
    private GameObject _resourcePrefab;
    private ConveyorCell _outputConveyor;
    private CancellationTokenSource _cts;

    public override void InitializeSystem()
    {
        base.InitializeSystem();
        OnConnectionChanged += UpdateTransferTarget;
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
            // TODO:コンベア同士の移動を実装する
            
            await UniTask.WaitUntil(() => _outputConveyor != null && _resourceAmount > 0, cancellationToken: token);
            // リソースの予約
            await UniTask.WaitUntil(() => _outputConveyor._resourceAmount <= 0, cancellationToken: token);
            var amountTemp = _resourceAmount;
            _resourceAmount = 0;
            _isTransferring = true;

            // 移動アニメーション
            var dir = DirectionEnumToVector(Directions.Forward);
            var padding = Vector3.up * 1.1f;
            var endPos = transform.position + dir + padding;

            var tween = _resourcePrefab.transform
                .DOMove(endPos, transferSecond);

            await tween.ToUniTask(cancellationToken: token);

            // 値の更新
            _isTransferring = false;
            _outputConveyor._resourcePrefab = _resourcePrefab;
            _outputConveyor._resourceAmount = amountTemp;
            _outputConveyor._resourceType = _resourceType;
            _resourceType = ResourceType.None;
        }
    }

    private async UniTask TakeResourceAsync(CancellationToken token)
    {
        while (_isActivate && !token.IsCancellationRequested)
        {
            // 後方のセルが存在しない場合、またはリソースを捨てに持っている場合は待機
            await UniTask.WaitUntil(() => _backwardCell != null && _resourceAmount <= 0, cancellationToken: token);

            var amount = 0;
            var type = ResourceType.None;

            // リソースが取れるまで待機
            await UniTask.WaitUntil(() =>
                _backwardCell.ExportableModule.TryExport(out amount, out type), cancellationToken: token);
            _isTransferring = true;

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
            _isTransferring = false;
            _resourcePrefab = itemObj;
            _resourceAmount = amount;
            _resourceType = type;
        }
    }

    private async UniTask StoreResourceAsync(CancellationToken token)
    {
        while (_isActivate && !token.IsCancellationRequested)
        {
            // 前方のセルが存在しない場合、またはリソースを持たない場合は待機
            await UniTask.WaitUntil(() => _forwardCell != null && _resourceAmount > 0, cancellationToken: token);

            var dir = DirectionEnumToVector(Directions.Forward);

            // リソースの予約
            var allocated = 0;
            await UniTask.WaitUntil(() =>
            {
                // 値が更新されるまで繰り返す
                allocated = _forwardCell.AllocateStorage(dir, _resourceAmount, _resourceType);
                if (allocated > 0)
                {
                    _resourceAmount = 0;
                    _isTransferring = true;
                }
                return allocated > 0;
            }, cancellationToken: token);

            // 移動アニメーション
            var padding = Vector3.up * 1.1f;
            var endPos = transform.position + dir + padding;

            var tween = _resourcePrefab.transform
                .DOMove(endPos, transferSecond)
                .SetEase(Ease.Linear);

            await tween.ToUniTask(cancellationToken: token);

            ResourceItemObjectPool.Instance.Return(_resourceType, _resourcePrefab);

            // 値の更新
            _forwardCell.StoreResource(dir, allocated);
            _resourceType = ResourceType.None;
            _isTransferring = false;
        }
    }
}