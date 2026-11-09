using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SkillTreeVerifierEditor
{
    private const string SessionKey = "FactoryGame.SkillTreeVerification";
    private static int _checks;
    static SkillTreeVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyPlay().Forget();
    };

    public static void Run()
    {
        _checks = 0;
        VerifyRules();
        CurrencyWalletVerifierEditor.Run();
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        // 自動検証の再生前に音声出力を止める。
        EditorUtility.audioMasterMute = true;
        Require(EditorUtility.audioMasterMute, "editor muted before play");
        SessionState.SetInt(SessionKey + ".checks", _checks);
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    private static SkillTreeCatalogDomain Catalog() => AssetDatabase
        .LoadAssetAtPath<SkillTreeDefinition>("Assets/Scripts/ScriptableObject/SkillTree.asset").CreateCatalog();
    private static SkillNodeDomain Node(string uid, string prerequisite = null, long price = 50) => new(uid, uid, price, prerequisite);

    private static void VerifyRules()
    {
        EffectiveStatsVerifierEditor.VerifyRules();
        var catalog = Catalog(); VerifyTypeRules(catalog);
        Require(typeof(SkillTreeDefinition.Node).GetField("hasEffect") == null && typeof(SkillTreeDefinition.Node).GetField("branch") == null && typeof(SkillTreeDefinition.Node).GetField("bonusPercent") == null, "obsolete Inspector fields removed");
        Invalid(() => new SkillTreeCatalogDomain(null));
        Invalid(() => new SkillTreeCatalogDomain(Array.Empty<SkillNodeDomain>()));
        Invalid(() => new SkillTreeCatalogDomain(new SkillNodeDomain[] { null }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { new SkillNodeDomain("a", "", 50, null) }));
        foreach (var price in new long[] { 0, -1 }) Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", price: price) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a"), Node("a") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", "missing") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", "a") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", "b"), Node("b", "a") }));
        var input = new[] { Node("a", price: long.MaxValue) }; var snapshot = new SkillTreeCatalogDomain(input); input[0] = Node("changed");
        Require(snapshot.TryGet("a", out _) && !snapshot.TryGet("changed", out _), "definition snapshot");
        var rich = new CurrencyWalletApplication(long.MaxValue);
        Require(new SkillTreeApplication(snapshot, rich, new SharedStatEffectsApplication()).Purchase("a") == SkillPurchaseResultApplication.Success && rich.Current.Balance == 0, "long maximum price");
        var wallet = new CurrencyWalletApplication(catalog.Nodes.Sum(node => node.Price) + 750); var effects = new SharedStatEffectsApplication(); var tree = new SkillTreeApplication(catalog, wallet, effects);
        Require(tree.Purchase(null) == SkillPurchaseResultApplication.UnknownNode && tree.Purchase("missing") == SkillPurchaseResultApplication.UnknownNode, "unknown nodes");
        var notifications = 0; var effectNotifications = 0; var reentries = 0; var callbacksValid = true;
        wallet.Changed += state => { callbacksValid &= tree.Purchase("basic-research") == SkillPurchaseResultApplication.Busy; reentries++; };
        tree.Changed += () => throw new InvalidOperationException("expected subscriber failure");
        tree.Changed += () => { notifications++; callbacksValid &= tree.Purchase("storage-capacity-2") == SkillPurchaseResultApplication.Busy; };
        effects.Changed += () => throw new InvalidOperationException("expected effect subscriber failure");
        effects.Changed += () => { effectNotifications++; callbacksValid &= wallet.Current.ReservedExpense == 0; };
        foreach (var node in catalog.Nodes) Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.Success, "type transaction " + node.Uid);
        Require(notifications == catalog.Nodes.Count && effectNotifications == catalog.Nodes.Count(node => node.CellType.HasValue) && reentries == catalog.Nodes.Count * 2 && wallet.Current.Balance == 750 && wallet.Current.ReservedExpense == 0, "type transaction total and notification isolation");
        Require(callbacksValid && wallet.LastNotificationException == null && tree.LastNotificationException != null && effects.LastNotificationException != null, "callbacks and exceptions recorded");
        var poor = new CurrencyWalletApplication(49);
        Require(new SkillTreeApplication(catalog, poor, new SharedStatEffectsApplication()).Purchase("basic-research") == SkillPurchaseResultApplication.InsufficientFunds && poor.Current.Balance == 49, "insufficient funds");
        var reserved = new CurrencyWalletApplication(100); reserved.TryReserveExpense(51, out var expense);
        Require(new SkillTreeApplication(catalog, reserved, new SharedStatEffectsApplication()).Purchase("basic-research") == SkillPurchaseResultApplication.InsufficientFunds, "reserved expense respected"); expense.TryCancel();
        var fresh = new SkillTreeApplication(catalog, new CurrencyWalletApplication(100), new SharedStatEffectsApplication());
        Require(!fresh.IsOwned("basic-research") && fresh.CanPurchase("basic-research") == SkillPurchaseResultApplication.Success, "session resets acquisitions");
    }

    private static void VerifyTypeRules(SkillTreeCatalogDomain catalog)
    {
        Require(catalog.Nodes.Count == 23, "root, eight ability chains and wood extraction through VIII");
        foreach (var node in catalog.Nodes)
        {
            Require(node.Price == (node.CellType.HasValue ? int.Parse(node.Uid.Substring(node.Uid.LastIndexOf('-') + 1)) * 50 : 50), "initial ability price");
            if (node.CellType.HasValue) Require(SkillTreeCatalogDomain.SupportsStat(node.CellType.Value, node.Stat), "ability belongs to target type");
        }
        SkillNodeDomain Effect(string uid, CellTypeDomain type, EquipmentStatDomain stat, decimal multiplier = 2, int addition = 0, string previous = null)
            => new(uid, uid, 50, previous, type, stat, multiplier, addition);
        Invalid(() => new SkillTreeCatalogDomain(new[] { Effect("a", CellTypeDomain.Crossing, EquipmentStatDomain.TransferAmount) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Effect("a", CellTypeDomain.Storage, EquipmentStatDomain.InputCapacity) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Effect("a", CellTypeDomain.Storage, EquipmentStatDomain.Capacity, -1) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Effect("a", CellTypeDomain.Storage, EquipmentStatDomain.Capacity, 1, -1) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Effect("a", CellTypeDomain.Storage, EquipmentStatDomain.Capacity), Effect("b", CellTypeDomain.Storage, EquipmentStatDomain.Capacity) }));
        var wallet = new CurrencyWalletApplication(catalog.Nodes.Sum(node => node.Price) + 750); var effects = new SharedStatEffectsApplication(); var tree = new SkillTreeApplication(catalog, wallet, effects);
        var changed = 0; var commits = 0;
        tree.Changed += () => { changed++; Require(tree.Purchase("wood-extraction-1") == SkillPurchaseResultApplication.Busy, "ability change reentry guarded"); };
        wallet.Changed += state => { if (state.ReservedExpense == 0) { commits++; foreach (var node in catalog.Nodes.Where(n => n.CellType.HasValue && tree.IsOwned(n.Uid)))
            Require(effects.GetModifier(node.CellType.Value, node.Stat).Multiplier >= node.Modifier.Multiplier, "commit sees configured ability"); } };
        Require(tree.Purchase("wood-extraction-2") == SkillPurchaseResultApplication.PrerequisiteMissing, "ability prerequisite");
        foreach (var node in catalog.Nodes)
        {
            Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.Success, "purchase configured ability " + node.Uid);
            var paid = wallet.Current.Balance;
            Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.AlreadyOwned && wallet.Current.Balance == paid, "ability purchase pays once");
        }
        Require(changed == catalog.Nodes.Count && commits == catalog.Nodes.Count && wallet.Current.Balance == 750 && wallet.Current.ReservedExpense == 0, "ability totals and notifications");
        Require(effects.GetModifier(CellTypeDomain.Conveyor, EquipmentStatDomain.TransferAmount).Multiplier == 1, "unrelated conveyor unchanged");
        var arbitrary = new SharedStatEffectsApplication();
        arbitrary.RegisterBase(CellTypeDomain.Storage, EquipmentStatDomain.Capacity, 50);
        arbitrary.RegisterBase(CellTypeDomain.ExtractorWood, EquipmentStatDomain.ExtractionAmount, 4);
        arbitrary.RegisterBase(CellTypeDomain.ExtractorWood, EquipmentStatDomain.Capacity, 10);
        var arbitraryWallet = new CurrencyWalletApplication(500);
        var custom = new SkillTreeApplication(new SkillTreeCatalogDomain(new[] {
            Effect("capacity-a", CellTypeDomain.Storage, EquipmentStatDomain.Capacity, 1.5m, 7),
            Effect("capacity-b", CellTypeDomain.Storage, EquipmentStatDomain.Capacity, 2.25m, 3, "capacity-a"),
            Effect("capacity-down", CellTypeDomain.Storage, EquipmentStatDomain.Capacity, 1, 0, "capacity-b"),
            Effect("extract", CellTypeDomain.ExtractorWood, EquipmentStatDomain.ExtractionAmount, 1.25m, 3),
            Effect("stock", CellTypeDomain.ExtractorWood, EquipmentStatDomain.Capacity, 0, 80)
        }), arbitraryWallet, arbitrary);
        Require(custom.Purchase("capacity-a") == SkillPurchaseResultApplication.Success && EffectiveStatsRulesDomain.Calculate(50, arbitrary.GetModifier(CellTypeDomain.Storage, EquipmentStatDomain.Capacity)) == 82, "arbitrary multiplier and addition");
        Require(custom.Purchase("capacity-b") == SkillPurchaseResultApplication.Success && EffectiveStatsRulesDomain.Calculate(50, arbitrary.GetModifier(CellTypeDomain.Storage, EquipmentStatDomain.Capacity)) == 115, "node values replace previous values without compounding");
        var balance = arbitraryWallet.Current.Balance;
        Require(custom.Purchase("capacity-down") == SkillPurchaseResultApplication.InvalidEffect && arbitraryWallet.Current.Balance == balance && !custom.IsOwned("capacity-down"), "decreasing existing capacity refused without payment");
        Require(custom.Purchase("extract") == SkillPurchaseResultApplication.Success && EffectiveStatsRulesDomain.Calculate(4, arbitrary.GetModifier(CellTypeDomain.ExtractorWood, EquipmentStatDomain.ExtractionAmount)) == 8 && arbitrary.GetModifier(CellTypeDomain.ExtractorWood, EquipmentStatDomain.Capacity).Multiplier == 1, "extraction and capacity configured independently");
        Require(custom.Purchase("stock") == SkillPurchaseResultApplication.Success && EffectiveStatsRulesDomain.Calculate(10, arbitrary.GetModifier(CellTypeDomain.ExtractorWood, EquipmentStatDomain.Capacity)) == 80, "absolute value through zero multiplier and addition");
        var changedWallet = new CurrencyWalletApplication(100); var changedEffects = new SharedStatEffectsApplication();
        var changedTree = new SkillTreeApplication(catalog, changedWallet, changedEffects);
        changedWallet.Changed += state => { if (state.ReservedExpense > 0) typeof(SharedStatEffectsApplication).GetMethod("ApplyWithoutNotification", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(changedEffects, new object[] { CellTypeDomain.Storage, EquipmentStatDomain.Capacity, new StatModifierDomain(2, 0) }); };
        Require(changedTree.Purchase("basic-research") == SkillPurchaseResultApplication.EffectsChanged && changedWallet.Current.Balance == 100 && changedWallet.Current.ReservedExpense == 0, "shared ability rechecked after reservation");
    }

    private static async UniTaskVoid VerifyPlay()
    {
        try
        {
            _checks = SessionState.GetInt(SessionKey + ".checks", 0);
            AudioListener.volume = 0;
            await UniTask.Yield();
            var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
            var grid = GridFieldDatabaseAdapter.Instance;
            var tree = generator.Skills;
            generator.Wallet.TryReserveIncome(tree.Catalog.Nodes.Sum(node => node.Price) + 250, out var income); income.TryCommit();
            var names = new[] { "ExtractorWood", "ExtractorStone", "Storage", "Crafter", "ExportConveyor" };
            var existing = new List<CellBaseAdapter>(); var inactive = new List<CellBaseAdapter>();
            for (var index = 0; index < names.Length; index++)
            {
                existing.Add(Install(grid, names[index], 20 + index, 20));
                var cell = Install(grid, names[index], 20 + index, 22); cell.gameObject.SetActive(false); inactive.Add(cell);
            }
            var storage = (StorageCellAdapter)existing[2];
            storage.AllocateStorage(Vector3Int.back, 30, ResourceTypeDomain.Wood); storage.StoreResource(Vector3Int.back, 20);
            var identity = storage.Identity;
            for (var x = 27; x <= 30; x++) for (var z = 21; z <= 23; z++) Install(grid, "Empty", x, z);
            var crossing = (CrossingCellAdapter)Install(grid, "Crossing", 28, 22);
            var receiver = (StorageCellAdapter)Install(grid, "Storage", 29, 22);
            receiver.InitializeSystem(); crossing.InitializeSystem();
            Require(crossing.AllocateStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone), "crossing reservation before shared growth");
            var pending = (System.Collections.IDictionary)typeof(CrossingCellAdapter).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(crossing);
            var transfer = pending[Vector3Int.right];
            Require(tree.Purchase("basic-research") == SkillPurchaseResultApplication.Success, "scene root");

            var activeExtractor = (ExtractorCellAdapter)Install(grid, "ExtractorWood", 50, 47, 180, cell => { Set(cell, "extractionAmount", 2); Set(cell, "extractionSecond", .4f); });
            await Wait(() => activeExtractor.ElapsedTime > .05f);
            Require(tree.Purchase("wood-extraction-1") == SkillPurchaseResultApplication.Success, "shared purchase during extraction");
            await Wait(() => activeExtractor.CurrentLoad > 0);
            Require(activeExtractor.CurrentLoad == 2 && activeExtractor.ExtractionAmount == 4, "active extraction keeps captured quantity");
            await Wait(() => activeExtractor.CurrentLoad > 2);
            Require(activeExtractor.CurrentLoad == 6, "next extraction uses type level"); activeExtractor.enabled = false;

            for (var x = 44; x <= 46; x++) for (var z = 44; z <= 48; z++) Install(grid, "Empty", x, z);
            var feed = (StorageCellAdapter)Install(grid, "Storage", 45, 45);
            Require(feed.AllocateStorage(Vector3Int.back, 20, ResourceTypeDomain.Wood), "feed reservation"); feed.StoreResource(Vector3Int.back, 20);
            Require(feed.CurrentLoad == 20, "feed stock before exporter");
            var exporter = (ExportConveyorCellAdapter)Install(grid, "ExportConveyor", 45, 46);
            Require(ReferenceEquals(typeof(ExportConveyorCellAdapter).GetField("_backwardCellBase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(exporter), feed), "exporter connected to test feed; actual=" + ((CellBaseAdapter)typeof(ExportConveyorCellAdapter).GetField("_backwardCellBase", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(exporter)) + "; feed=" + feed + "; difference=" + (exporter.transform.position - feed.transform.position));
            await Wait(() => ResourceId(exporter) != 0);
            var id = ResourceId(exporter); var pool = ResourceItemObjectPoolAdapter.Instance;
            Require(pool.TakeResourceDataById(id).amount == 5, "export captures original amount");
            Require(tree.Purchase("export-transfer-1") == SkillPurchaseResultApplication.Success && ResourceId(exporter) == id && pool.TakeResourceDataById(id).amount == 5, "type purchase preserves in-flight export identity and quantity");
            var output = (StorageCellAdapter)Install(grid, "Storage", 45, 47);
            await Wait(() => output.CurrentLoad >= 5);
            await Wait(() => ResourceId(exporter) != 0 && ResourceId(exporter) != id);
            Require(pool.TakeResourceDataById(ResourceId(exporter)).amount == 10, "next export uses shared level"); exporter.enabled = false;

            var activeCrafter = (CrafterCellAdapter)Install(grid, "Crafter", 40, 40, 0, cell => {
                var recipe = new RecipeDataDefinition(); Set(recipe, "ingredients", new[] { new IngredientDefinition { resourceType = ResourceTypeDomain.Stone, requiredAmount = 3 } });
                Set(recipe, "result", ResourceTypeDomain.Wood); Set(recipe, "resultAmount", 2); Set(recipe, "craftSecond", .4f);
                var database = ScriptableObject.CreateInstance<RecipeDatabaseDefinition>(); database.recipes = new[] { recipe };
                Set(cell, "recipeDatabase", database); Set(cell, "importDirection", DirectionsDomain.Left);
            });
            activeCrafter.AllocateStorage(Vector3Int.left, 3, ResourceTypeDomain.Stone); activeCrafter.StoreResource(Vector3Int.left, 3);
            await Wait(() => Operation(activeCrafter) != null);
            var operation = Operation(activeCrafter); activeCrafter.AllocateStorage(Vector3Int.left, 2, ResourceTypeDomain.Stone);
            Require(tree.Purchase("crafter-input-1") == SkillPurchaseResultApplication.Success && ReferenceEquals(operation, Operation(activeCrafter)) && activeCrafter.GetInput(DirectionsDomain.Left).Allocated == 2, "shared purchase preserves active crafting and reservation");
            await Wait(() => activeCrafter.ExportStorageAmount > 0); activeCrafter.enabled = false;
            Require(activeCrafter.ExportStorageAmount == 2 && operation.CurrentStage == CraftingOperationApplication.Stage.Completed, "active recipe remains unchanged");
            activeCrafter.StoreResource(Vector3Int.left, 2);
            Require(activeCrafter.GetInput(DirectionsDomain.Left).Amount == 2, "old crafting reservation commits");

            foreach (var node in tree.Catalog.Nodes.Where(node => node.CellType.HasValue))
            {
                var before = existing.Select(Stats).ToArray();
                if (!tree.IsOwned(node.Uid)) Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.Success, "scene ability " + node.Uid);
                for (var index = 0; index < names.Length; index++)
                {
                    var after = Stats(existing[index]);
                    foreach (var pair in after)
                        if (existing[index].ProgressionType != node.CellType || pair.Key != node.Stat)
                            Require(pair.Value == before[index][pair.Key], "unrelated type and ability remain unchanged");
                    VerifyStats(existing[index], grid.StatEffects); inactive[index].gameObject.SetActive(true); VerifyStats(inactive[index], grid.StatEffects); inactive[index].gameObject.SetActive(false);
                }
            }
            for (var index = 0; index < names.Length; index++) VerifyStats(Install(grid, names[index], 20 + index, 26), grid.StatEffects);
            Require(storage.CurrentLoad == 20 && storage.AllocatedAmount == 10 && ReferenceEquals(identity, storage.Identity), "stock reservation and instance preserved");
            storage.StoreResource(Vector3Int.back, 10); Require(storage.CurrentLoad == 30 && storage.AllocatedAmount == 0, "old stock reservation commits");
            Require(!crossing.SupportsProgression && ReferenceEquals(pending[Vector3Int.right], transfer) && receiver.AllocatedAmount == 5 && receiver.Capacity == 150, "crossing keeps upstream reservation while receiver grows");
            crossing.CancelStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone);
            Require(receiver.AllocatedAmount == 0 && pending.Count == 0, "crossing reservation cancels after type purchases");
            Require(generator.Wallet.Current.Balance == 350 && generator.Wallet.Current.ReservedExpense == 0, "scene type purchases charged once");
            Require(generator.Wallet.LastNotificationException == null && tree.LastNotificationException == null && grid.StatEffects.LastNotificationException == null, "scene notifications succeed");
            Debug.Log($"Skill tree checks passed: {_checks} assertions; type definitions, transactions, notifications, all five types, existing/inactive/new cells, extraction, export, crafting and reservations.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static int ResourceId(ConveyorCellAdapter cell) => (int)typeof(ConveyorCellAdapter).GetProperty("ResourceId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cell);
    private static CraftingOperationApplication Operation(CrafterCellAdapter cell) => (CraftingOperationApplication)typeof(CrafterCellAdapter).GetField("_operation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cell);
    private static async UniTask Wait(Func<bool> condition) => await UniTask.WaitUntil(condition).Timeout(TimeSpan.FromSeconds(12));

    private static CellBaseAdapter Install(GridFieldDatabaseAdapter grid, string name, int x, int z, float rotation = 0, Action<CellBaseAdapter> configure = null)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Field/General/F_{name}Cell.prefab");
        var old = grid.GetCell(x, z);
        var cell = UnityEngine.Object.Instantiate(source, old.transform.position, Quaternion.Euler(0, rotation, 0), old.transform.parent).GetComponent<CellBaseAdapter>();
        configure?.Invoke(cell);
        grid.SaveCell(x, z, cell);
        if (old is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
        UnityEngine.Object.Destroy(old.gameObject);
        cell.InitializeSystem();
        return cell;
    }

    private static Dictionary<EquipmentStatDomain, int> Stats(CellBaseAdapter cell) => cell switch
    {
        ExtractorCellAdapter e => new() { [EquipmentStatDomain.ExtractionAmount] = e.ExtractionAmount, [EquipmentStatDomain.Capacity] = e.StorageCapacity },
        StorageCellAdapter stock => new() { [EquipmentStatDomain.Capacity] = stock.Capacity },
        CrafterCellAdapter c => new() { [EquipmentStatDomain.InputCapacity] = c.IngredientCapacity, [EquipmentStatDomain.OutputCapacity] = c.ExporterCapacity },
        ConveyorCellAdapter conveyor => new() { [EquipmentStatDomain.TransferAmount] = conveyor.TransferAmount },
        _ => throw new InvalidOperationException("Unexpected ability cell")
    };
    private static void VerifyStats(CellBaseAdapter cell, SharedStatEffectsApplication effects)
    {
        foreach (var pair in Stats(cell))
        {
            var field = pair.Key switch { EquipmentStatDomain.ExtractionAmount => "extractionAmount", EquipmentStatDomain.Capacity => cell is StorageCellAdapter ? "capacity" : "storageCapacity", EquipmentStatDomain.InputCapacity => "ingredientCapacity", EquipmentStatDomain.OutputCapacity => "exporterCapacity", _ => "transferAmount" };
            var owner = pair.Key == EquipmentStatDomain.TransferAmount ? typeof(ConveyorCellAdapter) : cell.GetType();
            var value = (int)owner.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cell);
            Require(pair.Value == EffectiveStatsRulesDomain.Calculate(value, effects.GetModifier(cell.ProgressionType, pair.Key)), "configured ability applies to existing/inactive/new cell");
        }
    }

    private static void Invalid(Action action)
    {
        try { action(); } catch (ArgumentException) { _checks++; return; }
        throw new InvalidOperationException("Expected invalid definition.");
    }
    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Skill tree check failed: " + message);
    }
}
