using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class CrossingCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IResourceReusableApplication
{
    private const float EntranceOffset = .23f;
    private const float ExitOffset = .5f;
    [SerializeField] private ExportConveyorCellAdapter exportDefinition;
    private ConveyorRouteRulesDomain.Route _route;
    internal ConveyorRouteRulesDomain.Route LocalRoute => _route;
    internal void ConfigureWorldRoute(ConveyorRouteRulesDomain.Route route) =>
        ConfigureRoute(ConveyorRouteRulesDomain.Rotate(route, -QuarterTurns));
    internal void ConfigureRoute(ConveyorRouteRulesDomain.Route route)
    {
        if (route.Type != ConveyorRouteRulesDomain.Kind.Crossing || _cts != null)
            throw new InvalidOperationException("Configured crossing requires two straight routes.");
        _route = route;
    }
    protected override CellPortUseDomain PortUse(CellPortDefinition port) =>
        _route.Inputs == 0 ? base.PortUse(port) : RoutePortUse(port, _route);

    // 方向ごとの数量と所有者を保持し、速度だけを共有効果から読む。
    public override CellTypeDomain ProgressionType => CellTypeDomain.Crossing;
    private readonly Dictionary<Vector3Int, IContainableApplication> _adjacentContainers = new();
    private readonly Dictionary<Vector3Int, (IContainableApplication Target, Action<int?> Wake)> _downstreamSignals = new();
    private readonly Dictionary<Vector3Int, PendingTransfer> _pending = new();
    private CancellationTokenSource _cts;
    private bool _isDisconnected;
    private SaveGameApplication.Slot[] _restoredBatches;

    internal override Vector3 ResourceDisplayPosition(Vector3 point, Vector3Int travelDirection)
    {
        // モデルの横レーンを高くし、逆向きの資源は入口側で止めて表示を分ける。
        var across = Vector3.Dot(travelDirection, transform.right);
        var height = Mathf.Abs(across) > .5f ? 1.55f : ResourceDisplayHeight;
        return point - (Vector3)travelDirection * EntranceOffset + Vector3.up * height;
    }

    public SaveGameApplication.Slot[] CaptureBatches(Action<int> registerOwner)
    {
        var values = new List<SaveGameApplication.Slot>();
        foreach (var pair in _pending)
        {
            var transfer = pair.Value; var operation = transfer.Operation;
            if (!transfer.Started || operation.CurrentStage == ResourceTransferOperationApplication.Stage.Completed) continue;
            if (!ResourceItemObjectPoolAdapter.Instance.Resources.TryGet(operation.ResourceId,out var data) || data.Type != operation.Type || data.Amount != operation.Amount)
                throw new InvalidOperationException("Crossing owns a missing resource batch.");
            registerOwner(operation.ResourceId);
            var port = Ports.Single(p => -PortDirection(p.Id) == pair.Key && HasPort(p.Id, CellPortUseDomain.Input));
            values.Add(SaveGameApplication.SlotValue(port.Id,operation.Type,operation.Amount));
        }
        return values.OrderBy(v => v.port).ToArray();
    }
    public void RestoreBatches(SaveGameApplication.Slot[] batches)
    {
        if (_cts != null || _pending.Count != 0) throw new InvalidOperationException("Restore requires fresh crossing.");
        _restoredBatches = batches;
    }

    private sealed class PendingTransfer
    {
        public ResourceTransferCoordinatorApplication Coordinator;
        public ResourceTransferOperationApplication Operation => Coordinator.Operation;
        public ResourceReservationApplication Reservation => Coordinator.Reservation;
        public bool Started;
        public bool ReachedExit;
        public int InputPort;
        public CancellationTokenSource ActiveCts;
        public ResourceItemObjectPoolAdapter ResourceOwner;
    }

    public override void InitializeSystem()
    {
        _cts = new();
        OnGetConnectedCell += OnConnectionUpdated;
        OnLostConnectedCell += OnConnectionLost;
        OnDisconnected += Shutdown;
        base.InitializeSystem();
        if (_restoredBatches != null)
        {
            foreach (var slot in _restoredBatches)
            {
                var type = SaveKeysApplication.Resource(slot.resource);
                AcceptPulledBatch(slot.port, slot.amount, type, deferUntilConnected: true);
            }
            _restoredBatches = null;
        }
    }

    private void AcceptPulledBatch(int port, int amount, ResourceTypeDomain type, bool deferUntilConnected = false)
    {
        var pool = ResourceItemObjectPoolAdapter.Instance;
        var id = pool.CreateIdFromResourceData(type, amount);
        var direction = -PortDirection(port);
        var transfer = new PendingTransfer {Started = true, ResourceOwner = pool, InputPort = port,
            Coordinator = new ResourceTransferCoordinatorApplication(new ResourceTransferOperationApplication(this, id, type, amount))};
        _pending.Add(direction, transfer);
        pool.SetPosition(id, ResourceDisplayPosition(transform.position, direction));
        StoreResourceAsync(direction, transfer, _cts.Token, deferUntilConnected).Forget();
    }

    protected override void OnDestroy() { base.OnDestroy(); Shutdown(); }

    private void Shutdown()
    {
        if (_isDisconnected) return;
        _isDisconnected = true;
        foreach (var signal in _downstreamSignals.Values) signal.Target.ReceiveSpaceAvailable -= signal.Wake;
        _downstreamSignals.Clear();
        // 取消コールバックが即時に再開しても辞書の列挙が崩れないよう、先に所有権を外す。
        var pending = new List<KeyValuePair<Vector3Int, PendingTransfer>>(_pending);
        _pending.Clear();
        _cts?.Cancel();
        foreach (var pair in pending)
        {
            var transfer = pair.Value;
            var operation = transfer.Operation;
            transfer.ActiveCts?.Cancel();
            if (operation.CurrentStage == ResourceTransferOperationApplication.Stage.Completed) continue;
            transfer.Coordinator.Cancel();
            // 取消通知が先に非同期処理へ届いても、ID の所有者である交差セルが返却する。
            if (operation.ResourceId != 0 && transfer.ResourceOwner != null) transfer.ResourceOwner.DisposeId(operation.ResourceId);
        }
        _cts?.Dispose();
        _cts = null;
    }

    private void OnConnectionUpdated(Connection connection)
    {
        var dir = connection.Direction;
        var cell = connection.Neighbor;
        // 手動配置の交差は向きが未指定なので、受け取り先からの逆向き搬出を始めない。
        if (Application.isPlaying && _route.Inputs != 0 && HasPort(connection.PortId, CellPortUseDomain.Input))
            ConveyorLanePickupAdapter.Start(this, connection, exportDefinition,
                () => !_pending.ContainsKey(-dir), (amount, type) => AcceptPulledBatch(connection.PortId, amount, type), _cts.Token);
        if (cell is not IContainableApplication container || !HasPort(connection.PortId, CellPortUseDomain.Output) ||
            !cell.HasPort(connection.NeighborPortId, CellPortUseDomain.Input)) return;
        _adjacentContainers[dir] = container;
        if (_pending.TryGetValue(dir, out var pending)) pending.Coordinator.WakeForTargetChange();
        foreach (var input in Ports)
        {
            if (!HasPort(input.Id, CellPortUseDomain.Input) || -PortDirection(input.Id) != dir) continue;
            if (_downstreamSignals.Remove(dir, out var old)) old.Target.ReceiveSpaceAvailable -= old.Wake;
            var inputId = input.Id;
            var downstreamPortId = connection.NeighborPortId;
            void WakeUpstream(int? port)
            {
                if ((!port.HasValue || port.Value == downstreamPortId) && !_pending.ContainsKey(dir))
                    NotifyReceiveSpace(inputId);
            }
            container.ReceiveSpaceAvailable += WakeUpstream;
            _downstreamSignals.Add(dir, (container, WakeUpstream));
            WakeUpstream(null);
            break;
        }
    }

    private void OnConnectionLost(CellBaseAdapter cell)
    {
        var directions = new List<Vector3Int>();
        foreach (var pair in _adjacentContainers)
        {
            if (ReferenceEquals(pair.Value, cell)) directions.Add(pair.Key);
        }

        foreach (var dir in directions)
        {
            _adjacentContainers.Remove(dir);
            if (_downstreamSignals.Remove(dir, out var signal)) signal.Target.ReceiveSpaceAvailable -= signal.Wake;
            if (!_pending.TryGetValue(dir, out var transfer)) continue;
            transfer.Coordinator.WakeForTargetChange();
            if (!ReferenceEquals(transfer.Operation.Target?.Identity, cell)) continue;
            // 受け取り先を失った予約だけを取り消し、資源IDは交差セルに保持する。
            transfer.Coordinator.WaitForNewTarget();
            transfer.ActiveCts?.Cancel();
        }
    }

    public bool AllocateStorage(int portId, int amount, ResourceTypeDomain resourceType)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return false;
        var dir = -PortDirection(portId);
        if (_isDisconnected || amount <= 0 || resourceType == ResourceTypeDomain.None || _pending.ContainsKey(dir))
            return false;
        if (!_adjacentContainers.TryGetValue(dir, out var adjacent) || !adjacent.IsAlive) return false;
        var operation = new ResourceTransferOperationApplication(this, 0, resourceType, amount);
        ResourceReservationApplication reservation = null;
        // 次が搬送路なら、交差の入口でその先の枠まで塞がない。交差レーンが一件を受け持つ。
        if (adjacent is not ConveyorCellAdapter)
        {
            if (!ResourceReservationApplication.TryCreate(adjacent, TargetPortId(adjacent.Identity as CellBaseAdapter, dir),
                    amount, resourceType, out reservation)) return false;
            operation.TrySetTarget(adjacent);
            operation.TryMarkReserved();
        }
        _pending[dir] = new PendingTransfer
        {
            InputPort = portId,
            Coordinator = new ResourceTransferCoordinatorApplication(operation, reservation)
        };
        return true;
    }

    public void CancelStorage(int portId, int amount, ResourceTypeDomain resourceType)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return;
        var dir = -PortDirection(portId);
        if (!_pending.TryGetValue(dir, out var transfer)) return;
        var operation = transfer.Operation;
        if (operation.Amount != amount || operation.Type != resourceType) return;
        if (operation.IsTerminal) return;
        _pending.Remove(dir);
        transfer.Coordinator.Cancel();
        transfer.ActiveCts?.Cancel();
        if (operation.ResourceId != 0 && transfer.ResourceOwner != null) transfer.ResourceOwner.DisposeId(operation.ResourceId);
        NotifyReceiveSpace(portId);
    }

    public void StoreResource(int portId, int amount)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return;
        var dir = -PortDirection(portId);
        if (_cts == null || !_pending.TryGetValue(dir, out var transfer) ||
            transfer.Operation.Amount != amount || transfer.Operation.ResourceId == 0 || transfer.Started) return;
        transfer.Started = true;
        StoreResourceAsync(dir, transfer, _cts.Token).Forget();
    }

    private async UniTask StoreResourceAsync(Vector3Int dir, PendingTransfer transfer, CancellationToken token,
        bool deferUntilConnected = false)
    {
        var operation = transfer.Operation;
        var entrance = ResourceDisplayPosition(transform.position, dir);
        var exit = entrance + (Vector3)dir * (EntranceOffset + ExitOffset);
        try
        {
            // 復元時は接続を待つ。通常の引き継ぎでは予約済みの出口へそのまま進む。
            if (deferUntilConnected) await UniTask.Yield(PlayerLoopTiming.Update, token);
            while (!token.IsCancellationRequested)
            {
                using var activeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                transfer.ActiveCts = activeCts;
                try
                {
                    var nextCell = transform.position + dir;
                    var fallback = nextCell + Vector3.up * ResourceDisplayHeight;
                    Vector3 Destination(IContainableApplication endpoint) => endpoint.Identity is CellBaseAdapter cell
                        ? cell.ResourceDisplayPosition(nextCell, dir) : fallback;
                    var pool = transfer.ResourceOwner;
                    if (pool == null) throw new InvalidOperationException("Crossing transfer lost its resource owner.");
                    float TravelSeconds() => EffectiveDuration(pool.BaseTransferSeconds, EquipmentStatDomain.TransportSpeed);
                    if (!transfer.ReachedExit && transfer.Reservation == null &&
                        _adjacentContainers.TryGetValue(dir, out var successor) && successor is ConveyorCellAdapter)
                    {
                        var approach = await pool.Transfer(activeCts.Token, entrance, exit, operation.ResourceId,
                            TravelSeconds() * ResourceTravelDistance(entrance, exit));
                        activeCts.Token.ThrowIfCancellationRequested();
                        if (approach != ResourceAnimationResultApplication.Completed)
                        {
                            pool.SetPosition(operation.ResourceId, entrance);
                            await UniTask.Delay(100, cancellationToken: token);
                            continue;
                        }
                        transfer.ReachedExit = true;
                    }

                    var startPos = transfer.ReachedExit ? exit : entrance;
                    var result = await transfer.Coordinator.RunAsync(
                        () => _adjacentContainers.TryGetValue(dir, out var portTarget) ? TargetPortId(portTarget.Identity as CellBaseAdapter, dir) : -1, startPos,
                        fallback,
                        () => _adjacentContainers.TryGetValue(dir, out var adjacent) ? adjacent : null,
                        endpoint => _adjacentContainers.TryGetValue(dir, out var adjacent) &&
                                ReferenceEquals(adjacent.Identity, endpoint.Identity), pool.Resources,
                        pool, () => TravelSeconds() *
                            ResourceTravelDistance(startPos, Destination(operation.Target)), activeCts.Token,
                        resolveDestination: Destination);
                    if (result != ResourceAnimationResultApplication.Completed)
                    {
                        await UniTask.Delay(100, cancellationToken: token);
                        continue;
                    }
                    _pending.Remove(dir);
                    NotifyReceiveSpace(transfer.InputPort);
                    return;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    if (operation.CurrentStage == ResourceTransferOperationApplication.Stage.Cancelled) return;
                    if (operation.CurrentStage != ResourceTransferOperationApplication.Stage.WaitingForReservation)
                        throw;
                    // 接続先だけが失われた。次の接続までIDを保持する。
                    transfer.ResourceOwner.SetPosition(operation.ResourceId, transfer.ReachedExit ? exit : entrance);
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
                if (operation.CurrentStage != ResourceTransferOperationApplication.Stage.Completed)
                {
                    transfer.Coordinator.Cancel();
                    if (operation.ResourceId != 0 && transfer.ResourceOwner != null)
                        transfer.ResourceOwner.DisposeId(operation.ResourceId);
                }
                _pending.Remove(dir);
                if (!_isDisconnected) NotifyReceiveSpace(transfer.InputPort);
            }
        }
    }

    public void Reuse(int portId, int id)
    {
        if (!HasPort(portId, CellPortUseDomain.Input)) return;
        var dir = -PortDirection(portId);
        if (!_pending.TryGetValue(dir, out var transfer)) return;
        if (transfer.Operation.TryAttachId(id)) transfer.ResourceOwner = ResourceItemObjectPoolAdapter.Instance;
    }
}
