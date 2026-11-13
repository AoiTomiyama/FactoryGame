using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture, Category("Persistence")]
public sealed class SaveGameTests
{
    private static SaveGameApplication Data() => new()
    {
        formatVersion = 1, revision = 1, savedUtc = DateTime.UtcNow.ToString("O"),size = 1, seed = int.MinValue, balance = long.MaxValue,
        construction = Enum.GetValues(typeof(ResourceTypeDomain)).Cast<ResourceTypeDomain>().Where(t => t != ResourceTypeDomain.None)
            .Select(t => new SaveGameApplication.Stock {resource = SaveKeysApplication.Key(t),amount = long.MaxValue}).ToArray(),
        deliveries = Array.Empty<SaveGameApplication.Stock>(), skills = Array.Empty<string>(),skillRules = Array.Empty<string>(),
        definitions = new[] {new SaveGameApplication.Definition {kind = "empty",layout = "1,1,0,0|"}},
        cells = new[] {new SaveGameApplication.Cell {kind = "empty",inventory = Array.Empty<SaveGameApplication.Slot>(),batches = Array.Empty<SaveGameApplication.Slot>()}}
    };
    [Test] public void FormatRoundTripPreservesAllResourcesAndLargeCounts()
    {
        var data = Data(); var json = SaveJsonAdapter.Encode(data); var restored = SaveJsonAdapter.Decode(json);
        Assert.That(restored.balance,Is.EqualTo(long.MaxValue)); Assert.That(restored.seed,Is.EqualTo(int.MinValue));
        Assert.That(JsonUtility.ToJson(restored),Is.EqualTo(JsonUtility.ToJson(data)));
        foreach (CellTypeDomain cell in Enum.GetValues(typeof(CellTypeDomain)))
            if (cell != CellTypeDomain.None) Assert.That(SaveKeysApplication.Cell(SaveKeysApplication.Key(cell)),Is.EqualTo(cell));
    }
    [Test] public void TruncationCorruptionAndUnknownFormatAreRejected()
    {
        var json = SaveJsonAdapter.Encode(Data());
        Assert.Throws<ArgumentException>(() => SaveJsonAdapter.Decode(json.Substring(0,json.Length/2)));
        Assert.Throws<InvalidOperationException>(() => SaveJsonAdapter.Decode(json.Replace("empty","unregistered")));
        var data = Data(); data.formatVersion = 2;
        Assert.Throws<InvalidOperationException>(() => SaveJsonAdapter.Encode(data));
        data = Data(); data.cells[0].kind = "unregistered";
        Assert.Throws<InvalidOperationException>(() => data.ValidateShape());
    }
    [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
    public void InvalidProductionTimeIsRejected(float elapsed)
    {
        var data = Data(); data.cells[0].job = new SaveGameApplication.Job
            {seconds = 10,elapsed = elapsed,amount = 1,result = "wood",inputs = Array.Empty<SaveGameApplication.Slot>()};
        Assert.Throws<InvalidOperationException>(() => data.ValidateShape());
    }
    [Test] public void AtomicReplacementPreservesPreviousFileOnFailureAndUsesOneSlot()
    {
        var directory = Path.Combine(Path.GetTempPath(),"FactorySaveTests",Guid.NewGuid().ToString("N"));
        var file = new SaveFileApplication(Path.Combine(directory,"factory.json"));
        try
        {
            var first = SaveJsonAdapter.Encode(Data()); file.Write(first);
            using (var locked = new FileStream(file.Path,FileMode.Open,FileAccess.Read,FileShare.Read))
                Assert.Throws<IOException>(() => file.Write("replacement"));
            Assert.That(file.Read(),Is.EqualTo(first));
            var data = Data(); data.balance = 5; data.revision = 2;
            file.Write(SaveJsonAdapter.Encode(data));
            Assert.That(SaveJsonAdapter.Decode(file.Read()).balance,Is.EqualTo(5));
            Assert.That(Directory.GetFiles(directory).Select(Path.GetFileName),Is.EquivalentTo(new[] {"factory.json"}));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
    [Test] public void RestoreRebuildsSkillsAndDeliveryWithoutSpendingOrNotifications()
    {
        var catalog = new SkillTreeCatalogDomain(new[] {new SkillNodeDomain("root","Root",1,null),
            new SkillNodeDomain("capacity","Capacity",10,"root",CellTypeDomain.Storage,EquipmentStatDomain.Capacity,2,5)});
        var wallet = new CurrencyWalletApplication(77); var effects = new SharedStatEffectsApplication();
        var tree = new SkillTreeApplication(catalog,wallet,effects); var notifications = 0;
        tree.Changed += () => notifications++; wallet.Changed += _ => notifications++; effects.Changed += () => notifications++;
        tree.RestoreOwned(new[] {"capacity","root"});
        Assert.That(tree.CaptureOwned(),Is.EquivalentTo(new[] {"root","capacity"}));
        Assert.That(wallet.Current.Balance,Is.EqualTo(77));
        Assert.That(EffectiveStatsRulesDomain.Calculate(10,effects.GetModifier(CellTypeDomain.Storage,EquipmentStatDomain.Capacity)),Is.EqualTo(25));
        var deliveries = new DeliveryProgressApplication(new DeliveryGoalCatalogDomain(new[] {new DeliveryGoalCatalogDomain.Stage("s","Stage",
            new[] {new DeliveryGoalCatalogDomain.Requirement(ResourceTypeDomain.Wood,5)})}));
        deliveries.Changed += _ => notifications++; deliveries.Completed += () => notifications++;
        deliveries.RestoreDelivered(new Dictionary<ResourceTypeDomain,long> {{ResourceTypeDomain.Wood,5}});
        Assert.That(deliveries.Current.IsComplete); Assert.That(notifications,Is.Zero);
        var fresh = new SkillTreeApplication(catalog,wallet,new SharedStatEffectsApplication());
        Assert.Throws<ArgumentException>(() => fresh.RestoreOwned(new[] {"capacity"}));
    }
    [Test] public void ConstructionCaptureExcludesReservationsAndRestoresOnlyConfirmedStock()
    {
        var types = new[] {ResourceTypeDomain.MachineParts}; var stock = new ConstructionStockApplication(types);
        stock.TryReserveDeposit(types[0],10,out var deposit); deposit.TryCommit();
        stock.TryReserveDeposit(types[0],3,out var incoming);
        stock.TryReserveExpense(new[] {new ResourceCostDomain(types[0],4)},out var expense);
        var restored = new ConstructionStockApplication(types); restored.RestoreStored(stock.CaptureStored());
        Assert.That(restored.Stored(types[0]),Is.EqualTo(10)); Assert.That(restored.Available(types[0]),Is.EqualTo(10));
        incoming.TryCancel(); expense.TryCancel();
    }
}
