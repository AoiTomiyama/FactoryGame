using System;

/// <summary>設置先の再確認と支出予約を対応付け、失敗時は配置と予約を戻す。</summary>
public sealed class EquipmentPlacementApplication
{
    public enum Result { Success, InvalidTarget, InsufficientFunds, InsufficientMaterials, Failed, Busy }
    private readonly CurrencyWalletApplication _wallet;
    private readonly CurrencyPriceCatalogDomain _prices;
    private readonly ConstructionStockApplication _stock;
    private bool _executing;
    public Exception LastException { get; private set; }

    public EquipmentPlacementApplication(CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices, ConstructionStockApplication stock)
    {
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _prices = prices ?? throw new ArgumentNullException(nameof(prices));
        _stock = stock ?? throw new ArgumentNullException(nameof(stock));
    }

    public Result Execute(CellTypeDomain type, Func<bool> validate, Action install, Action rollback,
        Action complete, out long cost)
    {
        cost = 0;
        if (_executing) return Result.Busy;
        _executing = true;
        CurrencyTransactionApplication expense = null;
        ConstructionStockApplication.Reservation materials = null;
        var installing = false;
        try
        {
            LastException = null;
            if (!validate() || type != CellTypeDomain.Empty && !_prices.TryGetPlacementAmount(type, 1, out cost))
                return Result.InvalidTarget;
            if (!_prices.TryGetMaterialCosts(type, 1, out var requirements)) return Result.InvalidTarget;
            if (!_stock.TryReserveExpense(requirements, out materials)) return Result.InsufficientMaterials;
            if (cost > 0 && !_wallet.TryReserveExpense(cost, out expense)) return Result.InsufficientFunds;
            // 残高通知から対象が変わる場合があるため、予約後にもう一度確認する。
            if (!validate()) return Result.InvalidTarget;
            installing = true;
            install();
        }
        catch (Exception exception)
        {
            LastException = exception;
            return Result.Failed;
        }
        finally
        {
            if (LastException != null && installing)
            {
                try { rollback(); }
                catch (Exception exception) { LastException = new AggregateException(LastException, exception); }
                finally { materials?.TryCancel(); expense?.TryCancel(); _executing = false; }
            }
            else if (!installing)
            {
                materials?.TryCancel(); expense?.TryCancel();
                _executing = false;
            }
        }

        // 初期化が成功した後に旧セルを退役させ、確定通知から完成状態を読めるようにする。
        try
        {
            try { complete(); }
            catch (Exception exception) { LastException = exception; }
            materials?.TryCommit();
            expense?.TryCommit();
            return Result.Success;
        }
        finally { _executing = false; }
    }
}
