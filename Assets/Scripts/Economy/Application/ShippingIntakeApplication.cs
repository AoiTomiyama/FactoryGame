using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>定義された入力枠に数量・種別・売価と入金余地を固定する。</summary>
public sealed class ShippingIntakeApplication
{
    private sealed class Intake
    {
        public int Amount;
        public ResourceTypeDomain Type;
        public CurrencyTransactionApplication Transaction;
    }
    private readonly Dictionary<int, Intake> _inputs;
    private readonly CurrencyWalletApplication _wallet;
    private readonly CurrencyPriceCatalogDomain _prices;
    public bool Accepting { get; set; }

    public ShippingIntakeApplication(CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices, IEnumerable<int> portIds)
    {
        _inputs = portIds.ToDictionary(id => id, _ => (Intake)null);
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
    }

    public bool TryReserve(int input, int amount, ResourceTypeDomain type)
    {
        if (!Accepting || !_inputs.ContainsKey(input) || _inputs[input] != null ||
            !_prices.TryGetSaleAmount(type, amount, out var income)) return false;
        var intake = new Intake { Amount = amount, Type = type };
        // 残高通知からの再入でも同方向の枠を先に占有する。
        _inputs[input] = intake;
        var reserved = _wallet.TryReserveIncome(income, out var transaction);
        if (!reserved || !Accepting || !ReferenceEquals(_inputs[input], intake))
        {
            if (ReferenceEquals(_inputs[input], intake)) _inputs[input] = null;
            transaction?.TryCancel();
            return false;
        }
        intake.Transaction = transaction;
        return true;
    }

    public void Commit(int input, int amount) => Complete(input, amount, ResourceTypeDomain.None, true);
    public void Cancel(int input, int amount, ResourceTypeDomain type) => Complete(input, amount, type, false);

    private void Complete(int input, int amount, ResourceTypeDomain type, bool commit)
    {
        if (!_inputs.ContainsKey(input)) return;
        var intake = _inputs[input];
        if (intake == null || intake.Transaction == null || intake.Amount != amount ||
            (!commit && intake.Type != type)) return;
        _inputs[input] = null;
        if (commit) intake.Transaction.TryCommit();
        else intake.Transaction.TryCancel();
    }

    public void CancelAll()
    {
        // 全枠を外してから通知する。予約途中の枠はTryReserveへ戻った時に解放する。
        var previous = _inputs.Values.ToArray();
        foreach (var id in _inputs.Keys.ToArray()) _inputs[id] = null;
        foreach (var intake in previous) intake?.Transaction?.TryCancel();
    }
}
