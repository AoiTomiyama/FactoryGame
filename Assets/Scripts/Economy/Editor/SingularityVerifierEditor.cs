using System;
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
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void VerifyIntake()
    {
        var prices = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>(LayoutPath).CreateCatalog();
        var wallet = new CurrencyWalletApplication(100);
        var intake = new SingularityIntakeApplication(wallet, prices) { Accepting = true };
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
        intake = new SingularityIntakeApplication(wallet, prices) { Accepting = true };
        Require(wallet.TryReserveExpense(10, out var expense), "expense reservation");
        Require(intake.TryReserve(0, 2, ResourceTypeDomain.Wood) && !intake.TryReserve(1, 2, ResourceTypeDomain.Stone), "shared income limit");
        expense.TryCancel();
        Require(intake.TryReserve(1, 1, ResourceTypeDomain.Stone), "exact remaining room");
        intake.Commit(1, 1); intake.Commit(0, 2);
        Require(wallet.Current.Balance == long.MaxValue && wallet.Current.ReservedIncome == 0, "exact cap after expense cancel");
        wallet = new CurrencyWalletApplication(0);
        intake = new SingularityIntakeApplication(wallet, prices) { Accepting = true };
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
            Set(generator, "gridSize", 8);
            var layout = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>(LayoutPath);
            for (var seed = 0; seed < 3; seed++)
            {
                Set(generator, "seedValue", seed);
                generator.GenerateGrid(anchor);
                var singularities = anchor.GetComponentsInChildren<SingularityCellAdapter>();
                Require(singularities.Length == 1 && singularities[0].transform == anchor.GetChild(4).GetChild(4), "regeneration central singleton");
                Require(anchor.GetChild(4).GetChild(0).GetComponent<ResourceCellAdapter>().ResourceTypeDomain == ResourceTypeDomain.Wood, "initial wood");
                for (var z = 1; z < 4; z++) Require(anchor.GetChild(4).GetChild(z).GetComponent<EmptyCellAdapter>() != null, "initial route empty");
            }
            var rejected = false;
            try { layout.Validate(7); } catch (InvalidOperationException) { rejected = true; }
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
            await UniTask.Yield();
            var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
            var wallet = generator.Wallet;
            var grid = GridFieldDatabaseAdapter.Instance;
            var singularity = grid.GetCell(50, 50) as SingularityCellAdapter;
            Require(singularity != null && wallet.Current.Balance == 100, "MainScene central singularity and shared wallet");
            var identity = singularity.Identity;
            var directions = new[] { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back };
            for (var i = 0; i < 4; i++) Require(singularity.AllocateStorage(directions[i], i + 1,
                i % 2 == 0 ? ResourceTypeDomain.Wood : ResourceTypeDomain.Stone), "adapter mixed reservations");
            Require(!singularity.AllocateStorage(Vector3Int.up, 1, ResourceTypeDomain.Wood), "noncardinal input rejected");
            singularity.gameObject.SetActive(false);
            Require(wallet.Current.ReservedIncome == 0 && wallet.Current.Balance == 100 && singularity.IsAlive, "disable releases without payment and preserves lifetime");
            singularity.gameObject.SetActive(true);
            Require(ReferenceEquals(identity, singularity.Identity), "reenable stable identity");
            await VerifyCoordinator(singularity, wallet);
            var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
            Place(placer, grid.GetCell(50, 50), CellTypeDomain.Empty, 0);
            Require(grid.GetCell(50, 50) == singularity, "singularity deletion protected");
            Place(placer, grid.GetCell(50, 46), CellTypeDomain.Empty, 0);
            Require(grid.GetCell(50, 46) is ResourceCellAdapter, "resource deletion protected");
            var before = wallet.Current.Balance;
            Place(placer, grid.GetCell(50, 47), CellTypeDomain.ExtractorWood, 180);
            Place(placer, grid.GetCell(50, 48), CellTypeDomain.ExportConveyor, 0);
            Place(placer, grid.GetCell(50, 49), CellTypeDomain.Conveyor, 0);
            Require(grid.GetCell(50, 47) is ExtractorCellAdapter && grid.GetCell(50, 48) is ExportConveyorCellAdapter &&
                grid.GetCell(50, 49) is ConveyorCellAdapter, "actual pointer route placement");
            await UniTask.WaitUntil(() => wallet.Current.Balance > before).Timeout(TimeSpan.FromSeconds(30));
            Require(wallet.Current.Balance > before, "first actual extraction to sale");
            // 演出中の実コンベアを切断し、送り元IDを維持して再接続できることを確認する。
            var conveyor = grid.GetCell(50, 49) as ConveyorCellAdapter;
            await UniTask.WaitUntil(() => wallet.Current.ReservedIncome > 0).Timeout(TimeSpan.FromSeconds(20));
            singularity.enabled = false;
            var stoppedBalance = wallet.Current.Balance;
            Require(wallet.Current.ReservedIncome == 0, "connected disable cancels reservation synchronously");
            await UniTask.Delay(300);
            Require(wallet.Current.Balance == stoppedBalance, "disconnected presentation cannot pay");
            singularity.enabled = true;
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

    private static async UniTask VerifyCoordinator(SingularityCellAdapter singularity, CurrencyWalletApplication wallet)
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
            var transfer = coordinator.RunAsync(Vector3Int.right, Vector3.zero, Vector3.right, () => clone,
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
