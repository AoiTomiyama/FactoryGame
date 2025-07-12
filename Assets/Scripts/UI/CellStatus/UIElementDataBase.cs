public abstract class UIElementDataBase
{
    public readonly string StatusName;

    protected UIElementDataBase(string statusName)
    {
        StatusName = statusName;
    }

    public abstract UIStatusRowType UIStatusRowType { get; }
}

public class TextElementData : UIElementDataBase
{
    public string Text;

    public TextElementData(string statusName, string text) : base(statusName)
    {
        Text = text;
    }

    public override string ToString()
    {
        return $"{StatusName}: {Text}";
    }

    public override UIStatusRowType UIStatusRowType => UIStatusRowType.Text;
}

public class GaugeElementData : UIElementDataBase
{
    public float Max;
    public float Current;

    public GaugeElementData(string statusName, int max, int current) : base(statusName)
    {
        Max = max;
        Current = current;
    }

    public override UIStatusRowType UIStatusRowType => UIStatusRowType.Gauge;

    public override string ToString()
    {
        return $"{StatusName}: {Current}/{Max}";
    }
}

public class StorageElementData : GaugeElementData
{
    public ResourceType ResourceType;

    public StorageElementData(string statusName, int max, int current, ResourceType resourceType)
        : base(statusName, max, current)
    {
        ResourceType = resourceType;
    }

    public override string ToString()
    {
        return $"{StatusName}: {Current}/{Max} ({ResourceType})";
    }

    public override UIStatusRowType UIStatusRowType => UIStatusRowType.Storage;
}