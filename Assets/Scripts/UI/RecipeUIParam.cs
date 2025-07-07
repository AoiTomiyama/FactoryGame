using TMPro;
using UnityEngine;

/// <summary>
/// レシピ一つ分のUIパラメータを保持するラッパークラス
/// </summary>
public class RecipeUIParam : MonoBehaviour
{
    [Tooltip("材料のUI")]
    [SerializeField] private ResourceUIParam ingredientUI;
    
    [Tooltip("成果物のUI")]
    [SerializeField] private ResourceUIParam resultUI;
    
    [SerializeField] private TextMeshProUGUI craftTimeTextBox;

    public ResourceUIParam IngredientUI => ingredientUI;

    public ResourceUIParam ResultUI => resultUI;

    public TextMeshProUGUI CraftTimeTextBox => craftTimeTextBox;
}
