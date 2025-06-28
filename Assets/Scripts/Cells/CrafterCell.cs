using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CrafterCell : ConnectableCellBase, IExportable, IContainable
{
    [Header("クラフト設定")]
    [SerializeField] private float craftingSecond;
    [SerializeField] private int ingredientCapacity;
    [SerializeField] private int craftedCapacity;
    [SerializeField] [InlineSO]
    private RecipeDataSO recipeData;

    [Header("UI設定")]
    [SerializeField] private Image processBar;
    [SerializeField] private Image leftAmountBar;
    [SerializeField] private Image rightAmountBar;
    [SerializeField] private Image craftedAmountBar;

    // 入力関連
    private readonly Dictionary<Vector3Int, (ResourceType type, int amount, int allocated)> _resourceInputs = new();
    public Vector3Int[] InputDirections { get; private set; }

    // 出力関連
    public HashSet<(int length, List<ConnectableCellBase> path)> ExportPaths { get; private set; } = new();
    public ResourceType ExportResourceType { get; }
    private int _craftedAmount;
    private ConnectableCellBase _output;

    protected override void Start()
    {
        base.Start();
        InitAccessPoint();
    }

    protected override void SetConnectableDirections()
    {
        base.SetConnectableDirections();
        _connectableDirections = _connectableDirections.Where(dir => dir != Vector3Int.RoundToInt(-transform.forward)).ToArray();
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

    public int AllocateStorage(Vector3Int dir, int amount, ResourceType resourceType)
    {
        if (!_resourceInputs.TryGetValue(dir, out var inputStorage)) return 0;

        // 初めてのリソース追加
        if (inputStorage.type == ResourceType.None)
        {
            // レシピ内に指定のタイプがあるか
            if (recipeData.Ingredients.All(ing => ing.resourceType != resourceType)) return 0;

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
    }

    public void AddPath(int length, List<ConnectableCellBase> path)
    {
        // 既に同じパスが存在する場合は追加しない
        if (ExportPaths.Any(p => p.path.SequenceEqual(path))) return;
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("パスが空です。パスを追加できません。", this);
            return;
        }

        if (path.Last() is not IContainable)
        {
            Debug.LogWarning("パスの終点がストレージセルではありません。パスを追加できません。", this);
            return;
        }

        ExportPaths.Add((length, path));
        ExportPaths = ExportPaths.OrderBy(p => p.length).ToHashSet();
    }
}