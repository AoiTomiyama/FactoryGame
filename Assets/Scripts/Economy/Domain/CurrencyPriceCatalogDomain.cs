using System;
using System.Collections.Generic;

/// <summary>設定入力を複製し、プレイ中の売価と設備価格を固定する。</summary>
public sealed class CurrencyPriceCatalogDomain
{
    private readonly Dictionary<ResourceTypeDomain, long> _salePrices = new();
    private readonly Dictionary<CellTypeDomain, long> _placementPrices = new();
    private readonly Dictionary<CellTypeDomain, IReadOnlyList<ResourceCostDomain>> _materialCosts = new();
    public long InitialBalance { get; }

    public CurrencyPriceCatalogDomain(long initialBalance,
        IEnumerable<KeyValuePair<ResourceTypeDomain, long>> salePrices,
        IEnumerable<KeyValuePair<CellTypeDomain, long>> placementPrices,
        IEnumerable<KeyValuePair<CellTypeDomain, ResourceCostDomain[]>> materialCosts = null)
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
        foreach (var entry in materialCosts ?? Array.Empty<KeyValuePair<CellTypeDomain, ResourceCostDomain[]>>())
        {
            var values = entry.Value ?? throw new ArgumentException("Material costs must be assigned.");
            var types = new HashSet<ResourceTypeDomain>();
            if (!_placementPrices.ContainsKey(entry.Key) || _materialCosts.ContainsKey(entry.Key))
                throw new ArgumentException("Materials require a priced equipment type.");
            foreach (var value in values)
                if (value.Resource == ResourceTypeDomain.None || !Enum.IsDefined(typeof(ResourceTypeDomain), value.Resource) ||
                    value.Amount <= 0 || !types.Add(value.Resource)) throw new ArgumentException("Invalid or duplicate material costs.");
            _materialCosts.Add(entry.Key, Array.AsReadOnly((ResourceCostDomain[])values.Clone()));
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

    public bool TryGetMaterialCosts(CellTypeDomain type, int quantity, out IReadOnlyList<ResourceCostDomain> costs)
    {
        costs = Array.Empty<ResourceCostDomain>();
        if (quantity <= 0 || type != CellTypeDomain.Empty && !_placementPrices.ContainsKey(type)) return false;
        if (!_materialCosts.TryGetValue(type, out var units)) return true;
        var result = new ResourceCostDomain[units.Count];
        for (var i = 0; i < units.Count; i++)
        {
            if (units[i].Amount > long.MaxValue / quantity) return false;
            result[i] = new ResourceCostDomain(units[i].Resource, units[i].Amount * quantity);
        }
        costs = Array.AsReadOnly(result); return true;
    }

    private static bool IsEquipment(CellTypeDomain type)
        => ResourceCellRulesDomain.IsExtractor(type) || type == CellTypeDomain.ConstructionDepot ||
           type == CellTypeDomain.Conveyor || type == CellTypeDomain.ExportConveyor ||
           type == CellTypeDomain.Crossing || type == CellTypeDomain.Storage || type == CellTypeDomain.Crafter || type == CellTypeDomain.AdvancedCrafter || type == CellTypeDomain.Shipping;
}
