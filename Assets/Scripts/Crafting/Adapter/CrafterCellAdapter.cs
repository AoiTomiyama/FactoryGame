using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;


public class CrafterCellAdapter : ConnectableCellBaseAdapter, IContainableApplication, IExportableApplication, IDataProvidableApplication
{
    [Header("クラフト設定")]
    [SerializeField] private int ingredientCapacity;
    [SerializeField] private CellTypeDomain equipmentType = CellTypeDomain.Crafter;
    [SerializeField] private int exporterCapacity;
    [SerializeField] [InlineSOAttributeAdapter]
    private RecipeDatabaseDefinition recipeDatabase;

    [Header("その他の設定")]
    [SerializeField] private CrafterProviderDefinition crafterProvider;

    private readonly Dictionary<int, ResourceInputData> _resourceInputs = new();
    private bool _isActivate;
    private bool _isInitialized;
    private CraftingOperationApplication _operation;

    public int ExportStorageAmount { get; private set; }
    public float ProcessTime { get; private set; }
    public float ElapsedProcessTime { get; private set; }
    public event Action StatusChanged;

    public ResourceTypeDomain ExportResourceType { get; private set; }

    public int IngredientCapacity => EffectiveStat(ingredientCapacity, EquipmentStatDomain.InputCapacity);

    public int ExporterCapacity => EffectiveStat(exporterCapacity, EquipmentStatDomain.OutputCapacity);
    public override CellTypeDomain ProgressionType => equipmentType;
    protected override void ValidateBaseStats()
    {
        if (equipmentType is not (CellTypeDomain.Crafter or CellTypeDomain.AdvancedCrafter))
            throw new InvalidOperationException("Unsupported crafting equipment type.");
        ValidateStat(ingredientCapacity, EquipmentStatDomain.InputCapacity);
        ValidateStat(exporterCapacity, EquipmentStatDomain.OutputCapacity);
    }
    protected override void OnStatsChanged() => UpdateUI();

    public IUIDataProviderApplication GetDataProvider() => CellStatusProviderAdapter.Create(this, crafterProvider);

    private CancellationTokenSource _cts;

    public struct ResourceInputData
    {
        public ResourceTypeDomain Type { get; set; }
        public int Amount { get; set; }
        public int Allocated { get; set; }
    }

    private static ResourceStorageRulesDomain.Stock ToStock(ResourceInputData input)
        => new(input.Type, input.Amount, input.Allocated);

    private void ApplyInput(int direction, ResourceStorageRulesDomain.Stock stock)
    {
        _resourceInputs[direction] = new ResourceInputData
        {
            Type = stock.Type, Amount = stock.Amount, Allocated = stock.Reserved
        };
        UpdateUI();
    }

    public override void InitializeSystem()
    {
        if (_isInitialized) { base.InitializeSystem(); return; }
        base.InitializeSystem();
        InitAccessPoint();
        _isInitialized = true;
        StartProcessing();
    }

    private void OnEnable() => StartProcessing();
    private void OnDisable() => StopProcessing();
    protected override void OnDestroy() { base.OnDestroy(); StopProcessing(); }

    private void StartProcessing()
    {
        if (!_isInitialized || !isActiveAndEnabled || _cts != null) return;
        _isActivate = true;
        _cts = new CancellationTokenSource();
        CraftAsync(_cts.Token).Forget();
    }

    private void StopProcessing()
    {
        _isActivate = false;
        _operation?.TryCancel();
        var cts = _cts;
        _cts = null;
        cts?.Cancel();
        cts?.Dispose();
    }

    private void InitAccessPoint()
    {
        foreach (var port in Ports)
            if ((port.Use & CellPortUseDomain.Input) != 0) _resourceInputs.TryAdd(port.Id, new());
    }

    public ResourceInputData GetInput(int portId) => _resourceInputs.GetValueOrDefault(portId, new());
    public ResourceInputData GetInputAt(int index)
    {
        var ids = new List<int>(_resourceInputs.Keys); ids.Sort();
        return index >= 0 && index < ids.Count ? GetInput(ids[index]) : new();
    }
    public int InputCount => _resourceInputs.Count;

    private void UpdateUI()
    {
        StatusChanged?.Invoke();
    }

    private RecipeRulesDomain.Input[] CaptureInputs()
    {
        var inputs = new RecipeRulesDomain.Input[_resourceInputs.Count];
        var i = 0;
        foreach (var pair in _resourceInputs)
        {
            var id = pair.Key;
            var value = pair.Value;
            inputs[i++] = new RecipeRulesDomain.Input(id, value.Type, value.Amount, value.Allocated);
        }
        return inputs;
    }

    private RecipeRulesDomain.Output CaptureOutput() => new(ExportResourceType, ExportStorageAmount);

