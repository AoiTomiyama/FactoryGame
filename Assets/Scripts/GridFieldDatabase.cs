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
}