using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PipelineNetworkManager : MonoBehaviour
{
    private static PipelineNetworkManager _instance;

    public static PipelineNetworkManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<PipelineNetworkManager>();

            if (_instance != null) return _instance;
#if UNITY_EDITOR
            Debug.LogError($"{nameof(PipelineNetworkManager)}がシーンに存在しません。");
#endif
            return null;
        }
    }

    private readonly List<List<ConnectableCellBase>> _pipelineNetworks = new();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {
            Debug.LogWarning("PipelineNetworkManagerのインスタンスが複数存在します。最初のインスタンスを保持します。");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// セルをネットワークに追加します。
    /// </summary>
    /// <param name="cell"></param>
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
                _pipelineNetworks.Add(new List<ConnectableCellBase> { cell });
                break;
        }
    }

    /// <summary>
    /// セルをネットワークから削除します。
    /// </summary>
    /// <param name="cell"></param>
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
    /// <param name="network"></param>
    private static void RegisterAllNetworkPaths(List<ConnectableCellBase> network)
    {
        // ネットワーク内のセルが2つ以上ある場合のみ経路を登録
        if (network.Count < 2) return;

        // ネットワーク内の各セルを起点として経路を登録
        foreach (var startCell in network.Where(cell => cell is IExportable))
        {
            foreach (var endCell in network.Where(cell => cell is IContainable))
            {
                RegisterNearestPathByNetwork(startCell, endCell);
            }
        }
    }

    /// <summary>
    /// ネットワーク内の2つのセル間の最短経路を登録します。
    /// </summary>
    /// <param name="startCell"></param>
    /// <param name="endCell"></param>
    private static void RegisterNearestPathByNetwork(ConnectableCellBase startCell, ConnectableCellBase endCell)
    {
        if (startCell == null) return;

        // BFSを用いて、ネットワーク内での最短経路を設定
        var queue = new Queue<ConnectableCellBase>();
        var path = new Dictionary<ConnectableCellBase, ConnectableCellBase>();
        var visited = new HashSet<ConnectableCellBase> { startCell };
        var isReached = false;

        queue.Enqueue(startCell);
        while (queue.Count > 0)
        {
            var currentCell = queue.Dequeue();

            // ネットワーク内のセルを探索
            // 検索条件
            // ・ConnectableCellBase型である
            // ・既に訪問済みでない
            foreach (var connectableCell in currentCell.GetAdjacentCells()
                         .OfType<ConnectableCellBase>()
                         .Where(cell => !visited.Contains(cell)))
            {
                visited.Add(connectableCell);

                if (currentCell is IContainable)
                {
                    // IContainableに到達した場合、指定された終点かどうかを判定
                    if (currentCell == endCell)
                    {
                        path[connectableCell] = currentCell;
                        isReached = true;
                        break;
                    }
                    // 終点でない場合は、探索を続ける（登録は行わない）
                    continue;
                }

                // 次の経路を登録
                queue.Enqueue(connectableCell);
                path[connectableCell] = currentCell;
            }
            
            // 終点に到達した場合は、探索を終了
            if (isReached) break;
        }

        if (path.Count == 0)
        {
            Debug.LogWarning("指定されたネットワーク内に経路が見つかりませんでした。");
            return;
        }

        // 最短経路を設定
        var resultPath = new List<ConnectableCellBase>();

        var current = endCell;
        while (current != null && path.ContainsKey(current))
        {
            resultPath.Add(current);
            current = path[current];
        }

        var length = resultPath.Count;

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