#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture, Category("ResourceExpansion")]
public sealed class ResourceExpansionAssetTests : FactoryEditModeScope
{
    [Test] public void SavedResourcePrefabsPricesAndSharedModels()
    {
        var resources = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset");
        var cells = AssetDatabase.LoadAssetAtPath<CellDatabaseDefinition>("Assets/Scripts/ScriptableObject/GeneralCellDB.asset");
        var prices = AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset").CreateCatalog();
        foreach (var type in new[] { ResourceTypeDomain.Iron, ResourceTypeDomain.Copper, ResourceTypeDomain.Coal })
        {
            var resource = resources.GetResourceByType(type);
            Assert.That(resource.Prefab != null && resource.Icon != null && !string.IsNullOrEmpty(resource.ResourceName));
            var extractorType = ResourceCellRulesDomain.ExtractorType(type);
            Assert.That(cells.TryGetCellInfo(extractorType, out var info));
            var extractor = (ExtractorCellAdapter)info.FieldCellPrefab;
            Assert.That(extractor.ResourceTypeDomain, Is.EqualTo(type));
            Assert.That(extractor.ProgressionType, Is.EqualTo(extractorType));
            Assert.That(extractor.ExtractionSecond, Is.EqualTo(1.5f));
            Assert.That(extractor.ExtractionAmount, Is.EqualTo(1)); Assert.That(extractor.StorageCapacity, Is.EqualTo(10));
            Assert.That(prices.TryGetPlacementAmount(extractorType, 1, out var cost));
            Assert.That(cost, Is.EqualTo(type == ResourceTypeDomain.Coal ? 25 : 30));
            var fieldModel = info.FieldCellPrefab.transform.Find("Model").GetChild(0).gameObject;
            var previewModel = info.PlaceholderCellPrefab.transform.Find("Model").GetChild(0).gameObject;
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(fieldModel),
                Is.EqualTo(PrefabUtility.GetCorrespondingObjectFromSource(previewModel)));
            Assert.That(info.FieldCellPrefab.Footprint.Area, Is.EqualTo(1));
        }
        Assert.That(ResourceCellRulesDomain.ExtractorType(ResourceTypeDomain.Gold), Is.EqualTo(CellTypeDomain.None));
        Assert.That((int)ResourceTypeDomain.BuildingParts, Is.EqualTo(7)); Assert.That((int)CellTypeDomain.Shipping, Is.EqualTo(14));
        Assert.That(cells.GetAllCellInfos().Select(c => c.CellTypeDomain).Distinct().Count(), Is.EqualTo(cells.GetAllCellInfos().Count));
    }
}

[TestFixture, Category("ResourceExpansion")]
public sealed class ResourceGenerationTests : FactoryEditModeScope
{
    protected override bool NeedsMainScene => true;
    [Test] public void GeneratedTerrainIsStableRegardlessOfCandidateOrder()
    {
        var saved = TestScenes.Find<GridFieldGeneratorAdapter>();
        var generator = TestScenes.NewObject("ResourceGenerator").AddComponent<GridFieldGeneratorAdapter>();
        EditorUtility.CopySerialized(saved, generator);
        var serialized = new SerializedObject(generator); serialized.FindProperty("gridSize").intValue = 14;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var field = TestScenes.NewObject("GeneratedField").transform;
        generator.GenerateGrid(field);
        string Map() => string.Join(";", field.GetComponentsInChildren<ResourceCellAdapter>().OrderBy(r => r.transform.position.x)
            .ThenBy(r => r.transform.position.z).Select(r => r.transform.position + ":" + r.ResourceTypeDomain));
        var first = Map();
        serialized.Update(); var candidates = serialized.FindProperty("propPrefabs");
        candidates.MoveArrayElement(0, candidates.arraySize - 1); serialized.ApplyModifiedPropertiesWithoutUndo();
        generator.GenerateGrid(field); Assert.That(Map(), Is.EqualTo(first));
        var grid = field.gameObject.AddComponent<GridFieldDatabaseAdapter>(); grid.InitializeCells(14);
        Assert.That(grid.Owners.Count(),Is.EqualTo(196));
        Assert.That(grid.Owners.OfType<SingularityCellAdapter>(),Is.Empty);
        Assert.That(grid.Owners.OfType<ShippingCellAdapter>(),Is.Empty);
    }
    [Test] public void SavedMainSceneContainsAccessibleNewResources()
    {
        var grid = TestScenes.Find<GridFieldDatabaseAdapter>(); grid.InitializeCells(100);
        foreach (var entry in new[] { (46,48,ResourceTypeDomain.Iron), (54,48,ResourceTypeDomain.Copper), (46,54,ResourceTypeDomain.Coal) })
        {
            Assert.That(((ResourceCellAdapter)grid.GetCell(entry.Item1, entry.Item2)).ResourceTypeDomain, Is.EqualTo(entry.Item3));
            for (var d = 1; d <= 3; d++) Assert.That(grid.GetCell(entry.Item1, entry.Item2 - d), Is.TypeOf<EmptyCellAdapter>());
        }
    }
}
#endif
