using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceProvider", menuName = "Scriptable Objects/Provider/ResourceProvider")]
public class ResourceProvider : ProviderBase<ResourceCell>
{
    [SerializeField] private ResourceSO resourceDatabase;

    public override Dictionary<Label, UIElementDataBase> CreateUIElementData()
    {
        return new()
        {
            { Label.CellName, new TextElementData(GetName(Label.CellName), "Resource") },
            { Label.Location, new TextElementData(GetName(Label.Location), $"({System.XIndex}, {System.ZIndex})") },
            {
                Label.ResourceName,
                new TextElementData(GetName(Label.ResourceName), resourceDatabase.GetInfo(System.ResourceType).Name)
            }
        };
    }

    public override void UpdateData(Label label, UIElementDataBase data)
    {
        if (label == Label.Location && data is TextElementData textData)
        {
            textData.Text = $"({System.XIndex}, {System.ZIndex})";
        }
    }
}