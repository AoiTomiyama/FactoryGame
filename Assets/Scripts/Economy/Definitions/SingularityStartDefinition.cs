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
    [SerializeField] private ShippingCellAdapter shipping;
    public SingularityCellAdapter SingularityPrefab => singularity;
    public ResourceCellAdapter WoodPrefab => wood;
    public EmptyCellAdapter EmptyPrefab => empty;
    public ShippingCellAdapter ShippingPrefab => shipping;
    public (int x, int z) ShippingCell(int size)
    {
        var input = singularity.Ports.Where(p => p.Facing == DirectionsDomain.Back && (p.Use & CellPortUseDomain.Input) != 0)
            .OrderBy(p => Math.Abs(p.Cell.x)).ThenBy(p => p.Id).First();
        return (size / 2 + input.Cell.x, size / 2 + input.Cell.y - 1);
    }
    public (int x, int z) StarterCell(int size, int distance)
    {
        // 換金経路を出荷先へ接続し、特異点への吸収と分ける。
        var target = ShippingCell(size);
        return (target.x, target.z - distance);
    }
    public CurrencyPriceCatalogDomain CreateCatalog() => economy != null ? economy.CreateCatalog() :
        throw new InvalidOperationException("Assign economy definition.");

    public void Validate(int size)
    {
        if (size <= 0 || singularity == null || shipping == null || wood == null || empty == null ||
            wood.ResourceTypeDomain != ResourceTypeDomain.Wood)
            throw new InvalidOperationException("Initial layout requires singularity, shipping, wood and empty prefabs.");
        singularity.ValidatePorts();
        shipping.ValidatePorts();
        if (shipping.Footprint.Area != 1 || shipping.Ports.Count != 4 || shipping.Ports.Any(p => p.Use != CellPortUseDomain.Input))
            throw new InvalidOperationException("Shipping requires one tile and four input-only ports.");
        var occupied = singularity.Footprint.Coordinates(size / 2, size / 2, 0).ToHashSet();
        var sale = ShippingCell(size);
        if (sale.x < 0 || sale.z < 0 || sale.x >= size || sale.z >= size || occupied.Contains((sale.x, sale.z)))
            throw new InvalidOperationException("Initial shipping must be outside the singularity footprint.");
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

}
