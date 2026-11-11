/// <summary>換金せず資源を吸収する特異点。資源IDの終端処理は共有Coordinatorが所有する。</summary>
public sealed class SingularityCellAdapter : ConnectableCellBaseAdapter, IContainableApplication
{
    private AbsorptionIntakeApplication _intake;
    private AbsorptionIntakeApplication Intake => _intake ??= new AbsorptionIntakeApplication(System.Linq.Enumerable.Select(Ports, p => p.Id));
    public event System.Action<ResourceTypeDomain, int> Absorbed
    { add => Intake.Absorbed += value; remove { if (_intake != null) _intake.Absorbed -= value; } }
    private bool _initialized;
    public override void InitializeSystem()
    {
        _ = Intake;
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
        if (_intake != null) _intake.Accepting = false;
        OnDisconnect(); StopIntake();
    }
    protected override void OnDestroy() { base.OnDestroy(); StopIntake(); }
    public bool AllocateStorage(int portId, int amount, ResourceTypeDomain type)
        => isActiveAndEnabled && HasPort(portId, CellPortUseDomain.Input) && _intake != null && _intake.TryReserve(portId, amount, type);
    public void StoreResource(int portId, int amount) => _intake?.Commit(portId, amount);
    public void CancelStorage(int portId, int amount, ResourceTypeDomain type) => _intake?.Cancel(portId, amount, type);
}
