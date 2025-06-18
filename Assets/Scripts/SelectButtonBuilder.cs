using UnityEngine;
using UnityEngine.UI;

public class SelectButtonBuilder : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private CellDatabaseSO cellDatabase;

    private void Start()
    {
        var list = cellDatabase.GetAllCellInfos();
        var playerCursor = FindAnyObjectByType<PlayerCursorBehaviour>();

        foreach (var cellInfo in list)
        {
            if (cellInfo.cellType == CellType.None) continue; // Noneはスキップ
            var obj = Instantiate(buttonPrefab, transform);
            obj.name = $"Select{cellInfo.cellType}CellButton";
            var button = obj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => playerCursor.SetSelectedCellType(cellInfo.cellType));
            }
            else
            {
                Debug.LogError($"SelectButton component not found on {obj.name}");
            }
        }
    }
}