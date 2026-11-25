using System;
using System.Threading;

internal sealed class ConveyorLanePickupAdapter : IDisposable
{
    private readonly ConnectableCellBaseAdapter _owner;
    private readonly ConnectableCellBaseAdapter.Connection _connection;
    private readonly IExportableApplication _source;
    private readonly ConveyorPickupJobsAdapter _jobs;
    private readonly Func<bool> _canReceive;
    private readonly Func<int> _limit;
    private readonly Action<IExportableApplication, int, int> _take;
    private CancellationTokenRegistration _cancellation;
    private bool _disposed;

    internal bool IsQueued { get; set; }
    private int Port => _connection.NeighborPortId;

    private ConveyorLanePickupAdapter(ConnectableCellBaseAdapter owner, ConnectableCellBaseAdapter.Connection connection,
        IExportableApplication source, Func<bool> canReceive, Func<int> limit,
        Action<IExportableApplication, int, int> take, CancellationToken token)
    {
        _owner = owner;
        _connection = connection;
        _source = source;
        _jobs = GridFieldDatabaseAdapter.Instance.ConveyorPickupJobs;
        _canReceive = canReceive;
        _limit = limit;
        _take = take;
        _source.ExportStockAvailable += Queue;
        _owner.ReceiveSpaceAvailable += OnReceiveSpace;
        _connection.Closed += Dispose;
        _cancellation = token.Register(Dispose);
        Queue();
    }

    internal static ConveyorLanePickupAdapter Start(ConnectableCellBaseAdapter owner,
        ConnectableCellBaseAdapter.Connection connection, Func<bool> canReceive, Func<int> limit,
        Action<IExportableApplication, int, int> take, CancellationToken token)
    {
        if (token.IsCancellationRequested || connection.Neighbor is not IExportableApplication source ||
            !connection.Neighbor.HasPort(connection.NeighborPortId, CellPortUseDomain.Output)) return null;
        return new ConveyorLanePickupAdapter(owner, connection, source, canReceive, limit, take, token);
    }

    internal static void Start(ConnectableCellBaseAdapter owner, ConnectableCellBaseAdapter.Connection connection,
        ExportConveyorCellAdapter definition, Func<bool> canReceive, Action<int, ResourceTypeDomain> accept, CancellationToken token)
    {
        if (definition == null) return;
        Start(owner, connection, canReceive, () =>
        {
            var modifier = GridFieldDatabaseAdapter.Instance.StatEffects.GetModifier(
                CellTypeDomain.ExportConveyor, EquipmentStatDomain.TransferAmount);
            return EffectiveStatsRulesDomain.Calculate(definition.BaseTransferAmount, modifier);
        }, (source, port, limit) =>
        {
            if (source.TryExport(port, limit, out var amount, out var type)) accept(amount, type);
        }, token);
    }

    internal void Queue()
    {
        if (!_disposed) _jobs.Enqueue(this);
    }

    private void OnReceiveSpace(int? port)
    {
        if (!port.HasValue || port.Value == _connection.PortId) Queue();
    }

    private bool IsReady() => !_disposed && _owner != null && _connection.Neighbor != null &&
        _connection.Neighbor.isActiveAndEnabled &&
        _owner.Connections.TryGetValue(_connection.PortId, out var current) &&
        ReferenceEquals(current, _connection) && _canReceive();

    internal bool TryCapture(out ConveyorPickupJobsAdapter.Request request)
    {
        request = default;
        if (!IsReady()) return false;
        var stock = _source.ExportStock;
        if (stock <= 0) return false;
        request = new ConveyorPickupJobsAdapter.Request(stock, _limit());
        return true;
    }

    internal void Apply(ConveyorPickupJobsAdapter.Request request, int amount)
    {
        if (amount <= 0 || !IsReady()) return;
        var limit = _limit();
        // 同じ設備からの搬出や強化が先に確定した場合は、現在の在庫と上限を使う。
        if (request.Stock == _source.ExportStock && request.Limit == limit) limit = amount;
        _take(_source, Port, limit);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.ExportStockAvailable -= Queue;
        _owner.ReceiveSpaceAvailable -= OnReceiveSpace;
        _connection.Closed -= Dispose;
        _cancellation.Dispose();
    }
}
