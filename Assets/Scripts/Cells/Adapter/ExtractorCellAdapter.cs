using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using System;

public sealed class ExtractorCellAdapter : ConnectableCellBaseAdapter, IExportableApplication, IDataProvidableApplication
{
    [Header("抽出設定")]
    [SerializeField] private ResourceTypeDomain resourceType;
    [SerializeField] private float extractionSecond;
    [SerializeField] private int extractionAmount;
    [SerializeField] private int storageCapacity;

    [Header("その他設定")]
    [SerializeField] private ResourceDefinition resourceDatabase;
    [SerializeField] private ExtractorProviderDefinition extractorProvider;

    private ResourceCellAdapter _forwardCell;
    private GridFieldDatabaseAdapter _grid;
    private bool _initialized;
    private bool _isActivate;
    private bool _runnerActive;
    private CancellationTokenSource _cts;

    public int CurrentLoad { get; private set; }
    public float ElapsedTime { get; private set; }
    public float ExtractionSecond => extractionSecond;
    public ResourceTypeDomain ResourceTypeDomain => resourceType;
    public event Action StatusChanged;

    public int StorageCapacity => EffectiveStat(storageCapacity, EquipmentStatDomain.Capacity);
    public int ExtractionAmount => EffectiveStat(extractionAmount, EquipmentStatDomain.ExtractionAmount);
    public override CellTypeDomain ProgressionType => ResourceCellRulesDomain.ExtractorType(resourceType);
    protected override void ValidateBaseStats()
    {
        ValidateStat(storageCapacity, EquipmentStatDomain.Capacity);
        ValidateStat(extractionAmount, EquipmentStatDomain.ExtractionAmount);
    }
    protected override void OnStatsChanged() => UpdateUI();

    public IUIDataProviderApplication GetDataProvider() => CellStatusProviderAdapter.Create(this, extractorProvider);

    public override void InitializeSystem()
    {
        StopExtraction();
        base.InitializeSystem();
        _grid = GridFieldDatabaseAdapter.Instance;
        _initialized = true;
        _isActivate = isActiveAndEnabled;
        // 再初期化でも切断購読と生産処理を重複させない。
        OnDisconnected -= StopForDisconnect;
        OnDisconnected += StopForDisconnect;
        RefreshExtraction();
    }

    private void OnEnable()
    {
        _isActivate = _initialized;
        RefreshExtraction();
    }

    private void Update() => RefreshExtraction();

    private void OnDisable()
    {
        _isActivate = false;
        StopExtraction();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        OnDisconnected -= StopForDisconnect;
        _initialized = false;
        _isActivate = false;
        StopExtraction();
    }

    private void StopForDisconnect()
    {
        _initialized = false;
        _isActivate = false;
        StopExtraction();
    }

    private ResourceCellAdapter ResolveForwardResource()
    {
        if (_grid == null) return null;
        var x = XIndex + Mathf.RoundToInt(transform.forward.x);
        var z = ZIndex + Mathf.RoundToInt(transform.forward.z);
        if (!_grid.IsWithinBounds(x, z)) return null;
        var cell = _grid.GetCell(x, z) as ResourceCellAdapter;
        return cell != null && cell.isActiveAndEnabled && cell.ResourceTypeDomain == resourceType ? cell : null;
    }

    private void RefreshExtraction()
    {
        if (!_initialized || !_isActivate || !isActiveAndEnabled) return;
        // 資源セルは非接続型なので、通知や初期化時の隣接配列だけでは後置き・交換を検知できない。
        var target = ResolveForwardResource();
        if (target == null || target != _forwardCell)
        {
            StopExtraction();
            _forwardCell = target;
        }
        if (target == null || _runnerActive) return;
        _cts = new CancellationTokenSource();
        _runnerActive = true;
        RunExtractionAsync(target, _cts).Forget();
    }

    private void StopExtraction()
    {
        // CTSのDisposeは所有するrunnerが行う。取消完了前の新runner起動を防ぐ。
        var source = _cts;
        _cts = null;
        _forwardCell = null;
        source?.Cancel();
        if (ElapsedTime != 0)
        {
            ElapsedTime = 0;
            UpdateUI();
        }
    }

    private void UpdateUI() => StatusChanged?.Invoke();

    private async UniTask RunExtractionAsync(ResourceCellAdapter target, CancellationTokenSource source)
    {
        var token = source.Token;
        Tween tween = null;
        try
        {
            while (_isActivate && !token.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => CurrentLoad < StorageCapacity, cancellationToken: token);
                // 周期開始時の空きと抽出量を固定し、途中の強化は次周期へ反映する。
                var cycleAmount = Mathf.Min(StorageCapacity - CurrentLoad, ExtractionAmount);
                var completed = false;
                tween = DOTween.To(() => ElapsedTime, x => ElapsedTime = x,
                        ExtractionSecond, ExtractionSecond)
                    .OnUpdate(UpdateUI).OnComplete(() => completed = true).SetEase(Ease.Linear);
                await tween.ToUniTask(TweenCancelBehaviour.CancelAwait, token);
                // Tween完了と資源確定は別。取消・削除・交換・外部Killでは未完了分を増やさない。
                if (token.IsCancellationRequested || !completed || !_isActivate || !isActiveAndEnabled ||
                    !_initialized || target == null || ResolveForwardResource() != target) return;
                ElapsedTime = 0;
                CurrentLoad += cycleAmount;
                UpdateUI();
                tween = null;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 対象喪失・無効化・切断による正常な中断。
        }
        finally
        {
            if (tween != null && tween.IsActive()) tween.Kill();
            if (ReferenceEquals(_cts, source))
            {
                _cts = null;
                ElapsedTime = 0;
                UpdateUI();
            }
            _runnerActive = false;
            source.Dispose();
        }
    }

    public Vector3 GetPosition(int portId) => PortPosition(portId);

    public bool TryExport(int portId, int requestedAmount, out int amount, out ResourceTypeDomain type)
    {
        amount = 0;
        type = resourceType;
        if (!HasPort(portId, CellPortUseDomain.Output)) return false;
        
        // 出力可能な量がない、または要求量がない場合はfalseを返す
        if (CurrentLoad <= 0 || requestedAmount <= 0) return false;
        
        // 返却量を計算し、現在量を減らす
        amount = Mathf.Min(requestedAmount, CurrentLoad);
        CurrentLoad = Mathf.Max(0, CurrentLoad - requestedAmount);
        
        UpdateUI();
        return true;
    }
}
