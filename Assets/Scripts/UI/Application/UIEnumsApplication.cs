/// <summary>
/// UIで使用するラベルの列挙型
/// </summary>
public enum LabelEnumApplication
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
public enum UIStatusRowTypeApplication
{
    None,
    Text,
    Gauge,
    Storage,
}