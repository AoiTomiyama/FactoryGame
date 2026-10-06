using UnityEngine;

[CreateAssetMenu(fileName = "ExtractorProviderDefinition", menuName = "Scriptable Objects/Provider/ExtractorProviderDefinition")]
public class ExtractorProviderDefinition : ProviderBaseDefinition<ExtractorCellAdapter>
{
    protected override UIElementDataBaseApplication Create(LabelEnumApplication label) => label switch
    {
        LabelEnumApplication.CellName => new TextElementDataApplication(GetName(label), "Extractor"),
        LabelEnumApplication.Location => new TextElementDataApplication(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnumApplication.Amount => new StorageElementDataApplication(GetName(label), Cell.StorageCapacity),
        LabelEnumApplication.Progress => new GaugeElementDataApplication(GetName(label), 1),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnumApplication label, UIElementDataBaseApplication data)
    {
        if (data is GaugeElementDataApplication gaugeData)
        {
            gaugeData.Current = label switch
            {
                LabelEnumApplication.Amount => Cell.CurrentLoad,
                LabelEnumApplication.Progress => Cell.ElapsedTime / Cell.ExtractionSecond,
                _ => 0
            };
            gaugeData.GaugeText = label switch
            {
                LabelEnumApplication.Amount => $"{Cell.CurrentLoad}/{Cell.StorageCapacity}",
                LabelEnumApplication.Progress => $"{Cell.ExtractionSecond - Cell.ElapsedTime:F1} sec",
                _ => ""
            };
        }

        if (data is StorageElementDataApplication storageData)
        {
            storageData.ResourceTypeDomain = Cell.ResourceTypeDomain;
        }
    }
}