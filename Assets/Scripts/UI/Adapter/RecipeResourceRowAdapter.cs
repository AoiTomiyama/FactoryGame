using UnityEngine;
using UnityEngine.UI;

/// <summary>Recipeの資源アイコン・名称・数量を表示する行。</summary>
public sealed class RecipeResourceRowAdapter : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Text label;
    public void Render(Sprite sprite, string value, Font font)
    {
        icon.sprite = sprite;
        label.text = value;
        if (label.font == null) label.font = font;
    }
}
