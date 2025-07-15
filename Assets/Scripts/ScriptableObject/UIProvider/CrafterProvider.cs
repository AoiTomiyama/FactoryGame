using UnityEngine;

[CreateAssetMenu(fileName = "CrafterCellProvider", menuName = "Scriptable Objects/Provider/CrafterCellProvider")]
public class CrafterProvider : ProviderBase<CrafterCell>
{
    protected override UIElementDataBase Create(LabelEnum label) => label switch
    {
        LabelEnum.CellName => new TextElementData(GetName(label), "Crafter"),
        LabelEnum.Location => new TextElementData(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnum.LeftStorage => new StorageElementData(GetName(label), Cell.IngredientCapacity),
        LabelEnum.RightStorage => new StorageElementData(GetName(label), Cell.IngredientCapacity),
        LabelEnum.OutputStorage => new StorageElementData(GetName(label), Cell.ExportableModule.ExporterCapacity),
        LabelEnum.Progress => new GaugeElementData(GetName(label), 1),
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
                    gaugeData.Current = Cell.GetInput(Directions.Left).Amount;
                    gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                    break;
                case LabelEnum.RightStorage:
                    gaugeData.Current = Cell.GetInput(Directions.Right).Amount;
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
                LabelEnum.LeftStorage => Cell.GetInput(Directions.Left).Type,
                LabelEnum.RightStorage => Cell.GetInput(Directions.Right).Type,
                LabelEnum.OutputStorage => Cell.ExportableModule.ExportResourceType,
                _ => 0
            };
        }
    }
}