using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ExportPipeCell : ItemPipeCell, IExportable
{
    [SerializeField] private float exportIntervalSecond;
    [SerializeField] private int maxExportAmount;
    [SerializeField] private float itemExportBaseSecond;

    private StorageCell[] _storages;
    private bool _isExportable;

    public HashSet<(int length, List<ConnectableCellBase> path)> ExportPaths { get; private set; } = new();
    public ResourceType ResourceType { get; private set; }

    protected override void Start()
    {
        OnConnectionChanged += SearchStorageCell;
        base.Start();
        _isExportable = true;
        StartCoroutine(ExportEnumerator());
    }

    private void SearchStorageCell() => _storages = AdjacentCells.OfType<StorageCell>().ToArray();

    private IEnumerator ExportEnumerator()
    {
        while (_isExportable)
        {
            // 周囲にストレージセルがあるかどうか
            yield return new WaitUntil(() => _storages != null && ExportPaths.Count != 0);

            var takenAmount = 0;
            Vector3 beginPos = default;
            Action resourceUpdateAction = null;

            foreach (var cell in _storages)
            {
                // 各ストレージからリソースをもらう
                takenAmount = cell.ReserveResource(maxExportAmount, out var type);

                // 取得に失敗した場合、次のストレージへ
                if (takenAmount <= 0) continue;

                // 成功した場合、リソースタイプとストレージの座標を保存
                ResourceType = type;
                beginPos = cell.transform.position;
                resourceUpdateAction = () => cell.TakeResource(takenAmount);
                break;
            }

            if (takenAmount > 0)
            {
                // リソースの輸出
                yield return new WaitUntil(() => PipelineNetworkManager.TryExport(
                    exporter: this,
                    exportAmount: takenAmount,
                    exportItemSpeed: itemExportBaseSecond,
                    exportBeginPos: beginPos,
                    allocated: out _
                ));
                resourceUpdateAction?.Invoke();

                // 輸出後、インターバル分待機する
                yield return new WaitForSeconds(exportIntervalSecond);
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }
        }
    }

    public void AddPath(int length, List<ConnectableCellBase> path)
    {
        // 既に同じパスが存在する場合は追加しない
        if (ExportPaths.Any(p => p.path.SequenceEqual(path))) return;
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("パスが空です。パスを追加できません。", this);
            return;
        }

        if (path.Last() is not IContainable containable)
        {
            Debug.LogWarning("パスの終点がストレージセルではありません。パスを追加できません。", this);
            return;
        }

        // 隣接したストレージセルが輸出先にしないための処理
        if (_storages.Contains((ConnectableCellBase)containable))
        {
            Debug.LogWarning("終点が隣接ストレージと同一になるのは不正です。パスの追加をスキップしました。");
            return;
        }

        ExportPaths.Add((length, path));
        ExportPaths = ExportPaths.OrderBy(p => p.length).ToHashSet();
    }

    public void RefreshPath()
    {
        // 経路内にnullが含まれている場合、経路として不正なので除外する
        var refreshedPaths = ExportPaths.Where(pathInfo => pathInfo
            .path.All(cell => cell != null)).ToHashSet();
        ExportPaths.Clear();
        ExportPaths = refreshedPaths;
    }
}