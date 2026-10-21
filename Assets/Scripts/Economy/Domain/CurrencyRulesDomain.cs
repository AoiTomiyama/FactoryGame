/// <summary>価格と数量から、正の取引額を求める。</summary>
public static class CurrencyRulesDomain
{
    public static bool TryCalculateAmount(long unitPrice, int quantity, out long amount)
    {
        amount = 0;
        if (unitPrice <= 0 || quantity <= 0 || unitPrice > long.MaxValue / quantity) return false;
        amount = checked(unitPrice * quantity);
        return true;
    }
}
