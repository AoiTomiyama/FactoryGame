using System.Collections.Generic;

/// <summary>セル名と座標は見出しへ集約し、在庫・進捗には用途の分かるラベルを付ける。</summary>
public sealed class HudStatusProviderAdapter : IUIDataProviderApplication
{
    private readonly IUIDataProviderApplication _source;
    public HudStatusProviderAdapter(IUIDataProviderApplication source) => _source = source;
    public IReadOnlyDictionary<LabelEnumApplication, UIElementDataBaseApplication> ReadData()
    {
        var values = new Dictionary<LabelEnumApplication, UIElementDataBaseApplication>();
        foreach (var pair in _source.ReadData())
        {
            // 予約は内部の容量・取引管理で使い、HUDは現在の在庫と進捗を表示する。
            if (pair.Key == LabelEnumApplication.CellName || pair.Key == LabelEnumApplication.Location ||
                pair.Key == LabelEnumApplication.Reserved || pair.Key == LabelEnumApplication.Allocated) continue;
            var data = pair.Value;
            data.StatusName = pair.Key switch
            {
                LabelEnumApplication.LeftStorage => "左入力在庫",
                LabelEnumApplication.RightStorage => "右入力在庫",
                LabelEnumApplication.OutputStorage => "出力在庫",
                LabelEnumApplication.Progress => "残り時間",
                LabelEnumApplication.Allocated => "搬入予約",
                LabelEnumApplication.ResourceName => "資源",
                LabelEnumApplication.Amount when data is StorageElementDataApplication => "在庫",
                _ => data.StatusName
            };
            values.Add(pair.Key, data);
        }
        return values;
    }
}
