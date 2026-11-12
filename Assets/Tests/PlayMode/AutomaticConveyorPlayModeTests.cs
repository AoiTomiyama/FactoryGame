#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

[TestFixture,Category("AutomaticConveyor")]
public sealed class AutomaticConveyorPlayModeTests : FactoryPlayModeScope
{
    private ResourceItemObjectPoolAdapter _pool;
    private GridFieldDatabaseAdapter _grid;
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    [UnitySetUp] public IEnumerator CreateWorld()
    {
        _pool=new GameObject("AutomaticConveyorPool").AddComponent<ResourceItemObjectPoolAdapter>();
        typeof(ResourceItemObjectPoolAdapter).GetField("resourceDatabase",Fields).SetValue(_pool,
            AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset"));
        typeof(ResourceItemObjectPoolAdapter).GetField("transferSecond",Fields).SetValue(_pool,.25f);
        _grid=new GameObject("AutomaticConveyorGrid").AddComponent<GridFieldDatabaseAdapter>(); _grid.InitializeCells(16);
        yield return null;
    }
    private T Add<T>(string name,int x,int z) where T:CellBaseAdapter
    {
        var cell=Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>("Assets/Prefabs/Field/General/F_"+name+"Cell.prefab"),new Vector3(x,0,z),Quaternion.identity,_grid.transform);
        _grid.SaveCell(x,z,cell); cell.InitializeSystem(); return cell;
    }
    private static void Fill(StorageCellAdapter cell,ResourceTypeDomain type,int amount)
    { Assert.That(cell.AllocateStorage(0,amount,type));cell.StoreResource(0,amount); }
    private static async UniTask Until(Func<bool> condition)
    {
        var end=Time.realtimeSinceStartup+5;
        while(!condition()&&Time.realtimeSinceStartup<end)await UniTask.Yield();
        Assert.That(condition());
    }
    private static int Id(ConveyorCellAdapter core)=>(int)typeof(ConveyorCellAdapter).GetProperty("ResourceId",Fields).GetValue(core);

