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

    // 辞書は保存されないため、準備済み状態も保存データから復元しない。
    [NonSerialized]
    private bool isInitialized;

    private void OnValidate()
    {
        InvalidateLookup();
    }

    private void OnEnable()
    {
        // 再有効化を含め、設定配列を正本として実行時キャッシュを構築する。
        ValidateAndBuildLookup();
    }

    /// <summary>
    /// 保存済みデータのヴァリデーション処理。
    /// </summary>
    public void ValidateAndBuildLookup()
    {
        InvalidateLookup();

        var hashSet = new HashSet<CellTypeDomain>();

        foreach (var info in cellPairingInfos ?? Array.Empty<CellInfoDefinition>())
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
        EnsureLookup();

        if (_infoLookup == null || !_infoLookup.TryGetValue(cellType, out info))
        {
            info = default;
            return false;
        }

        return true;
    }

    public List<CellInfoDefinition> GetAllCellInfos()
    {
        EnsureLookup();

        return new(_infoLookup.Values);
    }

    public void SetCellInfos(IEnumerable<CellInfoDefinition> cellInfos)
    {
        cellPairingInfos = cellInfos.ToArray();
        // 呼出し側が手動Validateを省いても、次回取得で新しい定義だけを返す。
        InvalidateLookup();
    }

    private void InvalidateLookup()
    {
        isInitialized = false;
        _infoLookup.Clear();
    }

    private void EnsureLookup()
    {
        if (!isInitialized) ValidateAndBuildLookup();
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
