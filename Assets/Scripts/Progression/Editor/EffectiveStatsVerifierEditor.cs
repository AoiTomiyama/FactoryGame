using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EffectiveStatsVerifierEditor
{
    private const string SessionKey = "FactoryGame.EffectiveStatsVerification";
    private static int _checks;
    static EffectiveStatsVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyPlay().Forget();
    };

    public static void Run()
    {
        _checks = 0;
        VerifyPrefabBaselines();
        Require(EffectiveStatsRulesDomain.Calculate(7, 1, 0) == 7, "baseline");
        Require(EffectiveStatsRulesDomain.Calculate(7, 3, 25) == 26, "multiply before floor");
        Require(EffectiveStatsRulesDomain.Calculate(1, 1, 25) == 1, "positive minimum");
        Require(EffectiveStatsRulesDomain.Calculate(int.MaxValue, 1, 0) == int.MaxValue, "int endpoint");
        EffectiveStatsRulesDomain.ValidateBase(477218588);
        Throws<OverflowException>(() => EffectiveStatsRulesDomain.ValidateBase(477218589));
        Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.Calculate(0, 1, 0));
        Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.Calculate(-1, 1, 0));
        Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.Calculate(1, 0, 0));
        Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.Calculate(1, 4, 0));
        Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.Calculate(1, 1, -1));
        Throws<ArgumentOutOfRangeException>(() => EffectiveStatsRulesDomain.Calculate(1, 1, 51));
        var effects = new SharedStatEffectsApplication();
        var calls = 0;
        effects.Changed += () => calls++;
        Require(effects.TrySetBonus(StatBranchDomain.Storage, 25) &&
                effects.TrySetBonus(StatBranchDomain.Storage, 25) && calls == 1 &&
                !effects.TrySetBonus(StatBranchDomain.Storage, 0) &&
                !effects.TrySetBonus(StatBranchDomain.Storage, 51), "monotonic effects, idempotence, upper bound");
        ResourceReservationVerifierEditor.Run();
        CraftingVerifierEditor.RunRules();
        CellConnectionVerifierEditor.Run();
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void VerifyPrefabBaselines()
    {
        foreach (var name in new[] { "ExtractorWood", "ExtractorStone", "Conveyor", "ExportConveyor", "Storage", "Crafter" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Field/General/F_{name}Cell.prefab");
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                var cell = instance.GetComponent<CellBaseAdapter>();
                cell.ConfigureProgression(new SharedStatEffectsApplication());
                Require(cell.EquipmentLevel == 1, name + " initial level");
                switch (cell)
                {
                    case ExtractorCellAdapter extractor:
                        Require(extractor.ExtractionAmount == Base(extractor, "extractionAmount") &&
                                extractor.StorageCapacity == Base(extractor, "storageCapacity"), name + " unchanged extraction and capacity");
                        break;
                    case ConveyorCellAdapter conveyor:
                        Require(conveyor.TransferAmount == (int)typeof(ConveyorCellAdapter)
                            .GetField("transferAmount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(conveyor), name + " unchanged transfer");
                        break;
                    case StorageCellAdapter storage:
                        Require(storage.Capacity == Base(storage, "capacity"), "unchanged storage");
                        Set(storage, "capacity", 477218589);
                        Throws<OverflowException>(() => storage.ConfigureProgression(new SharedStatEffectsApplication()));
                        break;
                    case CrafterCellAdapter crafter:
                        Require(crafter.IngredientCapacity == Base(crafter, "ingredientCapacity") &&
                                crafter.ExporterCapacity == Base(crafter, "exporterCapacity"), "unchanged crafting capacities");
                        break;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
    }

    private static async UniTaskVoid VerifyPlay()
    {
        try
        {
            await UniTask.Yield();
            var grid = GridFieldDatabaseAdapter.Instance;
            var effects = grid.StatEffects;
            // 資源セルを含む既存の隣接参照が検証用の置換先を占有しないよう、空の区画を用意する。
            for (var x = 39; x <= 46; x++)
            for (var z = 39; z <= 48; z++)
                Install<EmptyCellAdapter>(grid, "Empty", x, z);
            var storage = Install<StorageCellAdapter>(grid, "Storage", 45, 45);
            Require(storage.Capacity == 50 && storage.EquipmentLevel == 1, "storage baseline prefab");
            Require(storage.AllocateStorage(Vector3Int.back, 30, ResourceTypeDomain.Wood), "reserve stock");
            storage.StoreResource(Vector3Int.back, 20);
            var identity = storage.Identity;
            var notifications = 0;
            storage.StatusChanged += () => notifications++;
            Require(storage.TrySetEquipmentLevel(2) && effects.TrySetBonus(StatBranchDomain.Storage, 25), "grow capacity");
            Require(storage.Capacity == 125 && storage.CurrentLoad == 20 && storage.AllocatedAmount == 10 &&
                    ReferenceEquals(identity, storage.Identity) && notifications == 2, "stock, reservation, identity and UI preserved");
            storage.StoreResource(Vector3Int.back, 10);
            Require(storage.CurrentLoad == 30 && storage.AllocatedAmount == 0, "old reservation commits after growth");
            Require(!storage.TrySetEquipmentLevel(1) && !storage.TrySetEquipmentLevel(4) && storage.EquipmentLevel == 2,
                "level decrease and upper bound refused");
            var fresh = Install<StorageCellAdapter>(grid, "Storage", 44, 45);
            Require(fresh.Capacity == 62 && fresh.EquipmentLevel == 1, "new cell receives shared effect and fresh level");
            fresh.gameObject.SetActive(false);
            effects.TrySetBonus(StatBranchDomain.Storage, 50);
            fresh.gameObject.SetActive(true);
            Require(fresh.Capacity == 75 && storage.Capacity == 150, "existing and reenabled cells share effects");
            Require(!grid.GetCell(50, 50).SupportsProgression && !grid.GetCell(50, 46).SupportsProgression &&
                    !grid.GetCell(50, 50).TrySetEquipmentLevel(2), "singularity and resource have no stats");

            var extractor = Install<ExtractorCellAdapter>(grid, "ExtractorWood", 50, 47, 180, cell =>
            { Set(cell, "extractionAmount", 4); Set(cell, "extractionSecond", .6f); });
            Require(extractor.ExtractionAmount == 4 && extractor.StorageCapacity == 15, "extractor baseline and storage branch");
            await Wait(() => extractor.ElapsedTime > .05f);
            extractor.TrySetEquipmentLevel(2);
            effects.TrySetBonus(StatBranchDomain.Extraction, 25);
            await Wait(() => extractor.CurrentLoad > 0);
            Require(extractor.CurrentLoad == 4 && extractor.ExtractionAmount == 10 && extractor.StorageCapacity == 30,
                "active extraction cycle keeps starting amount");
            await Wait(() => extractor.CurrentLoad > 4);
            Require(extractor.CurrentLoad == 14, "next extraction cycle uses effective amount");
            extractor.enabled = false;

            var exporter = Install<ExportConveyorCellAdapter>(grid, "ExportConveyor", 45, 46);
            Require(exporter.TransferAmount == 5, "export conveyor baseline");
            await Wait(() => GetResourceId(exporter) != 0);
            var pool = ResourceItemObjectPoolAdapter.Instance;
            var id = GetResourceId(exporter);
            Require(pool.TakeResourceDataById(id).amount == 5, "transfer captures base quantity");
            exporter.TrySetEquipmentLevel(2);
            effects.TrySetBonus(StatBranchDomain.Transfer, 50);
            Require(exporter.TransferAmount == 15 && GetResourceId(exporter) == id &&
                    pool.TakeResourceDataById(id).amount == 5, "in-flight ID and quantity fixed after growth");
            var receiver = Install<StorageCellAdapter>(grid, "Storage", 45, 47);
            await Wait(() => receiver.CurrentLoad >= 5);
            await Wait(() => GetResourceId(exporter) != 0 && GetResourceId(exporter) != id);
            Require(pool.TakeResourceDataById(GetResourceId(exporter)).amount == 15, "next export uses effective quantity");
            var conveyor = Install<ConveyorCellAdapter>(grid, "Conveyor", 44, 46);
            Require(conveyor.TransferAmount == 7 && conveyor.TrySetEquipmentLevel(3) && conveyor.TransferAmount == 22,
                "normal conveyor uses same rule with floor");

            var crafter = Install<CrafterCellAdapter>(grid, "Crafter", 40, 40, 0, cell =>
            {
                var recipe = new RecipeDataDefinition();
                Set(recipe, "ingredients", new[] { new IngredientDefinition { resourceType = ResourceTypeDomain.Stone, requiredAmount = 3 } });
                Set(recipe, "result", ResourceTypeDomain.Wood); Set(recipe, "resultAmount", 2); Set(recipe, "craftSecond", .6f);
                var database = ScriptableObject.CreateInstance<RecipeDatabaseDefinition>(); database.recipes = new[] { recipe };
                Set(cell, "recipeDatabase", database); Set(cell, "importDirection", DirectionsDomain.Left);
            });
            Require(crafter.IngredientCapacity == 7 && crafter.ExporterCapacity == 15, "crafter capacities share storage branch");
            crafter.AllocateStorage(Vector3Int.left, 3, ResourceTypeDomain.Stone); crafter.StoreResource(Vector3Int.left, 3);
            await Wait(() => GetOperation(crafter) != null);
            var operation = GetOperation(crafter);
            crafter.AllocateStorage(Vector3Int.left, 2, ResourceTypeDomain.Stone);
            crafter.TrySetEquipmentLevel(3);
            Require(ReferenceEquals(operation, GetOperation(crafter)) && crafter.IngredientCapacity == 22 &&
                    crafter.ExporterCapacity == 45 && crafter.GetInput(DirectionsDomain.Left).Allocated == 2,
                "growth preserves active plan and input reservation");
            await Wait(() => crafter.ExportStorageAmount > 0);
            crafter.enabled = false;
            Require(crafter.ExportStorageAmount == 2 && crafter.GetInput(DirectionsDomain.Left).Amount == 0 &&
                    crafter.GetInput(DirectionsDomain.Left).Allocated == 2 &&
                    operation.CurrentStage == CraftingOperationApplication.Stage.Completed, "fixed plan completes without scaled recipe");
            crafter.StoreResource(Vector3Int.left, 2);
            Require(crafter.GetInput(DirectionsDomain.Left).Amount == 2, "old crafting reservation commits");
            var beforeDestroy = notifications;
            UnityEngine.Object.Destroy(storage.gameObject);
            await UniTask.Yield();
            // 削除後の通知先が共有状態へ残っていないことを確認する。
            var handlers = (Action)typeof(SharedStatEffectsApplication).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(effects);
            Require(Array.TrueForAll(handlers.GetInvocationList(), callback => !ReferenceEquals(callback.Target, storage)) &&
                    notifications == beforeDestroy, "destroy detaches shared subscription");
            Require(new SharedStatEffectsApplication().GetBonus(StatBranchDomain.Storage) == 0, "new session resets effects");
            Debug.Log($"Effective stats checks passed: {_checks} assertions; rules, limits, baseline, active extraction/transfer/crafting, reservations, existing/new/reenabled cells and subscription lifetime.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static T Install<T>(GridFieldDatabaseAdapter grid, string prefab, int x, int z, float rotation = 0, Action<T> configure = null)
        where T : CellBaseAdapter
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Field/General/F_{prefab}Cell.prefab");
        var old = grid.GetCell(x, z);
        var cell = UnityEngine.Object.Instantiate(source, old.transform.position, Quaternion.Euler(0, rotation, 0), old.transform.parent).GetComponent<T>();
        configure?.Invoke(cell);
        grid.SaveCell(x, z, cell);
        if (old is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
        UnityEngine.Object.Destroy(old.gameObject);
        cell.InitializeSystem();
        return cell;
    }
    private static int GetResourceId(ConveyorCellAdapter cell) => (int)typeof(ConveyorCellAdapter)
        .GetProperty("ResourceId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cell);
    private static CraftingOperationApplication GetOperation(CrafterCellAdapter cell) => (CraftingOperationApplication)typeof(CrafterCellAdapter)
        .GetField("_operation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cell);
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static int Base(object target, string field) => (int)target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static async UniTask Wait(Func<bool> condition) => await UniTask.WaitUntil(condition).Timeout(TimeSpan.FromSeconds(12));
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { _checks++; return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Effective stats check failed: " + message);
    }
}
