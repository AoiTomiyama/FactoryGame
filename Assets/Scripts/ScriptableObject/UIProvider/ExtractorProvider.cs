using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ExtractorProvider", menuName = "Scriptable Objects/Provider/ExtractorProvider")]
public class ExtractorProvider : ProviderBase<ExtractorCell>
{
    public override Dictionary<Label, UIElementDataBase> CreateUIElementData()
    {
        return new()
        {
            { Label.CellName, new TextElementData(GetName(Label.CellName), "Extractor") },
            { Label.Location, new TextElementData(GetName(Label.Location), $"({System.XIndex}, {System.ZIndex})") },
            { Label.Amount, new StorageElementData(GetName(Label.Amount), System.ExportableModule.ExporterCapacity) },
            { Label.Progress, new GaugeElementData(GetName(Label.Progress), 1) },
        };
    }

    public override void UpdateData(Label label, UIElementDataBase data)
    {
        if (data is GaugeElementData gaugeData)
        {
            gaugeData.Current = label switch
            {
                Label.Amount => System.ExportableModule.ExportResourceAmount,
                Label.Progress => System.ElapsedTime / System.ExtractionSecond,
                _ => 0
            };
            gaugeData.GaugeText = label switch
            {
                Label.Amount => $"{System.ExportableModule.ExportResourceAmount}/{System.ExportableModule.ExporterCapacity}",
                Label.Progress => $"{System.ExtractionSecond - System.ElapsedTime:F1} sec",
                _ => ""
            };
        }
        if (data is StorageElementData storageData)
        {
            storageData.ResourceType = System.ResourceType;
        }
    }
}