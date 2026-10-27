using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CellSelectButtonUIAdapter : MonoBehaviour
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image cellIcon;
    [SerializeField] private TextMeshProUGUI cellNameText;
    [SerializeField] private Button button;

    private Text _label;
    public void ApplyStyle(Font font)
    {
        if (_label == null)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(button.transform, false);
            _label = go.GetComponent<Text>();
            _label.rectTransform.anchorMin = Vector2.zero;
            _label.rectTransform.anchorMax = Vector2.one;
            _label.rectTransform.offsetMin = new Vector2(8, 4);
            _label.rectTransform.offsetMax = new Vector2(-8, -4);
        }
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        button.GetComponent<Image>().color = backgroundImage.color;
        _label.font = font; _label.fontSize = 16; _label.color = Color.white;
        _label.alignment = TextAnchor.MiddleLeft; _label.raycastTarget = false;
        _label.text = cellNameText.text;
        cellNameText.gameObject.SetActive(false);
        cellIcon.gameObject.SetActive(false);
    }

    public bool SubMenuActive { get; set; }
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
        if (_label != null) _label.text = cellName;
        button.onClick.AddListener(() => onClick?.Invoke());
    }
    
    public void SetColor(Color color)
    {
        backgroundImage.color = color;
        if (_label != null) button.GetComponent<Image>().color = color;
    }
}