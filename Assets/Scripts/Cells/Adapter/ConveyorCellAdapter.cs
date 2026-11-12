using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ConveyorCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IResourceReusableApplication
{
    [SerializeField] protected float transferSecond;
    [SerializeField] private int transferAmount;
    [SerializeField] private ExportConveyorCellAdapter exportDefinition;
    private bool _exportMode, _modeEvaluationQueued, _takeLoopStarted;
    private IExportableApplication _backwardCell;
    private ConnectableCellBaseAdapter _backwardCellBase;
    private int _backwardPortId;
    protected virtual bool StartsAsExporter => false;
    public bool IsExportConveyor => StartsAsExporter || _exportMode;
    public event Action KindChanged;
    public int BaseTransferAmount => transferAmount;
    private IContainableApplication _forwardCell;
    private CellBaseAdapter _forwardCellBase;
    private TransferStatus _status;
    protected CancellationTokenSource _cts;
    private CancellationTokenSource _activeTransferCts;
    private ResourceTransferOperationApplication _activeTransfer;
    private bool _isDisconnected;
    private bool _transferLoopStarted;
    private bool _readyToSend;
    private int _incomingReservationAmount;
    private ResourceTypeDomain _incomingReservationType;
    public int TransferAmount => EffectiveStat(transferAmount, EquipmentStatDomain.TransferAmount);
    public override CellTypeDomain ProgressionType => IsExportConveyor ? CellTypeDomain.ExportConveyor : CellTypeDomain.Conveyor;
    protected override void ValidateBaseStats() => ValidateStat(transferAmount, EquipmentStatDomain.TransferAmount);
    protected bool HasResource { get; set; }

    /// <summary>
    /// 保持しているリソースのID値。
    /// 0 の場合、リソースを保持していないことを示す。（例外処理を設ける必要がある。）
    /// それ以外の値の場合、そのIDのリソースを保持していることを示す。
    /// </summary>
    private int _resourceId;
    private ResourceItemObjectPoolAdapter _resourceOwner;
    protected int ResourceId
    {
        get => _resourceId;
        set
        {
            _resourceId = value;
            // IDを受け取ったプールへ返す。Scene終了後に別のSingletonを探し直さない。
            _resourceOwner = value == 0 ? null : ResourceItemObjectPoolAdapter.Instance;
        }
    }

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
        if (_cts == null) _cts = new();
        OnGetConnectedCell -= OnConnectionUpdated; OnGetConnectedCell += OnConnectionUpdated;
        OnLostConnectedCell -= OnConnectionLost; OnLostConnectedCell += OnConnectionLost;
        OnDisconnected -= Shutdown; OnDisconnected += Shutdown;
        // 回転・再接続では処理と保持IDを残し、古い相手との試行だけを取り消す。
        _activeTransferCts?.Cancel();
        _forwardCell = null; _forwardCellBase = null;
        _backwardCell = null; _backwardCellBase = null;
        base.InitializeSystem();
        StoreResourceAsync(_cts.Token).Forget();
        if (IsExportConveyor) StartTaking();
    }

    protected override void OnDestroy() { base.OnDestroy(); Shutdown(); }

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
    private void OnConnectionUpdated(Connection connection)
    {
        if (IsExtractionConnection(connection))
        {
            _backwardCell = (IExportableApplication)connection.Neighbor;
            _backwardCellBase = connection.Neighbor;
            _backwardPortId = connection.NeighborPortId;
            ScheduleModeEvaluation();
        }
        var dir = connection.Direction;
        var cell = connection.Neighbor;
        var forward = DirectionEnumToVector(DirectionsDomain.Forward);

        // ・セルが前方である
        // ・セルがIContainableを実装している
        // ・前方セルが未設定である
        // 上記三つを満たす場合、前方セルとして設定する
        if (dir == forward && cell is IContainableApplication container && IsForwardEmpty())
        {
            _forwardCell = container;
            _forwardCellBase = cell;
            Debug.Log("connect to forward cell: " + cell.name);
        }
    }

    private void OnConnectionLost(CellBaseAdapter cell)
    {
        if (cell == _backwardCellBase) { _backwardCell = null; _backwardCellBase = null; }
        if (cell != _forwardCellBase) return;
        _forwardCell = null;
        _forwardCellBase = null;
        _activeTransferCts?.Cancel();
    }

    private bool IsExtractionConnection(Connection connection)
        => connection.Direction == DirectionEnumToVector(DirectionsDomain.Back) &&
           connection.Neighbor is IExportableApplication &&
           connection.Neighbor.HasPort(connection.NeighborPortId, CellPortUseDomain.Output);

    /// <summary>プレビューも接続時も、接した外周の出力口で判定する。在庫の有無は使わない。</summary>
    public static bool HasExtractionSource(GridFieldDatabaseAdapter grid, int x, int z, Quaternion rotation)
    {
        var forward = rotation * Vector3.forward;
        var dx = Mathf.RoundToInt(forward.x); var dz = Mathf.RoundToInt(forward.z);
        var bx = x - dx; var bz = z - dz;
        if (!grid.IsWithinBounds(bx,bz) || grid.GetCell(bx,bz) is not ConnectableCellBaseAdapter source ||
            source is not IExportableApplication || !source.isActiveAndEnabled) return false;
        return source.Ports.Any(port => (port.Use & CellPortUseDomain.Output) != 0 &&
            source.PortDirection(port.Id) == new Vector3Int(dx,0,dz) &&
            Vector3Int.RoundToInt(source.PortPosition(port.Id)) == new Vector3Int(bx,0,bz));
    }

    private void ScheduleModeEvaluation()
    {
        if (!Application.isPlaying || _modeEvaluationQueued || _cts == null) return;
        _modeEvaluationQueued = true;
        EvaluateModeAsync(_cts.Token).Forget();
    }
    private async UniTask EvaluateModeAsync(CancellationToken token)
    {
        try
        {
            // 設備の登録・初期化と失敗復元が完了した後の接続だけを採用する。
            await UniTask.Yield(PlayerLoopTiming.Update,token);
            if (this == null || !isActiveAndEnabled || _isDisconnected ||
                !Connections.Values.Any(IsExtractionConnection) || _backwardCellBase == null || !_backwardCellBase.isActiveAndEnabled) return;
            if (!IsExportConveyor) EnableExportMode();
            StartTaking();
        }
        catch (OperationCanceledException) when(token.IsCancellationRequested) { }
        finally { _modeEvaluationQueued = false; }
    }
    private void EnableExportMode()
    {
        GameObject prepared = null;
        var amount = exportDefinition != null ? exportDefinition.BaseTransferAmount : transferAmount;
        var grid = GridFieldDatabaseAdapter.Instance;
        // 状態を変える前に能力値と見た目を用意し、失敗時は元の搬送を保つ。
        EffectiveStatsRulesDomain.Calculate(amount,grid.StatEffects.GetModifier(CellTypeDomain.ExportConveyor,EquipmentStatDomain.TransferAmount));
        var model = transform.Find("Model");
        try
        {
            if (exportDefinition != null && model != null)
            {
                var sourceModel = exportDefinition.transform.Find("Model").GetChild(0);
                prepared = Instantiate(sourceModel.gameObject,model);
                prepared.SetActive(false);
                prepared.transform.localPosition = sourceModel.localPosition;
                prepared.transform.localRotation = sourceModel.localRotation;
                prepared.transform.localScale = sourceModel.localScale;
            }
        }
        catch { if (prepared != null) Destroy(prepared); throw; }
        _exportMode = true; transferAmount = amount;
        ConfigureProgression(grid.StatEffects);
        if (prepared != null)
        {
            foreach (Transform child in model)
                if (child.gameObject != prepared) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            prepared.SetActive(true);
        }
        if (KindChanged != null)
            foreach(Action handler in KindChanged.GetInvocationList())
                try { handler(); } catch(Exception exception) { Debug.LogException(exception); }
    }
    private void StartTaking()
    {
        if (!Application.isPlaying || _takeLoopStarted || _cts == null || _isDisconnected) return;
        TakeResourceAsync(_cts.Token).Forget();
    }
    private bool TryTakeBatch(out Vector3 startPosition)
    {
        startPosition = default;
        if (!isActiveAndEnabled || HasResource || ResourceId != 0 || _backwardCellBase == null ||
            !_backwardCellBase.isActiveAndEnabled || !_backwardCellBase.HasPort(_backwardPortId,CellPortUseDomain.Output)) return false;
        startPosition = _backwardCell.GetPosition(_backwardPortId) + Vector3.up * 1.1f;
        // 送り元の通知から搬入が再入しても、同じ枠を二重に使わない。
        HasResource = true;
        if (!_backwardCell.TryExport(_backwardPortId,TransferAmount,out var amount,out var type)) { HasResource = false; return false; }
        ResourceId = ResourceItemObjectPoolAdapter.Instance.CreateIdFromResourceData(type,amount);
        return true;
    }
    private async UniTask TakeResourceAsync(CancellationToken token)
    {
        _takeLoopStarted = true;
        try
        {
            await UniTask.Yield(PlayerLoopTiming.Update,token);
            while (!token.IsCancellationRequested)
            {
                var start = default(Vector3);
                await UniTask.WaitUntil(() => TryTakeBatch(out start),cancellationToken:token);
                var end = transform.position + Vector3.up * 1.1f;
                while (true)
                {
                    var result = await ResourceItemObjectPoolAdapter.Instance.Transfer(token,start,end,ResourceId);
                    token.ThrowIfCancellationRequested();
                    if (result == ResourceAnimationResultApplication.Completed) break;
                    await UniTask.Delay(100,cancellationToken:token);
                }
                // 搬出済みの資源は、後方の切断後も同じIDで前へ送る。
                MarkResourceReady();
            }
        }
        catch (OperationCanceledException) when(token.IsCancellationRequested) { }
        finally { _takeLoopStarted = false; }
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
            // プールが先に破棄された場合、プール側がデータと表示を終了済み。
            if (ResourceId != 0 && _resourceOwner != null) _resourceOwner.DisposeId(ResourceId);
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
        var dir = DirectionEnumToVector(DirectionsDomain.Forward);
        var id = ResourceId;
        var (type, amount) = ResourceItemObjectPoolAdapter.Instance.TakeResourceDataById(id);
        if (amount <= 0 || type == ResourceTypeDomain.None) return;

        // 一件の資源・ID・接続先を固定し、再接続時は新しい搬送記録を作る。
        var transfer = new ResourceTransferOperationApplication(this, target, id, type, amount);
        var pool = ResourceItemObjectPoolAdapter.Instance;
        var coordinator = new ResourceTransferCoordinatorApplication(transfer);
        _activeTransfer = transfer;

        using var transferCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _activeTransferCts = transferCts;
        var startPos = transform.position + Vector3.up * 1.1f;
        try
        {
            _status = TransferStatus.WaitingForStorage;
            var result = await coordinator.RunAsync(() => TargetPortId(targetCell, dir), startPos, transform.position + dir + Vector3.up * 1.1f,
                () => targetCell != null && _forwardCellBase == targetCell ? target : null,
                endpoint => ReferenceEquals(_forwardCellBase, endpoint.Identity), pool.Resources, pool, transferCts.Token,
                () => _status = TransferStatus.Storing);
            if (result != ResourceAnimationResultApplication.Completed)
            {
                await UniTask.Delay(100, cancellationToken: token);
                return;
            }
            ResourceId = 0;
            HasResource = false;
            _readyToSend = false;
        }
        finally
        {
            coordinator.Cancel();
            if (transfer.CurrentStage != ResourceTransferOperationApplication.Stage.Completed && !_isDisconnected)
                ResourceItemObjectPoolAdapter.Instance.SetPosition(id, startPos);
            if (_activeTransferCts == transferCts) _activeTransferCts = null;
            if (_activeTransfer == transfer) _activeTransfer = null;
            _status = TransferStatus.Idle;
        }
    }

    public bool AllocateStorage(int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!HasPort(dir, CellPortUseDomain.Input)) return false;
        // コンベアは一度に一件だけ全量を受け入れる。
        if (amount <= 0 || resourceType == ResourceTypeDomain.None || HasResource || _isDisconnected) return false;
        HasResource = true;
        _incomingReservationAmount = amount;
        _incomingReservationType = resourceType;
        return true;
    }

    public void StoreResource(int dir, int amount)
    {
        if (amount != _incomingReservationAmount || ResourceId == 0 || _isDisconnected) return;
        var info = ResourceItemObjectPoolAdapter.Instance.TakeResourceDataById(ResourceId);
        if (info.amount != amount || info.type != _incomingReservationType) return;
        _incomingReservationAmount = 0;
        _incomingReservationType = ResourceTypeDomain.None;
        _readyToSend = true;
    }

    public void CancelStorage(int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (amount <= 0 || amount != _incomingReservationAmount ||
            resourceType != _incomingReservationType) return;
        _incomingReservationAmount = 0;
        _incomingReservationType = ResourceTypeDomain.None;
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

    public void Reuse(int dir, int resourceId)
    {
        ResourceId = resourceId;
    }
}
