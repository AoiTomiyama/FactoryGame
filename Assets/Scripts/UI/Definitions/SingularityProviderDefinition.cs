using UnityEngine;

/// <summary>特異点専用Readerが返す表示項目。</summary>
public sealed class SingularityProviderDefinition : ProviderBaseDefinition
{
    private void OnEnable() => labelEntries = new[]
    {
        new LabelNameDefinition(LabelEnumApplication.CellName, "施設"),
        new LabelNameDefinition(LabelEnumApplication.Location, "座標"),
        new LabelNameDefinition(LabelEnumApplication.Amount, "残高"),
        new LabelNameDefinition(LabelEnumApplication.Reserved, "換金待ち")
    };
}
