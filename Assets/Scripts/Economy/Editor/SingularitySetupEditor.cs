using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>採用済み初期配置の資産とMainScene配線を更新する。</summary>
public static class SingularitySetupEditor
{
    private const string Folder = "Assets/Scripts/ScriptableObject/";
    public static void Run()
    {
        var economy = AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>(Folder + "CurrencyEconomy.asset");
        if (economy == null)
        {
            economy = ScriptableObject.CreateInstance<CurrencyEconomyDefinition>();
            AssetDatabase.CreateAsset(economy, Folder + "CurrencyEconomy.asset");
        }
        var prefabPath = "Assets/Prefabs/Field/General/F_SingularityCell.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<SingularityCellAdapter>(prefabPath);
        if (prefab == null)
        {
            var template = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Field/General/F_EmptyCell.prefab");
            try
            {
                UnityEngine.Object.DestroyImmediate(template.GetComponent<EmptyCellAdapter>());
                template.AddComponent<SingularityCellAdapter>();
                template.name = "F_SingularityCell";
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>());
                orb.name = "Singularity";
                orb.transform.SetParent(template.transform.GetChild(0), false);
                orb.transform.localPosition = Vector3.up * 0.9f;
                orb.transform.localScale = Vector3.one * 0.8f;
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(0.08f, 0.015f, 0.15f);
                AssetDatabase.CreateAsset(material, Folder + "Singularity.mat");
                orb.GetComponent<Renderer>().sharedMaterial = material;
                prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath).GetComponent<SingularityCellAdapter>();
            }
            finally { PrefabUtility.UnloadPrefabContents(template); }
        }
        var layout = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>(Folder + "SingularityStart.asset");
        if (layout == null)
        {
            layout = ScriptableObject.CreateInstance<SingularityStartDefinition>();
            AssetDatabase.CreateAsset(layout, Folder + "SingularityStart.asset");
        }
        Set(layout, "economy", economy);
        Set(layout, "singularity", prefab);
        Set(layout, "wood", AssetDatabase.LoadAssetAtPath<ResourceCellAdapter>("Assets/Prefabs/Field/General/F_ResourceWoodCell.prefab"));
        Set(layout, "empty", AssetDatabase.LoadAssetAtPath<EmptyCellAdapter>("Assets/Prefabs/Field/General/F_EmptyCell.prefab"));
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
        Set(generator, "initialLayout", layout);
        var size = new SerializedObject(generator).FindProperty("gridSize").intValue;
        layout.Validate(size);
        var grid = UnityEngine.Object.FindAnyObjectByType<GridFieldDatabaseAdapter>();
        var center = size / 2;
        for (var z = center - 4; z <= center; z++)
        {
            var old = grid.transform.GetChild(center).GetChild(z);
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(layout.GetInitialPrefab(size, center, z), old.parent);
            replacement.transform.position = old.position;
            replacement.name = old.name;
            replacement.transform.SetSiblingIndex(z);
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Singularity setup passed: assets and MainScene initial layout saved.");
    }
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
