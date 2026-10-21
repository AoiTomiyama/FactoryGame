using System;
using System.Collections.Generic;

/// <summary>設定入力を複製し、プレイ中の売価と設備価格を固定する。</summary>
public sealed class CurrencyPriceCatalogDomain
{
    private readonly Dictionary<ResourceTypeDomain, long> _salePrices = new();
    private readonly Dictionary<CellTypeDomain, long> _placementPrices = new();
    public long InitialBalance { get; }

    public CurrencyPriceCatalogDomain(long initialBalance,
        IEnumerable<KeyValuePair<ResourceTypeDomain, long>> salePrices,
        IEnumerable<KeyValuePair<CellTypeDomain, long>> placementPrices)
    {
        if (initialBalance < 0) throw new ArgumentOutOfRangeException(nameof(initialBalance));
        InitialBalance = initialBalance;
        if (salePrices == null) throw new ArgumentNullException(nameof(salePrices));
        if (placementPrices == null) throw new ArgumentNullException(nameof(placementPrices));
        foreach (var price in salePrices)
        {
            if (!Enum.IsDefined(typeof(ResourceTypeDomain), price.Key) || price.Key == ResourceTypeDomain.None ||
                price.Value <= 0 || _salePrices.ContainsKey(price.Key))
                throw new ArgumentException("Sale prices require unique resource types and positive values.");
            _salePrices.Add(price.Key, price.Value);
        }
        foreach (var price in placementPrices)
        {
            if (!IsEquipment(price.Key) || price.Value <= 0 || _placementPrices.ContainsKey(price.Key))
                throw new ArgumentException("Placement prices require unique equipment types and positive values.");
            _placementPrices.Add(price.Key, price.Value);
        }
    }

    public bool TryGetSaleAmount(ResourceTypeDomain type, int quantity, out long amount)
    {
        amount = 0;
        return _salePrices.TryGetValue(type, out var price) &&
               CurrencyRulesDomain.TryCalculateAmount(price, quantity, out amount);
    }

    public bool TryGetPlacementAmount(CellTypeDomain type, int quantity, out long amount)
    {
        amount = 0;
        return _placementPrices.TryGetValue(type, out var price) &&
               CurrencyRulesDomain.TryCalculateAmount(price, quantity, out amount);
    }

    private static bool IsEquipment(CellTypeDomain type)
        => type == CellTypeDomain.ExtractorStone || type == CellTypeDomain.ExtractorWood ||
           type == CellTypeDomain.Conveyor || type == CellTypeDomain.ExportConveyor ||
           type == CellTypeDomain.Crossing || type == CellTypeDomain.Storage || type == CellTypeDomain.Crafter;
}
