using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 素材の一行分を保持するラッパークラス
/// </summary>
public class ResourceRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameTextBox;
    [SerializeField] private TextMeshProUGUI amountTextBox;

    public void Set(Sprite image, string resourceName, int amount)
    {
        nameTextBox.text = resourceName;
        icon.sprite = image;
        amountTextBox.text = "......x" + amount;
    }
}