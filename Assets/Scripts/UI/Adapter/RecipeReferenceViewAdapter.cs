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
    private sealed class Detail
    {
        public readonly List<GameObject> Rows = new();
        public readonly List<(ResourceTypeDomain Type, RecipeResourceRowAdapter Row)> Links = new();

        public void SetVisible(bool visible)
        {
            foreach (var row in Rows) row.SetActive(visible);
        }
    }

    private readonly Dictionary<ResourceTypeDomain, Detail> _details = new();
    private Detail _visibleDetail;
    private ResourceTypeDomain _highlighted;
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
        {
            _listRows.Add(type, ResourceRow(_list, type, "", () => Select(type)));
            _details.Add(type, BuildDetail(type));
        }
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
        _details.TryGetValue(CurrentResource, out var next);
        if (_visibleDetail != next) _visibleDetail?.SetVisible(false);
        if (next != null)
        {
            foreach (var link in next.Links)
                link.Row.SetLinkVisible(_navigation.CanFollow(link.Type));
            if (_visibleDetail != next) next.SetVisible(true);
        }
        _visibleDetail = next;
        if (_highlighted != CurrentResource)
        {
            if (_listRows.TryGetValue(_highlighted, out var previousRow)) _layout.StyleButton(previousRow.Link);
            if (_listRows.TryGetValue(CurrentResource, out var currentRow)) _layout.StyleButton(currentRow.Link, true);
            _highlighted = CurrentResource;
        }
        _changed();
        if (DetailContent.gameObject.activeInHierarchy)
        {
            _detailScroll.StopMovement();
            LayoutRebuilder.ForceRebuildLayoutImmediate(DetailContent);
            _detailScroll.verticalNormalizedPosition = 1;
        }
    }

    private Detail BuildDetail(ResourceTypeDomain selected)
    {
        var detail = new Detail();
        var resourceName = _layout.Resources.GetResourceByType(selected).ResourceName;
        var methods = _catalog.MethodsFor(selected);
        if (_encyclopedia)
        {
            Label(detail, _layout.RecipeHeading, resourceName);
            Label(detail, _layout.TextRow, _catalog.TryGetUnitSalePrice(selected, out var price) ? $"売値 {price:N0} / 個" : "売却不可");
            var extractor = ResourceCellRulesDomain.ExtractorType(selected);
            if (extractor != CellTypeDomain.None)
                Label(detail, _layout.TextRow, $"入手：{SelectButtonBuilderAdapter.PlacementLabel(extractor)}");
        }

        foreach (var method in methods)
        {
            var info = method.Definition;
            var output = _encyclopedia ? $"できあがり {info.Recipe.ResultAmount}個" : $"{resourceName} ×{info.Recipe.ResultAmount}";
            Label(detail, _layout.RecipeHeading, output);
            foreach (var ingredient in info.Recipe.Ingredients)
            {
                var type = ingredient.Type;
                Action navigate = _catalog.HasRecipe(type) ? () => Follow(type) : null;
                var row = ResourceRow(DetailContent, type, $" ×{ingredient.Amount}", navigate);
                detail.Rows.Add(row.gameObject);
                if (navigate != null) detail.Links.Add((type, row));
            }
            Label(detail, _layout.TextRow, $"{info.Seconds:0.###} 秒");
        }
        // 起動時に全資源の詳細を作り、閲覧中は同じ行を表示し直す。
        detail.SetVisible(false);
        return detail;
    }

    private void Label(Detail detail, Text prefab, string value)
    {
        var text = Object.Instantiate(prefab, DetailContent);
        detail.Rows.Add(text.gameObject);
        if (text.font == null) text.font = _layout.Font;
        text.text = value;
    }

    private RecipeResourceRowAdapter ResourceRow(Transform root, ResourceTypeDomain type, string quantity, Action navigate)
    {
        var resource = _layout.Resources.GetResourceByType(type);
        var row = Object.Instantiate(_layout.ResourceRow, root);
        row.name = $"Reference_{type}";
        row.Render(resource.Icon, resource.ResourceName + quantity, _layout.Font);
        if (navigate != null)
        {
            row.Bind(() => navigate());
            _layout.StyleButton(row.Link);
        }
        return row;
    }

    public void Dispose()
    {
        foreach (var detail in _details.Values)
        foreach (var row in detail.Rows)
            if (row != null) { row.SetActive(false); Object.Destroy(row); }
        _details.Clear();
        _visibleDetail = null;
        foreach (var row in _listRows.Values)
            if (row != null) { row.gameObject.SetActive(false); Object.Destroy(row.gameObject); }
        _listRows.Clear();
    }
}
