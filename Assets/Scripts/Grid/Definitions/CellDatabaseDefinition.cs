using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CellDatabaseDefinition", menuName = "Scriptable Objects/CellDatabaseDefinition")]
public class CellDatabaseDefinition : ScriptableObject
{
    [SerializeField]
    private CellInfoDefinition[] cellPairingInfos;
    private readonly Dictionary<CellTypeDomain, CellInfoDefinition> _infoLookup = new();

    [InspectorReadOnlyAttributeAdapter] [Tooltip("ヴァリデーション済みかどうか")] [SerializeField]
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
        _infoLookup.Clear();

        var hashSet = new HashSet<CellTypeDomain>();

        foreach (var info in cellPairingInfos)
        {
            if (hashSet.Contains(info.CellTypeDomain))
            {
                Debug.LogWarning("重複する CellTypeDomain が存在します: " + info.CellTypeDomain, this);
            }

            if (info.FieldCellPrefab == null)
            {
                Debug.LogWarning($"CellTypeDomain {info.CellTypeDomain} に fieldCellPrefab が設定されていません", this);
            }

            if (info.PlaceholderCellPrefab == null)
            {
                Debug.LogWarning($"CellTypeDomain {info.CellTypeDomain} に placeholderCellPrefab が設定されていません", this);
            }

            hashSet.Add(info.CellTypeDomain);
            _infoLookup[info.CellTypeDomain] = info;
        }

        isInitialized = true;
    }

    /// <summary>
    /// 配列内の要素を取得するためのメソッド。存在しない場合は false を返す。
    /// </summary>
    public bool TryGetCellInfo(CellTypeDomain cellType, out CellInfoDefinition info)
    {
        if (!isInitialized)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(CellDatabaseDefinition)}が初期化されていません。ヴァリデーションを実行");
#endif
            ValidateAndBuildLookup();
        }

        if (_infoLookup == null || !_infoLookup.TryGetValue(cellType, out info))
        {
            info = default;
            return false;
        }

        return true;
    }

    public List<CellInfoDefinition> GetAllCellInfos()
    {
        if (!isInitialized)
        {
#if UNITY_EDITOR
            Debug.LogError($"{nameof(CellDatabaseDefinition)}が初期化されていません。ヴァリデーションを実行");
#endif
            ValidateAndBuildLookup();
        }

        return new(_infoLookup.Values);
    }

    public void SetCellInfos(IEnumerable<CellInfoDefinition> cellInfos)
    {
        cellPairingInfos = cellInfos.ToArray();
    }
}

[Serializable]
public struct CellInfoDefinition
{
    [SerializeField] private string cellName;
    [SerializeField] private CellBaseAdapter fieldCellPrefab;
    [SerializeField] private GameObject placeholderCellPrefab;
    [SerializeField] private CellTypeDomain cellType;

    public string CellName
    {
        get => cellName;
        set => cellName = value;
    }

    public CellBaseAdapter FieldCellPrefab
    {
        get => fieldCellPrefab;
        set => fieldCellPrefab = value;
    }

    public GameObject PlaceholderCellPrefab
    {
        get => placeholderCellPrefab;
        set => placeholderCellPrefab = value;
    }

    public CellTypeDomain CellTypeDomain
    {
        get => cellType;
        set => cellType = value;
    }
}