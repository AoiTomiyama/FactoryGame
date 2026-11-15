#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;

[TestFixture, Category("TerrainClearing")]
public sealed class TerrainClearingTests
{
    private static TerrainClearingRulesDomain.Quote Quote()
    {
        Assert.That(new TerrainClearingRulesDomain(20, 40).TryQuote(new[] {
            new TerrainClearingRulesDomain.Target(1, 2, ResourceTypeDomain.Wood),
            new TerrainClearingRulesDomain.Target(2, 2, ResourceTypeDomain.Stone) }, out var quote));
        return quote;
    }

    [Test] public void SavedPricesQuoteDistinctWoodAndStoneAndKeepMineralsOutsideTheTargets()
    {
        var rules = AssetDatabase.LoadAssetAtPath<CurrencyEconomyDefinition>("Assets/Scripts/ScriptableObject/CurrencyEconomy.asset").CreateClearingRules();
        Assert.That(rules.TryQuote(new[] {
            new TerrainClearingRulesDomain.Target(1, 1, ResourceTypeDomain.Wood),
            new TerrainClearingRulesDomain.Target(1, 1, ResourceTypeDomain.Wood),
            new TerrainClearingRulesDomain.Target(2, 1, ResourceTypeDomain.Stone),
            new TerrainClearingRulesDomain.Target(3, 1, ResourceTypeDomain.Iron),
            new TerrainClearingRulesDomain.Target(4, 1, ResourceTypeDomain.Copper),
            new TerrainClearingRulesDomain.Target(5, 1, ResourceTypeDomain.Coal) }, out var quote));
        Assert.That(quote.Cost, Is.EqualTo(60)); Assert.That(quote.Targets.Count, Is.EqualTo(2));
        Assert.That(rules.TryQuote(new[] { new TerrainClearingRulesDomain.Target(0, 0, ResourceTypeDomain.Iron) }, out _), Is.False);
    }

