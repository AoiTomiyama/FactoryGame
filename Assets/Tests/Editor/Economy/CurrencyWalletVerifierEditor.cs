using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>通貨の境界・予約競合・終端順序・通知の再入を検証する。</summary>
public static class CurrencyWalletVerifierEditor
{
    private static int _checks;

    [MenuItem("Tools/FactoryGame/Verify Currency Wallet")]
    public static void Run()
    {
        _checks = 0;
        VerifyPrices();
        VerifyBoundaries();
        VerifySettlementOrders();
        VerifyNotifications();
        ResourceReservationVerifierEditor.Run();
        Debug.Log($"Currency wallet checks passed: {_checks} assertions; prices, boundaries, settlement orders, reentrancy; resource reservation regression passed.");
    }

    private static void VerifyPrices()
    {
        foreach (var price in new[] { long.MinValue, -1L, 0L })
            Require(!CurrencyRulesDomain.TryCalculateAmount(price, 1, out var amount) && amount == 0, "Invalid price");
        foreach (var quantity in new[] { int.MinValue, -1, 0 })
            Require(!CurrencyRulesDomain.TryCalculateAmount(1, quantity, out var amount) && amount == 0, "Invalid quantity");
        Require(CurrencyRulesDomain.TryCalculateAmount(int.MaxValue, int.MaxValue, out var total) &&
                total == (long)int.MaxValue * int.MaxValue, "Int boundary uses long multiplication");
        Require(CurrencyRulesDomain.TryCalculateAmount(long.MaxValue, 1, out total) && total == long.MaxValue,
            "Exact long maximum");
        Require(!CurrencyRulesDomain.TryCalculateAmount(long.MaxValue, 2, out total) && total == 0, "Long overflow");
        var definition = ScriptableObject.CreateInstance<CurrencyEconomyDefinition>();
        try
        {
            var catalog = definition.CreateCatalog();
            Require(catalog.InitialBalance == 100, "Initial balance");
            foreach (var resource in new[] { ResourceTypeDomain.Wood, ResourceTypeDomain.Stone })
                Require(catalog.TryGetSaleAmount(resource, int.MaxValue, out total) && total == int.MaxValue, "Default sale price");
            foreach (var resource in new[] { ResourceTypeDomain.None, ResourceTypeDomain.Iron, ResourceTypeDomain.Gold, (ResourceTypeDomain)999 })
                Require(!catalog.TryGetSaleAmount(resource, 1, out total) && total == 0, "Undefined sale rejected");
            var cells = new[] { CellTypeDomain.ExtractorWood, CellTypeDomain.ExtractorStone, CellTypeDomain.Conveyor,
                CellTypeDomain.ExportConveyor, CellTypeDomain.Crossing, CellTypeDomain.Storage, CellTypeDomain.Crafter };
            var prices = new[] { 20L, 20L, 5L, 5L, 10L, 15L, 30L };
            for (var i = 0; i < cells.Length; i++)
                Require(catalog.TryGetPlacementAmount(cells[i], 2, out total) && total == prices[i] * 2, "Default equipment price");
            Require(!catalog.TryGetPlacementAmount(CellTypeDomain.Empty, 1, out _), "Protected cell has no price");
            Set(definition, "initialBalance", -1L);
            Throws(() => definition.CreateCatalog());
            Set(definition, "initialBalance", 7L);
            Set(definition, "salePrices", new[] { new CurrencyEconomyDefinition.SalePrice { resource = ResourceTypeDomain.Wood, price = 9 } });
            Require(definition.CreateCatalog().TryGetSaleAmount(ResourceTypeDomain.Wood, 1, out total) && total == 9,
                "Edited configuration is read by new catalog");
            Require(catalog.InitialBalance == 100 && catalog.TryGetSaleAmount(ResourceTypeDomain.Wood, 1, out total) && total == 1,
                "Existing catalog remains frozen");
            foreach (var resource in new[] { ResourceTypeDomain.None, (ResourceTypeDomain)999 })
            {
                Set(definition, "salePrices", new[] { new CurrencyEconomyDefinition.SalePrice { resource = resource, price = 1 } });
                Throws(() => definition.CreateCatalog());
            }
            foreach (var price in new[] { 0L, -1L, long.MinValue })
            {
                Set(definition, "salePrices", new[] { new CurrencyEconomyDefinition.SalePrice { resource = ResourceTypeDomain.Wood, price = price } });
                Throws(() => definition.CreateCatalog());
            }
            var duplicate = new CurrencyEconomyDefinition.SalePrice { resource = ResourceTypeDomain.Wood, price = 1 };
            Set(definition, "salePrices", new[] { duplicate, duplicate });
            Throws(() => definition.CreateCatalog());
            Set(definition, "salePrices", Array.Empty<CurrencyEconomyDefinition.SalePrice>());
            foreach (var cell in new[] { CellTypeDomain.None, CellTypeDomain.Empty, CellTypeDomain.ResourceWood, (CellTypeDomain)999 })
            {
                Set(definition, "placementPrices", new[] { new CurrencyEconomyDefinition.PlacementPrice { cell = cell, price = 1 } });
                Throws(() => definition.CreateCatalog());
            }
            foreach (var price in new[] { 0L, -1L })
            {
                Set(definition, "placementPrices", new[] { new CurrencyEconomyDefinition.PlacementPrice { cell = CellTypeDomain.Storage, price = price } });
                Throws(() => definition.CreateCatalog());
            }
            var duplicateCell = new CurrencyEconomyDefinition.PlacementPrice { cell = CellTypeDomain.Storage, price = 1 };
            Set(definition, "placementPrices", new[] { duplicateCell, duplicateCell });
            Throws(() => definition.CreateCatalog());
            Set(definition, "placementPrices", null);
            Throws(() => definition.CreateCatalog());
            Set(definition, "salePrices", null);
            Throws(() => definition.CreateCatalog());
        }
        finally { UnityEngine.Object.DestroyImmediate(definition); }
    }

