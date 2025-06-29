using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public sealed class PipelineNetworkManager : SingletonMonoBehaviour<PipelineNetworkManager>
{
    [SerializeField] private float itemTransferSecondPerCell;
    private readonly List<List<ConnectableCellBase>> _pipelineNetworks = new();

    /// <summary>
    /// セルをネットワークに追加します。
    /// </summary>
    /// <param name="cell">登録するセル</param>
    public void AddCellToNetwork(ConnectableCellBase cell)
    {
        if (cell == null) return;

        // 既にネットワークに登録されているかを確認
        if (_pipelineNetworks.Any(network => network.Contains(cell))) return;

        // ネットワークを検索して、同じネットワークに属するセルがあるか確認
        var connectedNetworks = _pipelineNetworks
            .Where(network => network.Any(cell.HasCellConnected)).ToList();

        switch (connectedNetworks.Count)
        {
            case >= 2:
            {
                // 複数のネットワークに属している場合、統合する
                var mergedNetwork = new List<ConnectableCellBase>();
                foreach (var network in connectedNetworks)
                {
                    mergedNetwork.AddRange(network);
                    _pipelineNetworks.Remove(network);
                }

                mergedNetwork.Add(cell);
                _pipelineNetworks.Add(mergedNetwork);
                RegisterAllNetworkPaths(mergedNetwork);
                break;
            }
            case 1:
                // 既存のネットワークに追加
                connectedNetworks[0].Add(cell);
                RegisterAllNetworkPaths(connectedNetworks[0]);
                break;
            default:
                // どのネットワークにも属していない場合、新しいネットワークを作成
                _pipelineNetworks.Add(new() { cell });
                break;
        }
    }

    /// <summary>
    /// セルをネットワークから削除します。
    /// </summary>
    /// <param name="cell">削除するセル</param>
    public void RemoveCellFromNetwork(ConnectableCellBase cell)
    {
        if (cell == null) return;

        // ネットワークからセルを削除
        var network = _pipelineNetworks.FirstOrDefault(n => n.Contains(cell));
        if (network == null) return;

        network.Remove(cell);

        // ネットワークが空になった場合は削除
        // 空でない場合は、経路を再登録
        if (network.Count == 0)
        {
            _pipelineNetworks.Remove(network);
        }
        else
        {
            RegisterAllNetworkPaths(network);
        }
    }

    /// <summary>
    /// ネットワーク内の全てのセル間の経路を登録します。
    /// </summary>
    /// <param name="network">検索対象のネットワーク</param>
    private static void RegisterAllNetworkPaths(List<ConnectableCellBase> network)
    {
        // ネットワーク内のセルが2つ以上ある場合のみ経路を登録
        if (network.Count < 2) return;

        // ネットワーク内の各セルを起点として経路を登録
        foreach (var startCell in network.Where(cell => cell is IExportable))
        {
            foreach (var endCell in network.Where(cell => cell is IContainable))
            {
                RegisterAllPathByNetwork(startCell, endCell);
            }
        }
    }

    /// <summary>
    /// ネットワーク内の2つのセル間の最短経路を登録します。
    /// </summary>
    /// <param name="startCell">始点となるセル</param>
    /// <param name="endCell">終点となるセル</param>
    private static void RegisterAllPathByNetwork(ConnectableCellBase startCell, ConnectableCellBase endCell)
    {
        if (startCell == null) return;

        // BFSを用いて、ネットワーク内での全経路を設定
        var queue = new Queue<(ConnectableCellBase cell, Dictionary<ConnectableCellBase, ConnectableCellBase> path)>();
        var visited = new HashSet<ConnectableCellBase> { startCell };
        var foundPaths = new List<Dictionary<ConnectableCellBase, ConnectableCellBase>>();

        queue.Enqueue((startCell, new()));

        while (queue.Count > 0)
        {
            var (currentCell, currentPath) = queue.Dequeue();

            foreach (var connectableCell in currentCell.GetAdjacentCells()
                         .OfType<ConnectableCellBase>()
                         .Where(cell => !visited.Contains(cell)))
            {
                var nextPath = new Dictionary<ConnectableCellBase, ConnectableCellBase>(currentPath)
                {
                    [connectableCell] = currentCell
                };

                if (connectableCell == endCell)
                {
                    foundPaths.Add(nextPath);
                    continue;
                }

                visited.Add(connectableCell);

                if (connectableCell is IContainable or IExportable)
                {
                    // 入力または出力の機能をもつセルは探索しない
                    continue;
                }

                queue.Enqueue((connectableCell, nextPath));
            }
        }

        if (foundPaths.Count == 0)
        {
            Debug.LogWarning("指定されたネットワーク内に経路が見つかりませんでした。");
            return;
        }

        foreach (var path in foundPaths)
        {
            // 最短経路を設定
            var resultPath = new List<ConnectableCellBase>();

            var current = endCell;
            while (current != null && path.ContainsKey(current))
            {
                resultPath.Add(current);
                current = path[current];
            }

            var length = resultPath.Count;
            if (length == 0) continue;

            // 片方はIExportable、もう片方はIContainableにだけキャスト可能の場合に経路を追加
            switch (startCell)
            {
                case IExportable exportableStart when endCell is IContainable:
                    resultPath.Reverse();
                    exportableStart.AddPath(length, resultPath);
                    break;
                case IContainable when endCell is IExportable exportableEnd:
                    exportableEnd.AddPath(length, resultPath);
                    break;
            }
        }
    }

    /// <summary>
    /// 始点から終点までの輸送を試行する
    /// </summary>
    /// <param name="exporter">始点となるセル</param>
    /// <param name="exportAmount">輸送する量</param>
    /// <param name="exportBeginPos">アニメーション開始地点の座標</param>
    /// <param name="allocated">予約に成功した輸送量</param>
    /// <param name="logMode">falseが返されるときのログ表示（デバッグ用）</param>
    /// <returns>輸送に成功したかどうか</returns>
    public bool TryExport(IExportable exporter, int exportAmount,
        Vector3 exportBeginPos, out int allocated, bool logMode = false)
    {
        allocated = 0;

        // 始点がnullの場合はfalse
        if (exporter == null)
        {
#if UNITY_EDITOR
            if (logMode) Debug.LogWarning("出力元がnullです");
#endif
            return false;
        }

        Vector3Int inputDirection = default;
        List<ConnectableCellBase> path = null;
        IContainable container = null;
        var exportType = exporter.ExportResourceType;
        var allocatedAmount = 0;
        var hasFoundPath = false;

        exporter.RefreshPath();
        if (exporter.ExportPaths.Count == 0)
        {
#if UNITY_EDITOR
            if (logMode) Debug.LogWarning("パスが割り当てられていません");
#endif
            return false;
        }

        foreach (var (_, p) in exporter.ExportPaths)
        {
            if (p?.LastOrDefault() is not IContainable containable)
            {
#if UNITY_EDITOR
                if (logMode) Debug.LogWarning("終点がContainableでないためスキップされました");
#endif
                continue;
            }
            
            if (p.Count < 2) continue;

            // 予め終点にリソースの輸入を予約する。
            var dir = Vector3Int.RoundToInt((p.Last().transform.position　- p[^2].transform.position).normalized);
            allocatedAmount = containable.AllocateStorage(dir, exportAmount, exportType);
            if (allocatedAmount <= 0) continue;

            // 一致した場合、要素を変数に保持。
            container = containable;
            inputDirection = dir;
            path = p;
            hasFoundPath = true;
            break;
        }

        // 一致しなかった場合、falseを返す
        if (!hasFoundPath)
        {
#if UNITY_EDITOR
            if (logMode) Debug.LogWarning("有効なパスが見つかりません");
#endif
            return false;
        }

        // 予約分を保存
        allocated = allocatedAmount;

        var padding = Vector3.up * 1.1f;
        var startPos = exportBeginPos + padding;

        // ObjectPoolからモデルを呼び出す
        var itemObj = ResourceItemObjectPool.Instance.GetPrefab(exportType);
        itemObj.transform.position = startPos;

        // 始点から終点までのアニメーション
        var pathPos = path.Select(p => p.transform.position + padding).Prepend(startPos).ToArray();
        itemObj.transform
            .DOPath(pathPos, itemTransferSecondPerCell * path.Count)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                // ストレージに保存
                container.StoreResource(inputDirection, allocatedAmount);

                // ObjectPoolにモデルを返す
                ResourceItemObjectPool.Instance.Return(exportType, itemObj);
            });

        // 全ての処理が問題なく処理できたのでtrueを返す
        return true;
    }
}