using System.Collections;
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

    [Header("UI設定")]
    [SerializeField] private Image extractionProgressBar;
    [SerializeField] private Image storageAmountBar;

    [Header("その他設定")]
    [SerializeField] private ResourceSO resourceDatabase;
    [SerializeField] private ExporterModule exportableModule;

    public ExporterModule ExportableModule => exportableModule;
    private CellBase _forwardCell;
    private bool _isActivate;

    protected override void Start()
    {
        base.Start();
        _isActivate = true;
        extractionProgressBar.fillAmount = 0;
        storageAmountBar.fillAmount = 0;
        
        _forwardCell = AdjacentCells
            .OfType<ResourceCell>()
            .FirstOrDefault(cell =>
                cell.XIndex == XIndex + Mathf.RoundToInt(transform.forward.x) &&
                cell.ZIndex == ZIndex + Mathf.RoundToInt(transform.forward.z) &&
                cell.ResourceType == resourceType);

        if (_forwardCell == null) return;
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ExportableModule)}がnullです。");
#endif
            return;
        }

        ExportableModule.ExportResourceType = resourceType;
        ExportableModule.OnExport += UpdateUI;
        StartCoroutine(ExtractFromForwardResourceEnumerator());
    }

    private IEnumerator ExtractFromForwardResourceEnumerator()
    {
        while (_isActivate)
        {
            // 容量に空きが出るまで待機
            yield return new WaitUntil(() => ExportableModule.ExportResourceAmount < ExportableModule.ExporterCapacity);
            
            extractionProgressBar.fillAmount = 0f;

            var tween = extractionProgressBar
                .DOFillAmount(1f, extractionSecond)
                .SetEase(Ease.Linear);

            // 抽出が終わるまで待機
            yield return tween.WaitForCompletion();
            
            // 輸出モジュールにリソースを転送するまで待機
            var available = ExportableModule.ExporterCapacity - ExportableModule.ExportResourceAmount;
            var gainAmount = Mathf.Min(available, extractionAmount);
            
            yield return new WaitUntil(() => ExportableModule.TryStackToExporter(gainAmount));
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (storageAmountBar != null)
        {
            storageAmountBar.fillAmount = (float)ExportableModule.ExportResourceAmount / ExportableModule.ExporterCapacity;
        }
    }
}