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
    [SerializeField] private DirectionsDomain importDirection;
    [SerializeField] private DirectionsDomain exportDirection;
    [SerializeField] private int exporterCapacity;
    [SerializeField] [InlineSOAttributeAdapter]
    private RecipeDatabaseDefinition recipeDatabase;

    [Header("その他の設定")]
    [SerializeField] private CrafterProviderDefinition crafterProvider;

    private readonly Dictionary<Vector3Int, ResourceInputData> _resourceInputs = new();
    private readonly HashSet<Vector3Int> _exportableDirections = new();
    private bool _isActivate;
    private bool _isInitialized;
    private CraftingOperationApplication _operation;

    public int ExportStorageAmount { get; private set; }
    public float ProcessTime { get; private set; }
    public float ElapsedProcessTime { get; private set; }
    public bool IsUIActive { private get; set; }

    public ResourceTypeDomain ExportResourceType { get; private set; }

    public int IngredientCapacity => ingredientCapacity;

    public int ExporterCapacity => exporterCapacity;

    public IUIDataProviderApplication GetDataProvider() => crafterProvider;

    private CancellationTokenSource _cts;

    public struct ResourceInputData
    {
        public ResourceTypeDomain Type { get; set; }
        public int Amount { get; set; }
        public int Allocated { get; set; }
    }

    private static ResourceStorageRulesDomain.Stock ToStock(ResourceInputData input)
        => new(input.Type, input.Amount, input.Allocated);

    private void ApplyInput(Vector3Int direction, ResourceStorageRulesDomain.Stock stock)
    {
        _resourceInputs[direction] = new ResourceInputData
        {
            Type = stock.Type, Amount = stock.Amount, Allocated = stock.Reserved
        };
        UpdateUI();
    }

    public override void InitializeSystem()
    {
        if (_isInitialized) return;
        base.InitializeSystem();
        InitAccessPoint();
        _isInitialized = true;
        StartProcessing();
    }

    private void OnEnable() => StartProcessing();
    private void OnDisable() => StopProcessing();
    private void OnDestroy() => StopProcessing();

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
        var values = (DirectionsDomain[])Enum.GetValues(typeof(DirectionsDomain));
        foreach (var direction in values)
        {
            if (HasFlag(importDirection, direction)) _resourceInputs.TryAdd(DirectionEnumToVector(direction), new());
            if (HasFlag(exportDirection, direction)) _exportableDirections.Add(DirectionEnumToVector(direction));
        }
    }

    public ResourceInputData GetInput(DirectionsDomain direction) =>
        _resourceInputs.GetValueOrDefault(DirectionEnumToVector(direction), new());

    private void UpdateUI()
    {
        if (!IsUIActive) return;
        CellStatusViewAdapter.Instance.UpdateUI();
    }

    private RecipeRulesDomain.Input[] CaptureInputs()
    {
        var inputs = new RecipeRulesDomain.Input[_resourceInputs.Count];
        var i = 0;
        foreach (var pair in _resourceInputs)
        {
            // World の4方向を単純な ID に変換する。Domain は Unity の座標を知らない。
            var id = pair.Key.x + pair.Key.z * 2;
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
            var direction = input.Id switch
            {
                1 => Vector3Int.right, -1 => Vector3Int.left,
                2 => Vector3Int.forward, -2 => Vector3Int.back,
                _ => throw new InvalidOperationException("Unknown crafting input ID.")
            };
            _resourceInputs[direction] = new ResourceInputData
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

    public bool AllocateStorage(Vector3Int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return false;
        if (!ResourceStorageRulesDomain.TryReserve(IngredientCapacity, ToStock(inputStorage), amount,
                resourceType, out var next)) return false;
        ApplyInput(dir, next);
        return true;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return;
        if (ResourceStorageRulesDomain.TryCommit(IngredientCapacity, ToStock(inputStorage), amount, out var next))
            ApplyInput(dir, next);
    }

    public void CancelStorage(Vector3Int dir, int amount, ResourceTypeDomain resourceType)
    {
        if (!_resourceInputs.TryGetValue(dir, out var input)) return;
        if (ResourceStorageRulesDomain.TryCancel(ToStock(input), amount, resourceType, out var next))
            ApplyInput(dir, next);
    }

    public Vector3 GetPosition() => transform.position;

    public bool TryExport(Vector3 from, int requestedAmount, out int amount, out ResourceTypeDomain type)
    {
        amount = 0;
        type = ExportResourceType;

        if (!_exportableDirections.Contains((from - transform.position).ToCardinalDirection())) return false;

        // 出力可能な量がない、または要求量がない場合はfalseを返す
        amount = ResourceStorageRulesDomain.GetExportableAmount(ExportStorageAmount, requestedAmount);
        if (amount == 0) return false;
        ExportStorageAmount -= amount;

        UpdateUI();
        return true;
    }
}
