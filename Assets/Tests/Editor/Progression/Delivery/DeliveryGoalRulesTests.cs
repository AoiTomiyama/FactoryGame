using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture, Category("Delivery")]
public sealed class DeliveryGoalRulesTests
{
    private const ResourceTypeDomain Wood = ResourceTypeDomain.Wood, Stone = ResourceTypeDomain.Stone;
    private static DeliveryGoalCatalogDomain.Requirement Need(ResourceTypeDomain type, long count) => new(type, count);
    private static DeliveryGoalCatalogDomain.Stage Stage(string id, params DeliveryGoalCatalogDomain.Requirement[] requirements) => new(id, id, requirements);
    private static DeliveryGoalCatalogDomain Catalog() => new(new[] { Stage("first", Need(Wood, 2), Need(Stone, 2)), Stage("last", Need(Wood, 4)) });
    private static void Deliver(DeliveryProgressApplication app, ResourceTypeDomain type, long amount)
    { Assert.That(app.TryReserve(type, amount, out var ticket), Is.True); Assert.That(ticket.TryCommit(), Is.True); }

    [Test, Description("編集元の配列を変更しても目標定義を変えない")]
    public void CatalogCopiesEditableInputs()
    {
        var requirements = new[] { Need(Wood, 2) }; var stage = new DeliveryGoalCatalogDomain.Stage("first", "基礎投入", requirements);
        var stages = new[] { stage }; var catalog = new DeliveryGoalCatalogDomain(stages);
        requirements[0] = Need(Stone, 9); stages[0] = Stage("changed", Need(Stone, 9));
        Assert.That(catalog.Stages[0], Is.SameAs(stage)); Assert.That(stage.Requirements[0].Resource, Is.EqualTo(Wood));
    }

