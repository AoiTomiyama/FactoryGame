using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ProviderBaseDefinition<T> : ScriptableObject, IUIDataProviderApplication where T : CellBaseAdapter, IDataProvidableApplication
{
    [SerializeField] protected LabelNameDefinition[] labelEntries;

    protected T Cell;
    private Dictionary<LabelEnumApplication, string> _labelMap;
    public void SwitchSystem(IDataProvidableApplication system) => Cell = system as T;

    /// <summary>
    /// ラベル名のマッピングを初期化します。
    /// </summary>
    private void InitMap() => _labelMap = labelEntries.ToDictionary(e => e.Label, e => e.Name);

    /// <summary>
    /// 指定されたラベルに対応する名前を取得します。
    /// 存在しない場合は "-" を返します。
    /// </summary>
    protected string GetName(LabelEnumApplication label)
    {
        if (_labelMap == null) InitMap();
        return _labelMap.GetValueOrDefault(label, "-");
    }

    /// <summary>
    /// 割り当てられたラベル名に応じてUIを作成します。
    /// </summary>
    /// <returns></returns>
    public Dictionary<LabelEnumApplication, UIElementDataBaseApplication> CreateUIElementData()
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
    protected abstract UIElementDataBaseApplication Create(LabelEnumApplication label);

    /// <summary>
    /// ラベルに応じてUIデータを更新します。
    /// </summary>
    /// <param name="label">指定のラベル</param>
    /// <param name="data">更新するUIデータ</param>
    public abstract void UpdateData(LabelEnumApplication label, UIElementDataBaseApplication data);
}

[Serializable]
public struct LabelNameDefinition
{
    [SerializeField] private LabelEnumApplication label;
    [SerializeField] private string name;

    public LabelEnumApplication Label => label;

    public string Name => name;
}