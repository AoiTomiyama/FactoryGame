using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SingularityStart", menuName = "FactoryGame/Singularity Start")]
public sealed class SingularityStartDefinition : ScriptableObject
{
    [SerializeField] private CurrencyEconomyDefinition economy;
    [SerializeField] private SingularityCellAdapter singularity;
    [SerializeField] private ShippingCellAdapter shipping;
    [SerializeField] private DeliveryGoalsDefinition deliveryGoals;
    public DeliveryGoalCatalogDomain CreateDeliveryCatalog() => deliveryGoals != null ? deliveryGoals.CreateCatalog() :
        throw new InvalidOperationException("Assign delivery goals definition.");
    public SingularityCellAdapter SingularityPrefab => singularity;
    public ShippingCellAdapter ShippingPrefab => shipping;
    public (int x, int z) ShippingCell(int size) => ShippingCell(new Vector2Int(size/2,size/2));
    public (int x, int z) ShippingCell(Vector2Int anchor)
    {
        var input = singularity.Ports.Where(p => p.Facing == DirectionsDomain.Back && (p.Use & CellPortUseDomain.Input) != 0)
            .OrderBy(p => Math.Abs(p.Cell.x)).ThenBy(p => p.Id).First();
        return (anchor.x + input.Cell.x, anchor.y + input.Cell.y - 1);
    }
    public CurrencyPriceCatalogDomain CreateCatalog() => economy != null ? economy.CreateCatalog() :
        throw new InvalidOperationException("Assign economy definition.");

    public void Validate(int size) => ValidateAt(size,new Vector2Int(size/2,size/2));
    public void ValidateAt(int size, Vector2Int anchor)
    {
        _ = CreateDeliveryCatalog();
        if (size <= 0 || singularity == null || shipping == null)
            throw new InvalidOperationException("Initial layout requires singularity and shipping prefabs.");
        singularity.ValidatePorts();
        shipping.ValidatePorts();
        if (shipping.Footprint.Area != 1 || shipping.Ports.Count != 4 || shipping.Ports.Any(p => p.Use != CellPortUseDomain.Input))
            throw new InvalidOperationException("Shipping requires one tile and four input-only ports.");
        var occupied = singularity.Footprint.Coordinates(anchor.x, anchor.y, 0).ToHashSet();
        var sale = ShippingCell(anchor);
        if (sale.x < 0 || sale.z < 0 || sale.x >= size || sale.z >= size || occupied.Contains((sale.x, sale.z)))
            throw new InvalidOperationException("Initial shipping must be outside the singularity footprint.");
        if (occupied.Any(p => p.x < 0 || p.z < 0 || p.x >= size || p.z >= size))
            throw new InvalidOperationException("Initial field is too small for the footprint and starter route.");
        var prices = CreateCatalog();
        if (!prices.TryGetSaleAmount(ResourceTypeDomain.Wood,1,out var saleValue) || saleValue<=0 ||
            !prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood,1,out var extractor) ||
            !prices.TryGetPlacementAmount(CellTypeDomain.Conveyor,1,out var conveyor) ||
            extractor>prices.InitialBalance || conveyor>prices.InitialBalance-extractor)
            throw new InvalidOperationException("Initial wood route must fit the initial balance.");
        foreach(var type in new[]{CellTypeDomain.ExtractorWood,CellTypeDomain.Conveyor})
            if (!prices.TryGetMaterialCosts(type,1,out var materials) || materials.Count!=0)
                throw new InvalidOperationException("Initial wood route must not require construction materials.");
    }

}
