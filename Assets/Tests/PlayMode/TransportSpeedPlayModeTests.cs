#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

[TestFixture,Category("Progression.Speed")]
public sealed class TransportSpeedPlayModeTests : FactoryPlayModeScope
{
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private GridFieldDatabaseAdapter _grid;
    private ResourceItemObjectPoolAdapter _pool;
    private SkillTreeApplication _skills;
    private class Presentation : IResourceTransferPresentationApplication
    {
        public float Seconds;
        public readonly UniTaskCompletionSource<ResourceAnimationResultApplication> Completion = new();
        public UniTask<ResourceAnimationResultApplication> Transfer(CancellationToken token,Vector3 from,Vector3 to,int id,float seconds)
        { Seconds=seconds; return Completion.Task; }
        public void SetPosition(int id,Vector3 position) { }
        public bool ReleaseVisual(int id) => true;
    }
    [UnitySetUp] public IEnumerator PrepareWorld()
    {
        _pool=new GameObject("TransportSpeedPool").AddComponent<ResourceItemObjectPoolAdapter>();
        typeof(ResourceItemObjectPoolAdapter).GetField("resourceDatabase",Fields).SetValue(_pool,
            AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset"));
        typeof(ResourceItemObjectPoolAdapter).GetField("transferSecond",Fields).SetValue(_pool,1f);
        _grid=new GameObject("TransportSpeedGrid").AddComponent<GridFieldDatabaseAdapter>(); _grid.InitializeCells(12);
        var catalog=AssetDatabase.LoadAssetAtPath<SkillTreeDefinition>("Assets/Scripts/ScriptableObject/SkillTree.asset").CreateCatalog();
        var deliveries=new DeliveryProgressApplication(AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>("Assets/Scripts/ScriptableObject/DeliveryGoals.asset").CreateCatalog());
        _skills=new SkillTreeApplication(catalog,new CurrencyWalletApplication(10000),_grid.StatEffects,deliveries);
        Assert.That(_skills.Purchase("basic-research"),Is.EqualTo(SkillPurchaseResultApplication.Success));
        yield return null;
    }
    private T Add<T>(string name,int x,int z) where T:CellBaseAdapter
    {
        var prefab=AssetDatabase.LoadAssetAtPath<T>("Assets/Prefabs/Field/General/F_"+name+"Cell.prefab");
        var cell=Object.Instantiate(prefab,new Vector3(x,0,z),Quaternion.identity,_grid.transform);
        _grid.SaveCell(x,z,cell); cell.InitializeSystem(); return cell;
    }
    private void BuyTopSpeed()
    { for(var tier=1;tier<=3;tier++) Assert.That(_skills.Purchase("transport-speed-"+tier),Is.EqualTo(SkillPurchaseResultApplication.Success)); }

    [UnityTest] public IEnumerator NormalAndCrossingActuallyDeliverFasterAndKeepTheirBatch() => Physical().ToCoroutine();
    private async UniTask Physical()
    {
        BuyTopSpeed();
        var normal=Add<ConveyorCellAdapter>("Conveyor",2,2); var normalSink=Add<StorageCellAdapter>("Storage",2,3);
        var cross=Add<CrossingCellAdapter>("Crossing",6,2); var crossSink=Add<StorageCellAdapter>("Storage",6,3);
        var normalInput=normal.Ports.First(p => normal.PortDirection(p.Id)==Vector3Int.back).Id;
        var crossInput=cross.Ports.First(p => cross.PortDirection(p.Id)==Vector3Int.back).Id;
        var a=_pool.CreateIdFromResourceData(ResourceTypeDomain.Wood,3); var b=_pool.CreateIdFromResourceData(ResourceTypeDomain.Stone,4);
        Assert.That(normal.AllocateStorage(normalInput,3,ResourceTypeDomain.Wood)); normal.Reuse(normalInput,a); normal.StoreResource(normalInput,3);
        Assert.That(cross.AllocateStorage(crossInput,4,ResourceTypeDomain.Stone)); cross.Reuse(crossInput,b); cross.StoreResource(crossInput,4);
        var start=Time.time;
        await UniTask.WaitUntil(() => normalSink.CurrentLoad==3 && crossSink.CurrentLoad==4).Timeout(TimeSpan.FromSeconds(4));
        Assert.That(Time.time-start,Is.LessThan(.85f),"base 1 second moves complete near 0.5 seconds at double speed");
        Assert.That(normalSink.CurrentLoad+crossSink.CurrentLoad,Is.EqualTo(7));
    }

    [UnityTest] public IEnumerator PromotionKeepsSharedSpeedAndRegistersTheExporterQuantityBase() => Promotion().ToCoroutine();
    private async UniTask Promotion()
    {
        BuyTopSpeed();
        var normal=Add<ConveyorCellAdapter>("Conveyor",3,3);
        var definition=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExportConveyorCellAdapter>("Assets/Prefabs/Field/General/F_ExportConveyorCell.prefab"));
        definition.gameObject.SetActive(false);
        typeof(ConveyorCellAdapter).GetField("transferAmount",Fields).SetValue(definition,2);
        typeof(ConveyorCellAdapter).GetField("exportDefinition",Fields).SetValue(normal,definition);
        var source=Add<StorageCellAdapter>("Storage",3,2); var sink=Add<StorageCellAdapter>("Storage",3,4);
        await UniTask.WaitUntil(() => normal.IsExportConveyor).Timeout(TimeSpan.FromSeconds(3));
        Assert.That(_grid.StatEffects.CanApply(CellTypeDomain.ExportConveyor,EquipmentStatDomain.TransferAmount,new StatModifierDomain(int.MaxValue,0)),Is.False,
            "promotion must register the exporter base even though shared effects are already assigned");
        var port=source.Ports.First(p => (p.Use&CellPortUseDomain.Input)!=0).Id;
        Assert.That(source.AllocateStorage(port,2,ResourceTypeDomain.Wood)); source.StoreResource(port,2);
        var start=Time.time;
        await UniTask.WaitUntil(() => sink.CurrentLoad==2).Timeout(TimeSpan.FromSeconds(4));
        Assert.That(Time.time-start,Is.LessThan(1.65f),"both extraction and forwarding use double speed after promotion");
        Assert.That(source.CurrentLoad,Is.Zero); Object.Destroy(definition.gameObject);
    }

