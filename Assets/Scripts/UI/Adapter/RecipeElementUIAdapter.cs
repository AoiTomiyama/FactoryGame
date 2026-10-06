using TMPro;
using UnityEngine;

/// <summary>
/// レシピ一つ分のUIパラメータを保持するラッパークラス
/// </summary>
public class RecipeElementUIAdapter : MonoBehaviour
{
    [Tooltip("材料のUI")]
    [SerializeField] private ResourceRowUIAdapter ingredientRowUI;
    
    [Tooltip("成果物のUI")]
    [SerializeField] private ResourceRowUIAdapter resultRowUI;
    
    [Tooltip("作成にかかる時間UI")]
    [SerializeField] private TextMeshProUGUI craftTimeTextBox;
    
    [SerializeField] private ResourceDefinition resourceDatabase;
    
    /// <summary>
    /// レシピ情報に基づいてUIを生成する
    /// </summary>
    /// <param name="recipe">参照するデータ</param>
    public void CreateRecipeUI(RecipeDataDefinition recipe)
    {
        gameObject.name = recipe.RecipeName;
        craftTimeTextBox.text = $"{recipe.CraftSecond} s";
        
        // 素材UIをレシピの数だけ複製して、値を変更する
        var parent = ingredientRowUI.transform.parent;
        foreach (var ingredient in recipe.Ingredients)
        {
            var ingredientParam = Instantiate(ingredientRowUI, parent);
            var resourceInfo = resourceDatabase.GetResourceByType(ingredient.resourceType);
            
            ingredientParam.Set(resourceInfo.Icon, resourceInfo.ResourceName, ingredient.requiredAmount);
        }
        
        // 元のIngredientUIを削除
        Destroy(ingredientRowUI.gameObject);
        
        var resultInfo = resourceDatabase.GetResourceByType(recipe.Result);
        resultRowUI.Set(resultInfo.Icon, resultInfo.ResourceName, recipe.ResultAmount);
    }
}
