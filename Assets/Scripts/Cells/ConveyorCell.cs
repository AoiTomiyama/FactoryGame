using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public class ConveyorCell : ConnectableCellBase, IContainable
{
    [SerializeField] private float transferSecond;
    [SerializeField] private int transferAmount;
    private int _resourceAmount;
    private ResourceType _resourceType;
    private IContainable _forwardCell;
    private TransferStatus _status;
    protected CancellationTokenSource _cts;

    protected int TransferAmount => transferAmount;
    protected bool HasResource { get; set; }
    protected GameObject ResourcePrefab { get; private set; }

    /// <summary>
    /// 現在の搬送ステータス
    /// </summary>
    private enum TransferStatus
    {
        // 待機中
        Idle,
        // リソースを搬出中
        Storing,
        // リソース搬入待機中
        WaitingForStorage,
        // リソース搬入可能か確認中
        CheckForStorage,
    }

    public override void InitializeSystem()
    {
        _cts = new();
        OnConnectionChanged += UpdateTransferTarget;
        base.InitializeSystem();
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (ResourcePrefab != null && _resourceType != ResourceType.None)
        {
            ResourceItemObjectPool.Instance.Return(_resourceType, ResourcePrefab);
        }
    }

    
    private void UpdateTransferTarget()
    {
        for (var i = 0; i < AdjacentCount; i++)
        {
            var cell = AdjacentCells[i];

            if (cell == null) continue;

            var dir = (cell.transform.position - transform.position).ToCardinalDirection();

            var forward = DirectionEnumToVector(Directions.Forward);

            if (dir == forward && cell is IContainable container && _forwardCell == null)
            {
                _forwardCell = container;
            }
        }
    }

    /// <summary>
    /// データの更新
    /// </summary>
    protected void UpdateResourceData(GameObject prefab, int amount, ResourceType type)
    {
        ResourcePrefab = prefab;
        _resourceAmount = amount;
        _resourceType = type;
    }

    /// <summary>
    /// 輸送アニメーション
    /// </summary>
    /// <param name="token">トークン</param>
    /// <param name="from">アニメーションの始点</param>
    /// <param name="to">アニメーションの終点</param>
    /// <param name="prefab">アニメーションの対象</param>
    protected async UniTask Transfer(CancellationToken token, Vector3 from, Vector3 to,
        GameObject prefab)
    {
        prefab.transform.position = from;
        var tween = prefab.transform
            .DOMove(to, transferSecond)
            .SetEase(Ease.Linear);

        await tween.ToUniTask(cancellationToken: token);
    }

    /// <summary>
    /// 前方のセルにリソースを送り込む
    /// </summary>
    /// <param name="token">トークン</param>
    protected async UniTask StoreResourceAsync(CancellationToken token)
    {
        _status = TransferStatus.CheckForStorage;
        // 前方のセルが存在しない場合、またはリソースを持たない場合は待機
        await UniTask.WaitUntil(() => _forwardCell != null && ResourcePrefab != null, cancellationToken: token);

        var dir = DirectionEnumToVector(Directions.Forward);
        _status = TransferStatus.WaitingForStorage;

        // リソースの予約
        await UniTask.WaitUntil(() => _forwardCell.AllocateStorage(dir, _resourceAmount, _resourceType),
            cancellationToken: token);
        _status = TransferStatus.Storing;

        // 輸送開始したため、自身のリソースを受付開始
        HasResource = false;

        // 移動アニメーション
        var padding = Vector3.up * 1.1f;
        var startPos = transform.position + padding;
        var endPos = transform.position + dir + padding;

        // 保存するリソースの情報を退避し、初期化
        var (prefab, amount, type) = (ResourcePrefab, _resourceAmount, _resourceType);
        UpdateResourceData(null, 0, ResourceType.None);

        await Transfer(token, startPos, endPos, prefab);

        // 値の更新
        if (_forwardCell is ConveyorCell conveyor)
        {
            conveyor.UpdateResourceData(prefab, amount, type);
            conveyor.StoreResourceAsync(token).Forget();
        }
        else
        {
            ResourceItemObjectPool.Instance.Return(type, prefab);
            _forwardCell.StoreResource(dir, amount);
        }

        _status = TransferStatus.Idle;
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        // HasResourceがfalseのときのみHasResourceをtrueにし返す
        if (HasResource) return false;
        
        HasResource = true;
        _resourceType  = resourceType;
        return true;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        // リソースが既にある場合は中断
        if (_resourceAmount > 0) return;

        // 現在量に追加する
        _resourceAmount += amount;
    }

    protected virtual void OnDrawGizmos()
    {
        // ステータスに応じて色を変更
        Gizmos.color = _status switch
        {
            TransferStatus.Idle => Color.white,
            TransferStatus.Storing => Color.green,
            TransferStatus.WaitingForStorage => Color.cyan,
            TransferStatus.CheckForStorage => Color.yellow,
            _ => Color.black
        };

        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, Vector3.one * 1f);
        if (HasResource)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position + Vector3.up * 1.5f, 0.1f);
        }

        if (_forwardCell != null)
        {
            Gizmos.color = Color.green;
            var start = transform.position + Vector3.up * 1.5f;
            var end = start + transform.forward * 0.5f;
            Gizmos.DrawLine(start, end);
        }
    }
}