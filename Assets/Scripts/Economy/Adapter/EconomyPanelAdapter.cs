using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

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
    [SerializeField] private HudLayoutAdapter layout;
    private Font _font;
    private GameObject _canvas;
    private GameObject _body;
    private RectTransform _header;
    private PersistentSummaryViewAdapter _summaryView;
    private EquipmentDetailsViewAdapter _equipmentView;
    private EquipmentDetailsPresenterAdapter _equipmentPresenter;
    public EquipmentDetailsViewAdapter EquipmentView => _equipmentView;
    public PersistentSummaryViewAdapter SummaryView => _summaryView;
    public RectTransform SummaryRect => _summaryView.Rect;
    public Transform RecipeContent => _pages[1].transform;
    public RecipeReferenceViewAdapter RecipeView { get; private set; }
    public void SetSummaryItems(params string[] items) => _summaryView.SetItems(items);
    private readonly GameObject[] _pages = new GameObject[3];
    private readonly Button[] _tabs = new Button[3];
    private readonly UnityAction[] _tabActions = new UnityAction[3];
    private UnityAction _closeAction;
    public int SelectedPage { get; private set; }
    public bool IsExpanded => _expanded;
    public Button PageButton(int index) => _tabs[index];
    public Transform PlacementButtons => _pages[0].transform;
    public string PlacementText => _placement != null ? _placement.text : "";

    public void ShowPage(int index)
    {
        if (index < 0 || index >= _pages.Length) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedPage = index;
        SetExpanded(true);
        _body.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].SetActive(i == index);
            _tabs[i].GetComponent<Image>().color = i == index
                ? layout.SelectedTabColor : layout.IdleTabColor;
        }
    }
    private Text _placement;
    private Text _feedback;
    private readonly Dictionary<string, Button> _nodes = new();
    public string BalanceText => _summaryView != null ? _summaryView.BalanceText : "";
    public string EquipmentText => _equipmentView != null ? _equipmentView.EquipmentText : "";
    public string FeedbackText => _feedback != null ? _feedback.text : "";
    public bool UpgradeInteractable => _equipmentView != null && _equipmentView.Interactable;
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
        if (_canvas == null) BindLayout();
        if (isActiveAndEnabled) Bind();
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
        _header.gameObject.SetActive(expanded);
        // 常時表示の残高と右側の強化欄は、左ページを閉じても更新する。
    }

    public EquipmentUpgradeResultApplication PurchaseUpgrade()
    {
        var result = _equipmentPresenter.Purchase();
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
        if (_summaryView == null || _wallet == null) return;
        var current = _wallet.Current;
        _summaryView.RenderWallet(current);
        _equipmentPresenter.Refresh(_selected);
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
    public static string UpgradeReason(EquipmentUpgradeResultApplication result)
        => EquipmentDetailsViewAdapter.UpgradeReason(result);
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
            _summaryView.UpdateLayout();
            layout.UpdateLayout();
            _view.TopInset = _summaryView.Rect.rect.height + 20;
        }
        if (!_bound) return;
        if (!ReferenceEquals(_selected, null) && _selected == null) Select(null);
        var active = _selected != null && _selected.isActiveAndEnabled;
        if (active != _selectedActive) { _selectedActive = active; Refresh(); }
        RefreshPlacement();
        _equipmentView.UpdateLayout();
        foreach (var button in _nodes.Values)
            button.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(88, button.GetComponentInChildren<Text>(true).preferredHeight + 12);
        foreach (var text in new[] { _feedback, _placement })
            text.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(text.GetComponent<LayoutElement>().minHeight, text.preferredHeight);
    }
    private void OnEnable() { if (_canvas != null) _canvas.SetActive(true); Bind(); }
    private void OnDisable() { Unbind(); _equipmentPresenter?.SetActive(false); if (_canvas != null) _canvas.SetActive(false); }
    private void OnDestroy()
    {
        Unbind();
        _equipmentPresenter?.Dispose();
        for (var i = 0; i < _tabs.Length; i++)
            if (_tabs[i] != null && _tabActions[i] != null) _tabs[i].onClick.RemoveListener(_tabActions[i]);
        if (layout != null && _closeAction != null) layout.Close.onClick.RemoveListener(_closeAction);
        foreach (var button in _nodes.Values) if (button != null) Destroy(button.gameObject);
        _nodes.Clear();
        RecipeView?.Dispose();
        _summaryView?.Dispose();
    }

    private void BindLayout()
    {
        // 固定枠は保存済みPrefabを使い、実行時は状態と操作だけを接続する。
        if (layout == null) throw new InvalidOperationException("HUD layout must be assigned in the scene.");
        _font = layout.Font;
        _canvas = layout.gameObject;
        _body = layout.Body.gameObject;
        _header = layout.Close.GetComponent<RectTransform>();
        _feedback = layout.Feedback;
        _placement = layout.Placement;
        for (var i = 0; i < 3; i++)
        {
            var page = i;
            _tabs[i] = layout.Tabs[i];
            _pages[i] = layout.Pages[i].gameObject;
            _tabActions[i] = () => { if (_expanded && SelectedPage == page) SetExpanded(false); else ShowPage(page); };
            _tabs[i].onClick.AddListener(_tabActions[i]);
        }
        _closeAction = () => SetExpanded(!_expanded);
        layout.Close.onClick.AddListener(_closeAction);
        _summaryView = new PersistentSummaryViewAdapter(layout.Summary, _font, layout.WalletItems, layout.TextRow);
        layout.PlacementBuilder.Configure(_font);
        RecipeView = new RecipeReferenceViewAdapter(_pages[1].transform, _font, layout.RecipeHeading, layout.ResourceRow);
        RecipeView.Render(layout.Recipes, layout.Resources);
        _view.ConfigureHud(_font, layout.Resources);
        _equipmentView = new EquipmentDetailsViewAdapter(layout.EquipmentHeading, layout.EquipmentGroup,
            layout.EquipmentText, layout.Upgrade, layout.EquipmentFeedback);
        _equipmentPresenter = new EquipmentDetailsPresenterAdapter(_equipmentView, _upgrades);
        foreach (var node in _skills.Catalog.Nodes)
        {
            var uid = node.Uid;
            var button = Instantiate(layout.SkillRow, _pages[2].transform);
            var label = button.GetComponentInChildren<Text>(true);
            if (label.font == null) label.font = _font;
            button.onClick.AddListener(() => PurchaseSkill(uid));
            _nodes.Add(uid, button);
        }
        ShowPage(0);
    }
}
