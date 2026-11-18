using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

[TestFixture, Category("TestInfrastructure")]
public sealed class TestSceneIsolationTests
{
    [Test] public void PreviewScopeKeepsWorkingSceneAndDisposesCreatedObjects()
    {
        var working = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = working.GetRootGameObjects().Select(root => root.GetInstanceID()).ToArray();
        var dirty = working.isDirty;
        UnityEngine.GameObject created, cloned;
        using (new TestScenes.EditorScope())
        {
            created = TestScenes.NewObject("ScopeIsolationProbe"); cloned = TestScenes.Clone(created);
            Assert.That(created.scene, Is.EqualTo(TestScenes.CurrentScene)); Assert.That(cloned.scene, Is.EqualTo(TestScenes.CurrentScene));
            Assert.That(created.scene, Is.Not.EqualTo(working));
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Is.EqualTo(working));
        }
        CollectionAssert.AreEqual(roots, working.GetRootGameObjects().Select(root => root.GetInstanceID()).ToArray());
        Assert.That(working.isDirty, Is.EqualTo(dirty)); Assert.That(created == null && cloned == null, Is.True);
    }
}

/// <summary>機能ごとに選択できるEdit Mode検証。ケース間でSceneと状態を共有しない。</summary>
public abstract class FactoryEditModeScope
{
    private TestScenes.EditorScope _scope;
    protected virtual bool NeedsMainScene => false;
    [SetUp] public void OpenScope() => _scope = new TestScenes.EditorScope(NeedsMainScene);
    [TearDown] public void CloseScope() => _scope?.Dispose();
}

[TestFixture, Category("Economy")]
public sealed class CurrencyWalletTests : FactoryEditModeScope
{
    [Test] public void PriceCatalogValidation() => CurrencyWalletAssertions.VerifyPrices();
    [Test] public void WalletAndTransactionBoundaries() => CurrencyWalletAssertions.VerifyBoundaries();
    [Test] public void SettlementOrderPreservesQuantities() => CurrencyWalletAssertions.VerifySettlementOrders();
    [Test] public void ReentrantAndFailingObservers() => CurrencyWalletAssertions.VerifyNotifications();

    [Test]
    public void IncomeRateUsesActualSampleTimeAndOnlyCommittedIncome()
    {
        var wallet = new CurrencyWalletApplication(100);
        var rate = new IncomeRateApplication(wallet, 10);
        Assert.That(wallet.TryReserveIncome(9, out var sale), Is.True);
        Assert.That(sale.TryCommit(), Is.True);
        Assert.That(wallet.TryReserveIncome(30, out var cancelled), Is.True);
        Assert.That(cancelled.TryCancel(), Is.True);
        Assert.That(wallet.TryReserveExpense(5, out var purchase), Is.True);
        Assert.That(purchase.TryCommit(), Is.True);

        rate.Advance(10.75);
        Assert.That(rate.IncomePerSecond, Is.Zero);
        rate.Advance(11.5);
        Assert.That(rate.IncomePerSecond, Is.EqualTo(6m));
        rate.Advance(12);
        Assert.That(rate.IncomePerSecond, Is.EqualTo(6m));
        rate.Advance(12.5);
        Assert.That(rate.IncomePerSecond, Is.Zero);
    }
}
[TestFixture, Category("Transfers")]
public sealed class ResourceReservationTests : FactoryEditModeScope
{
    [Test] public void QuantityBoundaries() => ResourceReservationAssertions.VerifyQuantityRules();
    [Test] public void ReservationTicketsCommitAndCancelOnce() => ResourceReservationAssertions.VerifyReservationTickets();
    [Test] public void TransferOperationOwnership() => ResourceReservationAssertions.VerifyTransferOperation();
    [Test] public void StorageReservations() => ResourceReservationAssertions.VerifyStorage();
    [Test] public void CraftingInputReservations() => ResourceReservationAssertions.VerifyCrafter();
    [Test] public void ConveyorReservations() => ResourceReservationAssertions.VerifyConveyor();
    [Test] public void CrossingReservations() => ResourceReservationAssertions.VerifyCrossing();
}
[TestFixture, Category("Progression")]
public sealed class EffectiveStatsTests : FactoryEditModeScope
{
    [Test] public void MultipliersAdditionsAndLimits() => EffectiveStatsAssertions.VerifyRules();
}
[TestFixture, Category("Crafting")]
public sealed class CraftingRulesTests : FactoryEditModeScope
{
    [Test]
    public void RecipeReferenceUsesEquipmentPortsCapacitiesAndShipmentPrices()
    {
        var wood = ResourceTypeDomain.Wood;
        var plank = ResourceTypeDomain.Plank;
        var recipe = new RecipeRulesDomain.Recipe(new[] {
            new RecipeRulesDomain.Ingredient(wood, 3), new RecipeRulesDomain.Ingredient(wood, 2)
        }, plank, 4);
        var method = new RecipeReferenceCatalogApplication.RecipeInfo("Two ports", 5, recipe);
        var alternative = new RecipeReferenceCatalogApplication.RecipeInfo("One port", 2,
            new RecipeRulesDomain.Recipe(new[] { new RecipeRulesDomain.Ingredient(wood, 1) }, plank, 1));
        var prices = new CurrencyPriceCatalogDomain(0,
            new[] { new System.Collections.Generic.KeyValuePair<ResourceTypeDomain, long>(plank, 27) },
            System.Array.Empty<System.Collections.Generic.KeyValuePair<CellTypeDomain, long>>());
        var catalog = new RecipeReferenceCatalogApplication(new[] { wood, plank }, prices, new[]
        {
            new RecipeReferenceCatalogApplication.Equipment(CellTypeDomain.Crafter, 1, 3, 4, new[] { method, alternative }),
            new RecipeReferenceCatalogApplication.Equipment(CellTypeDomain.AdvancedCrafter, 2, 2, 3, new[] { method, alternative })
        });
        var choices = catalog.MethodsFor(plank);
        Assert.That(choices.Select(value => value.Definition), Is.EqualTo(new[] { method, alternative }));
        var both = choices.Single(value => value.Definition == alternative);
        CollectionAssert.AreEquivalent(new[] { CellTypeDomain.Crafter, CellTypeDomain.AdvancedCrafter }, both.Machines.Select(item => item.Type));
        var twoPorts = choices.Single(value => value.Definition == method).Machines.Single();
        Assert.That(twoPorts.Type, Is.EqualTo(CellTypeDomain.AdvancedCrafter));
        Assert.That(twoPorts.InputCapacityNeeded, Is.EqualTo(3));
        Assert.That(twoPorts.OutputCapacityNeeded, Is.EqualTo(4));
        Assert.That(catalog.TryGetUnitSalePrice(plank, out var price), Is.True);
        Assert.That(price, Is.EqualTo(27));
        Assert.That(catalog.TryGetUnitSalePrice(wood, out _), Is.False);
    }

