using System;

public enum EquipmentUpgradeResultApplication
{
    Success, Busy, InvalidTarget, LevelChanged, MaxLevel, InvalidPrice, InsufficientFunds, Failed
}

/// <summary>登録と寿命の確認、通知を伴わないレベル確定をセル接続側へ委ねる。</summary>
public interface IEquipmentUpgradeTargetApplication
{
    bool IsAvailable { get; }
    CellTypeDomain Type { get; }
    int Level { get; }
    bool TrySetLevelWithoutNotification(int expectedLevel, int nextLevel);
    void NotifyChanged();
}

/// <summary>期待レベルを一回の購入条件にし、支出とレベルを通知前に確定する。</summary>
public sealed class EquipmentUpgradeApplication
{
    private readonly CurrencyWalletApplication _wallet;
    private readonly CurrencyPriceCatalogDomain _prices;
    private bool _purchasing;
    public Exception LastException { get; private set; }

    public EquipmentUpgradeApplication(CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices)
    {
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
    }

    public EquipmentUpgradeResultApplication CanPurchase(IEquipmentUpgradeTargetApplication target, int expectedLevel, out long cost)
    {
        cost = 0;
        if (target == null || !target.IsAvailable) return EquipmentUpgradeResultApplication.InvalidTarget;
        if (target.Level != expectedLevel) return EquipmentUpgradeResultApplication.LevelChanged;
        if (expectedLevel >= EffectiveStatsRulesDomain.MaxLevel) return EquipmentUpgradeResultApplication.MaxLevel;
        if (expectedLevel < 1 || !_prices.TryGetPlacementAmount(target.Type, expectedLevel, out cost))
            return EquipmentUpgradeResultApplication.InvalidPrice;
        return _wallet.Current.AvailableBalance < cost
            ? EquipmentUpgradeResultApplication.InsufficientFunds : EquipmentUpgradeResultApplication.Success;
    }

    public EquipmentUpgradeResultApplication Purchase(IEquipmentUpgradeTargetApplication target, int expectedLevel)
    {
        if (_purchasing) return EquipmentUpgradeResultApplication.Busy;
        _purchasing = true;
        CurrencyTransactionApplication expense = null;
        try
        {
            LastException = null;
            var result = CanPurchase(target, expectedLevel, out var cost);
            if (result != EquipmentUpgradeResultApplication.Success) return result;
            var type = target.Type;
            if (!_wallet.TryReserveExpense(cost, out expense)) return EquipmentUpgradeResultApplication.InsufficientFunds;
            // 支出予約の通知中に撤去・置換・レベル変更が起きた場合は全額を解放する。
            if (!target.IsAvailable || target.Type != type) return EquipmentUpgradeResultApplication.InvalidTarget;
            if (target.Level != expectedLevel) return EquipmentUpgradeResultApplication.LevelChanged;
            if (!target.TrySetLevelWithoutNotification(expectedLevel, expectedLevel + 1))
                return EquipmentUpgradeResultApplication.InvalidTarget;
            expense.TryCommit();
            // 購読者の例外でも成立済みの支払いとレベルを保つ。
            try { target.NotifyChanged(); }
            catch (Exception exception) { LastException = exception; }
            return EquipmentUpgradeResultApplication.Success;
        }
        catch (Exception exception)
        {
            LastException = exception;
            return EquipmentUpgradeResultApplication.Failed;
        }
        finally
        {
            expense?.TryCancel();
            _purchasing = false;
        }
    }
}
