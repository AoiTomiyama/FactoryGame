using System;
using System.Linq;

public class CrossedPipeCell : ConnectableCellBase
{
    public CellBase[] GetCrossedAdjacentCells(CellBase fromCell)
    {
        if (AdjacentCells == null || AdjacentCells.Length == 0)
        {
            return Array.Empty<CellBase>();
        }

        // 取得可能な隣接セルを取得元から交差する位置にあるものに限定する
        return AdjacentCells
            .Where(cell => cell != null && (cell.XIndex == fromCell.XIndex || cell.ZIndex == fromCell.ZIndex))
            .ToArray();
    }
}