using System;

[Serializable]
public abstract class UIElementDataBase
{
    public string statusName;
    public abstract UIStatusRowType ElementType { get; }
}

[Serializable]
public class TextElementData : UIElementDataBase
{
    public string text;
    public override UIStatusRowType ElementType => UIStatusRowType.Text;
}

[Serializable]
public class GaugeElementData : UIElementDataBase
{
    public float max;
    public float current;

    public override UIStatusRowType ElementType => UIStatusRowType.Gauge;
}

[Serializable]
public class StorageElementData : GaugeElementData
{
    public ResourceType resourceType;
    public override UIStatusRowType ElementType => UIStatusRowType.Storage;
}