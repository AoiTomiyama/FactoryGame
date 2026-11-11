using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CurrencyEconomy", menuName = "FactoryGame/Currency Economy")]
public sealed class CurrencyEconomyDefinition : ScriptableObject
{
    [Serializable]
    public struct SalePrice
    {
        public ResourceTypeDomain resource;
        public long price;
    }

    [Serializable]
    public struct PlacementPrice
    {
        public CellTypeDomain cell;
        public long price;
    }

    [SerializeField] private long initialBalance = 100;
    [SerializeField] private SalePrice[] salePrices =
    {
        new() { resource = ResourceTypeDomain.Wood, price = 1 },
        new() { resource = ResourceTypeDomain.Stone, price = 1 },
        new() { resource = ResourceTypeDomain.Plank, price = 3 },
        new() { resource = ResourceTypeDomain.StoneBrick, price = 4 },
        new() { resource = ResourceTypeDomain.BuildingParts, price = 16 },
        new() { resource = ResourceTypeDomain.Iron, price = 2 },
        new() { resource = ResourceTypeDomain.Copper, price = 2 },
        new() { resource = ResourceTypeDomain.Coal, price = 1 },
        new() { resource = ResourceTypeDomain.IronIngot, price = 6 },
        new() { resource = ResourceTypeDomain.CopperIngot, price = 6 },
        new() { resource = ResourceTypeDomain.CopperWire, price = 14 },
        new() { resource = ResourceTypeDomain.Steel, price = 16 },
        new() { resource = ResourceTypeDomain.MachineParts, price = 50 }
    };
    [SerializeField] private PlacementPrice[] placementPrices =
    {
        new() { cell = CellTypeDomain.ExtractorWood, price = 20 },
        new() { cell = CellTypeDomain.ExtractorStone, price = 20 },
        new() { cell = CellTypeDomain.Conveyor, price = 5 },
        new() { cell = CellTypeDomain.ExportConveyor, price = 5 },
        new() { cell = CellTypeDomain.Crossing, price = 10 },
        new() { cell = CellTypeDomain.Storage, price = 15 },
        new() { cell = CellTypeDomain.Crafter, price = 30 },
        new() { cell = CellTypeDomain.Shipping, price = 30 },
        new() { cell = CellTypeDomain.AdvancedCrafter, price = 90 },
        new() { cell = CellTypeDomain.ExtractorIron, price = 30 },
        new() { cell = CellTypeDomain.ExtractorCopper, price = 30 },
        new() { cell = CellTypeDomain.ExtractorCoal, price = 25 },
        new() { cell = CellTypeDomain.ConstructionDepot, price = 30 }
    };

    // 構成入口が一度取得して共有する。設定の変更は既存カタログへ遡及しない。
    public CurrencyPriceCatalogDomain CreateCatalog()
    {
        if (salePrices == null || placementPrices == null)
            throw new InvalidOperationException("Currency price arrays must be assigned.");
        var sales = new List<KeyValuePair<ResourceTypeDomain, long>>();
        foreach (var entry in salePrices) sales.Add(new(entry.resource, entry.price));
        var placements = new List<KeyValuePair<CellTypeDomain, long>>();
        foreach (var entry in placementPrices) placements.Add(new(entry.cell, entry.price));
        return new CurrencyPriceCatalogDomain(initialBalance, sales, placements);
    }
}
