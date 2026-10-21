using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SingularityStart", menuName = "FactoryGame/Singularity Start")]
public sealed class SingularityStartDefinition : ScriptableObject
{
    [SerializeField] private CurrencyEconomyDefinition economy;
    [SerializeField] private SingularityCellAdapter singularity;
    [SerializeField] private ResourceCellAdapter wood;
    [SerializeField] private EmptyCellAdapter empty;
    public CurrencyPriceCatalogDomain CreateCatalog() => economy != null ? economy.CreateCatalog() :
        throw new InvalidOperationException("Assign economy definition.");

    public void Validate(int size)
    {
        if (size < 8 || singularity == null || wood == null || empty == null ||
            wood.ResourceTypeDomain != ResourceTypeDomain.Wood)
            throw new InvalidOperationException("Initial layout requires size >= 8, singularity, wood and empty prefabs.");
        var prices = CreateCatalog();
        if (!prices.TryGetSaleAmount(ResourceTypeDomain.Wood, 1, out _) ||
            !prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood, 1, out var extractor) ||
            !prices.TryGetPlacementAmount(CellTypeDomain.ExportConveyor, 1, out var export) ||
            !prices.TryGetPlacementAmount(CellTypeDomain.Conveyor, 1, out var conveyor) ||
            extractor > prices.InitialBalance || export > prices.InitialBalance - extractor ||
            conveyor > prices.InitialBalance - extractor - export)
            throw new InvalidOperationException("Initial wood route must fit the initial balance.");
    }

    // 木材→抽出候補→搬出候補→通常搬送候補→中央の順で空間を保証する。
    public GameObject GetInitialPrefab(int size, int x, int z)
    {
        var center = size / 2;
        if (x == center && z == center) return singularity.gameObject;
        if (x == center && z == center - 4) return wood.gameObject;
        if (x == center && z > center - 4 && z < center) return empty.gameObject;
        return null;
    }
}
