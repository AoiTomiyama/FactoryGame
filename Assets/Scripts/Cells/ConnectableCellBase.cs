public abstract class ConnectableCellBase : CellBase
{
    protected CellBase[] AdjacentCells { get; set; }

    protected override void Awake()
    {
        base.Awake();
        ConnectAdjacentCells();
    }

    private void ConnectAdjacentCells()
    {
        const int adjacentCount = 4;
        AdjacentCells = new CellBase[adjacentCount];

        // 周囲1マス以内のセルを取得
        for (int i = 0; i < adjacentCount; i++)
        {
            if (!GridFieldDatabase.Instance.TryGetCellFromRange(XIndex, ZIndex, 1, out AdjacentCells[i],
                    AdjacentCells)) continue;
            
            // 取得できたセルがConnectableCellBaseのであれば、接続を行う
            if (AdjacentCells[i] is ConnectableCellBase connectableCell)
            {
                connectableCell.ConnectAdjacentCells();
            }
        }
    }
}