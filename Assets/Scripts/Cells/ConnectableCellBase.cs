using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ConnectableCellBase : CellBase
{
    protected CellBase[] AdjacentCells { get; private set; }
    protected const int AdjacentCount = 4;
    protected event Action OnAdjacentConnected;

    protected virtual void Start()
    {
        AdjacentCells = new CellBase[AdjacentCount];

        ConnectAdjacentCells(this);
        PipelineNetworkManager.Instance.AddCellToNetwork(this);
    }
    
    public bool HasCellConnected(CellBase cell) => AdjacentCells.Contains(cell);
    public CellBase[] GetAdjacentCells() => AdjacentCells;

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
            
            if (AdjacentCells.Contains(foundCell)) continue;
            
            if (GetType().Name == foundCell.GetType().Name)
            {
                if (GetType().Name != nameof(ItemPipeCell))
                    // 同じタイプのセル同士は接続しない
                    continue;
            }
            
            // 取得できたセルをAdjacentCellsに追加
            AdjacentCells[i] = foundCell;

            // 取得できたセルがConnectableCellBaseのであれば、接続を行う
            if (foundCell is not ConnectableCellBase connectableCell) continue;

            // 接続先セルのAdjacentCellsに接続元のセルがなければ追加
            if (connectableCell.AdjacentCells.Contains(fromCell)) continue;
            
            // 向こうのセルのAdjacentCellsに接続元のセルを追加
            connectableCell.ConnectAdjacentCells(fromCell);
        }
        
        // 接続が完了したらイベントを呼び出す
        OnAdjacentConnected?.Invoke();
    }

    private void DisconnectAdjacentCells()
    {
        if (AdjacentCells == null || AdjacentCells.Length == 0) return;
        // 接続を解除する
        for (int i = 0; i < AdjacentCount; i++)
        {
            if (AdjacentCells[i] == null) continue;
            if (AdjacentCells[i] is not ConnectableCellBase connectableCell) continue;

            // 向こうのセルのAdjacentCellsから接続元のセルを削除
            connectableCell.AdjacentCells = connectableCell.AdjacentCells
                .Select(cell => cell != this ? cell : null).ToArray();
            connectableCell.OnAdjacentConnected?.Invoke();
            
            AdjacentCells[i] = null;
        }
    }

    public void OnDisconnect()
    {
        DisconnectAdjacentCells();
        PipelineNetworkManager.Instance.RemoveCellFromNetwork(this);
    }

    protected virtual void OnDrawGizmos()
    {
        if (AdjacentCells == null || AdjacentCells.Length == 0) return;

        // 接続表示（デバッグ用）
        Gizmos.color = Color.green;
        var startPadding = Vector3.up * 3f;
        var endPadding = Vector3.up * 3.2f;
        foreach (var cell in AdjacentCells.Where(cell => cell != null))
        {
            Gizmos.DrawLine(transform.position + startPadding, cell.transform.position + endPadding);
        }
    }
}