using System;
using System.Linq;
using UnityEngine;

/// <summary>自然地形の対象範囲を、開始用の2設備へ置き換える。</summary>
public sealed class InitialCellPlacementAdapter
{
    public (SingularityCellAdapter core, ShippingCellAdapter shipping) Place(
        GridFieldDatabaseAdapter field, SingularityStartDefinition definition, Vector2Int anchor)
    {
        definition.ValidateAt(field.GeneratedSize, anchor);
        var occupied = definition.SingularityPrefab.Footprint.Coordinates(anchor.x, anchor.y, 0).ToHashSet();
        var sale = definition.ShippingCell(anchor);
        var registered = field.IsWithinBounds(0, 0);
        var coordinates = occupied.Append(sale).ToArray();
        var previous = registered ? field.CaptureCells(coordinates) : null;
        var cells = registered ? previous.Select(entry => entry.Owner).ToArray() : UnregisteredCells(field, coordinates);
        if (cells.Take(occupied.Count).Any(c => c is not (EmptyCellAdapter or ResourceCellAdapter)) ||
            cells[^1] is not EmptyCellAdapter)
            throw new InvalidOperationException("Selected start site is no longer available.");

        // 作成を先に済ませる。例外時は呼出側へ伝え、作成途中の状態を調査用に残す。
        var core = UnityEngine.Object.Instantiate(definition.SingularityPrefab, field.transform);
        var shipping = UnityEngine.Object.Instantiate(definition.ShippingPrefab, field.transform);
        core.transform.SetPositionAndRotation(new Vector3(anchor.x, 0, anchor.y), Quaternion.identity);
        shipping.transform.SetPositionAndRotation(new Vector3(sale.x, 0, sale.z), Quaternion.identity);
        core.name = $"Tile_{anchor.x}_{anchor.y}"; shipping.name = $"Tile_{sale.x}_{sale.z}";
        // 初期配置で作るセルはこの2設備だけ。範囲外は触らない。
        if (registered && !field.TryReplaceCells(new CellBaseAdapter[] { core, shipping }, previous))
            throw new InvalidOperationException("Selected start site changed during placement.");
        foreach (var cell in cells) Retire(cell);
        if (!registered) field.InitializeCells(field.GeneratedSize);
        return (core, shipping);
    }
    private static CellBaseAdapter[] UnregisteredCells(GridFieldDatabaseAdapter field, (int x, int z)[] coordinates)
    {
        var cells = field.GetComponentsInChildren<CellBaseAdapter>().ToDictionary(c =>
            (Mathf.RoundToInt(c.transform.position.x), Mathf.RoundToInt(c.transform.position.z)));
        return coordinates.Select(point => cells.TryGetValue(point, out var cell) ? cell : null).ToArray();
    }
    private static void Retire(CellBaseAdapter cell)
    {
        if (cell is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
        cell.gameObject.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(cell.gameObject);
        else UnityEngine.Object.DestroyImmediate(cell.gameObject);
    }
}
