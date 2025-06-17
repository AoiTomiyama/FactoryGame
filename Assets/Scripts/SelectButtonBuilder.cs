using System;
using UnityEngine;
using UnityEngine.UI;

public class SelectButtonBuilder : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private PlayerCursorBehaviour playerCursor;
    private void Start()
    {
        var buttonCount = Enum.GetValues(typeof(CellType)).Length;
        for (int i = 0; i < buttonCount; i++)
        {
            var cellType = (CellType)i;
            if (cellType is CellType.None) continue; // Noneはスキップ
            var obj = Instantiate(buttonPrefab, transform);
            obj.name = $"Select{cellType}CellButton";
            var button = obj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => playerCursor.SetSelectedCellType(cellType));
            }
            else
            {
                Debug.LogError($"SelectButton component not found on {obj.name}");
            }
        }
    }
}
