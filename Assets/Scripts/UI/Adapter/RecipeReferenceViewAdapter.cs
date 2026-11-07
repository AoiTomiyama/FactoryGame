using UnityEngine;
using UnityEngine.UI;

/// <summary>定義の読取結果を行Prefabへ渡す。加工状態と購入を所有しない。</summary>
public sealed class RecipeReferenceViewAdapter
{
    private readonly Transform _root;
    private readonly Font _font;
    private readonly Text _heading;
    private readonly RecipeResourceRowAdapter _resource;
    public RecipeReferenceViewAdapter(Transform root, Font font, Text heading, RecipeResourceRowAdapter resource)
    { _root = root; _font = font; _heading = heading; _resource = resource; }
    public void Dispose()
    {
        if (_root == null) return;
        foreach (Transform child in _root) { child.gameObject.SetActive(false); Object.Destroy(child.gameObject); }
    }
    public void Render(RecipeDatabaseDefinition database, ResourceDefinition resources)
    {
        Dispose();
        foreach (var recipe in database.recipes)
        {
            var heading = Object.Instantiate(_heading, _root);
            if (heading.font == null) heading.font = _font;
            heading.text = $"{recipe.RecipeName}　加工時間 {recipe.CraftSecond} 秒";
            foreach (var ingredient in recipe.Ingredients)
                Resource(resources, ingredient.resourceType, ingredient.requiredAmount, "材料");
            Resource(resources, recipe.Result, recipe.ResultAmount, "成果物");
        }
    }
    private void Resource(ResourceDefinition resources, ResourceTypeDomain type, int amount, string kind)
    {
        var info = resources.GetResourceByType(type);
        Object.Instantiate(_resource, _root).Render(info.Icon, $"{kind}: {info.ResourceName} ×{amount}", _font);
    }
}