    [UnityTest] public IEnumerator SourceThenLineAndLineThenSourceProduceTheSameResult()=>Order().ToCoroutine();
    private async UniTask Order()
    {
        foreach(var lineFirst in new[]{false,true})
        {
            var x=lineFirst?9:4; ConveyorCellAdapter core=null; StorageCellAdapter source=null;
            if(lineFirst)core=Add<ConveyorCellAdapter>("Conveyor",x,4);
            source=Add<StorageCellAdapter>("Storage",x,3);
            if(!lineFirst)core=Add<ConveyorCellAdapter>("Conveyor",x,4);
            var sink=Add<StorageCellAdapter>("Storage",x,5);
            var identity=core.Identity; var model=core.CellModel;
            await Until(()=>core.IsExportConveyor);
            Assert.That(core.ProgressionType,Is.EqualTo(CellTypeDomain.ExportConveyor));
            Assert.That(core.Identity,Is.SameAs(identity));Assert.That(core.CellModel,Is.SameAs(model));Assert.That(_grid.GetCell(x,4),Is.SameAs(core));
            Assert.That(core.transform.Find("Model").GetChild(core.transform.Find("Model").childCount-1).name,Does.Contain("ExportConveyor"));
            Fill(source,ResourceTypeDomain.Stone,5); await Until(()=>sink.CurrentLoad==5);
            Assert.That(source.CurrentLoad,Is.Zero);
        }
    }
    [UnityTest] public IEnumerator PromotionKeepsAnIncomingReservation()=>Reservation().ToCoroutine();
    private async UniTask Reservation()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);
        Assert.That(core.AllocateStorage(0,3,ResourceTypeDomain.Wood));
        var source=Add<StorageCellAdapter>("Storage",4,3); Fill(source,ResourceTypeDomain.Stone,5);
        await Until(()=>core.IsExportConveyor);
        Assert.That(source.CurrentLoad,Is.EqualTo(5));Assert.That(core.AllocateStorage(1,1,ResourceTypeDomain.Stone),Is.False);
        core.CancelStorage(0,3,ResourceTypeDomain.Wood);
        await Until(()=>Id(core)!=0);Assert.That(_pool.Resources.TryGet(Id(core),out var held));Assert.That(held.Amount,Is.EqualTo(5));
    }
    [UnityTest] public IEnumerator PromotionPreservesTheMovingBatchAndForwardReservation()=>Moving().ToCoroutine();
    private async UniTask Moving()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);var sink=Add<StorageCellAdapter>("Storage",4,5);
        Assert.That(core.AllocateStorage(0,3,ResourceTypeDomain.Wood));var id=_pool.CreateIdFromResourceData(ResourceTypeDomain.Wood,3);
        core.Reuse(0,id);core.StoreResource(0,3);
        await Until(()=>typeof(ConveyorCellAdapter).GetField("_activeTransfer",Fields).GetValue(core)!=null);
        var active=typeof(ConveyorCellAdapter).GetField("_activeTransfer",Fields).GetValue(core);var observed=false;
        core.KindChanged+=()=>{Assert.That(Id(core),Is.EqualTo(id));Assert.That(typeof(ConveyorCellAdapter).GetField("_activeTransfer",Fields).GetValue(core),Is.SameAs(active));observed=true;};
        var source=Add<StorageCellAdapter>("Storage",4,3);Fill(source,ResourceTypeDomain.Stone,5);
        await Until(()=>core.IsExportConveyor);Assert.That(observed);
        await Until(()=>sink.CurrentLoad==3&&source.CurrentLoad==0&&Id(core)!=0);
        Assert.That(sink.StoredResourceType,Is.EqualTo(ResourceTypeDomain.Wood));
        Assert.That(_pool.Resources.TryGet(id,out _),Is.False);Assert.That(_pool.Resources.TryGet(Id(core),out var held));Assert.That(held.Type,Is.EqualTo(ResourceTypeDomain.Stone));
    }
    [UnityTest] public IEnumerator SourceRotationAndReconnectionKeepRoleAndSettings()=>Reconnect().ToCoroutine();
    private async UniTask Reconnect()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);var source=Add<CrafterCellAdapter>("Crafter",4,3);
        await Until(()=>core.IsExportConveyor);typeof(ConveyorCellAdapter).GetField("transferAmount",Fields).SetValue(core,7);
        var identity=core.Identity;
        source.transform.rotation=Quaternion.Euler(0,180,0);source.InitializeSystem();await UniTask.DelayFrame(3);
        Assert.That(core.IsExportConveyor);Assert.That(core.TransferAmount,Is.EqualTo(7));Assert.That(core.Identity,Is.SameAs(identity));
        Assert.That(ConveyorCellAdapter.HasExtractionSource(_grid,4,4,Quaternion.identity),Is.False);
        source.transform.rotation=Quaternion.identity;source.InitializeSystem();await UniTask.DelayFrame(3);
        Assert.That(ConveyorCellAdapter.HasExtractionSource(_grid,4,4,Quaternion.identity));Assert.That(core.TransferAmount,Is.EqualTo(7));
        core.transform.rotation=Quaternion.Euler(0,90,0);core.InitializeSystem();await UniTask.DelayFrame(3);
        Assert.That(core.TransferAmount,Is.EqualTo(7));Assert.That(core.Identity,Is.SameAs(identity));
    }
    [UnityTest] public IEnumerator RolledBackSourceDoesNotPromoteAndReconnectionKeepsTheCell()=>RollbackSource().ToCoroutine();
    private async UniTask RollbackSource()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);var identity=core.Identity;
        var source=Add<StorageCellAdapter>("Storage",4,3);
        source.OnDisconnect();source.gameObject.SetActive(false);
        await UniTask.DelayFrame(3);
        Assert.That(core.IsExportConveyor,Is.False);
        source.gameObject.SetActive(true);source.InitializeSystem();
        await Until(()=>core.IsExportConveyor);
        Assert.That(core.Identity,Is.SameAs(identity));
    }
    [UnityTest] public IEnumerator InvalidPromotionVisualKeepsTheOriginalBatchAndRole()=>FailedPromotion().ToCoroutine();
    private async UniTask FailedPromotion()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);
        var bad=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExportConveyorCellAdapter>("Assets/Prefabs/Field/General/F_ExportConveyorCell.prefab"));bad.gameObject.SetActive(false);
        Object.DestroyImmediate(bad.transform.Find("Model").GetChild(0).gameObject);
        typeof(ConveyorCellAdapter).GetField("exportDefinition",Fields).SetValue(core,bad);
        Assert.That(core.AllocateStorage(0,2,ResourceTypeDomain.Wood));var id=_pool.CreateIdFromResourceData(ResourceTypeDomain.Wood,2);core.Reuse(0,id);core.StoreResource(0,2);
        Assert.Throws<TargetInvocationException>(()=>typeof(ConveyorCellAdapter).GetMethod("EnableExportMode",Fields).Invoke(core,null));
        Assert.That(core.IsExportConveyor,Is.False);Assert.That(Id(core),Is.EqualTo(id));Assert.That(_pool.Resources.TryGet(id,out var held));Assert.That(held.Amount,Is.EqualTo(2));
        Object.Destroy(bad.gameObject);await UniTask.Yield();
    }
}

