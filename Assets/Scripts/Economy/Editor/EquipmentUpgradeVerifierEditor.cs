using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Result = EquipmentUpgradeResultApplication;

public static class EquipmentUpgradeVerifierEditor
{
    private static int _checks;
    public static void Run() => EffectiveStatsVerifierEditor.RunUpgrades();

    private sealed class Target : IEquipmentUpgradeTargetApplication
    {
        public bool IsAvailable { get; set; } = true;
        public CellTypeDomain Type { get; set; } = CellTypeDomain.Storage;
        public int Level { get; set; } = 1;
        public bool Fail;
        public Action Notify;
        public bool TrySetLevelWithoutNotification(int expectedLevel, int nextLevel)
        {
            if (Fail) throw new InvalidOperationException("Injected validation failure");
            if (Level != expectedLevel) return false;
            Level = nextLevel;
            return true;
        }
        public void NotifyChanged() => Notify?.Invoke();
    }

    private static CurrencyPriceCatalogDomain Catalog(long price = 15) => new(100,
        Array.Empty<KeyValuePair<ResourceTypeDomain, long>>(),
        new[] { new KeyValuePair<CellTypeDomain, long>(CellTypeDomain.Storage, price) });

    public static void VerifyRules()
    {
        _checks = 0;
        var target = new Target();
        var wallet = new CurrencyWalletApplication(45);
        var app = new EquipmentUpgradeApplication(wallet, Catalog());
        Require(app.CanPurchase(target, 1, out var cost) == Result.Success && cost == 15, "first price");
        Require(app.Purchase(target, 1) == Result.Success && target.Level == 2 && wallet.Current.Balance == 30, "1 to 2 paid once");
        Require(app.Purchase(target, 1) == Result.LevelChanged && wallet.Current.Balance == 30, "repeated old level");
        Require(app.CanPurchase(target, 2, out cost) == Result.Success && cost == 30, "second price");
        Require(app.Purchase(target, 2) == Result.Success && target.Level == 3 && wallet.Current.Balance == 0, "2 to 3 exact funds");
        Require(app.Purchase(target, 3) == Result.MaxLevel && wallet.Current.ReservedExpense == 0, "maximum");
        wallet = new CurrencyWalletApplication(14); target = new Target();
        app = new EquipmentUpgradeApplication(wallet, Catalog());
        Require(app.Purchase(target, 1) == Result.InsufficientFunds && target.Level == 1 && wallet.Current.Balance == 14, "insufficient");
        Require(app.Purchase(null, 1) == Result.InvalidTarget, "null target");
        foreach (var type in new[] { CellTypeDomain.None, CellTypeDomain.Empty, CellTypeDomain.Singularity, CellTypeDomain.ResourceWood })
        {
            target.Type = type;
            Require(app.Purchase(target, 1) == Result.InvalidPrice, "unsupported price");
        }
        target.Type = CellTypeDomain.Storage;
        target.IsAvailable = false;
        Require(app.Purchase(target, 1) == Result.InvalidTarget, "unavailable");
        target.IsAvailable = true; target.Level = 0;
        Require(app.Purchase(target, 0) == Result.InvalidPrice, "invalid level");

        wallet = new CurrencyWalletApplication(long.MaxValue); target = new Target();
        app = new EquipmentUpgradeApplication(wallet, Catalog(long.MaxValue));
        Require(app.Purchase(target, 1) == Result.Success && wallet.Current.Balance == 0, "long maximum exact payment");
        Require(app.Purchase(target, 2) == Result.InvalidPrice && target.Level == 2, "price overflow");

        for (var mode = 0; mode < 4; mode++)
        {
            wallet = new CurrencyWalletApplication(100); target = new Target();
            app = new EquipmentUpgradeApplication(wallet, Catalog());
            var reentry = Result.Success;
            var observed = false;
            var selectedMode = mode;
            wallet.Changed += snapshot =>
            {
                if (snapshot.ReservedExpense == 0) return;
                observed = true;
                reentry = app.Purchase(target, 1);
                if (selectedMode == 0) target.IsAvailable = false;
                if (selectedMode == 1) target.Level = 2;
                if (selectedMode == 2) target.Type = CellTypeDomain.Conveyor;
                if (selectedMode == 3) target.Fail = true;
            };
            var result = app.Purchase(target, 1);
            Require(observed && reentry == Result.Busy && result == (mode == 1 ? Result.LevelChanged : mode == 3 ? Result.Failed : Result.InvalidTarget), "reservation race result");
            Require(wallet.Current.Balance == 100 && wallet.Current.ReservedExpense == 0 && wallet.LastNotificationException == null, "race releases expense");
        }

        wallet = new CurrencyWalletApplication(100); target = new Target();
        app = new EquipmentUpgradeApplication(wallet, Catalog());
        var consistent = true;
        var callbacks = 0;
        wallet.Changed += snapshot =>
        {
            callbacks++;
            if (snapshot.ReservedExpense == 0)
                consistent &= target.Level == 2 && snapshot.Balance == 85 && app.Purchase(target, 2) == Result.Busy &&
                    app.CanPurchase(target, 2, out var nextCost) == Result.Success && nextCost == 30;
        };
        target.Notify = () =>
        {
            consistent &= wallet.Current.Balance == 85 && wallet.Current.ReservedExpense == 0 && target.Level == 2;
            consistent &= app.Purchase(target, 2) == Result.Busy;
            throw new InvalidOperationException("Injected notification exception");
        };
        Require(app.Purchase(target, 1) == Result.Success && consistent && callbacks == 2 &&
            app.LastException != null && wallet.LastNotificationException == null, "atomic notification, reentry and exception isolation");
        // 他の購入予約がある場合も使用可能残高を使う。
        wallet = new CurrencyWalletApplication(20); target = new Target();
        wallet.TryReserveExpense(10, out var other);
        app = new EquipmentUpgradeApplication(wallet, Catalog());
        Require(app.Purchase(target, 1) == Result.InsufficientFunds && wallet.Current.ReservedExpense == 10, "other expense reservation");
        other.TryCancel();
        Require(app.Purchase(target, 1) == Result.Success && wallet.Current.Balance == 5, "cancel restores availability");
        Debug.Log($"Equipment upgrade rules passed: {_checks} assertions.");
    }

