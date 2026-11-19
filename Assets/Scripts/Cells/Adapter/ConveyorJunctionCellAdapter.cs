using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class ConveyorJunctionCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IResourceReusableApplication
{
    [SerializeField] private ExportConveyorCellAdapter exportDefinition;
    [SerializeField] private Material routeMaterial;
    [SerializeField] private Transform beltModel, beltSurface;
    internal Material RouteMaterial => routeMaterial;
    internal void ShowBelt(Transform root, ConveyorRouteRulesDomain.Route route) =>
        ConveyorRouteViewAdapter.ShowBelt(root, route, beltModel, beltSurface);
    private ConveyorJunctionApplication _junction;
    private ConveyorRouteRulesDomain.Route _route;
    private readonly Dictionary<int, (Connection Link, IContainableApplication Target, Action<int?> Wake)> _outputs = new();
    private CancellationTokenSource _lifetime, _attempt;
    private UniTaskCompletionSource _wake;
    private ResourceTransferCoordinatorApplication _active;
    private int _activePort = -1;
    private SaveGameApplication.Slot[] _restoredBatches;
    private ResourceItemObjectPoolAdapter _pool;
    public override CellTypeDomain ProgressionType => CellTypeDomain.Crossing;
    internal ConveyorRouteRulesDomain.Route LocalRoute => _route;
    internal ConveyorRouteRulesDomain.Route WorldRoute => ConveyorRouteRulesDomain.Rotate(_route, QuarterTurns);
    internal int InputCursor => _junction.InputCursor;
    internal int OutputCursor => _junction.OutputCursor;

    internal void ConfigureWorldRoute(ConveyorRouteRulesDomain.Route route) =>
        ConfigureRoute(ConveyorRouteRulesDomain.Rotate(route, -QuarterTurns));

    internal void ConfigureRoute(ConveyorRouteRulesDomain.Route route, int inputCursor = 0, int outputCursor = 0)
    {
        if (route.Type is not (ConveyorRouteRulesDomain.Kind.Split or ConveyorRouteRulesDomain.Kind.Merge) || _junction != null)
            throw new InvalidOperationException("Junction requires a fresh split or merge route.");
        _route = route;
        _junction = new ConveyorJunctionApplication(Ports.Where(p => HasPort(p.Id, CellPortUseDomain.Input)).Select(p => p.Id).OrderBy(p => p).ToArray(),
            Ports.Where(p => HasPort(p.Id, CellPortUseDomain.Output)).Select(p => p.Id).OrderBy(p => p).ToArray(), inputCursor, outputCursor);
    }

    protected override CellPortUseDomain PortUse(CellPortDefinition port) => RoutePortUse(port, _route);

    public override void InitializeSystem()
    {
        if (_junction == null) throw new InvalidOperationException("Junction has no route.");
        _pool = ResourceItemObjectPoolAdapter.Instance;
        _lifetime = new CancellationTokenSource();
        OnGetConnectedCell += Connect;
        OnLostConnectedCell += Disconnect;
        OnDisconnected += Shutdown;
        ShowBelt(CellModel.transform, _route);
        if (_restoredBatches != null)
        {
            foreach (var slot in _restoredBatches)
                AcceptPulledBatch(slot.port, slot.amount, SaveKeysApplication.Resource(slot.resource));
            _restoredBatches = null;
        }
        base.InitializeSystem();
        DispatchAsync(_lifetime.Token).Forget();
    }

    private void Connect(Connection connection)
    {
        if (HasPort(connection.PortId, CellPortUseDomain.Input))
            ConveyorLanePickupAdapter.Start(this, connection, exportDefinition, () => _junction.At(connection.PortId) == null,
                (amount, type) => AcceptPulledBatch(connection.PortId, amount, type), _lifetime.Token);
        if (!HasPort(connection.PortId, CellPortUseDomain.Output) || connection.Neighbor is not IContainableApplication target) return;
        void Wake(int? port) { if (!port.HasValue || port == connection.NeighborPortId) _wake?.TrySetResult(); }
        target.ReceiveSpaceAvailable += Wake;
        _outputs.Add(connection.PortId, (connection, target, Wake));
        Wake(null);
    }

    private void Disconnect(CellBaseAdapter cell)
    {
        foreach (var pair in _outputs.Where(pair => ReferenceEquals(pair.Value.Target.Identity, cell)).ToArray())
        {
            _outputs.Remove(pair.Key);
            pair.Value.Target.ReceiveSpaceAvailable -= pair.Value.Wake;
            if (_activePort != pair.Key) continue;
            _active?.WaitForNewTarget();
            _attempt?.Cancel();
        }
        _wake?.TrySetResult();
    }

    private void AcceptPulledBatch(int port, int amount, ResourceTypeDomain type)
    {
        if (!AllocateStorage(port, amount, type)) throw new InvalidOperationException("Junction input is already reserved.");
        var id = _pool.CreateIdFromResourceData(type, amount);
        Reuse(port, id);
        _pool.SetPosition(id, HoldingPosition(port));
        StoreResource(port, amount);
    }

    private Vector3 HoldingPosition(int port) => transform.position + (Vector3)PortDirection(port) * .24f +
        Vector3.up * ResourceDisplayHeight;

    public bool AllocateStorage(int port, int amount, ResourceTypeDomain type) =>
        _lifetime != null && _junction.Reserve(port, amount, type);

    public void Reuse(int port, int id)
    {
        if (_junction.At(port) is { Id: 0 } batch) batch.Id = id;
    }

    public void StoreResource(int port, int amount)
    {
        if (_junction.At(port) is not { Id: > 0 } batch || batch.Amount != amount) return;
        batch.Ready = true;
        _pool.SetPosition(batch.Id, HoldingPosition(port));
        _wake?.TrySetResult();
    }

    public void CancelStorage(int port, int amount, ResourceTypeDomain type)
    {
        if (_junction.At(port) is not { Ready: false } batch || batch.Amount != amount || batch.Type != type) return;
        _junction.Release(port);
        if (batch.Id != 0) _pool.DisposeId(batch.Id);
        NotifyReceiveSpace(port);
    }

    private async UniTask DispatchAsync(CancellationToken token)
    {
        try
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            while (!token.IsCancellationRequested)
            {
                var batch = _junction.NextBatch();
                ResourceReservationApplication Reserve(int port) =>
                    _outputs.TryGetValue(port, out var outlet) &&
                    ResourceReservationApplication.TryCreate(outlet.Target, outlet.Link.NeighborPortId, batch.Amount, batch.Type, out var r) ? r : null;
                var wake = new UniTaskCompletionSource();
                _wake = wake;
                if (batch == null || !_junction.TryDispatch(Reserve, out var output, out var reservation))
                {
                    await wake.Task.AttachExternalCancellation(token);
                    await UniTask.NextFrame(cancellationToken: token);
                    continue;
                }

                var direction = PortDirection(output);
                var next = transform.position + direction;
                var start = HoldingPosition(batch.Port);
                var operation = new ResourceTransferOperationApplication(this, reservation.Target, batch.Id, batch.Type, batch.Amount);
                operation.TryMarkReserved();
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token);
                _active = new ResourceTransferCoordinatorApplication(operation, reservation);
                _attempt = attempt;
                _activePort = output;
                try
                {
                    var result = await _active.RunAsync(() => reservation.PortId, start, next,
                        () => reservation.Target, endpoint => _outputs.TryGetValue(output, out var link) &&
                            ReferenceEquals(link.Target.Identity, endpoint.Identity),
                        _pool.Resources, _pool, () => EffectiveDuration(_pool.BaseTransferSeconds, EquipmentStatDomain.TransportSpeed),
                        attempt.Token, resolveDestination: endpoint => endpoint.Identity is CellBaseAdapter target
                            ? target.ResourceDisplayPosition(next, direction) : next + Vector3.up * ResourceDisplayHeight);
                    if (result == ResourceAnimationResultApplication.Completed)
                    {
                        _junction.Complete(batch, output);
                        NotifyReceiveSpace(batch.Port);
                    }
                    else await UniTask.Delay(100, cancellationToken: token);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                { _pool.SetPosition(batch.Id, start); }
                finally
                {
                    _active = null;
                    _activePort = -1;
                    _attempt = null;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    internal void RestoreBatches(SaveGameApplication.Slot[] slots) => _restoredBatches = slots;
    internal SaveGameApplication.Slot[] CaptureBatches(Action<int> registerOwner) => _junction.Batches
        .Where(batch => batch.Ready && (_active?.Operation.CurrentStage != ResourceTransferOperationApplication.Stage.Completed ||
            _active.Operation.ResourceId != batch.Id))
        .OrderBy(batch => batch.Port).Select(batch =>
        {
            registerOwner(batch.Id);
            return SaveGameApplication.SlotValue(batch.Port, batch.Type, batch.Amount);
        }).ToArray();

    protected override void OnDestroy() { base.OnDestroy(); Shutdown(); }

    private void Shutdown()
    {
        if (_lifetime == null) return;
        foreach (var outlet in _outputs.Values) outlet.Target.ReceiveSpaceAvailable -= outlet.Wake;
        _outputs.Clear();
        var completed = _active?.Operation.CurrentStage == ResourceTransferOperationApplication.Stage.Completed ? _active.Operation.ResourceId : 0;
        _active?.Cancel();
        var lifetime = _lifetime;
        _lifetime = null;
        lifetime.Cancel();
        lifetime.Dispose();
        foreach (var batch in _junction.Batches)
            if (batch.Id != 0 && batch.Id != completed && _pool != null) _pool.DisposeId(batch.Id);
    }
}