    private bool TryStartOperation(out CraftingOperationApplication operation, out float seconds)
    {
        operation = null;
        seconds = 0;
        if (recipeDatabase == null || recipeDatabase.recipes == null) return false;
        var inputs = CaptureInputs();
        foreach (var definition in recipeDatabase.recipes)
        {
            if (definition == null || definition.Ingredients == null ||
                float.IsNaN(definition.CraftSecond) || float.IsInfinity(definition.CraftSecond) ||
                definition.CraftSecond < 0) continue;
            var ingredients = new RecipeRulesDomain.Ingredient[definition.Ingredients.Length];
            for (var i = 0; i < ingredients.Length; i++)
            {
                var ingredient = definition.Ingredients[i];
                ingredients[i] = new RecipeRulesDomain.Ingredient(ingredient.resourceType, ingredient.requiredAmount);
            }
            var recipe = new RecipeRulesDomain.Recipe(ingredients, definition.Result, definition.ResultAmount);
            if (!RecipeRulesDomain.TryCreatePlan(recipe, inputs, CaptureOutput(), ExporterCapacity, out var plan))
                continue;
            operation = new CraftingOperationApplication(plan);
            seconds = definition.CraftSecond;
            return true;
        }
        return false;
    }

    private void ApplyCommit(RecipeRulesDomain.Commit commit)
    {
        // この反映中には await や通知を挟まない。全入力・成果物の更新後に UI を通知する。
        foreach (var input in commit.Inputs)
        {
            _resourceInputs[input.Id] = new ResourceInputData
            { Type = input.Type, Amount = input.Amount, Allocated = input.Reserved };
        }
        ExportResourceType = commit.Result.Type;
        ExportStorageAmount = commit.Result.Amount;
    }

    private async UniTask CraftAsync(CancellationToken token)
    {
        try
        {
            while (_isActivate && !token.IsCancellationRequested)
            {
                CraftingOperationApplication operation = null;
                var seconds = 0f;
                await UniTask.WaitUntil(() => TryStartOperation(out operation, out seconds), cancellationToken: token);
                token.ThrowIfCancellationRequested();
                _operation = operation;
                ProcessTime = seconds;
                ElapsedProcessTime = 0;
                Tween tween = null;
                var finished = seconds == 0;
                var killed = false;
                try
                {
                    if (!finished)
                    {
                        tween = DOTween.To(() => ElapsedProcessTime, t => ElapsedProcessTime = t,
                                seconds, seconds).OnUpdate(UpdateUI).SetEase(Ease.Linear)
                            .OnComplete(() => finished = true).OnKill(() => killed = true);
                        // 待機を取り消してから finally で停止する。外部停止も成功として扱わない。
                        await tween.ToUniTask(TweenCancelBehaviour.CancelAwait, token);
                    }
                    token.ThrowIfCancellationRequested();
                    if (!_isActivate || !finished || this == null) continue;
                    if (operation.TryComplete(CaptureInputs(), CaptureOutput(), ExporterCapacity, out var commit))
                        ApplyCommit(commit);
                }
                finally
                {
                    operation.TryCancel();
                    if (!killed) tween?.Kill();
                    _operation = null;
                    ProcessTime = 0;
                    ElapsedProcessTime = 0;
                    if (this != null) UpdateUI();
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 無効化・削除による中断は通常終了。数量の確定は行わない。
        }
    }

    public bool AllocateStorage(int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return false;
        if (!ResourceStorageRulesDomain.TryReserve(IngredientCapacity, ToStock(inputStorage), amount,
                resourceType, out var next)) return false;
        ApplyInput(dir, next);
        return true;
    }

    public void StoreResource(int dir, int amount)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return;
        if (ResourceStorageRulesDomain.TryCommit(IngredientCapacity, ToStock(inputStorage), amount, out var next))
            ApplyInput(dir, next);
    }

    public void CancelStorage(int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!_resourceInputs.TryGetValue(dir, out var input)) return;
        if (ResourceStorageRulesDomain.TryCancel(ToStock(input), amount, resourceType, out var next))
            ApplyInput(dir, next);
    }

    public Vector3 GetPosition(int portId) => PortPosition(portId);

    public bool TryExport(int portId, int requestedAmount, out int amount, out ResourceTypeDomain type)
    {
        amount = 0;
        type = ExportResourceType;

        if (!HasPort(portId, CellPortUseDomain.Output)) return false;

        // 出力可能な量がない、または要求量がない場合はfalseを返す
        amount = ResourceStorageRulesDomain.GetExportableAmount(ExportStorageAmount, requestedAmount);
        if (amount == 0) return false;
        ExportStorageAmount -= amount;

        UpdateUI();
        return true;
    }
}
