using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class CrafterCell : ConnectableCellBase, IExportable, IContainable
{
    [Header("クラフト設定")]
    [SerializeField] private int ingredientCapacity;
    [SerializeField] private int craftedCapacity;
    [SerializeField] [InlineSO]
    private RecipeDatabaseSO recipeDatabase;

    [Header("UI設定")]
    [SerializeField] private Image processBar;
    [SerializeField] private Image leftAmountBar;
    [SerializeField] private Image rightAmountBar;
    [SerializeField] private Image craftedAmountBar;

    // 入力関連
    private readonly Dictionary<Vector3Int, (ResourceType type, int amount, int allocated)> _resourceInputs = new();
    private Vector3Int[] InputDirections { get; set; }

    // 出力関連
    public HashSet<(int length, List<ConnectableCellBase> path)> ExportPaths { get; private set; } = new();
    public ResourceType ExportResourceType { get; private set; }
    private int _craftedAmount;
    private ConnectableCellBase _output;
    private bool _isCraftable;

    protected override void Start()
    {
        _isCraftable = true;
        base.Start();
        InitAccessPoint();
        StartCoroutine(CraftEnumerator());
    }

    protected override void SetConnectableDirections()
    {
        base.SetConnectableDirections();
        _connectableDirections = _connectableDirections.Where(dir => dir != Vector3Int.RoundToInt(-transform.forward))
            .ToArray();
    }

    private void InitAccessPoint()
    {
        // 入力は左右のみ登録する
        InputDirections = new[]
        {
            Vector3Int.RoundToInt(transform.right),
            Vector3Int.RoundToInt(-transform.right)
        };
        foreach (var dir in InputDirections)
        {
            _resourceInputs.Add(dir, (ResourceType.None, 0, 0));
        }
    }

    private IEnumerator CraftEnumerator()
    {
        while (_isCraftable)
        {
            // ストレージに保存できる容量があるか確認
            if (_craftedAmount < craftedCapacity)
            {
                RecipeData recipe = null;
                yield return new WaitUntil(() => HasAvailableRecipe(out recipe));
                processBar.fillAmount = 0f;

            var tween = processBar
                .DOFillAmount(1f, recipe.CraftSecond)
                .SetEase(Ease.Linear);

                yield return tween.WaitForCompletion();
                Craft(recipe);
            }
            else
            {
                // 容量上限に達した場合はスペースが空くまで待機
                yield return new WaitUntil(TryExportResource);
                UpdateUI();
            }
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
        if (_craftedAmount + recipe.ResultAmount > craftedCapacity) return false;
        var usedKeys = new HashSet<Vector3Int>();

        // レシピの要件を調べる
        foreach (var ingredient in recipe.Ingredients)
        {
            var hasIngredient = false;
            foreach (var key in _resourceInputs.Keys)
            {
                if (usedKeys.Contains(key)) continue;

                var input = _resourceInputs[key];
                if (input.type != ingredient.resourceType ||
                    input.amount < ingredient.requiredAmount)
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

    private void Craft(RecipeData recipe)
    {
        var usedKeys = new HashSet<Vector3Int>();
        foreach (var ingredient in recipe.Ingredients)
        {
            foreach (var key in _resourceInputs.Keys)
            {
                if (usedKeys.Contains(key)) continue;

                var input = _resourceInputs[key];
                if (input.type != ingredient.resourceType ||
                    input.amount < ingredient.requiredAmount)
                {
                    // 要件を満たさない場合、スキップする
                    continue;
                }

                // 一度レシピの要件を満たした入力は除外する
                input.amount -= ingredient.requiredAmount;
                _resourceInputs[key] = input;
                usedKeys.Add(key);
                break;
            }
        }

        _craftedAmount += recipe.ResultAmount;
        ExportResourceType = recipe.Result;

        _ = TryExportResource();
        UpdateUI();
    }

    private bool TryExportResource()
    {
        // ネットワークを介してターゲットにリソースを送る
        var isAllowedToTransfer = PipelineNetworkManager.Instance.TryExport(
            exporter: this,
            exportAmount: _craftedAmount,
            exportBeginPos: transform.position,
            allocated: out var allocatedAmount);

        // 輸出が確立されたら現在のリソース値から予約量を減らす
        if (isAllowedToTransfer) _craftedAmount -= allocatedAmount;

        return isAllowedToTransfer;
    }

    public int AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return 0;

        // 初めてのリソース追加
        if (inputStorage.type == ResourceType.None)
        {
            inputStorage.type = resourceType;
        }

        // 設定済みのリソースタイプと異なる場合、追加しない
        if (inputStorage.type != resourceType) return 0;

        // 既に容量限界に達している場合は0を返す
        // 入れようとしている値が空き容量を越えている場合は空き容量を返す
        // そうでない場合は指定された量を予約する
        var available = ingredientCapacity - inputStorage.amount - inputStorage.allocated;
        var allocated = Mathf.Min(available, amount);
        inputStorage.allocated += allocated;
        _resourceInputs[dir] = inputStorage;
        return allocated;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return;

        // 現在量に追加し、予約量を減らす。
        inputStorage.amount += amount;
        inputStorage.allocated -= amount;
        _resourceInputs[dir] = inputStorage;
        UpdateUI();
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
        leftAmountBar.fillAmount = (float)_resourceInputs[InputDirections[1]].amount / ingredientCapacity;
        rightAmountBar.fillAmount = (float)_resourceInputs[InputDirections[0]].amount / ingredientCapacity;
        craftedAmountBar.fillAmount = (float)_craftedAmount / craftedCapacity;
    }

    public void AddPath(int length, List<ConnectableCellBase> path)
    {
        // 既に同じパスが存在する場合は追加しない
        if (ExportPaths.Any(p => p.path.SequenceEqual(path))) return;
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("パスが空です。パスを追加できません。");
            return;
        }

        if (path.Last() is not IContainable)
        {
            Debug.LogWarning("パスの終点がストレージセルではありません。パスを追加できません。");
            return;
        }

        var exportDir = Vector3Int.RoundToInt((path[0].transform.position - transform.position).normalized);
        var forward = Vector3Int.RoundToInt(transform.forward);
        if (exportDir != forward) return;

        ExportPaths.Add((length, path));
        ExportPaths = ExportPaths.OrderBy(p => p.length).ToHashSet();
    }
}