using UnityEngine;

public class GridFieldDatabase : MonoBehaviour
{
    public CellBase[,] GridCells { get; set; }

    /// <summary>
    /// セルを配列に保存する
    /// </summary>
    public void SetNewCell(int x, int z, GameObject cellObject)
    {
        // 配列の初期化チェック
        if (GridCells == null)
        {
            Debug.LogError($"{nameof(GridCells)}が未割り当てです。");
            return;
        }
        
        // 引数のチェック
        if (cellObject == null)
        {
            Debug.LogError($"引数{nameof(cellObject)}がnullです。");
            return;
        }
        
        // 範囲外チェック
        if (x < 0 || z < 0 || x >= GridCells.GetLength(0) || z >= GridCells.GetLength(1))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return;
        }

        if (!cellObject.TryGetComponent<CellBase>(out var cellBase))
        {
            Debug.LogError($"{nameof(CellBase)}が未割り当てです。");
            return;
        }
        
        GridCells[x, z] = cellBase;
        cellBase.xIndex = x;
        cellBase.zIndex = z;
    }
}