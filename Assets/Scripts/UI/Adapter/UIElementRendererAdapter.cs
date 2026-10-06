using System.Collections.Generic;

public class UIElementRendererAdapter
{
    private Dictionary<LabelEnumApplication, UIElementDataBaseApplication> _elementDataBases;
    private readonly Dictionary<LabelEnumApplication, UIStatusRowBaseAdapter> _renderedUI = new();
    private IUIDataProviderApplication _dataProvider;

    public void InitUI(IUIDataProviderApplication dataProvider)
    {
        // プロバイダーの割り当て
        _dataProvider = dataProvider;
        
        // UI要素のデータをプロバイダーから取得
        _elementDataBases = _dataProvider.CreateUIElementData();
        
        // 各UI要素を初期化
        ResetUI();
        foreach (var (label, data) in _elementDataBases)
        {
            _renderedUI[label] = CellStatusViewAdapter.Instance.CreateStatusRow(data);
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