    [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6)]
    [Description("空の条件、重複、累計目標の減少を開始前に検出する")]
    public void InvalidStageDefinitionsAreRejected(int invalidCase)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = invalidCase switch
            {
                0 => new DeliveryGoalCatalogDomain(Array.Empty<DeliveryGoalCatalogDomain.Stage>()),
                1 => new DeliveryGoalCatalogDomain(new[] { Stage("x", Need(Wood, 1)), Stage("x", Need(Wood, 2)) }),
                2 => new DeliveryGoalCatalogDomain(new[] { Stage("x", Need(Wood, 2)), Stage("y", Need(Wood, 1)) }),
                3 => new DeliveryGoalCatalogDomain(new[] { Stage("x", Need(Wood, 1), Need(Wood, 2)) }),
                4 => new DeliveryGoalCatalogDomain(new[] { Stage("", Need(Wood, 1)) }),
                5 => new DeliveryGoalCatalogDomain(new[] { new DeliveryGoalCatalogDomain.Stage("x", "", new[] { Need(Wood, 1) }) }),
                _ => new DeliveryGoalCatalogDomain(new[] { Stage("x") })
            };
        });
    }

    [TestCase(long.MinValue), TestCase(-1L), TestCase(0L)]
    public void NonPositiveGoalCountsAreRejected(long amount) => Assert.Throws<ArgumentException>(() => Need(Wood, amount));
    [TestCase(ResourceTypeDomain.None), TestCase((ResourceTypeDomain)999)]
    public void InvalidGoalResourcesAreRejected(ResourceTypeDomain type) => Assert.Throws<ArgumentException>(() => Need(type, 1));
    [TestCase(long.MinValue), TestCase(-1L), TestCase(0L)]
    public void NonPositiveDeliveriesAreRejected(long amount) => Assert.That(new DeliveryProgressApplication(Catalog()).TryReserve(Wood, amount, out _), Is.False);
    [TestCase(ResourceTypeDomain.None), TestCase((ResourceTypeDomain)999)]
    public void InvalidDeliveryResourcesAreRejected(ResourceTypeDomain type) => Assert.That(new DeliveryProgressApplication(Catalog()).TryReserve(type, 1, out _), Is.False);

    [Test]
    public void CancellationDoesNotChangeDeliveredCountsOrNotify()
    {
        var app = new DeliveryProgressApplication(Catalog()); var notifications = 0; app.Changed += _ => notifications++;
        Assert.That(app.TryReserve(Wood, 5, out var wood), Is.True); Assert.That(app.TryReserve(Stone, 2, out var stone), Is.True);
        Assert.That(stone.TryCancel(), Is.True); Assert.That(stone.TryCommit(), Is.False);
        Assert.That(app.Current.Count(Stone), Is.Zero); Assert.That(notifications, Is.Zero);
        Assert.That(wood.TryCommit(), Is.True); Assert.That(wood.TryCommit(), Is.False); Assert.That(wood.TryCancel(), Is.False);
        Assert.That(app.Current.Count(Wood), Is.EqualTo(5)); Assert.That(notifications, Is.EqualTo(1));
    }

    [Test, Description("全品目が揃うと超過分を保持して複数段階を一度に達成する")]
    public void FullDeliveryAdvancesEverySatisfiedStageOnce()
    {
        var app = new DeliveryProgressApplication(Catalog()); var events = new List<string>(); var final = 0;
        app.Changed += s => events.Add("count:" + s.CompletedStages); app.StageCompleted += s => events.Add(s.Id); app.Completed += () => final++;
        Deliver(app, Wood, 5); Assert.That(app.Current.CompletedStages, Is.Zero); Deliver(app, Stone, 2);
        Assert.That(app.Current.IsComplete, Is.True); Assert.That(app.Current.Count(Wood), Is.EqualTo(5)); Assert.That(final, Is.EqualTo(1));
        CollectionAssert.AreEqual(new[] { "count:0", "count:2", "first", "last" }, events);
    }

    [Test]
    public void LaterStageResourcesDeliveredEarlyAreRetained()
    {
        var app = new DeliveryProgressApplication(new DeliveryGoalCatalogDomain(new[] { Stage("first", Need(Wood, 2)), Stage("last", Need(Stone, 3)) }));
        Deliver(app, Stone, 3); Assert.That(app.Current.CompletedStages, Is.Zero); Deliver(app, Wood, 2);
        Assert.That(app.Current.IsComplete, Is.True); Assert.That(app.Current.Count(Stone), Is.EqualTo(3));
    }

    [Test]
    public void SnapshotsStayUnchangedAndNewPlayStartsEmpty()
    {
        var app = new DeliveryProgressApplication(Catalog()); Deliver(app, Wood, 5); var old = app.Current; Deliver(app, Stone, 2);
        Assert.That(old.Count(Stone), Is.Zero); Assert.That(old.IsComplete, Is.False);
        Assert.That(new DeliveryProgressApplication(app.Catalog).Current.Count(Wood), Is.Zero);
    }

    [Test]
    public void AbsorptionContinuesAfterFinalCompletion()
    {
        var app = new DeliveryProgressApplication(Catalog()); var final = 0; app.Completed += () => final++;
        Deliver(app, Wood, 5); Deliver(app, Stone, 2); Deliver(app, Wood, 1); Deliver(app, ResourceTypeDomain.Gold, 1);
        Assert.That(app.Current.Count(Wood), Is.EqualTo(6)); Assert.That(app.Current.Count(ResourceTypeDomain.Gold), Is.EqualTo(1)); Assert.That(final, Is.EqualTo(1));
    }

    [Test]
    public void PendingReservationsProtectLongUpperBoundAndCancellationFreesSpace()
    {
        var app = new DeliveryProgressApplication(Catalog());
        Assert.That(app.TryReserve(Wood, long.MaxValue - 2, out var huge), Is.True); Assert.That(app.TryReserve(Wood, 2, out var two), Is.True);
        Assert.That(app.TryReserve(Wood, 1, out _), Is.False); Assert.That(two.TryCancel(), Is.True);
        Assert.That(app.TryReserve(Wood, 2, out two), Is.True); Assert.That(huge.TryCommit(), Is.True); Assert.That(two.TryCommit(), Is.True);
        Assert.That(app.Current.Count(Wood), Is.EqualTo(long.MaxValue)); Assert.That(app.TryReserve(Wood, 1, out _), Is.False);
    }

    [Test]
    public void ReentrantNotificationsStayOrderedDespiteObserverExceptions()
    {
        var app = new DeliveryProgressApplication(new DeliveryGoalCatalogDomain(new[] { Stage("a", Need(Wood, 1)), Stage("b", Need(Wood, 2)) }));
        var order = new List<string>(); var final = 0;
        app.Changed += s => { order.Add("count:" + s.CompletedStages); if (s.Count(Wood) == 1) Deliver(app, Wood, 1); };
        app.Changed += _ => throw new InvalidOperationException("expected observer failure");
        app.StageCompleted += s => order.Add(s.Id); app.Completed += () => { final++; Deliver(app, Wood, 1); };
        Deliver(app, Wood, 1);
        CollectionAssert.AreEqual(new[] { "count:1", "a", "count:2", "b", "count:2" }, order);
        Assert.That(final, Is.EqualTo(1)); Assert.That(app.LastNotificationException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void AbsorptionPortsRequireExactQuantityAndTypeAndDisconnectPreservesCommittedCounts()
    {
        var app = new DeliveryProgressApplication(Catalog()); var intake = new AbsorptionIntakeApplication(new[] { 1, 2 }, app) { Accepting = true };
        Assert.That(intake.TryReserve(1, 2, Wood), Is.True); Assert.That(intake.TryReserve(2, 2, Stone), Is.True);
        Assert.That(intake.Commit(1, 1), Is.False); Assert.That(intake.Cancel(1, 2, Stone), Is.False);
        Assert.That(intake.Commit(1, 2), Is.True); Assert.That(intake.Commit(1, 2), Is.False); intake.CancelAll();
        Assert.That(intake.Commit(2, 2), Is.False); Assert.That(app.Current.Count(Wood), Is.EqualTo(2)); Assert.That(app.Current.Count(Stone), Is.Zero);
    }
}
