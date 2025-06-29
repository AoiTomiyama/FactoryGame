using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeDatabaseSO", menuName = "Scriptable Objects/RecipeDatabaseSO")]
public class RecipeDatabaseSO : ScriptableObject
{
    public RecipeData[] recipes;
}
[Serializable]
public class RecipeData
{
    [SerializeField] [Tooltip("レシピ名")]
    private string recipeName;
    
    [SerializeField] [Tooltip("必要となるリソース情報")]
    private Ingredient[] ingredients;
    
    [SerializeField] [Tooltip("出来上がる成果物")]
    private ResourceType result;
    
    [SerializeField] [Tooltip("成果物の個数")]
    private int resultAmount;
    
    public Ingredient[] Ingredients => ingredients;
    public ResourceType Result => result;
    public int ResultAmount => resultAmount;
}

[Serializable]
public struct Ingredient
{
    public ResourceType resourceType;
    public int requiredAmount;
}
