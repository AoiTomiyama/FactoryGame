using System;
using UnityEngine;

/// <summary>消費専用の搬送先。資源IDの終端処理は共有Coordinatorが所有する。</summary>
public sealed class SingularityCellAdapter : ConnectableCellBaseAdapter, IContainableApplication
{
    private SingularityIntakeApplication _intake;
    private bool _initialized;

    public void Configure(CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices)
    {
        if (_intake != null) throw new InvalidOperationException("Singularity is already configured.");
        _intake = new SingularityIntakeApplication(wallet, prices);
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
        // 送り元の接続取消を先に終え、再有効化まで新規予約を止める。
        if (_intake != null) _intake.Accepting = false;
        OnDisconnect();
        StopIntake();
    }
    private void OnDestroy() => StopIntake();

    private static int Input(Vector3Int dir)
        => dir == Vector3Int.right ? 0 : dir == Vector3Int.left ? 1 :
           dir == Vector3Int.forward ? 2 : dir == Vector3Int.back ? 3 : -1;
    public bool AllocateStorage(Vector3Int dir, int amount, ResourceTypeDomain type)
        => isActiveAndEnabled && _intake != null && _intake.TryReserve(Input(dir), amount, type);
    public void StoreResource(Vector3Int dir, int amount) => _intake?.Commit(Input(dir), amount);
    public void CancelStorage(Vector3Int dir, int amount, ResourceTypeDomain type)
        => _intake?.Cancel(Input(dir), amount, type);
}