    [UnityTest] public IEnumerator WaitingUsesTheNewSpeedButAnAnimatingMoveKeepsItsCapturedTime() => SnapshotTime().ToCoroutine();
    private async UniTask SnapshotTime()
    {
        var source=Add<StorageCellAdapter>("Storage",1,1); var target=Add<StorageCellAdapter>("Storage",2,1);
        var port=target.Ports.First(p => (p.Use&CellPortUseDomain.Input)!=0).Id;
        var id=_pool.CreateIdFromResourceData(ResourceTypeDomain.Wood,2);
        var operation=new ResourceTransferOperationApplication(source,id,ResourceTypeDomain.Wood,2);
        var coordinator=new ResourceTransferCoordinatorApplication(operation); var presentation=new Presentation();
        IContainableApplication recipient=null; var reads=0; var multiplier=1f;
        var move=coordinator.RunAsync(() => port,Vector3.zero,Vector3.right,() => recipient,
            endpoint => ReferenceEquals(endpoint,target),_pool.Resources,presentation,() => { reads++;return 1/multiplier; },CancellationToken.None);
        await UniTask.DelayFrame(2); Assert.That(reads,Is.Zero);
        multiplier=2; recipient=target;
        coordinator.WakeForTargetChange();
        await UniTask.WaitUntil(() => presentation.Seconds>0).Timeout(TimeSpan.FromSeconds(3));
        Assert.That(presentation.Seconds,Is.EqualTo(.5f)); multiplier=4;
        await UniTask.DelayFrame(2); Assert.That(reads,Is.EqualTo(1)); Assert.That(presentation.Seconds,Is.EqualTo(.5f));
        presentation.Completion.TrySetResult(ResourceAnimationResultApplication.Completed);
        Assert.That(await move,Is.EqualTo(ResourceAnimationResultApplication.Completed)); Assert.That(target.CurrentLoad,Is.EqualTo(2));
    }
}
#endif
