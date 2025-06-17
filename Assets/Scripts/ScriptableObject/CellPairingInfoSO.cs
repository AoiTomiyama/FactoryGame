using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CellPairingInfoSO", menuName = "Scriptable Objects/CellPairingInfoSO")]
public class CellPairingInfoSo : ScriptableObject
{
    [SerializeField] private CellInfo[] cellPairingInfos;
    private Dictionary<CellType, CellInfo> _infoLookup;

    // 初期化、またはエディタでの変更時に重複チェックと辞書構築
    private void OnEnable()
    {
        ValidateAndBuildLookup();
    }

    private void OnValidate()
    {
        ValidateAndBuildLookup();
    }

    private void ValidateAndBuildLookup()
    {
        _infoLookup = new Dictionary<CellType, CellInfo>();

        foreach (var info in cellPairingInfos)
        {
            if (info.fieldCellPrefab == null)
            {
                Debug.LogWarning($"CellType {info.cellType} に fieldCellPrefab が設定されていません", this);
            }

            if (info.placeholderCellPrefab == null)
            {
                Debug.LogWarning($"CellType {info.cellType} に placeholderCellPrefab が設定されていません", this);
            }

            _infoLookup[info.cellType] = info;
        }
    }

    /// <summary>
    /// 配列内の用要素を取得するためのメソッド。存在しない場合は false を返す。
    /// </summary>
    public bool TryGetCellInfo(CellType cellType, out CellInfo info)
    {
        if (_infoLookup == null || !_infoLookup.TryGetValue(cellType, out info))
        {
            info = default;
            return false;
        }

        return true;
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
    ResourceWood,
    ResourceStone,
    ResourceIron,
    Empty,
}