using UnityEngine;

public sealed class ItemPipeCell : ConnectableCellBase
{
    [SerializeField] private GameObject pipeConnectionPrefab;
    private bool[] _isConnected;

    protected override void Start()
    {
        _isConnected = new bool[AdjacentCount];
        OnAdjacentConnected += OnConnected;
        base.Start();
    }

    private void OnConnected()
    {
        for (var i = 0; i < AdjacentCount; i++)
        {
            var cell = AdjacentCells[i];
            if (cell == null || _isConnected[i]) continue;
            if (cell is not (ItemPipeCell or IContainable or IExportable)) continue;

            var dir = cell.transform.position - transform.position;
            Instantiate(pipeConnectionPrefab, transform.position + dir / 3f + CellModel.transform.localPosition,
                Quaternion.identity, transform);
            _isConnected[i] = true;
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