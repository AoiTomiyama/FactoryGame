using UnityEngine;
using UnityEngine.UI;

/// <summary>Recipeの資源アイコン・名称・数量を表示する行。</summary>
public sealed class RecipeResourceRowAdapter : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Text label;
    [SerializeField] private Button link;
    [SerializeField] private Image background;
    private string _label;
    public Button Link => link;
    public Image Icon => icon;

    public void Bind(UnityEngine.Events.UnityAction navigate)
    {
        link.onClick.AddListener(navigate);
        SetLinkVisible(true);
    }

    public void SetLinkVisible(bool visible)
    {
        if (!visible) background.enabled = false;
        link.enabled = visible;
        if (visible) background.enabled = true;
        label.text = visible ? _label + "  ›" : _label;
    }
    public void Render(Sprite sprite, string value, Font font)
    {
        icon.sprite = sprite;
        _label = label.text = value;
        if (label.font == null) label.font = font;
    }
}