[TestFixture,Category("AutomaticConveyor")]
public sealed class AutomaticConveyorPlacementPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene=>true;
    [UnityTest] public IEnumerator UnifiedToolMatchesPreviewAndChargesOnePrice()=>VerifyPlacement().ToCoroutine();
    private static async UniTask VerifyPlacement()
    {
        var generator=Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();var placer=Object.FindAnyObjectByType<CellPlacerAdapter>();var grid=GridFieldDatabaseAdapter.Instance;
        var panel=Object.FindAnyObjectByType<EconomyPanelAdapter>();panel.ShowPage(0);
        void Place(CellTypeDomain type,int x,int z,float angle=0)
        { placer.transform.rotation=Quaternion.Euler(0,angle,0);placer.SetSelectedCellType(type);placer.SelectGrid(grid.GetCell(x,z).gameObject);placer.PointerBegin();placer.PointerDrag();placer.PointerEnd();Assert.That(placer.LastPlacedCount,Is.EqualTo(1)); }
        Place(CellTypeDomain.ExtractorWood,50,44,180);
        placer.transform.rotation=Quaternion.identity;placer.SetSelectedCellType(CellTypeDomain.Conveyor);placer.SelectGrid(grid.GetCell(50,45).gameObject);
        var preview=(GameObject)typeof(CellPlacerAdapter).GetField("_placeholderCell",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(placer);
        Assert.That(preview.name,Does.Contain("ExportConveyor"));
        placer.PointerBegin();placer.SelectGrid(grid.GetCell(50,46).gameObject);placer.PointerDrag();placer.PointerEnd();
        Assert.That(placer.LastPlacedCount,Is.EqualTo(2));Assert.That(placer.LastPlacementCost,Is.EqualTo(10));Assert.That(generator.Wallet.Current.Balance,Is.EqualTo(70));
        Assert.That(((ConveyorCellAdapter)grid.GetCell(50,45)).IsExportConveyor);Assert.That(((ConveyorCellAdapter)grid.GetCell(50,46)).IsExportConveyor,Is.False);
        Assert.That(placer.SelectedType,Is.EqualTo(CellTypeDomain.Conveyor));
        Assert.That(panel.PlacementButtons.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).Any(b=>b.name=="ExportConveyorButton"),Is.False);
        await UniTask.DelayFrame(2);
    }
}
#endif
