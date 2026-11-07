using UnityEngine;
using UnityEngine.UI;

/// <summary>保存済みHUDの参照と画面幅への追従。状態の読取と購入は担当しない。</summary>
public sealed class HudLayoutAdapter : MonoBehaviour
{
    public ScrollRect Body;
    public Button Close;
    public RectTransform Navigation;
    public Button[] Tabs;
    public Color SelectedTabColor = new(.24f, .43f, .53f), IdleTabColor = new(.14f, .21f, .27f);
    public RectTransform[] Pages;
    public RectTransform Summary;
    public Text[] WalletItems;
    public Text Feedback, Placement;
    public Text EquipmentHeading, EquipmentText, EquipmentFeedback;
    public GameObject EquipmentGroup;
    public Button Upgrade;
    public SelectButtonBuilderAdapter PlacementBuilder;
    public RecipeDatabaseDefinition Recipes;
    public ResourceDefinition Resources;
    public Text TextRow, RecipeHeading;
    public Button SkillRow;
    public RecipeResourceRowAdapter ResourceRow;
    private bool _initialized;
    private Vector2 _navigationPosition, _headerPosition, _bodyPosition, _bodySize;
    private float _width, _summaryHeight;
    public Font Font => TextRow.font;
    private void Awake() => InitializeLayout();
    public void ApplyColors()
    {
        // 背景と操作だけを共通色へ揃え、資源アイコンや進捗ゲージの意味を保つ。
        Summary.GetComponent<Image>().color = IdleTabColor;
        Body.GetComponent<Image>().color = IdleTabColor;
        var status = GetComponentInChildren<CellStatusViewAdapter>(true);
        if (status != null) status.StatusScroll.GetComponent<Image>().color = IdleTabColor;
        foreach (var button in GetComponentsInChildren<Button>(true)) StyleButton(button);
    }
    public void StyleButton(Button button, bool selected = false)
    {
        // ColorTintへ色を渡し、背景との二重乗算を避ける。
        button.targetGraphic.color = Color.white;
        var colors = button.colors;
        colors.normalColor = selected ? SelectedTabColor : IdleTabColor;
        colors.highlightedColor = colors.pressedColor = colors.selectedColor = SelectedTabColor;
        button.colors = colors;
    }
    private void InitializeLayout()
    {
        if (_initialized) return;
        _initialized = true;
        _navigationPosition = Navigation.anchoredPosition;
        _headerPosition = ((RectTransform)Close.transform).anchoredPosition;
        var body = (RectTransform)Body.transform;
        _bodyPosition = body.anchoredPosition; _bodySize = body.sizeDelta;
        _width = body.rect.width; _summaryHeight = Summary.rect.height;
    }
    public void UpdateLayout()
    {
        InitializeLayout();
        // 保存した配置へ、折返しで増えた高さと狭い画面の収容差分だけを加える。
        var delta = Summary.rect.height - _summaryHeight;
        Navigation.anchoredPosition = _navigationPosition - new Vector2(0, delta);
        var header = (RectTransform)Close.transform;
        header.anchoredPosition = _headerPosition - new Vector2(0, delta);
        var body = (RectTransform)Body.transform;
        body.anchoredPosition = _bodyPosition - new Vector2(0, delta);
        body.sizeDelta = _bodySize - new Vector2(0, delta);
        var width = Mathf.Min(_width, Mathf.Max(120, ((RectTransform)transform).rect.width - _bodyPosition.x - 424));
        body.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        header.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }
}
