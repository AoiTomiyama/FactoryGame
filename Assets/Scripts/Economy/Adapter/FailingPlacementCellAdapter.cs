#if UNITY_EDITOR
using System;

/// <summary>登録後の接続初期化失敗を再現するEditor検証用セル。</summary>
public sealed class FailingPlacementCellAdapter : ConnectableCellBaseAdapter
{
    public CellBaseAdapter Replacement;
    public override void InitializeSystem()
    {
        base.InitializeSystem();
        if (Replacement != null)
        {
            GridFieldDatabaseAdapter.Instance.SaveCell(XIndex, ZIndex, Replacement);
            return;
        }
        throw new InvalidOperationException("Injected cell initialization failure.");
    }
}
#endif
