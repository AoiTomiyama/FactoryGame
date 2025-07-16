using System;
using UnityEngine;

public class SelectButtonBuilder : MonoBehaviour
{
    [SerializeField] private CellSelectButtonUI buttonPrefab;
    [SerializeField] private CellDatabaseSO cellDatabase;
    [SerializeField] private SubMenuInfo[] subMenuInfos;

    [Serializable]
    public struct SubMenuInfo
    {
        [SerializeField] private CellType cellType;
        [SerializeField] private CellSelectSubMenuUI subMenu;

        public CellType CellType => cellType;

        public CellSelectSubMenuUI SubMenu => subMenu;
    }

    private void Start()
    {
        var list = cellDatabase.GetAllCellInfos();
        var playerCursor = FindAnyObjectByType<PlayerCursorBehaviour>();

        foreach (var cellInfo in list)
        {
            if (cellInfo.CellType == CellType.None) continue; // Noneはスキップ

            // 割り当てる情報のみを下層のラッパクラスに受け渡す
            // アイコンの設定は未定
            var buttonParam = Instantiate(buttonPrefab, transform);
            buttonParam.name = $"Select{cellInfo.CellType}CellButton";

            // 対応するサブメニューを探す
            foreach (var subMenuInfo in subMenuInfos)
            {
                if (subMenuInfo.CellType != cellInfo.CellType) continue;

                // サブメニューの設定
                Debug.Log("SubMenu");
                var subMenu = Instantiate(subMenuInfo.SubMenu, buttonParam.transform);
                subMenu.Set(cellInfo.CellType, playerCursor);
                buttonParam.Set(null, cellInfo.CellName,
                    () => subMenu.gameObject.SetActive(!subMenu.gameObject.activeSelf));
                subMenu.gameObject.SetActive(false);
            }

            buttonParam.Set(null, cellInfo.CellName, () => playerCursor.SetSelectedCellType(cellInfo.CellType));
        }
    }
}