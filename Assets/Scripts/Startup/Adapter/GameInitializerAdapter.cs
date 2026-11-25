using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>一回のプレイを構成し、準備した参照を既存の機能へ渡す起動入口。</summary>
public sealed class GameInitializerAdapter : MonoBehaviour
{
    [SerializeField] private GridFieldDatabaseAdapter field;
    [SerializeField] private GridFieldGeneratorAdapter terrainGenerator;
    [SerializeField] private SingularityStartDefinition initialLayout;
    [SerializeField] private SkillTreeDefinition skillTree;
    [SerializeField] private ResourceDefinition resources;
    [SerializeField] private EconomyPanelAdapter economyPanel;
    [SerializeField] private CellPlacerAdapter placer;
    [SerializeField] private CellStatusViewAdapter statusView;
    [SerializeField] private CameraControllerAdapter cameraController;
    [SerializeField] private PlayerCursorBehaviourAdapter cursor;
    [SerializeField] private StartupHudAdapter startupHud;
    [SerializeField] private GameSaveAdapter saves;
    private SaveSlotMenuAdapter _saveMenu;
    private (DateTime? Date, string Name, int? Seed)[] _slots;
    private int _setupSlot;
    private CancellationTokenSource _newGameAttempt;
    private SaveWorldAdapter _world;
    private int _fieldSize;
    private float? _timeScaleBeforeSlotSelection;
    private bool _returning;
    internal bool SaveMenuBlocksInput => _saveMenu?.BlocksGameplay ?? false;

    public GameStartApplication Session { get; } = new();
    public CurrencyWalletApplication Wallet { get; private set; }
    public IncomeRateApplication IncomeRate { get; private set; }
    public CurrencyPriceCatalogDomain Prices { get; private set; }
    public ConstructionStockApplication ConstructionStock { get; private set; }
    public DeliveryProgressApplication Deliveries { get; private set; }
    public SkillTreeApplication Skills { get; private set; }
    public GameSaveAdapter Saves => saves;
    public IEnumerable<ResourceTypeDomain> RegisteredResources => resources.GetAllResources().Select(r => r.ResourceTypeDomain);

    private async void Start()
    {
        var cancellation = destroyCancellationToken;
        try
        {
            ValidateConfiguration();
            cursor.ConfigureStartup(Session, cameraController);
            _world = new SaveWorldAdapter(field,placer);
            saves.Configure(this,_world,startupHud.Layout.Save);
            cameraController.ConfigureInputGate(() => Session.CurrentStage != GameStartApplication.Stage.Running || SaveMenuBlocksInput);
            _saveMenu = startupHud.SaveSlotMenu;
            _saveMenu.Configure(startupHud.Layout, ChooseSlot, () => ChooseSlot(0),
                () => OpenReturnMenu(), save => TryReturnToSaveSlotsAsync(save).Forget(), () => TryReturnToSaveSlotsAsync(false).Forget(),
                (name, seed) => TryStartNewGameAsync(_setupSlot, name, seed).Forget(), CancelNewGame);
            startupHud.BeginMenu();
            _slots = await saves.ReadSlotsAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            _fieldSize = field.GeneratedSize;
            Session.ShowMenu();
            _saveMenu.ShowSlots(_slots);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) { Fail("preparation",exception); }
    }

    // HUDを開閉してもプレイ中の計測を続ける。
    private void Update()
    {
        if (Session.CurrentStage == GameStartApplication.Stage.Running) IncomeRate?.Advance(Time.unscaledTimeAsDouble);
    }

    private void ChooseSlot(int slot)
    {
        if (SaveSlotCatalogApplication.IsNormal(slot) && !_slots[slot - SaveSlotCatalogApplication.FirstSlot].Date.HasValue)
        {
            if (!Session.TryOpenNewGame(slot)) return;
            _setupSlot = slot;
            _saveMenu.ShowNewGame(slot);
            return;
        }
        var switching = Session.TryBeginSlotSwitch();
        if (!switching && Session.CurrentStage != GameStartApplication.Stage.Menu) return;
        if (slot == 0) TryStartDebugAsync().Forget();
        else TryRestoreGameAsync(slot).Forget();
    }

    private void CancelNewGame()
    {
        if (!Session.TryCancelNewGame()) return;
        _newGameAttempt?.Cancel();
        _saveMenu.ShowSlots(_slots);
    }

