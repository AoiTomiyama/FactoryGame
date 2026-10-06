using System.Collections.Generic;

public interface IUIDataProviderApplication
{
    /// <summary>
    /// UI要素の辞書データを作成します。
    /// </summary>
    /// <returns>作成された辞書データ</returns>
    public Dictionary<LabelEnumApplication, UIElementDataBaseApplication> CreateUIElementData();
    
    /// <summary>
    /// プロバイダーのUIデータを更新します。
    /// </summary>
    public void UpdateData(LabelEnumApplication label, UIElementDataBaseApplication data);
    
    /// <summary>
    /// プロバイダーの参照先を切り替えます。
    /// </summary>
    public void SwitchSystem(IDataProvidableApplication system);
}