using System;

/// <summary>
/// セルの方向を表す列挙型
/// </summary>
[Flags]
public enum DirectionsDomain
{
    Forward = 1 << 0,
    Back = 1 << 1,
    Right = 1 << 2,
    Left = 1 << 3,
}

/// <summary>
/// リソースの種類を表す列挙型
/// </summary>
public enum ResourceTypeDomain
{
    None,
    Stone,
    Wood,
    Iron,
    Gold
}

/// <summary>
/// セルの種類を表す列挙型
/// </summary>
public enum CellTypeDomain
{
    None,
    Empty,
    ResourceWood,
    ResourceStone,
    ResourceIron,
    ExtractorStone,
    ExtractorWood,
    Storage,
    Crafter,
    Conveyor,
    ExportConveyor,
    Crossing,
}