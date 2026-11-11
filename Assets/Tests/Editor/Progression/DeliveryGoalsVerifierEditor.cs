using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>納品の数量境界と保存済みSceneの接続・表示を対象検証する。通しプレイは別工程。</summary>
[InitializeOnLoad]
public static class DeliveryGoalsVerifierEditor
{
    private const string Key = "FactoryGame.DeliveryGoalsVerification";
    private const string DefinitionPath = "Assets/Scripts/ScriptableObject/DeliveryGoals.asset";
    private static int _checks;
    static DeliveryGoalsVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
        SessionState.EraseBool(Key); VerifyPlay().Forget();
    };
    public static void Run()
    {
        _checks = 0;
        try { VerifyRules(); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); return; }
        SessionState.SetInt(Key + ".checks", _checks);
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        EditorUtility.audioMasterMute = true;
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    private static DeliveryGoalCatalogDomain.Requirement Need(ResourceTypeDomain type, long count) => new(type, count);
    private static DeliveryGoalCatalogDomain.Stage Stage(string id, params DeliveryGoalCatalogDomain.Requirement[] requirements)
        => new(id, id, requirements);
    private static DeliveryGoalCatalogDomain Catalog(params DeliveryGoalCatalogDomain.Stage[] stages) => new(stages);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Delivery goals: " + message); _checks++; }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (ArgumentException) { _checks++; return; }
        throw new InvalidOperationException("Delivery goals accepted invalid definition: " + message);
    }
    private static void Deliver(DeliveryProgressApplication app, ResourceTypeDomain type, long amount)
    { Require(app.TryReserve(type, amount, out var ticket) && ticket.TryCommit(), "reserve and commit " + type); }
    private static void VerifyRules()
    {
        var wood = ResourceTypeDomain.Wood; var stone = ResourceTypeDomain.Stone;
        var stages = new[] { Stage("first", Need(wood, 2), Need(stone, 2)), Stage("last", Need(wood, 4)) };
        var catalog = Catalog(stages); stages[0] = Stage("replaced", Need(wood, 9));
        Require(catalog.Stages[0].Id == "first", "catalog copies editable inputs");
        Reject(() => Catalog(), "empty");
        Reject(() => Catalog(Stage("x", Need(wood, 1)), Stage("x", Need(wood, 2))), "duplicate id");
        Reject(() => Catalog(Stage("x", Need(wood, 2)), Stage("y", Need(wood, 1))), "decreasing cumulative target");
        Reject(() => Stage("x", Need(wood, 1), Need(wood, 2)), "duplicate resource");
        Reject(() => Stage("", Need(wood, 1)), "empty id");
        Reject(() => new DeliveryGoalCatalogDomain.Stage("x", "", new[] { Need(wood, 1) }), "empty label");
        Reject(() => Stage("x"), "empty requirements");
        foreach (var count in new[] { long.MinValue, -1L, 0L }) Reject(() => Need(wood, count), "invalid target");
        Reject(() => Need(ResourceTypeDomain.None, 1), "none");
        Reject(() => Need((ResourceTypeDomain)999, 1), "undefined");
        var app = new DeliveryProgressApplication(catalog);
        var events = new List<string>();
        app.Changed += s => events.Add("count:" + s.CompletedStages);
        app.StageCompleted += s => events.Add(s.Id);
        var final = 0; app.Completed += () => final++;
        foreach (var count in new[] { long.MinValue, -1L, 0L }) Require(!app.TryReserve(wood, count, out _), "invalid delivery");
        Require(!app.TryReserve(ResourceTypeDomain.None, 1, out _) && !app.TryReserve((ResourceTypeDomain)999, 1, out _), "invalid resource");
        Require(app.TryReserve(wood, 5, out var pending), "first reservation");
        Require(app.TryReserve(stone, 2, out var cancel), "parallel reservation");
        Require(app.Current.Count(wood) == 0 && cancel.TryCancel() && !cancel.TryCommit() && events.Count == 0, "cancel never counts");
        Require(pending.TryCommit() && !pending.TryCommit() && !pending.TryCancel() && app.Current.Count(wood) == 5 && app.Current.CompletedStages == 0, "full overdelivery waits for second resource");
        var old = app.Current;
        Deliver(app, stone, 2);
        Require(app.Current.IsComplete && app.Current.Count(wood) == 5 && final == 1 && events.SequenceEqual(new[] { "count:0", "count:2", "first", "last" }), "one delivery advances multiple stages once");
        Require(old.Count(stone) == 0 && !old.IsComplete, "snapshots are immutable");
        Deliver(app, wood, 1); Deliver(app, ResourceTypeDomain.Gold, 1);
        Require(app.Current.Count(wood) == 6 && app.Current.Count(ResourceTypeDomain.Gold) == 1 && final == 1, "continue absorption after final and accept nongoal resources");
        Require(new DeliveryProgressApplication(catalog).Current.Count(wood) == 0, "new play resets state");
        var limit = new DeliveryProgressApplication(catalog);
        Require(limit.TryReserve(wood, long.MaxValue - 2, out var huge), "large reservation");
        Require(limit.TryReserve(wood, 2, out var two) && !limit.TryReserve(wood, 1, out _), "pending totals protect upper bound");
        Require(two.TryCancel() && limit.TryReserve(wood, 2, out two) && huge.TryCommit() && two.TryCommit() && limit.Current.Count(wood) == long.MaxValue && !limit.TryReserve(wood, 1, out _), "cancel frees limit; exact max commits");
        var reentrant = new DeliveryProgressApplication(Catalog(Stage("a", Need(wood, 1)), Stage("b", Need(wood, 2))));
        var order = new List<string>(); var completes = 0;
        reentrant.Changed += s => { order.Add("count:" + s.CompletedStages); if (s.Count(wood) == 1) Deliver(reentrant, wood, 1); };
        reentrant.Changed += _ => throw new InvalidOperationException("expected observer failure");
        reentrant.StageCompleted += s => order.Add(s.Id);
        reentrant.Completed += () => { completes++; Deliver(reentrant, wood, 1); };
        Deliver(reentrant, wood, 1);
        Require(order.SequenceEqual(new[] { "count:1", "a", "count:2", "b", "count:2" }) && completes == 1 && reentrant.LastNotificationException != null, "reentry and observer errors preserve event order and completion");
        var intakeApp = new DeliveryProgressApplication(catalog);
        var intake = new AbsorptionIntakeApplication(new[] { 1, 2 }, intakeApp) { Accepting = true };
        Require(intake.TryReserve(1, 2, wood) && intake.TryReserve(2, 2, stone) && !intake.Commit(1, 1) && !intake.Cancel(1, 2, stone), "ports fix quantity and type");
        Require(intake.Commit(1, 2) && !intake.Commit(1, 2) && intakeApp.Current.Count(wood) == 2, "intake commit counts once");
        intake.CancelAll(); Require(!intake.Commit(2, 2) && intakeApp.Current.Count(stone) == 0, "disconnect cancels pending only");
        var definition = AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>(DefinitionPath);
        var before = System.IO.File.ReadAllBytes(DefinitionPath);
        var testPath = AssetDatabase.GenerateUniqueAssetPath("Assets/DeliveryGoalsVerification.asset");
        var editable = UnityEngine.Object.Instantiate(definition);
        AssetDatabase.CreateAsset(editable, testPath);
        try
        {
            var serialized = new SerializedObject(editable);
            var stage = serialized.FindProperty("stages").GetArrayElementAtIndex(0);
            var label = stage.FindPropertyRelative("label"); var original = label.stringValue;
            Undo.RecordObject(editable, "Delivery goal verification"); label.stringValue = "編集確認";
            stage.FindPropertyRelative("requirements").GetArrayElementAtIndex(0).FindPropertyRelative("cumulativeAmount").longValue = 123;
            serialized.ApplyModifiedProperties();
            Require(editable.CreateCatalog().Stages[0].Label == "編集確認" && editable.CreateCatalog().Stages[0].Requirements[0].Amount == 123, "inspector edits feed catalog");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); Require(editable.CreateCatalog().Stages[0].Label == original, "definition undo");
            Undo.PerformRedo(); Require(editable.CreateCatalog().Stages[0].Label == "編集確認", "definition redo");
            AssetDatabase.SaveAssetIfDirty(editable); AssetDatabase.ImportAsset(testPath, ImportAssetOptions.ForceUpdate);
            var reread = AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>(testPath).CreateCatalog();
            Require(reread.Stages[0].Label == "編集確認" && reread.Stages[0].Requirements[0].Amount == 123, "saved edited definition reread");
        }
        finally { AssetDatabase.DeleteAsset(testPath); }
        Require(before.SequenceEqual(System.IO.File.ReadAllBytes(DefinitionPath)) && definition.CreateCatalog().Stages.Count == 3, "verification leaves source definition untouched");
        Debug.Log($"Delivery RULES passed: {_checks} assertions.");
    }
    private static async UniTaskVoid VerifyPlay()
    {
        try
        {
            _checks = SessionState.GetInt(Key + ".checks", 0); AudioListener.volume = 0;
            await UniTask.DelayFrame(5);
            var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
            var core = UnityEngine.Object.FindAnyObjectByType<SingularityCellAdapter>();
            var panel = UnityEngine.Object.FindAnyObjectByType<EconomyPanelAdapter>();
            var view = UnityEngine.Object.FindAnyObjectByType<CellStatusViewAdapter>();
            var layout = UnityEngine.Object.FindAnyObjectByType<HudLayoutAdapter>();
            var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
            var app = generator.Deliveries; var wallet = generator.Wallet.Current.Balance;
            Require(ReferenceEquals(app, core.Deliveries) && panel.SummaryView.Items[0].text.Contains("段階 1/3"), "shared runtime owner and initial HUD");
            view.UpdateUIStatusWindow(core);
            Require(panel.EquipmentText.Contains("0/40") && !panel.EquipmentView.UpgradeButton.gameObject.activeSelf, "core details show goals without apply");
            Require(core.AllocateStorage(0, 5, ResourceTypeDomain.Wood), "scene reservation");
            core.gameObject.SetActive(false); core.gameObject.SetActive(true); core.StoreResource(0, 5);
            Require(app.Current.Count(ResourceTypeDomain.Wood) == 0, "disable cancels input without losing progress");
            Require(core.AllocateStorage(0, 41, ResourceTypeDomain.Wood), "reserve scene wood"); core.StoreResource(0, 41);
            Require(panel.EquipmentText.Contains("41/40") && panel.SummaryView.Items.Any(t => t.text.Contains("41/40")) && generator.Wallet.Current.Balance == wallet, "delivery updates HUD, never wallet");
            Require(core.AllocateStorage(0, 40, ResourceTypeDomain.Stone), "reserve scene stone"); core.StoreResource(0, 40);
            Require(app.Current.CompletedStages == 1 && panel.EquipmentText.Contains("加工投入") && panel.SummaryView.Items[0].GetComponent<HudMotionAdapter>().IsAnimating, "stage advance updates details and pulses once");
            panel.enabled = false; Deliver(app, ResourceTypeDomain.Plank, 30); panel.enabled = true;
            view.UpdateUIStatusWindow(core);
            Require(panel.EquipmentText.Contains("30/30") && Subscribers(app, "Changed") == 1, "reopen refreshes deliveries with one subscription");
            panel.Configure(generator.Wallet, generator.Skills, view, placer, app);
            Require(Subscribers(app, "Changed") == 1 && Subscribers(app, "StageCompleted") == 1, "reconfigure does not duplicate subscriptions");
            await VerifyFit(panel, view, layout);
            await EconomyUIVerifierEditor.CaptureDeliveries(panel, view, layout.GetComponent<Canvas>());
            await VerifyLargeGoals(panel, view, layout, generator, placer, core);
            var camera = UnityEngine.Object.FindAnyObjectByType<CameraControllerAdapter>();
            camera.SetFirstPerson(true); await UniTask.Delay(600, ignoreTimeScale: true);
            Require(panel.SummaryView.Items.All(t => !t.gameObject.activeSelf) && panel.SummaryView.WalletItems[0].gameObject.activeInHierarchy, "walk keeps wallet and hides goal summary");
            camera.SetFirstPerson(false); await UniTask.Delay(600, ignoreTimeScale: true);
            Require(panel.SummaryView.Items.Where(t => t.gameObject.activeSelf).Count() == 3, "walk return restores goal summary");
            Deliver(app, ResourceTypeDomain.StoneBrick, 20); Deliver(app, ResourceTypeDomain.BuildingParts, 20);
            view.UpdateUIStatusWindow(core);
            Require(app.Current.IsComplete && panel.SummaryView.Items[0].text == "目標達成" && panel.EquipmentText.Contains("目標達成"), "final HUD");
            var current = app.Current.Count(ResourceTypeDomain.Wood);
            Require(core.AllocateStorage(0, 1, ResourceTypeDomain.Wood), "core accepts after final"); core.StoreResource(0, 1);
            Require(app.Current.Count(ResourceTypeDomain.Wood) == current + 1 && generator.Wallet.Current.Balance == wallet, "postfinal continues absorption");
            panel.SelectSkill(generator.Skills.Catalog.Nodes.First().Uid);
            Require(panel.EquipmentView.UpgradeButton.gameObject.activeSelf, "skill apply returns after core selection");
            panel.enabled = false;
            Require(Subscribers(app, "Changed") == 0 && Subscribers(app, "StageCompleted") == 0, "disable releases goal subscriptions");
            Debug.Log($"Delivery TARGETED checks passed: {_checks} assertions; rules, definition edit/save, actual core lifecycle, goals HUD and PC fit. Full gameplay loop deferred.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static int Subscribers(object target, string field) =>
        ((Delegate)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target))?.GetInvocationList().Length ?? 0;
    private static async UniTask VerifyLargeGoals(EconomyPanelAdapter panel, CellStatusViewAdapter view, HudLayoutAdapter layout,
        GridFieldGeneratorAdapter generator, CellPlacerAdapter placer, SingularityCellAdapter core)
    {
        var originalResources = layout.Resources;
        var resources = UnityEngine.Object.Instantiate(originalResources);
        var serialized = new SerializedObject(resources); var entries = serialized.FindProperty("initializedResourceArr");
        for (var i = 0; i < entries.arraySize; i++)
            entries.GetArrayElementAtIndex(i).FindPropertyRelative("resourceName").stringValue += "（長い名前の加工品を使った表示確認）";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var types = Enum.GetValues(typeof(ResourceTypeDomain)).Cast<ResourceTypeDomain>().Where(t => t != ResourceTypeDomain.None).ToArray();
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
