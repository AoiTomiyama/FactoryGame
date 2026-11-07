using System;
using UnityEngine;
using UnityEngine.UI;

public class CellSelectButtonUIAdapter : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button button;

    [SerializeField] private Text _label;
    public void ApplyStyle(Font font)
    {
        if (_label != null && _label.font == null) _label.font = font;
    }

    public bool SubMenuActive { get; set; }
    /// <summary>
    /// セルのアイコンと名前、クリック時のアクションを設定する
    /// </summary>
    public void Set(string cellName, Action onClick)
    {
        if (_label != null) _label.text = cellName;
        button.onClick.AddListener(() => onClick?.Invoke());
    }
    
    public void SetColor(Color color)
    {
        backgroundImage.color = color;
        if (_label != null) button.GetComponent<Image>().color = color;
    }
}
