using System;
using System.Collections.Generic;

public class UIElementRenderer
{
    private Dictionary<Label, UIElementDataBase> _elementDataBases;
    private Dictionary<Label, UIStatusRowBase> _renderedUI = new();
    private IUIDataProvider _dataProvider;

    public void InitUI(IUIDataProvider dataProvider)
    {
        // プロバイダーの割り当て
        _dataProvider = dataProvider;
        
        // UI要素のデータをプロバイダーから取得
        _elementDataBases = _dataProvider.CreateUIElementData();
        
        // 各UI要素を初期化
        _renderedUI = new();
        foreach (var (label, data) in _elementDataBases)
        {
            _renderedUI[label] = CellStatusView.Instance.CreateStatusRow(data);
        }
        
        // UIを更新
        UpdateUI();
    }

    public void UpdateUI()
    {
        foreach (var (label, data) in _elementDataBases)
        {
            // プロバイダーから最新のデータを取得
            _dataProvider.UpdateData(label, data);
            
            // UI要素を更新
            _renderedUI[label].RenderUIByData(data);
        }
    }
    
    public void ResetUI() => _renderedUI.Clear();
}

public enum Label
{
    CellName,
    Location,
    Amount,
    Allocated,
    Reserved
}

[Serializable]
public struct LabelName
{
    public Label label;
    public string name;
}