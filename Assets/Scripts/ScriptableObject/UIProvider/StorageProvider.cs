using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StorageCellProvider", menuName = "Scriptable Objects/Provider/StorageCellProvider")]
public class StorageProvider : ProviderBase<StorageCell>
{
    public override Dictionary<Label, UIElementDataBase> CreateUIElementData()
    {
        return new()
        {
            { Label.CellName, new TextElementData(GetName(Label.CellName), "Storage") },
            {
                Label.Location,
                new TextElementData(GetName(Label.Location), $"({System.XIndex}, {System.ZIndex})")
            },
            { Label.Amount, new StorageElementData(GetName(Label.Amount), System.Capacity) },
            { Label.Allocated, new GaugeElementData(GetName(Label.Allocated), System.Capacity) },
            { Label.Reserved, new GaugeElementData(GetName(Label.Reserved), System.Capacity) }
        };
    }

    public override void UpdateData(Label label, UIElementDataBase data)
    {
        if (data is StorageElementData s)
        {
            s.ResourceType = System.StoredResourceType;
        }

        if (data is GaugeElementData g)
        {
            g.Current = label switch
            {
                Label.Allocated => System.AllocatedAmount,
                Label.Reserved => System.ReservedAmount,
                Label.Amount => System.CurrentLoad,
                _ => g.Current
            };
            g.GaugeText = label switch
            {
                Label.Allocated => $"{System.AllocatedAmount}/{System.Capacity}",
                Label.Reserved => $"{System.ReservedAmount}/{System.Capacity}",
                Label.Amount => $"{System.CurrentLoad}/{System.Capacity}",
                _ => g.GaugeText
            };
        }

        if (data is TextElementData t)
        {
            if (label == Label.Location) t.Text = $"({System.XIndex}, {System.ZIndex})";
        }
    }
}