using UnityEngine;

public sealed class ItemPipeCell : ConnectableCellBase
{
    [SerializeField] private GameObject pipeConnectionPrefab;
    private GameObject[] _adjacentPipes;

    protected override void Start()
    {
        _adjacentPipes = new GameObject[AdjacentCount];
        OnAdjacentConnected += OnConnected;
        base.Start();
    }

    private void OnConnected()
    {
        for (var i = 0; i < AdjacentCount; i++)
        {
            var cell = AdjacentCells[i];
            
            var pipe = _adjacentPipes[i];
            if (pipe != null)
            {
                if (cell == null)
                {
                    Destroy(pipe);
                    _adjacentPipes[i] = null;
                }
                continue;
            }

            if (cell is not (ItemPipeCell or IContainable or IExportable)) continue;

            var dir = cell.transform.position - transform.position;
            var connectPipe = Instantiate(pipeConnectionPrefab,
                transform.position + dir / 3f + CellModel.transform.localPosition,
                Quaternion.identity, transform);
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