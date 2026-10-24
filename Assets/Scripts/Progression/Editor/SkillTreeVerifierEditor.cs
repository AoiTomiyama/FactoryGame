using System;
using System.Collections.Generic;
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
        var catalog = Catalog();
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
            Require(tree != null && generator.Wallet.Current.Balance == 100, "scene composition and initial funds");
            Require(generator.Wallet.TryReserveIncome(500, out var income) && income.TryCommit(), "fund all skills using scene wallet");
            var existing = new List<CellBaseAdapter>();
            var inactive = new List<CellBaseAdapter>();
            var prefabNames = new[] { "ExtractorWood", "ExtractorStone", "Conveyor", "ExportConveyor", "Storage", "Crafter" };
            for (var index = 0; index < prefabNames.Length; index++)
            {
                existing.Add(Install(grid, prefabNames[index], 20 + index, 20));
                var cell = Install(grid, prefabNames[index], 20 + index, 22);
                cell.gameObject.SetActive(false);
                inactive.Add(cell);
            }
            var storage = (StorageCellAdapter)existing[4];
            Require(storage.AllocateStorage(Vector3Int.back, 30, ResourceTypeDomain.Wood), "reserve existing capacity");
            storage.StoreResource(Vector3Int.back, 20);
            var identity = storage.Identity;
            // 交差は個別レベルを持つが、中継数量は上流の予約値を使う。
            for (var x = 27; x <= 30; x++)
            for (var z = 21; z <= 23; z++) Install(grid, "Empty", x, z);
            var crossing = (CrossingCellAdapter)Install(grid, "Crossing", 28, 22);
            var crossingTarget = (StorageCellAdapter)Install(grid, "Storage", 29, 22);
            crossingTarget.InitializeSystem();
            crossing.InitializeSystem();
            Require(crossing.SupportsProgression && crossing.TrySetEquipmentLevel(2), "crossing supports individual level");
            Require(crossing.AllocateStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone), "crossing fixes upstream quantity");
            var crossingPending = (System.Collections.IDictionary)typeof(CrossingCellAdapter)
                .GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(crossing);
            var crossingTransfer = crossingPending[Vector3Int.right];
            var crossingOperation = (ResourceTransferOperationApplication)crossingTransfer.GetType().GetProperty("Operation").GetValue(crossingTransfer);
            Require(tree.Purchase("basic-research") == SkillPurchaseResultApplication.Success, "scene root purchase");
            foreach (var branch in new[] { "extraction", "transfer", "storage" })
                Require(tree.Purchase(branch + "-1") == SkillPurchaseResultApplication.Success, "scene tier one");
            Require(crossing.EquipmentLevel == 2 && ReferenceEquals(crossingPending[Vector3Int.right], crossingTransfer) &&
                crossingOperation.Amount == 5 && crossingTarget.AllocatedAmount == 5 && crossingTarget.Capacity == 62,
                "tier one grows receiver capacity and preserves crossing level and upstream quantity");
            for (var index = 0; index < prefabNames.Length; index++)
            {
                VerifyStats(existing[index], 25);
                inactive[index].gameObject.SetActive(true);
                VerifyStats(inactive[index], 25);
                inactive[index].gameObject.SetActive(false);
                VerifyStats(Install(grid, prefabNames[index], 20 + index, 24), 25);
            }
            Require(existing[0].TrySetEquipmentLevel(3), "individual level composes with skills");
            foreach (var branch in new[] { "extraction", "transfer", "storage" })
                Require(tree.Purchase(branch + "-2") == SkillPurchaseResultApplication.Success, "scene tier two");
            Require(crossing.EquipmentLevel == 2 && ReferenceEquals(crossingPending[Vector3Int.right], crossingTransfer) &&
                crossingOperation.Amount == 5 && crossingTarget.AllocatedAmount == 5 && crossingTarget.Capacity == 75,
                "tier two preserves crossing level and upstream quantity");
            crossing.CancelStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone);
            Require(crossingTarget.AllocatedAmount == 0 && crossingPending.Count == 0, "crossing reservation cancels after shared effects");
            for (var index = 0; index < prefabNames.Length; index++)
            {
                VerifyStats(existing[index], 50);
                inactive[index].gameObject.SetActive(true);
                VerifyStats(inactive[index], 50);
                VerifyStats(Install(grid, prefabNames[index], 20 + index, 26), 50);
            }
            Require(storage.CurrentLoad == 20 && storage.AllocatedAmount == 10 && ReferenceEquals(storage.Identity, identity), "stock reservation identity preserved");
            storage.StoreResource(Vector3Int.back, 10);
            Require(storage.CurrentLoad == 30 && storage.AllocatedAmount == 0, "old reservation commits after purchase");
            Require(generator.Wallet.Current.Balance == 100 && generator.Wallet.Current.ReservedExpense == 0, "scene total charged once");
            Require(!grid.GetCell(50, 50).SupportsProgression && !grid.GetCell(50, 46).SupportsProgression && !Install(grid, "Empty", 28, 20).SupportsProgression,
                "protected cells have no progression");
            Debug.Log($"Skill tree checks passed: {_checks} assertions; definitions, transactions, callbacks, scene wallet, all ability types, I/II, existing/reenabled/new cells.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static CellBaseAdapter Install(GridFieldDatabaseAdapter grid, string name, int x, int z)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Field/General/F_{name}Cell.prefab");
        var old = grid.GetCell(x, z);
        var cell = UnityEngine.Object.Instantiate(source, old.transform.position, Quaternion.identity, old.transform.parent).GetComponent<CellBaseAdapter>();
        grid.SaveCell(x, z, cell);
        if (old is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
        UnityEngine.Object.Destroy(old.gameObject);
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
