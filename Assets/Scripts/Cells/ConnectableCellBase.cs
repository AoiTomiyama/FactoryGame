using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ConnectableCellBase : CellBase
{
    [SerializeField] private Directions allowedDirections =
        Directions.Forward | Directions.Back | Directions.Right | Directions.Left;
    
    private readonly HashSet<Vector3Int> _connectableDirections = new();
    private const int AdjacentCount = 4;
    protected CellBase[] AdjacentCells { get; private set; }
    
    /// <summary> 派生クラスで接続したセルを取得する際のデリゲート </summary>
    protected event Action<Vector3Int, CellBase> OnGetConnectedCell;

    public override void InitializeSystem()
    {
        AdjacentCells = new CellBase[AdjacentCount];
        SetConnectableDirections();
        ConnectAdjacentCells(this);
    }

    private void SetConnectableDirections()
    {
        var values = (Directions[])Enum.GetValues(typeof(Directions));
        foreach (var direction in values)
        {
            if (!HasFlag(allowedDirections, direction)) continue;
            _connectableDirections.Add(DirectionEnumToVector(direction));
        }
    }

    protected static bool HasFlag(Directions value, Directions flag) => (value & flag) == flag;

    protected Vector3Int DirectionEnumToVector(Directions direction) => direction switch
    {
        Directions.Forward => transform.forward.ToCardinalDirection(),
        Directions.Back => -transform.forward.ToCardinalDirection(),
        Directions.Right => transform.right.ToCardinalDirection(),
        Directions.Left => -transform.right.ToCardinalDirection(),
        _ => Vector3Int.zero,
    };

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

            // 取得できたセルがConnectableCellBaseのであれば、接続を行う
            if (foundCell is ConnectableCellBase connectableCell)
            {
                var dir = (foundCell.transform.position - transform.position).ToCardinalDirection();
                
                if (!_connectableDirections.Contains(dir) ||
                    !connectableCell._connectableDirections.Contains(-dir)) continue;

                // 接続先セルのAdjacentCellsに接続元のセルがなければ追加
                if (connectableCell.AdjacentCells.Contains(fromCell)) continue;

                AdjacentCells[i] = foundCell;
                
                // 新規接続セルを派生クラスにデリゲートとして伝達する。
                OnGetConnectedCell?.Invoke(dir, foundCell);

                // 向こうのセルのAdjacentCellsに接続元のセルを追加
                connectableCell.ConnectAdjacentCells(fromCell);
            }
            else
            {
                // 取得できたセルをAdjacentCellsに追加
                AdjacentCells[i] = foundCell;
            }
        }
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

            AdjacentCells[i] = null;
        }
    }

    public void OnDisconnect()
    {
        // 注: 以下の処理は本来ならOnDestroyで呼び出すのが望ましいが、
        // PlayModeからEditorModeに切り替えたタイミングでも呼ばれてしまう（=null参照が起こる）ため、
        // 独自の関数を定義し、外部から明示的に実行している。

        DisconnectAdjacentCells();
    }

    private void OnDrawGizmosSelected()
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