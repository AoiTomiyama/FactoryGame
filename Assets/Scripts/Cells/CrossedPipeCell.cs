public class CrossedPipeCell : ConnectableCellBase
{
    public CellBase[] GetAdjacentCells(CellBase fromCell)
    {
        // 取得可能な隣接セルを取得元から交差する位置にあるものに限定する
        var allAdjacentCells = GetAdjacentCells();
        var filteredCells = new CellBase[allAdjacentCells.Length];
        var index = 0;
        foreach (var cell in allAdjacentCells)
        {
            // fromCellの位置と同じXまたはZ座標を持つセルのみを追加
            if (cell.XIndex == fromCell.XIndex || cell.ZIndex == fromCell.ZIndex)
            {
                filteredCells[index++] = cell;
            }
        }
        return filteredCells;
    }
}
