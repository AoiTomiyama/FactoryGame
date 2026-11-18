using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>一方のタブの閲覧状態を、共有の詳細と行Prefabへ描画する。</summary>
public sealed class RecipeReferenceViewAdapter : IDisposable
{
    private readonly RectTransform _root;
    private readonly HudLayoutAdapter _layout;
    private readonly RecipeReferenceCatalogApplication _catalog;
    private readonly bool _encyclopedia;
    private readonly Action _changed;
    private readonly ResourceReferenceNavigationApplication _navigation = new();
    private readonly List<GameObject> _rows = new();
    private readonly Stack<float> _historyScroll = new();
    private float _scrollPosition = 1;

    public ResourceTypeDomain CurrentResource => _navigation.Current;
    public bool IsDetail => CurrentResource != ResourceTypeDomain.None;

    public RecipeReferenceViewAdapter(RectTransform root, HudLayoutAdapter layout,
        RecipeReferenceCatalogApplication catalog, bool encyclopedia, Action changed)
    {
        _root = root; _layout = layout; _catalog = catalog; _encyclopedia = encyclopedia; _changed = changed;
        Render();
    }

    public void RememberScroll()
    {
        if (_root.gameObject.activeInHierarchy)
            _scrollPosition = Mathf.Clamp01(_layout.Body.verticalNormalizedPosition);
    }

    public void RestoreScroll()
    {
        _layout.Body.StopMovement();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_layout.Body.content);
        _layout.Body.verticalNormalizedPosition = _scrollPosition;
    }

    public void Back()
    {
        if (!_navigation.TryBack()) return;
        _scrollPosition = _historyScroll.Pop();
        Render();
        RestoreScroll();
        _changed();
    }

    private void Enter(ResourceTypeDomain resource)
    {
        if (!_navigation.TryEnter(resource)) return;
        RememberScroll();
        _historyScroll.Push(_scrollPosition);
        _scrollPosition = 1;
        Render();
        RestoreScroll();
        _changed();
    }

    private void Render()
    {
        Dispose();
        if (!IsDetail)
        {
            foreach (var type in _catalog.Resources.Where(type => _encyclopedia || _catalog.HasRecipe(type)))
                ResourceRow(type, "", true);
            return;
        }

        var selected = CurrentResource;
        Heading(_layout.Resources.GetResourceByType(selected).ResourceName);
        if (_encyclopedia)
        {
            TextRow(_catalog.TryGetUnitSalePrice(selected, out var price) ? $"売値 {price:N0} / 個" : "売却不可");
            var extractor = ResourceCellRulesDomain.ExtractorType(selected);
            if (extractor != CellTypeDomain.None)
                TextRow($"入手：{SelectButtonBuilderAdapter.PlacementLabel(extractor)}");
        }

        var methods = _catalog.MethodsFor(selected);
        foreach (var method in methods)
        {
            var info = method.Definition;
            Heading($"{info.Name}　×{info.Recipe.ResultAmount}");
            TextRow($"{string.Join("／", method.Machines.Select(MachineLabel))}\n基準加工時間 {info.Seconds:0.###} 秒");
            foreach (var ingredient in info.Recipe.Ingredients)
                ResourceRow(ingredient.Type, $" ×{ingredient.Amount}",
                    _catalog.HasRecipe(ingredient.Type) && _navigation.CanEnter(ingredient.Type));
        }
    }

    private static string MachineLabel(RecipeReferenceCatalogApplication.Machine machine)
    {
        var label = SelectButtonBuilderAdapter.PlacementLabel(machine.Type);
        if (machine.InputCapacityNeeded > 0) label += $"（入力容量 {machine.InputCapacityNeeded} 以上）";
        if (machine.OutputCapacityNeeded > 0) label += $"（出力容量 {machine.OutputCapacityNeeded} 以上）";
        return label;
    }

    private void Heading(string label) => Label(_layout.RecipeHeading, label);
    private void TextRow(string label) => Label(_layout.TextRow, label);
    private void Label(Text prefab, string value)
    {
        var text = Object.Instantiate(prefab, _root);
        _rows.Add(text.gameObject);
        if (text.font == null) text.font = _layout.Font;
        text.text = value;
    }

    private void ResourceRow(ResourceTypeDomain type, string quantity, bool canEnter)
    {
        var resource = _layout.Resources.GetResourceByType(type);
        var row = Object.Instantiate(_layout.ResourceRow, _root);
        _rows.Add(row.gameObject);
        row.name = $"Reference_{type}";
        row.Render(resource.Icon, resource.ResourceName + quantity + (canEnter ? "  ›" : ""), _layout.Font);
        if (canEnter)
        {
            row.Bind(() => Enter(type));
            _layout.StyleButton(row.Link);
        }
    }

    public void Dispose()
    {
        foreach (var row in _rows)
            if (row != null) { row.SetActive(false); Object.Destroy(row); }
        _rows.Clear();
    }
}
