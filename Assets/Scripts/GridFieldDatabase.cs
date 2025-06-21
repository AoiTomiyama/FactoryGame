using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GridFieldDatabase : MonoBehaviour
{
    private static GridFieldDatabase _instance;

    public static GridFieldDatabase Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<GridFieldDatabase>();

            if (_instance != null) return _instance;
#if UNITY_EDITOR
            Debug.LogError($"{nameof(GridFieldDatabase)}がシーンに存在しません。");
#endif
            return null;
        }
    }

    private CellBase[,] _gridCells;
    private int _gridSize;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Debug.LogWarning("GridFieldDatabaseのインスタンスが複数存在します。最初のインスタンスを保持します。");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 配列の初期化とセルの保存を行う
    /// </summary>
    public void InitializeCells(int size)
    {
        // 引数のチェック
        if (size <= 0)
        {
            Debug.LogError("サイズは1以上の整数でなければなりません。");
            return;
        }

        _gridCells = new CellBase[size, size];
        _gridSize = size;

        for (int x = 0; x < size; x++)
        {
            var separator = transform.GetChild(x);
            for (int z = 0; z < size; z++)
            {
                var cell = separator.GetChild(z).gameObject;
                SaveCell(x, z, cell);
            }
        }
    }

    /// <summary>
    /// セルを配列に保存する
    /// </summary>
    public void SaveCell(int x, int z, GameObject cellObject)
    {
        // 配列の初期化チェック
        if (_gridCells == null)
        {
            Debug.LogError($"{nameof(_gridCells)}が未割り当てです。");
            return;
        }

        // 引数のチェック
        if (cellObject == null)
        {
            Debug.LogError($"引数{nameof(cellObject)}がnullです。");
            return;
        }

        // 範囲外チェック
        if (IsOutOfRange(x, z))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return;
        }

        if (!cellObject.TryGetComponent<CellBase>(out var cellBase))
        {
            Debug.LogError($"{nameof(CellBase)}が未割り当てです。");
            return;
        }

        _gridCells[x, z] = cellBase;
        cellBase.XIndex = x;
        cellBase.ZIndex = z;
    }

    /// <summary>
    /// 指定した座標のセルを取得する
    /// </summary>
    public CellBase GetCell(int x, int z)
    {
        // 範囲外チェック
        if (IsOutOfRange(x, z))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return null;
        }

        return _gridCells[x, z];
    }

    /// <summary>
    /// 指定した座標を中心に、マンハッタン距離 `range` 以内に存在する型 `T` のセルを検索する。
    /// 条件を満たすセルが見つかった場合は `cellBase` に代入し`true` を返し、それ以外は `false` を返す。
    /// </summary>
    /// <param name="x">中心となるX座標</param>
    /// <param name="z">中心となるZ座標</param>
    /// <param name="range">検索するマンハッタン距離</param>
    /// <param name="cellBase">見つかったセル（型T）</param>
    /// <param name="excludingList">あらかじめ検索から除外するリスト</param>
    /// <typeparam name="T">検索するセルの型（CellBaseの派生型）</typeparam>
    /// <returns>条件を満たすセルが見つかったかどうか</returns>
    public bool TryGetCellFromRange<T>(int x, int z, int range, out T cellBase, List<T> excludingList = null)
        where T : CellBase
    {
        cellBase = null;
        // 範囲外チェック
        if (IsOutOfRange(x, z))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return false;
        }

        var visited = new bool[_gridSize][];
        for (int i = 0; i < _gridSize; i++)
        {
            visited[i] = new bool[_gridSize];
        }

        // BFS（幅優先探索）を使用して、マンハッタン距離 range 以内のセルを探索
        var queue = new Queue<(int x, int z, int dist)>();
        queue.Enqueue((x, z, 0));
        visited[x][z] = true;
        var directions = new (int dx, int dz)[]
        {
            (1, 0), (-1, 0), (0, 1), (0, -1)
        };

        while (queue.Count > 0)
        {
            var (centerX, centerZ, dist) = queue.Dequeue();
            if (dist >= range) continue;

            foreach (var (dirX, dirZ) in directions)
            {
                var nextX = centerX + dirX;
                var nextZ = centerZ + dirZ;

                if (IsOutOfRange(nextX, nextZ))
                    continue;
                if (visited[nextX][nextZ])
                    continue;
                
                // セルが型Tで、かつ除外リストに含まれていない場合は、cellBaseに代入してtrueを返す
                if (_gridCells[nextX, nextZ] is T selectedCell && excludingList != null &&
                    !excludingList.Contains(selectedCell))
                {
                    cellBase = selectedCell;
                    return true;
                }

                visited[nextX][nextZ] = true;
                queue.Enqueue((nextX, nextZ, dist + 1));
            }
        }

        return false;
    }

    private bool IsOutOfRange(int x, int z) => x < 0 || z < 0 || x >= _gridSize || z >= _gridSize;
}