/// <summary>予約額を固定し、確定と取消のうち一つだけを実行する。</summary>
public sealed class CurrencyTransactionApplication
{
    public enum Result { Pending, Committed, Cancelled }

    private readonly CurrencyWalletApplication _wallet;
    public long Amount { get; }
    public bool IsIncome { get; }
    public Result CurrentResult { get; private set; } = Result.Pending;

    internal CurrencyTransactionApplication(CurrencyWalletApplication wallet, long amount, bool income)
    {
        _wallet = wallet;
        Amount = amount;
        IsIncome = income;
    }

    public bool TryCommit() => Complete(true);
    public bool TryCancel() => Complete(false);

    private bool Complete(bool commit)
    {
        if (CurrentResult != Result.Pending) return false;
        // 通知が同じ取引へ再入しても、終端処理は一回だけ実行する。
        CurrentResult = commit ? Result.Committed : Result.Cancelled;
        _wallet.Complete(Amount, IsIncome, commit);
        return true;
    }
}
