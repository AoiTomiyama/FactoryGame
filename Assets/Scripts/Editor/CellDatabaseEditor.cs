using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CellDatabaseSO))]
public class CellDatabaseEditor : Editor
{
    // ファイルのパスやPrefabのプレフィックス
    private const string PrefabFolderPath = "Assets/Prefabs/";
    private const string PlaceholderPrefabPrefix = "P_";
    private const string PlaceholderFilePath = PrefabFolderPath + "Placeholder";
    private const string FieldPrefabPrefix = "F_";
    private const string FieldFilePath = PrefabFolderPath + "Field";
    private const string Filter = "t:Prefab";

    [SerializeField] private GameObject basePrefab;
    [SerializeField] private GameObject modelPrefab;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var cellDatabase = (CellDatabaseSO)target;

        if (GUILayout.Button("Validate Cell Info"))
        {
            cellDatabase.ValidateAndBuildLookup();
        }

        if (GUILayout.Button("Auto Assign"))
        {
            AutoAssignData(cellDatabase);
        }
    }

    private static void AutoAssignData(CellDatabaseSO database)
    {
        // "Assets/Prefabs" 以下の .prefab ファイルを全検索
        var placeholders = AssetDatabase.FindAssets(Filter, new[] { PlaceholderFilePath });
        var fields = AssetDatabase.FindAssets(Filter, new[] { FieldFilePath });

        // 辞書へ登録
        var fieldDict = new Dictionary<string, GameObject>();
        foreach (var guid in fields)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fileName = Path.GetFileNameWithoutExtension(path);

            if (!fileName.StartsWith(FieldPrefabPrefix)) continue;
            var key = fileName[FieldPrefabPrefix.Length..];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            fieldDict[key] = prefab;
        }

        var placeholderDict = new Dictionary<string, GameObject>();
        foreach (var guid in placeholders)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fileName = Path.GetFileNameWithoutExtension(path);

            if (!fileName.StartsWith(PlaceholderPrefabPrefix)) continue;
            var key = fileName[PlaceholderPrefabPrefix.Length..];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            placeholderDict[key] = prefab;
        }

        var length = Enum.GetNames(typeof(CellType)).Length;
        var list = new CellInfo[length];
        // 配列に割り当て
        for (int i = 0; i < length; i++)
        {
            var cellTypeName = Enum.GetName(typeof(CellType), i) + "Cell";

            if (fieldDict.TryGetValue(cellTypeName, out var fieldPrefab))
            {
                list[i].fieldCellPrefab = fieldPrefab;
            }

            if (placeholderDict.TryGetValue(cellTypeName, out var placeholderPrefab))
            {
                list[i].placeholderCellPrefab = placeholderPrefab;
            }

            list[i].cellType = (CellType)i;
        }

        database.SetCellInfos(list);
        database.ValidateAndBuildLookup();

        Debug.Log("AutoAssignData 完了");
    }

}