using System;
using System.Collections.Generic;

/// <summary>一プレイの共有効果。増加だけを許可し、成立済み予約の容量を維持する。</summary>
public sealed class SharedStatEffectsApplication
{
    private readonly Dictionary<(CellTypeDomain, EquipmentStatDomain), StatModifierDomain> _modifiers = new();
    private readonly Dictionary<(CellTypeDomain, EquipmentStatDomain), HashSet<int>> _bases = new();
    public StatModifierDomain GetModifier(CellTypeDomain type, EquipmentStatDomain stat)
        => _modifiers.TryGetValue((type, stat), out var modifier) ? modifier : StatModifierDomain.Default;
    public void RegisterBase(CellTypeDomain type, EquipmentStatDomain stat, int value)
    {
        EffectiveStatsRulesDomain.Calculate(value, GetModifier(type, stat));
        if (!_bases.TryGetValue((type, stat), out var values)) _bases[(type, stat)] = values = new HashSet<int>();
        values.Add(value);
    }
    public bool CanApply(CellTypeDomain type, EquipmentStatDomain stat, StatModifierDomain modifier)
    {
        if (!SkillTreeCatalogDomain.SupportsStat(type, stat)) return false;
        var previous = GetModifier(type, stat);
        try
        {
            if (!_bases.TryGetValue((type, stat), out var values)) return EffectiveStatsRulesDomain.Calculate(1, modifier) >= 1;
            foreach (var value in values)
                if (EffectiveStatsRulesDomain.Calculate(value, modifier) < EffectiveStatsRulesDomain.Calculate(value, previous)) return false;
            return true;
        }
        catch (OverflowException) { return false; }
    }
    internal void ApplyWithoutNotification(CellTypeDomain type, EquipmentStatDomain stat, StatModifierDomain modifier)
    {
        if (!CanApply(type, stat, modifier)) throw new InvalidOperationException("Ability value would decrease or overflow.");
        _modifiers[(type, stat)] = modifier;
    }
    public event Action Changed;
    public Exception LastNotificationException { get; private set; }

    internal void NotifyChanged()
    {
        var handlers = Changed;
        if (handlers == null) return;
        // 一つの表示購読の失敗で、残りのセルへの能力通知を止めない。
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); } catch (Exception exception) { LastNotificationException = exception; }
    }
}
