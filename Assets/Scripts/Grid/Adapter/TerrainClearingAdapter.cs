using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>既存グリッドの資源をEmptyへ置き換える。資源と設備の二重占有を持たない。</summary>
public sealed class TerrainClearingAdapter
{
    public sealed class Preview
    {
        public TerrainClearingRulesDomain.Quote Quote { get; }
        internal GridOccupancyApplication.Entry<CellBaseAdapter>[] Previous { get; }
        internal Preview(TerrainClearingRulesDomain.Quote quote, GridOccupancyApplication.Entry<CellBaseAdapter>[] previous)
        { Quote = quote; Previous = previous; }
    }

    private readonly GridFieldDatabaseAdapter _grid;
    private readonly EmptyCellAdapter _empty;
    private readonly TerrainClearingApplication _transaction;
    public TerrainClearingRulesDomain Rules { get; }
    public bool IsUnlocked => _transaction.IsUnlocked;
    public Exception LastException => _transaction.LastException;

    public TerrainClearingAdapter(GridFieldDatabaseAdapter grid, EmptyCellAdapter empty,
        TerrainClearingRulesDomain rules, CurrencyWalletApplication wallet, Func<bool> isUnlocked)
    {
        _grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
        _empty = empty != null && empty.Footprint.Area == 1 ? empty : throw new ArgumentException("Clearing requires a one-tile Empty prefab.", nameof(empty));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _transaction = new TerrainClearingApplication(wallet, isUnlocked);
    }

    public Preview CreatePreview(IEnumerable<(int x, int z)> coordinates)
    {
        var candidates = coordinates.Distinct().Where(p => _grid.IsWithinBounds(p.x, p.z))
            .Select(p => _grid.GetCell(p.x, p.z)).OfType<ResourceCellAdapter>()
            .Where(c => c.isActiveAndEnabled && c.Footprint.Area == 1)
            .Select(c => new TerrainClearingRulesDomain.Target(c.XIndex, c.ZIndex, c.ResourceTypeDomain));
        if (!Rules.TryQuote(candidates, out var quote)) return null;
        return new Preview(quote, _grid.CaptureCells(quote.Targets.Select(t => (t.X, t.Z))));
    }

    public TerrainClearingApplication.Result Execute(Preview preview)
    {
        var created = new List<CellBaseAdapter>();
        bool Validate() => _grid != null && preview != null && preview.Previous.All(e =>
            _grid.IsWithinBounds(e.X, e.Z) && e.Owner != null && e.Owner.isActiveAndEnabled &&
            _grid.GetCell(e.X, e.Z) == e.Owner && e.Owner is ResourceCellAdapter raw && Rules.CanClear(raw.ResourceTypeDomain)) &&
            preview.Previous.Select((e, i) => ((ResourceCellAdapter)e.Owner).ResourceTypeDomain == preview.Quote.Targets[i].Resource).All(same => same);
        return _transaction.Execute(preview?.Quote, Validate, () =>
        {
            foreach (var entry in preview.Previous)
            {
                var cell = UnityEngine.Object.Instantiate(_empty, new Vector3(entry.X, 0, entry.Z), Quaternion.identity, _grid.transform);
                cell.name = $"Tile_{entry.X}_{entry.Z}";
                created.Add(cell);
            }
            if (!_grid.TryReplaceCells(created.ToArray(), preview.Previous)) throw new InvalidOperationException("Clearing cell registration failed.");
            foreach (var cell in created) { cell.gameObject.SetActive(true); cell.InitializeSystem(); }
        }, () =>
        {
            _grid.RestoreCells(preview.Previous);
            foreach (var cell in created)
                if (cell != null) { cell.gameObject.SetActive(false); UnityEngine.Object.Destroy(cell.gameObject); }
        }, () =>
        {
            foreach (var entry in preview.Previous)
            { entry.Owner.gameObject.SetActive(false); UnityEngine.Object.Destroy(entry.Owner.gameObject); }
            // 同フレームの保存が、既に消えた資源を使う採取ジョブを記録しないよう同期する。
            foreach (var extractor in preview.Previous.SelectMany(e => new[] { (e.X - 1, e.Z), (e.X + 1, e.Z), (e.X, e.Z - 1), (e.X, e.Z + 1) })
                .Distinct().Where(p => _grid.IsWithinBounds(p.Item1, p.Item2))
                .Select(p => _grid.GetCell(p.Item1, p.Item2)).OfType<ExtractorCellAdapter>().Distinct())
                extractor.RefreshResourceSource();
        });
    }
}
