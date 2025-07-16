/// <summary>
/// UIで使用するラベルの列挙型
/// </summary>
public enum LabelEnum
{
    CellName,
    Location,
    Amount,
    Allocated,
    Reserved,
    ResourceName,
    Progress,
    LeftStorage,
    RightStorage,
    OutputStorage,
}

/// <summary>
/// UI要素のデータ型を示す列挙型
/// </summary>
public enum UIStatusRowType
{
    None,
    Text,
    Gauge,
    Storage,
}