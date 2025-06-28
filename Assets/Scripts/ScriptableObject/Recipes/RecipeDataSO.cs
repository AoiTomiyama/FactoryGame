using System;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeDataSO", menuName = "Scriptable Objects/RecipeDataSO")]
public class RecipeDataSO : ScriptableObject
{
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