using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>一方のタブの閲覧状態を、共有の詳細と行Prefabへ描画する。</summary>
public sealed class RecipeReferenceViewAdapter : IDisposable
{
    private readonly RectTransform _list;
    private readonly ScrollRect _detailScroll;
    private readonly HudLayoutAdapter _layout;
    private readonly RecipeReferenceCatalogApplication _catalog;
    private readonly bool _encyclopedia;
    private readonly Action _changed;
    private readonly ResourceReferenceNavigationApplication _navigation = new();
    private readonly Dictionary<ResourceTypeDomain, RecipeResourceRowAdapter> _listRows = new();
    private readonly List<GameObject> _detailRows = new();
    private float _scrollPosition = 1;

    public ResourceTypeDomain CurrentResource => _navigation.Current;
    public bool IsDetail => CurrentResource != ResourceTypeDomain.None;
    public RectTransform DetailContent { get; }

    public RecipeReferenceViewAdapter(RectTransform list, RectTransform details, ScrollRect detailScroll,
        HudLayoutAdapter layout, RecipeReferenceCatalogApplication catalog, bool encyclopedia, Action changed)
    {
        _list = list; DetailContent = details; _detailScroll = detailScroll;
        _layout = layout; _catalog = catalog; _encyclopedia = encyclopedia; _changed = changed;
        // 詳細を開いても一覧を描き直さず、行の位置とスクロールを保つ。
        foreach (var type in _catalog.Resources.Where(type => _encyclopedia || _catalog.HasRecipe(type)))
            _listRows.Add(type, ResourceRow(_list, type, "", () => Select(type)));
    }

    public void RememberScroll()
    {
        if (_list.gameObject.activeInHierarchy)
            _scrollPosition = Mathf.Clamp01(_layout.Body.verticalNormalizedPosition);
    }

    public void RestoreScroll()
    {
        _layout.Body.StopMovement();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_layout.Body.content);
        _layout.Body.verticalNormalizedPosition = _scrollPosition;
    }

    private void Select(ResourceTypeDomain resource)
    {
        _navigation.Select(resource);
        RefreshDetail();
    }

    private void Follow(ResourceTypeDomain resource)
    {
        if (_navigation.TryFollow(resource)) RefreshDetail();
    }

    public void ClearSelection() => Select(ResourceTypeDomain.None);

    private void RefreshDetail()
    {
        ClearDetails();
        if (IsDetail) RenderDetail();
        foreach (var pair in _listRows)
            _layout.StyleButton(pair.Value.Link, pair.Key == CurrentResource);
        _changed();
        if (DetailContent.gameObject.activeInHierarchy)
        {
            _detailScroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(DetailContent);
            _detailScroll.verticalNormalizedPosition = 1;
        }
    }

    private void RenderDetail()
    {
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
            {
                var type = ingredient.Type;
                Action navigate = _catalog.HasRecipe(type) && _navigation.CanFollow(type) ? () => Follow(type) : null;
                _detailRows.Add(ResourceRow(DetailContent, type, $" ×{ingredient.Amount}", navigate).gameObject);
            }
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
        var text = Object.Instantiate(prefab, DetailContent);
        _detailRows.Add(text.gameObject);
        if (text.font == null) text.font = _layout.Font;
        text.text = value;
    }

    private RecipeResourceRowAdapter ResourceRow(Transform root, ResourceTypeDomain type, string quantity, Action navigate)
    {
        var resource = _layout.Resources.GetResourceByType(type);
        var row = Object.Instantiate(_layout.ResourceRow, root);
        row.name = $"Reference_{type}";
        row.Render(resource.Icon, resource.ResourceName + quantity + (navigate != null ? "  ›" : ""), _layout.Font);
        if (navigate != null)
        {
            row.Bind(() => navigate());
            _layout.StyleButton(row.Link);
        }
        return row;
    }

    private void ClearDetails()
    {
        foreach (var row in _detailRows)
            if (row != null) { row.SetActive(false); Object.Destroy(row); }
        _detailRows.Clear();
    }

    public void Dispose()
    {
        ClearDetails();
        foreach (var row in _listRows.Values)
            if (row != null) { row.gameObject.SetActive(false); Object.Destroy(row.gameObject); }
        _listRows.Clear();
    }
}
