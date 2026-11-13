using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>一回のプレイを構成し、準備した参照を既存の機能へ渡す起動入口。</summary>
public sealed class GameInitializerAdapter : MonoBehaviour
{
    [SerializeField] private GridFieldDatabaseAdapter field;
    [SerializeField] private SingularityStartDefinition initialLayout;
    [SerializeField] private SkillTreeDefinition skillTree;
    [SerializeField] private ResourceDefinition resources;
    [SerializeField] private EconomyPanelAdapter economyPanel;
    [SerializeField] private CellPlacerAdapter placer;
    [SerializeField] private CellStatusViewAdapter statusView;
    [SerializeField] private CameraControllerAdapter cameraController;
    [SerializeField] private PlayerCursorBehaviourAdapter cursor;
    [SerializeField] private StartSiteSelectionAdapter selection;

    public GameStartApplication Session { get; } = new();
    public CurrencyWalletApplication Wallet { get; private set; }
    public CurrencyPriceCatalogDomain Prices { get; private set; }
    public ConstructionStockApplication ConstructionStock { get; private set; }
    public DeliveryProgressApplication Deliveries { get; private set; }
    public SkillTreeApplication Skills { get; private set; }

    private void Start() => TryPrepareNewGame();

    public bool TryPrepareNewGame()
    {
        if (Session.CurrentStage != GameStartApplication.Stage.Uninitialized) return false;
        try
        {
            ValidateConfiguration();
            var candidates = DiscoverStartSites(field, initialLayout);
            cursor.ConfigureStartup(Session, selection, cameraController);
            Session.Prepare(candidates);
            selection.Begin(Session, initialLayout.SingularityPrefab.Footprint, () => TryStartGame());
            return true;
        }
        catch (Exception exception) { Fail("preparation", exception); return false; }
    }

    public static IReadOnlyList<StartSiteRulesDomain.Candidate> DiscoverStartSites(
        GridFieldDatabaseAdapter field, SingularityStartDefinition layout)
    {
        layout.Validate(field.GeneratedSize);
        var prices = layout.CreateCatalog();
        prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood, 1, out var extractor);
        prices.TryGetPlacementAmount(CellTypeDomain.Conveyor, 1, out var belt);
        return StartSiteRulesDomain.Candidates(field.CaptureTerrain(), field.GenerationSeed,
            layout.SingularityPrefab.Footprint, layout.ShippingCell(Vector2Int.zero), prices.InitialBalance, extractor, belt);
    }

    public bool TryStartGame()
    {
        if (!Session.TryBegin(out var candidate)) return false;
        try
        {
            // 予測できる設定不足を、地形を置き換える前に検出する。
            ValidateConfiguration();
            selection.RefreshSelection();
            var prices = initialLayout.CreateCatalog();
            var wallet = new CurrencyWalletApplication(prices.InitialBalance);
            var stock = new ConstructionStockApplication(resources.GetAllResources().Select(r => r.ResourceTypeDomain));
            var deliveries = new DeliveryProgressApplication(initialLayout.CreateDeliveryCatalog());
            var skillCatalog = skillTree.CreateCatalog();
            var cells = new InitialCellPlacementAdapter().Place(field, initialLayout, new Vector2Int(candidate.X, candidate.Z));
            field.InitializeCells(field.GeneratedSize);
            Prices = prices; Wallet = wallet; ConstructionStock = stock; Deliveries = deliveries;
            Skills = new SkillTreeApplication(skillCatalog, Wallet, field.StatEffects);
            ConfigureGameplay(cells.core);
            cameraController.FocusOn(cells.core.WorldCenter);
            Session.Complete();
            selection.Finish();
            return true;
        }
        catch (Exception exception) { Fail("start", exception); return false; }
    }

    // 新規開始の初期配置・初期値付与と、既存機能への配線を分けておく。
    private void ConfigureGameplay(SingularityCellAdapter core)
    {
        placer.Configure(Wallet, Prices, ConstructionStock, resources);
        core.Configure(Deliveries); core.InitializeSystem();
        foreach (var shipping in field.Owners.OfType<ShippingCellAdapter>())
        { shipping.Configure(Wallet, Prices); shipping.InitializeSystem(); }
        foreach (var depot in field.Owners.OfType<ConstructionDepotCellAdapter>())
        { depot.Configure(ConstructionStock); depot.InitializeSystem(); }
        economyPanel.Configure(Wallet, Skills, statusView, placer, Deliveries);
    }

    private void ValidateConfiguration()
    {
        var missing = new List<string>();
        if (field == null) missing.Add(nameof(field));
        if (initialLayout == null) missing.Add(nameof(initialLayout));
        if (skillTree == null) missing.Add(nameof(skillTree));
        if (resources == null) missing.Add(nameof(resources));
        if (economyPanel == null) missing.Add(nameof(economyPanel));
        if (placer == null) missing.Add(nameof(placer));
        if (statusView == null) missing.Add(nameof(statusView));
        if (cameraController == null) missing.Add(nameof(cameraController));
        if (cursor == null) missing.Add(nameof(cursor));
        if (selection == null) missing.Add(nameof(selection));
        if (missing.Count > 0)
            throw new MissingReferenceException($"{nameof(GameInitializerAdapter)}: missing references [{string.Join(", ", missing)}] on '{name}' (Scene: '{gameObject.scene.path}').");
        initialLayout.Validate(field.GeneratedSize);
        _ = skillTree.CreateCatalog();
        _ = new ConstructionStockApplication(resources.GetAllResources().Select(r => r.ResourceTypeDomain));
        selection.ValidateBindings();
    }
    private void Fail(string stage, Exception exception)
    {
        // 最初の例外を先に記録し、調査対象のSceneとUIはそのまま残す。
        Debug.LogError($"Game initialization failed during {stage}: {exception}", this);
        Session.Fail();
    }
    private void OnDestroy() { if (selection != null) selection.DisposePreviews(); }
}
