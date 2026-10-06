using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeDatabaseDefinition", menuName = "Scriptable Objects/RecipeDatabaseDefinition")]
public class RecipeDatabaseDefinition : ScriptableObject
{
    public RecipeDataDefinition[] recipes;
}
[Serializable]
public class RecipeDataDefinition
{
    [SerializeField] [Tooltip("レシピ名")]
    private string recipeName;
    
    [SerializeField] [Tooltip("必要となるリソース情報")]
    private IngredientDefinition[] ingredients;

    [SerializeField] [Tooltip("作成にかかる時間")]
    private float craftSecond;
    
    [SerializeField] [Tooltip("出来上がる成果物")]
    private ResourceTypeDomain result;
    
    [SerializeField] [Tooltip("成果物の個数")]
    private int resultAmount;
    
    public IngredientDefinition[] Ingredients => ingredients;
    public ResourceTypeDomain Result => result;
    public int ResultAmount => resultAmount;

    public float CraftSecond => craftSecond;

    public string RecipeName => recipeName;
}

[Serializable]
public struct IngredientDefinition
{
    public ResourceTypeDomain resourceType;
    public int requiredAmount;
}
