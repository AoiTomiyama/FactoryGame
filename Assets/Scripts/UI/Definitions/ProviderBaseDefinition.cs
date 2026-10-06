using System;
using UnityEngine;

/// <summary>編集可能な表示ラベルだけを保持する。選択セルや表示中の値を共有アセットへ保存しない。</summary>
public abstract class ProviderBaseDefinition : ScriptableObject
{
    [SerializeField] protected LabelNameDefinition[] labelEntries;
    public LabelNameDefinition[] GetLabels()
        => labelEntries == null ? Array.Empty<LabelNameDefinition>() : (LabelNameDefinition[])labelEntries.Clone();
}

[Serializable]
public struct LabelNameDefinition
{
    [SerializeField] private LabelEnumApplication label;
    [SerializeField] private string name;

    public LabelEnumApplication Label => label;

    public string Name => name;
}