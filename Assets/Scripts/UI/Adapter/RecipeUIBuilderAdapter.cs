using UnityEngine;

public class RecipeUIBuilderAdapter : MonoBehaviour
{
    [SerializeField] private RecipeDatabaseDefinition recipeDatabase;
    [SerializeField] private RecipeElementUIAdapter recipeElementUI;
    public RecipeDatabaseDefinition Database => recipeDatabase;
    public ResourceDefinition Resources => recipeElementUI.Resources;

    public void RetireLegacyPage()
    {
        // 定義だけを新しい参照ページへ渡し、旧ページの重複生成を止める。
        enabled = false;
        transform.parent.gameObject.SetActive(false);
    }

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