    private static void VerifyBoundaries()
    {
        Throws(() => new CurrencyWalletApplication(-1));
        var wallet = new CurrencyWalletApplication(0);
        var notifications = 0;
        wallet.Changed += _ => notifications++;
        foreach (var value in new[] { long.MinValue, -1L, 0L })
        {
            Require(!wallet.TryReserveIncome(value, out var rejected) && rejected == null, "Invalid income");
            Require(!wallet.TryReserveExpense(value, out rejected) && rejected == null, "Invalid expense");
        }
        Require(!wallet.TryReserveExpense(1, out _) && notifications == 0, "Insufficient balance has no notification");
        Require(wallet.TryReserveIncome(long.MaxValue, out var income), "Maximum income reservation");
        Require(!wallet.TryReserveIncome(1, out _) && !wallet.TryReserveExpense(1, out _), "Pending income is not spendable");
        Require(income.TryCommit(), "Maximum income commits");
        State(wallet, long.MaxValue, 0, 0);
        Require(!wallet.TryReserveIncome(1, out _), "Balance limit");
        Require(wallet.TryReserveExpense(long.MaxValue, out var expense), "Maximum expense reservation");
        Require(!wallet.TryReserveExpense(1, out _) && !wallet.TryReserveIncome(1, out _), "Expense reservation does not create income headroom");
        Require(expense.TryCancel(), "Expense cancellation");
        State(wallet, long.MaxValue, 0, 0);
        Require(wallet.TryReserveExpense(long.MaxValue, out expense) && expense.TryCommit(), "Maximum expense commits");
        State(wallet, 0, 0, 0);
        Require(wallet.TryReserveIncome(1, out income) && income.TryCancel(), "Income cancellation releases headroom");
        Require(wallet.TryReserveIncome(long.MaxValue, out income) && income.TryCancel(), "Released full headroom");
        State(wallet, 0, 0, 0);
        var other = new CurrencyWalletApplication(100);
        State(other, 100, 0, 0);
        State(wallet, 0, 0, 0);
    }

