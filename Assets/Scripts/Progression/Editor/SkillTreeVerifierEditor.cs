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

    public static void RunRegression()
    {
        EditorUtility.audioMasterMute = true;
        Require(EditorUtility.audioMasterMute, "editor muted before regression play");
        EffectiveStatsVerifierEditor.Run();
    }

    private static SkillTreeCatalogDomain Catalog() => AssetDatabase
        .LoadAssetAtPath<SkillTreeDefinition>("Assets/Scripts/ScriptableObject/SkillTree.asset").CreateCatalog();
    private static SkillNodeDomain Node(string uid, string prerequisite = null, long price = 50,
        StatBranchDomain? branch = null, int bonus = 0) => new(uid, uid, price, prerequisite, branch, bonus);

    private static void VerifyRules()
    {
        VerifyTypeRules(Catalog());
        // 旧共有率の基盤は能力計算の回帰用に保持し、採用アセットは種類別レベルだけを使う。
        var catalog = new SkillTreeCatalogDomain(new[] {
            Node("basic-research"), Node("extraction-1", "basic-research", branch: StatBranchDomain.Extraction, bonus: 25),
            Node("extraction-2", "extraction-1", 100, StatBranchDomain.Extraction, 25),
            Node("transfer-1", "basic-research", branch: StatBranchDomain.Transfer, bonus: 25),
            Node("transfer-2", "transfer-1", 100, StatBranchDomain.Transfer, 25),
            Node("storage-1", "basic-research", branch: StatBranchDomain.Storage, bonus: 25),
            Node("storage-2", "storage-1", 100, StatBranchDomain.Storage, 25) });
        Require(catalog.Nodes.Count == 7, "seven nodes");
        foreach (var node in catalog.Nodes)
        {
            Require(node.Price == (node.Uid.EndsWith("-2") ? 100 : 50), "adopted price " + node.Uid);
            Require(node.BonusPercent == (node.Branch.HasValue ? 25 : 0), "adopted effect " + node.Uid);
            Require(node.Branch == (node.Uid.StartsWith("extraction-") ? StatBranchDomain.Extraction :
                node.Uid.StartsWith("transfer-") ? StatBranchDomain.Transfer :
                node.Uid.StartsWith("storage-") ? StatBranchDomain.Storage : (StatBranchDomain?)null), "adopted branch");
            Require(node.Prerequisite == (node.Uid == "basic-research" ? null :
                node.Uid.EndsWith("-2") ? node.Uid.Replace("-2", "-1") : "basic-research"), "adopted prerequisite");
        }
        Invalid(() => new SkillTreeCatalogDomain(null));
        Invalid(() => new SkillTreeCatalogDomain(Array.Empty<SkillNodeDomain>()));
        Invalid(() => new SkillTreeCatalogDomain(new SkillNodeDomain[] { null }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { new SkillNodeDomain("a", "", 50, null, null, 0) }));
        foreach (var price in new long[] { 0, -1 }) Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", price: price) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a"), Node("a") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", "missing") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", "a") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", "b"), Node("b", "c"), Node("c", "a") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", branch: (StatBranchDomain)3, bonus: 25) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", branch: StatBranchDomain.Storage, bonus: 0) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", bonus: 25) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", branch: StatBranchDomain.Storage, bonus: int.MaxValue) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { Node("a", branch: StatBranchDomain.Storage, bonus: 25), Node("b", branch: StatBranchDomain.Storage, bonus: 26) }));
        var input = new[] { Node("a", price: long.MaxValue) };
        var snapshot = new SkillTreeCatalogDomain(input);
        input[0] = Node("changed");
        Require(snapshot.TryGet("a", out _) && !snapshot.TryGet("changed", out _), "definition snapshot");
        var rich = new CurrencyWalletApplication(long.MaxValue);
        Require(new SkillTreeApplication(snapshot, rich, new SharedStatEffectsApplication()).Purchase("a") == SkillPurchaseResultApplication.Success && rich.Current.Balance == 0,
            "long maximum price without overflow");

        var wallet = new CurrencyWalletApplication(500);
        var effects = new SharedStatEffectsApplication();
        var tree = new SkillTreeApplication(catalog, wallet, effects);
        Require(tree.Purchase(null) == SkillPurchaseResultApplication.UnknownNode && tree.Purchase("missing") == SkillPurchaseResultApplication.UnknownNode, "unknown nodes");
        Require(tree.Purchase("extraction-1") == SkillPurchaseResultApplication.PrerequisiteMissing && tree.Purchase("storage-2") == SkillPurchaseResultApplication.PrerequisiteMissing && wallet.Current.Balance == 500,
            "prerequisites reject without expense");
        var notifications = 0;
        var effectsNotifications = 0;
        var reentries = 0;
        var callbackStateValid = true;
        wallet.Changed += state =>
        {
            Require(tree.Purchase("basic-research") == SkillPurchaseResultApplication.Busy && tree.Purchase("transfer-1") == SkillPurchaseResultApplication.Busy, "wallet callback reentry");
            reentries++;
            if (state.ReservedExpense == 0 && tree.IsOwned("storage-1"))
                Require(effects.GetBonus(StatBranchDomain.Storage) >= 25, "commit observer sees acquired effect");
        };
        tree.Changed += () => throw new InvalidOperationException("expected subscriber failure");
        tree.Changed += () =>
        {
            notifications++;
            callbackStateValid &= tree.Purchase("storage-2") == SkillPurchaseResultApplication.Busy &&
                tree.CanPurchase("basic-research") == SkillPurchaseResultApplication.AlreadyOwned;
            if (notifications == 1) callbackStateValid &= tree.CanPurchase("storage-1") == SkillPurchaseResultApplication.Success;
        };
        effects.Changed += () => throw new InvalidOperationException("expected effect subscriber failure");
        effects.Changed += () => { effectsNotifications++; callbackStateValid &= wallet.Current.ReservedExpense == 0 && tree.Purchase("transfer-2") == SkillPurchaseResultApplication.Busy; };
        foreach (var node in catalog.Nodes)
        {
            Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.Success && tree.IsOwned(node.Uid), "purchase " + node.Uid);
            var balance = wallet.Current.Balance;
            Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.AlreadyOwned && wallet.Current.Balance == balance, "duplicate " + node.Uid);
        }
        Require(wallet.Current.Balance == 0 && wallet.Current.ReservedExpense == 0 && notifications == 7 && effectsNotifications == 6 && reentries == 14,
            "exact total, one notification per purchase, all subscribers reached");
        Require(tree.LastNotificationException != null && effects.LastNotificationException != null, "subscriber failures recorded");
        Require(wallet.LastNotificationException == null && callbackStateValid, "callback assertions survive exception isolation");
        foreach (StatBranchDomain branch in Enum.GetValues(typeof(StatBranchDomain))) Require(effects.GetBonus(branch) == 50, "branch capped at fifty");
        var poor = new CurrencyWalletApplication(49);
        var poorTree = new SkillTreeApplication(catalog, poor, new SharedStatEffectsApplication());
        Require(poorTree.Purchase("basic-research") == SkillPurchaseResultApplication.InsufficientFunds && poor.Current.Balance == 49 && !poorTree.IsOwned("basic-research"), "insufficient funds");
        var reserved = new CurrencyWalletApplication(100);
        reserved.TryReserveExpense(51, out var otherExpense);
        Require(new SkillTreeApplication(catalog, reserved, new SharedStatEffectsApplication()).Purchase("basic-research") == SkillPurchaseResultApplication.InsufficientFunds, "other expense respected");
        otherExpense.TryCancel();
        var changedWallet = new CurrencyWalletApplication(100);
        var changedEffects = new SharedStatEffectsApplication();
        var changedTree = new SkillTreeApplication(catalog, changedWallet, changedEffects);
        changedWallet.Changed += state => { if (state.ReservedExpense > 0) changedEffects.TrySetBonus(StatBranchDomain.Storage, 25); };
        Require(changedTree.Purchase("basic-research") == SkillPurchaseResultApplication.EffectsChanged && changedWallet.Current.Balance == 100 &&
            changedWallet.Current.ReservedExpense == 0 && !changedTree.IsOwned("basic-research"), "revalidation cancels reserved expense");
        var fresh = new SkillTreeApplication(catalog, new CurrencyWalletApplication(100), new SharedStatEffectsApplication());
        Require(!fresh.IsOwned("basic-research") && fresh.CanPurchase("basic-research") == SkillPurchaseResultApplication.Success, "new session resets acquisition");
    }

    private static void VerifyTypeRules(SkillTreeCatalogDomain catalog)
    {
        Require(catalog.Nodes.Count == 11, "root and five type chains");
        foreach (var node in catalog.Nodes)
        {
            Require(!node.Branch.HasValue && node.BonusPercent == 0, "adopted tree has no shared percentage branches");
            Require(node.Price == (node.TargetLevel == 3 ? 100 : 50), "type tier price");
            if (node.Uid != "basic-research") Require(node.CellType.HasValue && SkillTreeCatalogDomain.SupportsType(node.CellType.Value), "useful cell type");
        }
        SkillNodeDomain TypeNode(string uid, CellTypeDomain type, int level, string previous = null) => new(uid, uid, 50, previous, null, 0, type, level);
        Invalid(() => new SkillTreeCatalogDomain(new[] { TypeNode("a", CellTypeDomain.Crossing, 2) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { TypeNode("a", CellTypeDomain.Storage, 1) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { TypeNode("a", CellTypeDomain.Storage, 4) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { TypeNode("a", CellTypeDomain.Storage, 2), TypeNode("b", CellTypeDomain.Storage, 2) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { TypeNode("a", CellTypeDomain.Storage, 3) }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { TypeNode("a", CellTypeDomain.ExportConveyor, 2), TypeNode("b", CellTypeDomain.Storage, 3, "a") }));
        Invalid(() => new SkillTreeCatalogDomain(new[] { new SkillNodeDomain("a", "a", 50, null, StatBranchDomain.Storage, 25, CellTypeDomain.Storage, 2) }));
        var wallet = new CurrencyWalletApplication(1000);
        var effects = new SharedStatEffectsApplication();
        var tree = new SkillTreeApplication(catalog, wallet, effects);
        var changed = 0; var commitChecks = 0;
        tree.Changed += () => { changed++; Require(tree.Purchase("wood-1") == SkillPurchaseResultApplication.Busy, "type change reentry guarded"); };
        wallet.Changed += state => {
            if (state.ReservedExpense != 0) return;
            foreach (var node in catalog.Nodes.Where(node => node.CellType.HasValue && tree.IsOwned(node.Uid)))
                Require(effects.GetTypeLevel(node.CellType.Value) >= node.TargetLevel, "commit sees type level before notification");
            commitChecks++;
        };
        Require(tree.Purchase("wood-2") == SkillPurchaseResultApplication.PrerequisiteMissing, "type tier two is gated");
        foreach (var node in catalog.Nodes)
        {
            Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.Success, "buy type skill " + node.Uid);
            var paid = wallet.Current.Balance;
            Require(tree.Purchase(node.Uid) == SkillPurchaseResultApplication.AlreadyOwned && wallet.Current.Balance == paid, "type skill pays once");
        }
        Require(changed == 11 && commitChecks == 11 && wallet.Current.Balance == 200 && wallet.Current.ReservedExpense == 0, "type tree total and notifications");
        foreach (var type in catalog.Nodes.Where(node => node.CellType.HasValue).Select(node => node.CellType.Value).Distinct())
            Require(effects.GetTypeLevel(type) == 3, "shared type max level");
        Require(effects.GetTypeLevel(CellTypeDomain.Crossing) == 1 && effects.GetTypeLevel(CellTypeDomain.Conveyor) == 1 && effects.GetBonus(StatBranchDomain.Storage) == 0, "unrelated type and percentage untouched");
        var alteredWallet = new CurrencyWalletApplication(100);
        var alteredEffects = new SharedStatEffectsApplication();
        var altered = new SkillTreeApplication(catalog, alteredWallet, alteredEffects);
        alteredWallet.Changed += state => { if (state.ReservedExpense > 0) alteredEffects.TrySetTypeLevel(CellTypeDomain.Storage, 2); };
        Require(altered.Purchase("basic-research") == SkillPurchaseResultApplication.EffectsChanged && alteredWallet.Current.Balance == 100 && alteredWallet.Current.ReservedExpense == 0, "type effects rechecked after reservation");
        Require(new SharedStatEffectsApplication().GetTypeLevel(CellTypeDomain.Storage) == 1, "new session resets type levels");
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
            generator.Wallet.TryReserveIncome(1000, out var income); income.TryCommit();
            var names = new[] { "ExtractorWood", "ExtractorStone", "Storage", "Crafter", "ExportConveyor" };
            var keys = new[] { "wood", "stone", "storage", "crafter", "export" };
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
            Require(tree.Purchase("wood-1") == SkillPurchaseResultApplication.Success, "shared purchase during extraction");
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
            Require(tree.Purchase("export-1") == SkillPurchaseResultApplication.Success && ResourceId(exporter) == id && pool.TakeResourceDataById(id).amount == 5, "type purchase preserves in-flight export identity and quantity");
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
            Require(tree.Purchase("crafter-1") == SkillPurchaseResultApplication.Success && ReferenceEquals(operation, Operation(activeCrafter)) && activeCrafter.GetInput(DirectionsDomain.Left).Allocated == 2, "shared purchase preserves active crafting and reservation");
            await Wait(() => activeCrafter.ExportStorageAmount > 0); activeCrafter.enabled = false;
            Require(activeCrafter.ExportStorageAmount == 2 && operation.CurrentStage == CraftingOperationApplication.Stage.Completed, "active recipe remains unchanged");
            activeCrafter.StoreResource(Vector3Int.left, 2);
            Require(activeCrafter.GetInput(DirectionsDomain.Left).Amount == 2, "old crafting reservation commits");

            for (var tier = 1; tier <= 2; tier++)
            for (var index = 0; index < names.Length; index++)
            {
                var node = keys[index] + "-" + tier;
                var otherLevels = existing.Select(cell => cell.EquipmentLevel).ToArray();
                if (!tree.IsOwned(node)) Require(tree.Purchase(node) == SkillPurchaseResultApplication.Success, "scene type tier " + node);
                for (var other = 0; other < existing.Count; other++) if (other != index)
                    Require(existing[other].EquipmentLevel == otherLevels[other], "type purchase leaves other types unchanged");
                Require(existing[index].EquipmentLevel == tier + 1 && inactive[index].EquipmentLevel == tier + 1, "existing and inactive cells share type level");
                VerifyStats(existing[index], 0); inactive[index].gameObject.SetActive(true); VerifyStats(inactive[index], 0); inactive[index].gameObject.SetActive(false);
                var fresh = Install(grid, names[index], 20 + index, tier == 1 ? 24 : 26);
                Require(fresh.EquipmentLevel == tier + 1, "new cell inherits acquired type level"); VerifyStats(fresh, 0);
            }
            Require(storage.CurrentLoad == 20 && storage.AllocatedAmount == 10 && ReferenceEquals(identity, storage.Identity), "stock reservation and instance preserved");
            storage.StoreResource(Vector3Int.back, 10); Require(storage.CurrentLoad == 30 && storage.AllocatedAmount == 0, "old stock reservation commits");
            Require(crossing.EquipmentLevel == 1 && ReferenceEquals(pending[Vector3Int.right], transfer) && receiver.AllocatedAmount == 5 && receiver.Capacity == 150, "crossing keeps upstream reservation while receiver grows");
            crossing.CancelStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone);
            Require(receiver.AllocatedAmount == 0 && pending.Count == 0, "crossing reservation cancels after type purchases");
            Require(generator.Wallet.Current.Balance == 300 && generator.Wallet.Current.ReservedExpense == 0, "scene type purchases charged once");
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

    private static void VerifyStats(CellBaseAdapter cell, int bonus)
    {
        int Expected(string field) => EffectiveStatsRulesDomain.Calculate((int)cell.GetType()
            .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cell), cell.EquipmentLevel, bonus);
        switch (cell)
        {
            case ExtractorCellAdapter extractor:
                Require(extractor.ExtractionAmount == Expected("extractionAmount") && extractor.StorageCapacity == Expected("storageCapacity"), "extractor both abilities"); break;
            case ConveyorCellAdapter conveyor:
                var basis = (int)typeof(ConveyorCellAdapter).GetField("transferAmount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cell);
                Require(conveyor.TransferAmount == EffectiveStatsRulesDomain.Calculate(basis, cell.EquipmentLevel, bonus), "conveyor ability"); break;
            case StorageCellAdapter storage:
                Require(storage.Capacity == Expected("capacity"), "storage ability"); break;
            case CrafterCellAdapter crafter:
                Require(crafter.IngredientCapacity == Expected("ingredientCapacity") && crafter.ExporterCapacity == Expected("exporterCapacity"), "crafter both capacities"); break;
            default: throw new InvalidOperationException("Unexpected ability cell.");
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
