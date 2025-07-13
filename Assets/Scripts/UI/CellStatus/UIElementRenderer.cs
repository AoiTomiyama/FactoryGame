using System.Collections.Generic;

public class UIElementRenderer
{
    private readonly Dictionary<Label, UIElementDataBase> _elementDataBases;
    private readonly Dictionary<Label, UIStatusRowBase> _renderedUI = new();
    private readonly ICellDataProvider _dataProvider;

    public UIElementRenderer(ICellDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
        _elementDataBases = _dataProvider.CreateUIElementData();
    }

    public void InitUI()
    {
        foreach (var (label, data) in _elementDataBases)
        {
            _renderedUI[label] = CellStatusView.Instance.CreateStatusRow(data);
        }
    }

    public void UpdateUI()
    {
        foreach (var (label, data) in _elementDataBases)
        {
            _dataProvider.UpdateData(label, data);
            _renderedUI[label].RenderUIByData(data);
        }
    }
    
    public void ResetUI()
    {
        foreach (var uiElement in _renderedUI.Values)
        {
            uiElement.gameObject.SetActive(false);
        }
        _renderedUI.Clear();
    }
}

public enum Label
{
    CellName,
    Location,
    Amount,
    Allocated,
    Reserved
}