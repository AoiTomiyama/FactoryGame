#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture, Category("TerrainClearing")]
public sealed class TerrainClearingPlayModeTests : FactoryPlayModeScope
{
    private GridFieldDatabaseAdapter _grid;
    private CurrencyWalletApplication _wallet;
    private TerrainClearingAdapter _clearing;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnitySetUp] public IEnumerator CreateWorld()
    {
        _grid = new GameObject("ClearingGrid").AddComponent<GridFieldDatabaseAdapter>();
        _grid.InitializeCells(12);
        _wallet = new CurrencyWalletApplication(100);
        _clearing = new TerrainClearingAdapter(_grid, Prefab<EmptyCellAdapter>("Empty"),
            new TerrainClearingRulesDomain(20,40), _wallet, () => true);
        yield return null;
    }
    private static T Prefab<T>(string name) where T : CellBaseAdapter =>
        AssetDatabase.LoadAssetAtPath<T>("Assets/Prefabs/Field/General/F_" + name + "Cell.prefab");
    private T Add<T>(string name, int x, int z, bool initialize = true) where T : CellBaseAdapter
    {
        var cell = Object.Instantiate(Prefab<T>(name),new Vector3(x,0,z),Quaternion.identity,_grid.transform);
        _grid.SaveCell(x,z,cell);
        if (initialize) cell.InitializeSystem();
        return cell;
    }

    [UnityTest] public IEnumerator MixedSelectionClearsOnlyWoodAndStoneAndPreservesMineralsAndEquipment()
    {
        var wood = Add<ResourceCellAdapter>("ResourceWood",1,1);
        var stone = Add<ResourceCellAdapter>("ResourceStone",2,1);
        var minerals = new[] { Add<ResourceCellAdapter>("ResourceIron",3,1),
            Add<ResourceCellAdapter>("ResourceCopper",4,1),Add<ResourceCellAdapter>("ResourceCoal",5,1) };
        var equipment = Add<StorageCellAdapter>("Storage",6,1);
        var preview = _clearing.CreatePreview(Enumerable.Range(1,6).Select(x => (x,1)).Concat(new[] { (1,1) }));
        Assert.That(preview.Quote.Cost, Is.EqualTo(60)); Assert.That(preview.Quote.Targets.Count, Is.EqualTo(2));
        Assert.That(_clearing.Execute(preview), Is.EqualTo(TerrainClearingApplication.Result.Success));
        Assert.That(_wallet.Current.Balance, Is.EqualTo(40));
        Assert.That(_grid.GetCell(1,1), Is.TypeOf<EmptyCellAdapter>()); Assert.That(_grid.GetCell(2,1), Is.TypeOf<EmptyCellAdapter>());
        foreach (var cell in minerals.Append<CellBaseAdapter>(equipment)) Assert.That(_grid.GetCell(cell.XIndex,cell.ZIndex), Is.SameAs(cell));
        Assert.That(wood.gameObject.activeSelf || stone.gameObject.activeSelf, Is.False);
        yield return null;
        Assert.That(wood == null && stone == null);
    }

    [UnityTest] public IEnumerator StaleSelectionDoesNotOverwriteAReplacementOrSpendMoney()
    {
        var source = Add<ResourceCellAdapter>("ResourceWood",2,2);
        var preview = _clearing.CreatePreview(new[] { (2,2) });
        var replacement = Add<EmptyCellAdapter>("Empty",2,2);
        Assert.That(_clearing.Execute(preview), Is.EqualTo(TerrainClearingApplication.Result.InvalidTarget));
        Assert.That(_grid.GetCell(2,2), Is.SameAs(replacement)); Assert.That(source.gameObject.activeSelf);
        Assert.That(_wallet.Current.Balance, Is.EqualTo(100)); Assert.That(_wallet.Current.ReservedExpense, Is.Zero);
        yield return null;
    }

    [UnityTest] public IEnumerator PlacerCancellationRestoresModelsAndInsufficientFundsKeepTheWholeRange()
    {
        var wood = Add<ResourceCellAdapter>("ResourceWood",2,2); var stone = Add<ResourceCellAdapter>("ResourceStone",3,2);
        var placer = new GameObject("ClearingPlacer").AddComponent<CellPlacerAdapter>();
        typeof(CellPlacerAdapter).GetField("cellDatabaseArr",Fields).SetValue(placer,
            new[] { AssetDatabase.LoadAssetAtPath<CellDatabaseDefinition>("Assets/Scripts/ScriptableObject/GeneralCellDB.asset") });
        var resources = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset");
        var prices = AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset").CreateCatalog();
        placer.Configure(_wallet,prices,new ConstructionStockApplication(resources.GetAllResources().Select(r => r.ResourceTypeDomain)),resources);
        placer.ConfigureClearing(_clearing);
        yield return null;
        placer.SelectClearing(); placer.SetPlacementEnabled(true); placer.SelectGrid(wood.gameObject); placer.PointerBegin();
        placer.SelectGrid(stone.gameObject); placer.PointerDrag();
        Assert.That(placer.PreviewCount, Is.EqualTo(2)); Assert.That(placer.PreviewCost, Is.EqualTo(60));
        Assert.That(wood.isActiveAndEnabled && stone.isActiveAndEnabled);
        placer.SetPlacementEnabled(false); placer.PointerEnd();
        Assert.That(wood.CellModel.activeSelf && stone.CellModel.activeSelf); Assert.That(_wallet.Current.Balance, Is.EqualTo(100));
        Assert.That(_wallet.TryReserveExpense(41,out var held)); held.TryCommit();
        placer.SetPlacementEnabled(true); placer.SelectGrid(wood.gameObject); placer.PointerBegin();
        placer.SelectGrid(stone.gameObject); placer.PointerDrag(); placer.PointerEnd();
        Assert.That(placer.LastClearingResult, Is.EqualTo(TerrainClearingApplication.Result.InsufficientFunds));
        Assert.That(_grid.GetCell(2,2), Is.SameAs(wood)); Assert.That(_grid.GetCell(3,2), Is.SameAs(stone));
        Assert.That(wood.CellModel.activeSelf && stone.CellModel.activeSelf); Assert.That(_wallet.Current.Balance, Is.EqualTo(59));
        placer.SelectGrid(wood.gameObject); placer.PointerBegin(); placer.PointerEnd();
        Assert.That(placer.LastClearingResult, Is.EqualTo(TerrainClearingApplication.Result.Success));
        Assert.That(_wallet.Current.Balance, Is.EqualTo(39)); Assert.That(_grid.GetCell(2,2), Is.TypeOf<EmptyCellAdapter>());
        yield return null;
    }

    [UnityTest] public IEnumerator ClearingAnActiveSourceCancelsItsJobImmediatelyAndKeepsFinishedInventory()
    {
        Add<ResourceCellAdapter>("ResourceWood",4,4);
        var extractor = Add<ExtractorCellAdapter>("ExtractorWood",4,3,false);
        extractor.RestoreState(new SaveGameApplication.Cell { inventory = new[] { SaveGameApplication.SlotValue(0,ResourceTypeDomain.Wood,1) } });
        extractor.InitializeSystem();
        for (var i=0;i<5 && extractor.CaptureJob()==null;i++) yield return null;
        Assert.That(extractor.CaptureJob(), Is.Not.Null);
        var preview = _clearing.CreatePreview(new[] { (4,4) });
        Assert.That(extractor.CaptureJob(), Is.Not.Null,"a preview does not cancel extraction");
        Assert.That(_clearing.Execute(preview), Is.EqualTo(TerrainClearingApplication.Result.Success));
        Assert.That(extractor.CaptureJob(), Is.Null,"a same-frame save must not contain the removed source job");
        Assert.That(extractor.CurrentLoad, Is.EqualTo(1)); Assert.That(extractor.CaptureInventory().Single().amount, Is.EqualTo(1));
        Add<ResourceCellAdapter>("ResourceWood",4,7);
        var resumed = Add<ExtractorCellAdapter>("ExtractorWood",4,6,false);
        resumed.RestoreState(new SaveGameApplication.Cell { inventory = Array.Empty<SaveGameApplication.Slot>(),
            job = new SaveGameApplication.Job { amount = 1, seconds = 10, elapsed = 2,
                result = SaveKeysApplication.Key(ResourceTypeDomain.Wood), inputs = Array.Empty<SaveGameApplication.Slot>() } });
        resumed.InitializeSystem();
        Assert.That(resumed.CaptureJob(), Is.Not.Null);
        Assert.That(_clearing.Execute(_clearing.CreatePreview(new[] { (4,7) })), Is.EqualTo(TerrainClearingApplication.Result.Success));
        Assert.That(resumed.CaptureJob(), Is.Null,"a restored job must also disappear before its coroutine resumes");
        yield return null;
    }
}

