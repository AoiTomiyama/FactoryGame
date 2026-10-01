using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>レシピ欄と保管状態欄が資源アイコンを表示することを Play Mode で確認する。</summary>
[InitializeOnLoad]
public static class ResourceIconVerifier
{
    private const string SessionKey = "FactoryGame.ResourceIconVerifier.Running";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    static ResourceIconVerifier() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    public static void Run()
    {
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyAsync().Forget();
    }

    private static async UniTaskVoid VerifyAsync()
    {
        try
        {
            var resources = AssetDatabase.LoadAssetAtPath<ResourceSO>(
                "Assets/Scripts/ScriptableObject/ResourceDB.asset");
            var recipes = AssetDatabase.LoadAssetAtPath<RecipeDatabaseSO>(
                "Assets/Scripts/ScriptableObject/Recipes/RecipeDB.asset");
            var recipePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Recipe.prefab");
            var storagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/StorageParamLine.prefab");
            Require(resources != null && recipes != null && recipes.recipes.Length > 0 &&
                    recipePrefab != null && storagePrefab != null, "UI assets are available");

            var stone = GetResource(resources, ResourceType.Stone);
            var wood = GetResource(resources, ResourceType.Wood);
            Require(stone.Icon != null && wood.Icon != null, "both icons resolve as sprites");

            var recipeObject = UnityEngine.Object.Instantiate(recipePrefab);
            var recipeUI = recipeObject.GetComponent<RecipeElementUI>();
            Require(recipeUI != null, "recipe prefab has RecipeElementUI");
            recipeUI.CreateRecipeUI(recipes.recipes[0]);
            await UniTask.Yield(); // CreateRecipeUI が破棄するテンプレート行を除外する。
            var stoneRow = false;
            var woodRow = false;
            foreach (var row in recipeObject.GetComponentsInChildren<ResourceRowUI>(true))
            {
                var label = GetField<TextMeshProUGUI>(row, "nameTextBox");
                var icon = GetField<Image>(row, "icon");
                if (label.text == stone.Name && icon.sprite == stone.Icon) stoneRow = true;
                if (label.text == wood.Name && icon.sprite == wood.Icon) woodRow = true;
            }
            Require(stoneRow && woodRow, "recipe rows display the stone and wood icons");

            var storageObject = UnityEngine.Object.Instantiate(storagePrefab);
            var storageUI = storageObject.GetComponent<StorageUIStatusRow>();
            Require(storageUI != null, "storage prefab has StorageUIStatusRow");
            VerifyStorage(storageUI, ResourceType.Stone, stone.Icon, stone.Name);
            VerifyStorage(storageUI, ResourceType.Wood, wood.Icon, wood.Name);
            Debug.Log($"Resource icon checks passed: recipe and storage show stone={stone.Icon.name}, wood={wood.Icon.name}.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void VerifyStorage(StorageUIStatusRow row, ResourceType type,
        Sprite expectedIcon, string expectedName)
    {
        row.RenderUIByData(new StorageElementData("Storage", 10)
        {
            Current = 1,
            ResourceType = type
        });
        var icon = GetField<Image>(row, "resourceIcon");
        var label = GetField<TextMeshProUGUI>(row, "resourceText");
        Require(icon.enabled && icon.sprite == expectedIcon && label.text == expectedName,
            $"storage row displays {type}");
    }

    private static (Sprite Icon, string Name) GetResource(ResourceSO database, ResourceType type)
    {
        // 作業ツリーで進行中の ResourceSO 名称変更と、アイコン検証を独立させる。
        var method = typeof(ResourceSO).GetMethod("GetResourceByType") ??
                     typeof(ResourceSO).GetMethod("GetInfo");
        if (method == null) throw new InvalidOperationException("Resource lookup method is missing");
        var resource = method.Invoke(database, new object[] { type });
        var infoType = resource.GetType();
        var icon = infoType.GetProperty("Icon")?.GetValue(resource) as Sprite;
        var name = (infoType.GetProperty("ResourceName") ?? infoType.GetProperty("Name"))
            ?.GetValue(resource) as string;
        return (icon, name);
    }

    private static T GetField<T>(object target, string name) where T : class
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        if (field == null) throw new InvalidOperationException($"Missing field: {name}");
        return field.GetValue(target) as T;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
