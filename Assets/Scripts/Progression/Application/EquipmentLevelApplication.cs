/// <summary>設備一基の実行時レベル。容量を維持するため増加だけを許可する。</summary>
public sealed class EquipmentLevelApplication
{
    public int Level { get; private set; } = 1;
    public bool TrySet(int level)
    {
        if (level < Level || level > EffectiveStatsRulesDomain.MaxLevel) return false;
        Level = level;
        return true;
    }
}
