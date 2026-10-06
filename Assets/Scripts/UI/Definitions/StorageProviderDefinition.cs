using UnityEngine;

[CreateAssetMenu(fileName = "StorageCellProvider", menuName = "Scriptable Objects/Provider/StorageCellProvider")]
public class StorageProviderDefinition : ProviderBaseDefinition<StorageCellAdapter>
{
    protected override UIElementDataBaseApplication Create(LabelEnumApplication label) => label switch
    {
        LabelEnumApplication.CellName => new TextElementDataApplication(GetName(label), "Storage"),
        LabelEnumApplication.Location => new TextElementDataApplication(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnumApplication.Amount => new StorageElementDataApplication(GetName(label), Cell.Capacity),
        LabelEnumApplication.Allocated => new GaugeElementDataApplication(GetName(label), Cell.Capacity),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnumApplication label, UIElementDataBaseApplication data)
    {
        if (data is StorageElementDataApplication s)
        {
            s.ResourceTypeDomain = Cell.StoredResourceType;
        }

        if (data is GaugeElementDataApplication g)
        {
            g.Current = label switch
            {
                LabelEnumApplication.Allocated => Cell.AllocatedAmount,
                LabelEnumApplication.Amount => Cell.CurrentLoad,
                _ => g.Current
            };
            g.GaugeText = label switch
            {
                LabelEnumApplication.Allocated => $"{Cell.AllocatedAmount}/{Cell.Capacity}",
                LabelEnumApplication.Amount => $"{Cell.CurrentLoad}/{Cell.Capacity}",
                _ => g.GaugeText
            };
        }

        if (data is TextElementDataApplication t)
        {
            if (label == LabelEnumApplication.Location) t.Text = $"({Cell.XIndex}, {Cell.ZIndex})";
        }
    }
}