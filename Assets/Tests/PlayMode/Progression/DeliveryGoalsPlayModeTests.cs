#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>保存済みSceneの納品とHUDを個別に実行する。Sceneと音量は各テストの後に片付ける。</summary>
[TestFixture, Category("Delivery")]
public sealed class DeliveryGoalsPlayModeTests
{
    private GridFieldGeneratorAdapter _generator;
    private SingularityCellAdapter _core;
    private EconomyPanelAdapter _panel;
    private CellStatusViewAdapter _view;
    private HudLayoutAdapter _layout;
    private CellPlacerAdapter _placer;
    private DeliveryProgressApplication App => _generator.Deliveries;
    private Scene _scene;
    private bool _muted;
    private float _volume, _timeScale;

    [UnitySetUp]
    public IEnumerator LoadSavedScene()
    {
        _muted = EditorUtility.audioMasterMute; _volume = AudioListener.volume; _timeScale = Time.timeScale;
        EditorUtility.audioMasterMute = true; AudioListener.volume = 0; Time.timeScale = 1;
        _scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (var i = 0; i < 5; i++) yield return null;
        _generator = Object.FindAnyObjectByType<GridFieldGeneratorAdapter>(); _core = Object.FindAnyObjectByType<SingularityCellAdapter>();
        _panel = Object.FindAnyObjectByType<EconomyPanelAdapter>(); _view = Object.FindAnyObjectByType<CellStatusViewAdapter>();
        _layout = Object.FindAnyObjectByType<HudLayoutAdapter>(); _placer = Object.FindAnyObjectByType<CellPlacerAdapter>();
        Assert.That(_generator, Is.Not.Null); Assert.That(App, Is.Not.Null); Assert.That(_core, Is.Not.Null);
    }

