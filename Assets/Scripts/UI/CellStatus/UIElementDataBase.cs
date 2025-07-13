public abstract class UIElementDataBase
{
    public string StatusName;

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

    public override UIStatusRowType UIStatusRowType => UIStatusRowType.Text;
}

public class GaugeElementData : UIElementDataBase
{
    public float Max;
    public float Current;
    public string GaugeText;

    public GaugeElementData(string statusName, int max) : base(statusName)
    {
        Max = max;
    }

    public override UIStatusRowType UIStatusRowType => UIStatusRowType.Gauge;
}

public class StorageElementData : GaugeElementData
{
    public ResourceType ResourceType;

    public StorageElementData(string statusName, int max) : base(statusName, max)
    {
    }

    public override UIStatusRowType UIStatusRowType => UIStatusRowType.Storage;
}