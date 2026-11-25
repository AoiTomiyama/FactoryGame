#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture,Category("StartSite")]
public sealed class StartSiteRulesTests
{
    internal static StartSiteRulesDomain.Terrain[,] Map()
    {
        var map=new StartSiteRulesDomain.Terrain[100,100];
        for(var x=0;x<100;x++) for(var z=0;z<100;z++) map[x,z]=new StartSiteRulesDomain.Terrain(true);
        foreach(var p in new[]{(50,43,ResourceTypeDomain.Wood),(54,54,ResourceTypeDomain.Stone),(46,48,ResourceTypeDomain.Iron),
            (54,48,ResourceTypeDomain.Copper),(46,54,ResourceTypeDomain.Coal)}) map[p.Item1,p.Item2]=new StartSiteRulesDomain.Terrain(false,p.Item3);
        return map;
    }
    [TestCase(0)] [TestCase(-1)] [TestCase(1)] [TestCase(int.MinValue)] [TestCase(int.MaxValue)]
    public void CandidateDiscoveryPreservesEveryTerrainCellAndFitsTheBudget(int seed)
    {
        var map=Map();var before=(StartSiteRulesDomain.Terrain[,])map.Clone();
        var site = StartSiteRulesDomain.Find(map, seed, new CellFootprintDomain(5, 5, -2, -2), (0, -3), 100, 20, 5);
        Assert.That(site, Is.Not.Null);
        Assert.That((site.X, site.Z), Is.EqualTo((50, 50)));
        Assert.That(20 + site.Belts.Count * 5, Is.LessThanOrEqualTo(100));
        foreach (var point in site.Belts) Assert.That(map[point.x, point.z].Empty);
        Assert.That(map[site.Wood.x, site.Wood.z].Resource, Is.EqualTo(ResourceTypeDomain.Wood));
        for(var x=0;x<100;x++) for(var z=0;z<100;z++) Assert.That(map[x,z],Is.EqualTo(before[x,z]));
    }
    [Test] public void ResourceWithOnlyATurningExitAndADeadEndIsNotTreatedAsAccessible()
    {
        var map=Map();
        foreach(var p in new[]{(46,53),(45,54),(47,54),(47,55),(44,55),(46,58),(45,57),(47,57),(47,56)})
            map[p.Item1,p.Item2]=default;
        Assert.That(StartSiteRulesDomain.Find(map,0,new CellFootprintDomain(5,5,-2,-2),(0,-3),100,20,5),Is.Null);
    }
    [Test] public void MissingResourcesOrMoneyProduceNoFabricatedCandidate()
    {
        var empty=new StartSiteRulesDomain.Terrain[14,14];
        Assert.That(StartSiteRulesDomain.Find(empty,0,new CellFootprintDomain(5,5,-2,-2),(0,-3),100,20,5),Is.Null);
        Assert.That(StartSiteRulesDomain.Find(Map(),0,new CellFootprintDomain(5,5,-2,-2),(0,-3),19,20,5),Is.Null);
    }
}

