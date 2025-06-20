using System;
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
        foreach (var network in
                 _pipelineNetworks.Where(network => network.Any(cell.HasCellConnected)))
        {
            // 既存のネットワークに追加
            network.Add(cell);
            
            if (cell is IContainable containableCell)
            {
                // IContainableセルの場合、ネットワークの最初のセルを設定
                containableCell.networkPath = GetNearestPathByNetwork(cell);
            }
            return;
        }

        // どのネットワークにも属していない場合、新しいネットワークを作成
        _pipelineNetworks.Add(new List<ConnectableCellBase> { cell });
    }

    public List<ConnectableCellBase> GetNearestPathByNetwork(ConnectableCellBase startCell)
    {
        if (startCell == null) return null;

        var network = _pipelineNetworks.FirstOrDefault(network => network.Contains(startCell));
        if (network == null)
        {
            Debug.LogWarning("指定されたセルはネットワークに属していません。");
            return null;
        }

        // BFSを用いて、ネットワーク内での最短経路を設定
        var queue = new Queue<ConnectableCellBase>();
        var visited = new HashSet<ConnectableCellBase>();
        var path = new Dictionary<ConnectableCellBase, ConnectableCellBase>();
        ConnectableCellBase targetCell = null;

        queue.Enqueue(startCell);
        visited.Add(startCell);
        while (queue.Count > 0)
        {
            var currentCell = queue.Dequeue();

            // 目的のセルに到達した場合、探索を終了
            if (currentCell is IContainable)
            {
                targetCell = currentCell;
                break;
            }

            // ネットワーク内のセルを探索
            foreach (var adjacentCell in currentCell.GetAdjacentCells())
            {
                if (adjacentCell == null || visited.Contains(adjacentCell)) continue;

                if (adjacentCell is not ConnectableCellBase connectableCell) continue;

                if (!network.Contains(connectableCell)) continue;

                queue.Enqueue(connectableCell);
                visited.Add(connectableCell);
                path[connectableCell] = currentCell;
            }
        }

        if (path.Count == 0)
        {
            Debug.LogWarning("指定されたネットワーク内に経路が見つかりませんでした。");
            return null;
        }

        // 最短経路を設定
        var resultPath = new List<ConnectableCellBase>();
        var current = targetCell;
        while (current != null && path.ContainsKey(current))
        {
            resultPath.Add(current);
            current = path[current];
        }

        // 経路を逆順にして、開始セルから目的セルへの順序にする
        resultPath.Reverse(); 
        return resultPath;
    }
}