using System;
using System.Collections;
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

    public void AddCellToNetwork(ConnectableCellBase cell)
    {
        if (cell == null) return;

        // 既にネットワークに登録されているかを確認
        if (_pipelineNetworks.Any(network => network.Contains(cell))) return;

        // ネットワークを検索して、同じネットワークに属するセルがあるか確認
        var connectedNetworks = _pipelineNetworks
            .Where(network => network.Any(cell.HasCellConnected)).ToList();

        Debug.Log("connected: " + connectedNetworks.Count);
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
                return;
            }
            case 1:
                // 既存のネットワークに追加
                connectedNetworks[0].Add(cell);
                RegisterAllNetworkPaths(connectedNetworks[0]);
                return;
            default:
                // どのネットワークにも属していない場合、新しいネットワークを作成
                _pipelineNetworks.Add(new List<ConnectableCellBase> { cell });
                break;
        }
    }

    private void RegisterAllNetworkPaths(List<ConnectableCellBase> network)
    {
        // ネットワーク内のセルが2つ以上ある場合のみ経路を登録
        if (network.Count < 2) return;

        // ネットワーク内の各セルを起点として経路を登録
        foreach (var startCell in network.Where(startCell => startCell is IExportable or IContainable))
        {
            RegisterNearestPathByNetwork(startCell);
        }
    }

    private void RegisterNearestPathByNetwork(ConnectableCellBase startCell)
    {
        if (startCell == null) return;

        var network = _pipelineNetworks.FirstOrDefault(network => network.Contains(startCell));
        if (network == null)
        {
            Debug.LogWarning("指定されたセルはネットワークに属していません。");
            return;
        }

        // BFSを用いて、ネットワーク内での最短経路を設定
        var queue = new Queue<ConnectableCellBase>();
        var visited = new HashSet<ConnectableCellBase>();
        var path = new Dictionary<ConnectableCellBase, ConnectableCellBase>();
        ConnectableCellBase endCell = null;

        queue.Enqueue(startCell);
        visited.Add(startCell);
        while (queue.Count > 0)
        {
            var currentCell = queue.Dequeue();

            // ネットワーク内のセルを探索
            foreach (var adjacentCell in currentCell.GetAdjacentCells())
            {
                if (adjacentCell == null || visited.Contains(adjacentCell)) continue;

                if (adjacentCell is not ConnectableCellBase connectableCell) continue;

                queue.Enqueue(connectableCell);
                visited.Add(connectableCell);
                path[connectableCell] = currentCell;
            }
            
            // 片方はIExportable、もう片方はIContainableにだけキャスト可能な場合を終了判定とする。

            if (startCell is IExportable && currentCell is IContainable ||
                startCell is IContainable && currentCell is IExportable)
            {
                endCell = currentCell;
                break;
            }
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
        Debug.Log(string.Join("->", resultPath.Select(cell => $"({cell.XIndex}, {cell.ZIndex})")));

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