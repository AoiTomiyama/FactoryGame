using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExtractorCellBase : ConnectableCellBase
{
    [Header("抽出設定")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private float extractionSecond;
    [SerializeField] private int extractionAmount;
    [SerializeField] private int extractionCapacity;

    [Header("UI設定")]
    [SerializeField] private Image extractionProgressBar;
    [SerializeField] private Image storageAmountBar;

    private CellBase _forwardCell;
    private int _currentExtractedAmount;

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
            if (_currentExtractedAmount < extractionCapacity)
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
                OutputResources();
                UpdateUI();
            }
        }
    }

    /// <summary>
    /// 周囲のストレージセルが見つかり、かつ容量に空きがあるかを確認する
    /// </summary>
    /// <returns></returns>
    private bool HasStorageCapacity(out StorageCell storageCell)
    {
        foreach (var cell in AdjacentCells)
        {
            // セルがnullの場合はスキップ
            if (cell == null) continue;
            
            // ストレージセルでない場合もスキップ
            if (cell is not StorageCell storage) continue;
            
            // ストレージセルが見つかったが、容量がいっぱいの場合はスキップ
            if (storage.IsFull()) continue;
            
            storageCell = storage;
            return true;
        }

        // 周囲のセルにストレージセルが見つからない、もしくは全て容量がいっぱいの場合はfalseを返す
        storageCell = null;
        return false;
    }

    private void Extract()
    {
        // 前方のセルがリソースセルで、指定されたリソースタイプと一致する場合
        if (_forwardCell is ResourceCell resourceCell &&
            resourceCell.ResourceType == resourceType)
        {
            _currentExtractedAmount += extractionAmount;
        }

        OutputResources();
        UpdateUI();
    }

    private void OutputResources()
    {
        // ストレージセルがない場合、処理を終了
        if (!HasStorageCapacity(out var storage)) return;

        // ストレージセルに保存できる量がある場合は、保存する
        var overflowAmount = storage.StoreResource(_currentExtractedAmount, resourceType);
        _currentExtractedAmount = 0;

        // ストレージに保存できなかった分は戻す
        _currentExtractedAmount += overflowAmount;
    }

    private void UpdateUI()
    {
        if (storageAmountBar != null)
        {
            storageAmountBar.fillAmount = (float)_currentExtractedAmount / extractionCapacity;
        }
    }
}