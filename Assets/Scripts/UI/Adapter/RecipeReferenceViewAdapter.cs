using UnityEngine;
using UnityEngine.UI;

/// <summary>レシピ定義を参照表示する。加工の判定・開始・選択は行わない。</summary>
public sealed class RecipeReferenceViewAdapter
{
    private readonly Transform _root;
    private readonly Font _font;
    public RecipeReferenceViewAdapter(Transform root, Font font) { _root = root; _font = font; }
    public void Render(RecipeDatabaseDefinition database, ResourceDefinition resources)
    {
        foreach (Transform child in _root) { child.gameObject.SetActive(false); Object.Destroy(child.gameObject); }
        foreach (var recipe in database.recipes)
        {
            MakeText(_root, $"{recipe.RecipeName}　加工時間 {recipe.CraftSecond} 秒", 18, 40);
            foreach (var ingredient in recipe.Ingredients)
                MakeRecipeResource(resources, ingredient.resourceType, ingredient.requiredAmount, "材料");
            MakeRecipeResource(resources, recipe.Result, recipe.ResultAmount, "成果物");
        }
    }
    private void MakeRecipeResource(ResourceDefinition resources, ResourceTypeDomain type, int amount, string label)
    {
        var info = resources.GetResourceByType(type);
        var row = new GameObject("RecipeResource", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(_root, false);
        row.GetComponent<LayoutElement>().preferredHeight = 36;
        row.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(row.transform, false);
        icon.GetComponent<Image>().sprite = info.Icon;
        icon.GetComponent<Image>().preserveAspect = true;
        icon.GetComponent<LayoutElement>().minWidth = 32;
        icon.GetComponent<LayoutElement>().preferredWidth = 32;
        icon.GetComponent<LayoutElement>().flexibleWidth = 0;
        icon.GetComponent<LayoutElement>().minHeight = 32;
        icon.GetComponent<LayoutElement>().preferredHeight = 32;
        MakeText(row.transform, $"{label}: {info.ResourceName} ×{amount}", 16, 32).GetComponent<LayoutElement>().flexibleWidth = 1;
    }
    private Text MakeText(Transform parent, string value, int size, int height)
        => HudUIAdapter.MakeText(_font, parent, value, size, height);
}
