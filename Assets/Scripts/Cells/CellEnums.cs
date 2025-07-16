using System;

/// <summary>
/// セルの方向を表す列挙型
/// </summary>
[Flags]
public enum Directions
{
    Forward = 1 << 0,
    Back = 1 << 1,
    Right = 1 << 2,
    Left = 1 << 3,
}

/// <summary>
/// リソースの種類を表す列挙型
/// </summary>
public enum ResourceType
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
public enum CellType
{
    None,
    Empty,
    ResourceWood,
    ResourceStone,
    ResourceIron,
    ExtractorStone,
    ExtractorWood,
    Storage,
    ItemPipe,
    ItemRedPipe,
    ItemGreenPipe,
    ItemBluePipe,
    ExportPipe,
    Crafter,
}
    
public enum PipeColorEnum
{
    Default,
    Red,
    Green,
    Blue,
}