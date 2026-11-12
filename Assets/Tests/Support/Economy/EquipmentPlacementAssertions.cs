#if UNITY_EDITOR
using System;
using System.Reflection;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class EquipmentPlacementAssertions
{
    private static int _checks;




    private static ConstructionStockApplication FundedStock()
    {
        var stock = new ConstructionStockApplication(new[] { ResourceTypeDomain.Plank, ResourceTypeDomain.IronIngot });
        foreach (var type in new[] { ResourceTypeDomain.Plank, ResourceTypeDomain.IronIngot })
        { stock.TryReserveDeposit(type, 200, out var ticket); ticket.TryCommit(); }
        return stock;
    }

    public static void VerifyTransactions()
    {
        var prices = AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset").CreateCatalog();
        var wallet = new CurrencyWalletApplication(15);
        var service = new EquipmentPlacementApplication(wallet, prices, FundedStock());
        var installed = false;
        var result = service.Execute(CellTypeDomain.Storage, () => true, () => installed = true,
            () => installed = false, () => Require(installed, "complete sees installed state"), out var cost);
        Require(result == EquipmentPlacementApplication.Result.Success && cost == 15 && wallet.Current.Balance == 0, "exact balance success");
        result = service.Execute(CellTypeDomain.Conveyor, () => true, () => throw new Exception("must not install"), () => { }, () => { }, out _);
        Require(result == EquipmentPlacementApplication.Result.InsufficientFunds && wallet.Current.ReservedExpense == 0, "insufficient stops before generation");
        for (var stage = 0; stage < 3; stage++)
        {
            wallet = new CurrencyWalletApplication(100);
            service = new EquipmentPlacementApplication(wallet, prices, FundedStock());
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
        service = new EquipmentPlacementApplication(wallet, prices, FundedStock());
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

    public static async UniTask VerifyPlay()
    {
        try
        {
            _checks = 0;
            await UniTask.Yield();
            var grid = GridFieldDatabaseAdapter.Instance;
            AudioListener.volume = 0;
            var generator = TestScenes.Find<GridFieldGeneratorAdapter>();
            // このケースは配置の復元を扱う。材料不足はConstructionStockTestsで独立して確認する。
            foreach (var type in new[] { ResourceTypeDomain.Plank, ResourceTypeDomain.IronIngot })
            { generator.ConstructionStock.TryReserveDeposit(type, 200, out var ticket); ticket.TryCommit(); }
            var placer = TestScenes.Find<CellPlacerAdapter>();
            var wallet = generator.Wallet;
            Require(wallet.Current.Balance == 100, "shared starting balance");
            // 初回経路は課金後も100以内で成立する。
            var strip=grid.Owners.OfType<EmptyCellAdapter>().First(c=>c.ZIndex>0 && grid.IsWithinBounds(c.XIndex,c.ZIndex+2) &&
                grid.GetCell(c.XIndex,c.ZIndex+1) is EmptyCellAdapter && grid.GetCell(c.XIndex,c.ZIndex+2) is EmptyCellAdapter &&
                grid.GetCell(c.XIndex,c.ZIndex-1) is not ResourceCellAdapter);
            var testX=strip.XIndex;var testZ=strip.ZIndex;
            Place(placer, grid.GetCell(testX, testZ), CellTypeDomain.ExtractorWood, 180);
            Require(wallet.Current.Balance == 80 && placer.LastPlacedCount == 1 && placer.LastPlacementCost == 20,
                "extractor charged once");
            Require(Quaternion.Angle(grid.GetCell(testX, testZ).transform.rotation, Quaternion.Euler(0, 180, 0)) < .1f, "rotation retained");
            Require(Get<CellBaseAdapter>(placer, "_selectedCell") == grid.GetCell(testX, testZ) &&
                Get<CellBaseAdapter>(CellStatusViewAdapter.Instance, "_selectedCell") == grid.GetCell(testX, testZ), "cursor and status select new cell");
            Place(placer, grid.GetCell(testX, testZ+1), CellTypeDomain.ExportConveyor, 0);
            Place(placer, grid.GetCell(testX, testZ+2), CellTypeDomain.Conveyor, 0);
            Require(wallet.Current.Balance == 70 && wallet.Current.ReservedExpense == 0, "three purchased cells cost thirty");
            // 実収益はStartSitePlayModeTestsで確認する。このケースは購入・撤去と失敗復元を扱う。
            // 撤去は返金せず、既存の在庫・搬送取消経路を使う。
            var balance = wallet.Current.Balance;
            Place(placer, grid.GetCell(testX, testZ+1), CellTypeDomain.Empty, 0);
            Require(wallet.Current.Balance == balance && grid.GetCell(testX, testZ+1) is EmptyCellAdapter && placer.LastPlacementCost == 0,
                "removal free without refund");
            Place(placer, grid.GetCell(testX, testZ), CellTypeDomain.Empty, 0);
            Place(placer, grid.GetCell(testX, testZ+2), CellTypeDomain.Empty, 0);
            await UniTask.Delay(100);
            var singularity = grid.Owners.OfType<SingularityCellAdapter>().Single();
            var resource = grid.Owners.OfType<ResourceCellAdapter>().First();
            Place(placer, singularity, CellTypeDomain.Empty, 0);
            Place(placer, resource, CellTypeDomain.Conveyor, 0);
            Require(grid.Owners.OfType<SingularityCellAdapter>().Single() == singularity && grid.Owners.OfType<ResourceCellAdapter>().First() == resource, "protected targets unchanged");
            // 3セルの逆向きドラッグで2セル分の残高だけを用意する。
            wallet.TryReserveExpense(wallet.Current.Balance - 10, out var drain);
            drain.TryCommit();
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            placer.SelectGrid(grid.GetCell(testX, testZ+2).gameObject); placer.PointerBegin();
            placer.SelectGrid(grid.GetCell(testX, testZ).gameObject); placer.PointerDrag();
            Require(placer.PreviewCount == 3 && placer.PreviewCost == 15 && placer.PreviewText.Contains("15"), "range preview quantity and cost");
            var panel = generator.GetComponent<EconomyPanelAdapter>();
            var availableBeforeToggle = wallet.Current.AvailableBalance;
            panel.ShowPage(1); panel.ShowPage(2); panel.ShowPage(0);
            panel.SetExpanded(false); panel.SetExpanded(true);
            panel.enabled = false; panel.enabled = true;
            Require(wallet.Current.AvailableBalance == availableBeforeToggle && placer.PreviewCount == 0,
                "UI page, visibility and enable changes cancel placement without payment");
            placer.SelectGrid(grid.GetCell(testX, testZ+2).gameObject); placer.PointerBegin();
            placer.SelectGrid(grid.GetCell(testX, testZ).gameObject); placer.PointerDrag();
            await UniTask.DelayFrame(2);
            Require(panel.PlacementText.Contains("費用 15") && panel.PlacementText.Contains("残高不足"), "integrated range quote reports insufficient funds");
            var placementWarnings = 0;
            Application.LogCallback countWarning = (message, stack, type) => { if (type == LogType.Warning && message.Contains("配置失敗：残高不足")) placementWarnings++; };
            Application.logMessageReceived += countWarning;
            try { placer.PointerEnd(); placer.PointerEnd(); }
            finally { Application.logMessageReceived -= countWarning; }
            Require(placementWarnings == 0, "insufficient funds uses UI instead of system warning");
            await UniTask.DelayFrame(2);
            Require(!panel.PlacementText.Contains("確定") && !panel.PlacementText.Contains("支出") && panel.PlacementText.Contains("残高不足"), "integrated result matches partial placement expense");
            // 反復PointerEnd後も残高と配置を変えない。
            Require(grid.GetCell(testX, testZ+2) is ConveyorCellAdapter && grid.GetCell(testX, testZ+1) is ConveyorCellAdapter &&
                grid.GetCell(testX, testZ) is EmptyCellAdapter && wallet.Current.Balance == 0, "reverse range stops on third cell");
            Require(grid.GetCell(testX, testZ).CellModel.activeSelf && placer.PreviewCount == 0, "unplaced model and preview restored");
            Require(placer.LastPlacedCount == 2 && placer.LastPlacementCost == 10, "repeated end preserves result");
            // 失敗前後の結果表示を独立した順方向操作で確認する。
            Place(placer, grid.GetCell(testX, testZ+2), CellTypeDomain.Empty, 0);
            Place(placer, grid.GetCell(testX, testZ+1), CellTypeDomain.Empty, 0);
            wallet.TryReserveIncome(10, out var fund); fund.TryCommit();
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            placer.SelectGrid(grid.GetCell(testX, testZ).gameObject); placer.PointerBegin();
            placer.SelectGrid(grid.GetCell(testX, testZ+2).gameObject); placer.PointerDrag();
            await Capture("placement-preview.png");
            placer.PointerEnd();
            Require(grid.GetCell(testX, testZ) is ConveyorCellAdapter && grid.GetCell(testX, testZ+1) is ConveyorCellAdapter &&
                grid.GetCell(testX, testZ+2) is EmptyCellAdapter, "forward coordinate order");
            Require(placer.LastPlacedCount == 2 && placer.LastPlacementCost == 10 &&
                placer.LastPlacementResult == EquipmentPlacementApplication.Result.InsufficientFunds && placer.ResultText.Contains("残高不足"), "partial result display");
            await Capture("placement-result.png");
            Place(placer, grid.GetCell(testX, testZ), CellTypeDomain.Empty, 0);
            Place(placer, grid.GetCell(testX, testZ+1), CellTypeDomain.Empty, 0);
            // 登録後に初期化が失敗する実Adapterを使い、グリッドと隣接参照を復元する。
            wallet.TryReserveIncome(20, out fund); fund.TryCommit();
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            var cached = Get<CellBaseAdapter>(placer, "_cachedCell");
            var template = TestScenes.NewObject("Faulting placement template");
            TestScenes.NewObject("Model").transform.SetParent(template.transform);
            template.SetActive(false);
            var failing = template.AddComponent<FailingPlacementCellAdapter>();
            Set(placer, "_cachedCell", failing);
            var original = grid.GetCell(testX, testZ+2);
            var systemWarnings = 0;
            Application.LogCallback countSystemWarning = (message, stack, type) => { if (type == LogType.Warning && message.Contains("配置失敗：設置に失敗しました")) systemWarnings++; };
            Application.logMessageReceived += countSystemWarning;
            try { placer.SelectGrid(original.gameObject); placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd(); }
            finally { Application.logMessageReceived -= countSystemWarning; }
            await UniTask.DelayFrame(2);
            Require(systemWarnings == 1 && !panel.PlacementText.Contains("失敗"), "system failure uses Console without HUD error");
            Require(grid.GetCell(testX, testZ+2) == original && original.gameObject.activeSelf && original.CellModel.activeSelf &&
                wallet.Current.Balance == 20 && wallet.Current.ReservedExpense == 0 && placer.LastPlacedCount == 0 &&
                placer.LastPlacementResult == EquipmentPlacementApplication.Result.Failed, "real initialization rollback");
            failing.Replacement = original;
            placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Require(grid.GetCell(testX, testZ+2) == original && wallet.Current.Balance == 20 &&
                wallet.Current.ReservedExpense == 0 && placer.LastPlacementResult == EquipmentPlacementApplication.Result.Failed,
                "registration changed during initialization restores original");
            Require(((ConnectableCellBaseAdapter)singularity).Connections.Count == 0, "rollback removes neighbor references");
            Set(placer, "_cachedCell", cached);
            UnityEngine.Object.Destroy(template);
            Place(placer, original, CellTypeDomain.Storage, 90);
            Require(wallet.Current.Balance == 5 && grid.GetCell(testX, testZ+2) is StorageCellAdapter, "placement works after rollback");
            var occupied = grid.GetCell(testX, testZ+2);
            var occupiedBalance = wallet.Current.Balance;
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            placer.SelectGrid(occupied.gameObject); placer.PointerBegin(); placer.PointerDrag();
            Require(placer.PlacementNotice.Contains("既存セル") && placer.PreviewCount == 0, "occupied target has dedicated UI reason");
            placer.PointerEnd();
            await UniTask.DelayFrame(2);
            Require(panel.PlacementText.Contains("既存セル") && grid.GetCell(testX, testZ+2) == occupied && wallet.Current.Balance == occupiedBalance, "overlap stays visible without replacement or expense");
            VerifyMultiblockPlacement(grid, placer, wallet);
            Debug.Log($"Equipment placement PLAY checks passed: {_checks} assertions; paid starter route, range order, partial funds, rollback, protection, selection, rotation and removal.");
        }
        catch (Exception exception) { throw; }
    }

    private static void VerifyMultiblockPlacement(GridFieldDatabaseAdapter grid, CellPlacerAdapter placer,
        CurrencyWalletApplication wallet)
    {
        // 地形に依存しない小領域で、実際の配置入口と設備寿命を検証する。
        placer.SetSelectedCellType(CellTypeDomain.Empty);
        var emptyTemplate = Get<CellBaseAdapter>(placer, "_cachedCell");
        for (var x = 7; x <= 14; x++)
        for (var z = 7; z <= 14; z++)
        {
            var old = grid.GetCell(x, z);
            if (old is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
            old.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(old.gameObject);
            var empty = TestScenes.Clone(emptyTemplate, new Vector3(x, 0, z), Quaternion.identity, grid.transform);
            grid.SaveCell(x, z, empty); empty.gameObject.SetActive(true);
        }
        wallet.TryReserveIncome(100, out var funds); funds.TryCommit();
        for (var turn = 0; turn < 4; turn++)
        {
            placer.transform.rotation = Quaternion.Euler(0, turn * 90, 0);
            placer.SetSelectedCellType(CellTypeDomain.Storage);
            var template = Get<CellBaseAdapter>(placer, "_cachedCell");
            SetFootprint(template, 3, 2);
            var points = template.Footprint.Coordinates(10, 10, turn).ToArray();
            var prior = points.Select(p => grid.GetCell(p.x, p.z)).ToArray();
            var balance = wallet.Current.Balance;
            placer.SelectGrid(grid.GetCell(10, 10).gameObject);
            placer.PointerBegin(); placer.PointerDrag();
            Require(placer.PreviewCount == 1 && placer.PreviewCost == 15, "multiblock preview counts one equipment");
            placer.PointerEnd();
            var owner = grid.GetCell(10, 10);
            Require(owner is StorageCellAdapter && points.All(p => grid.GetCell(p.x, p.z) == owner) &&
                wallet.Current.Balance == balance - 15 && placer.LastPlacedCount == 1, "rotated footprint charged once " + turn);
            Require(prior.All(c => !c.gameObject.activeSelf), "all replaced models and colliders retired");
            var far = points.Last();
            placer.SelectGrid(owner.gameObject, new Vector3(far.x, .5f, far.z));
            Require(Get<CellBaseAdapter>(placer, "_selectedCell") == owner, "any occupied tile selects owner");
            // 角1マスだけの重なりも、設置と課金の前に拒否する。
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
            placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Require(placer.PreviewCount == 0 && wallet.Current.Balance == balance - 15 &&
                points.All(p => grid.GetCell(p.x, p.z) == owner), "partial overlap cannot fragment owner");
            placer.SetSelectedCellType(CellTypeDomain.Empty);
            placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Require(points.All(p => grid.GetCell(p.x, p.z) is EmptyCellAdapter) &&
                placer.LastPlacedCount == 1 && wallet.Current.Balance == balance - 15 && !owner.gameObject.activeSelf,
                "any tile removes whole equipment once " + turn);
        }
        placer.SetSelectedCellType(CellTypeDomain.Storage);
        var cached = Get<CellBaseAdapter>(placer, "_cachedCell");
        var faultObject = TestScenes.NewObject("Multiblock fault template");
        faultObject.SetActive(false);
        TestScenes.NewObject("Model").transform.SetParent(faultObject.transform);
        var failing = faultObject.AddComponent<FailingPlacementCellAdapter>();
        SetFootprint(failing, 3, 2);
        Set(placer, "_cachedCell", failing);
        placer.transform.rotation = Quaternion.identity;
        var originals = failing.Footprint.Coordinates(10, 10, 0).Select(p => (p, owner: grid.GetCell(p.x, p.z))).ToArray();
        var before = wallet.Current.Balance;
        placer.SelectGrid(grid.GetCell(10, 10).gameObject); placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
        Require(placer.LastPlacementResult == EquipmentPlacementApplication.Result.Failed &&
            originals.All(e => grid.GetCell(e.p.x, e.p.z) == e.owner && e.owner.gameObject.activeSelf && e.owner.CellModel.activeSelf) &&
            wallet.Current.Balance == before && wallet.Current.ReservedExpense == 0, "multiblock initialization failure restores six owners and expense");
        Set(placer, "_cachedCell", cached);
        UnityEngine.Object.Destroy(faultObject);
        wallet.TryReserveIncome(400, out var advancedFunds); advancedFunds.TryCommit();
        for (var turn = 0; turn < 4; turn++)
        {
            var beforePlacement = wallet.Current.Balance;
            Place(placer, grid.GetCell(10, 10), CellTypeDomain.AdvancedCrafter, turn * 90);
            var owner = grid.GetCell(10, 10) as CrafterCellAdapter;
            Require(owner != null && owner.InputCount == 2 && owner.Footprint.Area == 6 &&
                owner.ProgressionType == CellTypeDomain.AdvancedCrafter && wallet.Current.Balance == beforePlacement - 90,
                "saved advanced prefab places once at rotation " + turn);
            var occupied = owner.OccupiedCoordinates.ToArray();
            Physics.SyncTransforms();
            var box = owner.GetComponent<BoxCollider>();
            Require(box.size == new Vector3(3, 1, 2) && occupied.All(p => box.bounds.Contains(new Vector3(p.x, .25f, p.z))),
                "collider contains every occupied tile");
            placer.SetSelectedCellType(CellTypeDomain.Empty);
            var corner = occupied.Last();
            placer.SelectGrid(owner.gameObject, new Vector3(corner.x, .5f, corner.z));
            placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Require(occupied.All(p => grid.GetCell(p.x, p.z) is EmptyCellAdapter) && placer.LastPlacedCount == 1,
                "saved advanced prefab removes all six tiles");
        }
        wallet.TryReserveExpense(wallet.Current.Balance, out var drain); drain.TryCommit();
        var untouched = grid.GetCell(10, 10);
        Place(placer, untouched, CellTypeDomain.AdvancedCrafter, 0);
        Require(placer.LastPlacementResult == EquipmentPlacementApplication.Result.InsufficientFunds && grid.GetCell(10, 10) == untouched &&
            wallet.Current.ReservedExpense == 0, "insufficient funds leave multiblock occupancy untouched");
    }

    private static void SetFootprint(CellBaseAdapter cell, int width, int depth)
    {
        typeof(CellBaseAdapter).GetField("footprintWidth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cell, width);
        typeof(CellBaseAdapter).GetField("footprintDepth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cell, depth);
        if (cell is ConnectableCellBaseAdapter connected)
            CellConnectionAssertions.SetPorts(connected, new CellPortDefinition(0, 0, 0, DirectionsDomain.Back, CellPortUseDomain.Input),
                new CellPortDefinition(1, width - 1, 0, DirectionsDomain.Back, CellPortUseDomain.Input),
                new CellPortDefinition(2, width / 2, depth - 1, DirectionsDomain.Forward, CellPortUseDomain.Output));
    }

    private static async UniTask Capture(string name)
    {
        if (!TestScenes.CaptureImages || Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
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

#endif
