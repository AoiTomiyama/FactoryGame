using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GenerateVariantWindowEditor : EditorWindow
{
    private GameObject _baseFieldPrefab;
    private GameObject _basePlaceholderPrefab;
    private GameObject _modelPrefab;
    private string _placeholderFilePath = "Assets/Prefabs/Placeholder/General";
    private string _fieldFilePath = "Assets/Prefabs/Field/General";

    private const string PlaceholderPrefabPrefix = "P_";
    private const string FieldPrefabPrefix = "F_";
    private const string Filter = "t:Prefab";

    [MenuItem("Tools/Prefab Variant Creator")]
    public static void OpenWindow()
    {
        GetWindow<GenerateVariantWindowEditor>("Prefab Variant Creator");
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

        GUI.enabled = _baseFieldPrefab != null && _basePlaceholderPrefab != null && _modelPrefab != null;

        if (GUILayout.Button("バリアント作成"))
        {
            CreateNewVariant(_modelPrefab, _basePlaceholderPrefab, _placeholderFilePath, PlaceholderPrefabPrefix);
            CreateNewVariant(_modelPrefab, _baseFieldPrefab, _fieldFilePath, FieldPrefabPrefix);
        }

        GUI.enabled = true;
    }


    private static GameObject CreateNewVariant(GameObject baseModel, GameObject basePrefab, string path, string prefabPrefix)
    {
        const string ModelPrefabSuffix = "Model";
        const string CellPrefabSuffix = "Cell";
        if (!AssetDatabase.IsValidFolder(path))
        {
            Debug.LogError("指定された保存フォルダが存在しません: " + path);
            return null;
        }

        var targetFolder = AssetDatabase.FindAssets(Filter, new[] { path })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();

        if (baseModel == null || basePrefab == null || path == null)
        {
            return null;
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
        }
        else
        {
            Debug.LogError($"既に同名のPrefabが存在します: {prefabName}");
        }

        // シーン上のインスタンスは不要なら削除
        DestroyImmediate(instance);
        var createdPath = Path.Combine(path, prefabName + ".prefab").Replace("\\", "/");
        return AssetDatabase.LoadAssetAtPath<GameObject>(createdPath);
    }

    [Serializable] private sealed class VariantManifest { public VariantJob[] jobs; }
    [Serializable] private sealed class VariantJob
    {
        public string model, fieldBase, placeholderBase, fieldTemplate, fieldFolder, placeholderFolder;
        public float modelY;
        public IntOverride[] integers;
    }
    [Serializable] private sealed class IntOverride { public string property; public int value; }

    /// <summary>既存Creatorと同じ構成でまとめて作成する。入力JSONは作業用ファイルで、設定資産にはしない。</summary>
    public static void CreateFromManifest()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "-prefabVariants");
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing -prefabVariants manifest.");
        var manifest = JsonUtility.FromJson<VariantManifest>(File.ReadAllText(args[index + 1]));
        if (manifest?.jobs == null) throw new ArgumentException("Missing variant jobs.");
        // 全出力を先に確認し、既存Variantの設定を作成処理で上書きしない。
        foreach (var job in manifest.jobs)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(job.model);
            if (model == null) throw new ArgumentException("Missing model: " + job.model);
            var name = model.name.Replace("Model", "");
            if (!name.EndsWith("Cell")) name += "Cell";
            if (File.Exists(Path.Combine(job.fieldFolder, FieldPrefabPrefix + name + ".prefab")) ||
                File.Exists(Path.Combine(job.placeholderFolder, PlaceholderPrefabPrefix + name + ".prefab")))
                throw new InvalidOperationException("Variant already exists: " + name);
        }
        foreach (var job in manifest.jobs)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(job.model);
            var fieldBase = AssetDatabase.LoadAssetAtPath<GameObject>(job.fieldBase);
            var placeholderBase = AssetDatabase.LoadAssetAtPath<GameObject>(job.placeholderBase);
            var template = AssetDatabase.LoadAssetAtPath<CellBaseAdapter>(job.fieldTemplate);
            if (model == null || fieldBase == null || placeholderBase == null || template == null)
                throw new ArgumentException("Missing prefab input: " + job.model);
            var field = CreateNewVariant(model, fieldBase, job.fieldFolder, FieldPrefabPrefix);
            var placeholder = CreateNewVariant(model, placeholderBase, job.placeholderFolder, PlaceholderPrefabPrefix);
            ConfigureCreatedVariant(field, template, job);
            ConfigureCreatedVariant(placeholder, null, job);
        }
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureCreatedVariant(GameObject prefab, CellBaseAdapter template, VariantJob job)
    {
        if (prefab == null) throw new InvalidOperationException("Variant was not created.");
        var path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.Find("Model").GetChild(0).localPosition = new Vector3(0, job.modelY, 0);
            if (template != null)
            {
                // 基底の白セルから空セルの動作だけを交換し、モデルとPrefabの継承を保持する。
                foreach (var old in root.GetComponents<CellBaseAdapter>()) DestroyImmediate(old);
                var component = (CellBaseAdapter)root.AddComponent(template.GetType());
                EditorUtility.CopySerialized(template, component);
                var serialized = new SerializedObject(component);
                foreach (var entry in job.integers ?? Array.Empty<IntOverride>())
                {
                    var property = serialized.FindProperty(entry.property);
                    if (property == null) throw new ArgumentException("Missing serialized property: " + entry.property);
                    property.intValue = entry.value;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}