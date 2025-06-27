using System;
using System.Collections.Generic;
using System.IO;
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
        var list = new List<CellInfo>();
        
        for (int i = 0; i < length; i++)
        {
            var cellType = (CellType)i;
            var cellTypeName = $"{cellType}Cell";
        
            if (fieldDict.TryGetValue(cellTypeName, out var fieldPrefab) &&
                placeholderDict.TryGetValue(cellTypeName, out var placeholderPrefab) &&
                !database.TryGetCellInfo(cellType, out _))
            {
                list.Add(new CellInfo
                {
                    fieldCellPrefab = fieldPrefab,
                    placeholderCellPrefab = placeholderPrefab,
                    cellType = cellType
                });
            }
        }

        database.SetCellInfos(list);
        Debug.Log(list.Count > 0 ? "自動アサイン完了" : "未登録のセルはありません。");
        database.ValidateAndBuildLookup();
    }
}