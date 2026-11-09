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
    }
}
