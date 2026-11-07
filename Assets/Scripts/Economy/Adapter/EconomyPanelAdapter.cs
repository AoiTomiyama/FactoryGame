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
    private readonly GameObject[] _pages = new GameObject[3];
    private readonly Button[] _tabs = new Button[3];
    public int SelectedPage { get; private set; }
    public Button PageButton(int index) => _tabs[index];
    public Transform PlacementButtons => _pages[0].transform;
    public string PlacementText => _placement != null ? _placement.text : "";

    public void ShowPage(int index)
    {
        if (index < 0 || index >= _pages.Length) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedPage = index;
        _body.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].SetActive(i == index);
            _tabs[i].GetComponent<Image>().color = i == index
                ? new Color(0.24f, 0.43f, 0.53f) : new Color(0.14f, 0.21f, 0.27f);
        }
    }
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
            button.GetComponentInChildren<Text>(true).text = $"{node.Label}　費用 {node.Price}\n前提: {prerequisite}　効果: {effect}\n{SkillReason(state)}";
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
        _placement.text = _placer.PreviewText.Replace(_placer.SelectedType.ToString(), SelectButtonBuilderAdapter.PlacementLabel(_placer.SelectedType)) + "\n" + _placer.ResultText;
    }
    private void LateUpdate()
    {
        if (_canvas != null)
        {
            var width = Mathf.Min(400, Mathf.Max(120, _canvas.GetComponent<RectTransform>().rect.width - 24));
            _header.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _body.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
        if (!_bound) return;
        if (!ReferenceEquals(_selected, null) && _selected == null) Select(null);
        var active = _selected != null && _selected.isActiveAndEnabled;
        if (active != _selectedActive) { _selectedActive = active; Refresh(); }
        RefreshPlacement();
        foreach (var button in _nodes.Values)
            button.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(88, button.GetComponentInChildren<Text>(true).preferredHeight + 12);
        foreach (var text in new[] { _balance, _feedback, _placement, _equipment })
            text.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(text.GetComponent<LayoutElement>().minHeight, text.preferredHeight);
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
        _canvas = new GameObject("PlacementCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas.transform.SetParent(transform, false);
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.GetComponent<Canvas>().sortingOrder = 20;
        var scaler = _canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 1;
        var header = MakeButton(_canvas.transform, "配置・成長　表示 / 非表示", () => SetExpanded(!_expanded), 36);
        header.GetComponentInChildren<Text>().fontSize = 18;
        header.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        _header = header.GetComponent<RectTransform>();
        Anchor(header.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(400, 36));
        _body = new GameObject("EconomyScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        _body.transform.SetParent(_canvas.transform, false);
        // 下部の操作帯へスクロール内容が重ならない余白を確保する。
        Anchor(_body.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 1), new Vector2(12, -52), new Vector2(400, -160));
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
        _balance.fontStyle = FontStyle.Bold;
        _feedback = MakeText(content.transform, "換金するとここに入金額を表示します", 14, 40);
        var navigation = new GameObject("Pages", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        navigation.transform.SetParent(content.transform, false);
        navigation.GetComponent<LayoutElement>().preferredHeight = 36;
        navigation.GetComponent<HorizontalLayoutGroup>().spacing = 6;
        var names = new[] { "配置", "設備強化", "スキル" };
        for (var i = 0; i < names.Length; i++)
        {
            var page = i;
            _tabs[i] = MakeButton(navigation.transform, names[i], () => ShowPage(page), 36);
            _pages[i] = new GameObject(names[i], typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _pages[i].transform.SetParent(content.transform, false);
            var pageLayout = _pages[i].GetComponent<VerticalLayoutGroup>();
            pageLayout.spacing = 8; pageLayout.childControlHeight = true; pageLayout.childForceExpandHeight = false;
            _pages[i].GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        _placement = MakeText(_pages[0].transform, "", 16, 84);
        // シーンの既存Builderとボタン定義を使い、旧タブの表示を統合UIへ移す。
        foreach (var builder in FindObjectsByType<SelectButtonBuilderAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            builder.AttachTo(_pages[0].transform, _font);
        _equipment = MakeText(_pages[1].transform, "", 16, 104);
        _upgrade = MakeButton(_pages[1].transform, "選択設備を強化", () => PurchaseUpgrade(), 36);
        foreach (var node in _skills.Catalog.Nodes)
        {
            var uid = node.Uid;
            _nodes.Add(uid, MakeButton(_pages[2].transform, "", () => PurchaseSkill(uid), 88));
        }
        ShowPage(0);
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
