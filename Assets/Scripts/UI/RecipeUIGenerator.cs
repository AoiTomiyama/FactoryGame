using UnityEngine;

public class RecipeUIGenerator : MonoBehaviour
{
    [SerializeField] private RecipeDatabaseSO recipeDatabase;
    [SerializeField] private RecipeUIParam recipeUIParam;

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
            var recipeUI = Instantiate(recipeUIParam, transform);
            recipeUI.CreateRecipeUI(recipe);
        }
    }

}