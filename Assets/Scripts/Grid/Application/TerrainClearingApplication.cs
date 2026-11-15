using System;

/// <summary>整地一式の支払いと置換を対応付け、予約通知後にも取得状態と対象を確認する。</summary>
public sealed class TerrainClearingApplication
{
    public const string SkillUid = "terrain-clearing";
    public enum Result { Success, Locked, InvalidTarget, InsufficientFunds, Busy, Failed }
    private readonly CurrencyWalletApplication _wallet;
    private readonly Func<bool> _isUnlocked;
    private bool _executing;
    public Exception LastException { get; private set; }
    public bool IsUnlocked => _isUnlocked();

    public TerrainClearingApplication(CurrencyWalletApplication wallet, Func<bool> isUnlocked)
    {
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _isUnlocked = isUnlocked ?? throw new ArgumentNullException(nameof(isUnlocked));
    }

    public Result Execute(TerrainClearingRulesDomain.Quote quote, Func<bool> validate,
        Action install, Action rollback, Action complete)
    {
        if (_executing) return Result.Busy;
        _executing = true;
        LastException = null;
        CurrencyTransactionApplication expense = null;
        var installing = false;
        var installed = false;
        try
        {
            if (!IsUnlocked) return Result.Locked;
            if (quote == null || !validate()) return Result.InvalidTarget;
            if (!_wallet.TryReserveExpense(quote.Cost, out expense)) return Result.InsufficientFunds;
            if (!IsUnlocked) return Result.Locked;
            if (!validate()) return Result.InvalidTarget;
            installing = true;
            install();
            installed = true;
            // 完了通知の失敗で、設置済みの空き地だけが無料になることを防ぐ。
            try { complete(); }
            catch (Exception exception) { LastException = exception; }
            expense.TryCommit();
            return Result.Success;
        }
        catch (Exception exception)
        {
            LastException = exception;
            if (installing && !installed)
            {
                try { rollback(); }
                catch (Exception rollbackError) { LastException = new AggregateException(exception, rollbackError); }
            }
            return Result.Failed;
        }
        finally
        {
            expense?.TryCancel();
            _executing = false;
        }
    }
}
