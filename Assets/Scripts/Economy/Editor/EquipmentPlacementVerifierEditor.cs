using System;
using System.Reflection;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class EquipmentPlacementVerifierEditor
{
    private const string SessionKey = "FactoryGame.EquipmentPlacementVerification";
    private static int _checks;
    static EquipmentPlacementVerifierEditor() => EditorApplication.playModeStateChanged += state =>
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyPlay().Forget();
    };

    public static void Run()
    {
        _checks = 0;
        VerifyTransactions();
        CellConnectionVerifierEditor.Run();
        UIStatusVerifierEditor.VerifyAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        if (!Application.isBatchMode)
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void VerifyTransactions()
    {
        var prices = AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset").CreateCatalog();
        var wallet = new CurrencyWalletApplication(15);
        var service = new EquipmentPlacementApplication(wallet, prices);
        var installed = false;
        var result = service.Execute(CellTypeDomain.Storage, () => true, () => installed = true,
            () => installed = false, () => Require(installed, "complete sees installed state"), out var cost);
        Require(result == EquipmentPlacementApplication.Result.Success && cost == 15 && wallet.Current.Balance == 0, "exact balance success");
        result = service.Execute(CellTypeDomain.Conveyor, () => true, () => throw new Exception("must not install"), () => { }, () => { }, out _);
        Require(result == EquipmentPlacementApplication.Result.InsufficientFunds && wallet.Current.ReservedExpense == 0, "insufficient stops before generation");
        for (var stage = 0; stage < 3; stage++)
        {
            wallet = new CurrencyWalletApplication(100);
            service = new EquipmentPlacementApplication(wallet, prices);
            var grid = 0;
            var restores = 0;
            result = service.Execute(CellTypeDomain.Conveyor, () => true, () =>
            {
                if (stage > 0) grid = 1;
                throw new InvalidOperationException("injected generation/registration/initialization failure");
            }, () => { grid = 0; restores++; }, () => throw new Exception("must not complete"), out _);
            Require(result == EquipmentPlacementApplication.Result.Failed && grid == 0 && restores == 1 &&
                wallet.Current.Balance == 100 && wallet.Current.ReservedExpense == 0, "failure restores target and expense " + stage);
        }
        wallet = new CurrencyWalletApplication(100);
        service = new EquipmentPlacementApplication(wallet, prices);
        var valid = true;
        var calls = 0;
        wallet.Changed += snapshot =>
        {
            if (snapshot.ReservedExpense == 0) return;
            var nested = service.Execute(CellTypeDomain.Conveyor, () => true, () => calls++, () => { }, () => { }, out _);
            Require(nested == EquipmentPlacementApplication.Result.Busy, "notification reentry refused");
            valid = false;
        };
        result = service.Execute(CellTypeDomain.Conveyor, () => valid, () => calls++, () => { }, () => { }, out _);
        Require(result == EquipmentPlacementApplication.Result.InvalidTarget && calls == 0 && wallet.Current.ReservedExpense == 0 &&
            wallet.Current.Balance == 100, "target rechecked after reservation notification");
        Debug.Log($"Equipment placement EDIT checks passed: {_checks} assertions.");
    }

    private static async UniTaskVoid VerifyPlay()
    {
        try
        {
            _checks = 0;
            await UniTask.Yield();
            var grid = GridFieldDatabaseAdapter.Instance;
            var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
            var placer = UnityEngine.Object.FindAnyObjectByType<CellPlacerAdapter>();
            var wallet = generator.Wallet;
            Require(wallet.Current.Balance == 100, "shared starting balance");
            // 初回経路は課金後も100以内で成立する。
            Place(placer, grid.GetCell(50, 47), CellTypeDomain.ExtractorWood, 180);
            Require(wallet.Current.Balance == 80 && placer.LastPlacedCount == 1 && placer.LastPlacementCost == 20,
                "extractor charged once");
            Require(Quaternion.Angle(grid.GetCell(50, 47).transform.rotation, Quaternion.Euler(0, 180, 0)) < .1f, "rotation retained");
            Require(Get<CellBaseAdapter>(placer, "_selectedCell") == grid.GetCell(50, 47) &&
                Get<CellBaseAdapter>(CellStatusViewAdapter.Instance, "_selectedCell") == grid.GetCell(50, 47), "cursor and status select new cell");
            Place(placer, grid.GetCell(50, 48), CellTypeDomain.ExportConveyor, 0);
            Place(placer, grid.GetCell(50, 49), CellTypeDomain.Conveyor, 0);
            Require(wallet.Current.Balance == 70 && wallet.Current.ReservedExpense == 0, "starter route costs thirty");
            await UniTask.WaitUntil(() => wallet.Current.Balance > 70).Timeout(TimeSpan.FromSeconds(30));
            Require(wallet.Current.Balance > 70, "paid route extracts and sells");
            // 撤去は返金せず、既存の在庫・搬送取消経路を使う。
            var balance = wallet.Current.Balance;
            Place(placer, grid.GetCell(50, 48), CellTypeDomain.Empty, 0);
            Require(wallet.Current.Balance == balance && grid.GetCell(50, 48) is EmptyCellAdapter && placer.LastPlacementCost == 0,
                "removal free without refund");
            Place(placer, grid.GetCell(50, 47), CellTypeDomain.Empty, 0);
            Place(placer, grid.GetCell(50, 49), CellTypeDomain.Empty, 0);
            await UniTask.Delay(100);
            var singularity = grid.GetCell(50, 50);
            var resource = grid.GetCell(50, 46);
            Place(placer, singularity, CellTypeDomain.Empty, 0);
            Place(placer, resource, CellTypeDomain.Conveyor, 0);
            Require(grid.GetCell(50, 50) == singularity && grid.GetCell(50, 46) == resource, "protected targets unchanged");
            // 3セルの逆向きドラッグで2セル分の残高だけを用意する。
            wallet.TryReserveExpense(wallet.Current.Balance - 10, out var drain);
            drain.TryCommit();
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            placer.SelectGrid(grid.GetCell(50, 49).gameObject); placer.PointerBegin();
            placer.SelectGrid(grid.GetCell(50, 47).gameObject); placer.PointerDrag();
            Require(placer.PreviewCount == 3 && placer.PreviewCost == 15 && placer.PreviewText.Contains("15"), "range preview quantity and cost");
            var panel = generator.GetComponent<EconomyPanelAdapter>();
            var availableBeforeToggle = wallet.Current.AvailableBalance;
            panel.ShowPage(1); panel.ShowPage(2); panel.ShowPage(0);
            panel.SetExpanded(false); panel.SetExpanded(true);
            panel.enabled = false; panel.enabled = true;
            Require(wallet.Current.AvailableBalance == availableBeforeToggle && placer.PreviewCount == 3,
                "UI page, visibility and enable changes preserve placement quote without payment");
            await UniTask.DelayFrame(2);
            Require(panel.PlacementText.Contains("費用 15") && panel.PlacementText.Contains("残高不足"), "integrated range quote reports insufficient funds");
            placer.PointerEnd(); placer.PointerEnd();
            await UniTask.DelayFrame(2);
            Require(panel.PlacementText.Contains("確定 2個 / 支出 10") && panel.PlacementText.Contains("残高不足"), "integrated result matches partial placement expense");
            // 反復PointerEnd後も残高と配置を変えない。
            Require(grid.GetCell(50, 49) is ConveyorCellAdapter && grid.GetCell(50, 48) is ConveyorCellAdapter &&
                grid.GetCell(50, 47) is EmptyCellAdapter && wallet.Current.Balance == 0, "reverse range stops on third cell");
            Require(grid.GetCell(50, 47).CellModel.activeSelf && placer.PreviewCount == 0, "unplaced model and preview restored");
            Require(placer.LastPlacedCount == 2 && placer.LastPlacementCost == 10, "repeated end preserves result");
            // 失敗前後の結果表示を独立した順方向操作で確認する。
            Place(placer, grid.GetCell(50, 49), CellTypeDomain.Empty, 0);
            Place(placer, grid.GetCell(50, 48), CellTypeDomain.Empty, 0);
            wallet.TryReserveIncome(10, out var fund); fund.TryCommit();
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            placer.SelectGrid(grid.GetCell(50, 47).gameObject); placer.PointerBegin();
            placer.SelectGrid(grid.GetCell(50, 49).gameObject); placer.PointerDrag();
            await Capture("placement-preview.png");
            placer.PointerEnd();
            Require(grid.GetCell(50, 47) is ConveyorCellAdapter && grid.GetCell(50, 48) is ConveyorCellAdapter &&
                grid.GetCell(50, 49) is EmptyCellAdapter, "forward coordinate order");
            Require(placer.LastPlacedCount == 2 && placer.LastPlacementCost == 10 &&
                placer.LastPlacementResult == EquipmentPlacementApplication.Result.InsufficientFunds && placer.ResultText.Contains("残高不足"), "partial result display");
            await Capture("placement-result.png");
            Place(placer, grid.GetCell(50, 47), CellTypeDomain.Empty, 0);
            Place(placer, grid.GetCell(50, 48), CellTypeDomain.Empty, 0);
            // 登録後に初期化が失敗する実Adapterを使い、グリッドと隣接参照を復元する。
            wallet.TryReserveIncome(20, out fund); fund.TryCommit();
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            var cached = Get<CellBaseAdapter>(placer, "_cachedCell");
            var template = new GameObject("Faulting placement template");
            new GameObject("Model").transform.SetParent(template.transform);
            template.SetActive(false);
            var failing = template.AddComponent<FailingPlacementCellAdapter>();
            Set(placer, "_cachedCell", failing);
            var original = grid.GetCell(50, 49);
            placer.SelectGrid(original.gameObject); placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Require(grid.GetCell(50, 49) == original && original.gameObject.activeSelf && original.CellModel.activeSelf &&
                wallet.Current.Balance == 20 && wallet.Current.ReservedExpense == 0 && placer.LastPlacedCount == 0 &&
                placer.LastPlacementResult == EquipmentPlacementApplication.Result.Failed, "real initialization rollback");
            failing.Replacement = original;
            placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Require(grid.GetCell(50, 49) == original && wallet.Current.Balance == 20 &&
                wallet.Current.ReservedExpense == 0 && placer.LastPlacementResult == EquipmentPlacementApplication.Result.Failed,
                "registration changed during initialization restores original");
            var adjacent = (CellBaseAdapter[])typeof(ConnectableCellBaseAdapter)
                .GetProperty("AdjacentCells", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(singularity);
            Require(Array.TrueForAll(adjacent, cell => cell == null), "rollback removes neighbor references");
            Set(placer, "_cachedCell", cached);
            UnityEngine.Object.Destroy(template);
            Place(placer, original, CellTypeDomain.Storage, 90);
            Require(wallet.Current.Balance == 5 && grid.GetCell(50, 49) is StorageCellAdapter, "placement works after rollback");
            Debug.Log($"Equipment placement PLAY checks passed: {_checks} assertions; paid starter route, range order, partial funds, rollback, protection, selection, rotation and removal.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static async UniTask Capture(string name)
    {
        if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var path = Path.Combine(Path.GetDirectoryName(Application.dataPath), name);
        if (File.Exists(path)) File.Delete(path);
        await UniTask.WaitForEndOfFrame();
        var capture = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(path, capture.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(capture); }
    }

    private static void Place(CellPlacerAdapter placer, CellBaseAdapter cell, CellTypeDomain type, float rotation)
    {
        placer.transform.rotation = Quaternion.Euler(0, rotation, 0);
        placer.SetSelectedCellType(type);
        placer.SelectGrid(cell.gameObject);
        placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
    }
    private static T Get<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Equipment placement check failed: " + message);
    }
}