    [Test]
    public void RecipeSelectionStartsANewIngredientPath()
    {
        var trail = new ResourceReferenceNavigationApplication();
        Assert.That(trail.TryFollow(ResourceTypeDomain.Wood), Is.False);
        trail.Select(ResourceTypeDomain.Wood);
        Assert.That(trail.TryFollow(ResourceTypeDomain.Wood), Is.False);
        Assert.That(trail.TryFollow(ResourceTypeDomain.Plank));
        Assert.That(trail.TryFollow(ResourceTypeDomain.Wood), Is.False);
        trail.Select(ResourceTypeDomain.Plank);
        Assert.That(trail.TryFollow(ResourceTypeDomain.Wood));
        Assert.That(trail.Current, Is.EqualTo(ResourceTypeDomain.Wood));
    }

    [Test] public void ImmutablePlansQuantitiesAndCancellation() => CraftingAssertions.VerifyRules();
    [Test] public void IngredientMatchingAgreesWithIndependentSearch() => CraftingAssertions.VerifyMatchingAgainstSearch();
}
[TestFixture, Category("Grid")]
public sealed class CellConnectionTests : FactoryEditModeScope
{
    [Test] public void FourDirectionsDeletionReconnectionAndRotation() => CellConnectionAssertions.VerifyConnections();
    [Test] public void RotatedFootprintsAndOccupancy() => CellConnectionAssertions.VerifyFootprints();
    [Test] public void ExteriorPortsOfMultiblockCells() => CellConnectionAssertions.VerifyExteriorPorts();
}
[TestFixture, Category("Grid")]
public sealed class GridGenerationTests : FactoryEditModeScope
{
    [Test] public void ColdDefinitionsAndCacheInvalidation() => GridFieldGeneratorAssertions.VerifyCellDefinitions();
    [Test] public void PrefabCellsGridLinesAndClear() => GridFieldGeneratorAssertions.VerifyGeneration();
}
[TestFixture, Category("Placement")]
public sealed class EquipmentPlacementRulesTests : FactoryEditModeScope
{
    [Test] public void PaymentRollbackAndReentrancy() => EquipmentPlacementAssertions.VerifyTransactions();
}
[TestFixture, Category("Economy")]
public sealed class ShippingRulesTests : FactoryEditModeScope
{
    [Test] public void FourPortIntakeLimitsAndOneShotSettlement() => SingularityAssertions.VerifyIntake();
    [Test] public void SavedShippingAssetsAndAbsorption() => ShippingAssertions.VerifyAssetsAndAbsorption();
}
[TestFixture, Category("Grid")]
public sealed class SingularityGenerationTests : FactoryEditModeScope
{
    protected override bool NeedsMainScene => true;
    [Test] public void GeneratedTerrainAndStartCandidateDiscoveryRemainSeparate() => SingularityAssertions.VerifyGeneration();
}
[TestFixture, Category("Progression")]
public sealed class SkillTreeRulesTests : FactoryEditModeScope
{
    [Test] public void CatalogPrerequisitesEffectsAndPurchaseTransactions() => SkillTreeAssertions.VerifyRules();
}
[TestFixture, Category("UI")]
public sealed class UIAssetTests : FactoryEditModeScope
{
    [Test] public void SavedDefinitionsLabelsAndPrefabReferences() => UIStatusAssertions.VerifyAssets();
}
[TestFixture, Category("UI")]
public sealed class HudEditorTests : FactoryEditModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator SavedReferencesFontAndPalettePreview()
    {
        HudPrefabAssertions.Prepare(); yield return null; yield return null;
        HudPrefabAssertions.VerifyEditModeColors();
    }
}
