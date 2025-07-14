using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

public class CrafterCell : ConnectableCellBase, IContainable, IExportable
{
    [Header("クラフト設定")]
    [SerializeField] private int ingredientCapacity;
    [SerializeField] [InlineSO]
    private RecipeDatabaseSO recipeDatabase;

    [Header("その他の設定")]
    [SerializeField] private ExporterModule exportableModule;
    public ExporterModule ExportableModule => exportableModule;
    private readonly Dictionary<Vector3Int, (ResourceType type, int amount, int allocated)> _resourceInputs = new();
    private bool _isActivate;


    [SerializeField]
    private List<LabelName> labelNames;
    private readonly Dictionary<Label, UIStatusRowBase> _renderedUI = new();
    private Dictionary<Label, UIElementDataBase> _uiElementDataBases;
    private float _processTime;
    private float _elapsedProcessTime;
    public bool IsUIActive { get; set; }

    private enum Label
    {
        CellName,
        Location,
        LeftStorage,
        RightStorage,
        OutputStorage,
        Process,
    }

    [Serializable]
    private struct LabelName
    {
        public Label label;
        public string name;
    }

    protected override void Start()
    {
        if (ExportableModule == null)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(ExportableModule)}がnullです。");
#endif
        }

        ExportableModule.OnFilterPath += path =>
        {
            var exportDir = Vector3Int.RoundToInt((path[0].transform.position - transform.position).normalized);
            var forward = Vector3Int.RoundToInt(transform.forward);
            return exportDir == forward;
        };
        base.Start();
        InitAccessPoint();
        SetDefaultUIData();

        _isActivate = true;
        ExportableModule.OnExport += InitUI;

        StartCoroutine(CraftEnumerator());
    }

    private void SetDefaultUIData()
    {
        _uiElementDataBases = new()
        {
            { Label.CellName, new TextElementData("-", "Crafter") },
            { Label.Location, new TextElementData("-", $"({XIndex}, {ZIndex})") },
            { Label.LeftStorage, new StorageElementData("-", ingredientCapacity) },
            { Label.RightStorage, new StorageElementData("-", ingredientCapacity) },
            { Label.OutputStorage, new StorageElementData("-", ExportableModule.ExporterCapacity) },
            { Label.Process, new GaugeElementData("-", 1) }
        };

        var usedLabels = new HashSet<Label>();
        foreach (var labelName in labelNames)
        {
            if (!usedLabels.Add(labelName.label)) continue; // 重複ラベルはスキップ
            if (_uiElementDataBases.TryGetValue(labelName.label, out var data))
            {
                data.StatusName = labelName.name + ":";
            }
        }
    }

    public void InitUI()
    {
        if (!IsUIActive) return;

        foreach (var (label, data) in _uiElementDataBases)
        {
            if (data is GaugeElementData gaugeData)
            {
                switch (label)
                {
                    case Label.Process:
                        gaugeData.Current = _elapsedProcessTime;
                        gaugeData.Max = _processTime;
                        gaugeData.GaugeText = $"{_processTime - _elapsedProcessTime:F1} sec";
                        break;
                    case Label.LeftStorage:
                        gaugeData.Current = _resourceInputs[GetDirection(Directions.Left)].amount;
                        gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                        break;
                    case Label.RightStorage:
                        gaugeData.Current = _resourceInputs[GetDirection(Directions.Right)].amount;
                        gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                        break;
                    case Label.OutputStorage:
                        gaugeData.Current = ExportableModule.ExportResourceAmount;
                        gaugeData.GaugeText = $"{gaugeData.Current}/{gaugeData.Max}";
                        break;
                    default:
                        gaugeData.Current = 0;
                        gaugeData.GaugeText = "";
                        break;
                }
            }

            if (data is StorageElementData storageData)
            {
                storageData.ResourceType = label switch
                {
                    Label.LeftStorage => _resourceInputs[GetDirection(Directions.Left)].type,
                    Label.RightStorage => _resourceInputs[GetDirection(Directions.Right)].type,
                    Label.OutputStorage => ExportableModule.ExportResourceType,
                    _ => 0
                };
            }

            if (_renderedUI.TryGetValue(label, out var uiElement))
            {
                uiElement.RenderUIByData(data);
            }
            else
            {
                _renderedUI[label] = CellStatusView.Instance.CreateStatusRow(data);
            }
        }
    }

    public void ResetUI() => _renderedUI.Clear();

    protected override void SetConnectableDirections()
    {
        base.SetConnectableDirections();
        _connectableDirections = _connectableDirections.Where(dir => dir != Vector3Int.RoundToInt(-transform.forward))
            .ToArray();
    }

    private void InitAccessPoint()
    {
        // 入力は左右のみ登録する
        _resourceInputs.TryAdd(GetDirection(Directions.Right), (ResourceType.None, 0, 0));
        _resourceInputs.TryAdd(GetDirection(Directions.Left), (ResourceType.None, 0, 0));
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
            _processTime = recipe.CraftSecond;

            var tween = DOTween.To(
                    getter: () => _elapsedProcessTime,
                    setter: t => _elapsedProcessTime = t,
                    endValue: _processTime, duration: _processTime)
                .OnUpdate(InitUI)
                .SetEase(Ease.Linear);

            // クラフトが完了するまで待機
            yield return tween.WaitForCompletion();
            _elapsedProcessTime = 0f;
            InitUI();

            var result = Craft(recipe);
            var available = ExportableModule.ExporterCapacity - ExportableModule.ExportResourceAmount;
            var gainAmount = Mathf.Min(available, result);
            ExportableModule.ExportResourceType = recipe.Result;
            yield return new WaitUntil(() => ExportableModule.TryStackToExporter(gainAmount));
            InitUI();
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

    private int Craft(RecipeData recipe)
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

        return recipe.ResultAmount;
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
        InitUI();
        return allocated;
    }

    public void StoreResource(Vector3Int dir, int amount)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return;

        // 現在量に追加し、予約量を減らす。
        inputStorage.amount += amount;
        inputStorage.allocated -= amount;
        _resourceInputs[dir] = inputStorage;
        InitUI();
    }
}