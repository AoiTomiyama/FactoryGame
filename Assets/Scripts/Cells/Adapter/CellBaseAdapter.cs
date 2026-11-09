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
    public virtual CellTypeDomain ProgressionType => CellTypeDomain.None;
    public bool SupportsProgression => SkillTreeCatalogDomain.SupportsType(ProgressionType);

    public void ConfigureProgression(SharedStatEffectsApplication effects)
    {
        if (!SupportsProgression) return;
        if (effects == null) throw new ArgumentNullException(nameof(effects));
        if (ReferenceEquals(_statEffects, effects)) return;
        // 構成を替える場合も現在の能力を減らさず、成立済みの在庫と予約を保つ。
        foreach (EquipmentStatDomain stat in Enum.GetValues(typeof(EquipmentStatDomain)))
        {
            var previous = _statEffects?.GetModifier(ProgressionType, stat) ?? StatModifierDomain.Default;
            var next = effects.GetModifier(ProgressionType, stat);
            if (next.Multiplier < previous.Multiplier || next.Addition < previous.Addition)
                throw new InvalidOperationException("Cannot reduce configured ability.");
        }
        var previousEffects = _statEffects;
        _statEffects = effects;
        try { ValidateBaseStats(); } catch { _statEffects = previousEffects; throw; }
        if (previousEffects != null) previousEffects.Changed -= OnStatsChanged;
        _statEffects.Changed += OnStatsChanged;
        OnStatsChanged();
    }

    protected int EffectiveStat(int baseValue, EquipmentStatDomain stat)
        => EffectiveStatsRulesDomain.Calculate(baseValue, _statEffects?.GetModifier(ProgressionType, stat) ?? StatModifierDomain.Default);
    protected void ValidateStat(int baseValue, EquipmentStatDomain stat)
    {
        EffectiveStatsRulesDomain.Calculate(baseValue, StatModifierDomain.Default);
        _statEffects?.RegisterBase(ProgressionType, stat, baseValue);
    }
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
