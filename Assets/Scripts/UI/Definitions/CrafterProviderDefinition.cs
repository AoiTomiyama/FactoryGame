using UnityEngine;

[CreateAssetMenu(fileName = "CrafterCellProvider", menuName = "Scriptable Objects/Provider/CrafterCellProvider")]
public class CrafterProviderDefinition : ProviderBaseDefinition<CrafterCellAdapter>
{
    protected override UIElementDataBaseApplication Create(LabelEnumApplication label) => label switch
    {
        LabelEnumApplication.CellName => new TextElementDataApplication(GetName(label), "Crafter"),
        LabelEnumApplication.Location => new TextElementDataApplication(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnumApplication.Progress => new GaugeElementDataApplication(GetName(label), 1),
        LabelEnumApplication.OutputStorage => new StorageElementDataApplication(GetName(label), Cell.ExporterCapacity),
        LabelEnumApplication.LeftStorage or LabelEnumApplication.RightStorage
            => new StorageElementDataApplication(GetName(label), Cell.IngredientCapacity),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnumApplication label, UIElementDataBaseApplication data)
    {
        if (data is GaugeElementDataApplication gaugeData)
        {
            switch (label)
            {
                case LabelEnumApplication.Progress:
                    gaugeData.Current = Cell.ElapsedProcessTime;
                    gaugeData.Max = Cell.ProcessTime;
                    gaugeData.GaugeText = $"{Cell.ProcessTime - Cell.ElapsedProcessTime:F1} sec";
                    break;
                case LabelEnumApplication.LeftStorage:
                case LabelEnumApplication.RightStorage:
                    gaugeData.Current = Cell.GetInput(LabelToDir(label)).Amount;
                    gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                    break;
                case LabelEnumApplication.OutputStorage:
                    gaugeData.Current = Cell.ExportStorageAmount;
                    gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                    break;
                default:
                    gaugeData.Current = 0;
                    gaugeData.GaugeText = "";
                    break;
            }
        }

        if (data is StorageElementDataApplication storageData)
        {
            storageData.ResourceTypeDomain = label switch
            {
                LabelEnumApplication.LeftStorage or LabelEnumApplication.RightStorage => Cell.GetInput(LabelToDir(label)).Type,
                LabelEnumApplication.OutputStorage => Cell.ExportResourceType,
                _ => 0
            };
        }
    }

    private static DirectionsDomain LabelToDir(LabelEnumApplication label)
    {
        return label switch
        {
            LabelEnumApplication.LeftStorage => DirectionsDomain.Left,
            LabelEnumApplication.RightStorage => DirectionsDomain.Right,
            _ => throw new System.NotImplementedException($"Unsupported label: {label}")
        };
    }
}