[TestFixture, Category("TerrainClearing")]
public sealed class TerrainClearingHudPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    protected override bool ChooseNewGame => false;
    protected override int? FixedTerrainSeed => 423794866;

    [UnityTest] public IEnumerator UnlockControlsFitAndClearedCellsSurviveFreshSceneLoad() => Verify().ToCoroutine();
    private async UniTask Verify()
    {
        var game = TestScenes.Find<GameInitializerAdapter>();
        Assert.That(await game.TryStartNewGameAsync(1, null, FixedTerrainSeed));
        await UniTask.DelayFrame(2);
        var placer = TestScenes.Find<CellPlacerAdapter>();
        var panel = TestScenes.Find<EconomyPanelAdapter>();
        var layout = TestScenes.Find<HudLayoutAdapter>();
        var operation = TestScenes.Find<OperationBarAdapter>();
        var clearingButton = TestScenes.All<CellSelectButtonUIAdapter>(FindObjectsInactive.Include).Single(b => b.name == "TerrainClearingButton");
        Assert.That(placer.ClearingAvailable, Is.False); Assert.That(clearingButton.gameObject.activeSelf, Is.False);
        Assert.That(game.Wallet.TryReserveIncome(1000,out var income)); income.TryCommit();
        Assert.That(game.Skills.Purchase("basic-research"), Is.EqualTo(SkillPurchaseResultApplication.Success));
        var uid = TerrainClearingApplication.SkillUid;
        var screen = panel.SkillScreen;
        screen.Open();
        Assert.That(screen.IsNodeVisible(uid), Is.False);
        foreach (var pair in new[] { (ResourceTypeDomain.Wood,40L), (ResourceTypeDomain.Stone,40L),
            (ResourceTypeDomain.Plank,30L), (ResourceTypeDomain.StoneBrick,20L) })
        { Assert.That(game.Deliveries.TryReserve(pair.Item1,pair.Item2,out var delivered)); delivered.TryCommit(); }
        Assert.That(screen.IsNodeVisible(uid), Is.True,"delivery reveals the clearing node without reopening");
        screen.Select(uid);
        Assert.That(screen.Description.text, Does.Not.Contain("×"));
        screen.PurchaseButton.onClick.Invoke();
        Assert.That(game.Skills.IsOwned(uid));
        screen.Close(); panel.ShowPage(0);
        await UniTask.DelayFrame(2);
        Assert.That(clearingButton.gameObject.activeSelf);
        clearingButton.GetComponentInChildren<Button>(true).onClick.Invoke();
        Assert.That(placer.IsClearingSelected);
        var grid = GridFieldDatabaseAdapter.Instance;
        var resources = new[] { ResourceTypeDomain.Wood,ResourceTypeDomain.Stone }.Select(type =>
            grid.Owners.OfType<ResourceCellAdapter>().OrderBy(c => c.XIndex).ThenBy(c => c.ZIndex).First(c => c.ResourceTypeDomain == type)).ToArray();
        var coordinates = resources.Select(c => (c.XIndex,c.ZIndex)).ToArray();
        var before = game.Wallet.Current.Balance;
        foreach (var source in resources)
        {
            placer.SelectGrid(source.gameObject); placer.PointerBegin(); placer.PointerEnd();
            Assert.That(placer.LastClearingResult, Is.EqualTo(TerrainClearingApplication.Result.Success));
        }
        Assert.That(game.Wallet.Current.Balance, Is.EqualTo(before - 60));
        foreach (var size in new[] { new Vector2Int(1600,900),new Vector2Int(1366,768) })
        {
            Screen.SetResolution(size.x,size.y,false); await UniTask.DelayFrame(4); Canvas.ForceUpdateCanvases();
            Assert.That(operation.PlacementInfo.preferredHeight, Is.LessThanOrEqualTo(operation.PlacementInfo.rectTransform.rect.height + 1),"clearing operation text " + size);
            screen.Open(); screen.Select(uid); await UniTask.DelayFrame(3); Canvas.ForceUpdateCanvases();
            Assert.That(screen.Description.preferredHeight, Is.LessThanOrEqualTo(screen.Description.rectTransform.rect.height + 1),"clearing detail " + size);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(screen.PurchaseButton.GetComponentInParent<ScrollRect>().viewport,
                screen.PurchaseButton.transform);
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(screen.PurchaseButton.GetComponentInParent<ScrollRect>().viewport.rect.yMin - 1));
            screen.Close(); panel.ShowPage(0);
        }
        var saves = TestScenes.Find<GameSaveAdapter>();
        Assert.That(saves.RequestSave());
        await UniTask.WaitUntil(() => !saves.IsSaving).Timeout(TimeSpan.FromSeconds(20),DelayType.Realtime);
        Assert.That(saves.LastError, Is.Null);
        var balance = game.Wallet.Current.Balance;
        await ReloadMainScene().ToUniTask();
        game = TestScenes.Find<GameInitializerAdapter>();
        Assert.That(await game.TryRestoreGameAsync());
        Assert.That(game.Skills.IsOwned(uid)); Assert.That(TestScenes.Find<CellPlacerAdapter>().ClearingAvailable);
        Assert.That(game.Wallet.Current.Balance, Is.EqualTo(balance));
        grid = GridFieldDatabaseAdapter.Instance;
        foreach (var point in coordinates) Assert.That(grid.GetCell(point.XIndex,point.ZIndex), Is.TypeOf<EmptyCellAdapter>());
    }
}
#endif
