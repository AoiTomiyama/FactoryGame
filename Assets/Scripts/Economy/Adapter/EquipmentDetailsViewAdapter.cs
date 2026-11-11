using UnityEngine;
using UnityEngine.UI;

/// <summary>設備情報の保存済み表示部品。状態の読取と購入はPresenterが担当する。</summary>
public sealed class EquipmentDetailsViewAdapter
{
    private readonly Text _equipment, _feedback;
    private readonly GameObject _group;
    public Text Heading { get; }
    public Button UpgradeButton { get; }
    public Transform UpgradeRoot => _group.transform;
    public bool Interactable => UpgradeButton.interactable;
    public string EquipmentText { get => _equipment.text; set => _equipment.text = value; }
    public string Feedback { get => _feedback.text; set { _feedback.text = value; _feedback.gameObject.SetActive(!string.IsNullOrEmpty(value)); } }
    public EquipmentDetailsViewAdapter(Text heading, GameObject group, Text equipment, Button button, Text feedback)
    { Heading = heading; _group = group; _equipment = equipment; UpgradeButton = button; _feedback = feedback; }
    public void UpdateLayout()
    {
        // 状態行が増えても見出しと強化操作の順を保つ。
        Heading.transform.SetAsFirstSibling();
        _group.transform.SetAsLastSibling();
        foreach (var text in new[] { Heading, _equipment, _feedback })
            text.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(text.GetComponent<LayoutElement>().minHeight, text.preferredHeight);
        if (!_group.activeSelf || !UpgradeButton.gameObject.activeSelf) return;
        // 説明領域を残りの高さまで伸ばし、購入操作を詳細枠の下段へ揃える。
        // 長文は自然な高さを優先し、既存のスクロールで末尾へ到達できるようにする。
        var content = (RectTransform)_group.transform.parent;
        var viewport = (RectTransform)content.parent;
        var outer = content.GetComponent<VerticalLayoutGroup>();
        var inner = _group.GetComponent<VerticalLayoutGroup>();
        var footer = LayoutUtility.GetPreferredHeight((RectTransform)UpgradeButton.transform);
        if (_feedback.gameObject.activeSelf)
            footer += LayoutUtility.GetPreferredHeight(_feedback.rectTransform) + inner.spacing;
        var available = viewport.rect.height - outer.padding.vertical - outer.spacing
            - LayoutUtility.GetPreferredHeight(Heading.rectTransform) - inner.padding.vertical - inner.spacing - footer;
        var description = _equipment.GetComponent<LayoutElement>();
        description.preferredHeight = Mathf.Max(description.preferredHeight, available);

    }
}
