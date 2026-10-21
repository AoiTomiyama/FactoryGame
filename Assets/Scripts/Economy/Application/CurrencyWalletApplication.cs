using System;
using System.Collections.Generic;

/// <summary>一プレイの所有者が生成し、利用先へ同じインスタンスを渡す共有残高。</summary>
public sealed class CurrencyWalletApplication
{
    public readonly struct Snapshot
    {
        public long Balance { get; }
        public long ReservedIncome { get; }
        public long ReservedExpense { get; }
        public long AvailableBalance => checked(Balance - ReservedExpense);

        internal Snapshot(long balance, long income, long expense)
        {
            Balance = balance;
            ReservedIncome = income;
            ReservedExpense = expense;
        }
    }

    private long _balance;
    private long _income;
    private long _expense;
    private readonly Queue<Snapshot> _notifications = new();
    private bool _notifying;

    public Snapshot Current => new(_balance, _income, _expense);
    public event Action<Snapshot> Changed;
    public Exception LastNotificationException { get; private set; }

    public CurrencyWalletApplication(long initialBalance)
    {
        if (initialBalance < 0) throw new ArgumentOutOfRangeException(nameof(initialBalance));
        _balance = initialBalance;
    }

    public bool TryReserveIncome(long amount, out CurrencyTransactionApplication transaction)
        => TryReserve(amount, true, out transaction);

    public bool TryReserveExpense(long amount, out CurrencyTransactionApplication transaction)
        => TryReserve(amount, false, out transaction);

    private bool TryReserve(long amount, bool income, out CurrencyTransactionApplication transaction)
    {
        transaction = null;
        if (amount <= 0) return false;
        // 支出取消後も入金できるよう、予約支出で入金余地を増やさない。
        if (income ? amount > long.MaxValue - _balance - _income : amount > _balance - _expense)
            return false;
        transaction = new CurrencyTransactionApplication(this, amount, income);
        if (income) _income = checked(_income + amount);
        else _expense = checked(_expense + amount);
        Notify();
        return true;
    }

    internal void Complete(long amount, bool income, bool commit)
    {
        // 成立済み予約の値をまとめて更新し、通知からは整合した状態を読めるようにする。
        if (income)
        {
            _income = checked(_income - amount);
            if (commit) _balance = checked(_balance + amount);
        }
        else
        {
            _expense = checked(_expense - amount);
            if (commit) _balance = checked(_balance - amount);
        }
        Notify();
    }

    private void Notify()
    {
        _notifications.Enqueue(Current);
        if (_notifying) return;
        _notifying = true;
        try
        {
            // 再入による変更も発生順で通知し、購読者の例外で取引結果を変えない。
            while (_notifications.Count > 0)
            {
                var snapshot = _notifications.Dequeue();
                var handlers = Changed;
                if (handlers == null) continue;
                foreach (Action<Snapshot> handler in handlers.GetInvocationList())
                {
                    try { handler(snapshot); }
                    catch (Exception exception) { LastNotificationException = exception; }
                }
            }
        }
        finally { _notifying = false; }
    }
}
