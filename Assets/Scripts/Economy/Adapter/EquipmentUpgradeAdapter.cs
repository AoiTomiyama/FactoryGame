using System;

/// <summary>共有購入処理へ、選択したセルの登録・寿命と固定座標を接続する。</summary>
public sealed class EquipmentUpgradeAdapter
{
    private readonly GridFieldDatabaseAdapter _grid;
    private readonly EquipmentUpgradeApplication _purchase;
    public Exception LastException => _purchase.LastException;

    public EquipmentUpgradeAdapter(GridFieldDatabaseAdapter grid, CurrencyWalletApplication wallet, CurrencyPriceCatalogDomain prices)
    {
        _grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
        _purchase = new EquipmentUpgradeApplication(wallet, prices);
    }

    public EquipmentUpgradeResultApplication CanPurchase(CellBaseAdapter cell, int expectedLevel, out long cost)
        => _purchase.CanPurchase(new Target(_grid, cell), expectedLevel, out cost);

    public EquipmentUpgradeResultApplication Purchase(CellBaseAdapter cell, int expectedLevel)
        => _purchase.Purchase(new Target(_grid, cell), expectedLevel);

    private sealed class Target : IEquipmentUpgradeTargetApplication
    {
        private readonly GridFieldDatabaseAdapter _grid;
        private readonly CellBaseAdapter _cell;
        private readonly int _x;
        private readonly int _z;
        public Target(GridFieldDatabaseAdapter grid, CellBaseAdapter cell)
        {
            _grid = grid;
            _cell = cell;
            if (cell != null) { _x = cell.XIndex; _z = cell.ZIndex; }
        }
        public bool IsAvailable => _grid != null && _cell != null && _cell.isActiveAndEnabled &&
            _cell.SupportsProgression && _cell.XIndex == _x && _cell.ZIndex == _z &&
            _grid.IsWithinBounds(_x, _z) && ReferenceEquals(_grid.GetCell(_x, _z), _cell);
        public int Level => _cell.EquipmentLevel;
        public CellTypeDomain Type => _cell switch
        {
            ExtractorCellAdapter extractor => extractor.ResourceTypeDomain switch
            {
                ResourceTypeDomain.Wood => CellTypeDomain.ExtractorWood,
                ResourceTypeDomain.Stone => CellTypeDomain.ExtractorStone,
                _ => CellTypeDomain.None
            },
            ExportConveyorCellAdapter => CellTypeDomain.ExportConveyor,
            ConveyorCellAdapter => CellTypeDomain.Conveyor,
            StorageCellAdapter => CellTypeDomain.Storage,
            CrafterCellAdapter => CellTypeDomain.Crafter,
            CrossingCellAdapter => CellTypeDomain.Crossing,
            _ => CellTypeDomain.None
        };
        public bool TrySetLevelWithoutNotification(int expectedLevel, int nextLevel)
            => IsAvailable && _cell.EquipmentLevel == expectedLevel && _cell.TrySetEquipmentLevelWithoutNotification(nextLevel);
        public void NotifyChanged() { if (_cell != null) _cell.NotifyEquipmentStatsChanged(); }
    }
}
