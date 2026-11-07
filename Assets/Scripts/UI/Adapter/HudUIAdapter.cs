using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>HUDの表示生成と用途別の共通色。ゲーム状態は保持しない。</summary>
public static class HudUIAdapter
{
    public static readonly Color Background = new(0.07f, 0.09f, 0.12f, 0.96f);
    public static readonly Color ButtonColor = new(0.22f, 0.34f, 0.43f);
    public static readonly Color Selected = new(0.24f, 0.43f, 0.53f);
    public static readonly Color Idle = new(0.14f, 0.21f, 0.27f);
    public static Text MakeText(Font font, Transform parent, string value, int size, int height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.text = value; text.color = Color.white;
        text.fontStyle = size >= 19 ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        go.GetComponent<LayoutElement>().minHeight = height;
        return text;
    }

    public static Button MakeButton(Font font, Transform parent, string label, Action click, int height)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = HudUIAdapter.ButtonColor;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var button = go.GetComponent<Button>(); button.onClick.AddListener(() => click());
        var text = MakeText(font, go.transform, label, 16, height);
        Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(8, 2); text.rectTransform.offsetMax = new Vector2(-8, -2);
        return button;
    }
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
