using UnityEngine;

[CreateAssetMenu(fileName = "StorageCellProvider", menuName = "Scriptable Objects/Provider/StorageCellProvider")]
public class StorageProvider : ProviderBase<StorageCell>
{
    protected override UIElementDataBase Create(LabelEnum labelEnum) => labelEnum switch
    {
        LabelEnum.CellName => new TextElementData(GetName(labelEnum), "Storage"),
        LabelEnum.Location => new TextElementData(GetName(labelEnum), $"({System.XIndex}, {System.ZIndex})"),
        LabelEnum.Amount => new StorageElementData(GetName(labelEnum), System.Capacity),
        LabelEnum.Allocated => new GaugeElementData(GetName(labelEnum), System.Capacity),
        LabelEnum.Reserved => new GaugeElementData(GetName(labelEnum), System.Capacity),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnum label, UIElementDataBase data)
    {
        if (data is StorageElementData s)
        {
            s.ResourceType = System.StoredResourceType;
        }

        if (data is GaugeElementData g)
        {
            g.Current = label switch
            {
                LabelEnum.Allocated => System.AllocatedAmount,
                LabelEnum.Reserved => System.ReservedAmount,
                LabelEnum.Amount => System.CurrentLoad,
                _ => g.Current
            };
            g.GaugeText = label switch
            {
                LabelEnum.Allocated => $"{System.AllocatedAmount}/{System.Capacity}",
                LabelEnum.Reserved => $"{System.ReservedAmount}/{System.Capacity}",
                LabelEnum.Amount => $"{System.CurrentLoad}/{System.Capacity}",
                _ => g.GaugeText
            };
        }

        if (data is TextElementData t)
        {
            if (label == LabelEnum.Location) t.Text = $"({System.XIndex}, {System.ZIndex})";
        }
    }
}