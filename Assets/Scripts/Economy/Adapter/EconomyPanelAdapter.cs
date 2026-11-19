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
    private DeliveryProgressApplication _deliveries;
    private DeliveryGoalsViewAdapter _deliveryView;
    private CellStatusViewAdapter _view;
    private CellPlacerAdapter _placer;
    private CellBaseAdapter _selected;
    private IDataProvidableApplication _source;
    private bool _bound;
    private IncomeRateApplication _incomeRate;
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
    private PersistentSummaryViewAdapter _summaryView;
    private EquipmentDetailsViewAdapter _equipmentView;
    private EquipmentDetailsPresenterAdapter _equipmentPresenter;
    public EquipmentDetailsViewAdapter EquipmentView => _equipmentView;
    public PersistentSummaryViewAdapter SummaryView => _summaryView;
    public RectTransform SummaryRect => _summaryView.Rect;
    public const int RecipePage = 1;
    public const int EncyclopediaPage = 3;
    private const int SkillTab = 2;
    public Transform RecipeContent => _pages[RecipePage].transform;
    public Transform EncyclopediaContent => _pages[EncyclopediaPage].transform;
    public RecipeReferenceViewAdapter RecipeView { get; private set; }
    public RecipeReferenceViewAdapter EncyclopediaView { get; private set; }
    private RecipeReferenceViewAdapter ActiveReference => SelectedPage switch
    {
        RecipePage => RecipeView,
        EncyclopediaPage => EncyclopediaView,
        _ => null
    };
    public void SetSummaryItems(params string[] items) => _summaryView.SetItems(items);
    private readonly GameObject[] _pages = new GameObject[4];
    private readonly Button[] _tabs = new Button[4];
    private readonly UnityAction[] _tabActions = new UnityAction[4];
    private UnityAction _closeAction;
    public int SelectedPage { get; private set; }
    public bool IsExpanded => _expanded;
    public Button PageButton(int index) => _tabs[index];
    public Transform PlacementButtons => _pages[0].transform;
    public string PlacementText => _placement != null ? _placement.text : "";

    public void ShowPage(int index)
    {
        if (index < 0 || index >= _pages.Length || _pages[index] == null)
            throw new ArgumentOutOfRangeException(nameof(index));
        ActiveReference?.RememberScroll();
        SelectedPage = index;
        layout.Body.vertical = true;
        for (var i = 0; i < _pages.Length; i++)
            if (_pages[i] != null) _pages[i].SetActive(i == index);
        SetExpanded(true);
        if (ActiveReference != null) ActiveReference.RestoreScroll();
        else layout.Body.verticalNormalizedPosition = 1;
    }
    private OperationBarAdapter _operation;
    private Text _placement;
    private string _equipmentActionLabel;
    private float _equipmentTextMinHeight;
    public string BalanceText => _summaryView != null ? _summaryView.BalanceText : "";
    public string EquipmentText => _equipmentView != null ? _equipmentView.EquipmentText : "";
    public bool UpgradeInteractable => _equipmentView != null && _equipmentView.Interactable;
    public bool IsSubscribed => _bound;
    public SkillScreenAdapter SkillScreen => layout.SkillScreen;
    public OperationBarAdapter Operation => _operation;

    public void Configure(CurrencyWalletApplication wallet, IncomeRateApplication incomeRate,
        CellStatusViewAdapter view, CellPlacerAdapter placer, DeliveryProgressApplication deliveries)
    {
        Unbind();
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _incomeRate = incomeRate ?? throw new ArgumentNullException(nameof(incomeRate));
        _deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        _view = view;
        _placer = placer;
        if (_canvas == null) BindLayout();
        _deliveryView = new DeliveryGoalsViewAdapter(_deliveries.Catalog, layout.Resources);
        if (isActiveAndEnabled) Bind();
    }

    public void ConfigureReferences(RecipeReferenceCatalogApplication catalog)
    {
        RecipeView?.Dispose();
        EncyclopediaView?.Dispose();
        RecipeView = new RecipeReferenceViewAdapter((RectTransform)RecipeContent, layout.RecipeDetails,
            _view.StatusScroll, layout, catalog, false, UpdateReferenceDetail);
        EncyclopediaView = new RecipeReferenceViewAdapter((RectTransform)EncyclopediaContent, layout.EncyclopediaDetails,
            _view.StatusScroll, layout, catalog, true, UpdateReferenceDetail);
        UpdateReferenceDetail();
        ActiveReference?.RestoreScroll();
    }

    private void UpdateReferenceDetail() =>
        _view.SetReferenceContent(_expanded && ActiveReference?.IsDetail == true ? ActiveReference.DetailContent : null);

    private void CloseReference() => ActiveReference?.ClearSelection();

    internal void ReleaseWorld()
    {
        if (_view != null) SetExpanded(false);
        Unbind();
    }

    private void Bind()
    {
        if (_bound || _wallet == null) return;
        _bound = true;
        _summaryView.RenderIncome(_incomeRate.IncomePerSecond);
        UpdatePlacementMode();
        _wallet.Changed += WalletChanged;
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
        _deliveries.Changed -= DeliveryChanged;
        _deliveries.StageCompleted -= DeliveryStageCompleted;
        if (_view != null) _view.SelectionChanged -= Select;
        Select(null);
        _bound = false;
    }

    private void Select(CellBaseAdapter cell)
    {
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
        // 納品達成で購入条件が変わるスキルも、次の選択操作を待たず更新する。
        Refresh();
    }
    private void DeliveryStageCompleted(DeliveryGoalCatalogDomain.Stage _)
    {
        if (_summaryView.Items.Count > 0) HudMotionAdapter.Bind(_summaryView.Items[0]).Pulse();
    }

    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        layout.SetLeftExpanded(expanded);
        UpdateReferenceDetail();
        UpdatePlacementMode();
        for (var i = 0; i < _tabs.Length; i++) layout.StyleButton(_tabs[i], expanded && SelectedPage == i);
        // 閉じたタブに選択フォーカスが残り、選択色へ戻ることを防ぐ。
        if (!expanded && EventSystem.current != null)
            foreach (var tab in _tabs)
                if (EventSystem.current.currentSelectedGameObject == tab.gameObject) EventSystem.current.SetSelectedGameObject(null);
        // 常時表示の残高と右側の強化欄は、左ページを閉じても更新する。
    }

    private void Refresh()
    {
        if (_summaryView == null || _wallet == null) return;
        var current = _wallet.Current;
        _summaryView.RenderWallet(current);
        if (_selected is ConstructionDepotCellAdapter depot)
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
        RefreshPlacement();
    }

    private void SkillVisibilityChanged(bool _) => UpdatePlacementMode();
    private void UpdatePlacementMode()
    {
        var active = isActiveAndEnabled && !_firstPerson && !SkillScreen.IsOpen && _expanded && SelectedPage == 0;
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
        _summaryView.RenderIncome(_incomeRate.IncomePerSecond);
        if (!ReferenceEquals(_selected, null) && _selected == null) Select(null);
        var active = _selected != null && _selected.isActiveAndEnabled;
        if (active != _selectedActive) { _selectedActive = active; Refresh(); }
        RefreshPlacement();
        _equipmentView.UpdateLayout();
    }
    private void OnEnable() { if (_canvas != null) _canvas.SetActive(true); Bind(); }
    private void OnDisable() { UpdatePlacementMode(); Unbind(); _equipmentPresenter?.SetActive(false); if (_canvas != null) _canvas.SetActive(false); }
    private void OnDestroy()
    {
        if (_cameraController != null) _cameraController.FirstPersonChanged -= SetFirstPersonMode;
        _placer?.SetPlacementEnabled(false); _operation?.SetPlacementActive(false);
        Unbind();
        if (layout != null && SkillScreen != null) SkillScreen.VisibilityChanged -= SkillVisibilityChanged;
        for (var i = 0; i < _tabs.Length; i++)
            if (_tabs[i] != null && _tabActions[i] != null) _tabs[i].onClick.RemoveListener(_tabActions[i]);
        if (layout != null && _closeAction != null) layout.Close.onClick.RemoveListener(_closeAction);
        if (_view != null) _view.ReferenceClosed -= CloseReference;
        RecipeView?.Dispose();
        EncyclopediaView?.Dispose();
        _summaryView?.Dispose();
    }

    private void BindLayout()
    {
        // 固定枠は保存済みPrefabを使い、実行時は状態と操作だけを接続する。
        if (layout == null) throw new InvalidOperationException("HUD layout must be assigned in the scene.");
        layout.ApplyColors();
        _font = layout.Font;
        _canvas = layout.gameObject;
        _operation = FindAnyObjectByType<OperationBarAdapter>(FindObjectsInactive.Include);
        _cameraController = FindAnyObjectByType<CameraControllerAdapter>();
        if (_cameraController != null) _cameraController.FirstPersonChanged += SetFirstPersonMode;
        _placement = _operation.PlacementInfo;
        _pages[0] = layout.Pages[0].gameObject;
        _pages[RecipePage] = layout.Pages[1].gameObject;
        _pages[EncyclopediaPage] = layout.EncyclopediaPage.gameObject;
        _view.ReferenceClosed += CloseReference;
        for (var i = 0; i < _tabs.Length; i++)
        {
            var page = i;
            _tabs[i] = layout.Tabs[i];
            _tabActions[i] = i == SkillTab ? () => SkillScreen.Open() : () => { if (_expanded && SelectedPage == page) SetExpanded(false); else ShowPage(page); };
            _tabs[i].onClick.AddListener(_tabActions[i]);
        }
        _closeAction = () => SetExpanded(!_expanded);
        layout.Close.onClick.AddListener(_closeAction);
        _summaryView = new PersistentSummaryViewAdapter(layout.Summary, _font, layout.WalletItems, layout.TextRow,
            layout.Save != null ? (RectTransform)layout.Save.transform : null,
            layout.ReturnToSlots != null ? (RectTransform)layout.ReturnToSlots.transform : null);
        layout.PlacementBuilder.Configure(_font);
        _view.ConfigureHud(_font, layout.Resources);
        _equipmentView = new EquipmentDetailsViewAdapter(layout.EquipmentHeading, layout.EquipmentGroup,
            layout.EquipmentText, layout.Upgrade, layout.EquipmentFeedback);
        _equipmentActionLabel = layout.Upgrade.GetComponentInChildren<Text>(true).text;
        _equipmentTextMinHeight = layout.EquipmentText.GetComponent<LayoutElement>().minHeight;
        _equipmentPresenter = new EquipmentDetailsPresenterAdapter(_equipmentView);
        SkillScreen.VisibilityChanged += SkillVisibilityChanged;
        ShowPage(0);
    }
}
