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

    [Header("UI設定")]
    [SerializeField] private Image extractionProgressBar;
    [SerializeField] private Image storageAmountBar;

    [Header("その他設定")]
    [SerializeField] private ResourceSO resourceDatabase;

    private int StorageAmount { get; set; }

    public ResourceType ExportResourceType => resourceType;

    public HashSet<(int length, List<ConnectableCellBase> path)> ExportPaths { get; private set; } = new();
    private CellBase _forwardCell;
    private bool _isExtractable;

    protected override void Start()
    {
        base.Start();
        foreach (var cell in AdjacentCells)
        {
            if (cell == null) continue;
            if (cell.XIndex != XIndex + Mathf.RoundToInt(transform.forward.x) ||
                cell.ZIndex != ZIndex + Mathf.RoundToInt(transform.forward.z)) continue;
            if (cell is ResourceCell resourceCell &&
                resourceCell.ResourceType == resourceType)
                
            // 有効なリソースセルを前方に見つけたら保存
            _forwardCell = cell;
            _isExtractable = true;
            break;
        }

        extractionProgressBar.fillAmount = 0;
        storageAmountBar.fillAmount = 0;

        if (_forwardCell == null || _forwardCell is not ResourceCell) return;
        StartCoroutine(ExtractEnumerator());
    }

    private IEnumerator ExtractEnumerator()
    {
        while (_isExtractable)
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
                yield return new WaitUntil(TryExportResources);
                UpdateUI();
            }
        }
    }

    private void Extract()
    {
        if (_forwardCell == null || _forwardCell is not ResourceCell) return;
        StorageAmount += extractionAmount;

        // 抽出後、一度だけ輸出を試行する
        _ = TryExportResources();
        UpdateUI();
    }

    private bool TryExportResources()
    {
        // ネットワークを介してターゲットにリソースを送る
        var isAllowedToTransfer = PipelineNetworkManager.Instance.TryExport(
            exporter: this,
            exportAmount: StorageAmount,
            exportBeginPos: transform.position,
            allocated: out var allocatedAmount);

        // 輸出が確立されたら現在のリソース値から予約量を減らす
        if (isAllowedToTransfer) StorageAmount -= allocatedAmount;

        return isAllowedToTransfer;
    }

    public void RefreshPath()
    {
        // 経路内にnullが含まれている場合、経路として不正なので除外する
        var refreshedPaths = ExportPaths.Where(pathInfo => pathInfo
            .path.All(cell => cell != null)).ToHashSet();
        ExportPaths.Clear();
        ExportPaths = refreshedPaths;
    }


    private void UpdateUI()
    {
        if (storageAmountBar != null)
        {
            storageAmountBar.fillAmount = (float)StorageAmount / extractionCapacity;
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

        if (path.Last() is not IContainable)
        {
            Debug.LogWarning("パスの終点がストレージセルではありません。パスを追加できません。", this);
            return;
        }

        ExportPaths.Add((length, path));
        ExportPaths = ExportPaths.OrderBy(p => p.length).ToHashSet();
    }

    // 描画のし過ぎでシーンが重くなるためコメントアウト
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.blue;
        var startPadding = Vector3.up * 5f;
        foreach (var (_, path) in ExportPaths.Where(pathInfo => pathInfo.path != null && pathInfo.path.Count != 0))
        {
            // パスの先頭から終点までの線を描画
    
            ConnectableCellBase firstCell = this;
            foreach (var cell in path)
            {
                Gizmos.DrawLine(firstCell.transform.position + startPadding, cell.transform.position + startPadding);
                firstCell = cell;
            }
    
            startPadding += Vector3.up * 0.2f;
        }
    }
}