using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

public sealed class PipelineNetworkManager : SingletonMonoBehaviour<PipelineNetworkManager>
{
    [SerializeField] private float itemTransferSecondPerCell;
    private readonly HashSet<HashSet<ConnectableCellBase>> _pipelineNetworks = new();

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
                var mergedNetwork = new HashSet<ConnectableCellBase>();
                foreach (var network in connectedNetworks)
                {
                    mergedNetwork = mergedNetwork.Concat(network).ToHashSet();
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
            foreach (var startCell in network.OfType<IExportable>())
            {
                // ExportableModuleのExportPathsから、削除されたセルを含む経路を削除
                startCell.ExportableModule.ExportPaths =
                    startCell.ExportableModule.ExportPaths
                        .Where(path => !path.Contains(cell)).ToHashSet();
            }

            RegisterAllNetworkPaths(network);
        }
    }

    /// <summary>
    /// ネットワーク内の全てのセル間の経路を登録します。
    /// </summary>
    /// <param name="network">検索対象のネットワーク</param>
    private void RegisterAllNetworkPaths(HashSet<ConnectableCellBase> network)
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
    /// ネットワーク内の2つのセル間の最短経路を非同期・マルチスレッドで登録します。
    /// </summary>
    /// <param name="startCell">始点となるセル</param>
    /// <param name="endCell">終点となるセル</param>
    /// <param name="token">キャンセレーショントークン</param>
    private async UniTask RegisterAllPathByNetworkAsync(
        ConnectableCellBase startCell,
        ConnectableCellBase endCell,
        CancellationToken token = default)
    {
        if (startCell == null) return;

        // 重い処理をワーカースレッドで実行
        var foundPaths = await UniTask.RunOnThreadPool(() =>
        {
            // スレッドセーフなコレクション
            // ConcurrentBagを使用して、見つかったパスを保存
            var foundPaths = new ConcurrentBag<Dictionary<ConnectableCellBase, ConnectableCellBase>>();

            // BFSのため、訪問済みを管理するためのConcurrentDictionary
            var visited = new ConcurrentDictionary<ConnectableCellBase, bool>();

            // 各スレッドで処理するためのキュー
            var workQueue =
                new ConcurrentQueue<(ConnectableCellBase cell,
                    Dictionary<ConnectableCellBase, ConnectableCellBase> path, int depth)>();

            // 始点セルをキューに追加
            visited[startCell] = true;
            workQueue.Enqueue((startCell, new(), 0));

            // 探索の深さとスレッド数の制限
            const int MaxDepth = 50;
            const int MaxThreads = 10;

            // Parallel.Forを使用して、複数のスレッドでBFS探索を行う
            Parallel.For(0, MaxThreads, new() { CancellationToken = token }, _ =>
            {
                // 各スレッドでローカルキューを使用
                var localQueue =
                    new Queue<(ConnectableCellBase, Dictionary<ConnectableCellBase, ConnectableCellBase>, int)>();

                // トークンがキャンセルされるまでループ
                while (!token.IsCancellationRequested)
                {
                    // グローバルキューから作業を取得
                    var hasWork = false;
                    for (int i = 0; i < 10 && workQueue.TryDequeue(out var workItem); i++)
                    {
                        localQueue.Enqueue(workItem);
                        hasWork = true;
                    }

                    // もしローカルキューが空で、グローバルキューも空ならば終了
                    if (!hasWork)
                    {
                        Thread.Sleep(1);
                        if (!workQueue.TryDequeue(out var lastItem)) break;
                        localQueue.Enqueue(lastItem);
                    }

                    // ローカルキューの処理
                    while (localQueue.Count > 0 && !token.IsCancellationRequested)
                    {
                        // キューから現在のセル、パス、深さを取得
                        var (currentCell, currentPath, depth) = localQueue.Dequeue();

                        // 深さ制限を超えた場合はスキップ
                        if (depth >= MaxDepth) continue;

                        // 現在のセルの隣接セルを取得
                        var adjacentCells = currentCell is CrossedPipeCell crossedPipeCell
                            ? crossedPipeCell.GetCrossedAdjacentCells(currentPath.GetValueOrDefault(currentCell))
                            : currentCell.GetAdjacentCells();

                        foreach (var cell in adjacentCells)
                        {
                            // 接続可能なセルでない、または既に訪問済みのセルはスキップ
                            if (cell is not ConnectableCellBase connectableCell ||
                                !visited.TryAdd(connectableCell, true) ||
                                cell == startCell) continue;

                            // 接続可能なセルを見つけた場合、経路を更新
                            var nextPath = new Dictionary<ConnectableCellBase, ConnectableCellBase>(currentPath)
                            {
                                [connectableCell] = currentCell
                            };

                            // 終点に到達
                            if (connectableCell == endCell)
                            {
                                foundPaths.Add(nextPath);
                                continue;
                            }

                            // 探索継続の条件
                            if (connectableCell is IContainable or IExportable) continue;

                            // 次のセルと経路をキューに追加
                            workQueue.Enqueue((connectableCell, nextPath, depth + 1));
                        }
                    }
                }
            });

            // キューに残っているパスを全て取得してリストに変換
            return foundPaths.ToList();
        }, cancellationToken: token);

        if (foundPaths.Count == 0) return;

        // パス登録（メインスレッドに戻して実行）
        await UniTask.SwitchToMainThread(token);

        foreach (var path in foundPaths)
        {
            var resultPath = new List<ConnectableCellBase>();
            var current = endCell;

            // 終点から始点までのパスを逆順に辿る
            while (current != null && path.TryGetValue(current, out var next))
            {
                resultPath.Add(current);
                current = next;
            }

            // パスが空でない場合、始点セルがIExportableであればパスを登録
            if (resultPath.Count > 0 && startCell is IExportable exportableStart)
            {
                resultPath.Reverse();
                AddPath(exportableStart.ExportableModule, resultPath);
            }

            // 大量のパスがある場合はフレーム制御
            if (foundPaths.Count > 20)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
    }

    /// <summary>
    /// 同期版のラッパー（後方互換性のため）
    /// </summary>
    /// <param name="startCell">始点となるセル</param>
    /// <param name="endCell">終点となるセル</param>
    private void RegisterAllPathByNetwork(ConnectableCellBase startCell, ConnectableCellBase endCell)
    {
        RegisterAllPathByNetworkAsync(startCell, endCell).Forget();
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
    public bool TryExport(ExporterModule exporter, int exportAmount,
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

        RefreshPath(exporter);
        if (exporter.ExportPaths.Count == 0)
        {
#if UNITY_EDITOR
            if (logMode) Debug.LogWarning("パスが割り当てられていません");
#endif
            return false;
        }

        foreach (var p in exporter.ExportPaths)
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
            var dir = (p.Last().transform.position　- p[^2].transform.position).ToCardinalDirection();
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
        var textMesh = itemObj.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            // Textが存在する場合、予約量を表示
            textMesh.text = allocatedAmount.ToString();
        }

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

    private static void RefreshPath(ExporterModule exporter)
    {
        // 経路内にnullが含まれている場合、経路として不正なので除外する
        var refreshedPaths = exporter.ExportPaths.Where(p => p.All(cell => cell != null)).ToHashSet();
        exporter.ExportPaths.Clear();
        exporter.ExportPaths = refreshedPaths;
    }

    private void AddPath(ExporterModule exporter, List<ConnectableCellBase> path)
    {
        // 既に同じパスが存在する場合は追加しない
        if (exporter.ExportPaths.Any(p => p.SequenceEqual(path))) return;
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("パスが空です。パスを追加できません。", this);
            return;
        }

        if (path.Last() is not IContainable)
        {
            Debug.LogWarning("パスの終点がストレージセルではありません。パスを追加できません。", this);
            return;
        }

        // 各セルごとに設定されたフィルタリングをチェックする
        if (exporter.OnFilterPath != null && !exporter.OnFilterPath.Invoke(path)) return;

        exporter.ExportPaths.Add(path);
        exporter.ExportPaths = exporter.ExportPaths.OrderBy(p => p.Count).ToHashSet();
    }
}