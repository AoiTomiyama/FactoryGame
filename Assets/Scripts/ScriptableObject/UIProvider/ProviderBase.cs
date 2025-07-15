using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ProviderBase<T> : ScriptableObject, IUIDataProvider where T : CellBase, IDataProvidable
{
    [SerializeField] protected LabelName[] labelEntries;

    protected T System;
    private Dictionary<Label, string> _labelMap;
    public void SwitchSystem(IDataProvidable system) => System = system as T;

    /// <summary>
    /// ラベル名のマッピングを初期化します。
    /// </summary>
    private void InitMap()
    {
        if (labelEntries == null || labelEntries.Length == 0)
        {
            Debug.LogWarning("Label entries are not set or empty in " + name, this);
            return;
        }

        _labelMap ??= labelEntries.ToDictionary(e => e.Label1, e => e.Name);
    }

    /// <summary>
    /// 指定されたラベルに対応する名前を取得します。
    /// 存在しない場合は "-" を返します。
    /// </summary>
    protected string GetName(Label label)
    {
        InitMap();
        return _labelMap.GetValueOrDefault(label, "-");
    }

    public abstract Dictionary<Label, UIElementDataBase> CreateUIElementData();
    public abstract void UpdateData(Label label, UIElementDataBase data);
}

public enum Label
{
    CellName,
    Location,
    Amount,
    Allocated,
    Reserved,
    ResourceName,
    Progress
}

[Serializable]
public struct LabelName
{
    [SerializeField] private Label label;
    [SerializeField] private string name;

    public Label Label1 => label;

    public string Name => name;
}