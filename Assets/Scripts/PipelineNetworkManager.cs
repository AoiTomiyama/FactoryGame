using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public sealed class PipelineNetworkManager : SingletonMonoBehaviour<PipelineNetworkManager>
{
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
                _pipelineNetworks.Add(new List<ConnectableCellBase> { cell });
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
                RegisterNearestPathByNetwork(startCell, endCell);
            }
        }
    }

    /// <summary>
    /// ネットワーク内の2つのセル間の最短経路を登録します。
    /// </summary>
    /// <param name="startCell">始点となるセル</param>
    /// <param name="endCell">終点となるセル</param>
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
                
                // 終点かどうかを判定
                if (connectableCell == endCell)
                {
                    path[connectableCell] = currentCell;
                    isReached = true;
                    break;
                }
                
                if (connectableCell is IContainable or IExportable)
                {
                    // 入力または出力の機能をもつセルは探索しない
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
        if (length == 0) return;

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

    /// <summary>
    /// 始点から終点までの輸送を試行する
    /// </summary>
    /// <param name="exporter">始点となるセル</param>
    /// <param name="exportAmount">輸送する量</param>
    /// <param name="exportItemSpeed">輸送する速度</param>
    /// <param name="exportBeginPos">アニメーション開始地点の座標</param>
    /// <param name="allocated">予約に成功した輸送量</param>
    /// <param name="logMode">falseが返されるときのログ表示（デバッグ用）</param>
    /// <returns>輸送に成功したかどうか</returns>
    public static bool TryExport(IExportable exporter, int exportAmount, float exportItemSpeed,
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

        var exportType = exporter.ResourceType;
        List<ConnectableCellBase> path = null;
        IContainable container = null;
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
            // 検索条件
            // ・終点がIContainableである。
            // ・IContainableに許容量がある。
            // ・IExportableとIContainableのタイプが不正でない
            if (p?.LastOrDefault() is not IContainable containable)
            {
#if UNITY_EDITOR
                if (logMode) Debug.LogWarning("終点がContainableでないためスキップされました");
#endif
                continue;
            }

            if (containable.IsFull())
            {
#if UNITY_EDITOR
                if (logMode) Debug.LogWarning("終点が容量限界のためスキップされました");
#endif
                continue;
            }

            if (containable.StoredResourceType != ResourceType.None &&
                containable.StoredResourceType != exportType)
            {
#if UNITY_EDITOR
                if (logMode) Debug.LogWarning("リソースタイプが不正のためスキップされました");
#endif
                continue;
            }

            // 一致した場合、要素を変数に保持。
            path = p;
            container = containable;
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

        // 予め終点にリソースの輸入を予約する。
        var allocatedAmount = container.AllocateStorage(exportAmount, exportType);

        // 予約分を保存
        allocated = allocatedAmount;

        var padding = Vector3.up * 1.1f;

        // ObjectPoolからモデルを呼び出す
        var itemObj = ResourceItemObjectPool.Instance.GetPrefab(exportType);
        itemObj.transform.position = exportBeginPos + padding;

        // 始点から終点までのアニメーション
        itemObj.transform
            .DOPath(path.Select(cell => cell.transform.position + padding).ToArray(),
                exportItemSpeed * path.Count)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                // ストレージに保存
                container.StoreResource(allocatedAmount);

                // ObjectPoolにモデルを返す
                ResourceItemObjectPool.Instance.Return(exportType, itemObj);
            });

        // 全ての処理が問題なく処理できたのでtrueを返す
        return true;
    }
}