using System;
using System.Collections.Generic;
using System.Linq;

public abstract class ConnectableCellBase : CellBase
{
    protected CellBase[] AdjacentCells { get; private set; }
    private const int AdjacentCount = 4;

    protected virtual void Start()
    {
        AdjacentCells = new CellBase[AdjacentCount];

        ConnectAdjacentCells(this);
    }

    private void ConnectAdjacentCells(ConnectableCellBase fromCell)
    {
        // 自分自身を除外リストに追加
        var excludingList = new List<CellBase> { this };

        // 周囲1マス以内のセルを取得
        for (int i = 0; i < AdjacentCount; i++)
        {
            if (!GridFieldDatabase.Instance.TryGetCellFromRange(XIndex, ZIndex, 1, out AdjacentCells[i],
                    excludingList)) continue;

            excludingList.Add(AdjacentCells[i]);

            // 取得できたセルがConnectableCellBaseのであれば、接続を行う
            if (AdjacentCells[i] is not ConnectableCellBase connectableCell) continue;

            // 接続先セルのAdjacentCellsに接続元のセルがなければ追加
            if (connectableCell.AdjacentCells.Contains(fromCell)) continue;
            
            var index = Array.IndexOf(connectableCell.AdjacentCells, 
                connectableCell.AdjacentCells.FirstOrDefault(cell => cell.XIndex == fromCell.XIndex && cell.ZIndex == fromCell.ZIndex));
                
            connectableCell.AdjacentCells[index] = fromCell;
        }
    }
}