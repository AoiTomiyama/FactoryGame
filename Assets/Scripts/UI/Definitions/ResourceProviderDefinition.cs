using UnityEngine;

[CreateAssetMenu(fileName = "ResourceProviderDefinition", menuName = "Scriptable Objects/Provider/ResourceProviderDefinition")]
public class ResourceProviderDefinition : ProviderBaseDefinition<ResourceCellAdapter>
{
    [SerializeField] private ResourceDefinition resourceDatabase;

    protected override UIElementDataBaseApplication Create(LabelEnumApplication label) => label switch
    {
        LabelEnumApplication.CellName => new TextElementDataApplication(GetName(label), "Resource"),
        LabelEnumApplication.Location => new TextElementDataApplication(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnumApplication.ResourceName => new TextElementDataApplication(GetName(label), resourceDatabase.GetResourceByType(Cell.ResourceTypeDomain).ResourceName),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnumApplication label, UIElementDataBaseApplication data)
    {
        if (label == LabelEnumApplication.Location && data is TextElementDataApplication textData)
        {
            textData.Text = $"({Cell.XIndex}, {Cell.ZIndex})";
        }
    }
}