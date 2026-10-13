using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>実際の表示行とセル通知を使い、表示値・購読寿命・数量の非変更を確認する。</summary>
[InitializeOnLoad]
public static class UIStatusVerifierEditor
{
    private const string SessionKey = "FactoryGame.UIStatusVerifierEditor.Running";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static UIStatusVerifierEditor() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    public static void Run()
    {
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    public static void RunAssetsAndBuild()
    {
        VerifyAssets();
        GridFieldGeneratorVerifierEditor.RunPlayerBuild();
    }

    public static void VerifyAssets()
    {
        // 既存アセットの継承構造を変えてもラベルと参照が読めることを確認する。
        foreach (var guid in AssetDatabase.FindAssets("t:ProviderBaseDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var definition = AssetDatabase.LoadAssetAtPath<ProviderBaseDefinition>(path);
            Require(definition != null && definition.GetLabels().Length > 0, "serialized provider labels survive: " + path);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Field" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var cell in prefab.GetComponentsInChildren<CellBaseAdapter>(true))
                if (cell is IDataProvidableApplication source)
                    Require(source.GetDataProvider()?.ReadData().Count > 0, "prefab status binding survives: " + path);
        }
        Debug.Log("UI status asset checks passed: saved definitions, labels and prefab references.");
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyAsync().Forget();
    }

    private static async UniTaskVoid VerifyAsync()
    {
        try
        {
            var root = new GameObject("StatusCheck:Canvas", typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var panel = new GameObject("Panel", typeof(RectTransform)).transform;
            panel.SetParent(root.transform);
            var container = new GameObject("Rows", typeof(RectTransform)).transform;
            container.SetParent(panel);
            var view = root.AddComponent<CellStatusViewAdapter>();
            Set(view, "elementWindowTransform", panel);
            Set(view, "statusRowContainer", container);
            var rowInfo = new StatusRowInfoAdapter[3];
            var names = new[] { "TextParamLine", "GaugeParamLine", "StorageParamLine" };
            var kinds = new[] { UIStatusRowTypeApplication.Text, UIStatusRowTypeApplication.Gauge, UIStatusRowTypeApplication.Storage };
            for (var i = 0; i < rowInfo.Length; i++)
            {
                object info = new StatusRowInfoAdapter();
                Set(info, "rowType", kinds[i]);
                Set(info, "prefab", AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/UI/{names[i]}.prefab")
                    .GetComponent<UIStatusRowBaseAdapter>());
                rowInfo[i] = (StatusRowInfoAdapter)info;
            }
            Set(view, "statusRowPrefabs", rowInfo);
            await UniTask.Yield();
            var definition = Define<StorageProviderDefinition>(LabelEnumApplication.CellName, LabelEnumApplication.Location,
                LabelEnumApplication.Amount, LabelEnumApplication.Allocated);
            var a = Cell<StorageCellAdapter>();
            var b = Cell<StorageCellAdapter>();
            Set(a, "capacity", 10); Set(b, "capacity", 10);
            Set(a, "dataProvider", definition); Set(b, "dataProvider", definition);
            var providerA = a.GetDataProvider();
            var providerB = b.GetDataProvider();
            var snapshot = providerA.ReadData();
            Require(a.AllocateStorage(Vector3Int.left, 3, ResourceTypeDomain.Stone), "reserve initial stock");
            a.StoreResource(Vector3Int.left, 3);
            Require(((GaugeElementDataApplication)providerA.ReadData()[LabelEnumApplication.Amount]).Current == 3 &&
                    ((GaugeElementDataApplication)providerB.ReadData()[LabelEnumApplication.Amount]).Current == 0 &&
                    ((GaugeElementDataApplication)snapshot[LabelEnumApplication.Amount]).Current == 0,
                "shared definition does not share the selected cell or old snapshot");
            ((GaugeElementDataApplication)snapshot[LabelEnumApplication.Amount]).Current = 999;
            for (var i = 0; i < 10; i++) providerA.ReadData();
            Require(a.CurrentLoad == 3 && a.AllocatedAmount == 0, "reading and editing display snapshots cannot change quantities");

            view.UpdateUIStatusWindow(a);
            Require(a.AllocateStorage(Vector3Int.left, 2, ResourceTypeDomain.Stone), "reserve while selected");
            Require(GaugeText(view, LabelEnumApplication.Allocated) == "2/10", "reservation notification updates the selected row");
            a.StoreResource(Vector3Int.left, 2);
            Require(GaugeText(view, LabelEnumApplication.Amount) == "5/10", "commit notification updates the selected row");
            Require(a.AllocateStorage(Vector3Int.left, 1, ResourceTypeDomain.Stone), "reserve before cancellation");
            a.CancelStorage(Vector3Int.left, 1, ResourceTypeDomain.Stone);
            Require(GaugeText(view, LabelEnumApplication.Allocated) == "0/10" &&
                    a.TryExport(Vector3.zero, 2, out var amount, out _) && amount == 2 &&
                    GaugeText(view, LabelEnumApplication.Amount) == "3/10", "cancel and export update displayed quantities");
            view.UpdateUIStatusWindow(b);
            Require(Get(a, "StatusChanged") == null, "selection switch unsubscribes the previous source");
            Require(b.AllocateStorage(Vector3Int.left, 1, ResourceTypeDomain.Wood), "second selection reserves");
            b.StoreResource(Vector3Int.left, 1);
            Require(GaugeText(view, LabelEnumApplication.Amount) == "1/10", "second selection owns its provider");
            view.enabled = false;
            Require(!panel.gameObject.activeSelf && Get(b, "StatusChanged") == null && Rows(view).Count == 0,
                "disabling the view closes the panel and clears its subscription and rows");
            view.enabled = true;
            view.UpdateUIStatusWindow(b);
            Require(panel.gameObject.activeSelf && GaugeText(view, LabelEnumApplication.Amount) == "1/10",
                "re-enabled view can create a new selection session");
            view.SetStatusWindowActive(false);
            Require(Get(b, "StatusChanged") == null && Rows(view).Count == 0, "hide clears subscription and rendered rows");
            view.UpdateUIStatusWindow(b);
            UnityEngine.Object.Destroy(b.gameObject);
            await UniTask.Yield(); await UniTask.Yield();
            Require(!panel.gameObject.activeSelf && Rows(view).Count == 0, "deleting a selected source closes its view");

            var resource = Cell<ResourceCellAdapter>();
            var resourceDefinition = Define<ResourceProviderDefinition>(LabelEnumApplication.ResourceName);
            var database = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset");
            Set(resourceDefinition, "resourceDatabase", database); Set(resource, "resourceProvider", resourceDefinition);
            Set(resource, "resourceType", ResourceTypeDomain.Stone);
            view.UpdateUIStatusWindow(resource);
            var text = (TextUIStatusRowAdapter)Rows(view)[LabelEnumApplication.ResourceName];
            Require(((TextMeshProUGUI)Get(text, "statusText")).text == database.GetResourceByType(ResourceTypeDomain.Stone).ResourceName,
                "resource name survives the configuration/provider split");
            view.SetStatusWindowActive(false);

            await VerifyCustomStatus(view);
            await VerifyExtractorLifecycle(view);
            await VerifyCrafting(view);
            view.SetStatusWindowActive(false);
            ResourceReservationVerifierEditor.Run();
            CraftingVerifierEditor.RunRules();
            Debug.Log("UI status checks passed: independent sessions/snapshots, read-only quantities, reservation/commit/cancel/export, selection/hide/disable/re-enable/deletion, resource name, extractor/crafter progress and completion, zero-duration gauge.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static async UniTask VerifyCustomStatus(CellStatusViewAdapter view)
    {
        var definition = Define<StorageProviderDefinition>(LabelEnumApplication.CellName, LabelEnumApplication.Amount);
        var a = Cell<UIStatusFixtureCellAdapter>();
        var b = Cell<UIStatusFixtureCellAdapter>();
        a.Definition = b.Definition = definition;
        var providerA = a.GetDataProvider();
        var providerB = b.GetDataProvider();
        var snapshot = providerA.ReadData();
        Require(CellStatusProviderAdapter.CreateCustom(null, null) == null, "missing custom definition keeps null-provider behavior");
        try
        {
            CellStatusProviderAdapter.CreateCustom(definition, null);
            throw new InvalidOperationException("missing custom reader was accepted");
        }
        catch (ArgumentNullException) { }
        view.UpdateUIStatusWindow(a);
        var nameRow = (TextUIStatusRowAdapter)Rows(view)[LabelEnumApplication.CellName];
        Require(((TextMeshProUGUI)Get(nameRow, "statusText")).text == "Custom status fixture" &&
                GaugeText(view, LabelEnumApplication.Amount) == "2/10" && a.SubscriptionCount == 1,
            "a cell outside the central switch renders its dedicated text and gauge");
        a.ChangeDisplayValue(6);
        Require(GaugeText(view, LabelEnumApplication.Amount) == "6/10" &&
                ((GaugeElementDataApplication)snapshot[LabelEnumApplication.Amount]).Current == 2 &&
                ((GaugeElementDataApplication)providerB.ReadData()[LabelEnumApplication.Amount]).Current == 2,
            "custom notification updates UI without sharing sessions or old snapshots");
        ((GaugeElementDataApplication)snapshot[LabelEnumApplication.Amount]).Current = 999;
        for (var i = 0; i < 10; i++) view.UpdateUI();
        Require(a.StoredAmount == 7 && b.StoredAmount == 7 && a.DisplayValue == 6,
            "custom reads and snapshot edits do not change inventory or source state");
        var entries = definition.GetLabels();
        object changed = entries[0]; Set(changed, "name", "Changed after selection");
        entries[0] = (LabelNameDefinition)changed;
        typeof(ProviderBaseDefinition).GetField("labelEntries", Fields).SetValue(definition, entries);
        Require(providerA.ReadData()[LabelEnumApplication.CellName].StatusName == "CellName" &&
                a.GetDataProvider().ReadData()[LabelEnumApplication.CellName].StatusName == "Changed after selection",
            "custom session copies labels and new sessions see changed settings");
        view.UpdateUIStatusWindow(b);
        Require(a.SubscriptionCount == 0 && b.SubscriptionCount == 1, "custom selection switch releases subscription");
        a.ChangeDisplayValue(9);
        Require(GaugeText(view, LabelEnumApplication.Amount) == "2/10", "unselected custom source cannot update UI");
        view.enabled = false;
        Require(b.SubscriptionCount == 0 && Rows(view).Count == 0, "custom disable releases subscription and rows");
        view.enabled = true; view.UpdateUIStatusWindow(b);
        view.SetStatusWindowActive(false);
        Require(b.SubscriptionCount == 0 && Rows(view).Count == 0, "custom hide releases subscription and rows");
        view.UpdateUIStatusWindow(b);
        UnityEngine.Object.Destroy(b.gameObject);
        await UniTask.Yield(); await UniTask.Yield();
        Require(b.SubscriptionCount == 0 && Rows(view).Count == 0, "custom deletion releases subscription and rows");
        UnityEngine.Object.Destroy(a.gameObject); UnityEngine.Object.Destroy(definition);
        Debug.Log("Custom cell status checks passed: dedicated adapter, text/gauge, notification, independent snapshots/labels, unchanged inventory, selection/hide/disable/re-enable/deletion, missing definition/reader.");
    }

    private static async UniTask VerifyExtractorLifecycle(CellStatusViewAdapter view)
    {
        var root = new GameObject("StatusCheck:ExtractorGrid");
        var grid = root.AddComponent<GridFieldDatabaseAdapter>();
        Set(grid, "_gridCells", new CellBaseAdapter[3, 3]);
        Set(grid, "_gridSize", 3);
        var extractor = Cell<ExtractorCellAdapter>();
        extractor.transform.SetParent(root.transform);
        Set(extractor, "extractorProvider", Define<ExtractorProviderDefinition>(LabelEnumApplication.Amount, LabelEnumApplication.Progress));
        Set(extractor, "resourceType", ResourceTypeDomain.Stone); Set(extractor, "storageCapacity", 4);
        Set(extractor, "extractionAmount", 2); Set(extractor, "extractionSecond", 0.35f);
        grid.SaveCell(1, 1, extractor);
        extractor.InitializeSystem();
        view.UpdateUIStatusWindow(extractor);
        await UniTask.Delay(100);
        Require(extractor.CurrentLoad == 0 && extractor.ElapsedTime == 0, "no source does not start extraction");
        var resource = ExtractorSource(grid, root.transform, ResourceTypeDomain.Wood);
        await UniTask.Delay(100);
        Require(extractor.CurrentLoad == 0 && extractor.ElapsedTime == 0, "wrong resource type is not extracted");
        Set(resource, "resourceType", ResourceTypeDomain.Stone);
        extractor.InitializeSystem(); extractor.InitializeSystem();
        await Wait(() => Fill(view, LabelEnumApplication.Progress) > 0);
        Require(Fill(view, LabelEnumApplication.Progress) <= 1, "normal initialization publishes extraction progress");
        await Wait(() => extractor.CurrentLoad == 2);
        Require(GaugeText(view, LabelEnumApplication.Amount) == "2/4", "completion publishes existing extraction amount");
        await Wait(() => extractor.ElapsedTime > 0.02f);
        UnityEngine.Object.Destroy(resource.gameObject);
        await UniTask.Delay(450);
        Require(extractor.CurrentLoad == 2 && extractor.ElapsedTime == 0 && Fill(view, LabelEnumApplication.Progress) == 0,
            "source deletion cancels unfinished gain and resets progress without consuming inventory");
        resource = ExtractorSource(grid, root.transform, ResourceTypeDomain.Stone);
        await Wait(() => extractor.ElapsedTime > 0.02f);
        resource.enabled = false;
        await UniTask.Delay(450);
        Require(extractor.CurrentLoad == 2 && extractor.ElapsedTime == 0, "disabled source stops extraction");
        resource.enabled = true;
        await Wait(() => extractor.ElapsedTime > 0.02f);
        // 同じ種別でも別セルなら未完了進捗を引き継がない。旧抽出の残り時間より長く、
        // 新しい抽出時間より短い区間で、旧処理の早期確定がないことを確認する。
        await Wait(() => extractor.ElapsedTime > 0.22f);
        var replacedStone = resource;
        resource = ExtractorSource(grid, root.transform, ResourceTypeDomain.Stone);
        await UniTask.Delay(160);
        Require(extractor.CurrentLoad == 2, "same-type replacement discards old progress before quantity commit");
        UnityEngine.Object.Destroy(replacedStone.gameObject);
        var previous = resource;
        resource = ExtractorSource(grid, root.transform, ResourceTypeDomain.Wood);
        await UniTask.Delay(450);
        Require(extractor.CurrentLoad == 2 && extractor.ElapsedTime == 0, "replacement is re-read from grid, not stale adjacency");
        UnityEngine.Object.Destroy(previous.gameObject);
        Set(resource, "resourceType", ResourceTypeDomain.Stone);
        await Wait(() => extractor.ElapsedTime > 0.02f);
        extractor.enabled = false;
        Require(extractor.ElapsedTime == 0 && Fill(view, LabelEnumApplication.Progress) == 0, "disable synchronously discards progress");
        await UniTask.Delay(450);
        Require(extractor.CurrentLoad == 2, "disabled equipment retains stock without producing");
        extractor.enabled = true;
        await Wait(() => extractor.ElapsedTime > 0.02f);
        extractor.gameObject.SetActive(false);
        await UniTask.Delay(450);
        Require(extractor.CurrentLoad == 2 && extractor.ElapsedTime == 0, "inactive equipment cancels extraction");
        extractor.gameObject.SetActive(true);
        await Wait(() => extractor.CurrentLoad == 4);
        await UniTask.Delay(450);
        Require(extractor.CurrentLoad == 4 && extractor.ElapsedTime == 0, "re-enable resumes once and capacity remains bounded");
        Require(extractor.TryExport(Vector3.zero, 1, out var amount, out var type) && amount == 1 && type == ResourceTypeDomain.Stone,
            "existing export quantity and type survive lifecycle changes");
        await Wait(() => extractor.CurrentLoad == 4);
        Require(GaugeText(view, LabelEnumApplication.Amount) == "4/4", "remaining capacity clamps gain and UI stays current");
        extractor.TryExport(Vector3.zero, 2, out _, out _);
        await Wait(() => extractor.ElapsedTime > 0.02f);
        extractor.OnDisconnect();
        await UniTask.Delay(450);
        Require(extractor.CurrentLoad == 2 && extractor.ElapsedTime == 0, "disconnect stops until initialization");
        extractor.InitializeSystem();
        await Wait(() => extractor.ElapsedTime > 0.02f);
        UnityEngine.Object.Destroy(extractor.gameObject);
        await UniTask.Yield();
        Require(extractor == null, "destroy cancels the active runner without unobserved cancellation");
        view.SetStatusWindowActive(false);
        UnityEngine.Object.Destroy(root); await UniTask.Yield();
        Debug.Log("Extractor lifecycle checks passed: normal initialization, duplicate initialization, missing/late/wrong/replaced/deleted/disabled source, disable/re-enable, progress discard, inventory/capacity/export/UI, disconnect/reinitialize/destroy.");
    }

    private static ResourceCellAdapter ExtractorSource(GridFieldDatabaseAdapter grid, Transform parent, ResourceTypeDomain type)
    {
        var cell = Cell<ResourceCellAdapter>(); cell.transform.SetParent(parent);
        Set(cell, "resourceType", type); grid.SaveCell(1, 2, cell);
        return cell;
    }

    private static async UniTask VerifyCrafting(CellStatusViewAdapter view)
    {
        var gridObject = new GameObject("StatusCheck:Grid");
        var grid = gridObject.AddComponent<GridFieldDatabaseAdapter>();
        var row = new GameObject("Row").transform; row.SetParent(gridObject.transform);
        var crafter = Cell<CrafterCellAdapter>(); crafter.transform.SetParent(row);
        Set(crafter, "crafterProvider", Define<CrafterProviderDefinition>(LabelEnumApplication.Progress,
            LabelEnumApplication.LeftStorage, LabelEnumApplication.RightStorage, LabelEnumApplication.OutputStorage));
        Set(crafter, "ingredientCapacity", 10); Set(crafter, "exporterCapacity", 2);
        Set(crafter, "importDirection", DirectionsDomain.Left | DirectionsDomain.Right);
        var recipe = new RecipeDataDefinition();
        Set(recipe, "ingredients", new[]
        {
            new IngredientDefinition { resourceType = ResourceTypeDomain.Stone, requiredAmount = 2 },
            new IngredientDefinition { resourceType = ResourceTypeDomain.Wood, requiredAmount = 2 }
        });
        Set(recipe, "result", ResourceTypeDomain.Wood); Set(recipe, "resultAmount", 2); Set(recipe, "craftSecond", 0.3f);
        var database = ScriptableObject.CreateInstance<RecipeDatabaseDefinition>(); database.recipes = new[] { recipe };
        Set(crafter, "recipeDatabase", database); grid.InitializeCells(1); crafter.InitializeSystem();
        view.UpdateUIStatusWindow(crafter);
        Require(Fill(view, LabelEnumApplication.Progress) == 0, "idle zero-duration progress is finite");
        Require(crafter.AllocateStorage(Vector3Int.left, 2, ResourceTypeDomain.Stone) &&
                crafter.AllocateStorage(Vector3Int.right, 2, ResourceTypeDomain.Wood), "craft inputs reserve");
        crafter.StoreResource(Vector3Int.left, 2); crafter.StoreResource(Vector3Int.right, 2);
        Require(GaugeText(view, LabelEnumApplication.LeftStorage) == "2/10", "craft input notification reaches UI");
        await Wait(() => Fill(view, LabelEnumApplication.Progress) > 0);
        await Wait(() => crafter.ExportStorageAmount == 2);
        Require(GaugeText(view, LabelEnumApplication.OutputStorage) == "2/2" &&
                GaugeText(view, LabelEnumApplication.LeftStorage) == "0/10" &&
                Fill(view, LabelEnumApplication.Progress) == 0, "craft completion publishes the full committed state");
        var before = crafter.ExportStorageAmount;
        for (var i = 0; i < 5; i++) view.UpdateUI();
        Require(crafter.ExportStorageAmount == before, "re-render cannot produce or consume resources");
        view.SetStatusWindowActive(false);
        UnityEngine.Object.Destroy(gridObject); await UniTask.Yield();
    }

    private static T Cell<T>() where T : CellBaseAdapter
    {
        var root = new GameObject("StatusCheck:" + typeof(T).Name);
        new GameObject("Model").transform.SetParent(root.transform);
        return root.AddComponent<T>();
    }
    private static T Define<T>(params LabelEnumApplication[] labels) where T : ProviderBaseDefinition
    {
        var definition = ScriptableObject.CreateInstance<T>();
        var entries = new LabelNameDefinition[labels.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            object entry = new LabelNameDefinition(); Set(entry, "label", labels[i]); Set(entry, "name", labels[i].ToString());
            entries[i] = (LabelNameDefinition)entry;
        }
        typeof(ProviderBaseDefinition).GetField("labelEntries", Fields).SetValue(definition, entries);
        return definition;
    }
    private static Dictionary<LabelEnumApplication, UIStatusRowBaseAdapter> Rows(CellStatusViewAdapter view)
        => (Dictionary<LabelEnumApplication, UIStatusRowBaseAdapter>)Get(Get(view, "_uiRenderer"), "_renderedUI");
    private static string GaugeText(CellStatusViewAdapter view, LabelEnumApplication label)
        => ((TextMeshProUGUI)typeof(GaugeUIStatusRowAdapter).GetField("gaugeText", Fields).GetValue(Rows(view)[label])).text;
    private static float Fill(CellStatusViewAdapter view, LabelEnumApplication label)
        => ((Image)typeof(GaugeUIStatusRowAdapter).GetField("gauge", Fields).GetValue(Rows(view)[label])).fillAmount;
    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static async UniTask Wait(Func<bool> predicate)
    {
        for (var i = 0; i < 200; i++) { if (predicate()) return; await UniTask.Delay(20, ignoreTimeScale: true); }
        throw new TimeoutException("Expected UI state was not reached.");
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
