#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>独自のEditor終了を使わず、失敗時もSceneと音量を戻してTest Runnerへ結果を返す。</summary>
public abstract class FactoryPlayModeScope
{
    private Scene _scene;
    private bool _muted;
    private float _volume, _scale;
    protected virtual bool NeedsMainScene => false;
    protected virtual bool PrepareHud => false;
    protected virtual bool ChooseDefaultStart => true;
    protected virtual bool ChooseNewGame => true;
    protected string SaveTestDirectory { get; private set; }
    private Exception _preparationError;
    private void BeforeStart(Scene scene, LoadSceneMode mode)
    {
        if (scene.path != "Assets/Scenes/MainScene.unity") return;
        try
        {
            SceneManager.SetActiveScene(scene);
            TestScenes.Find<GameSaveAdapter>().SetStorage(new SaveFileApplication(Path.Combine(SaveTestDirectory,"factory.json")));
            if (PrepareHud) HudPrefabAssertions.Prepare();
        }
        catch (Exception exception) { _preparationError = exception; }
    }
    [UnitySetUp]
    public IEnumerator OpenScope()
    {
        _muted = EditorUtility.audioMasterMute; _volume = AudioListener.volume; _scale = Time.timeScale;
        EditorUtility.audioMasterMute = true; AudioListener.volume = 0; Time.timeScale = 1;
        _preparationError = null;
        SaveTestDirectory = Path.Combine(Path.GetTempPath(),"FactorySaveTests",Guid.NewGuid().ToString("N"));
        SceneManager.sceneLoaded += BeforeStart;
        if (NeedsMainScene)
        {
            _scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            if (_preparationError != null) throw _preparationError;
        }
        else
        {
            _scene = SceneManager.CreateScene("FactoryTestScene"); SceneManager.SetActiveScene(_scene);
            // 開始Sceneを破棄するとTest Runner自身も停止する。作成したSceneだけをTearDownで閉じる。
        }
        yield return null; yield return null; yield return null;
        if (NeedsMainScene)
        {
            var initializer = TestScenes.Find<GameInitializerAdapter>();
            yield return UniTask.WaitUntil(() => initializer.Session.CurrentStage != GameStartApplication.Stage.Uninitialized)
                .Timeout(TimeSpan.FromSeconds(30), DelayType.Realtime).ToCoroutine();
            Assert.That(initializer.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Menu));
            if (!ChooseNewGame) yield break;
            Assert.That(initializer.TryPrepareNewGame(),Is.True);
            Assert.That(initializer.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Choosing));
            if (ChooseDefaultStart)
            {
                Assert.That(initializer.TryStartGame(), Is.True, "confirm default start for this fixture");
                Assert.That(initializer.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Running));
                yield return null; yield return null; yield return null;
            }
        }
    }
    protected IEnumerator ReloadMainScene()
    {
        _scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var initializer = TestScenes.Find<GameInitializerAdapter>();
        yield return UniTask.WaitUntil(() => initializer.Session.CurrentStage != GameStartApplication.Stage.Uninitialized)
            .Timeout(TimeSpan.FromSeconds(30), DelayType.Realtime).ToCoroutine();
        if (_preparationError != null) throw _preparationError;
        if (ChooseNewGame) Assert.That(initializer.TryPrepareNewGame(),Is.True);
    }
    [UnityTearDown]
    public IEnumerator CloseScope()
    {
        SceneManager.sceneLoaded -= BeforeStart;
        var empty = SceneManager.CreateScene("FactoryTestCleanup_" + Guid.NewGuid().ToString("N")); SceneManager.SetActiveScene(empty);
        if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        if (Directory.Exists(SaveTestDirectory)) Directory.Delete(SaveTestDirectory,true);
        Time.timeScale = _scale; AudioListener.volume = _volume; EditorUtility.audioMasterMute = _muted;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true; TestScenes.CaptureImages = false;
    }
}
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
    [UnityTest] public IEnumerator EarnedCurrencyFundsSkillsAndReinvestment() => EconomyLoopAssertions.Verify().ToCoroutine();
    [UnityTest] public IEnumerator PhysicalFactoryCompletesEveryDeliveryGoalAndKeepsRunning() => DeliveryFactoryAssertions.Verify().ToCoroutine();
}
[TestFixture, Category("UI")]
public sealed class EconomyHudPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    protected override bool PrepareHud => true;
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
