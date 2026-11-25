#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

[TestFixture, Category("StartSite")]
public sealed class StartSitePlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    protected override bool ChooseNewGame => false;

    [UnityTest] public IEnumerator EnteredSeedBuildsTerrainAndAutomaticallyPlacesTheFirstValidSite() =>
        VerifyDirectStart().ToCoroutine();

    private static async UniTask VerifyDirectStart()
    {
        var game = TestScenes.Find<GameInitializerAdapter>();
        var grid = GridFieldDatabaseAdapter.Instance;
        var generator = TestScenes.Find<GridFieldGeneratorAdapter>();
        var layout = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset");
        var assetSeed = grid.GenerationSeed;
        int seed = 0;
        StartSiteRulesDomain.Terrain[,] expected = null;
        StartSiteRulesDomain.Candidate site = null;
        foreach (var candidate in new[] { 0, 1, 2, 3, 4, 5, 14, 27, 99 })
        {
            if (candidate == assetSeed) continue;
            expected = await generator.BuildTerrainAsync(grid.GeneratedSize, candidate, default);
            var prices = layout.CreateCatalog();
            prices.TryGetPlacementAmount(CellTypeDomain.ExtractorWood, 1, out var extractor);
            prices.TryGetPlacementAmount(CellTypeDomain.Conveyor, 1, out var belt);
            site = StartSiteRulesDomain.Find(expected, candidate, layout.SingularityPrefab.Footprint,
                layout.ShippingCell(Vector2Int.zero), prices.InitialBalance, extractor, belt);
            if (site != null) { seed = candidate; break; }
        }
        Assert.That(site, Is.Not.Null);
        Press("SaveSlot1");
        GameObject.Find("WorldSeedInput").GetComponent<InputField>().text = seed.ToString();
        GameObject.Find("SaveNameInput").GetComponent<InputField>().text = "種から始める工場";
        Press("BeginNewGame");
        await WaitForStage(game, GameStartApplication.Stage.Running);
        Assert.That(grid.GenerationSeed, Is.EqualTo(seed));
        Assert.That(game.Saves.DisplayName, Is.EqualTo("種から始める工場"));
        Assert.That(game.Saves.Storage.Exists, Is.False);

        var core = grid.Owners.OfType<SingularityCellAdapter>().Single();
        var shipping = grid.Owners.OfType<ShippingCellAdapter>().Single();
        Assert.That((core.XIndex, core.ZIndex), Is.EqualTo((site.X, site.Z)));
        Assert.That((shipping.XIndex, shipping.ZIndex), Is.EqualTo(site.Shipping));
        Assert.That(grid.Owners.OfType<ConnectableCellBaseAdapter>().Count(), Is.EqualTo(2));
        var occupied = core.OccupiedCoordinates.ToHashSet();
        occupied.Add(site.Shipping);
        for (var x = 0; x < grid.GeneratedSize; x++)
        for (var z = 0; z < grid.GeneratedSize; z++)
        {
            if (occupied.Contains((x, z))) continue;
            var cell = grid.GetCell(x, z);
            var resource = cell is ResourceCellAdapter raw ? raw.ResourceTypeDomain : ResourceTypeDomain.None;
            Assert.That(new StartSiteRulesDomain.Terrain(cell is EmptyCellAdapter, resource), Is.EqualTo(expected[x, z]));
        }

        var placer = TestScenes.Find<CellPlacerAdapter>();
        void Place(CellTypeDomain type, (int x, int z) point, (int x, int z) toward)
        {
            placer.transform.rotation = Quaternion.LookRotation(new Vector3(toward.x - point.x, 0, toward.z - point.z));
            placer.SetSelectedCellType(type);
            placer.SelectGrid(grid.GetCell(point.x, point.z).gameObject);
            placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Assert.That(placer.LastPlacedCount, Is.EqualTo(1));
        }
        Place(CellTypeDomain.ExtractorWood, site.Extractor, site.Wood);
        for (var i = 0; i < site.Belts.Count; i++)
            Place(CellTypeDomain.Conveyor, site.Belts[i], i + 1 < site.Belts.Count ? site.Belts[i + 1] : site.Shipping);
        var balance = game.Wallet.Current.Balance;
        await UniTask.WaitUntil(() => game.Wallet.Current.Balance > balance).Timeout(TimeSpan.FromSeconds(12), DelayType.Realtime);
    }

    [UnityTest] public IEnumerator BlankSeedStartsWithAnAutomaticallyGeneratedTerrain() => VerifyBlankSeed().ToCoroutine();

    private static async UniTask VerifyBlankSeed()
    {
        var game = TestScenes.Find<GameInitializerAdapter>();
        var grid = GridFieldDatabaseAdapter.Instance;
        var generator = TestScenes.Find<GridFieldGeneratorAdapter>();
        Press("SaveSlot1");
        Assert.That(GameObject.Find("WorldSeedInput").GetComponent<InputField>().text, Is.Empty);
        for (var i = 0; i < 8 && game.Session.CurrentStage == GameStartApplication.Stage.Configuring; i++)
        {
            Press("BeginNewGame");
            await UniTask.WaitUntil(() => game.Session.CurrentStage is not
                (GameStartApplication.Stage.Preparing or GameStartApplication.Stage.Starting))
                .Timeout(TimeSpan.FromSeconds(30), DelayType.Realtime);
        }
        Assert.That(game.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Running));
        Assert.That(game.Saves.DisplayName, Is.EqualTo("工場 1"));
        var expected = await generator.BuildTerrainAsync(grid.GeneratedSize, grid.GenerationSeed, default);
        var resources = grid.Owners.OfType<ResourceCellAdapter>().ToArray();
        Assert.That(resources, Is.Not.Empty);
        foreach (var cell in resources)
            Assert.That(cell.ResourceTypeDomain, Is.EqualTo(expected[cell.XIndex, cell.ZIndex].Resource));
    }

    [UnityTest] public IEnumerator InvalidOrCancelledPreparationKeepsTheCurrentWorldAndEmptySlot() =>
        VerifyBackAndValidation().ToCoroutine();

    private static async UniTask VerifyBackAndValidation()
    {
        var game = TestScenes.Find<GameInitializerAdapter>();
        var grid = GridFieldDatabaseAdapter.Instance;
        var before = grid.CaptureTerrain();
        var seed = grid.GenerationSeed;
        Press("SaveSlot1");
        var seedInput = GameObject.Find("WorldSeedInput").GetComponent<InputField>();
        Assert.That(seedInput.text, Is.Empty);
        seedInput.text = "2147483648";
        Press("BeginNewGame");
        Assert.That(game.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Configuring));
        seedInput.text = "";
        Press("BeginNewGame");
        Assert.That(game.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Preparing));
        Press("BackFromNewGame");
        await UniTask.DelayFrame(2);
        Assert.That(game.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Menu));

        var serialized = new SerializedObject(TestScenes.Find<GridFieldGeneratorAdapter>());
        var props = serialized.FindProperty("propPrefabs");
        for (var i = 0; i < props.arraySize; i++) props.GetArrayElementAtIndex(i).FindPropertyRelative("threshold").floatValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Press("SaveSlot1");
        seedInput.text = "-32";
        GameObject.Find("SaveNameInput").GetComponent<InputField>().text = "設定を残す";
        Press("BeginNewGame");
        await WaitForStage(game, GameStartApplication.Stage.Configuring);
        Assert.That(seedInput.text, Is.EqualTo("-32"));
        Assert.That(GameObject.Find("SaveNameInput").GetComponent<InputField>().text, Is.EqualTo("設定を残す"));
        Assert.That(grid.GenerationSeed, Is.EqualTo(seed));
        Assert.That(grid.CaptureTerrain(), Is.EqualTo(before));
        Assert.That(game.Saves.Storage.Exists, Is.False);
    }

    private static UniTask WaitForStage(GameInitializerAdapter game, GameStartApplication.Stage stage)
        => UniTask.WaitUntil(() => game.Session.CurrentStage == stage)
            .Timeout(TimeSpan.FromSeconds(30), DelayType.Realtime);

    private static void Press(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
}
#endif
