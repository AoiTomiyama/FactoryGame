using System;

public enum EquipmentStatDomain { ExtractionAmount, Capacity, InputCapacity, OutputCapacity, TransferAmount }

/// <summary>ノードに設定する取得後の倍率と加算値。型全体の同じ能力へ適用する。</summary>
public readonly struct StatModifierDomain
{
    public decimal Multiplier { get; }
    public int Addition { get; }
    public StatModifierDomain(decimal multiplier, int addition)
    {
        if (multiplier < 0 || multiplier > int.MaxValue || addition < 0 || multiplier == 0 && addition == 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        Multiplier = multiplier; Addition = addition;
    }
    public static StatModifierDomain Default => new(1, 0);
}

public static class EffectiveStatsRulesDomain
{
    public static int Calculate(int baseValue, StatModifierDomain modifier)
    {
        if (baseValue <= 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
        return checked((int)Math.Max(1, decimal.Floor(baseValue * modifier.Multiplier + modifier.Addition)));
    }
}
