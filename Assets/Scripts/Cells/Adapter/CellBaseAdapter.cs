using System;
using UnityEngine;

public abstract class CellBaseAdapter : MonoBehaviour, IResourceTransferEndpointApplication
{
    public object Identity => this;
    // インターフェース参照でも、破棄されたUnityオブジェクトを寿命終了として判定する。
    public bool IsAlive => this != null;
    public int XIndex { get; set; }
    public int ZIndex { get; set; }
    public GameObject CellModel { get; private set; }
    private SharedStatEffectsApplication _statEffects;
    private readonly EquipmentLevelApplication _equipmentLevel = new();
    public int EquipmentLevel => _equipmentLevel.Level;
    public virtual bool SupportsProgression => false;

    public void ConfigureProgression(SharedStatEffectsApplication effects)
    {
        if (!SupportsProgression) return;
        if (effects == null) throw new ArgumentNullException(nameof(effects));
        ValidateBaseStats();
        if (ReferenceEquals(_statEffects, effects)) return;
        // 再構成による容量減少を防ぎ、既存の在庫・予約をそのまま保持する。
        if (_statEffects != null)
            foreach (StatBranchDomain branch in Enum.GetValues(typeof(StatBranchDomain)))
                if (effects.GetBonus(branch) < _statEffects.GetBonus(branch))
                    throw new InvalidOperationException("Cannot reduce equipment effects.");
        if (_statEffects != null) _statEffects.Changed -= OnStatsChanged;
        _statEffects = effects;
        _statEffects.Changed += OnStatsChanged;
        OnStatsChanged();
    }

    public bool TrySetEquipmentLevel(int level)
    {
        var previous = EquipmentLevel;
        if (!TrySetEquipmentLevelWithoutNotification(level)) return false;
        if (previous != EquipmentLevel) OnStatsChanged();
        return true;
    }

    // 購入経路は残高の確定後に通知し、購読先が支出と能力を同時に読み取れるようにする。
    internal bool TrySetEquipmentLevelWithoutNotification(int level)
    {
        if (!SupportsProgression || level < EquipmentLevel || level > EffectiveStatsRulesDomain.MaxLevel) return false;
        ValidateBaseStats();
        if (level == EquipmentLevel) return true;
        _equipmentLevel.TrySet(level);
        return true;
    }

    internal void NotifyEquipmentStatsChanged() => OnStatsChanged();

    protected int EffectiveStat(int baseValue, StatBranchDomain branch)
        => EffectiveStatsRulesDomain.Calculate(baseValue, EquipmentLevel, _statEffects?.GetBonus(branch) ?? 0);
    protected virtual void ValidateBaseStats() { }
    protected virtual void OnStatsChanged() { }
    protected virtual void OnDestroy()
    {
        if (_statEffects != null) _statEffects.Changed -= OnStatsChanged;
    }

    private void Awake()
    {
        CellModel = transform.GetChild(0).gameObject;
    }

    public virtual void InitializeSystem()
    {
    }
}
