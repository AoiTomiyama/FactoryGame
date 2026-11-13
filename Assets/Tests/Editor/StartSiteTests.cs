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
        var sites=StartSiteRulesDomain.Candidates(map,seed,new CellFootprintDomain(5,5,-2,-2),(0,-3),100,20,5);
        Assert.That(sites.Count,Is.InRange(1,3));
        Assert.That(sites[0].X,Is.EqualTo(50));Assert.That(sites[0].Z,Is.EqualTo(50));
        foreach(var site in sites)
        {
            Assert.That(20+site.Belts.Count*5,Is.LessThanOrEqualTo(100));
            foreach(var point in site.Belts) Assert.That(map[point.x,point.z].Empty);
            Assert.That(map[site.Wood.x,site.Wood.z].Resource,Is.EqualTo(ResourceTypeDomain.Wood));
        }
        for(var x=0;x<100;x++) for(var z=0;z<100;z++) Assert.That(map[x,z],Is.EqualTo(before[x,z]));
    }
    [Test] public void ResourceWithOnlyATurningExitAndADeadEndIsNotTreatedAsAccessible()
    {
        var map=Map();
        foreach(var p in new[]{(46,53),(45,54),(47,54),(47,55),(44,55),(46,58),(45,57),(47,57),(47,56)})
            map[p.Item1,p.Item2]=default;
        Assert.That(StartSiteRulesDomain.Candidates(map,0,new CellFootprintDomain(5,5,-2,-2),(0,-3),100,20,5),Is.Empty);
    }
    [Test] public void MissingResourcesOrMoneyProduceNoFabricatedCandidate()
    {
        var empty=new StartSiteRulesDomain.Terrain[14,14];
        Assert.That(StartSiteRulesDomain.Candidates(empty,0,new CellFootprintDomain(5,5,-2,-2),(0,-3),100,20,5),Is.Empty);
        Assert.That(StartSiteRulesDomain.Candidates(Map(),0,new CellFootprintDomain(5,5,-2,-2),(0,-3),19,20,5),Is.Empty);
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
        var candidates=GameInitializerAdapter.DiscoverStartSites(field,UnityEditor.AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset"));
        Assert.That(candidates.Count,Is.InRange(1,3));
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
        var candidates=GameInitializerAdapter.DiscoverStartSites(field,UnityEditor.AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset"));
        Assert.That(before.Length,Is.EqualTo(196));Assert.That(field.GetComponentsInChildren<CellBaseAdapter>(),Is.EquivalentTo(before));
        foreach(var pair in positions) Assert.That(pair.Key.transform.position,Is.EqualTo(pair.Value));
        if(threshold==1) { Assert.That(candidates,Is.Empty);Assert.That(before.All(c=>c is EmptyCellAdapter)); }
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
        Assert.That(field.GeneratedSize, Is.EqualTo(100));
        Assert.That(field.GenerationSeed, Is.EqualTo(423794866));
        var edit = new SerializedObject(generator);
        edit.FindProperty("gridSize").intValue = 14;
        edit.FindProperty("seedValue").intValue = 1;
        edit.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(field.GeneratedSize, Is.EqualTo(100));
        Assert.That(field.GenerationSeed, Is.EqualTo(423794866));
        Assert.That(field.CaptureTerrain().Length, Is.EqualTo(10000));
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
    [Test] public void SelectionAndStartHaveOneOwnerAndRejectReentry()
    {
        var candidates = StartSiteRulesDomain.Candidates(StartSiteRulesTests.Map(), 0, new CellFootprintDomain(5,5,-2,-2), (0,-3), 100,20,5).ToList();
        var session = new GameStartApplication(); session.Prepare(candidates);
        var count = candidates.Count; candidates.Clear();
        Assert.That(session.Candidates.Count, Is.EqualTo(count));
        Assert.That(session.TrySelect(-1), Is.False); Assert.That(session.TrySelect(count), Is.False);
        Assert.That(session.TrySelect(count-1), Is.True);
        Assert.That(session.TryBegin(out var selected), Is.True);
        Assert.That(selected, Is.SameAs(session.Selected));
        Assert.That(session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Starting));
        Assert.That(session.TryBegin(out _), Is.False); Assert.That(session.TrySelect(0), Is.False);
        session.Complete();
        Assert.That(session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Running));
        Assert.That(session.TryBegin(out _), Is.False);
    }
    [Test] public void EmptyAndFailedSessionsNeverBeginOrReprepare()
    {
        var session = new GameStartApplication(); session.Prepare(System.Array.Empty<StartSiteRulesDomain.Candidate>());
        Assert.That(session.CanChoose, Is.False); Assert.That(session.TryBegin(out _), Is.False);
        session.Fail();
        Assert.That(session.TryBegin(out _), Is.False); Assert.That(session.TrySelect(0), Is.False);
        Assert.Throws<System.InvalidOperationException>(() => session.Prepare(System.Array.Empty<StartSiteRulesDomain.Candidate>()));
    }
}
#endif
