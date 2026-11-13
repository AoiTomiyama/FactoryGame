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
    [SerializeField] private GameSaveAdapter saves;

    public GameStartApplication Session { get; } = new();
    public CurrencyWalletApplication Wallet { get; private set; }
    public CurrencyPriceCatalogDomain Prices { get; private set; }
    public ConstructionStockApplication ConstructionStock { get; private set; }
    public DeliveryProgressApplication Deliveries { get; private set; }
    public SkillTreeApplication Skills { get; private set; }
    public GameSaveAdapter Saves => saves;
    public IEnumerable<ResourceTypeDomain> RegisteredResources => resources.GetAllResources().Select(r => r.ResourceTypeDomain);

    private void Start()
    {
        try
        {
            ValidateConfiguration();
            cursor.ConfigureStartup(Session,selection,cameraController);
            saves.Configure(this,new SaveWorldAdapter(field,placer),selection.Layout.Save);
            Session.ShowMenu();
            selection.BeginMenu(() => TryPrepareNewGame(),() => TryRestoreGame(),saves.Storage.Exists);
        }
        catch (Exception exception) { Fail("preparation",exception); }
    }

    public bool TryPrepareNewGame()
    {
        if (Session.CurrentStage is not (GameStartApplication.Stage.Uninitialized or GameStartApplication.Stage.Menu)) return false;
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

    public bool TryRestoreGame()
    {
        if (Session.CurrentStage != GameStartApplication.Stage.Menu) return false;
        try
        {
            ValidateConfiguration();
            var data = saves.Read();
            var world = new SaveWorldAdapter(field,placer);
            var catalog = skillTree.CreateCatalog();
            // ファイルと定義の検査を終えるまで、Sceneの地形を変更しない。
            world.Validate(data,catalog,RegisteredResources);
            if (!Session.TryBeginRestore()) return false;
            var cells = world.BuildCells(data);
            Prices = initialLayout.CreateCatalog(); Wallet = new CurrencyWalletApplication(data.balance);
            ConstructionStock = new ConstructionStockApplication(RegisteredResources);
            ConstructionStock.RestoreStored(SaveGameApplication.StockValues(data.construction));
            Deliveries = new DeliveryProgressApplication(initialLayout.CreateDeliveryCatalog());
            Deliveries.RestoreDelivered(SaveGameApplication.StockValues(data.deliveries));
            Skills = new SkillTreeApplication(catalog,Wallet,field.StatEffects);
            Skills.RestoreOwned(data.skills);
            world.RestoreCellStates(data,cells);
            var core = cells.OfType<SingularityCellAdapter>().Single();
            ConfigureGameplay(core);
            cameraController.FocusOn(core.WorldCenter);
            Session.Complete(); selection.Finish();
            return true;
        }
        catch (Exception exception) { Fail("load",exception); return false; }
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
        core.Configure(Deliveries);
        foreach (var shipping in field.Owners.OfType<ShippingCellAdapter>())
        { shipping.Configure(Wallet, Prices); }
        foreach (var depot in field.Owners.OfType<ConstructionDepotCellAdapter>())
        { depot.Configure(ConstructionStock); }
        // 全在庫・能力・受入先が揃ってから接続と非同期処理を開始する。
        foreach (var cell in field.Owners) cell.InitializeSystem();
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
        if (saves == null) missing.Add(nameof(saves));
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
