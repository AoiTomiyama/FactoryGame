using System;

public enum EquipmentStatDomain { ExtractionAmount, Capacity, InputCapacity, OutputCapacity, TransferAmount, CraftingSpeed, TransportSpeed }

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
    public static bool IsSpeed(EquipmentStatDomain stat) => stat is EquipmentStatDomain.CraftingSpeed or EquipmentStatDomain.TransportSpeed;
    public static float CalculateDuration(float baseSeconds, StatModifierDomain modifier)
    {
        if (float.IsNaN(baseSeconds) || float.IsInfinity(baseSeconds) || baseSeconds < 0 || modifier.Multiplier < 1 || modifier.Addition != 0)
            throw new ArgumentOutOfRangeException(nameof(baseSeconds), "Duration requires finite time and a speed multiplier of at least one without addition.");
        var seconds = (float)(baseSeconds / (double)modifier.Multiplier);
        if (baseSeconds > 0 && seconds <= 0) throw new ArgumentOutOfRangeException(nameof(baseSeconds), "Positive production time underflowed.");
        return seconds;
    }
    public static int Calculate(int baseValue, StatModifierDomain modifier)
    {
        if (baseValue <= 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
        return checked((int)Math.Max(1, decimal.Floor(baseValue * modifier.Multiplier + modifier.Addition)));
    }
}
