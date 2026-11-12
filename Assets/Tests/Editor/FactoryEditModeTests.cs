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