[TestFixture,Category("StartSite")]
public sealed class StartSiteGenerationTests : FactoryEditModeScope
{
    protected override bool NeedsMainScene=>true;
    [Test] public void SavedTerrainHasNoInitialEquipmentAndDiscoveryDoesNotModifyIt()
    {
        var generator=TestScenes.Find<GridFieldGeneratorAdapter>();var field=TestScenes.Find<GridFieldDatabaseAdapter>();
        var cells=field.GetComponentsInChildren<CellBaseAdapter>();
        Assert.That(cells.Length,Is.EqualTo(10000));Assert.That(cells.OfType<SingularityCellAdapter>(),Is.Empty);Assert.That(cells.OfType<ShippingCellAdapter>(),Is.Empty);
        var positions=cells.ToDictionary(c=>c,c=>c.transform.position);
        var candidates=GameInitializerAdapter.FindStartSite(field,UnityEditor.AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset"));
        Assert.That(candidates, Is.Not.Null);
        Assert.That(field.GetComponentsInChildren<CellBaseAdapter>(),Is.EquivalentTo(cells));
        foreach(var p in positions) Assert.That(p.Key.transform.position,Is.EqualTo(p.Value));
    }
    [TestCase(0,0f)] [TestCase(int.MinValue,1f)] [TestCase(int.MaxValue,0f)]
    public void ProceduralGenerationAndCandidateDiscoveryNeverAddGuaranteedCells(int seed,float threshold)
    {
        var generator=TestScenes.NewObject("ReadOnlySiteGenerator").AddComponent<GridFieldGeneratorAdapter>();
        EditorUtility.CopySerialized(TestScenes.Find<GridFieldGeneratorAdapter>(),generator);
        var serialized=new SerializedObject(generator);serialized.FindProperty("gridSize").intValue=14;serialized.FindProperty("seedValue").intValue=seed;
        var props=serialized.FindProperty("propPrefabs");
        for(var i=0;i<props.arraySize;i++) props.GetArrayElementAtIndex(i).FindPropertyRelative("threshold").floatValue=threshold;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var field=TestScenes.NewObject("NaturalField").AddComponent<GridFieldDatabaseAdapter>();generator.GenerateGrid(field.transform);
        var before=field.GetComponentsInChildren<CellBaseAdapter>();var positions=before.ToDictionary(c=>c,c=>c.transform.position);
        var candidates=GameInitializerAdapter.FindStartSite(field,UnityEditor.AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset"));
        Assert.That(before.Length,Is.EqualTo(196));Assert.That(field.GetComponentsInChildren<CellBaseAdapter>(),Is.EquivalentTo(before));
        foreach(var pair in positions) Assert.That(pair.Key.transform.position,Is.EqualTo(pair.Value));
        if(threshold==1) { Assert.That(candidates,Is.Null);Assert.That(before.All(c=>c is EmptyCellAdapter)); }
    }
}
[TestFixture, Category("Grid")]
public sealed class FieldGenerationMetadataTests : FactoryEditModeScope
{
    protected override bool NeedsMainScene => true;

    [Test] public void SavedGenerationResultIsIndependentFromEditedInputs()
    {
        var generator = TestScenes.Find<GridFieldGeneratorAdapter>();
        var field = TestScenes.Find<GridFieldDatabaseAdapter>();
        Assert.That(generator.TargetField, Is.SameAs(field));
        var size = field.GeneratedSize;
        var seed = field.GenerationSeed;
        Assert.That(size, Is.GreaterThan(0));
        var edit = new SerializedObject(generator);
        edit.FindProperty("gridSize").intValue = 14;
        edit.FindProperty("seedValue").intValue = ~seed;
        edit.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(field.GeneratedSize, Is.EqualTo(size));
        Assert.That(field.GenerationSeed, Is.EqualTo(seed));
        Assert.That(field.CaptureTerrain().Length, Is.EqualTo(size * size));
    }

    [Test] public void ClearAndRegenerateKeepTheTargetAndNeedNoEconomyDefinition()
    {
        var generator = TestScenes.Clone(TestScenes.Find<GridFieldGeneratorAdapter>());
        var field = TestScenes.NewObject("StableGenerationTarget").AddComponent<GridFieldDatabaseAdapter>();
        var edit = new SerializedObject(generator);
        edit.FindProperty("targetField").objectReferenceValue = field;
        edit.FindProperty("gridSize").intValue = 14;
        edit.FindProperty("seedValue").intValue = 3;
        edit.ApplyModifiedPropertiesWithoutUndo();
        generator.GenerateGrid(field.transform);
        Assert.That(field.GeneratedSize, Is.EqualTo(14));
        Assert.That(field.GenerationSeed, Is.EqualTo(3));
        Assert.That(field.CaptureTerrain().Length, Is.EqualTo(196));
        GridFieldGeneratorAdapter.ClearGrid(field.transform);
        Assert.That(field != null && generator.TargetField == field, Is.True);
        Assert.That(field.transform.childCount, Is.Zero);
        Assert.That(field.GeneratedSize, Is.Zero);
        Assert.Throws<System.InvalidOperationException>(() => field.CaptureTerrain());
        generator.GenerateGrid(field.transform);
        Assert.That(generator.TargetField, Is.SameAs(field));
        Assert.That(field.GeneratedSize, Is.EqualTo(14));
        Assert.That(field.CaptureTerrain().Length, Is.EqualTo(196));
    }
}

[TestFixture, Category("StartSite")]
public sealed class GameStartSessionTests
{
    [Test] public void SetupAndStartRejectASecondRequest()
    {
        var session = new GameStartApplication();
        session.ShowMenu();
        Assert.That(session.TryOpenNewGame(1));
        Assert.That(session.TryBeginNewGame(2), Is.False);
        Assert.That(session.TryBeginNewGame(1));
        Assert.That(session.TryBeginNewGame(1), Is.False);
        session.BeginPreparedGame();
        Assert.That(session.TryCancelNewGame(), Is.False);
        session.Complete();
        Assert.That(session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Running));
    }

    [Test] public void CancellingRestoresTheSlotScreen()
    {
        var session = new GameStartApplication();
        session.ShowMenu();
        Assert.That(session.TryOpenNewGame(1));
        Assert.That(session.TryBeginNewGame(1));
        Assert.That(session.TryCancelNewGame());
        Assert.That(session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Menu));
    }

}
[TestFixture, Category("Startup")]
public sealed class StartupReferenceDiagnosticsTests : FactoryEditModeScope
{
    protected override bool NeedsMainScene => true;

