using UnityEngine;

public class ItemPipeCell : ConnectableCellBase
{
    [SerializeField] private PipeColorEnum pipeColor;
    [SerializeField] private PipeColorMapping pipeColorMapping;
    [SerializeField] private Renderer[] pipeRenderers;
    [SerializeField] private GameObject pipeConnectionPrefab;
    private GameObject[] _adjacentPipes;

    public void SetPipeColor(PipeColorEnum color)
    {
        if (color == pipeColor) return;
        pipeColor = color;
        foreach (var pipeRenderer in pipeRenderers)
        {
            pipeRenderer.material = pipeColorMapping.GetPipeMaterial(color);
        }
    }

    public override void InitializeSystem()
    {
        _adjacentPipes = new GameObject[AdjacentCount];
        OnConnectionChanged += UpdateConnection;

        // 隣接セルのフィルタリング
        // ItemPipeCell の場合、同じ色のパイプのみ接続を許可
        // ただし、デフォルト色のパイプはどの色とも接続可能
        // それ以外のセルは接続を許可
        OnFilterAdjacentCell += cell =>
            cell is not ItemPipeCell pipeCell ||
            pipeColor == PipeColorEnum.Default ||
            pipeCell.pipeColor == PipeColorEnum.Default ||
            pipeCell.pipeColor == pipeColor;

        base.InitializeSystem();
    }

    /// <summary>
    /// 接続変更時に、状態に応じて中間部分のパイプを生成・削除します
    /// </summary>
    private void UpdateConnection()
    {
        for (var i = 0; i < AdjacentCount; i++)
        {
            var cell = AdjacentCells[i];
            var pipe = _adjacentPipes[i];

            if (pipe != null)
            {
                // 隣接セルが削除された場合、パイプも削除
                if (cell == null)
                {
                    Destroy(pipe);
                    _adjacentPipes[i] = null;
                }

                continue;
            }

            // 隣接セルが ItemPipeCell, IContainable, IExportable のいずれかでなければスキップ
            if (cell is not (ItemPipeCell or IContainable or IExportable)) continue;

            var dir = cell.transform.position - transform.position;
            var pos = transform.position + dir / 3f + CellModel.transform.localPosition;
            var connectPipe = Instantiate(pipeConnectionPrefab, pos, Quaternion.identity, transform);
            connectPipe.transform.forward = dir.normalized;
            if (pipeColor != PipeColorEnum.Default)
            {
                // パイプの色を設定
                foreach (var pipeRenderer in connectPipe.GetComponentsInChildren<Renderer>())
                {
                    pipeRenderer.material = pipeColorMapping.GetPipeMaterial(pipeColor);
                }
            }

            _adjacentPipes[i] = connectPipe;
        }
    }
}