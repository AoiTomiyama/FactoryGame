using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>共有Applicationの判定をそのまま表示し、購入命令を同じ所有者へ渡す。</summary>
public sealed class EconomyPanelAdapter : MonoBehaviour
{
    private CurrencyWalletApplication _wallet;
    private SkillTreeApplication _skills;
    private DeliveryProgressApplication _deliveries;
    private DeliveryGoalsViewAdapter _deliveryView;
    private CellStatusViewAdapter _view;
    private CellPlacerAdapter _placer;
    private CellBaseAdapter _selected;
    private IDataProvidableApplication _source;
    private bool _bound;
    private bool _expanded = true;
    private bool _selectedActive;
    private bool _firstPerson;
    private CameraControllerAdapter _cameraController;
    private void SetFirstPersonMode(bool active)
    {
        _firstPerson = active;
        layout.SetFirstPersonMode(active);
        _view.SetFirstPersonMode(active);
        _summaryView.SetFirstPersonMode(active);
        _operation.SetFirstPersonMode(active);
        UpdatePlacementMode();
    }
    [SerializeField] private HudLayoutAdapter layout;
    private Font _font;
    private GameObject _canvas;
    private GameObject _body;
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
        // スキルは樹形図自身が縦横の移動を扱い、親ページとの二重スクロールを避ける。
        layout.Body.vertical = index != 2;
        SetExpanded(true);
        _body.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].SetActive(i == index);
            layout.StyleButton(_tabs[i], i == index);
        }
    }
    private OperationBarAdapter _operation;
    private Text _placement;
    private SkillTreeViewAdapter _skillView;
    private string _selectedSkill, _equipmentActionLabel;
    private float _equipmentTextMinHeight;
    public string SelectedSkill => _selectedSkill;
    public string BalanceText => _summaryView != null ? _summaryView.BalanceText : "";
    public string EquipmentText => _equipmentView != null ? _equipmentView.EquipmentText : "";
    public bool UpgradeInteractable => _equipmentView != null && _equipmentView.Interactable;
    public bool IsSubscribed => _bound;
    public Button SkillButton(string uid) => _skillView.Buttons[uid];

    public void Configure(CurrencyWalletApplication wallet, SkillTreeApplication skills,
        CellStatusViewAdapter view, CellPlacerAdapter placer, DeliveryProgressApplication deliveries)
    {
        Unbind();
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        _view = view;
        _placer = placer;
        if (_canvas == null) BindLayout();
        _deliveryView = new DeliveryGoalsViewAdapter(_deliveries.Catalog, layout.Resources);
        if (isActiveAndEnabled) Bind();
    }

    private void Bind()
    {
        if (_bound || _wallet == null) return;
        _bound = true;
        UpdatePlacementMode();
        _wallet.Changed += WalletChanged;
        _skills.Changed += Refresh;
        _deliveries.Changed += DeliveryChanged;
        _deliveries.StageCompleted += DeliveryStageCompleted;
        DeliveryChanged(_deliveries.Current);
        if (_view != null) _view.SelectionChanged += Select;
        Select(_view != null ? _view.SelectedCell : null);
        Refresh();
    }

    private void Unbind()
    {
        if (!_bound) return;
        _wallet.Changed -= WalletChanged;
        _skills.Changed -= Refresh;
        _deliveries.Changed -= DeliveryChanged;
        _deliveries.StageCompleted -= DeliveryStageCompleted;
        if (_view != null) _view.SelectionChanged -= Select;
        Select(null);
        _bound = false;
    }

    private void Select(CellBaseAdapter cell)
    {
        _selectedSkill = null;
        if (_source != null) _source.StatusChanged -= Refresh;
        if (_selected is ConveyorCellAdapter previousConveyor) previousConveyor.KindChanged -= Refresh;
        if (_selected is ConstructionDepotCellAdapter previousDepot) previousDepot.StatusChanged -= Refresh;
        _selected = cell;
        _selectedActive = cell != null && cell.isActiveAndEnabled;
        _source = cell as IDataProvidableApplication;
        if (_source != null) _source.StatusChanged += Refresh;
        if (_selected is ConveyorCellAdapter conveyor) conveyor.KindChanged += Refresh;
        if (_selected is ConstructionDepotCellAdapter depot) depot.StatusChanged += Refresh;
        Refresh();
    }

    private void WalletChanged(CurrencyWalletApplication.Snapshot _) => Refresh();
    private void DeliveryChanged(DeliveryProgressApplication.Snapshot snapshot)
    {
        _summaryView.SetItems(_deliveryView.Summary(snapshot));
        if (_selected is SingularityCellAdapter && _selectedSkill == null) Refresh();
    }
    private void DeliveryStageCompleted(DeliveryGoalCatalogDomain.Stage _)
    {
        if (_summaryView.Items.Count > 0) HudMotionAdapter.Bind(_summaryView.Items[0]).Pulse();
    }

    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        layout.SetLeftExpanded(expanded);
        UpdatePlacementMode();
        for (var i = 0; i < _tabs.Length; i++) layout.StyleButton(_tabs[i], expanded && SelectedPage == i);
        // 閉じたタブに選択フォーカスが残り、選択色へ戻ることを防ぐ。
        if (!expanded && EventSystem.current != null)
            foreach (var tab in _tabs)
                if (EventSystem.current.currentSelectedGameObject == tab.gameObject) EventSystem.current.SetSelectedGameObject(null);
        // 常時表示の残高と右側の強化欄は、左ページを閉じても更新する。
    }

    public SkillPurchaseResultApplication PurchaseSkill(string uid)
    {
        var result = _skills.Purchase(uid);
        if (result != SkillPurchaseResultApplication.Success && result != SkillPurchaseResultApplication.InsufficientFunds &&
            result != SkillPurchaseResultApplication.PrerequisiteMissing && result != SkillPurchaseResultApplication.AlreadyOwned) Debug.LogWarning($"スキル適用失敗 ({uid})：{SkillReason(result)}", this);
        Refresh();
        return result;
    }
    public void SelectSkill(string uid)
    {
        if (!_skills.Catalog.TryGet(uid, out _)) return;
        // スキル間の切替では右枠を閉じ直さず、ボタンの有効状態と色の瞬間的な初期化を避ける。
        if (_selectedSkill == null) _view.ShowDetails();
        _selectedSkill = uid;
        Refresh();
    }
    private void ApplyDetails()
    {
        if (_selectedSkill != null) PurchaseSkill(_selectedSkill);
    }

    private void Refresh()
    {
        if (_summaryView == null || _wallet == null) return;
        var current = _wallet.Current;
        _summaryView.RenderWallet(current);
        if (_selectedSkill != null && _skills.Catalog.TryGet(_selectedSkill, out var node))
        {
            var state = _skills.CanPurchase(node.Uid);
            var prerequisite = node.Prerequisite != null && _skills.Catalog.TryGet(node.Prerequisite, out var parent)
                ? parent.Label : "なし";
            var effect = node.CellType.HasValue ? $"同種セルすべて\n{StatName(node.Stat)} ×{node.Modifier.Multiplier:0.###}" + (node.Modifier.Addition == 0 ? "" : $" ＋{node.Modifier.Addition}") : "能力別の強化を解放";
            _equipmentView.Heading.gameObject.SetActive(true); _equipmentView.UpgradeRoot.gameObject.SetActive(true);
            _equipmentView.UpgradeButton.gameObject.SetActive(true);
            _equipmentView.Heading.text = node.Label;
            layout.EquipmentText.GetComponent<LayoutElement>().minHeight = 64;
            _equipmentView.EquipmentText = effect;
            _equipmentView.Feedback = state == SkillPurchaseResultApplication.PrerequisiteMissing ? prerequisite + "が必要"
                : state == SkillPurchaseResultApplication.InsufficientFunds ? SkillReason(state) : "";
            layout.EquipmentFeedback.gameObject.SetActive(!string.IsNullOrEmpty(_equipmentView.Feedback));
            _equipmentView.UpgradeButton.GetComponentInChildren<Text>(true).text = _skills.IsOwned(node.Uid) ? "適用済み" : $"適用する　{node.Price}";
            _equipmentView.UpgradeButton.interactable = state == SkillPurchaseResultApplication.Success;
        }
        else if (_selected is ConstructionDepotCellAdapter depot)
        {
            _equipmentPresenter.Refresh(_selected);
            layout.EquipmentText.GetComponent<LayoutElement>().minHeight = _equipmentTextMinHeight;
            _equipmentView.UpgradeRoot.gameObject.SetActive(true);
            _equipmentView.UpgradeButton.gameObject.SetActive(false);
            var items = layout.Resources.GetAllResources().Where(r => depot.Stock.Stored(r.ResourceTypeDomain) > 0)
                .Select(r => $"{r.ResourceName}　{depot.Stock.Stored(r.ResourceTypeDomain)}");
            _equipmentView.EquipmentText = items.Any() ? string.Join("\n", items) : "在庫なし";
            _equipmentView.Feedback = "";
        }
        else if (_selected is SingularityCellAdapter)
        {
            layout.EquipmentText.GetComponent<LayoutElement>().minHeight = _equipmentTextMinHeight;
            _deliveryView.RenderDetails(_equipmentView, _deliveries.Current, _selectedActive);
        }
        else
        {
            _equipmentView.UpgradeButton.GetComponentInChildren<Text>(true).text = _equipmentActionLabel;
            layout.EquipmentText.GetComponent<LayoutElement>().minHeight = _equipmentTextMinHeight;
            _equipmentPresenter.Refresh(_selected);
            layout.EquipmentFeedback.gameObject.SetActive(!string.IsNullOrEmpty(_equipmentView.Feedback));
        }
        _skillView.Refresh(_selectedSkill);
        RefreshPlacement();
    }

    public static string StatName(EquipmentStatDomain stat) => stat switch
    {
        EquipmentStatDomain.ExtractionAmount => "抽出量", EquipmentStatDomain.Capacity => "容量",
        EquipmentStatDomain.InputCapacity => "入力容量", EquipmentStatDomain.OutputCapacity => "出力容量", _ => "搬出量"
    };
    public static string SkillReason(SkillPurchaseResultApplication result) => result switch
    {
        SkillPurchaseResultApplication.Success => "取得できます",
        SkillPurchaseResultApplication.Busy => "購入処理中",
        SkillPurchaseResultApplication.UnknownNode => "スキルが見つかりません",
        SkillPurchaseResultApplication.AlreadyOwned => "取得済み",
        SkillPurchaseResultApplication.PrerequisiteMissing => "前提スキルが必要です",
        SkillPurchaseResultApplication.InsufficientFunds => "残高不足",
        SkillPurchaseResultApplication.InvalidEffect => "強化値が現在値を下回るか上限を超えています",
        _ => "共有効果が変わりました"
    };

    private void UpdatePlacementMode()
    {
        var active = isActiveAndEnabled && !_firstPerson && _expanded && SelectedPage == 0;
        _placer?.SetPlacementEnabled(active);
        _operation?.SetPlacementActive(active);
    }

    private void RefreshPlacement()
    {
        if (_placer == null) return;
        _placement.text = _placer.PreviewText.Replace(_placer.SelectedType.ToString(), SelectButtonBuilderAdapter.PlacementLabel(_placer.SelectedType)) + (string.IsNullOrEmpty(_placer.PlacementNotice) ? "" : "\n" + _placer.PlacementNotice);
    }
    private void LateUpdate()
    {
        if (_canvas != null)
        {
            _summaryView.UpdateLayout();
            layout.UpdateLayout();
            _view.TopInset = _summaryView.Rect.rect.height + 8;
        }
        if (!_bound) return;
        if (!ReferenceEquals(_selected, null) && _selected == null) Select(null);
        var active = _selected != null && _selected.isActiveAndEnabled;
        if (active != _selectedActive) { _selectedActive = active; Refresh(); }
        RefreshPlacement();
        _equipmentView.UpdateLayout();
        _skillView.UpdateLayout();
    }
    private void OnEnable() { if (_canvas != null) _canvas.SetActive(true); Bind(); }
    private void OnDisable() { UpdatePlacementMode(); Unbind(); _equipmentPresenter?.SetActive(false); if (_canvas != null) _canvas.SetActive(false); }
    private void OnDestroy()
    {
        if (_cameraController != null) _cameraController.FirstPersonChanged -= SetFirstPersonMode;
        _placer?.SetPlacementEnabled(false); _operation?.SetPlacementActive(false);
        Unbind();
        _equipmentPresenter?.Dispose();
        for (var i = 0; i < _tabs.Length; i++)
            if (_tabs[i] != null && _tabActions[i] != null) _tabs[i].onClick.RemoveListener(_tabActions[i]);
        if (layout != null && _closeAction != null) layout.Close.onClick.RemoveListener(_closeAction);
        _skillView?.Dispose();
        RecipeView?.Dispose();
        _summaryView?.Dispose();
    }

    private void BindLayout()
    {
        // 固定枠は保存済みPrefabを使い、実行時は状態と操作だけを接続する。
        if (layout == null) throw new InvalidOperationException("HUD layout must be assigned in the scene.");
        layout.ApplyColors();
        _font = layout.Font;
        _canvas = layout.gameObject;
        _body = layout.Body.gameObject;
        _operation = FindAnyObjectByType<OperationBarAdapter>(FindObjectsInactive.Include);
        _cameraController = FindAnyObjectByType<CameraControllerAdapter>();
        if (_cameraController != null) _cameraController.FirstPersonChanged += SetFirstPersonMode;
        _placement = _operation.PlacementInfo;
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
        _summaryView = new PersistentSummaryViewAdapter(layout.Summary, _font, layout.WalletItems, layout.TextRow, layout.Save != null ? (RectTransform)layout.Save.transform : null);
        layout.PlacementBuilder.Configure(_font);
        RecipeView = new RecipeReferenceViewAdapter(_pages[1].transform, _font, layout.RecipeHeading, layout.ResourceRow);
        RecipeView.Render(layout.Recipes, layout.Resources);
        _view.ConfigureHud(_font, layout.Resources);
        _equipmentView = new EquipmentDetailsViewAdapter(layout.EquipmentHeading, layout.EquipmentGroup,
            layout.EquipmentText, layout.Upgrade, layout.EquipmentFeedback);
        _equipmentActionLabel = layout.Upgrade.GetComponentInChildren<Text>(true).text;
        _equipmentTextMinHeight = layout.EquipmentText.GetComponent<LayoutElement>().minHeight;
        _equipmentPresenter = new EquipmentDetailsPresenterAdapter(_equipmentView, ApplyDetails);
        _skillView = new SkillTreeViewAdapter(_pages[2].transform, layout, _skills, SelectSkill);
        ShowPage(0);
    }
}
