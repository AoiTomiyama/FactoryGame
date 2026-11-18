#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture, Category("Persistence")]
public sealed class SaveGamePlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    protected override bool ChooseNewGame => false;
    [UnityTest, Explicit("Supply a saved-game copy through FACTORY_SAVE_TEST_INPUT")]
    public IEnumerator ExistingSaveRestoresAndSavesAcrossFrames() => ExistingSaveRoundTrip().ToCoroutine();
    private static async UniTask ExistingSaveRoundTrip()
    {
        var input = System.Environment.GetEnvironmentVariable("FACTORY_SAVE_TEST_INPUT");
        Assert.That(File.Exists(input), "An isolated copy of the saved game is required.");
        var game = TestScenes.Find<GameInitializerAdapter>();
        var destination = game.Saves.Storage.Path;
        Assert.That(Path.GetFullPath(input), Is.Not.EqualTo(destination));
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(input, destination);
        var expected = game.Saves.Read();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var load = game.TryRestoreGameAsync().AsTask();
        var loadFrames = 0;
        var longestLoadFrame = 0d;
        var lastFrame = clock.Elapsed.TotalMilliseconds;
        while (!load.IsCompleted)
        {
            await UniTask.NextFrame();
            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(30), "load timed out");
            var now = clock.Elapsed.TotalMilliseconds;
            longestLoadFrame = Math.Max(longestLoadFrame, now - lastFrame);
            lastFrame = now;
            loadFrames++;
        }
        Assert.That(await load);
        Assert.That(loadFrames, Is.GreaterThan(1));
        Assert.That(game.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Running));
        Assert.That(GridFieldDatabaseAdapter.Instance.Owners.Count(), Is.EqualTo(expected.cells.Length));
        var loadMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        Assert.That(game.Saves.RequestSave());
        lastFrame = clock.Elapsed.TotalMilliseconds;
        var longestSaveFrame = 0d;
        var saveFrames = 0;
        while (game.Saves.IsSaving)
        {
            await UniTask.NextFrame();
            Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(30), "save timed out");
            var now = clock.Elapsed.TotalMilliseconds;
            longestSaveFrame = Math.Max(longestSaveFrame, now - lastFrame);
            lastFrame = now;
            saveFrames++;
        }
        var saveMs = clock.Elapsed.TotalMilliseconds;
        Assert.That(game.Saves.LastError, Is.Null);
        var saved = game.Saves.Read();
        Assert.That(saved.cells.Length, Is.EqualTo(expected.cells.Length));
        Assert.That(saved.seed, Is.EqualTo(expected.seed));
        Assert.That(saved.skills, Is.EqualTo(expected.skills));
        Debug.Log($"Save round-trip metrics: cells={saved.cells.Length}; load_ms={loadMs:F1}; load_frames={loadFrames}; max_load_frame_ms={longestLoadFrame:F1}; save_ms={saveMs:F1}; save_frames={saveFrames}; max_save_frame_ms={longestSaveFrame:F1}");
    }
    [UnityTest] public IEnumerator MenuRequiresExplicitNewGameAndSavedHudControlFits() => MenuAndFit().ToCoroutine();
    private static async UniTask MenuAndFit()
    {
        var game = TestScenes.Find<GameInitializerAdapter>(); var bar = TestScenes.Find<OperationBarAdapter>();
        Assert.That(game.Session.CurrentStage,Is.EqualTo(GameStartApplication.Stage.Menu));
        Assert.That(game.Wallet,Is.Null); Assert.That(game.Saves.RequestSave(),Is.False);
        Assert.That(bar.RotateButton.GetComponentInChildren<Text>().text,Is.EqualTo("新規開始"));
        Assert.That(bar.ViewButton.interactable,Is.False);
        Assert.That(TestScenes.Find<CameraControllerAdapter>().SetFirstPerson(true),Is.False);
        bar.RotateButton.onClick.Invoke(); Assert.That(game.Session.CurrentStage,Is.EqualTo(GameStartApplication.Stage.Choosing));
        bar.WalkButton.onClick.Invoke(); await UniTask.DelayFrame(2);
        var layout = TestScenes.Find<HudLayoutAdapter>(); var label = layout.Save.GetComponentInChildren<Text>();
        Assert.That(label.text,Is.EqualTo("保存 [F5]"));
        Assert.That(label.preferredWidth,Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1));
        Assert.That(label.preferredHeight,Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1));
        var canvas = layout.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<CanvasScaler>().enabled = false;
        foreach (var size in new[] {new Vector2(1600,900),new Vector2(1366,768),new Vector2(960,720)})
        {
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(size.x * 720 / size.y,720);
            await UniTask.DelayFrame(3); Canvas.ForceUpdateCanvases();
            var buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(layout.Summary,(RectTransform)layout.Save.transform);
            var rect = layout.Summary.rect;
            Assert.That(buttonBounds.min.x,Is.GreaterThanOrEqualTo(rect.xMin)); Assert.That(buttonBounds.max.x,Is.LessThanOrEqualTo(rect.xMax));
            var summary = TestScenes.Find<EconomyPanelAdapter>().SummaryView;
            foreach (var item in summary.WalletItems.Append(summary.IncomeItem).Concat(summary.Items).Where(t => t.gameObject.activeSelf))
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(layout.Summary,item.rectTransform);
                Assert.That(bounds.max.x,Is.LessThanOrEqualTo(buttonBounds.min.x - 1),item.text);
                Assert.That(item.preferredHeight,Is.LessThanOrEqualTo(item.rectTransform.rect.height + 1),item.text);
            }
        }
        Assert.That(await game.TryRestoreGameAsync(),Is.False,"load is restricted to the menu");
    }
    [UnityTest] public IEnumerator FreshSceneRestoresEconomyJobsAndContinuesProduction() => RoundTrip().ToCoroutine();
    private async UniTask RoundTrip()
    {
        var game = StartNew(); var grid = GridFieldDatabaseAdapter.Instance;
        Assert.That(game.Wallet.TryReserveIncome(10000,out var funds)); funds.TryCommit();
        Assert.That(game.Skills.Purchase("basic-research"),Is.EqualTo(SkillPurchaseResultApplication.Success));
        Assert.That(game.Skills.Purchase("storage-capacity-1"),Is.EqualTo(SkillPurchaseResultApplication.Success));
        game.ConstructionStock.TryReserveDeposit(ResourceTypeDomain.MachineParts,17,out var materials); materials.TryCommit();
        game.Deliveries.TryReserve(ResourceTypeDomain.Plank,7,out var delivered); delivered.TryCommit();
        var storage = (StorageCellAdapter)Install(CellTypeDomain.Storage);
        var input = storage.Ports.First(p => (p.Use & CellPortUseDomain.Input) != 0).Id;
        Assert.That(storage.AllocateStorage(input,storage.Capacity,ResourceTypeDomain.Iron)); storage.StoreResource(input,storage.Capacity);
        var site = TestScenes.ExtractorSite(ResourceTypeDomain.Wood);
        var extractor = (ExtractorCellAdapter)Install(CellTypeDomain.ExtractorWood,site.X,site.Z,site.Yaw);
        var crafter = (CrafterCellAdapter)Install(CellTypeDomain.Crafter);
        var craftPort = crafter.Ports.First(p => (p.Use & CellPortUseDomain.Input) != 0).Id;
        Assert.That(crafter.AllocateStorage(craftPort,2,ResourceTypeDomain.Wood)); crafter.StoreResource(craftPort,2);
        await UniTask.WaitUntil(() => extractor.ElapsedTime > .1f && crafter.ElapsedProcessTime > .1f).Timeout(TimeSpan.FromSeconds(10));
        var secondsBeforeSkill = extractor.ActiveExtractionSecond;
        Assert.That(game.Skills.Purchase("basic-extraction-1"), Is.EqualTo(SkillPurchaseResultApplication.DeliveryStageMissing));
        Assert.That(game.Skills.Purchase("extraction-speed-1"), Is.EqualTo(SkillPurchaseResultApplication.Success));
        Assert.That(extractor.ActiveExtractionSecond, Is.EqualTo(secondsBeforeSkill));
        Assert.That(extractor.ExtractionSecond, Is.EqualTo(secondsBeforeSkill / 1.25f).Within(.001f));
        Time.timeScale = 0;
        var world = new SaveWorldAdapter(grid,TestScenes.Find<CellPlacerAdapter>());
        var captureClock = System.Diagnostics.Stopwatch.StartNew();
        var before = world.Capture(game,1);
        captureClock.Stop();
        var saveClock = System.Diagnostics.Stopwatch.StartNew();
        Assert.That(game.Saves.RequestSave()); Assert.That(game.Saves.RequestSave(),Is.False,"one writer");
        await UniTask.WaitUntil(() => !game.Saves.IsSaving).Timeout(TimeSpan.FromSeconds(10), DelayType.Realtime);
        Assert.That(game.Saves.LastError,Is.Null);
        Debug.Log($"Save metrics: cells={before.cells.Length}; bytes={new FileInfo(game.Saves.Storage.Path).Length}; capture_ms={captureClock.Elapsed.TotalMilliseconds:F2}; request_to_write_ms={saveClock.Elapsed.TotalMilliseconds:F2}");
        var saved = game.Saves.Read();
        var originalExtractor = saved.cells.Single(c => c.x == extractor.XIndex && c.z == extractor.ZIndex);
        var originalCrafter = saved.cells.Single(c => c.x == crafter.XIndex && c.z == crafter.ZIndex);
        Assert.That(originalExtractor.job,Is.Not.Null); Assert.That(originalCrafter.job,Is.Not.Null);
        Assert.That(extractor.ElapsedTime,Is.GreaterThanOrEqualTo(before.cells.Single(c => c.x == extractor.XIndex && c.z == extractor.ZIndex).job.elapsed));
        var extractorX = extractor.XIndex; var extractorZ = extractor.ZIndex;
        var crafterX = crafter.XIndex; var crafterZ = crafter.ZIndex;
        await ReloadMainScene().ToUniTask();
        game = TestScenes.Find<GameInitializerAdapter>(); grid = GridFieldDatabaseAdapter.Instance;
        Assert.That(TestScenes.Find<OperationBarAdapter>().ViewButton.interactable);
        var restoring = game.TryRestoreGameAsync();
        Assert.That(game.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Starting));
        Assert.That(game.TryPrepareNewGame(), Is.False);
        Assert.That(await game.TryRestoreGameAsync(), Is.False, "one reader");
        Assert.That(TestScenes.Find<OperationBarAdapter>().ViewButton.interactable, Is.False);
        Assert.That(await restoring);
        var restored = new SaveWorldAdapter(grid,TestScenes.Find<CellPlacerAdapter>()).Capture(game,1);
        Assert.That(restored.balance,Is.EqualTo(saved.balance));
        Assert.That(restored.skills,Is.EqualTo(saved.skills));
        Assert.That(JsonUtility.ToJson(new Stocks {values = restored.construction}),Is.EqualTo(JsonUtility.ToJson(new Stocks {values = saved.construction})));
        Assert.That(JsonUtility.ToJson(new Stocks {values = restored.deliveries}),Is.EqualTo(JsonUtility.ToJson(new Stocks {values = saved.deliveries})));
        Assert.That(restored.cells.Length,Is.EqualTo(saved.cells.Length));
        foreach (var pair in saved.cells.Zip(restored.cells,(a,b) => (a,b)))
            Assert.That(JsonUtility.ToJson(pair.b),Is.EqualTo(JsonUtility.ToJson(pair.a)),pair.a.kind + " at " + pair.a.x + "," + pair.a.z);
        extractor = (ExtractorCellAdapter)grid.GetCell(extractorX,extractorZ);
        crafter = (CrafterCellAdapter)grid.GetCell(crafterX,crafterZ);
        Assert.That(extractor.ActiveExtractionSecond, Is.EqualTo(secondsBeforeSkill));
        var startingLoad = extractor.CurrentLoad;
        Time.timeScale = 1;
        await UniTask.WaitUntil(() => extractor.CurrentLoad > startingLoad).Timeout(TimeSpan.FromSeconds(10));
        Assert.That(extractor.CurrentLoad - startingLoad, Is.EqualTo(originalExtractor.job.amount));
        await UniTask.WaitUntil(() => extractor.ElapsedTime > 0).Timeout(TimeSpan.FromSeconds(10));
        Assert.That(extractor.ActiveExtractionSecond, Is.EqualTo(secondsBeforeSkill / 1.25f).Within(.001f));
        Time.timeScale = 20;
        await UniTask.WaitUntil(() => extractor.CurrentLoad > 0 && crafter.ExportStorageAmount > 0).Timeout(TimeSpan.FromSeconds(10));
        Assert.That(crafter.ExportResourceType,Is.EqualTo(ResourceTypeDomain.Plank));
        Assert.That(crafter.GetInput(craftPort).Amount,Is.Zero,"ingredients consumed once at completion");
        Assert.That(game.Saves.RequestSave()); await UniTask.WaitUntil(() => !game.Saves.IsSaving).Timeout(TimeSpan.FromSeconds(10));
        Assert.That(game.Saves.LastError,Is.Null); Assert.That(Directory.GetFiles(SaveTestDirectory).Length,Is.EqualTo(1));
    }
    [Serializable] private sealed class Stocks { public SaveGameApplication.Stock[] values; }
    [UnityTest] public IEnumerator TransitOwnershipSurvivesReloadAndBlockedBatchesContinue() => TransitRoundTrip().ToCoroutine();
    private async UniTask TransitRoundTrip()
    {
        var game = StartNew(); var grid = GridFieldDatabaseAdapter.Instance;
        var anchor = grid.Owners.OfType<EmptyCellAdapter>().First(e => e.XIndex > 1 && e.ZIndex > 1 && e.ZIndex + 3 < grid.GeneratedSize &&
            Enumerable.Range(0,4).All(i => grid.GetCell(e.XIndex,e.ZIndex + i) is EmptyCellAdapter));
        var x = anchor.XIndex; var z = anchor.ZIndex;
        var source = (StorageCellAdapter)Install(CellTypeDomain.Storage,x,z);
        var belt = (ConveyorCellAdapter)Install(CellTypeDomain.Conveyor,x,z + 1);
        var crossing = (CrossingCellAdapter)Install(CellTypeDomain.Crossing,x,z + 2);
        var target = (StorageCellAdapter)Install(CellTypeDomain.Storage,x,z + 3);
        var empty = grid.Owners.OfType<EmptyCellAdapter>().First(e => e.ZIndex > 0 &&
            grid.GetCell(e.XIndex,e.ZIndex - 1) is EmptyCellAdapter);
        var fallbackX = empty.XIndex;
        var fallbackZ = empty.ZIndex;
        var formerExporter = (ConveyorCellAdapter)Install(CellTypeDomain.ExportConveyor,fallbackX,fallbackZ);
        var port = source.Ports.First(p => (p.Use & CellPortUseDomain.Input) != 0).Id;
        Assert.That(source.AllocateStorage(port,10,ResourceTypeDomain.Wood)); source.StoreResource(port,10);
        var pool = ResourceItemObjectPoolAdapter.Instance;
        var edit = new UnityEditor.SerializedObject(pool); edit.FindProperty("transferSecond").floatValue = 1; edit.ApplyModifiedPropertiesWithoutUndo();
        await UniTask.WaitUntil(() => crossing.CaptureBatches(_ => {}).Length == 1 && belt.CaptureBatches(_ => {}).Length == 1)
            .Timeout(TimeSpan.FromSeconds(15),DelayType.Realtime);
        Time.timeScale = 0;
        Assert.That(game.Saves.RequestSave());
        await UniTask.WaitUntil(() => !game.Saves.IsSaving).Timeout(TimeSpan.FromSeconds(10),DelayType.Realtime);
        Assert.That(game.Saves.LastError,Is.Null);
        var saved = game.Saves.Read();
        Assert.That(saved.cells.Where(c => c.batches.Length != 0).Sum(c => c.batches.Sum(b => b.amount)),Is.EqualTo(10));
        Assert.That(formerExporter.IsExportConveyor,Is.False);
        var fallback = saved.cells.Single(c => c.x == fallbackX && c.z == fallbackZ);
        Assert.That(SaveKeysApplication.Cell(fallback.kind),Is.EqualTo(CellTypeDomain.ExportConveyor));
        Assert.That(fallback.exporter,Is.False);
        var oldIds = new System.Collections.Generic.HashSet<int>(); crossing.CaptureBatches(i => oldIds.Add(i)); belt.CaptureBatches(i => oldIds.Add(i));
        Assert.That(target.CurrentLoad,Is.Zero,"reservation is excluded from confirmed inventory");
        await ReloadMainScene().ToUniTask(); game = TestScenes.Find<GameInitializerAdapter>();
        Assert.That(await game.TryRestoreGameAsync()); grid = GridFieldDatabaseAdapter.Instance;
        belt = (ConveyorCellAdapter)grid.GetCell(x,z + 1); crossing = (CrossingCellAdapter)grid.GetCell(x,z + 2); target = (StorageCellAdapter)grid.GetCell(x,z + 3);
        var newIds = new System.Collections.Generic.HashSet<int>(); crossing.CaptureBatches(i => newIds.Add(i)); belt.CaptureBatches(i => newIds.Add(i));
        Assert.That(newIds.Count,Is.EqualTo(2)); Assert.That(newIds.Overlaps(oldIds),Is.False,"runtime IDs are not persisted");
        Assert.That(belt.IsExportConveyor,"automatic exporter role is retained");
        Assert.That(((ConveyorCellAdapter)grid.GetCell(fallbackX,fallbackZ)).IsExportConveyor,Is.False,
            "a saved export prefab in normal mode restores in normal mode");
        Assert.That(target.CurrentLoad,Is.Zero,"load does not instantly complete transit");
        Time.timeScale = 10;
        await UniTask.WaitUntil(() => target.CurrentLoad == 10).Timeout(TimeSpan.FromSeconds(15),DelayType.Realtime);
        Assert.That(ResourceItemObjectPoolAdapter.Instance.Resources.Count,Is.Zero);
    }
    [UnityTest] public IEnumerator LoadedLineCompletesRemainingFinalDeliveryOnce() => ContinueFinalDelivery().ToCoroutine();
    private async UniTask ContinueFinalDelivery()
    {
        var game = StartNew(); var grid = GridFieldDatabaseAdapter.Instance;
        var goals = game.Deliveries.Catalog.Stages;
        var last = goals.Last().Requirements.First(r => r.Amount > goals.Take(goals.Count - 1).SelectMany(s => s.Requirements).Where(q => q.Resource == r.Resource).Select(q => q.Amount).DefaultIfEmpty().Max());
        foreach (var group in goals.SelectMany(s => s.Requirements).GroupBy(r => r.Resource))
        {
            var count = group.Max(r => r.Amount) - (group.Key == last.Resource ? 1 : 0);
            if (count > 0) { game.Deliveries.TryReserve(group.Key,count,out var deposit); deposit.TryCommit(); }
        }
        Assert.That(game.Deliveries.Current.IsComplete,Is.False);
        var core = grid.Owners.OfType<SingularityCellAdapter>().Single();
        var contact = core.Ports.Select(p => (position: Vector3Int.RoundToInt(core.PortPosition(p.Id)) + core.PortDirection(p.Id),direction: core.PortDirection(p.Id)))
            .First(p => grid.IsWithinBounds(p.position.x,p.position.z) && grid.GetCell(p.position.x,p.position.z) is EmptyCellAdapter);
        var yaw = Quaternion.LookRotation(-contact.direction).eulerAngles.y;
        var belt = (ConveyorCellAdapter)Install(CellTypeDomain.Conveyor,contact.position.x,contact.position.z,yaw);
        var input = belt.Ports.First(p => (p.Use & CellPortUseDomain.Input) != 0).Id;
        Assert.That(belt.AllocateStorage(input,1,last.Resource));
        belt.Reuse(input,ResourceItemObjectPoolAdapter.Instance.CreateIdFromResourceData(last.Resource,1)); belt.StoreResource(input,1);
        Time.timeScale = 0; Assert.That(game.Saves.RequestSave());
        await UniTask.WaitUntil(() => !game.Saves.IsSaving).Timeout(TimeSpan.FromSeconds(10),DelayType.Realtime);
        Assert.That(game.Saves.LastError,Is.Null);
        await ReloadMainScene().ToUniTask(); game = TestScenes.Find<GameInitializerAdapter>(); Assert.That(await game.TryRestoreGameAsync());
        var completed = 0; game.Deliveries.Completed += () => completed++;
        Assert.That(game.Deliveries.Current.IsComplete,Is.False);
        Time.timeScale = 10;
        await UniTask.WaitUntil(() => game.Deliveries.Current.IsComplete).Timeout(TimeSpan.FromSeconds(10),DelayType.Realtime);
        Assert.That(completed,Is.EqualTo(1)); Assert.That(game.Deliveries.Current.Count(last.Resource),Is.EqualTo(last.Amount));
        Assert.That(game.Wallet.Current.Balance,Is.EqualTo(100),"delivery does not pay shipping income");
    }
    [UnityTest] public IEnumerator InvalidWorldIsRejectedBeforeTerrainChanges()
    {
        var game = StartNew(); var world = new SaveWorldAdapter(GridFieldDatabaseAdapter.Instance,TestScenes.Find<CellPlacerAdapter>());
        var data = world.Capture(game,1); data.cells[1].x = data.cells[0].x; data.cells[1].z = data.cells[0].z;
        Assert.Throws<InvalidOperationException>(() => data.ValidateShape());
        yield return ReloadMainScene(); game = TestScenes.Find<GameInitializerAdapter>();
        var terrain = GridFieldDatabaseAdapter.Instance.GetComponentsInChildren<CellBaseAdapter>();
        Directory.CreateDirectory(SaveTestDirectory);
        File.WriteAllText(game.Saves.Storage.Path, "corrupt");
        LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("Game initialization failed during load:"));
        var restored = true;
        yield return game.TryRestoreGameAsync().ToCoroutine(value => restored = value);
        Assert.That(restored,Is.False); Assert.That(game.Session.CurrentStage,Is.EqualTo(GameStartApplication.Stage.Failed));
        Assert.That(GridFieldDatabaseAdapter.Instance.GetComponentsInChildren<CellBaseAdapter>(),Is.EquivalentTo(terrain));
        Assert.That(File.ReadAllText(game.Saves.Storage.Path),Is.EqualTo("corrupt"));
    }

    [UnityTest] public IEnumerator OverlappingFootprintIsRejectedBeforeTerrainChanges() => OverlappingFootprint().ToCoroutine();
    private async UniTask OverlappingFootprint()
    {
        var game = StartNew();
        var grid = GridFieldDatabaseAdapter.Instance;
        var world = new SaveWorldAdapter(grid, TestScenes.Find<CellPlacerAdapter>());
        var data = world.Capture(game, 1);
        var core = grid.Owners.OfType<SingularityCellAdapter>().Single();
        var overlap = core.OccupiedCoordinates.First(point => point != (core.XIndex, core.ZIndex));
        var empty = data.cells.First(cell => cell.kind == "empty");
        empty.x = overlap.x;
        empty.z = overlap.z;
        var save = SaveJsonAdapter.Encode(data);
        await ReloadMainScene().ToUniTask();
        game = TestScenes.Find<GameInitializerAdapter>();
        var terrain = GridFieldDatabaseAdapter.Instance.GetComponentsInChildren<CellBaseAdapter>();
        Directory.CreateDirectory(SaveTestDirectory);
        File.WriteAllText(game.Saves.Storage.Path, save);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Saved cell occupancy is invalid"));
        Assert.That(await game.TryRestoreGameAsync(), Is.False);
        Assert.That(GridFieldDatabaseAdapter.Instance.GetComponentsInChildren<CellBaseAdapter>(), Is.EquivalentTo(terrain));
    }
    private static GameInitializerAdapter StartNew()
    {
        var game = TestScenes.Find<GameInitializerAdapter>();
        Assert.That(game.TryPrepareNewGame()); Assert.That(game.TryStartGame()); return game;
    }
    private static CellBaseAdapter Install(CellTypeDomain type, int x = -1,int z = -1,float yaw = 0)
    {
        var grid = GridFieldDatabaseAdapter.Instance; var prefab = TestScenes.Find<CellPlacerAdapter>().FieldPrefab(type);
        var rotation = Mathf.RoundToInt(yaw / 90);
        if (x < 0)
        {
            var anchor = grid.Owners.OfType<EmptyCellAdapter>().First(e => e.XIndex > 2 && e.ZIndex > 2 &&
                prefab.Footprint.Coordinates(e.XIndex,e.ZIndex,rotation).All(p => grid.IsWithinBounds(p.x,p.z) && grid.GetCell(p.x,p.z) is EmptyCellAdapter));
            x = anchor.XIndex; z = anchor.ZIndex;
        }
        var old = prefab.Footprint.Coordinates(x,z,rotation).Select(p => grid.GetCell(p.x,p.z)).Distinct().ToArray();
        Assert.That(old.All(c => c is EmptyCellAdapter));
        var cell = Object.Instantiate(prefab,new Vector3(x,0,z),Quaternion.Euler(0,yaw,0),grid.transform);
        grid.SaveCell(x,z,cell);
        foreach (var previous in old) {previous.gameObject.SetActive(false); Object.Destroy(previous.gameObject);}
        cell.InitializeSystem(); return cell;
    }
}
#endif