    [UnityTearDown]
    public IEnumerator UnloadTestScene()
    {
        // 失敗したテストでもScene・購読・複製UIを残さず、次のケースを初期状態から始める。
        var empty = SceneManager.CreateScene("DeliveryTestsCleanup"); SceneManager.SetActiveScene(empty);
        if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        Time.timeScale = _timeScale; AudioListener.volume = _volume; EditorUtility.audioMasterMute = _muted;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    private void Receive(ResourceTypeDomain resource, int amount, int port = 0)
    { Assert.That(_core.AllocateStorage(port, amount, resource), Is.True); _core.StoreResource(port, amount); }
    private static void Deliver(DeliveryProgressApplication app, ResourceTypeDomain resource, long amount)
    { Assert.That(app.TryReserve(resource, amount, out var reservation), Is.True); Assert.That(reservation.TryCommit(), Is.True); }

    [Test, Description("特異点とHUDが同じ累計を読み、納品で段階表示を更新する")]
    public void DeliveryUpdatesSharedProgressAndHudWithoutPayingCurrency()
    {
        var balance = _generator.Wallet.Current.Balance;
        Assert.That(_core.Deliveries, Is.SameAs(App)); Assert.That(_panel.SummaryView.Items[0].text, Does.Contain("段階 1/3"));
        _view.UpdateUIStatusWindow(_core); Assert.That(_panel.EquipmentText, Does.Contain("0/40"));
        Assert.That(_panel.EquipmentView.UpgradeButton.gameObject.activeSelf, Is.False);
        Receive(ResourceTypeDomain.Wood, 41);
        Assert.That(_panel.EquipmentText, Does.Contain("41/40")); Assert.That(_panel.SummaryView.Items.Any(t => t.text.Contains("41/40")), Is.True);
        Receive(ResourceTypeDomain.Stone, 40);
        Assert.That(App.Current.CompletedStages, Is.EqualTo(1)); Assert.That(_panel.EquipmentText, Does.Contain("加工投入"));
        Assert.That(_panel.SummaryView.Items[0].GetComponent<HudMotionAdapter>().IsAnimating, Is.True);
        Assert.That(_generator.Wallet.Current.Balance, Is.EqualTo(balance));
    }

    [Test]
    public void CoreDisableCancelsPendingInputsAndKeepsCommittedDeliveries()
    {
        Receive(ResourceTypeDomain.Wood, 2);
        Assert.That(_core.AllocateStorage(1, 5, ResourceTypeDomain.Wood), Is.True);
        _core.gameObject.SetActive(false); _core.gameObject.SetActive(true); _core.StoreResource(1, 5);
        Assert.That(App.Current.Count(ResourceTypeDomain.Wood), Is.EqualTo(2));
        Receive(ResourceTypeDomain.Wood, 1); Assert.That(App.Current.Count(ResourceTypeDomain.Wood), Is.EqualTo(3));
    }

    [Test]
    public void FinalGoalRemainsVisibleAndAbsorptionContinuesIndependentlyOfShipping()
    {
        var final = 0; App.Completed += () => final++;
        Receive(ResourceTypeDomain.Wood, 40); Receive(ResourceTypeDomain.Stone, 40); Receive(ResourceTypeDomain.Plank, 30);
        Receive(ResourceTypeDomain.StoneBrick, 20); Receive(ResourceTypeDomain.BuildingParts, 20); _view.UpdateUIStatusWindow(_core);
        Assert.That(App.Current.IsComplete, Is.True); Assert.That(_panel.SummaryView.Items[0].text, Is.EqualTo("目標達成"));
        Assert.That(_panel.EquipmentText, Does.Contain("目標達成")); Receive(ResourceTypeDomain.Wood, 1);
        Assert.That(App.Current.Count(ResourceTypeDomain.Wood), Is.EqualTo(41)); Assert.That(final, Is.EqualTo(1));
        var balance = _generator.Wallet.Current.Balance;
        var shipping = Object.FindAnyObjectByType<ShippingCellAdapter>();
        Assert.That(shipping.AllocateStorage(0, 5, ResourceTypeDomain.Wood), Is.True); shipping.StoreResource(0, 5);
        Assert.That(_generator.Wallet.Current.Balance, Is.EqualTo(balance + 5)); Assert.That(App.Current.Count(ResourceTypeDomain.Wood), Is.EqualTo(41));
    }

    [Test]
    public void PanelReopenAndReconfigureRestoreProgressWithoutDuplicateSubscriptions()
    {
        _panel.enabled = false; Deliver(App, ResourceTypeDomain.Wood, 3);
        Assert.That(Subscribers(App, "Changed"), Is.Zero); _panel.enabled = true; _view.UpdateUIStatusWindow(_core);
        Assert.That(_panel.EquipmentText, Does.Contain("3/40"));
        _panel.Configure(_generator.Wallet, _generator.Skills, _view, _placer, App);
        Assert.That(Subscribers(App, "Changed"), Is.EqualTo(1)); Assert.That(Subscribers(App, "StageCompleted"), Is.EqualTo(1));
        _panel.enabled = false; Assert.That(Subscribers(App, "Changed"), Is.Zero); Assert.That(Subscribers(App, "StageCompleted"), Is.Zero);
    }

    [UnityTest, Category("Delivery.Layout")]
    public IEnumerator GoalSummaryAndDetailsFitBothPcSizes()
    {
        _view.UpdateUIStatusWindow(_core);
        return VerifyFit(_panel, _view, _layout).ToCoroutine();
    }

    [UnityTest, Category("Delivery.Layout")]
    public IEnumerator SevenLongResourceNamesAndLargeCountsWrapAndScrollToLastRequirement()
        => VerifyLargeGoals(_panel, _view, _layout, _generator, _placer, _core).ToCoroutine();

    [UnityTest]
    public IEnumerator WalkingHidesGoalsAndReturnRestoresLatestProgress() => VerifyWalk().ToCoroutine();
    private async UniTask VerifyWalk()
    {
        var camera = Object.FindAnyObjectByType<CameraControllerAdapter>();
        Assert.That(camera.SetFirstPerson(true), Is.True); await UniTask.Delay(600, ignoreTimeScale: true);
        Assert.That(_panel.SummaryView.Items.All(t => !t.gameObject.activeSelf), Is.True);
        Assert.That(_panel.SummaryView.WalletItems[0].gameObject.activeInHierarchy, Is.True);
        Receive(ResourceTypeDomain.Wood, 3);
        Assert.That(camera.SetFirstPerson(false), Is.True); await UniTask.Delay(600, ignoreTimeScale: true);
        Assert.That(_panel.SummaryView.Items.Count(t => t.gameObject.activeSelf), Is.EqualTo(3));
        Assert.That(_panel.SummaryView.Items.Any(t => t.gameObject.activeSelf && t.text.Contains("3/40")), Is.True);
    }

    private static int Subscribers(object target, string field) =>
        ((Delegate)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target))?.GetInvocationList().Length ?? 0;
    private static DeliveryGoalCatalogDomain.Requirement Need(ResourceTypeDomain type, long count) => new(type, count);
    private static DeliveryGoalCatalogDomain Catalog(params DeliveryGoalCatalogDomain.Stage[] stages) => new(stages);
    private static void Require(bool condition, string message) => Assert.That(condition, Is.True, message);
    private static async UniTask VerifyLargeGoals(EconomyPanelAdapter panel, CellStatusViewAdapter view, HudLayoutAdapter layout,
        GridFieldGeneratorAdapter generator, CellPlacerAdapter placer, SingularityCellAdapter core)
    {
        var originalResources = layout.Resources;
        var resources = UnityEngine.Object.Instantiate(originalResources);
        var serialized = new SerializedObject(resources); var entries = serialized.FindProperty("initializedResourceArr");
        for (var i = 0; i < entries.arraySize; i++)
            entries.GetArrayElementAtIndex(i).FindPropertyRelative("resourceName").stringValue += "（長い名前の加工品を使った表示確認）";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // このケースは7品目の収容を検証する。Enumへの資源追加で入力件数を変えない。
        var types = new[] { ResourceTypeDomain.Wood, ResourceTypeDomain.Stone, ResourceTypeDomain.Plank,
            ResourceTypeDomain.StoneBrick, ResourceTypeDomain.BuildingParts, ResourceTypeDomain.IronIngot, ResourceTypeDomain.CopperWire };
        Require(types.All(type => !string.IsNullOrEmpty(resources.GetResourceByType(type).ResourceName)), "seven named resources are registered");
        var app = new DeliveryProgressApplication(Catalog(new DeliveryGoalCatalogDomain.Stage("long", "長い段階名を設定した場合の収容確認", types.Select(t => Need(t, long.MaxValue)))));
        foreach (var type in types) Deliver(app, type, long.MaxValue - 1);
        try
        {
            layout.Resources = resources;
            panel.Configure(generator.Wallet, generator.Skills, view, placer, app); view.UpdateUIStatusWindow(core);
            var canvas = layout.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.GetComponent<CanvasScaler>().enabled = false;
            foreach (var resolution in new[] { new Vector2(1600, 900), new Vector2(1366, 768) })
            {
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(resolution.x * 720 / resolution.y, 720);
                await UniTask.DelayFrame(5); Canvas.ForceUpdateCanvases();
                var texts = panel.SummaryView.WalletItems.Concat(panel.SummaryView.Items).Where(t => t.gameObject.activeSelf).ToArray();
                Require(texts.Length == 9, "all seven resources visible");
                foreach (var type in types)
                    Require(panel.SummaryView.Items.Any(t => t.text.Contains(resources.GetResourceByType(type).ResourceName)),
                        "summary includes resource " + type);
                foreach (var text in texts)
                    Require(Inside(text.rectTransform, panel.SummaryRect) && text.preferredHeight <= text.rectTransform.rect.height + 1,
                        "long summary wraps without clipping " + resolution);
                for (var i = 1; i < texts.Length; i++)
                {
                    var a = RectTransformUtility.CalculateRelativeRectTransformBounds(panel.SummaryRect, texts[i - 1].rectTransform);
                    var b = RectTransformUtility.CalculateRelativeRectTransformBounds(panel.SummaryRect, texts[i].rectTransform);
                    Require(a.max.x <= b.min.x + 1 || b.max.y <= a.min.y + 1, "wrapped summary rows do not overlap");
                }
                view.StatusScroll.verticalNormalizedPosition = 0; await UniTask.DelayFrame(4); Canvas.ForceUpdateCanvases();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view.StatusScroll.viewport, layout.EquipmentText.rectTransform);
                Require(layout.EquipmentText.preferredHeight <= layout.EquipmentText.rectTransform.rect.height + 1 &&
                    bounds.min.y >= view.StatusScroll.viewport.rect.yMin - 1 && bounds.min.y <= view.StatusScroll.viewport.rect.yMax + 1,
                    "long goal details scroll to final resource " + resolution);
            }
        }
        finally
        {
            layout.Resources = originalResources;
            panel.Configure(generator.Wallet, generator.Skills, view, placer, generator.Deliveries); view.UpdateUIStatusWindow(core);
            UnityEngine.Object.Destroy(resources);
        }
    }
    private static async UniTask VerifyFit(EconomyPanelAdapter panel, CellStatusViewAdapter view, HudLayoutAdapter layout)
    {
        var canvas = layout.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<CanvasScaler>().enabled = false;
        var root = (RectTransform)canvas.transform;
        foreach (var resolution in new[] { new Vector2(1600, 900), new Vector2(1366, 768) })
        {
            root.sizeDelta = new Vector2(resolution.x * 720 / resolution.y, 720);
            panel.SetExpanded(false); await UniTask.Delay(350, ignoreTimeScale: true); Canvas.ForceUpdateCanvases();
            foreach (var text in panel.SummaryView.WalletItems.Concat(panel.SummaryView.Items).Where(t => t.gameObject.activeSelf))
            {
                Require(Inside(text.rectTransform, panel.SummaryRect) && Inside(text.rectTransform, root), "summary inside screen " + resolution);
                Require(text.preferredHeight <= text.rectTransform.rect.height + 1, "summary text fit " + resolution);
            }
            Require(layout.Summary.GetComponentsInChildren<ScrollRect>().Length == 0, "summary remains fixed");
            view.StatusScroll.verticalNormalizedPosition = 1; await UniTask.DelayFrame(3); Canvas.ForceUpdateCanvases();
            Require(layout.EquipmentText.preferredHeight <= layout.EquipmentText.rectTransform.rect.height + 1 &&
                Inside(layout.EquipmentText.rectTransform, view.StatusScroll.viewport), "goal details fit " + resolution);
        }
    }
    private static bool Inside(RectTransform child, RectTransform parent)
    {
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, child);
        return bounds.min.x >= parent.rect.xMin - 1 && bounds.max.x <= parent.rect.xMax + 1 &&
            bounds.min.y >= parent.rect.yMin - 1 && bounds.max.y <= parent.rect.yMax + 1;
    }
}
#endif
