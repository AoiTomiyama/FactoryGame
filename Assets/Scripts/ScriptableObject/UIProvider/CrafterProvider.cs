using UnityEngine;

[CreateAssetMenu(fileName = "CrafterCellProvider", menuName = "Scriptable Objects/Provider/CrafterCellProvider")]
public class CrafterProvider : ProviderBase<CrafterCell>
{
    protected override UIElementDataBase Create(LabelEnum label) => label switch
    {
        LabelEnum.CellName => new TextElementData(GetName(label), "Crafter"),
        LabelEnum.Location => new TextElementData(GetName(label), $"({System.XIndex}, {System.ZIndex})"),
        LabelEnum.LeftStorage => new StorageElementData(GetName(label), System.IngredientCapacity),
        LabelEnum.RightStorage => new StorageElementData(GetName(label), System.IngredientCapacity),
        LabelEnum.OutputStorage => new StorageElementData(GetName(label), System.ExportableModule.ExporterCapacity),
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
                    gaugeData.Current = System.ElapsedProcessTime;
                    gaugeData.Max = System.ProcessTime;
                    gaugeData.GaugeText = $"{System.ProcessTime - System.ElapsedProcessTime:F1} sec";
                    break;
                case LabelEnum.LeftStorage:
                    gaugeData.Current = System.GetInput(Directions.Left).Amount;
                    gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                    break;
                case LabelEnum.RightStorage:
                    gaugeData.Current = System.GetInput(Directions.Right).Amount;
                    gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                    break;
                case LabelEnum.OutputStorage:
                    gaugeData.Current = System.ExportableModule.ExportResourceAmount;
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
                LabelEnum.LeftStorage => System.GetInput(Directions.Left).Type,
                LabelEnum.RightStorage => System.GetInput(Directions.Right).Type,
                LabelEnum.OutputStorage => System.ExportableModule.ExportResourceType,
                _ => 0
            };
        }
    }
}