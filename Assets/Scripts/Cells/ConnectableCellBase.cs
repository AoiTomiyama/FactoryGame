using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ConnectableCellBase : CellBase
{
    protected CellBase[] AdjacentCells { get; private set; }
    private const int AdjacentCount = 4;

    protected virtual void Start()
    {
        AdjacentCells = new CellBase[AdjacentCount];

        ConnectAdjacentCells(this);
    }

    private void ConnectAdjacentCells(ConnectableCellBase fromCell)
    {
        // 自分自身を除外リストに追加
        var excludingList = new List<CellBase>(AdjacentCells) { this };

        // 周囲1マス以内のセルを取得
        for (int i = 0; i < AdjacentCount; i++)
        {
            if (AdjacentCells[i] != null) continue;
            
            if (!GridFieldDatabase.Instance.TryGetCellFromRange(XIndex, ZIndex, 1, out var foundCell,
                    excludingList)) continue;
            
            // 取得できたセルを除外リストに追加
            excludingList.Add(foundCell);
            
            // 取得できたセルがEmptyCellであればスキップ
            if (foundCell is EmptyCell) continue;
            
            // 取得できたセルをAdjacentCellsに追加
            AdjacentCells[i] = foundCell;

            // 取得できたセルがConnectableCellBaseのであれば、接続を行う
            if (foundCell is not ConnectableCellBase connectableCell) continue;

            // 接続先セルのAdjacentCellsに接続元のセルがなければ追加
            if (connectableCell.AdjacentCells.Contains(fromCell)) continue;
            
            // 向こうのセルのAdjacentCellsに接続元のセルを追加
            connectableCell.ConnectAdjacentCells(fromCell);
        }
    }

    private void OnDrawGizmos()
    {
        if (AdjacentCells == null || AdjacentCells.Length == 0) return;

        // 接続表示（デバッグ用）
        Gizmos.color = Color.green;
        var startPadding = Vector3.up * 3f;
        var endPadding = Vector3.up * 4f;
        foreach (var cell in AdjacentCells.Where(cell => cell != null))
        {
            Gizmos.DrawLine(transform.position + startPadding, cell.transform.position + endPadding);
        }
    }
}