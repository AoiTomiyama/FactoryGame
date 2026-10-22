using System;

public enum StatBranchDomain { Extraction, Transfer, Storage }

public static class EffectiveStatsRulesDomain
{
    public const int MaxLevel = 3;
    public const int MaxBonusPercent = 50;

    public static int Calculate(int baseValue, int level, int bonusPercent)
    {
        if (baseValue <= 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
        if (level < 1 || level > MaxLevel) throw new ArgumentOutOfRangeException(nameof(level));
        if (bonusPercent < 0 || bonusPercent > MaxBonusPercent)
            throw new ArgumentOutOfRangeException(nameof(bonusPercent));
        // 中間値はlongで計算し、切り捨てた有効値がintに収まることを確認する。
        return checked((int)Math.Max(1L, (long)baseValue * level * (100 + bonusPercent) / 100));
    }

    public static void ValidateBase(int baseValue) => Calculate(baseValue, MaxLevel, MaxBonusPercent);
}
