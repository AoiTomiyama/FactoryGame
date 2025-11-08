using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GenerateVariantWindow : EditorWindow
{
    private GameObject _baseFieldPrefab;
    private GameObject _basePlaceholderPrefab;
    private GameObject _modelPrefab;
    private string _placeholderFilePath = "Assets/Prefabs/Cells/Placeholder";
    private string _fieldFilePath = "Assets/Prefabs/Cells/Field";

    private const string PlaceholderPrefabPrefix = "P_";
    private const string FieldPrefabPrefix = "F_";
    private const string Filter = "t:Prefab";

    [MenuItem("Tools/Prefab Variant Creator")]
    public static void OpenWindow()
    {
        GetWindow<GenerateVariantWindow>("Prefab Variant Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("プレハブバリアント作成", EditorStyles.boldLabel);

        GUILayout.Label("元Prefab");
        _basePlaceholderPrefab = (GameObject)EditorGUILayout.ObjectField(PlaceholderPrefabPrefix,
            _basePlaceholderPrefab, typeof(GameObject), false);

        _baseFieldPrefab =
            (GameObject)EditorGUILayout.ObjectField(FieldPrefabPrefix, _baseFieldPrefab, typeof(GameObject), false);

        _modelPrefab = (GameObject)EditorGUILayout.ObjectField("追加するモデル", _modelPrefab, typeof(GameObject), false);

        _placeholderFilePath = EditorGUILayout.TextField
            ("プレースホルダPrefab保存先フォルダ", _placeholderFilePath);

        _fieldFilePath = EditorGUILayout.TextField
            ("フィールドPrefab保存先フォルダ", _fieldFilePath);

        if (GUILayout.Button("元となるPrefabを検索し自動割り当て"))
        {
            var allPrefabs = AssetDatabase.FindAssets(Filter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, prefab: AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                .ToArray();

            foreach (var (path, prefab) in allPrefabs)
            {
                if (prefab.name.StartsWith(PlaceholderPrefabPrefix) && prefab.name.Contains("Empty"))
                {
                    _basePlaceholderPrefab = prefab;
                    Debug.Log("元となるプレースホルダPrefabを自動割り当て: " + path);
                }
                else if (prefab.name.StartsWith(FieldPrefabPrefix) && prefab.name.Contains("Empty"))
                {
                    _baseFieldPrefab = prefab;
                    Debug.Log("元となるフィールドPrefabを自動割り当て: " + path);
                }
            }
        }

        GUI.enabled = _baseFieldPrefab != null && _modelPrefab != null;

        if (GUILayout.Button("バリアント作成"))
        {
            CreateNewVariant(_modelPrefab, _basePlaceholderPrefab, _placeholderFilePath, PlaceholderPrefabPrefix);
            CreateNewVariant(_modelPrefab, _baseFieldPrefab, _fieldFilePath, FieldPrefabPrefix);
        }

        GUI.enabled = true;
    }


    private static void CreateNewVariant(GameObject baseModel, GameObject basePrefab, string path, string prefabPrefix)
    {
        const string ModelPrefabSuffix = "Model";
        const string CellPrefabSuffix = "Cell";
        if (!AssetDatabase.IsValidFolder(path))
        {
            Debug.LogError("指定された保存フォルダが存在しません: " + path);
            return;
        }

        var targetFolder = AssetDatabase.FindAssets(Filter, new[] { path })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();

        if (baseModel == null || basePrefab == null || path == null)
        {
            return;
        }

        // Prefabのインスタンスを生成
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(baseModel, instance.transform.Find(ModelPrefabSuffix));
        model.transform.localPosition = Vector3.zero;

        var prefabName = prefabPrefix + model.name.Replace(ModelPrefabSuffix, "");
        if (!prefabName.EndsWith(CellPrefabSuffix))
        {
            prefabName += CellPrefabSuffix;
        }

        if (!targetFolder.Contains(prefabName))
        {
            // PrefabVariantとして保存
            var savePath = Path.Combine(path, prefabName + ".prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(instance, savePath);
            Debug.Log("PrefabVariant 作成完了: " + path);
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogError($"既に同名のPrefabが存在します: {prefabName}");
        }

        // シーン上のインスタンスは不要なら削除
        DestroyImmediate(instance);
    }
}