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
    [SerializeField] private int storageCapacity;

    [Header("その他設定")]
    [SerializeField] private ResourceSO resourceDatabase;
    [SerializeField] private ExtractorProvider extractorProvider;

    private CellBase _forwardCell;
    private bool _isActivate;
    private CancellationTokenSource _cts;

    public int CurrentLoad { get; private set; }
    public float ElapsedTime { get; private set; }
    public float ExtractionSecond => extractionSecond;
    public ResourceType ResourceType => resourceType;
    public bool IsUIActive { private get; set; }

    public int StorageCapacity => storageCapacity;

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
        while (_isActivate && !token.IsCancellationRequested)
        {
            // 容量に空きが出るまで待機
            await UniTask.WaitUntil(() => CurrentLoad < StorageCapacity,
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
            var available = StorageCapacity - CurrentLoad;
            var gainAmount = Mathf.Min(available, extractionAmount);
            CurrentLoad += gainAmount;
            
            UpdateUI();
        }
    }
    
    
    public Vector3 GetPosition() => transform.position;

    public bool TryExport(Vector3 from, int requestedAmount, out int amount, out ResourceType type)
    {
        amount = 0;
        type = resourceType;
        
        // 出力可能な量がない、または要求量がない場合はfalseを返す
        if (CurrentLoad <= 0 || requestedAmount <= 0) return false;
        
        // 返却量を計算し、現在量を減らす
        amount = Mathf.Min(requestedAmount, CurrentLoad);
        CurrentLoad = Mathf.Max(0, CurrentLoad - requestedAmount);
        
        UpdateUI();
        return true;
    }
}