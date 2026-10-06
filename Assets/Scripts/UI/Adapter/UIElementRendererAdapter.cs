using System;
using System.Collections.Generic;

/// <summary>取得済みの表示値を行へ描画する。セルの数量や共有設定を書き換えない。</summary>
public class UIElementRendererAdapter
{
    private readonly Dictionary<LabelEnumApplication, UIStatusRowBaseAdapter> _renderedUI = new();
    private IUIDataProviderApplication _dataProvider;

    public void InitUI(IUIDataProviderApplication dataProvider,
        Func<UIElementDataBaseApplication, UIStatusRowBaseAdapter> createRow)
    {
        ResetUI();
        _dataProvider = dataProvider;
        foreach (var pair in dataProvider.ReadData()) _renderedUI.Add(pair.Key, createRow(pair.Value));
    }

    public void UpdateUI()
    {
        if (_dataProvider == null) return;
        // 全表示値を取得してから描画し、読み取りと Unity の表示更新を区別する。
        foreach (var pair in _dataProvider.ReadData()) _renderedUI[pair.Key].RenderUIByData(pair.Value);
    }

    public void ResetUI()
    {
        _renderedUI.Clear();
        _dataProvider = null;
    }
}
