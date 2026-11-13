#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>開始メニュー・HUD・カメラを作らず、特異点の受入と共有状態を直接接続する。</summary>
[TestFixture, Category("Delivery")]
public sealed class DeliveryCorePlayModeTests : FactoryPlayModeScope
{
    private SingularityCellAdapter _core;
    private ShippingCellAdapter _shipping;
    private DeliveryProgressApplication _deliveries;
    private CurrencyWalletApplication _wallet;

    [UnitySetUp] public IEnumerator ConfigureDeliveryWithoutUi()
    {
        var definition = AssetDatabase.LoadAssetAtPath<SingularityStartDefinition>("Assets/Scripts/ScriptableObject/SingularityStart.asset");
        var prices = definition.CreateCatalog();
        _wallet = new CurrencyWalletApplication(prices.InitialBalance);
        _deliveries = new DeliveryProgressApplication(definition.CreateDeliveryCatalog());
        var field = TestScenes.NewObject("DeliveryTestField").AddComponent<GridFieldDatabaseAdapter>();
        _core = TestScenes.Clone(definition.SingularityPrefab,new Vector3(5,0,5),Quaternion.identity,field.transform);
        _shipping = TestScenes.Clone(definition.ShippingPrefab,new Vector3(5,0,2),Quaternion.identity,field.transform);
        field.InitializeCells(12);
        _core.Configure(_deliveries); _shipping.Configure(_wallet,prices);
        _core.InitializeSystem(); _shipping.InitializeSystem();
        Assert.That(TestScenes.All<Canvas>(),Is.Empty,"delivery setup has no UI");
        Assert.That(TestScenes.All<GameInitializerAdapter>(),Is.Empty,"delivery setup does not use game startup");
        yield return null;
    }
    private void Receive(ResourceTypeDomain resource,int amount,int port = 0)
    { Assert.That(_core.AllocateStorage(port,amount,resource)); _core.StoreResource(port,amount); }

    [Test] public void DeliveryCommitsSharedProgressWithoutPayingCurrency()
    {
        var balance = _wallet.Current.Balance;
        Assert.That(_core.Deliveries,Is.SameAs(_deliveries));
        Receive(ResourceTypeDomain.Wood,41); Receive(ResourceTypeDomain.Stone,40);
        Assert.That(_deliveries.Current.Count(ResourceTypeDomain.Wood),Is.EqualTo(41));
        Assert.That(_deliveries.Current.CompletedStages,Is.EqualTo(1));
        Assert.That(_wallet.Current.Balance,Is.EqualTo(balance));
    }
    [Test] public void CoreDisableCancelsPendingInputsAndKeepsCommittedDeliveries()
    {
        Receive(ResourceTypeDomain.Wood,2);
        Assert.That(_core.AllocateStorage(1,5,ResourceTypeDomain.Wood));
        _core.gameObject.SetActive(false); _core.gameObject.SetActive(true); _core.StoreResource(1,5);
        Assert.That(_deliveries.Current.Count(ResourceTypeDomain.Wood),Is.EqualTo(2));
        Receive(ResourceTypeDomain.Wood,1); Assert.That(_deliveries.Current.Count(ResourceTypeDomain.Wood),Is.EqualTo(3));
    }
    [Test] public void AbsorptionContinuesAfterCompletionIndependentlyOfShipping()
    {
        var completed = 0; _deliveries.Completed += () => completed++;
        Receive(ResourceTypeDomain.Wood,40); Receive(ResourceTypeDomain.Stone,40);
        Receive(ResourceTypeDomain.Plank,30); Receive(ResourceTypeDomain.StoneBrick,20); Receive(ResourceTypeDomain.BuildingParts,20);
        Assert.That(_deliveries.Current.IsComplete);
        Receive(ResourceTypeDomain.Wood,1);
        Assert.That(_deliveries.Current.Count(ResourceTypeDomain.Wood),Is.EqualTo(41)); Assert.That(completed,Is.EqualTo(1));
        var balance = _wallet.Current.Balance;
        Assert.That(_shipping.AllocateStorage(0,5,ResourceTypeDomain.Wood)); _shipping.StoreResource(0,5);
        Assert.That(_wallet.Current.Balance,Is.EqualTo(balance+5));
        Assert.That(_deliveries.Current.Count(ResourceTypeDomain.Wood),Is.EqualTo(41));
    }
}
#endif
