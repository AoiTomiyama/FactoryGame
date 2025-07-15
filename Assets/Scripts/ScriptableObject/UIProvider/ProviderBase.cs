using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ProviderBase<T> : ScriptableObject, IUIDataProvider where T : CellBase, IDataProvidable
{
    [SerializeField] protected LabelName[] labelEntries;

    protected T System;
    private Dictionary<LabelEnum, string> _labelMap;
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

        _labelMap ??= labelEntries.ToDictionary(e => e.Label, e => e.Name);
    }

    /// <summary>
    /// 指定されたラベルに対応する名前を取得します。
    /// 存在しない場合は "-" を返します。
    /// </summary>
    protected string GetName(LabelEnum label)
    {
        InitMap();
        return _labelMap.GetValueOrDefault(label, "-");
    }

    /// <summary>
    /// 割り当てられたラベル名に応じてUIを作成します。
    /// </summary>
    /// <returns></returns>
    public Dictionary<LabelEnum, UIElementDataBase> CreateUIElementData()
    {
        if (labelEntries == null || labelEntries.Length == 0)
        {
            Debug.LogWarning($"ラベル名の設定がありません： ${nameof(T)}Provider");
            return null;
        }
        var labels = labelEntries.Select(labelSet => labelSet.Label).ToArray();

        var dict = new Dictionary<LabelEnum, UIElementDataBase>(labels.Length);
        foreach (var label in labels)
            dict.Add(label, Create(label));

        return dict;
    }

    protected abstract UIElementDataBase Create(LabelEnum label);
    public abstract void UpdateData(LabelEnum label, UIElementDataBase data);
}

public enum LabelEnum
{
    CellName,
    Location,
    Amount,
    Allocated,
    Reserved,
    ResourceName,
    Progress,
    LeftStorage,
    RightStorage,
    OutputStorage,
}

[Serializable]
public struct LabelName
{
    [SerializeField] private LabelEnum label;
    [SerializeField] private string name;

    public LabelEnum Label => label;

    public string Name => name;
}