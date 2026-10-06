using System;

/// <summary>Unity のセルや表示を参照せず、資源の保有量と予約量を計算する。</summary>
public static class ResourceStorageRulesDomain
{
    public readonly struct Stock
    {
        public ResourceTypeDomain Type { get; }
        public int Amount { get; }
        public int Reserved { get; }

        public Stock(ResourceTypeDomain type, int amount, int reserved)
        {
            Type = type;
            Amount = amount;
            Reserved = reserved;
        }
    }

    public static int GetAvailableCapacity(int capacity, Stock stock)
    {
        if (!IsValid(stock) || capacity < 0 || stock.Amount > capacity) return 0;
        // 合計の加算による int の桁あふれを避け、予約量を含む空きを求める。
        var space = capacity - stock.Amount;
        return stock.Reserved <= space ? space - stock.Reserved : 0;
    }

    public static int GetReservableAmount(int capacity, Stock stock, ResourceTypeDomain type)
    {
        if (type == ResourceTypeDomain.None || (stock.Type != ResourceTypeDomain.None && stock.Type != type)) return 0;
        return GetAvailableCapacity(capacity, stock);
    }

    public static bool TryReserve(int capacity, Stock stock, int amount, ResourceTypeDomain type, out Stock next)
    {
        next = stock;
        // 呼び出し側が要求量のまま確定するため、一部だけの予約は受け付けない。
        if (amount <= 0 || amount > GetReservableAmount(capacity, stock, type)) return false;
        next = new Stock(type, stock.Amount, stock.Reserved + amount);
        return true;
    }

    public static bool TryCommit(int capacity, Stock stock, int amount, out Stock next)
    {
        next = stock;
        if (!IsValid(stock) || amount <= 0 || amount > stock.Reserved ||
            stock.Amount > capacity || amount > capacity - stock.Amount) return false;
        next = new Stock(stock.Type, stock.Amount + amount, stock.Reserved - amount);
        return true;
    }

    public static bool TryCancel(Stock stock, int amount, ResourceTypeDomain type, out Stock next)
    {
        next = stock;
        if (!IsValid(stock) || type == ResourceTypeDomain.None || stock.Type != type ||
            amount <= 0 || amount > stock.Reserved) return false;
        var reserved = stock.Reserved - amount;
        var nextType = stock.Amount == 0 && reserved == 0 ? ResourceTypeDomain.None : stock.Type;
        next = new Stock(nextType, stock.Amount, reserved);
        return true;
    }

    public static int GetExportableAmount(int currentAmount, int requestedAmount)
        => currentAmount > 0 && requestedAmount > 0 ? Math.Min(currentAmount, requestedAmount) : 0;

    public static bool TryExport(Stock stock, int requestedAmount, out Stock next,
        out int amount, out ResourceTypeDomain type)
    {
        next = stock;
        amount = 0;
        type = stock.Type;
        if (!IsValid(stock) || stock.Type == ResourceTypeDomain.None) return false;
        amount = GetExportableAmount(stock.Amount, requestedAmount);
        if (amount == 0) return false;
        var remaining = stock.Amount - amount;
        // 現在量が尽きても、同種の搬入予約が残る間は資源種別を保持する。
        var nextType = remaining == 0 && stock.Reserved == 0 ? ResourceTypeDomain.None : stock.Type;
        next = new Stock(nextType, remaining, stock.Reserved);
        return true;
    }

    private static bool IsValid(Stock stock)
        => stock.Amount >= 0 && stock.Reserved >= 0 &&
           (stock.Type != ResourceTypeDomain.None || (stock.Amount == 0 && stock.Reserved == 0));
}
