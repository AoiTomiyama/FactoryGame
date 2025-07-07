using System;
using UnityEngine;

public class RecipeUIGenerator : MonoBehaviour
{
    [SerializeField] private RecipeDatabaseSO recipeDatabase;
    [SerializeField] private RecipeUIParam recipeUIParam;
    [SerializeField] private ResourceSO resourceDatabase;

    private void Start()
    {
        if (recipeDatabase == null)
        {
            Debug.LogError("RecipeDatabaseSO is not assigned in the inspector.");
            return;
        }

        foreach (var recipe in recipeDatabase.recipes)
        {
            CreateRecipeUI(recipe);
        }
    }

    private void CreateRecipeUI(RecipeData recipe)
    {
        var recipeUI = Instantiate(recipeUIParam, transform);
        recipeUI.name = recipe.RecipeName;
        recipeUI.CraftTimeTextBox.text = $"{recipe.CraftSecond} s";
        
        // 素材UIをレシピの数だけ複製して、値を変更する
        var parent = recipeUI.IngredientUI.transform.parent;
        foreach (var ingredient in recipe.Ingredients)
        {
            var ingredientUI = Instantiate(recipeUIParam.IngredientUI, parent);
            var resourceInfo = resourceDatabase.GetInfo(ingredient.resourceType);
            
            ingredientUI.Set(resourceInfo.Icon, resourceInfo.Name, ingredient.requiredAmount);
        }
        
        // 元のIngredientUIを削除
        Destroy(recipeUI.IngredientUI.gameObject);
        
        var resultInfo = resourceDatabase.GetInfo(recipe.Result);
        recipeUI.ResultUI.Set(resultInfo.Icon, resultInfo.Name, recipe.ResultAmount);
    }
}