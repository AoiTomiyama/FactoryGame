#if UNITY_EDITOR
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture, Category("Crafting")]
public sealed class CraftingPlayModeTests : FactoryPlayModeScope
{
    [UnityTest] public IEnumerator CompletionReservationsAndInterruptedCellLifecycle() => CraftingAssertions.VerifyPlayAsync().ToCoroutine();
    [UnityTest] public IEnumerator SavedRecipesOutputAndSale() => SavedProducts().ToCoroutine();
    private static async UniTask SavedProducts()
    {
        var grid = new GameObject("SavedProductGrid").AddComponent<GridFieldDatabaseAdapter>();
        var row = new GameObject("SavedProductRow").transform; row.SetParent(grid.transform);
        await CraftingAssertions.VerifySavedProducts(grid, row);
    }
}
[TestFixture, Category("Placement")]
public sealed class EquipmentPlacementPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator PlacementPurchaseRemovalAndMultiblockRotations() => EquipmentPlacementAssertions.VerifyPlay().ToCoroutine();
}
[TestFixture, Category("Economy")]
public sealed class ShippingPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator StarterRouteMultipleShippingCellsAndTransferOwnership() => SingularityAssertions.VerifyPlay().ToCoroutine();
}
[TestFixture, Category("Progression")]
public sealed class SkillTreePlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator ExistingAndNewCellsShareTypeEffectsDuringOperation() => SkillTreeAssertions.VerifyPlay().ToCoroutine();
}
[TestFixture, Category("Economy.Integration")]
public sealed class EconomyLoopPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    // 通しの納品ラインは座標を指定して建てるため、地形の変更と切り分ける。
    protected override int? FixedTerrainSeed => 423794866;
    [UnityTest] public IEnumerator EarnedCurrencyFundsSkillsAndReinvestment() => EconomyLoopAssertions.Verify().ToCoroutine();
    [UnityTest] public IEnumerator PhysicalFactoryCompletesEveryDeliveryGoalAndKeepsRunning() => DeliveryFactoryAssertions.Verify().ToCoroutine();
}
[TestFixture, Category("UI")]
public sealed class EconomyHudPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    protected override bool PrepareHud => true;
    protected override int? FixedTerrainSeed => 423794866;
    [UnityTest] public IEnumerator BalanceSelectionSkillsInputAnimationAndPcLayout() => EconomyUIAssertions.Verify().ToCoroutine();
    [UnityTest, Category("UI.Capture"), Explicit("HUD画像が必要な場合に個別実行する")]
    public IEnumerator HudImagesAcrossPcSizes()
    {
        TestScenes.CaptureImages = true;
        return EconomyUIAssertions.Verify().ToCoroutine();
    }
}
[TestFixture, Category("UI")]
public sealed class StatusUIPlayModeTests : FactoryPlayModeScope
{
    [UnityTest] public IEnumerator StatusSubscriptionsExtractorLifecycleAndCrafting() => UIStatusAssertions.VerifyAsync().ToCoroutine();
}
[TestFixture, Category("Resources")]
public sealed class ResourceIconPlayModeTests : FactoryPlayModeScope
{
    [UnityTest] public IEnumerator RecipeAndStorageNamesAndIcons() => ResourceIconAssertions.VerifyAsync().ToCoroutine();
}
[TestFixture, Category("Input")]
public sealed class FirstPersonPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator WalkingTransitionsControlsAndHudVisibility() => FirstPersonAssertions.Verify().ToCoroutine();
}
[TestFixture, Category("Grid")]
public sealed class CellDefinitionPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator ColdDefinitionsDriveActualSelectionAndPointerPlacement() => CellDefinitionAssertions.VerifyPlacement();
}
[TestFixture, Category("Rendering.Benchmark")]
public sealed class FieldRenderingComparisonTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest, Explicit("GPU描画比較と画像書出しを行うため、比較を依頼された場合に個別実行する")]
    public IEnumerator OcclusionComparisonAndDynamicMeshes() => FieldRenderingAssertions.Verify().ToCoroutine();
}
#endif
