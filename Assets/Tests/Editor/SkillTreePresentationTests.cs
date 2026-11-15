#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;

[TestFixture, Category("Progression")]
public sealed class SkillTreePresentationTests
{
    [Test] public void PrerequisitesAreHiddenAndMoneyOnlyChangesTheAvailableHighlight()
    {
        var goals = new DeliveryGoalCatalogDomain(new[] { new DeliveryGoalCatalogDomain.Stage("processed","Processed",
            new[] { new DeliveryGoalCatalogDomain.Requirement(ResourceTypeDomain.Plank,1) }) });
        var deliveries = new DeliveryProgressApplication(goals);
        var catalog = new SkillTreeCatalogDomain(new[] { new SkillNodeDomain("root","Root",50,null),
            new SkillNodeDomain("next","Next",100,"root"),
            new SkillNodeDomain("gated","Gated",200,"root",requiredDeliveryStage:"processed") });
        var wallet = new CurrencyWalletApplication(49);
        var skills = new SkillTreeApplication(catalog,wallet,new SharedStatEffectsApplication(),deliveries);
        var presentation = new SkillTreePresentationApplication(skills);
        var snapshot = presentation.Capture();
        Assert.That(snapshot.Single(n => n.Node.Uid=="root").Visible);
        Assert.That(snapshot.Single(n => n.Node.Uid=="root").Highlighted,Is.False);
        Assert.That(snapshot.Where(n => n.Visible).Count(),Is.EqualTo(1));
        wallet.TryReserveIncome(1,out var income); income.TryCommit();
        Assert.That(presentation.Capture().Single(n => n.Node.Uid=="root").Highlighted);
        Assert.That(skills.Purchase("root"),Is.EqualTo(SkillPurchaseResultApplication.Success));
        snapshot = presentation.Capture();
        Assert.That(snapshot.Single(n => n.Node.Uid=="root").Owned);
        Assert.That(snapshot.Single(n => n.Node.Uid=="root").Highlighted,Is.False);
        Assert.That(snapshot.Single(n => n.Node.Uid=="next").Visible);
        Assert.That(snapshot.Single(n => n.Node.Uid=="next").Highlighted,Is.False);
        Assert.That(snapshot.Single(n => n.Node.Uid=="gated").Visible,Is.False);
        deliveries.TryReserve(ResourceTypeDomain.Plank,1,out var ticket); ticket.TryCommit();
        Assert.That(presentation.Capture().Single(n => n.Node.Uid=="gated").Visible);
    }
}
#endif