    public static void VerifyScene()
    {
        _checks = 0;
        var grid = GridFieldDatabaseAdapter.Instance;
        var generator = UnityEngine.Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
        var upgrades = new EquipmentUpgradeAdapter(grid, generator.Wallet, generator.Prices);
        var wallet = generator.Wallet;
        wallet.TryReserveIncome(10000, out var income); income.TryCommit();
        Require(upgrades != null && EditorUtility.audioMasterMute, "session service and muted audio");
        Require(upgrades.Purchase(null, 1) == Result.InvalidTarget && upgrades.Purchase(grid.GetCell(50, 50), 1) == Result.InvalidTarget &&
            upgrades.Purchase(grid.GetCell(50, 46), 1) == Result.InvalidTarget, "protected targets");
        var names = new[] { "ExtractorWood", "ExtractorStone", "Conveyor", "ExportConveyor", "Crossing", "Storage", "Crafter" };
        var types = new[] { CellTypeDomain.ExtractorWood, CellTypeDomain.ExtractorStone, CellTypeDomain.Conveyor,
            CellTypeDomain.ExportConveyor, CellTypeDomain.Crossing, CellTypeDomain.Storage, CellTypeDomain.Crafter };
        for (var i = 0; i < names.Length; i++)
        {
            var cell = Install(grid, names[i], 20 + i, 20);
            var before = wallet.Current.Balance;
            var identity = cell.Identity;
            var position = cell.transform.position;
            var rotation = cell.transform.rotation;
            generator.Prices.TryGetPlacementAmount(types[i], 1, out var price);
            Require(upgrades.CanPurchase(cell, 1, out var quote) == Result.Success && quote == price, "type and price " + names[i]);
            Require(upgrades.Purchase(cell, 1) == Result.Success && upgrades.Purchase(cell, 1) == Result.LevelChanged &&
                upgrades.Purchase(cell, 2) == Result.Success && upgrades.Purchase(cell, 3) == Result.MaxLevel, "levels and repeated input " + names[i]);
            Require(wallet.Current.Balance == before - price * 3 && wallet.Current.ReservedExpense == 0 &&
                cell.EquipmentLevel == 3 && ReferenceEquals(identity, cell.Identity) && ReferenceEquals(grid.GetCell(20 + i, 20), cell) &&
                cell.transform.position == position && cell.transform.rotation == rotation, "instance and transform preserved " + names[i]);
        }
        foreach (var mutation in new[] { "disable", "replace", "destroy", "move" })
        {
            var cell = Install(grid, "Storage", 20, 22);
            var balance = wallet.Current.Balance;
            Action<CurrencyWalletApplication.Snapshot> handler = snapshot =>
            {
                if (snapshot.ReservedExpense == 0) return;
                if (mutation == "disable") cell.gameObject.SetActive(false);
                if (mutation == "replace") Install(grid, "Empty", 20, 22);
                if (mutation == "destroy") UnityEngine.Object.DestroyImmediate(cell.gameObject);
                if (mutation == "move") cell.XIndex++;
            };
            wallet.Changed += handler;
            var result = upgrades.Purchase(cell, 1);
            wallet.Changed -= handler;
            Require(result == Result.InvalidTarget && wallet.Current.Balance == balance && wallet.Current.ReservedExpense == 0 &&
                wallet.LastNotificationException == null, "cell race " + mutation);
            if (mutation == "move") cell.XIndex--;
        }
        var invalid = (StorageCellAdapter)Install(grid, "Storage", 21, 22);
        typeof(StorageCellAdapter).GetField("capacity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(invalid, int.MaxValue);
        var saved = wallet.Current.Balance;
        Require(upgrades.Purchase(invalid, 1) == Result.Failed && invalid.EquipmentLevel == 1 && wallet.Current.Balance == saved &&
            wallet.Current.ReservedExpense == 0, "invalid stats release expense before level update");
        typeof(StorageCellAdapter).GetField("capacity", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(invalid, 50);
        for (var x = 29; x <= 32; x++)
        for (var z = 29; z <= 31; z++) Install(grid, "Empty", x, z);
        var crossing = (CrossingCellAdapter)Install(grid, "Crossing", 30, 30);
        var receiver = (StorageCellAdapter)Install(grid, "Storage", 31, 30);
        Require(crossing.AllocateStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone), "crossing downstream reservation");
        var pool = ResourceItemObjectPoolAdapter.Instance;
        var id = pool.CreateIdFromResourceData(ResourceTypeDomain.Stone, 5);
        crossing.Reuse(Vector3Int.right, id);
        var pending = (System.Collections.IDictionary)typeof(CrossingCellAdapter)
            .GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(crossing);
        var transfer = pending[Vector3Int.right];
        var links = typeof(ConnectableCellBaseAdapter).GetProperty("AdjacentCells", BindingFlags.Instance | BindingFlags.NonPublic);
        var adjacent = links.GetValue(crossing);
        var opposite = links.GetValue(receiver);
        Require(upgrades.Purchase(crossing, 1) == Result.Success && ReferenceEquals(transfer, pending[Vector3Int.right]) &&
            ReferenceEquals(adjacent, links.GetValue(crossing)) && ReferenceEquals(opposite, links.GetValue(receiver)) &&
            receiver.AllocatedAmount == 5 && pool.TakeResourceDataById(id).amount == 5, "crossing holds connection, reservation and ID after purchase");
        crossing.CancelStorage(Vector3Int.right, 5, ResourceTypeDomain.Stone);
        Require(receiver.AllocatedAmount == 0 && pending.Count == 0, "old crossing reservation cancels after purchase");
        Debug.Log($"Equipment upgrade scene checks passed: {_checks} assertions; all equipment, prices, repeated input, identity, transform and deletion races.");
    }

    private static CellBaseAdapter Install(GridFieldDatabaseAdapter grid, string name, int x, int z)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Field/General/F_{name}Cell.prefab");
        var old = grid.GetCell(x, z);
        var cell = UnityEngine.Object.Instantiate(source, old != null ? old.transform.position : new Vector3(x, 0, z),
            Quaternion.identity, grid.transform.GetChild(x)).GetComponent<CellBaseAdapter>();
        if (old != null)
        {
            if (old is ConnectableCellBaseAdapter connected) connected.OnDisconnect();
            old.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(old.gameObject);
        }
        grid.SaveCell(x, z, cell);
        cell.InitializeSystem();
        return cell;
    }

    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException("Equipment upgrade check failed: " + message);
    }
}
