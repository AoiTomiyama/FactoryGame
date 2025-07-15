using UnityEngine;

[CreateAssetMenu(fileName = "ExtractorProvider", menuName = "Scriptable Objects/Provider/ExtractorProvider")]
public class ExtractorProvider : ProviderBase<ExtractorCell>
{
    protected override UIElementDataBase Create(LabelEnum label) => label switch
    {
        LabelEnum.CellName => new TextElementData(GetName(label), "Extractor"),
        LabelEnum.Location => new TextElementData(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnum.Amount => new StorageElementData(GetName(label), Cell.ExportableModule.ExporterCapacity),
        LabelEnum.Progress => new GaugeElementData(GetName(label), 1),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnum label, UIElementDataBase data)
    {
        if (data is GaugeElementData gaugeData)
        {
            gaugeData.Current = label switch
            {
                LabelEnum.Amount => Cell.ExportableModule.ExportResourceAmount,
                LabelEnum.Progress => Cell.ElapsedTime / Cell.ExtractionSecond,
                _ => 0
            };
            gaugeData.GaugeText = label switch
            {
                LabelEnum.Amount => $"{Cell.ExportableModule.ExportResourceAmount}/{Cell.ExportableModule.ExporterCapacity}",
                LabelEnum.Progress => $"{Cell.ExtractionSecond - Cell.ElapsedTime:F1} sec",
                _ => ""
            };
        }
        if (data is StorageElementData storageData)
        {
            storageData.ResourceType = Cell.ResourceType;
        }
    }
}