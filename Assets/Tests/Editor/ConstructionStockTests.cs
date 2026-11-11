#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture, Category("Construction")]
public sealed class ConstructionStockTests
{
    private static ConstructionStockApplication Stock() => new(new[] { ResourceTypeDomain.Plank, ResourceTypeDomain.IronIngot, ResourceTypeDomain.MachineParts });
    private static void Deposit(ConstructionStockApplication stock, ResourceTypeDomain type, long amount)
    { Assert.That(stock.TryReserveDeposit(type, amount, out var ticket)); Assert.That(ticket.TryCommit()); }
    private static CurrencyPriceCatalogDomain Prices() => AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset").CreateCatalog();

    [Test] public void DepositsAndConstructionReservationsShareTheSameUpperBound()
    {
        var stock = Stock();
        Assert.That(stock.TryReserveDeposit(ResourceTypeDomain.Plank, long.MaxValue - 1, out var first));
        Assert.That(stock.TryReserveDeposit(ResourceTypeDomain.Plank, 1, out var second));
        Assert.That(stock.TryReserveDeposit(ResourceTypeDomain.Plank, 1, out _), Is.False);
        Assert.That(first.TryCommit()); Assert.That(second.TryCommit()); Assert.That(first.TryCommit(), Is.False);
        Assert.That(stock.Stored(ResourceTypeDomain.Plank), Is.EqualTo(long.MaxValue));
        Assert.That(stock.TryReserveExpense(new[] { new ResourceCostDomain(ResourceTypeDomain.Plank, 2) }, out var spend));
        Assert.That(stock.TryReserveDeposit(ResourceTypeDomain.Plank, 1, out _), Is.False, "取消できる支出を入庫余地に使わない");
        Assert.That(spend.TryCancel()); Assert.That(spend.TryCommit(), Is.False);
        Assert.That(stock.TryReserveDeposit(ResourceTypeDomain.Gold, 1, out _), Is.False);
    }
    [Test] public void AllMaterialsAreReservedTogetherAndFailureRestoresMoneyAndStock()
    {
        var stock = Stock(); Deposit(stock, ResourceTypeDomain.Plank, 2); Deposit(stock, ResourceTypeDomain.IronIngot, 2);
        var wallet = new CurrencyWalletApplication(90); var service = new EquipmentPlacementApplication(wallet, Prices(), stock);
        var restored = false;
        var result = service.Execute(CellTypeDomain.AdvancedCrafter, () => true, () => throw new InvalidOperationException("initialization"),
            () => restored = true, () => Assert.Fail("must roll back"), out _);
        Assert.That(result, Is.EqualTo(EquipmentPlacementApplication.Result.Failed)); Assert.That(restored);
        Assert.That(wallet.Current.Balance, Is.EqualTo(90)); Assert.That(wallet.Current.ReservedExpense, Is.Zero);
        Assert.That(stock.Available(ResourceTypeDomain.Plank), Is.EqualTo(2)); Assert.That(stock.Available(ResourceTypeDomain.IronIngot), Is.EqualTo(2));
        result = service.Execute(CellTypeDomain.AdvancedCrafter, () => true, () => {}, () => {}, () => {}, out _);
        Assert.That(result, Is.EqualTo(EquipmentPlacementApplication.Result.Success)); Assert.That(wallet.Current.Balance, Is.Zero);
        Assert.That(stock.Stored(ResourceTypeDomain.Plank), Is.Zero); Assert.That(stock.Stored(ResourceTypeDomain.IronIngot), Is.Zero);
    }
    [Test] public void MaterialOrCurrencyShortageAndPartialPlacementOnlyChargeSuccessfulUnits()
    {
        var stock = Stock(); Deposit(stock, ResourceTypeDomain.Plank, 4);
        var wallet = new CurrencyWalletApplication(45); var service = new EquipmentPlacementApplication(wallet, Prices(), stock); var installed = 0;
        EquipmentPlacementApplication.Result Place() => service.Execute(CellTypeDomain.Storage, () => true, () => installed++, () => installed--, () => {}, out _);
        Assert.That(Place(), Is.EqualTo(EquipmentPlacementApplication.Result.Success)); Assert.That(Place(), Is.EqualTo(EquipmentPlacementApplication.Result.Success));
        Assert.That(Place(), Is.EqualTo(EquipmentPlacementApplication.Result.InsufficientMaterials)); Assert.That(installed, Is.EqualTo(2));
        Assert.That(wallet.Current.Balance, Is.EqualTo(15)); Assert.That(stock.Stored(ResourceTypeDomain.Plank), Is.Zero);
        Deposit(stock, ResourceTypeDomain.Plank, 2); Assert.That(wallet.TryReserveExpense(15, out var other));
        Assert.That(Place(), Is.EqualTo(EquipmentPlacementApplication.Result.InsufficientFunds));
        Assert.That(stock.Available(ResourceTypeDomain.Plank), Is.EqualTo(2)); Assert.That(other.TryCancel());
    }
    [Test] public void ReentrantCurrencyNotificationRechecksTargetAndReleasesBothReservations()
    {
        var stock = Stock(); Deposit(stock, ResourceTypeDomain.Plank, 2);
        var wallet = new CurrencyWalletApplication(15); var service = new EquipmentPlacementApplication(wallet, Prices(), stock); var valid = true;
        wallet.Changed += state =>
        {
            if (state.ReservedExpense == 0) return;
            Assert.That(service.Execute(CellTypeDomain.Storage, () => true, () => Assert.Fail(), () => {}, () => {}, out _), Is.EqualTo(EquipmentPlacementApplication.Result.Busy));
            valid = false;
        };
        Assert.That(service.Execute(CellTypeDomain.Storage, () => valid, () => Assert.Fail(), () => {}, () => {}, out _), Is.EqualTo(EquipmentPlacementApplication.Result.InvalidTarget));
        Assert.That(wallet.Current.Balance, Is.EqualTo(15)); Assert.That(stock.Available(ResourceTypeDomain.Plank), Is.EqualTo(2));
    }
    [Test] public void MultipleDepotInputsCancelIndependentlyAndKeepCommittedStock()
    {
        var stock = Stock(); var a = new ConstructionDepotIntakeApplication(stock, Enumerable.Range(0,8)) { Accepting = true };
        var b = new ConstructionDepotIntakeApplication(stock, Enumerable.Range(0,8)) { Accepting = true };
        Assert.That(a.TryReserve(0, 2, ResourceTypeDomain.Plank)); Assert.That(b.TryReserve(0, 3, ResourceTypeDomain.Plank));
        Assert.That(a.Commit(0, 1), Is.False); Assert.That(a.Cancel(0, 2, ResourceTypeDomain.IronIngot), Is.False);
        Assert.That(a.Commit(0, 2)); Assert.That(a.Commit(0, 2), Is.False); b.CancelAll();
        Assert.That(stock.Stored(ResourceTypeDomain.Plank), Is.EqualTo(2));
        var notified = 0; stock.Changed += () => { notified++; throw new Exception("observer"); };
        Assert.That(b.TryReserve(7, 1, ResourceTypeDomain.IronIngot)); Assert.That(b.Commit(7, 1));
        Assert.That(notified, Is.EqualTo(1)); Assert.That(stock.LastNotificationException, Is.Not.Null);
        Assert.That(stock.Stored(ResourceTypeDomain.IronIngot), Is.EqualTo(1));
    }
    [Test] public void SavedCostsAllowBootstrapAndRejectInvalidOrOverflowingMaterials()
    {
        var prices = Prices();
        foreach (var type in new[] { CellTypeDomain.ExtractorWood, CellTypeDomain.ExtractorStone, CellTypeDomain.ExtractorIron,
            CellTypeDomain.ExtractorCopper, CellTypeDomain.ExtractorCoal, CellTypeDomain.Conveyor, CellTypeDomain.ExportConveyor,
            CellTypeDomain.Crafter, CellTypeDomain.ConstructionDepot })
        { Assert.That(prices.TryGetMaterialCosts(type,1,out var costs)); Assert.That(costs, Is.Empty); }
        Assert.That(prices.TryGetMaterialCosts(CellTypeDomain.AdvancedCrafter,1,out var advanced));
        CollectionAssert.AreEquivalent(new[] { ResourceTypeDomain.Plank, ResourceTypeDomain.IronIngot }, advanced.Select(c => c.Resource));
        var units = new[] { new ResourceCostDomain(ResourceTypeDomain.Plank, long.MaxValue) };
        var catalog = new CurrencyPriceCatalogDomain(0, Array.Empty<KeyValuePair<ResourceTypeDomain,long>>(),
            new[] { new KeyValuePair<CellTypeDomain,long>(CellTypeDomain.Storage,15) },
            new[] { new KeyValuePair<CellTypeDomain,ResourceCostDomain[]>(CellTypeDomain.Storage,units) });
        units[0] = new ResourceCostDomain(ResourceTypeDomain.Plank,1);
        Assert.That(catalog.TryGetMaterialCosts(CellTypeDomain.Storage,2,out _), Is.False);
        Assert.That(catalog.TryGetMaterialCosts(CellTypeDomain.Storage,1,out var fixedCost)); Assert.That(fixedCost[0].Amount, Is.EqualTo(long.MaxValue));
        Assert.Throws<ArgumentException>(() => new CurrencyPriceCatalogDomain(0, Array.Empty<KeyValuePair<ResourceTypeDomain,long>>(),
            new[] { new KeyValuePair<CellTypeDomain,long>(CellTypeDomain.Storage,15) },
            new[] { new KeyValuePair<CellTypeDomain,ResourceCostDomain[]>(CellTypeDomain.Storage,new[] { new ResourceCostDomain(ResourceTypeDomain.Plank,1), new ResourceCostDomain(ResourceTypeDomain.Plank,2) }) }));
    }
    [Test] public void DepotAssetSharesItsModelAndUsesEightInputOnlyPorts()
    {
        var cells = AssetDatabase.LoadAssetAtPath<CellDatabaseDefinition>("Assets/Scripts/ScriptableObject/GeneralCellDB.asset");
        Assert.That(cells.TryGetCellInfo(CellTypeDomain.ConstructionDepot,out var info));
        var depot = info.FieldCellPrefab as ConstructionDepotCellAdapter; Assert.That(depot != null);
        Assert.That(depot.Footprint.Width,Is.EqualTo(2)); Assert.That(depot.Footprint.Depth,Is.EqualTo(2));
        Assert.That(depot.Ports.Count,Is.EqualTo(8)); Assert.That(depot.Ports.All(p => p.Use == CellPortUseDomain.Input));
        Assert.That(typeof(IExportableApplication).IsAssignableFrom(depot.GetType()),Is.False);
        var fieldModel=depot.transform.Find("Model").GetChild(0).gameObject;
        var previewModel=info.PlaceholderCellPrefab.transform.Find("Model").GetChild(0).gameObject;
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(fieldModel),Is.EqualTo(PrefabUtility.GetCorrespondingObjectFromSource(previewModel)));
        Assert.That(depot.transform.Find("Model").localScale,Is.EqualTo(new Vector3(2,1,2)));
        Assert.That(info.PlaceholderCellPrefab.transform.Find("Model").localScale,Is.EqualTo(new Vector3(2,1,2)));
    }
}
#endif
