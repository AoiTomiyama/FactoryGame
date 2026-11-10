using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SingularityStart", menuName = "FactoryGame/Singularity Start")]
public sealed class SingularityStartDefinition : ScriptableObject
{
    [SerializeField] private CurrencyEconomyDefinition economy;
    [SerializeField] private SingularityCellAdapter singularity;
    [SerializeField] private ResourceCellAdapter wood;
    [SerializeField] private EmptyCellAdapter empty;
    public SingularityCellAdapter SingularityPrefab => singularity;
    public ResourceCellAdapter WoodPrefab => wood;
    public EmptyCellAdapter EmptyPrefab => empty;
    public (int x, int z) StarterCell(int size, int distance)
    {
        // 後辺の中央入力を起点に、設備寸法に応じて初回経路を外へ伸ばす。
        var input = singularity.Ports.Where(p => p.Facing == DirectionsDomain.Back && (p.Use & CellPortUseDomain.Input) != 0)
            .OrderBy(p => Math.Abs(p.Cell.x)).ThenBy(p => p.Id).First();
        return (size / 2 + input.Cell.x, size / 2 + input.Cell.y - distance);
    }
    public CurrencyPriceCatalogDomain CreateCatalog() => economy != null ? economy.CreateCatalog() :
        throw new InvalidOperationException("Assign economy definition.");

    public void Validate(int size)
    {
        if (size <= 0 || singularity == null || wood == null || empty == null ||
            wood.ResourceTypeDomain != ResourceTypeDomain.Wood)
            throw new InvalidOperationException("Initial layout requires singularity, wood and empty prefabs.");
        singularity.ValidatePorts();
        var occupied = singularity.Footprint.Coordinates(size / 2, size / 2, 0).ToHashSet();
        if (occupied.Any(p => p.x < 0 || p.z < 0 || p.x >= size || p.z >= size) ||
            Enumerable.Range(1, 4).Select(i => StarterCell(size, i)).Any(p => p.x < 0 || p.z < 0 || p.x >= size || p.z >= size || occupied.Contains(p)))
            throw new InvalidOperationException("Initial field is too small for the footprint and starter route.");
        var prices = CreateCatalog();
        if (!prices.TryGetSaleAmount(ResourceTypeDomain.Wood, 1, out _) ||
            !prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood, 1, out var extractor) ||
            !prices.TryGetPlacementAmount(CellTypeDomain.ExportConveyor, 1, out var export) ||
            !prices.TryGetPlacementAmount(CellTypeDomain.Conveyor, 1, out var conveyor) ||
            extractor > prices.InitialBalance || export > prices.InitialBalance - extractor ||
            conveyor > prices.InitialBalance - extractor - export)
            throw new InvalidOperationException("Initial wood route must fit the initial balance.");
    }

    public GameObject GetInitialPrefab(int size, int x, int z)
    {
        if (x == size / 2 && z == size / 2) return singularity.gameObject;
        for (var distance = 1; distance <= 4; distance++)
            if (StarterCell(size, distance) == (x, z)) return distance == 4 ? wood.gameObject : empty.gameObject;
        return null;
    }
}
