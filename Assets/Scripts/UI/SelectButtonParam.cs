using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectButtonParam : MonoBehaviour
{
    [SerializeField] private Image cellIcon;
    [SerializeField] private TextMeshProUGUI cellNameText;
    [SerializeField] private Button button;

    /// <summary>
    /// セルのアイコンと名前、クリック時のアクションを設定する
    /// </summary>
    public void Set(Image icon, string cellName, Action onClick)
    {
        if (icon != null)
        {
            cellIcon.sprite = icon.sprite;
        }
        cellNameText.text = cellName;
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}