using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ResourceCell : CellBase, IUIRenderable
{
    [SerializeField] private ResourceType resourceType;

    public ResourceType ResourceType => resourceType;
    public bool IsUIActive { get; set; }
    private readonly Dictionary<Label, UIStatusRowBase> _uiStatusRow = new();
    private Dictionary<Label, UIElementDataBase> _uiElementData;

    private enum Label
    {
        CellName,
        Location,
        Type
    }

    private void Start()
    {
        _uiElementData = new()
        {
            { Label.CellName, new TextElementData("CellName:", "Resource") },
            { Label.Location, new TextElementData("Location", $"({XIndex}, {ZIndex})") },
            { Label.Type, new TextElementData("Type", Enum.GetName(typeof(ResourceType), resourceType)) }
        };
    }

    public void UpdateUI()
    {
        if (!IsUIActive) return;

        foreach (var (label, data) in _uiElementData)
        {
            if (label == Label.Location && data is TextElementData textData)
            {
                textData.Text = $"({XIndex}, {ZIndex})";
            }
            if (_uiStatusRow.TryGetValue(label, out var uiElement))
            {
                uiElement.RenderUIByData(data);
            }
            else
            {
                _uiStatusRow[label] = CellStatusView.Instance.CreateStatusRow(data);
            }
        }
    }

    public void ResetUI() => _uiStatusRow.Clear();
}

public enum ResourceType
{
    None,
    Stone,
    Wood,
    Iron,
    Gold
}