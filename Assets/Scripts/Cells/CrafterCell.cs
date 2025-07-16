using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class CrafterCell : ConnectableCellBase, IContainable, IExportable, IDataProvidable
{
    [Header("クラフト設定")]
    [SerializeField] private int ingredientCapacity;
    [SerializeField] private Directions importDirection;
    [SerializeField] private Directions exportDirection;
    [SerializeField] [InlineSO]
    private RecipeDatabaseSO recipeDatabase;

    [Header("その他の設定")]
    [SerializeField] private ExporterModule exportableModule;
    [SerializeField] private CrafterProvider crafterProvider;

    private readonly Dictionary<Vector3Int, ResourceInputData> _resourceInputs = new();
    private readonly HashSet<Vector3Int> _exportableDirections = new();
    private bool _isActivate;

    public float ProcessTime { get; private set; }
    public float ElapsedProcessTime { get; private set; }
    public bool IsUIActive { private get; set; }
    public ExporterModule ExportableModule => exportableModule;
    public int IngredientCapacity => ingredientCapacity;
    public IUIDataProvider GetDataProvider() => crafterProvider;

    public struct ResourceInputData
    {
        public ResourceType Type { get; set; }
        public int Amount { get; set; }
        public int Allocated { get; set; }
    }

    public override void InitializeSystem()
    {
        ModuleSetUp();
        base.InitializeSystem();
        InitAccessPoint();
        _isActivate = true;
        StartCoroutine(CraftEnumerator());
    }

    private void ModuleSetUp()
    {
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ExportableModule)}がnullです。");
#endif
        }

        ExportableModule.OnFilterPath += path =>
        {
            var dir = (path[0].transform.position - transform.position).ToCardinalDirection();
            return _exportableDirections.Any(exportableDirection => dir == exportableDirection);
        };

        ExportableModule.OnExport += UpdateUI;
    }

    private void InitAccessPoint()
    {
        var values = (Directions[])Enum.GetValues(typeof(Directions));
        foreach (var direction in values)
        {
            if (HasFlag(importDirection, direction)) _resourceInputs.TryAdd(DirectionEnumToVector(direction), new());
            if (HasFlag(exportDirection, direction)) _exportableDirections.Add(DirectionEnumToVector(direction));
        }
    }

    public ResourceInputData GetInput(Directions direction) =>
        _resourceInputs.GetValueOrDefault(DirectionEnumToVector(direction), new());

    private void UpdateUI()
    {
        if (!IsUIActive) return;
        CellStatusView.Instance.UpdateUI();
    }

    private IEnumerator CraftEnumerator()
    {
        while (_isActivate)
        {
            // 容量に空きが出るまで待機
            yield return new WaitUntil(() => ExportableModule.ExportResourceAmount < ExportableModule.ExporterCapacity);

            // 作成可能なレシピが見つかるまで待機
            RecipeData recipe = null;
            yield return new WaitUntil(() => HasAvailableRecipe(out recipe));
            ProcessTime = recipe.CraftSecond;

            var tween = DOTween.To(
                    getter: () => ElapsedProcessTime,
                    setter: t => ElapsedProcessTime = t,
                    endValue: ProcessTime, duration: ProcessTime)
                .OnUpdate(UpdateUI)
                .SetEase(Ease.Linear);

            // クラフトが完了するまで待機
            yield return tween.WaitForCompletion();
            ElapsedProcessTime = 0f;
            UpdateUI();

            var result = Craft(recipe);
            var available = ExportableModule.ExporterCapacity - ExportableModule.ExportResourceAmount;
            var gainAmount = Mathf.Min(available, result);
            ExportableModule.ExportResourceType = recipe.Result;
            yield return new WaitUntil(() => ExportableModule.TryStackToExporter(gainAmount));
            UpdateUI();
        }
    }

    /// <summary>
    /// クラフト素材が全て揃っていて、クラフト可能かの判定を行う
    /// </summary>
    /// <returns>素材が全て揃っているかどうか</returns>
    private bool HasAvailableRecipe(out RecipeData foundRecipeData)
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
    private bool CheckRecipe(RecipeData recipe)
    {
        // 生成後が容量オーバーする場合はfalse
        if (ExportableModule.ExportResourceAmount + recipe.ResultAmount > ExportableModule.ExporterCapacity)
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

    private int Craft(RecipeData recipe)
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

    public int AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return 0;

        // 初めてのリソース追加
        if (inputStorage.Type == ResourceType.None)
        {
            inputStorage.Type = resourceType;
        }

        // 設定済みのリソースタイプと異なる場合、追加しない
        if (inputStorage.Type != resourceType) return 0;

        // 既に容量限界に達している場合は0を返す
        // 入れようとしている値が空き容量を越えている場合は空き容量を返す
        // そうでない場合は指定された量を予約する
        var available = IngredientCapacity - inputStorage.Amount - inputStorage.Allocated;
        var allocated = Mathf.Min(available, amount);
        inputStorage.Allocated += allocated;
        _resourceInputs[dir] = inputStorage;
        UpdateUI();
        return allocated;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return;

        // 現在量に追加し、予約量を減らす。
        inputStorage.Amount += amount;
        inputStorage.Allocated -= amount;
        _resourceInputs[dir] = inputStorage;
        UpdateUI();
    }
}