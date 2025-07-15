using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ProviderBase<T> : ScriptableObject, IUIDataProvider where T : CellBase, IDataProvidable
{
    [SerializeField] protected LabelName[] labelEntries;

    protected T Cell;
    private Dictionary<LabelEnum, string> _labelMap;
    public void SwitchSystem(IDataProvidable system) => Cell = system as T;

    /// <summary>
    /// ラベル名のマッピングを初期化します。
    /// </summary>
    private void InitMap() => _labelMap = labelEntries.ToDictionary(e => e.Label, e => e.Name);

    /// <summary>
    /// 指定されたラベルに対応する名前を取得します。
    /// 存在しない場合は "-" を返します。
    /// </summary>
    protected string GetName(LabelEnum label)
    {
        if (_labelMap == null) InitMap();
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

        return labelEntries.Select(labelName => labelName.Label).ToDictionary(label => label, Create);
    }

    /// <summary>
    /// 渡されたラベルに対応するUI要素データを作成します。
    /// </summary>
    /// <param name="label">指定のラベル</param>
    /// <returns>作成されたUIデータ</returns>
    protected abstract UIElementDataBase Create(LabelEnum label);

    /// <summary>
    /// ラベルに応じてUIデータを更新します。
    /// </summary>
    /// <param name="label">指定のラベル</param>
    /// <param name="data">更新するUIデータ</param>
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