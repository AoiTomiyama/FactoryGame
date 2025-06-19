using System;
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
            Debug.LogError("GridFieldDatabaseがシーンに存在しません。");
#endif
            return null;
        }
    }

    private CellBase[,] _gridCells;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
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
        if (x < 0 || z < 0 || x >= _gridCells.GetLength(0) || z >= _gridCells.GetLength(1))
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
        if (x < 0 || z < 0 || x >= _gridCells.GetLength(0) || z >= _gridCells.GetLength(1))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return null;
        }

        return _gridCells[x, z];
    }

    /// <summary>
    /// 指定した座標　(`x`, `z`) を中心に、マンハッタン距離 `range` 以内に存在する型 `T` のセルを検索する。
    /// 条件を満たすセルが見つかった場合は `cellBase` に代入し、`true` を返し、それ以外は `false` を返す。
    /// </summary>
    /// <param name="x">中心となるX座標</param>
    /// <param name="z">中心となるZ座標</param>
    /// <param name="range">検索するマンハッタン距離</param>
    /// <param name="cellBase">見つかったセル（型T）</param>
    /// <typeparam name="T">検索するセルの型（CellBaseの派生型）</typeparam>
    /// <returns>条件を満たすセルが見つかったかどうか</returns>
    public bool TryGetCellFromRange<T>(int x, int z, int range, out T cellBase) where T : CellBase
    {
        // 範囲外チェック
        if (x < 0 || z < 0 || x >= _gridCells.GetLength(0) || z >= _gridCells.GetLength(1))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            cellBase = null;
            return false;
        }

        // 一定のマンハッタン距離内に指定のセルがあるかを調べる。
        for (int i = -range; i <= range; i++)
        {
            for (int j = -range; j <= range; j++)
            {
                if (Mathf.Abs(i) + Mathf.Abs(j) > range) continue;

                var dx = x + i;
                var dz = z + j;

                if (dx < 0 || dz < 0 || dx >= _gridCells.GetLength(0) ||
                    dz >= _gridCells.GetLength(1))
                    continue;

                if (_gridCells[dx, dz] is not T selectedCell) continue;
                cellBase = selectedCell;
                return true;
            }
        }

        cellBase = null;
        return false;
    }
}