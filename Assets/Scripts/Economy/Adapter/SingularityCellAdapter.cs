/// <summary>換金せず資源を吸収する特異点。資源IDの終端処理は共有Coordinatorが所有する。</summary>
public sealed class SingularityCellAdapter : ConnectableCellBaseAdapter, IContainableApplication
{
    private AbsorptionIntakeApplication _intake;
    public DeliveryProgressApplication Deliveries { get; private set; }
    public event System.Action<ResourceTypeDomain, int> Absorbed;
    public void Configure(DeliveryProgressApplication deliveries)
    {
        if (deliveries == null) throw new System.ArgumentNullException(nameof(deliveries));
        if (ReferenceEquals(Deliveries, deliveries)) return;
        if (_initialized) throw new System.InvalidOperationException("Cannot replace delivery progress while initialized.");
        Deliveries = deliveries;
        _intake = new AbsorptionIntakeApplication(System.Linq.Enumerable.Select(Ports, p => p.Id), deliveries);
        _intake.Absorbed += NotifyAbsorbed;
    }
    private void NotifyAbsorbed(ResourceTypeDomain type, int amount)
    {
        if (Absorbed == null) return;
        foreach (System.Action<ResourceTypeDomain, int> handler in Absorbed.GetInvocationList())
            try { handler(type, amount); } catch (System.Exception exception) { UnityEngine.Debug.LogException(exception, this); }
    }
    private bool _initialized;
    public override void InitializeSystem()
    {
        if (_intake == null) throw new System.InvalidOperationException("Configure delivery progress before initializing singularity.");
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
