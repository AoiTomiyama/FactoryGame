using UnityEngine;

public class ItemPipeCell : ConnectableCellBase
{
    [SerializeField] private GameObject pipeConnectionPrefab;
    private GameObject[] _adjacentPipes;

    protected override void Start()
    {
        _adjacentPipes = new GameObject[AdjacentCount];
        OnConnectionChanged += UpdateConnection;
        base.Start();
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
            _adjacentPipes[i] = connectPipe;
        }
    }
}

enum PipeIOType
{
    None,
    Input,
    Output,
    Both,
}