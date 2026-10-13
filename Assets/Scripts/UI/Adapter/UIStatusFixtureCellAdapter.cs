#if UNITY_EDITOR
using System;

/// <summary>専用Adapterの接続検証だけに使うセル。実行時の設備やゲーム仕様には追加しない。</summary>
public sealed class UIStatusFixtureCellAdapter : CellBaseAdapter, IDataProvidableApplication
{
    public ProviderBaseDefinition Definition { get; set; }
    public int StoredAmount { get; private set; } = 7;
    public int DisplayValue { get; private set; } = 2;
    public event Action StatusChanged;
    public int SubscriptionCount => StatusChanged?.GetInvocationList().Length ?? 0;

    public IUIDataProviderApplication GetDataProvider()
        => CellStatusProviderAdapter.CreateCustom(Definition, label => UIStatusFixtureReaderAdapter.Read(this, label));

    public void ChangeDisplayValue(int value)
    {
        DisplayValue = value;
        StatusChanged?.Invoke();
    }
}

/// <summary>中央の具体型分岐を経由しない、セル専用の読み取り処理の検証例。</summary>
internal static class UIStatusFixtureReaderAdapter
{
    public static UIElementDataBaseApplication Read(UIStatusFixtureCellAdapter cell, LabelEnumApplication label)
        => label switch
        {
            LabelEnumApplication.CellName => new TextElementDataApplication("", "Custom status fixture"),
            LabelEnumApplication.Amount => new GaugeElementDataApplication("", 10)
                { Current = cell.DisplayValue, GaugeText = $"{cell.DisplayValue}/10" },
            _ => throw new ArgumentException($"Unsupported fixture label: {label}")
        };
}

#endif
