using UnityEngine;
using UnityEngine.UI;

/// <summary>Recipeの資源アイコン・名称・数量を表示する行。</summary>
public sealed class RecipeResourceRowAdapter : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Text label;
    [SerializeField] private Button link;
    [SerializeField] private Image background;
    public Button Link => link;
    public Image Icon => icon;

    public void Bind(UnityEngine.Events.UnityAction navigate)
    {
        background.enabled = true;
        link.enabled = true;
        link.onClick.AddListener(navigate);
    }
    public void Render(Sprite sprite, string value, Font font)
    {
        icon.sprite = sprite;
        label.text = value;
        if (label.font == null) label.font = font;
    }
}
