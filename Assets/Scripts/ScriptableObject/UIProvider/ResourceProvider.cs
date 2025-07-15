using UnityEngine;

[CreateAssetMenu(fileName = "ResourceProvider", menuName = "Scriptable Objects/Provider/ResourceProvider")]
public class ResourceProvider : ProviderBase<ResourceCell>
{
    [SerializeField] private ResourceSO resourceDatabase;

    protected override UIElementDataBase Create(LabelEnum label) => label switch
    {
        LabelEnum.CellName => new TextElementData(GetName(label), "Resource"),
        LabelEnum.Location => new TextElementData(GetName(label), $"({Cell.XIndex}, {Cell.ZIndex})"),
        LabelEnum.ResourceName => new TextElementData(GetName(label), resourceDatabase.GetInfo(Cell.ResourceType).Name),
        _ => throw new System.NotImplementedException(),
    };

    public override void UpdateData(LabelEnum label, UIElementDataBase data)
    {
        if (label == LabelEnum.Location && data is TextElementData textData)
        {
            textData.Text = $"({Cell.XIndex}, {Cell.ZIndex})";
        }
    }
}