    [Test] public void ConflictingCoordinatesInvalidPricesAndOverflowDoNotProduceAQuote()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainClearingRulesDomain(0, 40));
        var rules = new TerrainClearingRulesDomain(long.MaxValue, 40);
        Assert.That(rules.TryQuote(new[] {
            new TerrainClearingRulesDomain.Target(0, 0, ResourceTypeDomain.Wood),
            new TerrainClearingRulesDomain.Target(1, 0, ResourceTypeDomain.Stone) }, out _), Is.False);
        Assert.That(rules.TryQuote(new[] {
            new TerrainClearingRulesDomain.Target(0, 0, ResourceTypeDomain.Wood),
            new TerrainClearingRulesDomain.Target(0, 0, ResourceTypeDomain.Stone) }, out _), Is.False);
    }

    [TestCase(false, 60, TerrainClearingApplication.Result.Locked)]
    [TestCase(true, 59, TerrainClearingApplication.Result.InsufficientFunds)]
    public void LockedOrInsufficientSelectionDoesNotInstallAnything(bool unlocked, long balance, TerrainClearingApplication.Result expected)
    {
        var wallet = new CurrencyWalletApplication(balance);
        var operation = new TerrainClearingApplication(wallet, () => unlocked);
        Assert.That(operation.Execute(Quote(), () => true, () => Assert.Fail("install"), () => Assert.Fail("rollback"), () => Assert.Fail("complete")), Is.EqualTo(expected));
        Assert.That(wallet.Current.Balance, Is.EqualTo(balance)); Assert.That(wallet.Current.ReservedExpense, Is.Zero);
    }

    [Test] public void ChangedTargetDuringReservationCancelsTheWholeExpenseAndRejectsReentry()
    {
        var wallet = new CurrencyWalletApplication(60);
        var valid = true; var quote = Quote(); var reentry = TerrainClearingApplication.Result.Success;
        var operation = new TerrainClearingApplication(wallet, () => true);
        wallet.Changed += state =>
        {
            if (state.ReservedExpense == 0) return;
            valid = false;
            reentry = operation.Execute(quote, () => true, () => {}, () => {}, () => {});
        };
        Assert.That(operation.Execute(quote, () => valid, () => Assert.Fail("install"), () => {}, () => {}), Is.EqualTo(TerrainClearingApplication.Result.InvalidTarget));
        Assert.That(wallet.Current.Balance, Is.EqualTo(60)); Assert.That(wallet.Current.ReservedExpense, Is.Zero);
        Assert.That(reentry, Is.EqualTo(TerrainClearingApplication.Result.Busy));
    }

    [Test] public void FailedInstallationRollsBackTheWorldAndRetainsTheOriginalError()
    {
        var wallet = new CurrencyWalletApplication(60); var installed = 0;
        var error = new InvalidOperationException("clearing registration failed");
        var operation = new TerrainClearingApplication(wallet, () => true);
        Assert.That(operation.Execute(Quote(), () => true, () => { installed = 1; throw error; },
            () => installed = 0, () => Assert.Fail("complete")), Is.EqualTo(TerrainClearingApplication.Result.Failed));
        Assert.That(installed, Is.Zero); Assert.That(operation.LastException, Is.SameAs(error));
        Assert.That(wallet.Current.Balance, Is.EqualTo(60)); Assert.That(wallet.Current.ReservedExpense, Is.Zero);
    }

    [Test] public void ExactPaymentCommitsTheWholeSelectionAndObserversReadTheCompletedWorld()
    {
        var wallet = new CurrencyWalletApplication(60); var installed = false; var retired = false;
        var operation = new TerrainClearingApplication(wallet, () => true);
        var observerSawCompleted = false;
        wallet.Changed += state => { if (state.Balance == 0) observerSawCompleted = installed && retired; };
        Assert.That(operation.Execute(Quote(), () => true, () => installed = true, () => installed = false, () => retired = true), Is.EqualTo(TerrainClearingApplication.Result.Success));
        Assert.That(wallet.Current.Balance, Is.Zero); Assert.That(wallet.Current.ReservedExpense, Is.Zero);
        Assert.That(observerSawCompleted, Is.True);
    }

    [Test] public void ClearingSkillRequiresBasicResearchAndCompletedProcessedDeliveriesAndRestoresWithoutPayment()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>("Assets/Scripts/ScriptableObject/SkillTree.asset").CreateCatalog();
        var goals = AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>("Assets/Scripts/ScriptableObject/DeliveryGoals.asset").CreateCatalog();
        var deliveries = new DeliveryProgressApplication(goals);
        var wallet = new CurrencyWalletApplication(550);
        var skills = new SkillTreeApplication(catalog, wallet, new SharedStatEffectsApplication(), deliveries);
        var uid = TerrainClearingApplication.SkillUid;
        Assert.That(skills.Purchase(uid), Is.EqualTo(SkillPurchaseResultApplication.PrerequisiteMissing));
        Assert.That(skills.Purchase("basic-research"), Is.EqualTo(SkillPurchaseResultApplication.Success));
        Assert.That(skills.Purchase(uid), Is.EqualTo(SkillPurchaseResultApplication.DeliveryStageMissing));
        foreach (var pair in new[] { (ResourceTypeDomain.Wood,40L), (ResourceTypeDomain.Stone,40L),
            (ResourceTypeDomain.Plank,30L), (ResourceTypeDomain.StoneBrick,19L) })
        { Assert.That(deliveries.TryReserve(pair.Item1,pair.Item2,out var ticket)); ticket.TryCommit(); }
        Assert.That(skills.CanPurchase(uid), Is.EqualTo(SkillPurchaseResultApplication.DeliveryStageMissing));
        Assert.That(deliveries.TryReserve(ResourceTypeDomain.StoneBrick,1,out var last)); last.TryCommit();
        Assert.That(skills.Purchase(uid), Is.EqualTo(SkillPurchaseResultApplication.Success));
        Assert.That(skills.Purchase(uid), Is.EqualTo(SkillPurchaseResultApplication.AlreadyOwned));
        Assert.That(wallet.Current.Balance, Is.Zero);
        var restored = new SkillTreeApplication(catalog, wallet, new SharedStatEffectsApplication(), deliveries);
        restored.RestoreOwned(skills.CaptureOwned());
        Assert.That(restored.IsOwned(uid)); Assert.That(wallet.Current.Balance, Is.Zero);
        var invalid = new SkillTreeApplication(catalog, new CurrencyWalletApplication(500), new SharedStatEffectsApplication(),new DeliveryProgressApplication(goals));
        Assert.Throws<ArgumentException>(() => invalid.RestoreOwned(skills.CaptureOwned()));
    }

    [Test] public void UnknownDeliveryPrerequisiteFailsConfigurationInsteadOfLeavingALockedNode()
    {
        var node = new SkillNodeDomain("clearing", "Clearing", 500, null, requiredDeliveryStage:"missing");
        var catalog = new SkillTreeCatalogDomain(new[] { node });
        var goals = AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>("Assets/Scripts/ScriptableObject/DeliveryGoals.asset").CreateCatalog();
        Assert.Throws<ArgumentException>(() => new SkillTreeApplication(catalog,new CurrencyWalletApplication(500),
            new SharedStatEffectsApplication(),new DeliveryProgressApplication(goals)));
    }
}
#endif
