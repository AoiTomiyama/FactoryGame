using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class ConnectableCellBase : CellBase
{
    [SerializeField] private Directions allowedDirections =
        Directions.Forward | Directions.Back | Directions.Right | Directions.Left;
    
    private readonly HashSet<Vector3Int> _connectableDirections = new();
    // スロットは常に +X, -X, +Z, -Z。向かい側は i ^ 1 で求める。
    private static readonly Vector3Int[] AdjacentDirections =
        { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back };
    private const int AdjacentCount = 4;
    protected CellBase[] AdjacentCells { get; private set; }
    protected Action OnDisconnected;
    
    /// <summary> 派生クラスで接続したセルを取得する際のデリゲート </summary>
    protected event Action<Vector3Int, CellBase> OnGetConnectedCell;
    /// <summary>隣接セルが削除された時に、そのセルを保持する派生クラスへ通知する。</summary>
    protected event Action<CellBase> OnLostConnectedCell;

    public override void InitializeSystem()
    {
        AdjacentCells = new CellBase[AdjacentCount];
        SetConnectableDirections();
        ConnectAdjacentCells();
    }

    private void SetConnectableDirections()
    {
        _connectableDirections.Clear();
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

    private void ConnectAdjacentCells()
    {
        var grid = GridFieldDatabase.Instance;
        for (var i = 0; i < AdjacentCount; i++)
        {
            var dir = AdjacentDirections[i];
            var x = XIndex + dir.x;
            var z = ZIndex + dir.z;
            if (!grid.IsWithinBounds(x, z)) continue;

            // 座標から直接読む。フィールド全体の探索用配列は作らない。
            var neighbor = grid.GetCell(x, z);
            if (neighbor == null || neighbor is EmptyCell) continue;

            if (neighbor is not ConnectableCellBase connectable)
            {
                AdjacentCells[i] = neighbor;
                continue;
            }

            if (connectable.AdjacentCells == null ||
                !_connectableDirections.Contains(dir) ||
                !connectable._connectableDirections.Contains(-dir)) continue;

            var opposite = i ^ 1;
            if (connectable.AdjacentCells[opposite] != null &&
                connectable.AdjacentCells[opposite] != this) continue;

            // 両側の参照を先に確定してから通知し、通知先からも同じ接続を見られるようにする。
            var addedHere = AdjacentCells[i] != neighbor;
            var addedThere = connectable.AdjacentCells[opposite] != this;
            AdjacentCells[i] = neighbor;
            connectable.AdjacentCells[opposite] = this;
            if (addedHere) OnGetConnectedCell?.Invoke(dir, neighbor);
            if (addedThere) connectable.OnGetConnectedCell?.Invoke(-dir, this);
        }
    }

    private void DisconnectAdjacentCells()
    {
        if (AdjacentCells == null || AdjacentCells.Length == 0) return;
        for (var i = 0; i < AdjacentCount; i++)
        {
            var neighbor = AdjacentCells[i];
            AdjacentCells[i] = null;
            if (neighbor is not ConnectableCellBase connectable || connectable.AdjacentCells == null)
                continue;

            var opposite = i ^ 1;
            if (connectable.AdjacentCells[opposite] != this) continue;
            connectable.AdjacentCells[opposite] = null;
            connectable.OnLostConnectedCell?.Invoke(this);
        }
    }

    public void OnDisconnect()
    {
        // 注: 以下の処理は本来ならOnDestroyで呼び出すのが望ましいが、
        // PlayModeからEditorModeに切り替えたタイミングでも呼ばれてしまう（=null参照が起こる）ため、
        // 独自の関数を定義し、外部から明示的に実行している。

        DisconnectAdjacentCells();
        OnDisconnected?.Invoke();
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
