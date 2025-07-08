using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// 保存済みデータのヴァリデーション処理。
    /// </summary>
    public void ValidateAndBuildLookup()
    {
        _infoLookup = new();

        var hashSet = new HashSet<CellType>();

        foreach (var info in cellPairingInfos)
        {
            if (hashSet.Contains(info.CellType))
            {
                Debug.LogWarning("重複する CellType が存在します: " + info.CellType, this);
            }

            if (info.FieldCellPrefab == null)
            {
                Debug.LogWarning($"CellType {info.CellType} に fieldCellPrefab が設定されていません", this);
            }

            if (info.PlaceholderCellPrefab == null)
            {
                Debug.LogWarning($"CellType {info.CellType} に placeholderCellPrefab が設定されていません", this);
            }

            hashSet.Add(info.CellType);
            _infoLookup[info.CellType] = info;
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

        return new(_infoLookup.Values);
    }

    public void SetCellInfos(IEnumerable<CellInfo> cellInfos)
    {
        cellPairingInfos = cellInfos.ToArray();
    }
}

[Serializable]
public struct CellInfo
{
    [SerializeField] private string cellName;
    [SerializeField] private GameObject fieldCellPrefab;
    [SerializeField] private GameObject placeholderCellPrefab;
    [SerializeField] private CellType cellType;

    public string CellName => cellName;

    public GameObject FieldCellPrefab
    {
        get => fieldCellPrefab;
        set => fieldCellPrefab = value;
    }

    public GameObject PlaceholderCellPrefab
    {
        get => placeholderCellPrefab;
        set => placeholderCellPrefab = value;
    }

    public CellType CellType
    {
        get => cellType;
        set => cellType = value;
    }
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
    Crafter,
}