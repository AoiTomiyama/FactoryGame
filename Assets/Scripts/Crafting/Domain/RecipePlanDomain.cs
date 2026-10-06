using System;
using System.Collections.Generic;

/// <summary>成立判定で選んだ入力と消費量。加工中に定義や配列が変更されても計画を変えない。</summary>
public sealed class RecipePlanDomain
{
    public readonly struct Consumption
    {
        public int InputId { get; }
        public ResourceTypeDomain Type { get; }
        public int Amount { get; }

        internal Consumption(int inputId, ResourceTypeDomain type, int amount)
        {
            InputId = inputId;
            Type = type;
            Amount = amount;
        }
    }

    public IReadOnlyList<Consumption> Consumptions { get; }
    public ResourceTypeDomain ResultType { get; }
    public int ResultAmount { get; }

    internal RecipePlanDomain(Consumption[] consumptions, ResourceTypeDomain resultType, int resultAmount)
    {
        Consumptions = Array.AsReadOnly((Consumption[])consumptions.Clone());
        ResultType = resultType;
        ResultAmount = resultAmount;
    }
}
