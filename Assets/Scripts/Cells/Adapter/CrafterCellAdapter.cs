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
        base.InitializeSystem();
        InitAccessPoint();
        _isActivate = true;
        _cts = new();
        CraftAsync(_cts.Token).Forget();
    }

    private void OnDestroy()
    {
        _isActivate = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
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

    private async UniTask CraftAsync(CancellationToken token)
    {
        while (_isActivate && !token.IsCancellationRequested)
        {
            // 容量に空きが出るまで待機
            await UniTask.WaitUntil(() => ExportStorageAmount < ExporterCapacity, cancellationToken: token);

            // 作成可能なレシピが見つかるまで待機
            RecipeDataDefinition recipe = null;
            await UniTask.WaitUntil(() => HasAvailableRecipe(out recipe), cancellationToken: token);
            ProcessTime = recipe.CraftSecond;


            Tween tween = null;
            try
            {
                tween = DOTween.To(
                        getter: () => ElapsedProcessTime,
                        setter: t => ElapsedProcessTime = t,
                        endValue: ProcessTime, duration: ProcessTime)
                    .OnUpdate(UpdateUI)
                    .SetEase(Ease.Linear);

                // クラフトが完了するまで待機（キャンセル対応）
                await tween.ToUniTask(cancellationToken: token);
            }
            finally
            {
                // キャンセルされた場合はTweenを破棄
                tween?.Kill();
            }

            // クラフトが完了するまで待機
            ElapsedProcessTime = 0f;
            UpdateUI();

            var result = Craft(recipe);
            var available = ExporterCapacity - ExportStorageAmount;
            var gainAmount = Mathf.Min(available, result);
            ExportResourceType = recipe.Result;
            ExportStorageAmount += gainAmount;

            UpdateUI();
        }
    }

    /// <summary>
    /// クラフト素材が全て揃っていて、クラフト可能かの判定を行う
    /// </summary>
    /// <returns>素材が全て揃っているかどうか</returns>
    private bool HasAvailableRecipe(out RecipeDataDefinition foundRecipeData)
    {
        foreach (var recipe in recipeDatabase.recipes)
        {
            if (!CheckRecipe(recipe)) continue;
            foundRecipeData = recipe;
            return true;
        }

        foundRecipeData = null;
        return false;
    }

    /// <summary>
    /// 現在のストレージを参照して、レシピが有効かを調べる
    /// </summary>
    /// <param name="recipe">チェックするレシピ</param>
    /// <returns>有効かどうか</returns>
    private bool CheckRecipe(RecipeDataDefinition recipe)
    {
        // 生成後が容量オーバーする場合はfalse
        if (ExportStorageAmount + recipe.ResultAmount > ExporterCapacity)
            return false;
        var usedKeys = new HashSet<Vector3Int>();

        // レシピの要件を調べる
        foreach (var ingredient in recipe.Ingredients)
        {
            var hasIngredient = false;
            foreach (var key in _resourceInputs.Keys)
            {
                if (usedKeys.Contains(key)) continue;

                var input = _resourceInputs[key];
                if (input.Type != ingredient.resourceType ||
                    input.Amount < ingredient.requiredAmount)
                {
                    // 要件を満たさない場合、スキップする
                    continue;
                }

                // 一度レシピの要件を満たした入力は除外する
                usedKeys.Add(key);
                hasIngredient = true;
                break;
            }

            // 一つでも要件が満たされなかったら検索を打ち切る
            if (!hasIngredient) return false;
        }

        return true;
    }

    private int Craft(RecipeDataDefinition recipe)
    {
        var usedKeys = new HashSet<Vector3Int>();
        foreach (var ingredient in recipe.Ingredients)
        {
            foreach (var key in _resourceInputs.Keys)
            {
                if (usedKeys.Contains(key)) continue;

                var input = _resourceInputs[key];
                if (input.Type != ingredient.resourceType ||
                    input.Amount < ingredient.requiredAmount)
                {
                    // 要件を満たさない場合、スキップする
                    continue;
                }

                // 一度レシピの要件を満たした入力は除外する
                input.Amount -= ingredient.requiredAmount;
                _resourceInputs[key] = input;
                usedKeys.Add(key);
                break;
            }
        }

        return recipe.ResultAmount;
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
