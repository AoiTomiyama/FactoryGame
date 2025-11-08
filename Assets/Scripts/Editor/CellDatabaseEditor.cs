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
    private const string PlaceholderPrefabPrefix = "P_";
    private const string FieldPrefabPrefix = "F_";
    private const string Filter = "t:Prefab";
    
    private string _placeholderFilePath;
    private string _fieldFilePath;


    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var cellDatabase = (CellDatabaseSO)target;
        
        _placeholderFilePath = EditorGUILayout.TextField
            ("プレースホルダPrefab保存先フォルダ", _placeholderFilePath);
        
        _fieldFilePath = EditorGUILayout.TextField
            ("フィールドPrefab保存先フォルダ", _fieldFilePath);
        

        if (GUILayout.Button("Validate Cell Info"))
        {
            cellDatabase.ValidateAndBuildLookup();
        }

        if (GUILayout.Button("Auto Assign"))
        {
            AutoAssignData(cellDatabase);
        }
    }

    private void AutoAssignData(CellDatabaseSO database)
    {
        // "Assets/Prefabs" 以下の .prefab ファイルを全検索
        var placeholders = AssetDatabase.FindAssets(Filter, new[] { _placeholderFilePath });
        var fields = AssetDatabase.FindAssets(Filter, new[] { _fieldFilePath });

        // 辞書へ登録
        var fieldDict = new Dictionary<string, CellBase>();
        foreach (var guid in fields)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fileName = Path.GetFileNameWithoutExtension(path);

            if (!fileName.StartsWith(FieldPrefabPrefix)) continue;
            var key = fileName[FieldPrefabPrefix.Length..];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<CellBase>();
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

        var list = new List<CellInfo>();
        var values = (CellType[])Enum.GetValues(typeof(CellType));
        foreach (var cellType in values)
        {
            var cellTypeName = $"{cellType}Cell";
        
            if (fieldDict.TryGetValue(cellTypeName, out var fieldPrefab) &&
                placeholderDict.TryGetValue(cellTypeName, out var placeholderPrefab))
            {
                list.Add(new()
                {
                    CellName = Enum.GetName(typeof(CellType), cellType),
                    FieldCellPrefab = fieldPrefab,
                    PlaceholderCellPrefab = placeholderPrefab,
                    CellType = cellType
                });
            }
        }

        database.SetCellInfos(list);
        Debug.Log(list.Count > 0 ? "自動アサイン完了" : "未登録のセルはありません。");
        database.ValidateAndBuildLookup();
    }
}