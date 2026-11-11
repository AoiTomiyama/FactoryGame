using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SingularityVerifierEditor
{
    private const string SessionKey = "FactoryGame.SingularityVerification";
    private const string LayoutPath = "Assets/Scripts/ScriptableObject/SingularityStart.asset";
    private static int _checks;
    static SingularityVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyPlay().Forget();
    };

    public static void Run()
    {
        _checks = 0;
        CurrencyWalletVerifierEditor.Run();
        VerifyIntake();
        VerifyGeneration();
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        EditorUtility.audioMasterMute = true;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void VerifyIntake()
    {
        var prices = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>(LayoutPath).CreateCatalog();
        var wallet = new CurrencyWalletApplication(100);
        var intake = new ShippingIntakeApplication(wallet, prices, new[] { 0, 1, 2, 3 }) { Accepting = true };
        for (var i = 0; i < 4; i++)
            Require(intake.TryReserve(i, i + 1, i % 2 == 0 ? ResourceTypeDomain.Wood : ResourceTypeDomain.Stone), "four mixed inputs");
        Require(wallet.Current.ReservedIncome == 10 && wallet.Current.Balance == 100, "reserve without payment");
        Require(!intake.TryReserve(0, 1, ResourceTypeDomain.Stone), "same direction contention");
        intake.Commit(0, 2);
        intake.Cancel(1, 2, ResourceTypeDomain.Wood);
        Require(wallet.Current.ReservedIncome == 10, "mismatch leaves reservations");
        intake.Commit(0, 1);
        intake.Commit(0, 1);
        intake.Cancel(1, 2, ResourceTypeDomain.Stone);
        intake.Cancel(1, 2, ResourceTypeDomain.Stone);
        Require(wallet.Current.Balance == 101 && wallet.Current.ReservedIncome == 7, "commit and cancel once");
        intake.CancelAll();
        intake.CancelAll();
        Require(wallet.Current.ReservedIncome == 0, "release remaining directions once");
        foreach (var amount in new[] { int.MinValue, -1, 0 }) Require(!intake.TryReserve(0, amount, ResourceTypeDomain.Wood), "invalid quantities");
        foreach (var type in new[] { ResourceTypeDomain.None, ResourceTypeDomain.Gold, (ResourceTypeDomain)999 })
            Require(!intake.TryReserve(0, 1, type), "undefined sale");
        Require(!intake.TryReserve(-1, 1, ResourceTypeDomain.Wood) && !intake.TryReserve(4, 1, ResourceTypeDomain.Wood), "invalid directions");
        wallet = new CurrencyWalletApplication(long.MaxValue - 3);
        intake = new ShippingIntakeApplication(wallet, prices, new[] { 0, 1, 2, 3 }) { Accepting = true };
        Require(wallet.TryReserveExpense(10, out var expense), "expense reservation");
        Require(intake.TryReserve(0, 2, ResourceTypeDomain.Wood) && !intake.TryReserve(1, 2, ResourceTypeDomain.Stone), "shared income limit");
        expense.TryCancel();
        Require(intake.TryReserve(1, 1, ResourceTypeDomain.Stone), "exact remaining room");
        intake.Commit(1, 1); intake.Commit(0, 2);
        Require(wallet.Current.Balance == long.MaxValue && wallet.Current.ReservedIncome == 0, "exact cap after expense cancel");
        wallet = new CurrencyWalletApplication(0);
        intake = new ShippingIntakeApplication(wallet, prices, new[] { 0, 1, 2, 3 }) { Accepting = true };
        var callbacks = 0;
        wallet.Changed += snapshot =>
        {
            if (++callbacks != 1) return;
            Require(!intake.TryReserve(0, 1, ResourceTypeDomain.Wood), "reentrant same input blocked");
            intake.Accepting = false;
            intake.CancelAll();
        };
        Require(!intake.TryReserve(0, 1, ResourceTypeDomain.Wood) && wallet.Current.ReservedIncome == 0, "disable inside reservation notification");
        intake.Accepting = true;
        Require(intake.TryReserve(0, int.MaxValue, ResourceTypeDomain.Wood), "int maximum reserve");
        intake.Commit(0, int.MaxValue);
        Require(wallet.Current.Balance == int.MaxValue, "int maximum sale");
    }

    private static void VerifyGeneration()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var source = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
        var generator = UnityEngine.Object.Instantiate(source);
        var anchor = new GameObject("GenerationVerification").transform;
        try
        {
            Set(generator, "gridSize", 14);
            var layout = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>(LayoutPath);
            for (var seed = 0; seed < 3; seed++)
            {
                Set(generator, "seedValue", seed);
                generator.GenerateGrid(anchor);
                var singularities = anchor.GetComponentsInChildren<SingularityCellAdapter>();
                Require(singularities.Length == 1 && singularities[0].transform.position == new Vector3(7, 0, 7), "regeneration central singleton");
                var cells = anchor.GetComponentsInChildren<CellBaseAdapter>();
                var woodPoint = layout.StarterCell(14, 4);
                Require(cells.OfType<ResourceCellAdapter>().Any(c => c.ResourceTypeDomain == ResourceTypeDomain.Wood &&
                    c.transform.position == new Vector3(woodPoint.x, 0, woodPoint.z)), "initial wood outside footprint");
                for (var distance = 1; distance <= 3; distance++)
                {
                    var point = layout.StarterCell(14, distance);
                    Require(cells.OfType<EmptyCellAdapter>().Any(c => c.transform.position == new Vector3(point.x, 0, point.z)), "starter route outside footprint");
                }
                Require(singularities[0].Footprint.Area == 25 && singularities[0].Ports.Count == 20 && cells.Length == 172,
                    "generated field has one 25-tile owner and no interior terrain");
            }
            var original = anchor.GetComponentInChildren<SingularityCellAdapter>();
            UnityEngine.Object.DestroyImmediate(original.gameObject);
            var displaced = new System.Collections.Generic.List<ResourceCellAdapter>();
            foreach (var point in layout.SingularityPrefab.Footprint.Coordinates(7, 7, 0))
            {
                var resource = ((GameObject)PrefabUtility.InstantiatePrefab(layout.WoodPrefab.gameObject, anchor)).GetComponent<ResourceCellAdapter>();
                resource.transform.position = new Vector3(point.x, 0, point.z);
                displaced.Add(resource);
            }
            generator.PrepareInitialLayout(anchor);
            Require(displaced.All(c => c == null) && anchor.GetComponentsInChildren<SingularityCellAdapter>().Length == 1,
                "forced initial placement retires all original resources before registration");
            var rejected = false;
            try { layout.Validate(11); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "small grid rejected");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(anchor.gameObject);
            UnityEngine.Object.DestroyImmediate(generator.gameObject);
        }
        Debug.Log($"Singularity EDIT checks passed: {_checks} assertions; direction contention, quantities, one-shot, upper bound, reentrancy, regeneration.");
    }

    private sealed class Presentation : IResourceTransferPresentationApplication
    {
        public readonly UniTaskCompletionSource<ResourceAnimationResultApplication> Completion = new();
        public int Releases;
        public UniTask<ResourceAnimationResultApplication> Transfer(CancellationToken token, Vector3 from, Vector3 to, int id) => Completion.Task;
        public void SetPosition(int id, Vector3 position) { }
        public bool ReleaseVisual(int id) { Releases++; return Releases == 1; }
    }

    private static async UniTaskVoid VerifyPlay()
    {
        try
        {
            _checks = 0;
            AudioListener.volume = 0;
            await UniTask.Yield();
            var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
            var wallet = generator.Wallet;
            var grid = GridFieldDatabaseAdapter.Instance;
            var singularity = grid.GetCell(50, 50) as SingularityCellAdapter;
            Require(singularity != null && wallet.Current.Balance == 100, "MainScene central singularity and shared wallet");
            Require(grid.Owners.OfType<SingularityCellAdapter>().Count() == 1 && singularity.OccupiedCoordinates.All(p => grid.GetCell(p.x, p.z) == singularity),
                "all 25 tiles return one owner");
            for (var port = 0; port < 20; port++) Require(singularity.AllocateStorage(port, 1, ResourceTypeDomain.Wood), "twenty independent inputs");
            Require(wallet.Current.ReservedIncome == 0, "absorption never reserves income");
            for (var port = 0; port < 20; port++) singularity.CancelStorage(port, 1, ResourceTypeDomain.Wood);
            Require(wallet.Current.ReservedIncome == 0 && wallet.Current.Balance == 100, "twenty cancellations preserve balance");
            var identity = singularity.Identity;
            var directions = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back };
            for (var i = 0; i < 4; i++) Require(singularity.AllocateStorage(i, i + 1,
                i % 2 == 0 ? ResourceTypeDomain.Wood : ResourceTypeDomain.Stone), "adapter mixed reservations");
            Require(!singularity.AllocateStorage(-1, 1, ResourceTypeDomain.Wood), "noncardinal input rejected");
            singularity.gameObject.SetActive(false);
            Require(wallet.Current.ReservedIncome == 0 && wallet.Current.Balance == 100 && singularity.IsAlive, "disable releases without payment and preserves lifetime");
            singularity.gameObject.SetActive(true);
            Require(ReferenceEquals(identity, singularity.Identity), "reenable stable identity");
            var shipping = grid.Owners.OfType<ShippingCellAdapter>().Single();
            await VerifyCoordinator(shipping, wallet);
            var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
            Place(placer, grid.GetCell(50, 50), CellTypeDomain.Empty, 0);
            Require(grid.GetCell(50, 50) == singularity, "singularity deletion protected");
            Place(placer, grid.GetCell(50, 43), CellTypeDomain.Empty, 0);
            Require(grid.GetCell(50, 43) is ResourceCellAdapter, "resource deletion protected");
            var empty = grid.Owners.OfType<EmptyCellAdapter>().First(c => c.XIndex > 5 && c.XIndex < 40 && c.ZIndex > 5 && c.ZIndex < 40);
            var x = empty.XIndex; var z = empty.ZIndex; var purchaseBalance = wallet.Current.Balance;
            Place(placer, empty, CellTypeDomain.Shipping, 90);
            var added = grid.GetCell(x, z) as ShippingCellAdapter;
            Require(added != null && wallet.Current.Balance == purchaseBalance - 30, "additional shipping purchased through existing placement transaction");
            var saleBalance = wallet.Current.Balance;
            Require(added.AllocateStorage(0, 2, ResourceTypeDomain.Wood) && shipping.AllocateStorage(0, 3, ResourceTypeDomain.Stone), "two shipping cells share income room");
            Require(singularity.AllocateStorage(0, 4, ResourceTypeDomain.Iron), "singularity absorbs unsellable resource independently");
            singularity.StoreResource(0, 4);
            Require(wallet.Current.ReservedIncome == 5 && wallet.Current.Balance == saleBalance, "absorption never changes balance or shipping income reservations");
            added.gameObject.SetActive(false);
            Require(wallet.Current.ReservedIncome == 3, "shipping disable cancels only its own reservation");
            shipping.StoreResource(0, 3); shipping.StoreResource(0, 3);
            Require(wallet.Current.Balance == saleBalance + 3 && wallet.Current.ReservedIncome == 0, "other shipping commits exactly once");
            added.gameObject.SetActive(true);
            Require(added.AllocateStorage(3, 2, ResourceTypeDomain.Plank), "rotated shipping resumes after reenable");
            added.StoreResource(3, 2);
            Require(wallet.Current.Balance == saleBalance + 9, "new shipping uses existing processed-resource prices");
            Require(added.AllocateStorage(0, 1, ResourceTypeDomain.Wood), "shipping removal has pending income");
            var removalBalance = wallet.Current.Balance;
            Place(placer, added, CellTypeDomain.Empty, 0);
            Require(grid.GetCell(x, z) is EmptyCellAdapter && wallet.Current.ReservedIncome == 0 && wallet.Current.Balance == removalBalance,
                "shipping removal cancels pending sale and has no refund");
            var before = wallet.Current.Balance - 30;
            Place(placer, grid.GetCell(50, 44), CellTypeDomain.ExtractorWood, 180);
            Place(placer, grid.GetCell(50, 45), CellTypeDomain.ExportConveyor, 0);
            Place(placer, grid.GetCell(50, 46), CellTypeDomain.Conveyor, 0);
            Require(grid.GetCell(50, 44) is ExtractorCellAdapter && grid.GetCell(50, 45) is ExportConveyorCellAdapter &&
                grid.GetCell(50, 46) is ConveyorCellAdapter, "actual pointer route placement");
            await UniTask.WaitUntil(() => wallet.Current.Balance > before).Timeout(TimeSpan.FromSeconds(30));
            Require(wallet.Current.Balance > before, "first actual extraction to sale");
            // 演出中の実コンベアを切断し、送り元IDを維持して再接続できることを確認する。
            var conveyor = grid.GetCell(50, 46) as ConveyorCellAdapter;
            await UniTask.WaitUntil(() => wallet.Current.ReservedIncome > 0).Timeout(TimeSpan.FromSeconds(20));
            shipping.enabled = false;
            var stoppedBalance = wallet.Current.Balance;
            Require(wallet.Current.ReservedIncome == 0, "connected disable cancels reservation synchronously");
            await UniTask.Delay(300);
            Require(wallet.Current.Balance == stoppedBalance, "disconnected presentation cannot pay");
            shipping.enabled = true;
            await UniTask.WaitUntil(() => wallet.Current.Balance > stoppedBalance).Timeout(TimeSpan.FromSeconds(20));
            Require(wallet.Current.Balance > stoppedBalance, "reconnect resumes owned resource");
            await UniTask.WaitUntil(() => wallet.Current.ReservedIncome > 0).Timeout(TimeSpan.FromSeconds(20));
            var heldId = (int)typeof(ConveyorCellAdapter).GetProperty("ResourceId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(conveyor);
            var pool = ResourceItemObjectPoolAdapter.Instance;
            var beforeRemoval = wallet.Current.Balance;
            Place(placer, conveyor, CellTypeDomain.Empty, 0);
            await UniTask.Delay(300);
            Require(wallet.Current.ReservedIncome == 0 && wallet.Current.Balance == beforeRemoval &&
                !pool.Resources.TryGet(heldId, out _) && !pool.ReleaseVisual(heldId), "source removal releases ID without payment");
            Debug.Log($"Singularity PLAY checks passed: {_checks} assertions; MainScene pointer route, extraction and sale, protected cells, coordinator outcomes, lifetime, reconnect and ID release.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static async UniTask VerifyCoordinator(ShippingCellAdapter singularity, CurrencyWalletApplication wallet)
    {
        for (var scenario = 0; scenario < 6; scenario++)
        {
            var clone = UnityEngine.Object.Instantiate(singularity);
            var localWallet = new CurrencyWalletApplication(0);
            clone.Configure(localWallet, UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>().Prices);
            clone.XIndex = 0; clone.ZIndex = 0;
            clone.InitializeSystem();
            var resources = new ResourceTransitStoreApplication();
            var id = resources.Create(ResourceTypeDomain.Wood, 2);
            var presentation = new Presentation();
            var operation = new ResourceTransferOperationApplication(singularity, clone, id, ResourceTypeDomain.Wood, 2);
            var coordinator = new ResourceTransferCoordinatorApplication(operation);
            if (scenario == 5) localWallet.Changed += snapshot =>
            {
                if (snapshot.Balance == 0) return;
                coordinator.Cancel();
                UnityEngine.Object.DestroyImmediate(clone.gameObject);
            };
            using var cts = new CancellationTokenSource();
            var transfer = coordinator.RunAsync(() => 0, Vector3.zero, Vector3.right, () => clone,
                endpoint => clone != null && clone.isActiveAndEnabled, resources, presentation, cts.Token);
            await UniTask.WaitUntil(() => localWallet.Current.ReservedIncome == 2).Timeout(TimeSpan.FromSeconds(3));
            Require(localWallet.Current.ReservedIncome == 2, "coordinator fixes sale before presentation");
            if (scenario == 2) { clone.gameObject.SetActive(false); coordinator.WaitForNewTarget(); }
            if (scenario == 3) UnityEngine.Object.DestroyImmediate(clone.gameObject);
            if (scenario == 4) cts.Cancel();
            presentation.Completion.TrySetResult(scenario == 1 ? ResourceAnimationResultApplication.MissingVisual : ResourceAnimationResultApplication.Completed);
            try { await transfer; } catch (OperationCanceledException) { Require(scenario >= 2, "only interrupted transfers cancel"); }
            if (scenario == 0 || scenario == 5)
                Require(localWallet.Current.Balance == 2 && localWallet.Current.ReservedIncome == 0 && !resources.Remove(id) &&
                    presentation.Releases == 1 && !operation.TryComplete(), "success pays and releases ID once");
            else
                Require(localWallet.Current.Balance == 0 && localWallet.Current.ReservedIncome == 0 && resources.TryGet(id, out _) &&
                    presentation.Releases == 0, "failure retains source ID and releases income");
            coordinator.Cancel();
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone.gameObject);
            Require(localWallet.Current.ReservedIncome == 0, "lifetime cleanup idempotent");
        }
    }

    private static void Place(CellPlacerAdapter placer, CellBaseAdapter cell, CellTypeDomain type, float rotation)
    {
        placer.transform.rotation = Quaternion.Euler(0, rotation, 0);
        placer.SetSelectedCellType(type);
        placer.SelectGrid(cell.gameObject);
        placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
    }
    private static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Singularity check failed: " + message);
    }
}
