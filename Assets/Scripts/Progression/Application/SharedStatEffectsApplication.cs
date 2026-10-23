using System;

/// <summary>一プレイの共有効果。増加だけを許可し、成立済み予約の容量を維持する。</summary>
public sealed class SharedStatEffectsApplication
{
    private readonly int[] _bonuses = new int[3];
    public event Action Changed;
    public Exception LastNotificationException { get; private set; }

    public int GetBonus(StatBranchDomain branch)
    {
        var index = (int)branch;
        if (index < 0 || index >= _bonuses.Length) throw new ArgumentOutOfRangeException(nameof(branch));
        return _bonuses[index];
    }

    public bool TrySetBonus(StatBranchDomain branch, int percent)
    {
        var previous = GetBonus(branch);
        if (percent < previous || percent > EffectiveStatsRulesDomain.MaxBonusPercent) return false;
        if (percent == previous) return true;
        SetBonusWithoutNotification(branch, percent);
        NotifyChanged();
        return true;
    }

    internal void SetBonusWithoutNotification(StatBranchDomain branch, int percent)
    {
        if (percent < GetBonus(branch) || percent > EffectiveStatsRulesDomain.MaxBonusPercent)
            throw new ArgumentOutOfRangeException(nameof(percent));
        _bonuses[(int)branch] = percent;
    }

    internal void NotifyChanged()
    {
        var handlers = Changed;
        if (handlers == null) return;
        // 一つの表示購読の失敗で、残りのセルへの能力通知を止めない。
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); } catch (Exception exception) { LastNotificationException = exception; }
    }
}
