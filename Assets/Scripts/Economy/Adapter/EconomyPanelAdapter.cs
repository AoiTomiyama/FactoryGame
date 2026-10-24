using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>共有Applicationの判定をそのまま表示し、購入命令を同じ所有者へ渡す。</summary>
public sealed class EconomyPanelAdapter : MonoBehaviour
{
    private CurrencyWalletApplication _wallet;
    private SkillTreeApplication _skills;
    private EquipmentUpgradeAdapter _upgrades;
    private CellStatusViewAdapter _view;
    private CellPlacerAdapter _placer;
    private CellBaseAdapter _selected;
    private IDataProvidableApplication _source;
    private CurrencyWalletApplication.Snapshot _previous;
    private bool _bound;
    private bool _expanded = true;
    private bool _selectedActive;
    private int _quotedLevel;
    private Font _font;
    private GameObject _canvas;
    private GameObject _body;
    private RectTransform _header;
    private Text _balance;
    private Text _placement;
    private Text _equipment;
    private Text _feedback;
    private Button _upgrade;
    private readonly Dictionary<string, Button> _nodes = new();
    public string BalanceText => _balance != null ? _balance.text : "";
    public string EquipmentText => _equipment != null ? _equipment.text : "";
    public string FeedbackText => _feedback != null ? _feedback.text : "";
    public bool UpgradeInteractable => _upgrade != null && _upgrade.interactable;
    public bool IsSubscribed => _bound;
    public Button SkillButton(string uid) => _nodes[uid];

    public void Configure(CurrencyWalletApplication wallet, SkillTreeApplication skills,
        EquipmentUpgradeAdapter upgrades, CellStatusViewAdapter view, CellPlacerAdapter placer)
    {
        Unbind();
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
        _view = view;
        _placer = placer;
        if (_canvas == null) Build();
        if (isActiveAndEnabled && _expanded) Bind();
    }

    private void Bind()
    {
        if (_bound || _wallet == null) return;
        _bound = true;
        _previous = _wallet.Current;
        _wallet.Changed += WalletChanged;
        _skills.Changed += Refresh;
        if (_view != null) _view.SelectionChanged += Select;
        Select(_view != null ? _view.SelectedCell : null);
        Refresh();
    }

    private void Unbind()
    {
        if (!_bound) return;
        _wallet.Changed -= WalletChanged;
        _skills.Changed -= Refresh;
        if (_view != null) _view.SelectionChanged -= Select;
        Select(null);
        _bound = false;
    }

    private void Select(CellBaseAdapter cell)
    {
        if (_source != null) _source.StatusChanged -= Refresh;
        _selected = cell;
        _selectedActive = cell != null && cell.isActiveAndEnabled;
        _source = cell as IDataProvidableApplication;
        if (_source != null) _source.StatusChanged += Refresh;
        Refresh();
    }

