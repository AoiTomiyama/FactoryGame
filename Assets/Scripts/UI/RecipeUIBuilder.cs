using UnityEngine;

public class RecipeUIBuilder : MonoBehaviour
{
    [SerializeField] private RecipeDatabaseSO recipeDatabase;
    [SerializeField] private RecipeElementUI recipeElementUI;

    private void Start()
    {
        if (recipeDatabase == null)
        {
            Debug.LogError("RecipeDatabaseSO is not assigned in the inspector.");
            return;
        }

        foreach (var recipe in recipeDatabase.recipes)
        {
            // レシピ情報をラッパクラスに渡してUIを生成
            var recipeUI = Instantiate(recipeElementUI, transform);
            recipeUI.CreateRecipeUI(recipe);
        }
    }

}