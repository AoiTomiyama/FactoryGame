using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ConveyorCell : ConnectableCellBase, IContainable, IResourceReusable
{
    [SerializeField] protected float transferSecond;
    [SerializeField] private int transferAmount;
    private IContainable _forwardCell;
    private TransferStatus _status;
    protected CancellationTokenSource _cts;
    protected int TransferAmount => transferAmount;
    protected bool HasResource { get; set; }

    /// <summary>
    /// 保持しているリソースのID値。
    /// 0 の場合、リソースを保持していないことを示す。（例外処理を設ける必要がある。）
    /// それ以外の値の場合、そのIDのリソースを保持していることを示す。
    /// </summary>
    protected int ResourceId { get; set; }

    /// <summary>
    /// 現在の輸送ステータス
    /// デバッグ用に視覚的な非同期処理を確認するために設けている。
    /// </summary>
    private enum TransferStatus
    {
        // 待機中、または何もしていない
        Idle,

        // リソースを搬出中
        Storing,

        // リソース搬出待機中
        WaitingForStorage,

        // リソース搬出可能か確認中
        CheckForStorage,
    }

    public override void InitializeSystem()
    {
        _cts = new();
        OnGetConnectedCell += OnConnectionUpdated;
        base.InitializeSystem();
    }

    private void OnConnectionUpdated(Vector3Int dir, CellBase cell)
    {
        var forward = DirectionEnumToVector(Directions.Forward);

        if (dir == forward && cell is IContainable container && _forwardCell == null)
        {
            _forwardCell = container;
        }
    }

    private void OnDestroy()
    {
        if (ResourceId != 0)
        {
            ResourceItemObjectPool.Instance.DisposeId(ResourceId);
        }
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// 前方のセルにリソースを送り込む
    /// </summary>
    /// <param name="token">トークン</param>
    protected async UniTask StoreResourceAsync(CancellationToken token)
    {
        _status = TransferStatus.CheckForStorage;
        // 前方のセルが存在しない場合、またはリソースを持たない場合は待機
        await UniTask.WaitUntil(() => _forwardCell != null && ResourceId != 0, cancellationToken: token);

        var dir = DirectionEnumToVector(Directions.Forward);
        _status = TransferStatus.WaitingForStorage;
        
        var (type, amount) = ResourceItemObjectPool.Instance.TakeById(ResourceId);

        // リソースの予約
        await UniTask.WaitUntil(() => _forwardCell.AllocateStorage(dir, amount, type),
            cancellationToken: token);
        _status = TransferStatus.Storing;

        // 輸送開始したため、自身のリソースを受付開始
        HasResource = false;
        var id = ResourceId;
        ResourceId = 0;

        // 移動アニメーション
        var padding = Vector3.up * 1.1f;
        var startPos = transform.position + padding;
        var endPos = transform.position + dir + padding;

        await ResourceItemObjectPool.Instance.Transfer(token, startPos, endPos, id);

        if (_forwardCell is IResourceReusable resourceReusable)
        {
            resourceReusable.Reuse(id);
        }
        else
        {
            ResourceItemObjectPool.Instance.DisposeId(id);
        }

        _forwardCell.StoreResource(dir, amount);
        _status = TransferStatus.Idle;
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        // HasResourceがfalseのときのみHasResourceをtrueにし返す
        return !HasResource && (HasResource = true);
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        StoreResourceAsync(_cts.Token).Forget();
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

    public void Reuse(int resourceId)
    {
        ResourceId = resourceId;
    }
}