using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ConveyorCell : ConnectableCellBase, IContainable, IResourceReusable
{
    [SerializeField] protected float transferSecond;
    [SerializeField] private int transferAmount;
    private IContainable _forwardCell;
    private CellBase _forwardCellBase;
    private TransferStatus _status;
    protected CancellationTokenSource _cts;
    private CancellationTokenSource _activeTransferCts;
    private ResourceTransferOperation _activeTransfer;
    private bool _isDisconnected;
    private bool _transferLoopStarted;
    private bool _readyToSend;
    private int _incomingReservationAmount;
    private ResourceType _incomingReservationType;
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
        OnLostConnectedCell += OnConnectionLost;
        OnDisconnected += Shutdown;
        base.InitializeSystem();
        StoreResourceAsync(_cts.Token).Forget();
    }

    private void OnDestroy() => Shutdown();

    private void Shutdown()
    {
        if (_isDisconnected) return;
        _isDisconnected = true;
        _activeTransferCts?.Cancel();
        _cts?.Cancel();
    }

    /// <summary>
    /// 隣接セル更新時、呼び出されるコールバック
    /// </summary>
    /// <param name="dir">接続先の方向</param>
    /// <param name="cell">接続するセル</param>
    private void OnConnectionUpdated(Vector3Int dir, CellBase cell)
    {
        var forward = DirectionEnumToVector(Directions.Forward);

        // ・セルが前方である
        // ・セルがIContainableを実装している
        // ・前方セルが未設定である
        // 上記三つを満たす場合、前方セルとして設定する
        if (dir == forward && cell is IContainable container && IsForwardEmpty())
        {
            _forwardCell = container;
            _forwardCellBase = cell;
            Debug.Log("connect to forward cell: " + cell.name);
        }
    }

    private void OnConnectionLost(CellBase cell)
    {
        if (cell != _forwardCellBase) return;
        _forwardCell = null;
        _forwardCellBase = null;
        _activeTransferCts?.Cancel();
    }

    private bool IsForwardEmpty()
    {
        return _forwardCellBase == null;
    }

    /// <summary>
    /// 前方のセルにリソースを送り込む
    /// </summary>
    /// <param name="token">トークン</param>
    protected async UniTask StoreResourceAsync(CancellationToken token)
    {
        if (_transferLoopStarted) return;
        _transferLoopStarted = true;
        try
        {
            while (!token.IsCancellationRequested)
            {
                _status = TransferStatus.CheckForStorage;
                await UniTask.WaitUntil(() => _readyToSend && ResourceId != 0 &&
                    _forwardCellBase != null, cancellationToken: token);

                try
                {
                    await TransferOnce(token);
                }
                catch (System.OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // 搬送先だけが切断された。資源を保持して次の接続を待つ。
                }
            }
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested)
        {
            // セル自身の削除による終了。
        }
        finally
        {
            if (ResourceId != 0) ResourceItemObjectPool.Instance.DisposeId(ResourceId);
            ResourceId = 0;
            HasResource = false;
            _readyToSend = false;
            _cts?.Dispose();
            _cts = null;
            _status = TransferStatus.Idle;
        }
    }

    private async UniTask TransferOnce(CancellationToken token)
    {
        var target = _forwardCell;
        var targetCell = _forwardCellBase;
        var dir = DirectionEnumToVector(Directions.Forward);
        var id = ResourceId;
        var (type, amount) = ResourceItemObjectPool.Instance.TakeResourceDataById(id);
        if (amount <= 0 || type == ResourceType.None) return;

        // 一件の資源・ID・接続先を固定し、再接続時は新しい搬送記録を作る。
        var transfer = new ResourceTransferOperation(this, targetCell, id, type, amount);
        _activeTransfer = transfer;

        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _activeTransferCts = transferCts;
        var reserved = false;
        var startPos = transform.position + Vector3.up * 1.1f;
        try
        {
            _status = TransferStatus.WaitingForStorage;
            await UniTask.WaitUntil(() =>
            {
                if (targetCell == null || _forwardCellBase != targetCell) return false;
                if (!target.AllocateStorage(dir, amount, type)) return false;
                reserved = true;
                transfer.TryMarkReserved();
                return true;
            }, cancellationToken: transferCts.Token);

            transferCts.Token.ThrowIfCancellationRequested();
            _status = TransferStatus.Storing;
            transfer.TryMarkAnimating();
            await ResourceItemObjectPool.Instance.Transfer(transferCts.Token,
                startPos, transform.position + dir + Vector3.up * 1.1f, id);
            transferCts.Token.ThrowIfCancellationRequested();
            if (targetCell == null || _forwardCellBase != targetCell)
                throw new System.OperationCanceledException();

            // 受け取り先へIDを渡してから予約を確定する。交差セルはIDを演出に再利用する。
            if (target is IResourceReusable reusable) reusable.Reuse(dir, id);
            target.StoreResource(dir, amount);
            if (!transfer.TryComplete()) return;
            if (target is not IResourceReusable) ResourceItemObjectPool.Instance.DisposeId(id);
            ResourceId = 0;
            HasResource = false;
            _readyToSend = false;
        }
        finally
        {
            if (reserved && transfer.CurrentStage != ResourceTransferOperation.Stage.Completed && targetCell != null)
                target.CancelStorage(dir, amount, type);
            if (transfer.CurrentStage != ResourceTransferOperation.Stage.Completed && !_isDisconnected)
                ResourceItemObjectPool.Instance.SetPosition(id, startPos);
            if (transfer.CurrentStage != ResourceTransferOperation.Stage.Completed) transfer.TryCancel();
            if (_activeTransferCts == transferCts) _activeTransferCts = null;
            if (_activeTransfer == transfer) _activeTransfer = null;
            _status = TransferStatus.Idle;
        }
    }

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        // コンベアは一度に一件だけ全量を受け入れる。
        if (amount <= 0 || resourceType == ResourceType.None || HasResource || _isDisconnected) return false;
        HasResource = true;
        _incomingReservationAmount = amount;
        _incomingReservationType = resourceType;
        return true;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (amount != _incomingReservationAmount || ResourceId == 0 || _isDisconnected) return;
        var info = ResourceItemObjectPool.Instance.TakeResourceDataById(ResourceId);
        if (info.amount != amount || info.type != _incomingReservationType) return;
        _incomingReservationAmount = 0;
        _incomingReservationType = ResourceType.None;
        _readyToSend = true;
    }

    public void CancelStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (amount <= 0 || amount != _incomingReservationAmount ||
            resourceType != _incomingReservationType) return;
        _incomingReservationAmount = 0;
        _incomingReservationType = ResourceType.None;
        HasResource = false;
    }

    protected void MarkResourceReady() => _readyToSend = true;

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

    public void Reuse(Vector3Int dir, int resourceId)
    {
        ResourceId = resourceId;
    }
}
