using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectButtonBuilder : MonoBehaviour
{
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private CellDatabaseSO cellDatabase;

    private void Start()
    {
        var list = cellDatabase.GetAllCellInfos();
        var playerCursor = FindAnyObjectByType<PlayerCursorBehaviour>();

        foreach (var cellInfo in list)
        {
            if (cellInfo.CellType == CellType.None) continue; // Noneはスキップ
            var button = Instantiate(buttonPrefab, transform);
            button.name = $"Select{cellInfo.CellType}CellButton";
            button.onClick.AddListener(() => playerCursor.SetSelectedCellType(cellInfo.CellType));
            
            var buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            buttonText.text = cellInfo.CellName;
        }
    }
}