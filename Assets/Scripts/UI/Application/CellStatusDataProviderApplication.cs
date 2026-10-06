using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>一つの表示セッションのラベルと読み取り処理。取得結果をゲーム状態とは別に所有する。</summary>
public sealed class CellStatusDataProviderApplication : IUIDataProviderApplication
{
    private readonly Dictionary<LabelEnumApplication, string> _labels;
    private readonly Func<LabelEnumApplication, UIElementDataBaseApplication> _read;

    public CellStatusDataProviderApplication(IEnumerable<KeyValuePair<LabelEnumApplication, string>> labels,
        Func<LabelEnumApplication, UIElementDataBaseApplication> read)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
        _labels = new Dictionary<LabelEnumApplication, string>();
        foreach (var pair in labels) _labels.Add(pair.Key, pair.Value);
    }

    public IReadOnlyDictionary<LabelEnumApplication, UIElementDataBaseApplication> ReadData()
    {
        var values = new Dictionary<LabelEnumApplication, UIElementDataBaseApplication>();
        foreach (var pair in _labels)
        {
            var value = _read(pair.Key);
            value.StatusName = pair.Value;
            values.Add(pair.Key, value);
        }
        return new ReadOnlyDictionary<LabelEnumApplication, UIElementDataBaseApplication>(values);
    }
}