    [Test] public void MissingInitializerReferencesIncludeMemberAndContext()
    {
        var source = TestScenes.Find<GameInitializerAdapter>();
        foreach (var member in new[] { "field", "initialLayout", "skillTree", "resources", "economyPanel", "placer", "statusView", "cameraController", "cursor", "startupHud", "terrainGenerator" })
        {
            var target = TestScenes.NewObject("Missing_" + member).AddComponent<GameInitializerAdapter>();
            EditorUtility.CopySerialized(source, target);
            var edit = new SerializedObject(target);
            edit.FindProperty(member).objectReferenceValue = null; edit.ApplyModifiedPropertiesWithoutUndo();
            ExpectMissing(target, nameof(GameInitializerAdapter), member, target.name);
        }
    }
    [Test] public void MissingPresentationReferencesIncludeMemberAndContext()
    {
        foreach (var member in new[] { "hud", "bar", "placer", "statusView" })
        {
            var view = TestScenes.NewObject("MissingView_" + member).AddComponent<StartupHudAdapter>();
            EditorUtility.CopySerialized(TestScenes.Find<StartupHudAdapter>(), view);
            var edit = new SerializedObject(view);
            edit.FindProperty(member).objectReferenceValue = null; edit.ApplyModifiedPropertiesWithoutUndo();
            var target = TestScenes.NewObject("DiagnosticInitializer_" + member).AddComponent<GameInitializerAdapter>();
            EditorUtility.CopySerialized(TestScenes.Find<GameInitializerAdapter>(), target);
            edit = new SerializedObject(target); edit.FindProperty("startupHud").objectReferenceValue = view; edit.ApplyModifiedPropertiesWithoutUndo();
            ExpectMissing(target, nameof(StartupHudAdapter), member, view.name);
        }
    }
    [Test] public void MultipleMissingReferencesAreReportedTogether()
    {
        var target = TestScenes.NewObject("MultipleMissing").AddComponent<GameInitializerAdapter>();
        EditorUtility.CopySerialized(TestScenes.Find<GameInitializerAdapter>(), target);
        var edit = new SerializedObject(target);
        edit.FindProperty("resources").objectReferenceValue = null;
        edit.FindProperty("skillTree").objectReferenceValue = null; edit.ApplyModifiedPropertiesWithoutUndo();
        ExpectMissing(target, nameof(GameInitializerAdapter), "skillTree, resources", target.name);
    }

    private static void ExpectMissing(GameInitializerAdapter target, string component, string members, string owner)
    {
        var method = typeof(GameInitializerAdapter).GetMethod("ValidateConfiguration",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(target, null));
        Assert.That(error.InnerException, Is.TypeOf<MissingReferenceException>());
        Assert.That(error.InnerException.Message,
            Does.Contain($"{component}: missing references [{members}] on '{owner}'"));
    }
}
#endif