    public async UniTask<bool> TryStartNewGameAsync(int slot, string name, int? seed)
    {
        if (!SaveSlotCatalogApplication.IsNormal(slot) || _slots == null ||
            _slots[slot - SaveSlotCatalogApplication.FirstSlot].Date.HasValue) return false;
        if (Session.TryOpenNewGame(slot))
        {
            _setupSlot = slot;
            _saveMenu.ShowNewGame(slot);
        }
        if (!Session.TryBeginNewGame(slot)) return false;
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        _newGameAttempt = attempt;
        var cancellation = attempt.Token;
        try
        {
            _saveMenu.ShowTerrainPreparation();
            await UniTask.NextFrame(cancellationToken: cancellation);
            ValidateConfiguration();
            // 乱数は Unity の共有状態に触れず、空欄で始める操作ごとに一つ引く。
            var actualSeed = seed ?? RandomSeed();
            var terrain = await terrainGenerator.BuildTerrainAsync(_fieldSize, actualSeed, cancellation);
            var footprint = initialLayout.SingularityPrefab.Footprint;
            var shipping = initialLayout.ShippingCell(Vector2Int.zero);
            var prices = initialLayout.CreateCatalog();
            var site = await System.Threading.Tasks.Task.Run(
                () => FindStartSite(terrain, actualSeed, footprint, shipping, prices, cancellation), cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (site == null)
            {
                Session.NewGameNeedsAnotherSeed();
                _saveMenu.ShowNewGameForm(seed.HasValue ? "このシードでは始められる場所がありません" : "もう一度始めると別の地形を試せます");
                return false;
            }

            // 開始できる地形が確定してから、表示中の工場を入れ替える。
            Session.BeginPreparedGame();
            _saveMenu.SetBusy("地形を準備中…");
            PrepareWorldSwitch();
            await _world.BuildInitialTerrainAsync(terrain, actualSeed, cancellation);
            saves.SelectSlot(slot, name);
            StartGame(site, prices);
            _saveMenu.SetDebugSession(false);
            _saveMenu.Hide();
            startupHud.Finish();
            ResumeSlotSelectionTime();
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception exception)
        {
            if (cancellation.IsCancellationRequested) return false;
            Fail("start", exception);
            _saveMenu.ShowFailure("ゲームを始められませんでした");
            return false;
        }
        finally
        {
            if (ReferenceEquals(_newGameAttempt, attempt)) _newGameAttempt = null;
        }
    }

    private static int RandomSeed()
    {
        var bytes = new byte[sizeof(int)];
        using var random = System.Security.Cryptography.RandomNumberGenerator.Create();
        random.GetBytes(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private void PrepareWorldSwitch()
    {
        economyPanel.SkillScreen.ReleaseWorld();
        cameraController.RestoreOverview();
        economyPanel.ReleaseWorld();
        startupHud.BeginMenu();
        placer.ResetWorldSelection();
        Wallet = null; IncomeRate = null; Prices = null;
        ConstructionStock = null; Deliveries = null; Skills = null;
    }

    public UniTask<bool> TryRestoreGameAsync(int slot = SaveSlotCatalogApplication.FirstSlot) =>
        SaveSlotCatalogApplication.IsNormal(slot) ? RestoreAsync(slot, debug: false) : UniTask.FromResult(false);

    public UniTask<bool> TryStartDebugAsync() => RestoreAsync(0, debug: true);

    private async UniTask<bool> RestoreAsync(int slot, bool debug)
    {
        var switching = Session.CurrentStage == GameStartApplication.Stage.SwitchingSlot;
        if (!Session.TryBeginRestore()) return false;
        var cancellation = destroyCancellationToken;
        try
        {
            _saveMenu?.SetBusy("ロード中…");
            await UniTask.NextFrame(cancellationToken: cancellation);
            ValidateConfiguration();
            var deliveryCatalog = initialLayout.CreateDeliveryCatalog();
            var data = debug
                ? DebugWorldSeedAdapter.Create(_fieldSize, placer)
                : await saves.ReadAsync(slot, cancellation);
            cancellation.ThrowIfCancellationRequested();
            var catalog = skillTree.CreateCatalog();
            // ファイルと定義の検査を終えるまで、Sceneの地形を変更しない。
            var owners = await _world.ValidateAsync(data,catalog,RegisteredResources,deliveryCatalog,
                cancellation,includeOccupancy: true);
            if (switching) PrepareWorldSwitch();
            if (debug) saves.SelectDebug();
            else saves.SelectSlot(slot, data.displayName);
            var cells = await _world.BuildCellsAsync(data,owners,cancellation);
            Prices = initialLayout.CreateCatalog(); Wallet = new CurrencyWalletApplication(data.balance);
            ConstructionStock = new ConstructionStockApplication(RegisteredResources);
            ConstructionStock.RestoreStored(SaveGameApplication.StockValues(data.construction));
            Deliveries = new DeliveryProgressApplication(deliveryCatalog);
            Deliveries.RestoreDelivered(SaveGameApplication.StockValues(data.deliveries));
            Skills = new SkillTreeApplication(catalog,Wallet,field.StatEffects,Deliveries);
            Skills.RestoreOwned(data.skills);
            await _world.RestoreCellStatesAsync(data,cells,cancellation);
            var core = cells.OfType<SingularityCellAdapter>().Single();
            await UniTask.NextFrame(cancellationToken: cancellation);
            ConfigureGameplayEndpoints(core, cells);
            ConfigureEconomy();
            await UniTask.NextFrame(cancellationToken: cancellation);
            ConfigureSkills();
            await UniTask.NextFrame(cancellationToken: cancellation);
            // 接続済みセルの搬送が初期化途中に進まないよう、稼働開始は最後の同じフレームで行う。
            InitializeCellSystems(cells);
            cameraController.FocusOn(core.WorldCenter);
            Session.Complete();
            _saveMenu?.SetDebugSession(debug);
            _saveMenu?.Hide();
            startupHud.Finish();
            ResumeSlotSelectionTime();
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception exception)
        {
            if (cancellation.IsCancellationRequested) return false;
            Fail("load",exception);
            _saveMenu?.ShowFailure("ロード失敗");
            return false;
        }
    }

    public bool OpenReturnMenu()
    {
        if (Session.CurrentStage != GameStartApplication.Stage.Running || _returning) return false;
        cursor.CancelCurrentGesture();
        _saveMenu.ShowReturn(!saves.IsDebug);
        return true;
    }

    public async UniTask<bool> TryReturnToSaveSlotsAsync(bool save)
    {
        var wasRunning = Session.CurrentStage == GameStartApplication.Stage.Running;
        if (_returning || (!wasRunning && Session.CurrentStage != GameStartApplication.Stage.Failed) ||
            (save && (!wasRunning || saves.IsDebug))) return false;
        _returning = true;
        var cancellation = destroyCancellationToken;
        try
        {
            if (wasRunning && (save || saves.IsSaving))
            {
                _saveMenu.SetBusy("保存中…");
                if (!await saves.CompleteSaveBeforeReturn(save))
                {
                    _saveMenu.SetReturnFailure("保存失敗");
                    return false;
                }
            }
            _saveMenu.SetBusy("セーブ選択へ戻っています…");
            _slots = await saves.ReadSlotsAsync(cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!Session.TryShowSlotSelection()) return false;
            // 一覧を開いている間は今のフィールドを描画し、ゲーム内の時間だけを止める。
            _timeScaleBeforeSlotSelection ??= Time.timeScale;
            Time.timeScale = 0;
            _saveMenu.ShowSlots(_slots);
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception exception)
        {
            Debug.LogError($"Returning to save slots failed: {exception}", this);
            _saveMenu.SetReturnFailure("セーブ選択へ戻れませんでした");
            return false;
        }
        finally { _returning = false; }
    }

    public static StartSiteRulesDomain.Candidate FindStartSite(
        GridFieldDatabaseAdapter field, SingularityStartDefinition layout)
    {
        layout.Validate(field.GeneratedSize);
        return FindStartSite(field.CaptureTerrain(), field.GenerationSeed,
            layout.SingularityPrefab.Footprint, layout.ShippingCell(Vector2Int.zero), layout.CreateCatalog());
    }

    private static StartSiteRulesDomain.Candidate FindStartSite(StartSiteRulesDomain.Terrain[,] terrain,
        int seed, CellFootprintDomain footprint, (int x, int z) shipping, CurrencyPriceCatalogDomain prices,
        CancellationToken cancellation = default)
    {
        prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood, 1, out var extractor);
        prices.TryGetPlacementAmount(CellTypeDomain.Conveyor, 1, out var belt);
        return StartSiteRulesDomain.Find(terrain, seed, footprint, shipping, prices.InitialBalance, extractor, belt, cancellation);
    }

    private void StartGame(StartSiteRulesDomain.Candidate site, CurrencyPriceCatalogDomain prices)
    {
        var wallet = new CurrencyWalletApplication(prices.InitialBalance);
        var stock = new ConstructionStockApplication(resources.GetAllResources().Select(r => r.ResourceTypeDomain));
        var deliveries = new DeliveryProgressApplication(initialLayout.CreateDeliveryCatalog());
        var skillCatalog = skillTree.CreateCatalog();
        var cells = new InitialCellPlacementAdapter().Place(field, initialLayout, new Vector2Int(site.X, site.Z));
        Prices = prices; Wallet = wallet; ConstructionStock = stock; Deliveries = deliveries;
        Skills = new SkillTreeApplication(skillCatalog, Wallet, field.StatEffects, Deliveries);
        ConfigureGameplay(cells.core);
        cameraController.FocusOn(cells.core.WorldCenter);
        Session.Complete();
    }

    // 新規開始の初期配置・初期値付与と、既存機能への配線を分けておく。
    private void ConfigureGameplay(SingularityCellAdapter core)
    {
        var cells = field.Owners;
        ConfigureGameplayEndpoints(core, cells);
        InitializeCellSystems(cells);
        ConfigureEconomy();
        ConfigureSkills();
    }

    private void ConfigureGameplayEndpoints(SingularityCellAdapter core, IEnumerable<CellBaseAdapter> cells)
    {
        placer.Configure(Wallet, Prices, ConstructionStock, resources);
        placer.ConfigureClearing(new TerrainClearingAdapter(field, (EmptyCellAdapter)placer.FieldPrefab(CellTypeDomain.Empty),
            initialLayout.CreateClearingRules(), Wallet, () => Skills.IsOwned(TerrainClearingApplication.SkillUid)));
        core.Configure(Deliveries);
        foreach (var shipping in cells.OfType<ShippingCellAdapter>())
        { shipping.Configure(Wallet, Prices); }
        foreach (var depot in cells.OfType<ConstructionDepotCellAdapter>())
        { depot.Configure(ConstructionStock); }
    }

    private static void InitializeCellSystems(IEnumerable<CellBaseAdapter> cells)
    {
        foreach (var cell in cells.OfType<ConnectableCellBaseAdapter>()) cell.InitializeSystem();
    }

    private void ConfigureEconomy()
    {
        IncomeRate = new IncomeRateApplication(Wallet, Time.unscaledTimeAsDouble);
        economyPanel.Configure(Wallet, IncomeRate, statusView, placer, Deliveries);
        economyPanel.ConfigureReferences(RecipeReferenceCatalogAdapter.Create(placer.FieldPrefabs(), resources, Prices));
    }

    private void ConfigureSkills()
    {
        var hud = startupHud.Layout;
        var screen = economyPanel.SkillScreen;
        screen.Configure(Wallet, Skills, Deliveries, field.StatEffects, hud, () => placer.ClearingPriceText,
            cursor.CancelCurrentGesture, new[] { hud.GameplayRoot, statusView.transform, economyPanel.Operation.transform });
        cursor.ConfigureInputGate(() => SaveMenuBlocksInput || screen.BlocksGameplayInput);
        cameraController.ConfigureInputGate(() => SaveMenuBlocksInput || screen.BlocksGameplayInput);
    }

    private void ValidateConfiguration()
    {
        var missing = new List<string>();
        if (field == null) missing.Add(nameof(field));
        if (terrainGenerator == null) missing.Add(nameof(terrainGenerator));
        if (initialLayout == null) missing.Add(nameof(initialLayout));
        if (skillTree == null) missing.Add(nameof(skillTree));
        if (resources == null) missing.Add(nameof(resources));
        if (economyPanel == null) missing.Add(nameof(economyPanel));
        if (placer == null) missing.Add(nameof(placer));
        if (statusView == null) missing.Add(nameof(statusView));
        if (cameraController == null) missing.Add(nameof(cameraController));
        if (cursor == null) missing.Add(nameof(cursor));
        if (startupHud == null) missing.Add(nameof(startupHud));
        if (saves == null) missing.Add(nameof(saves));
        if (missing.Count > 0)
            throw new MissingReferenceException($"{nameof(GameInitializerAdapter)}: missing references [{string.Join(", ", missing)}] on '{name}' (Scene: '{gameObject.scene.path}').");
        initialLayout.Validate(field.GeneratedSize);
        skillTree.CreateCatalog().ValidateDeliveryPrerequisites(initialLayout.CreateDeliveryCatalog());
        _ = initialLayout.CreateClearingRules();
        _ = new ConstructionStockApplication(resources.GetAllResources().Select(r => r.ResourceTypeDomain));
        startupHud.ValidateBindings();
        if (economyPanel.SkillScreen == null || startupHud.Layout.GameplayRoot == null)
            throw new MissingReferenceException("GameInitializerAdapter: assign the full-screen skill UI and gameplay HUD root.");
        economyPanel.SkillScreen.ValidateBindings();
    }
    private void Fail(string stage, Exception exception)
    {
        // 最初の例外を先に記録し、調査対象のSceneとUIはそのまま残す。
        Debug.LogError($"Game initialization failed during {stage}: {exception}", this);
        Session.Fail();
    }
    private void ResumeSlotSelectionTime()
    {
        if (_timeScaleBeforeSlotSelection is { } before) Time.timeScale = before;
        _timeScaleBeforeSlotSelection = null;
    }

    private void OnDestroy()
    {
        ResumeSlotSelectionTime();
    }
}
