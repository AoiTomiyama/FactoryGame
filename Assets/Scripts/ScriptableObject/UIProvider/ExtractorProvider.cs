using UnityEngine;

[CreateAssetMenu(fileName = "ExtractorProvider", menuName = "Scriptable Objects/Provider/ExtractorProvider")]
public class ExtractorProvider : ProviderBase<ExtractorCell>
{
    protected override UIElementDataBase Create(LabelEnum label) => label switch
    {
        LabelEnum.CellName => new TextElementData(GetName(label), "Extractor"),
        LabelEnum.Location => new TextElementData(GetName(label), $"({System.XIndex}, {System.ZIndex})"),
        LabelEnum.Amount => new StorageElementData(GetName(label), System.ExportableModule.ExporterCapacity),
        LabelEnum.Progress => new GaugeElementData(GetName(label), 1),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnum label, UIElementDataBase data)
    {
        if (data is GaugeElementData gaugeData)
        {
            gaugeData.Current = label switch
            {
                LabelEnum.Amount => System.ExportableModule.ExportResourceAmount,
                LabelEnum.Progress => System.ElapsedTime / System.ExtractionSecond,
                _ => 0
            };
            gaugeData.GaugeText = label switch
            {
                LabelEnum.Amount => $"{System.ExportableModule.ExportResourceAmount}/{System.ExportableModule.ExporterCapacity}",
                LabelEnum.Progress => $"{System.ExtractionSecond - System.ElapsedTime:F1} sec",
                _ => ""
            };
        }
        if (data is StorageElementData storageData)
        {
            storageData.ResourceType = System.ResourceType;
        }
    }
}