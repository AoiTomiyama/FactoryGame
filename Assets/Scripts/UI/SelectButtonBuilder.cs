using UnityEngine;

public class SelectButtonBuilder : MonoBehaviour
{
    [SerializeField] private CellSelectButtonUI buttonPrefab;
    [SerializeField] private CellDatabaseSO cellDatabase;

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
            buttonParam.Set(null, cellInfo.CellName, () => playerCursor.SetSelectedCellType(cellInfo.CellType));
        }
    }
}