    private void WalletChanged(CurrencyWalletApplication.Snapshot snapshot)
    {
        if (snapshot.Balance > _previous.Balance)
            _feedback.text = $"換金 +{snapshot.Balance - _previous.Balance}";
        _previous = snapshot;
        Refresh();
    }

    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        _body.SetActive(expanded);
        if (expanded && isActiveAndEnabled) Bind(); else Unbind();
    }

    public EquipmentUpgradeResultApplication PurchaseUpgrade()
    {
        var result = _upgrades.Purchase(_selected, _quotedLevel);
        _feedback.text = result == EquipmentUpgradeResultApplication.Success ? "設備を強化しました" : UpgradeReason(result);
        Refresh();
        return result;
    }

    public SkillPurchaseResultApplication PurchaseSkill(string uid)
    {
        var result = _skills.Purchase(uid);
        _feedback.text = result == SkillPurchaseResultApplication.Success ? "スキルを取得しました" : SkillReason(result);
        Refresh();
        return result;
    }

    private void Refresh()
    {
        if (_balance == null || _wallet == null) return;
        var current = _wallet.Current;
        _balance.text = $"残高 {current.Balance}　使用可能 {current.AvailableBalance}\n支出予約 {current.ReservedExpense}　換金待ち {current.ReservedIncome}";
        var level = _selected != null ? _selected.EquipmentLevel : 0;
        _quotedLevel = level;
        var result = _upgrades.CanPurchase(_selected, level, out var cost);
        _equipment.text = _selected != null && _selected.SupportsProgression
            ? $"設備 ({_selected.XIndex}, {_selected.ZIndex})　Lv.{level}/3\n{Stats(_selected)}\n強化費 {(cost > 0 ? cost.ToString() : "—")}　{UpgradeReason(result)}"
            : "設備を右クリックして強化対象を選択";
        _upgrade.interactable = result == EquipmentUpgradeResultApplication.Success;
        foreach (var node in _skills.Catalog.Nodes)
        {
            var button = _nodes[node.Uid];
            var state = _skills.CanPurchase(node.Uid);
            button.interactable = state == SkillPurchaseResultApplication.Success;
            var prerequisite = node.Prerequisite != null && _skills.Catalog.TryGet(node.Prerequisite, out var parent)
                ? parent.Label : "なし";
            var effect = node.Branch.HasValue ? $"{BranchName(node.Branch.Value)} +{node.BonusPercent}%" : "各効率スキルを解禁";
            button.GetComponentInChildren<Text>().text = $"{node.Label}　費用 {node.Price}\n前提: {prerequisite}　効果: {effect}\n{SkillReason(state)}";
        }
        RefreshPlacement();
    }

    private static string BranchName(StatBranchDomain branch) => branch switch
    {
        StatBranchDomain.Extraction => "抽出量", StatBranchDomain.Transfer => "搬送量", _ => "在庫容量"
    };
    private static string Stats(CellBaseAdapter cell) => cell switch
    {
        ExtractorCellAdapter extractor => $"抽出量 {extractor.ExtractionAmount}　容量 {extractor.StorageCapacity}",
        ConveyorCellAdapter conveyor => $"搬送量 {conveyor.TransferAmount}",
        StorageCellAdapter storage => $"容量 {storage.Capacity}",
        CrafterCellAdapter crafter => $"入力容量 {crafter.IngredientCapacity}　出力容量 {crafter.ExporterCapacity}",
        CrossingCellAdapter => "接続元の搬送量を使用", _ => ""
    };
    public static string UpgradeReason(EquipmentUpgradeResultApplication result) => result switch
    {
        EquipmentUpgradeResultApplication.Success => "強化できます",
        EquipmentUpgradeResultApplication.Busy => "購入処理中",
        EquipmentUpgradeResultApplication.InvalidTarget => "対象設備がありません",
        EquipmentUpgradeResultApplication.LevelChanged => "レベルが変わりました。選択し直してください",
        EquipmentUpgradeResultApplication.MaxLevel => "最大レベル",
        EquipmentUpgradeResultApplication.InvalidPrice => "価格が無効です",
        EquipmentUpgradeResultApplication.InsufficientFunds => "残高不足",
        _ => "強化に失敗しました"
    };
    public static string SkillReason(SkillPurchaseResultApplication result) => result switch
    {
        SkillPurchaseResultApplication.Success => "取得できます",
        SkillPurchaseResultApplication.Busy => "購入処理中",
        SkillPurchaseResultApplication.UnknownNode => "スキルが見つかりません",
        SkillPurchaseResultApplication.AlreadyOwned => "取得済み",
        SkillPurchaseResultApplication.PrerequisiteMissing => "前提スキルが必要です",
        SkillPurchaseResultApplication.InsufficientFunds => "残高不足",
        _ => "共有効果が変わりました"
    };

    private void RefreshPlacement()
    {
        if (_placer == null) return;
        _placement.text = _placer.PreviewText + "\n" + _placer.ResultText;
    }
    private void LateUpdate()
    {
        if (_canvas != null)
        {
            var width = Mathf.Min(350, Mathf.Max(120, _canvas.GetComponent<RectTransform>().rect.width - 24));
            _header.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _body.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
        if (!_bound) return;
        if (!ReferenceEquals(_selected, null) && _selected == null) Select(null);
        var active = _selected != null && _selected.isActiveAndEnabled;
        if (active != _selectedActive) { _selectedActive = active; Refresh(); }
        RefreshPlacement();
        foreach (var button in _nodes.Values)
            button.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(88, button.GetComponentInChildren<Text>().preferredHeight + 8);
    }
    private void OnEnable() { if (_canvas != null) _canvas.SetActive(true); if (_expanded) Bind(); }
    private void OnDisable() { Unbind(); if (_canvas != null) _canvas.SetActive(false); }
    private void OnDestroy()
    {
        Unbind();
        if (_canvas != null) Destroy(_canvas);
        if (_font != null) Destroy(_font);
    }

    private void Build()
    {
        // OSフォントで既存TMPアセットに不足する日本語も表示する。
        _font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Arial" }, 16);
        _canvas = new GameObject("EconomyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas.transform.SetParent(transform, false);
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.GetComponent<Canvas>().sortingOrder = 20;
        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1;
        var header = MakeButton(_canvas.transform, "経済・成長　表示 / 非表示", () => SetExpanded(!_expanded), 36);
        _header = header.GetComponent<RectTransform>();
        Anchor(header.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(350, 36));
        _body = new GameObject("EconomyScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        _body.transform.SetParent(_canvas.transform, false);
        Anchor(_body.GetComponent<RectTransform>(), new Vector2(0, 0.25f), new Vector2(0, 1), new Vector2(12, -52), new Vector2(350, -128));
        _body.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.12f, 0.96f);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(_body.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10); layout.spacing = 8;
        layout.childControlHeight = true; layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = _body.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = rect; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 25;
        _balance = MakeText(content.transform, "", 16, 48);
        _feedback = MakeText(content.transform, "換金するとここに入金額を表示します", 16, 40);
        _placement = MakeText(content.transform, "", 16, 64);
        MakeText(content.transform, "設備強化", 19, 26);
        _equipment = MakeText(content.transform, "", 16, 88);
        _upgrade = MakeButton(content.transform, "選択設備を強化", () => PurchaseUpgrade(), 34);
        MakeText(content.transform, "スキルツリー", 19, 26);
        foreach (var node in _skills.Catalog.Nodes)
        {
            var uid = node.Uid;
            _nodes.Add(uid, MakeButton(content.transform, "", () => PurchaseSkill(uid), 88));
        }
    }

    private Text MakeText(Transform parent, string value, int size, int height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = _font; text.fontSize = size; text.text = value; text.color = Color.white;
        text.fontStyle = size >= 19 ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        go.GetComponent<LayoutElement>().minHeight = height;
        return text;
    }
    private Button MakeButton(Transform parent, string label, Action click, int height)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.22f, 0.34f, 0.43f);
        go.GetComponent<LayoutElement>().preferredHeight = height;
        var button = go.GetComponent<Button>(); button.onClick.AddListener(() => click());
        var text = MakeText(go.transform, label, 16, height);
        Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(8, 2); text.rectTransform.offsetMax = new Vector2(-8, -2);
        return button;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
}
