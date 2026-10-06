using UnityEngine;

public class RecipeUIBuilderAdapter : MonoBehaviour
{
    [SerializeField] private RecipeDatabaseDefinition recipeDatabase;
    [SerializeField] private RecipeElementUIAdapter recipeElementUI;

    private void Start()
    {
        if (recipeDatabase == null)
        {
            Debug.LogError("RecipeDatabaseDefinition is not assigned in the inspector.");
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