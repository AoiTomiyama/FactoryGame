/// <summary>資源と採取設備の対応を共有し、未対応資源を別の設備へ誤分類しない。</summary>
public static class ResourceCellRulesDomain
{
    public static CellTypeDomain ExtractorType(ResourceTypeDomain resource) => resource switch
    {
        ResourceTypeDomain.Wood => CellTypeDomain.ExtractorWood,
        ResourceTypeDomain.Stone => CellTypeDomain.ExtractorStone,
        ResourceTypeDomain.Iron => CellTypeDomain.ExtractorIron,
        ResourceTypeDomain.Copper => CellTypeDomain.ExtractorCopper,
        ResourceTypeDomain.Coal => CellTypeDomain.ExtractorCoal,
        _ => CellTypeDomain.None
    };
    public static bool IsExtractor(CellTypeDomain type) => type is CellTypeDomain.ExtractorWood or
        CellTypeDomain.ExtractorStone or CellTypeDomain.ExtractorIron or CellTypeDomain.ExtractorCopper or CellTypeDomain.ExtractorCoal;
    public static bool IsNatural(CellTypeDomain type) => type is CellTypeDomain.ResourceWood or
        CellTypeDomain.ResourceStone or CellTypeDomain.ResourceIron or CellTypeDomain.ResourceCopper or CellTypeDomain.ResourceCoal;
}
