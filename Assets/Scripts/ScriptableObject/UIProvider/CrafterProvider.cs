using UnityEngine;

[CreateAssetMenu(fileName = "CrafterCellProvider", menuName = "Scriptable Objects/Provider/CrafterCellProvider")]
public class CrafterProvider : ProviderBase<CrafterCell>
{
    protected override UIElementDataBase Create(LabelEnum label) => label switch
    {
        LabelEnum.CellName => new TextElementData(GetName(label), "Crafter"),
        LabelEnum.Location => new TextElementData(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnum.Progress => new GaugeElementData(GetName(label), 1),
        LabelEnum.OutputStorage => new StorageElementData(GetName(label), Cell.ExportableModule.ExporterCapacity),
        LabelEnum.LeftStorage or LabelEnum.RightStorage 
            => new StorageElementData(GetName(label), Cell.IngredientCapacity),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnum label, UIElementDataBase data)
    {
        if (data is GaugeElementData gaugeData)
        {
            switch (label)
            {
                case LabelEnum.Progress:
                    gaugeData.Current = Cell.ElapsedProcessTime;
                    gaugeData.Max = Cell.ProcessTime;
                    gaugeData.GaugeText = $"{Cell.ProcessTime - Cell.ElapsedProcessTime:F1} sec";
                    break;
                case LabelEnum.LeftStorage:
                case LabelEnum.RightStorage:
                    gaugeData.Current = Cell.GetInput(LabelToDir(label)).Amount;
                    gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                    break;
                case LabelEnum.OutputStorage:
                    gaugeData.Current = Cell.ExportableModule.ExportResourceAmount;
                    gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                    break;
                default:
                    gaugeData.Current = 0;
                    gaugeData.GaugeText = "";
                    break;
            }
        }

        if (data is StorageElementData storageData)
        {
            storageData.ResourceType = label switch
            {
                LabelEnum.LeftStorage or LabelEnum.RightStorage => Cell.GetInput(LabelToDir(label)).Type,
                LabelEnum.OutputStorage => Cell.ExportableModule.ExportResourceType,
                _ => 0
            };
        }
    }
    
    private static Directions LabelToDir(LabelEnum label)
    {
        return label switch
        {
            LabelEnum.LeftStorage => Directions.Left,
            LabelEnum.RightStorage => Directions.Right,
            _ => throw new System.NotImplementedException($"Unsupported label: {label}")
        };
    }
}