using System;
using UnityEngine;

/// <summary>消費専用の搬送先。資源IDの終端処理は共有Coordinatorが所有する。</summary>
public sealed class SingularityCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IDataProvidableApplication
{
    private CurrencyWalletApplication _wallet;
    private SingularityProviderDefinition _statusDefinition;
    public event Action StatusChanged;
    public IUIDataProviderApplication GetDataProvider()
    {
        if (_statusDefinition == null) _statusDefinition = ScriptableObject.CreateInstance<SingularityProviderDefinition>();
        return CellStatusProviderAdapter.CreateCustom(_statusDefinition, label => new TextElementDataApplication("", label switch
        {
            LabelEnumApplication.CellName => "特異点",
            LabelEnumApplication.Location => $"({XIndex}, {ZIndex})",
            LabelEnumApplication.Amount => (_wallet?.Current.Balance ?? 0).ToString(),
            LabelEnumApplication.Reserved => (_wallet?.Current.ReservedIncome ?? 0).ToString(),
            _ => throw new ArgumentException("Unsupported singularity label.")
        }));
    }
    private void OnWalletChanged(CurrencyWalletApplication.Snapshot _) => StatusChanged?.Invoke();
    private SingularityIntakeApplication _intake;
    private bool _initialized;

    public void Configure(CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices)
    {
        if (_intake != null) throw new InvalidOperationException("Singularity is already configured.");
        _intake = new SingularityIntakeApplication(wallet, prices, System.Linq.Enumerable.Select(Ports, p => p.Id));
        _wallet = wallet;
        if (isActiveAndEnabled) _wallet.Changed += OnWalletChanged;
    }

    public override void InitializeSystem()
    {
        if (_intake == null) throw new InvalidOperationException("Configure singularity before connecting it.");
        OnDisconnected -= StopIntake;
        OnDisconnected += StopIntake;
        _initialized = true;
        _intake.Accepting = isActiveAndEnabled;
        base.InitializeSystem();
    }

    private void OnEnable()
    {
        if (_wallet != null) { _wallet.Changed -= OnWalletChanged; _wallet.Changed += OnWalletChanged; }
        if (_initialized) InitializeSystem();
    }
    private void StopIntake()
    {
        if (_intake == null) return;
        _intake.Accepting = false;
        _intake.CancelAll();
    }
    private void OnDisable()
    {
        if (_wallet != null) _wallet.Changed -= OnWalletChanged;
        // 送り元の接続取消を先に終え、再有効化まで新規予約を止める。
        if (_intake != null) _intake.Accepting = false;
        OnDisconnect();
        StopIntake();
    }
    protected override void OnDestroy()
    {
        if (_wallet != null) _wallet.Changed -= OnWalletChanged;
        if (_statusDefinition != null) Destroy(_statusDefinition);
        base.OnDestroy(); StopIntake();
    }

    public bool AllocateStorage(int portId, int amount, ResourceTypeDomain type)
        => isActiveAndEnabled && HasPort(portId, CellPortUseDomain.Input) && _intake != null && _intake.TryReserve(portId, amount, type);
    public void StoreResource(int portId, int amount) => _intake?.Commit(portId, amount);
    public void CancelStorage(int portId, int amount, ResourceTypeDomain type)
        => _intake?.Cancel(portId, amount, type);
}