    private static void VerifySettlementOrders()
    {
        // 4予約の全24順序と確定/取消の全16組合せを独立した残高モデルと照合する。
        for (var a = 0; a < 4; a++)
        for (var b = 0; b < 4; b++)
        for (var c = 0; c < 4; c++)
        for (var d = 0; d < 4; d++)
        {
            if (a == b || a == c || a == d || b == c || b == d || c == d) continue;
            for (var mask = 0; mask < 16; mask++)
            {
                var wallet = new CurrencyWalletApplication(long.MaxValue - 10);
                var tickets = new CurrencyTransactionApplication[4];
                Require(wallet.TryReserveIncome(4, out tickets[0]) && wallet.TryReserveIncome(6, out tickets[1]) &&
                        wallet.TryReserveExpense(3, out tickets[2]) && wallet.TryReserveExpense(7, out tickets[3]), "Concurrent reservations");
                Require(!wallet.TryReserveIncome(1, out _), "Concurrent income limit");
                long balance = long.MaxValue - 10, income = 10, expense = 10;
                State(wallet, balance, income, expense);
                foreach (var i in new[] { a, b, c, d })
                {
                    var ticket = tickets[i];
                    var commit = (mask & (1 << i)) != 0;
                    Require(commit ? ticket.TryCommit() : ticket.TryCancel(), "First terminal action");
                    if (ticket.IsIncome) income -= ticket.Amount;
                    else expense -= ticket.Amount;
                    if (commit) balance += ticket.IsIncome ? ticket.Amount : -ticket.Amount;
                    State(wallet, balance, income, expense);
                    Require(!ticket.TryCommit() && !ticket.TryCancel(), "Repeated and opposite terminal actions rejected");
                    State(wallet, balance, income, expense);
                }
            }
        }
    }

    private static void VerifyNotifications()
    {
        var wallet = new CurrencyWalletApplication(10);
        Require(wallet.TryReserveExpense(10, out var expense), "Reentry expense");
        var seen = new List<long>();
        var observerErrors = new List<Exception>();
        wallet.Changed += snapshot =>
        {
            try
            {
                Require(!expense.TryCommit() && !expense.TryCancel(), "Terminal state is visible before notification");
                if (snapshot.Balance == 0 && snapshot.ReservedIncome == 0)
                {
                    State(wallet, 0, 0, 0);
                    Require(wallet.TryReserveIncome(5, out var income) && income.TryCommit(), "Nested reservation and commit");
                }
            }
            catch (Exception exception) { observerErrors.Add(exception); }
        };
        wallet.Changed += snapshot => seen.Add(snapshot.Balance);
        wallet.Changed += _ => throw new InvalidOperationException("Expected observer failure");
        var afterFailure = 0;
        wallet.Changed += _ => afterFailure++;
        Require(expense.TryCommit(), "Outer commit survives observer failure");
        State(wallet, 5, 0, 0);
        Require(observerErrors.Count == 0, "Reentrant observer assertions passed");
        Require(seen.Count == 3 && seen[0] == 0 && seen[1] == 0 && seen[2] == 5 && afterFailure == 3,
            "Observers receive queued snapshots in change order");
        Require(wallet.LastNotificationException is InvalidOperationException, "Observer failure retained for diagnostics");
        var cancellationWallet = new CurrencyWalletApplication(1);
        Require(cancellationWallet.TryReserveIncome(1, out var cancel), "Reentry cancellation");
        cancellationWallet.Changed += _ => Require(!cancel.TryCancel() && !cancel.TryCommit(), "Cancellation reentry rejected");
        Require(cancel.TryCancel(), "Cancellation succeeds once");
        State(cancellationWallet, 1, 0, 0);
        Require(cancellationWallet.LastNotificationException == null, "Cancellation observer assertions passed");
    }

    private static void State(CurrencyWalletApplication wallet, long balance, long income, long expense)
    {
        var state = wallet.Current;
        Require(state.Balance == balance && state.ReservedIncome == income && state.ReservedExpense == expense &&
                state.AvailableBalance == balance - expense, "Wallet state matches model");
    }

    private static void Set(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static void Throws(Action action)
    {
        try { action(); }
        catch (ArgumentException) { _checks++; return; }
        catch (InvalidOperationException) { _checks++; return; }
        throw new InvalidOperationException("Expected invalid configuration rejection");
    }

    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
