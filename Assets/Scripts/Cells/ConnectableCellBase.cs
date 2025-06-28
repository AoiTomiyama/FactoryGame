using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ConnectableCellBase : CellBase
{
    protected Vector3Int[] _connectableDirections;
    protected CellBase[] AdjacentCells { get; private set; }
    protected const int AdjacentCount = 4;
    protected event Action OnConnectionChanged;

    protected virtual void Start()
    {
        AdjacentCells = new CellBase[AdjacentCount];
        SetConnectableDirections();
        ConnectAdjacentCells(this);
        PipelineNetworkManager.Instance.AddCellToNetwork(this);
    }

    protected virtual void SetConnectableDirections()
    {
        _connectableDirections = new[]
        {
            Vector3Int.right,
            Vector3Int.forward,
            Vector3Int.left,
            Vector3Int.back,
        };
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

            if (GetType().Name != nameof(ItemPipeCell) &&
                GetType().Name == foundCell.GetType().Name)
            {
                // 同じタイプのセル同士は接続しない
                continue;
            }

            // 取得できたセルがConnectableCellBaseのであれば、接続を行う
            if (foundCell is ConnectableCellBase connectableCell)
            {
                var dir = Vector3Int.RoundToInt((transform.position - foundCell.transform.position).normalized);
                if (!_connectableDirections.Contains(-dir) ||
                    !connectableCell._connectableDirections.Contains(dir)) continue;

                // 接続先セルのAdjacentCellsに接続元のセルがなければ追加
                if (connectableCell.AdjacentCells.Contains(fromCell)) continue;

                AdjacentCells[i] = foundCell;

                // 向こうのセルのAdjacentCellsに接続元のセルを追加
                connectableCell.ConnectAdjacentCells(fromCell);
            }
            else
            {
                // 取得できたセルをAdjacentCellsに追加
                AdjacentCells[i] = foundCell;
            }
        }

        // 接続が完了したらイベントを呼び出す
        OnConnectionChanged?.Invoke();
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
            connectableCell.OnConnectionChanged?.Invoke();

            AdjacentCells[i] = null;
        }
    }

    public void OnDisconnect()
    {
        // 注: 以下の処理は本来ならOnDestroyで呼び出すのが望ましいが、
        // PlayModeからEditorModeに切り替えたタイミングでも呼ばれてしまう（=null参照が起こる）ため、
        // 独自の関数を定義し、外部から明示的に実行している。

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