using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "CellDatabaseSO", menuName = "Scriptable Objects/CellDatabaseSO")]
public class CellDatabaseSO : ScriptableObject
{
    [SerializeField] private CellInfo[] cellPairingInfos;
    private Dictionary<CellType, CellInfo> _infoLookup;

    [InspectorReadOnly] [Tooltip("ヴァリデーション済みかどうか")] [SerializeField]
    private bool isInitialized;

    private void OnValidate()
    {
        isInitialized = false;
    }

    private void OnEnable()
    {
        if (isInitialized) return;
        ValidateAndBuildLookup();
    }

    public void AutoAssignData()
    {
        const string PlaceholderPrefabPrefix = "P_";
        const string PlaceholderFileName = "Placeholder";
        const string FieldPrefabPrefix = "F_";
        const string FieldFileName = "Field";
        const string PrefabFolderName = "Assets/Prefabs/";
        const string Filter = "t:Prefab";
        
        // "Assets/Prefabs" 以下の .prefab ファイルを全検索
        var placeholders = AssetDatabase.FindAssets(Filter, new[] { PrefabFolderName + PlaceholderFileName });
        var fields = AssetDatabase.FindAssets(Filter, new[] { PrefabFolderName + FieldFileName });

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

        // 配列に割り当て
        for (int i = 0; i < Enum.GetNames(typeof(CellType)).Length; i++)
        {
            var cellTypeName = Enum.GetName(typeof(CellType), i) + "Cell";

            if (fieldDict.TryGetValue(cellTypeName, out var fieldPrefab))
            {
                cellPairingInfos[i].fieldCellPrefab = fieldPrefab;
            }

            if (placeholderDict.TryGetValue(cellTypeName, out var placeholderPrefab))
            {
                cellPairingInfos[i].placeholderCellPrefab = placeholderPrefab;
            }

            cellPairingInfos[i].cellType = (CellType)i;
        }

        Debug.Log("AutoAssignData 完了");
    }

    public void ValidateAndBuildLookup()
    {
        _infoLookup = new Dictionary<CellType, CellInfo>();

        var hashSet = new HashSet<CellType>();

        foreach (var info in cellPairingInfos)
        {
            if (hashSet.Contains(info.cellType))
            {
                Debug.LogWarning("重複する CellType が存在します: " + info.cellType, this);
            }

            if (info.fieldCellPrefab == null)
            {
                Debug.LogWarning($"CellType {info.cellType} に fieldCellPrefab が設定されていません", this);
            }

            if (info.placeholderCellPrefab == null)
            {
                Debug.LogWarning($"CellType {info.cellType} に placeholderCellPrefab が設定されていません", this);
            }

            hashSet.Add(info.cellType);
            _infoLookup[info.cellType] = info;
        }

        isInitialized = true;
    }

    /// <summary>
    /// 配列内の用要素を取得するためのメソッド。存在しない場合は false を返す。
    /// </summary>
    public bool TryGetCellInfo(CellType cellType, out CellInfo info)
    {
        if (!isInitialized)
        {
            Debug.LogError($"{nameof(CellDatabaseSO)}が初期化されていません。ヴァリデーションを実行");
            ValidateAndBuildLookup();
        }

        if (_infoLookup == null || !_infoLookup.TryGetValue(cellType, out info))
        {
            info = default;
            return false;
        }

        return true;
    }

    public List<CellInfo> GetAllCellInfos()
    {
        if (!isInitialized)
        {
            Debug.LogError($"{nameof(CellDatabaseSO)}が初期化されていません。ヴァリデーションを実行");
            ValidateAndBuildLookup();
        }

        return new List<CellInfo>(_infoLookup.Values);
    }
}

[Serializable]
public struct CellInfo
{
    public GameObject fieldCellPrefab;
    public GameObject placeholderCellPrefab;
    public CellType cellType;
}

public enum CellType
{
    None,
    Empty,
    ResourceWood,
    ResourceStone,
    ResourceIron,
    ExtractorStone,
    ExtractorWood,
    Storage,
    ItemPipe,
    ExportPipe,
}