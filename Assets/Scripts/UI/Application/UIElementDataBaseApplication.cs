public abstract class UIElementDataBaseApplication
{
    public string StatusName;

    protected UIElementDataBaseApplication(string statusName)
    {
        StatusName = statusName;
    }

    public abstract UIStatusRowTypeApplication UIStatusRowTypeApplication { get; }
}

public class TextElementDataApplication : UIElementDataBaseApplication
{
    public string Text;

    public TextElementDataApplication(string statusName, string text) : base(statusName)
    {
        Text = text;
    }

    public override UIStatusRowTypeApplication UIStatusRowTypeApplication => UIStatusRowTypeApplication.Text;
}

public class GaugeElementDataApplication : UIElementDataBaseApplication
{
    public float Max;
    public float Current;
    public string GaugeText;

    public GaugeElementDataApplication(string statusName, int max) : base(statusName)
    {
        Max = max;
    }

    public override UIStatusRowTypeApplication UIStatusRowTypeApplication => UIStatusRowTypeApplication.Gauge;
}

public class StorageElementDataApplication : GaugeElementDataApplication
{
    public ResourceTypeDomain ResourceTypeDomain;

    public StorageElementDataApplication(string statusName, int max) : base(statusName, max)
    {
    }

    public override UIStatusRowTypeApplication UIStatusRowTypeApplication => UIStatusRowTypeApplication.Storage;
}