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
    [SerializeField, Min(1)] private int footprintWidth = 1;
    [SerializeField, Min(1)] private int footprintDepth = 1;
    [SerializeField] private Vector2Int footprintOffset;
    public CellFootprintDomain Footprint => new(footprintWidth, footprintDepth, footprintOffset.x, footprintOffset.y);
    public int QuarterTurns => Mathf.RoundToInt(transform.eulerAngles.y / 90f);
    public System.Collections.Generic.IEnumerable<(int x, int z)> OccupiedCoordinates
        => Footprint.Coordinates(XIndex, ZIndex, QuarterTurns);
    public Vector3 WorldCenter => transform.TransformPoint(new Vector3(
        footprintOffset.x + (footprintWidth - 1) * .5f, 0, footprintOffset.y + (footprintDepth - 1) * .5f));
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
        ApplyFootprintCollider();
    }

    public void ApplyFootprintCollider()
    {
        if (!TryGetComponent<BoxCollider>(out var box)) return;
        var center = new Vector3(footprintOffset.x + (footprintWidth - 1) * .5f,
            box.center.y, footprintOffset.y + (footprintDepth - 1) * .5f);
        var size = new Vector3(footprintWidth, box.size.y, footprintDepth);
        if (box.center != center) box.center = center;
        if (box.size != size) box.size = size;
    }
    private void OnValidate() { if (footprintWidth > 0 && footprintDepth > 0) ApplyFootprintCollider(); }

    public virtual void InitializeSystem()
    {
    }
}
