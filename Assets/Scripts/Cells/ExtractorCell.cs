using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExtractorCell : ConnectableCellBase, IExportable
{
    [Header("抽出設定")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private float extractionSecond;
    [SerializeField] private int extractionAmount;
    [SerializeField] private int extractionCapacity;
    [SerializeField] private float itemMoveBaseSecond;

    [Header("UI設定")]
    [SerializeField] private Image extractionProgressBar;
    [SerializeField] private Image storageAmountBar;

    [Header("その他設定")]
    [SerializeField] private ResourceSO resourceDatabase;

    private int StorageAmount { get; set; }

    public HashSet<(int length, List<ConnectableCellBase> path)> ExportPaths { get; set; } = new();
    private List<ConnectableCellBase> _currentPath;
    private CellBase _forwardCell;

    protected override void Start()
    {
        base.Start();
        foreach (var cell in AdjacentCells)
        {
            if (cell == null) continue;
            if (cell.XIndex != XIndex + Mathf.RoundToInt(transform.forward.x) ||
                cell.ZIndex != ZIndex + Mathf.RoundToInt(transform.forward.z)) continue;
            // 前方のセルを見つけたら保存
            _forwardCell = cell;
            break;
        }

        extractionProgressBar.fillAmount = 0;
        storageAmountBar.fillAmount = 0;

        if (_forwardCell == null || _forwardCell is not ResourceCell) return;
        StartCoroutine(ExtractEnumerator());
    }

    private IEnumerator ExtractEnumerator()
    {
        while (true)
        {
            // ストレージに保存できる容量があるか確認
            if (StorageAmount < extractionCapacity)
            {
                extractionProgressBar.fillAmount = 0f;

                var tween = extractionProgressBar
                    .DOFillAmount(1f, extractionSecond)
                    .SetEase(Ease.Linear);

                yield return tween.WaitForCompletion();
                Extract();
            }
            else
            {
                // 容量上限に達した場合はスペースが空くまで待機
                yield return new WaitUntil(() => HasStorageCapacity(out _));
                ExportResources();
                UpdateUI();
            }
        }
    }

    /// <summary>
    /// 周囲のストレージセルが見つかり、かつ容量に空きがあるかを確認する
    /// </summary>
    private bool HasStorageCapacity(out IContainable containable)
    {
        if (ExportPaths == null || ExportPaths.Count == 0)
        {
            containable = null;
            return false;
        }

        containable = null;
        var hasCapacity = false;

        RefreshPaths();

        // 経路の先にストレージセルがある場合、そこにリソースを保存する
        foreach (var (_, path) in ExportPaths)
        {
            // 経路の終点がストレージセルでない場合はスキップ
            if (path?.Last() is not IContainable storage) continue;

            if (storage.IsFull()) continue;

            if (storage.StoredResourceType != ResourceType.None &&
                storage.StoredResourceType != resourceType)
            {
                // ストレージセルのリソースタイプが異なる場合はスキップ
                continue;
            }

            // 空きのあるストレージセルが見つかった場合は、trueを返す
            _currentPath = path;
            containable = storage;
            hasCapacity = true;
            break;
        }

        return hasCapacity;
    }

    private void RefreshPaths()
    {
        // ExportPathsを更新するために、現在のセルからストレージセルまでのパスを再計算
        var refreshedPaths = ExportPaths.Where(pathInfo => pathInfo.path.All(cell => cell != null)).ToHashSet();
        ExportPaths.Clear();
        ExportPaths = refreshedPaths;
    }

    private void Extract()
    {
        // 前方のセルがリソースセルで、指定されたリソースタイプと一致する場合
        if (_forwardCell is ResourceCell resourceCell &&
            resourceCell.ResourceType == resourceType)
        {
            StorageAmount += extractionAmount;
        }

        ExportResources();
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (storageAmountBar != null)
        {
            storageAmountBar.fillAmount = (float)StorageAmount / extractionCapacity;
        }
    }

    public void ExportResources()
    {
        // 経路の先にストレージセルがある場合、そこにリソースを保存する
        if (!HasStorageCapacity(out var containable)) return;

        var allocatedAmount = containable.AllocateStorage(StorageAmount);
        StorageAmount -= allocatedAmount;

        var padding = Vector3.up * 1.1f;
        var itemObj = ResourceItemObjectPool.Instance.GetPrefab(resourceType);
        itemObj.transform.position = transform.position + padding;
        
        // 始点から終点までのアニメーション
        itemObj.transform
            .DOPath(_currentPath.Select(cell => cell.transform.position + padding).ToArray(),
                itemMoveBaseSecond * _currentPath.Count)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                // ストレージに保存できる量を計算
                var overflowAmount = containable.StoreResource(allocatedAmount, resourceType);

                // ストレージに保存できなかった分は戻す
                StorageAmount += overflowAmount;
                ResourceItemObjectPool.Instance.Return(resourceType, itemObj);
            });
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

        if (path.Last() is not IContainable)
        {
            Debug.LogWarning("パスの終点がストレージセルではありません。パスを追加できません。", this);
            return;
        }

        ExportPaths.Add((length, path));
        ExportPaths = ExportPaths.OrderBy(p => p.length).ToHashSet();
    }

    // 描画のし過ぎでシーンが重くなるためコメントアウト
    // protected override void OnDrawGizmos()
    // {
    //     base.OnDrawGizmos();
    //     Gizmos.color = Color.blue;
    //     var startPadding = Vector3.up * 5f;
    //     foreach (var (_, path) in ExportPaths.Where(pathInfo => pathInfo.path != null && pathInfo.path.Count != 0))
    //     {
    //         // パスの先頭から終点までの線を描画
    //
    //         ConnectableCellBase firstCell = this;
    //         foreach (var cell in path)
    //         {
    //             Gizmos.DrawLine(firstCell.transform.position + startPadding, cell.transform.position + startPadding);
    //             firstCell = cell;
    //         }
    //
    //         startPadding += Vector3.up * 0.2f;
    //     }
    // }
}