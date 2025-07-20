using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

public sealed class ExtractorCell : ConnectableCellBase, IExportable, IDataProvidable
{
    [Header("抽出設定")]
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private float extractionSecond;
    [SerializeField] private int extractionAmount;

    [Header("その他設定")]
    [SerializeField] private ResourceSO resourceDatabase;
    [SerializeField] private ExporterModule exportableModule;
    [SerializeField] private ExtractorProvider extractorProvider;

    private CellBase _forwardCell;
    private bool _isActivate;
    private CancellationTokenSource _cts;

    public ExporterModule ExportableModule => exportableModule;
    public float ElapsedTime { get; private set; }
    public float ExtractionSecond => extractionSecond;
    public ResourceType ResourceType => resourceType;
    public bool IsUIActive { private get; set; }
    public IUIDataProvider GetDataProvider() => extractorProvider;

    public override void InitializeSystem()
    {
        base.InitializeSystem();
        _isActivate = true;

        _forwardCell = AdjacentCells
            .OfType<ResourceCell>()
            .FirstOrDefault(cell =>
                cell.XIndex == XIndex + Mathf.RoundToInt(transform.forward.x) &&
                cell.ZIndex == ZIndex + Mathf.RoundToInt(transform.forward.z) &&
                cell.ResourceType == ResourceType);

        if (_forwardCell == null) return;
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ExportableModule)}がnullです。");
#endif
            return;
        }

        ExportableModule.ExportResourceType = ResourceType;
        ExportableModule.OnExport += UpdateUI;
        
        _cts = new();
        TakeResourcesFromAdjacentStorageAsync(_cts.Token).Forget();
    }
    
    private void OnDestroy()
    {
        _isActivate = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void UpdateUI()
    {
        if (!IsUIActive) return;
        CellStatusView.Instance.UpdateUI();
    }

    private async UniTask TakeResourcesFromAdjacentStorageAsync(CancellationToken token)
    {
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"{nameof(ExportableModule)}がnullです。エクスポート処理を中断します。");
#endif
            return;
        }

        while (_isActivate && !token.IsCancellationRequested)
        {
            // 容量に空きが出るまで待機
            await UniTask.WaitUntil(() => ExportableModule.ExportResourceAmount < ExportableModule.ExporterCapacity,
                cancellationToken: token);

            var tween = DOTween.To(
                    () => ElapsedTime,
                    x => ElapsedTime = x,
                    ExtractionSecond,
                    ExtractionSecond)
                .OnUpdate(UpdateUI)
                .SetEase(Ease.Linear);

            // 抽出が終わるまで待機
            await tween.ToUniTask(cancellationToken: token);
            ElapsedTime = 0;

            // 輸出モジュールにリソースを転送するまで待機
            var available = ExportableModule.ExporterCapacity - ExportableModule.ExportResourceAmount;
            var gainAmount = Mathf.Min(available, extractionAmount);

            await UniTask.WaitUntil(() => ExportableModule.TryStackToExporter(gainAmount), cancellationToken: token);
            UpdateUI();
        }
    }
}