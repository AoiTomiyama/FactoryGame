using System;
using System.Linq;

/// <summary>搬入が確定した資源を換金する。納品進行と資源IDの所有は持たない。</summary>
public sealed class ShippingCellAdapter : ConnectableCellBaseAdapter, IContainableApplication
{
    private ShippingIntakeApplication _intake;
    private bool _initialized;
    public void Configure(CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices)
    {
        if (_intake != null) throw new InvalidOperationException("Shipping cell is already configured.");
        _intake = new ShippingIntakeApplication(wallet, prices, Ports.Select(p => p.Id));
    }
    public override void InitializeSystem()
    {
        if (_intake == null) throw new InvalidOperationException("Configure shipping before connecting it.");
        OnDisconnected -= StopIntake; OnDisconnected += StopIntake;
        _initialized = true; _intake.Accepting = isActiveAndEnabled;
        base.InitializeSystem();
    }
    private void OnEnable() { if (_initialized) InitializeSystem(); }
    private void StopIntake()
    {
        if (_intake == null) return;
        _intake.Accepting = false; _intake.CancelAll();
    }
    private void OnDisable()
    {
        // 接続の取消を先に伝え、再入時にも新しい換金予約を受け付けない。
        if (_intake != null) _intake.Accepting = false;
        OnDisconnect(); StopIntake();
    }
    protected override void OnDestroy() { base.OnDestroy(); StopIntake(); }
    public bool AllocateStorage(int portId, int amount, ResourceTypeDomain type)
        => isActiveAndEnabled && HasPort(portId, CellPortUseDomain.Input) && _intake != null && _intake.TryReserve(portId, amount, type);
    public void StoreResource(int portId, int amount) => _intake?.Commit(portId, amount);
    public void CancelStorage(int portId, int amount, ResourceTypeDomain type) => _intake?.Cancel(portId, amount, type);
}
