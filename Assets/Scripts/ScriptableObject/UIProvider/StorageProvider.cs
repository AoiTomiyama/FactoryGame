using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "StorageCellProvider", menuName = "Scriptable Objects/Provider/StorageCellProvider")]
public class StorageProvider : ScriptableObject, IUIDataProvider
{
    [SerializeField] private LabelName[] labelEntries;

    private Dictionary<Label, string> _labelMap;
    private StorageCell _storageSystem;

    public Dictionary<Label, UIElementDataBase> CreateUIElementData()
    {
        InitLabelMap();

        return new()
        {
            { Label.CellName, new TextElementData(GetName(Label.CellName), "Storage") },
            {
                Label.Location,
                new TextElementData(GetName(Label.Location), $"({_storageSystem.XIndex}, {_storageSystem.ZIndex})")
            },
            { Label.Amount, new StorageElementData(GetName(Label.Amount), _storageSystem.Capacity) },
            { Label.Allocated, new GaugeElementData(GetName(Label.Allocated), _storageSystem.Capacity) },
            { Label.Reserved, new GaugeElementData(GetName(Label.Reserved), _storageSystem.Capacity) }
        };
    }

    public void SwitchSystem(IDataProvidable system)
    {
        _storageSystem = system as StorageCell;
        InitLabelMap();
    }

    private void InitLabelMap()
    {
        if (_labelMap != null) return;
        _labelMap = labelEntries.ToDictionary(e => e.label, e => e.name);
    }

    private string GetName(Label label) => _labelMap.GetValueOrDefault(label, "-");

    public void UpdateData(Label label, UIElementDataBase data)
    {
        if (data is StorageElementData s)
        {
            s.ResourceType = _storageSystem.StoredResourceType;
        }

        if (data is GaugeElementData g)
        {
            g.Current = label switch
            {
                Label.Allocated => _storageSystem.AllocatedAmount,
                Label.Reserved => _storageSystem.ReservedAmount,
                Label.Amount => _storageSystem.CurrentLoad,
                _ => g.Current
            };
            g.GaugeText = label switch
            {
                Label.Allocated => $"{_storageSystem.AllocatedAmount}/{_storageSystem.Capacity}",
                Label.Reserved => $"{_storageSystem.ReservedAmount}/{_storageSystem.Capacity}",
                Label.Amount => $"{_storageSystem.CurrentLoad}/{_storageSystem.Capacity}",
                _ => g.GaugeText
            };
        }

        if (data is TextElementData t)
        {
            if (label == Label.Location) t.Text = $"({_storageSystem.XIndex}, {_storageSystem.ZIndex})";
        }
    }
}