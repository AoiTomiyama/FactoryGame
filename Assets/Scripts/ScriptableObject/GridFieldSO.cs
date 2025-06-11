using UnityEngine;

[CreateAssetMenu(fileName = "GridFieldSO", menuName = "Scriptable Objects/GridFieldSO")]
public class GridFieldSO : ScriptableObject
{
    public GameObject[,] GridCells { get; set; }

    public void SetNewCell(int x, int z, GameObject cell)
    {
        if (GridCells == null) return;
        
        if (x < 0 || z < 0 || x >= GridCells.GetLength(0) || z >= GridCells.GetLength(1))
        {
            Debug.LogError($"({x}, {z}) は範囲外です。");
            return;
        }

        GridCells[x, z] = cell;
        if (cell.TryGetComponent<CellBehaviour>(out var cellBehaviour))
        {
            cellBehaviour.xIndex = x;
            cellBehaviour.zIndex = z;
        }
        else
        {
            Debug.LogError($"{typeof(CellBehaviour)}が未割り当てです。");
        }
    }
}