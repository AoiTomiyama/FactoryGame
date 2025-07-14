using System.Collections.Generic;

public interface IUIDataProvider
{
    /// <summary>
    /// UI要素の辞書データを作成します。
    /// </summary>
    /// <returns>作成された辞書データ</returns>
    public Dictionary<Label, UIElementDataBase> CreateUIElementData();
    
    /// <summary>
    /// プロバイダーのUIデータを更新します。
    /// </summary>
    public void UpdateData(Label label, UIElementDataBase data);
    
    /// <summary>
    /// プロバイダーの参照先を切り替えます。
    /// </summary>
    public void SwitchSystem(IDataProvidable system);
}