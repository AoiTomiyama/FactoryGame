using System.Collections.Generic;

public class StorageCellProvider : ICellDataProvider
{
    private readonly StorageCell _system;

    public StorageCellProvider(StorageCell system)
    {
        _system = system;
    }

    public Dictionary<Label, UIElementDataBase> CreateUIElementData()
    {
        return new()
        {
            { Label.CellName, new TextElementData("-", "Storage") },
            { Label.Location, new TextElementData("-", $"({_system.XIndex}, {_system.ZIndex})") },
            { Label.Amount, new StorageElementData("-", _system.Capacity) },
            { Label.Allocated, new GaugeElementData("-", _system.Capacity) },
            { Label.Reserved, new GaugeElementData("-", _system.Capacity) }
        };
    }

    public void UpdateData(Label label, UIElementDataBase data)
    {
        switch (data)
        {
            case StorageElementData s:
                s.ResourceType = _system.StoredResourceType;
                break;
            case GaugeElementData g:
                g.Current = label switch
                {
                    Label.Allocated => _system.AllocatedAmount,
                    Label.Reserved => _system.ReservedAmount,
                    Label.Amount => _system.CurrentLoad,
                    _ => 0
                };
                g.GaugeText = label switch
                {
                    Label.Allocated => $"{_system.AllocatedAmount}/{_system.Capacity}",
                    Label.Reserved => $"{_system.ReservedAmount}/{_system.Capacity}",
                    Label.Amount => $"{_system.CurrentLoad}/{_system.Capacity}",
                    _ => ""
                };
                break;
            case TextElementData t:
                if (label == Label.Location) t.Text = $"({_system.XIndex}, {_system.ZIndex})";
                break;
        }
    }
}