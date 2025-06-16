using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CellPairingInfoSO", menuName = "Scriptable Objects/CellPairInfoSO")]
public class CellPairingInfoSO : ScriptableObject
{
    [Serializable]
    public struct CellPairingInfo
    {
        public CellType cellType;
        public GameObject fieldCellPrefab;
        public GameObject placeholderCellPrefab;
    }

    [SerializeField] private CellPairingInfo[] cellPairingInfos;
    
    public CellPairingInfo GetCellInfo(CellType cellType) => cellPairingInfos.FirstOrDefault(x => x.cellType == cellType);
}

public enum CellType
{
    None,
    Resource,
    Empty,
}