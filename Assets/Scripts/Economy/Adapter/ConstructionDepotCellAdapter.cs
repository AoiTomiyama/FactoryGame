using System;
using System.Linq;

/// <summary>建設資材庫は搬入専用。確定在庫の所有者は共有Applicationとし、撤去後も在庫を保つ。</summary>
public sealed class ConstructionDepotCellAdapter : ConnectableCellBaseAdapter, IContainableApplication
{
    private ConstructionDepotIntakeApplication _intake;
    private bool _initialized;
    public ConstructionStockApplication Stock { get; private set; }
    public event Action StatusChanged;
    public void Configure(ConstructionStockApplication stock)
    {
        if (_intake != null) throw new InvalidOperationException("Construction depot is already configured.");
        Stock = stock ?? throw new ArgumentNullException(nameof(stock));
        _intake = new ConstructionDepotIntakeApplication(stock, Ports.Select(p => p.Id));
        Stock.Changed += Notify;
    }
    private void Notify() => StatusChanged?.Invoke();
    public override void InitializeSystem()
    {
        if (_intake == null) throw new InvalidOperationException("Configure construction stock before connecting the depot.");
        OnDisconnected -= StopIntake; OnDisconnected += StopIntake;
        _initialized = true; _intake.Accepting = isActiveAndEnabled;
        base.InitializeSystem();
    }
    private void OnEnable() { if (_initialized) InitializeSystem(); }
    private void StopIntake()
    { if (_intake != null) { _intake.Accepting = false; _intake.CancelAll(); } }
    private void OnDisable()
    {
        if (_intake != null) _intake.Accepting = false;
        OnDisconnect(); StopIntake();
    }
    protected override void OnDestroy()
    {
        base.OnDestroy(); StopIntake(); if (Stock != null) Stock.Changed -= Notify;
    }
    public bool AllocateStorage(int port, int amount, ResourceTypeDomain resource)
        => isActiveAndEnabled && HasPort(port, CellPortUseDomain.Input) && _intake != null && _intake.TryReserve(port, amount, resource);
    public void StoreResource(int port, int amount) => _intake?.Commit(port, amount);
    public void CancelStorage(int port, int amount, ResourceTypeDomain resource) => _intake?.Cancel(port, amount, resource);
}
