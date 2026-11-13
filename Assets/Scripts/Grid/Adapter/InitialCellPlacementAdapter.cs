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
        var cells = field.GetComponentsInChildren<CellBaseAdapter>().ToDictionary(c =>
            (Mathf.RoundToInt(c.transform.position.x), Mathf.RoundToInt(c.transform.position.z)));
        if (occupied.Any(p => !cells.TryGetValue(p, out var c) || c is not EmptyCellAdapter && c is not ResourceCellAdapter) ||
            !cells.TryGetValue(sale, out var target) || target is not EmptyCellAdapter)
            throw new InvalidOperationException("Selected start site is no longer available.");

        SingularityCellAdapter core = null; ShippingCellAdapter shipping = null;
        try
        {
            // 作成が失敗しても地形を失わないよう、2設備を先に揃える。
            core = UnityEngine.Object.Instantiate(definition.SingularityPrefab, field.transform);
            shipping = UnityEngine.Object.Instantiate(definition.ShippingPrefab, field.transform);
            core.transform.SetPositionAndRotation(new Vector3(anchor.x, 0, anchor.y), Quaternion.identity);
            shipping.transform.SetPositionAndRotation(new Vector3(sale.x, 0, sale.z), Quaternion.identity);
            core.name = $"Tile_{anchor.x}_{anchor.y}"; shipping.name = $"Tile_{sale.x}_{sale.z}";
        }
        catch
        {
            if (core != null) Retire(core);
            if (shipping != null) Retire(shipping);
            throw;
        }
        // 初期配置で作るセルはこの2設備だけ。範囲外は触らない。
        foreach (var p in occupied.Append(sale)) Retire(cells[p]);
        return (core, shipping);
    }
    private static void Retire(CellBaseAdapter cell)
    {
        if (cell is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
        cell.gameObject.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(cell.gameObject);
        else UnityEngine.Object.DestroyImmediate(cell.gameObject);
    }
}
