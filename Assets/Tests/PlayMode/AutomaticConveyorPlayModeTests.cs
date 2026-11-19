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
    private T Add<T>(string name,int x,int z, Action<T> configure = null) where T:CellBaseAdapter
    {
        var cell=Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>("Assets/Prefabs/Field/General/F_"+name+"Cell.prefab"),new Vector3(x,0,z),Quaternion.identity,_grid.transform);
        configure?.Invoke(cell);
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

    private ConveyorJunctionCellAdapter Junction(int x, int z, DirectionsDomain inputs, DirectionsDomain outputs) =>
        Add<ConveyorJunctionCellAdapter>("ConveyorJunction", x, z, cell =>
            typeof(ConveyorJunctionCellAdapter).GetMethod("ConfigureRoute", Fields)
                .Invoke(cell, new object[] { new ConveyorRouteRulesDomain.Route(inputs, outputs), 0 }));

    private int Send(IContainableApplication lane, IResourceReusableApplication receiver, int port,
        ResourceTypeDomain type, int count)
    {
        Assert.That(lane.AllocateStorage(port, count, type));
        var id = _pool.CreateIdFromResourceData(type, count);
        receiver.Reuse(port, id);
        lane.StoreResource(port, count);
        return id;
    }

    [UnityTest] public IEnumerator JunctionSkipsAFullOutputAndResumesWhenSpaceOpens() => Split().ToCoroutine();
    private async UniTask Split()
    {
        var junction = Junction(5, 5, DirectionsDomain.Back, DirectionsDomain.Left | DirectionsDomain.Right | DirectionsDomain.Forward);
        var right = Add<StorageCellAdapter>("Storage", 6, 5);
        var left = Add<StorageCellAdapter>("Storage", 4, 5);
        var ahead = Add<StorageCellAdapter>("Storage", 5, 6);
        Fill(right, ResourceTypeDomain.Wood, right.Capacity);
        var input = junction.Ports.Single(port => junction.HasPort(port.Id, CellPortUseDomain.Input)).Id;
        Send(junction, junction, input, ResourceTypeDomain.Wood, 3);
        await Until(() => left.CurrentLoad == 3);
        Send(junction, junction, input, ResourceTypeDomain.Wood, 3);
        await Until(() => ahead.CurrentLoad == 3);
        Fill(left, ResourceTypeDomain.Wood, left.Capacity - 3);
        Fill(ahead, ResourceTypeDomain.Wood, ahead.Capacity - 3);
        Send(junction, junction, input, ResourceTypeDomain.Wood, 3);
        await UniTask.DelayFrame(4);
        Assert.That(_pool.Resources.Count, Is.EqualTo(1));
        var exit = right.Ports.First(port => (port.Use & CellPortUseDomain.Output) != 0).Id;
        Assert.That(right.TryExport(exit, 2, out _, out _));
        await UniTask.DelayFrame(4);
        Assert.That(right.AllocatedAmount, Is.Zero);
        Assert.That(right.TryExport(exit, 1, out _, out _));
        await Until(() => right.CurrentLoad == right.Capacity && _pool.Resources.Count == 0);
    }

    [UnityTest] public IEnumerator SideInputsChangeTheBeltAppearanceAndKeepItsHeldBatch() => VisualMerge().ToCoroutine();
    private async UniTask VisualMerge()
    {
        var belt = Add<ConveyorCellAdapter>("Conveyor", 5, 5);
        var left = Add<ConveyorCellAdapter>("Conveyor", 4, 5, cell => cell.transform.rotation = Quaternion.Euler(0, 90, 0));
        var right = Add<ConveyorCellAdapter>("Conveyor", 6, 5, cell => cell.transform.rotation = Quaternion.Euler(0, 270, 0));
        var rear = Add<ConveyorCellAdapter>("Conveyor", 5, 4, cell => cell.transform.rotation = Quaternion.Euler(0, 90, 0));
        await Until(() => belt.CellModel.transform.Find("JunctionBelt") != null);
        var model = belt.CellModel.transform.Find("JunctionBelt");
        var straight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Models/ConveyorCellModel.prefab");
        Assert.That(model.Find("BeltCenter").GetComponent<MeshFilter>().sharedMesh,
            Is.SameAs(straight.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh));
        Assert.That(model.Find("LeftBelt").gameObject.activeSelf);
        Assert.That(model.Find("RightBelt").gameObject.activeSelf);
        Assert.That(model.Find("BackBelt").gameObject.activeSelf, Is.False);
        Assert.That(Vector3.Dot(model.Find("LeftBelt").forward, Vector3.right), Is.EqualTo(1).Within(.001f));
        Assert.That(Vector3.Dot(model.Find("RightBelt").forward, Vector3.left), Is.EqualTo(1).Within(.001f));
        Assert.That(Vector3.Dot(model.Find("ForwardBelt").forward, Vector3.forward), Is.EqualTo(1).Within(.001f));
        rear.transform.rotation = Quaternion.identity;
        rear.InitializeSystem();
        await Until(() => model.Find("BackBelt").gameObject.activeSelf);

        var sink = Add<StorageCellAdapter>("Storage", 5, 6);
        var exit = sink.Ports.First(port => (port.Use & CellPortUseDomain.Output) != 0).Id;
        Fill(sink, ResourceTypeDomain.Wood, sink.Capacity);
        var first = Send(left, left, 3, ResourceTypeDomain.Wood, 2);
        await Until(() => Id(belt) == first && belt.CaptureBatches(_ => { }).Length == 1);
        Assert.That(belt.AllocateStorage(0, 1, ResourceTypeDomain.Wood), Is.False);
        right.OnDisconnect();
        right.gameObject.SetActive(false);
        Object.Destroy(right.gameObject);
        await Until(() => !model.Find("RightBelt").gameObject.activeSelf);
        Assert.That(_grid.GetCell(5, 5), Is.SameAs(belt));
        Assert.That(Id(belt), Is.EqualTo(first));

        Assert.That(sink.TryExport(exit, 5, out _, out _));
        await Until(() => sink.CurrentLoad == sink.Capacity - 3);
        Send(rear, rear, 3, ResourceTypeDomain.Wood, 3);
        await Until(() => sink.CurrentLoad == sink.Capacity && _pool.Resources.Count == 0);
    }

    [UnityTest] public IEnumerator ConfiguredCrossingPullsOnlyFromItsInputSides() => CrossingPickup().ToCoroutine();
    private async UniTask CrossingPickup()
    {
        var crossing = Add<CrossingCellAdapter>("Crossing", 5, 5, cell =>
            typeof(CrossingCellAdapter).GetMethod("ConfigureRoute", Fields).Invoke(cell, new object[]
            {
                new ConveyorRouteRulesDomain.Route(DirectionsDomain.Back | DirectionsDomain.Left, DirectionsDomain.Forward | DirectionsDomain.Right)
            }));
        var sourceLower = Add<StorageCellAdapter>("Storage", 5, 4);
        var sourceUpper = Add<CrafterCellAdapter>("Crafter", 4, 5, cell =>
        {
            cell.transform.rotation = Quaternion.Euler(0, 270, 0);
            cell.RestoreState(new SaveGameApplication.Cell
            {
                inventory = new[] { SaveGameApplication.SlotValue(int.MaxValue, ResourceTypeDomain.Stone, 4) }
            });
        });
        var sinkLower = Add<StorageCellAdapter>("Storage", 5, 6);
        var sinkUpper = Add<StorageCellAdapter>("Storage", 6, 5);
        Fill(sinkLower, ResourceTypeDomain.Wood, sinkLower.Capacity);
        Fill(sourceLower, ResourceTypeDomain.Wood, 3);
        await UniTask.DelayFrame(3);
        Assert.That(sinkUpper.CurrentLoad, Is.Zero);
        sourceUpper.transform.rotation = Quaternion.Euler(0, 90, 0);
        sourceUpper.InitializeSystem();
        await Until(() => sinkUpper.CurrentLoad == 4 && crossing.CaptureBatches(_ => {}).Length == 1);
        Assert.That(sinkUpper.StoredResourceType, Is.EqualTo(ResourceTypeDomain.Stone));
        sourceUpper.OnDisconnect();
        sourceUpper.gameObject.SetActive(false);
        Object.Destroy(sourceUpper.gameObject);
        var replacement = Add<StorageCellAdapter>("Storage", 4, 5);
        Fill(replacement, ResourceTypeDomain.Stone, 1);
        await Until(() => sinkUpper.CurrentLoad == 5);
        Assert.That(sourceLower.CurrentLoad, Is.Zero);
        var exit = sinkLower.Ports.First(port => (port.Use & CellPortUseDomain.Output) != 0).Id;
        Assert.That(sinkLower.TryExport(exit, 3, out _, out _));
        await Until(() => sinkLower.CurrentLoad == sinkLower.Capacity && _pool.Resources.Count == 0);
    }

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
    [UnityTest] public IEnumerator SourceRotationFallsBackAndReconnectionKeepsSettings()=>Reconnect().ToCoroutine();
    private async UniTask Reconnect()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);var source=Add<CrafterCellAdapter>("Crafter",4,3);
        await Until(()=>core.IsExportConveyor);typeof(ConveyorCellAdapter).GetField("transferAmount",Fields).SetValue(core,7);
        var identity=core.Identity;
        source.transform.rotation=Quaternion.Euler(0,180,0);source.InitializeSystem();await UniTask.DelayFrame(3);
        Assert.That(core.IsExportConveyor,Is.False);Assert.That(core.ProgressionType,Is.EqualTo(CellTypeDomain.Conveyor));Assert.That(core.Identity,Is.SameAs(identity));
        Assert.That(ConveyorCellAdapter.HasExtractionSource(_grid,4,4,Quaternion.identity),Is.False);
        source.transform.rotation=Quaternion.identity;source.InitializeSystem();await UniTask.DelayFrame(3);
        Assert.That(ConveyorCellAdapter.HasExtractionSource(_grid,4,4,Quaternion.identity));Assert.That(core.IsExportConveyor);Assert.That(core.TransferAmount,Is.EqualTo(7));
        core.transform.rotation=Quaternion.Euler(0,90,0);core.InitializeSystem();await UniTask.DelayFrame(3);
        Assert.That(core.TransferAmount,Is.EqualTo(7));Assert.That(core.Identity,Is.SameAs(identity));
    }
    [UnityTest] public IEnumerator ExplicitExporterFallsBackWithItsMovingBatch()=>FallbackWithBatch().ToCoroutine();
    private async UniTask FallbackWithBatch()
    {
        var source=Add<StorageCellAdapter>("Storage",4,3);
        var core=Add<ExportConveyorCellAdapter>("ExportConveyor",4,4);
        var sink=Add<StorageCellAdapter>("Storage",4,5);
        Fill(source,ResourceTypeDomain.Wood,3);
        await Until(()=>Id(core)!=0);
        var id=Id(core);
        source.OnDisconnect();
        await Until(()=>!core.IsExportConveyor);
        Assert.That(_grid.GetCell(4,4),Is.SameAs(core));
        Assert.That(core.transform.Find("Model").GetChild(1).gameObject.activeSelf);
        Assert.That(Id(core),Is.EqualTo(id));
        await Until(()=>sink.CurrentLoad==3);
        Assert.That(_pool.Resources.TryGet(id,out _),Is.False);
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
    [UnityTest] public IEnumerator DefaultCrafterCapacityAcceptsAWholeBatchAfterRecipeRemainder()=>RecipeRemainder().ToCoroutine();
    private async UniTask RecipeRemainder()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);var crafter=Add<CrafterCellAdapter>("Crafter",4,5);
        Assert.That(crafter.IngredientCapacity,Is.EqualTo(10));
        Assert.That(crafter.AllocateStorage(0,2,ResourceTypeDomain.Stone));crafter.StoreResource(0,2);
        Assert.That(core.AllocateStorage(0,5,ResourceTypeDomain.Stone));var id=_pool.CreateIdFromResourceData(ResourceTypeDomain.Stone,5);
        core.Reuse(0,id);core.StoreResource(0,5);
        Time.timeScale=10;
        await Until(()=>crafter.ExportStorageAmount==2 && crafter.GetInput(0).Amount==1 && Id(core)==0);
        Assert.That(_pool.Resources.Count,Is.Zero);Assert.That(crafter.GetInput(0).Allocated,Is.Zero);
    }
    [UnityTest] public IEnumerator InsufficientSpaceKeepsTheWholeBatchUntilCapacityIncreases()=>WaitForCapacity().ToCoroutine();
    private async UniTask WaitForCapacity()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);
        var sink=Add<StorageCellAdapter>("Storage",4,5, cell => typeof(StorageCellAdapter).GetField("capacity",Fields).SetValue(cell,3));
        Fill(sink,ResourceTypeDomain.Wood,1);
        Assert.That(core.AllocateStorage(0,5,ResourceTypeDomain.Wood));var id=_pool.CreateIdFromResourceData(ResourceTypeDomain.Wood,5);
        core.Reuse(0,id);core.StoreResource(0,5);
        await UniTask.Delay(400);
        Assert.That(Id(core),Is.EqualTo(id));Assert.That(_pool.Resources.TryGet(id,out var held));Assert.That(held.Amount,Is.EqualTo(5));
        Assert.That(_pool.Resources.Count,Is.EqualTo(1));Assert.That(sink.CurrentLoad,Is.EqualTo(1));Assert.That(sink.AllocatedAmount,Is.Zero);
        // 容量拡張後に同じ5個を一括で受け取り、分割用のIDを作らない。
        var catalog = new SkillTreeCatalogDomain(new[]
        {
            new SkillNodeDomain("capacity", "Capacity", 1, null, new[] {CellTypeDomain.Storage},
                EquipmentStatDomain.Capacity, multiplier: 2)
        });
        var skills = new SkillTreeApplication(catalog, new CurrencyWalletApplication(1), _grid.StatEffects);
        Assert.That(skills.Purchase("capacity"),Is.EqualTo(SkillPurchaseResultApplication.Success));
        Assert.That(sink.Capacity, Is.EqualTo(6));
        await Until(()=>sink.CurrentLoad==6);Assert.That(Id(core),Is.Zero);Assert.That(_pool.Resources.Count,Is.Zero);
    }
    [UnityTest] public IEnumerator CrossingWakesUpstreamWhenItsDestinationMakesSpace() => CrossingWait().ToCoroutine();
    private async UniTask CrossingWait()
    {
        var belt = Add<ConveyorCellAdapter>("Conveyor", 4, 4);
        Add<CrossingCellAdapter>("Crossing", 4, 5);
        var sink = Add<StorageCellAdapter>("Storage", 4, 6);
        var output = sink.Ports.First(p => (p.Use & CellPortUseDomain.Output) != 0).Id;
        Fill(sink, ResourceTypeDomain.Wood, sink.Capacity);
        Assert.That(belt.AllocateStorage(0, 5, ResourceTypeDomain.Wood));
        var id = _pool.CreateIdFromResourceData(ResourceTypeDomain.Wood, 5);
        belt.Reuse(0, id);
        belt.StoreResource(0, 5);
        await UniTask.DelayFrame(8);
        Assert.That(Id(belt), Is.EqualTo(id));
        Assert.That(sink.AllocatedAmount, Is.Zero);
        Assert.That(sink.TryExport(output, 3, out var first, out _));
        Assert.That(first, Is.EqualTo(3));
        await UniTask.DelayFrame(3);
        Assert.That(Id(belt), Is.EqualTo(id));
        Assert.That(sink.AllocatedAmount, Is.Zero);
        Assert.That(sink.TryExport(output, 2, out var second, out _));
        Assert.That(second, Is.EqualTo(2));
        await Until(() => Id(belt) == 0 && sink.CurrentLoad == sink.Capacity);
        Assert.That(_pool.Resources.Count, Is.Zero);
    }

    [UnityTest] public IEnumerator CrossingReachesItsExitWhileTheNextBeltIsOccupied() => CrossingExitWait().ToCoroutine();
    private async UniTask CrossingExitWait()
    {
        var before = Add<ConveyorCellAdapter>("Conveyor", 4, 4);
        var crossing = Add<CrossingCellAdapter>("Crossing", 4, 5);
        var after = Add<ConveyorCellAdapter>("Conveyor", 4, 6);
        var sink = Add<StorageCellAdapter>("Storage", 4, 7);
        Fill(sink, ResourceTypeDomain.Wood, sink.Capacity);
        var port = before.Ports.Single(p => before.PortDirection(p.Id) == Vector3Int.back).Id;
        var previous = Send(after, after, port, ResourceTypeDomain.Wood, 2);
        var next = Send(before, before, port, ResourceTypeDomain.Wood, 3);
        var visual = ResourceTransferAssertions.GetVisual(_pool, next);
        var exit = crossing.transform.position + Vector3.forward * .5f;
        await Until(() => Mathf.Abs(visual.transform.position.z - exit.z) < .001f);
        await UniTask.DelayFrame(3);
        Assert.That(visual.transform.position.z, Is.EqualTo(exit.z).Within(.001f));
        Assert.That(Id(before), Is.Zero);
        Assert.That(Id(after), Is.EqualTo(previous));
        var heldId = 0;
        Assert.That(crossing.CaptureBatches(id => heldId = id).Length, Is.EqualTo(1));
        Assert.That(heldId, Is.EqualTo(next));

        var output = sink.Ports.First(p => (p.Use & CellPortUseDomain.Output) != 0).Id;
        Assert.That(sink.TryExport(output, 5, out var removed, out _));
        Assert.That(removed, Is.EqualTo(5));
        await Until(() => sink.CurrentLoad == sink.Capacity && _pool.Resources.Count == 0);
    }

    [UnityTest] public IEnumerator CrossingShowsTwoStraightPathsAtEveryRotation() => CrossingDisplay().ToCoroutine();
    private async UniTask CrossingDisplay()
    {
        for (var rotation = 0; rotation < 4; rotation++)
        {
            var center = new Vector3Int(4 + 5 * (rotation % 2), 0, 4 + 5 * (rotation / 2));
            var crossing = Add<CrossingCellAdapter>("Crossing", center.x, center.z,
                cell => cell.transform.rotation = Quaternion.Euler(0, rotation * 90, 0));
            var forward = Vector3Int.RoundToInt(crossing.transform.forward);
            var right = Vector3Int.RoundToInt(crossing.transform.right);

            (StorageCellAdapter Result, GameObject Visual) Supply(Vector3Int direction, ResourceTypeDomain type, int amount)
            {
                var from = center - direction;
                var to = center + direction;
                var belt = Add<ConveyorCellAdapter>("Conveyor", from.x, from.z,
                    cell => cell.transform.rotation = Quaternion.LookRotation(direction));
                var result = Add<StorageCellAdapter>("Storage", to.x, to.z);
                var input = belt.Ports.First(port => (port.Use & CellPortUseDomain.Input) != 0).Id;
                Assert.That(belt.AllocateStorage(input, amount, type));
                var id = _pool.CreateIdFromResourceData(type, amount);
                belt.Reuse(input, id);
                belt.StoreResource(input, amount);
                return (result, ResourceTransferAssertions.GetVisual(_pool, id));
            }

            var lower = Supply(forward, ResourceTypeDomain.Wood, 3);
            var upper = Supply(right, ResourceTypeDomain.Stone, 4);
            await Until(() => crossing.CaptureBatches(_ => {}).Length == 2);
            Assert.That(upper.Visual.transform.position.y - lower.Visual.transform.position.y, Is.GreaterThan(.30f));
            Assert.That(Vector3.Dot(lower.Visual.transform.position - center, right), Is.EqualTo(0).Within(.01f));
            Assert.That(Vector3.Dot(upper.Visual.transform.position - center, forward), Is.EqualTo(0).Within(.01f));
            await Until(() => lower.Result.CurrentLoad == 3 && upper.Result.CurrentLoad == 4);
            Assert.That(lower.Result.StoredResourceType, Is.EqualTo(ResourceTypeDomain.Wood));
            Assert.That(upper.Result.StoredResourceType, Is.EqualTo(ResourceTypeDomain.Stone));
        }
        Assert.That(_pool.Resources.Count, Is.Zero);
    }
    [UnityTest] public IEnumerator InvalidPromotionVisualKeepsTheOriginalBatchAndRole()=>FailedPromotion().ToCoroutine();
    private async UniTask FailedPromotion()
    {
        var core=Add<ConveyorCellAdapter>("Conveyor",4,4);
        var bad=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExportConveyorCellAdapter>("Assets/Prefabs/Field/General/F_ExportConveyorCell.prefab"));bad.gameObject.SetActive(false);
        Object.DestroyImmediate(bad.transform.Find("Model").GetChild(0).gameObject);
        typeof(ConveyorCellAdapter).GetField("exportDefinition",Fields).SetValue(core,bad);
        Assert.That(core.AllocateStorage(0,2,ResourceTypeDomain.Wood));var id=_pool.CreateIdFromResourceData(ResourceTypeDomain.Wood,2);core.Reuse(0,id);core.StoreResource(0,2);
        Assert.Throws<TargetInvocationException>(()=>typeof(ConveyorCellAdapter).GetMethod("SetExportMode",Fields).Invoke(core,new object[] {true,true}));
        Assert.That(core.IsExportConveyor,Is.False);Assert.That(Id(core),Is.EqualTo(id));Assert.That(_pool.Resources.TryGet(id,out var held));Assert.That(held.Amount,Is.EqualTo(2));
        Object.Destroy(bad.gameObject);await UniTask.Yield();
    }
}

[TestFixture,Category("AutomaticConveyor")]
public sealed class AutomaticConveyorPlacementPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene=>true;
    [UnityTest] public IEnumerator UnifiedToolMatchesPreviewAndChargesOnePrice()=>VerifyPlacement().ToCoroutine();
    [UnityTest] public IEnumerator DragConvertsABeltWithAnIncomingReservationAndPreservesItsDirection() => VerifyRouteDrag(false).ToCoroutine();
    [UnityTest] public IEnumerator DragAddsSideOutputsToMakeAThreeWaySplit() => VerifyRouteDrag(true, connectForward: true).ToCoroutine();
    [UnityTest] public IEnumerator DragMakesALeftRightSplitWhenTheForwardExitIsUnconnected() => VerifyRouteDrag(true).ToCoroutine();

    private static async UniTask VerifyRouteDrag(bool split, bool connectForward = false)
    {
        var game = Object.FindAnyObjectByType<GameInitializerAdapter>();
        var placer = Object.FindAnyObjectByType<CellPlacerAdapter>();
        var grid = GridFieldDatabaseAdapter.Instance;
        Object.FindAnyObjectByType<EconomyPanelAdapter>().ShowPage(0);
        Assert.That(game.ConstructionStock.TryReserveDeposit(ResourceTypeDomain.CopperWire, 1, out var materials));
        Assert.That(materials.TryCommit());
        var center = grid.Owners.OfType<EmptyCellAdapter>().First(cell =>
            cell.XIndex > 0 && cell.ZIndex > 0 && cell.XIndex + 1 < grid.GeneratedSize && cell.ZIndex + 1 < grid.GeneratedSize &&
            grid.GetCell(cell.XIndex - 1, cell.ZIndex) is EmptyCellAdapter && grid.GetCell(cell.XIndex + 1, cell.ZIndex) is EmptyCellAdapter &&
            grid.GetCell(cell.XIndex, cell.ZIndex - 1) is EmptyCellAdapter && grid.GetCell(cell.XIndex, cell.ZIndex + 1) is EmptyCellAdapter);
        var x = center.XIndex;
        var z = center.ZIndex;
        void Drag(int from, int to, int previewCount, long previewCost, bool checkArrows = false, bool bothSides = false)
        {
            placer.SelectGrid(grid.GetCell(from, z).gameObject);
            placer.PointerBegin();
            placer.SelectGrid(grid.GetCell(to, z).gameObject);
            placer.PointerDrag();
            Assert.That(placer.PreviewCount, Is.EqualTo(previewCount));
            Assert.That(placer.PreviewCost, Is.EqualTo(previewCost));
            if (checkArrows)
            {
                var marker = Object.FindObjectsByType<ConveyorRouteViewAdapter>(FindObjectsSortMode.None)
                    .Single(view => view.transform.parent.name.StartsWith("P_", StringComparison.Ordinal));
                if (split)
                {
                    Assert.That(marker.transform.Find("BeltCenter").GetComponent<MeshRenderer>().enabled);
                    Assert.That(marker.transform.Find("ForwardBelt").gameObject.activeSelf, Is.EqualTo(!bothSides || connectForward));
                    Assert.That(marker.transform.Find("RightBelt").gameObject.activeSelf);
                    Assert.That(marker.transform.Find("BackBelt").gameObject.activeSelf);
                    Assert.That(marker.transform.Find("LeftBelt").gameObject.activeSelf, Is.EqualTo(bothSides));
                }
                else
                {
                    Assert.That(marker.GetComponent<MeshRenderer>().enabled);
                    var mesh = marker.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.vertexCount, Is.EqualTo(28));
                    Assert.That(mesh.bounds.max.y, Is.GreaterThan(.5f));
                }
            }
            placer.PointerEnd();
            if (previewCount > 0)
            {
                Assert.That(placer.LastPlacedCount, Is.EqualTo(previewCount));
                Assert.That(placer.LastPlacementCost, Is.EqualTo(previewCost));
            }
        }

        placer.transform.rotation = Quaternion.identity;
        placer.SetSelectedCellType(CellTypeDomain.Conveyor);
        Drag(x, x, 1, 5);
        var original = (ConveyorCellAdapter)grid.GetCell(x, z);
        void PlaceAt(CellTypeDomain type, int depth)
        {
            placer.SetSelectedCellType(type);
            placer.SelectGrid(grid.GetCell(x, depth).gameObject);
            placer.PointerBegin();
            placer.PointerDrag();
            placer.PointerEnd();
            Assert.That(placer.LastPlacedCount, Is.EqualTo(1));
        }
        StorageCellAdapter sink = null;
        if (split)
        {
            if (connectForward) PlaceAt(CellTypeDomain.Conveyor, z + 1);
            Assert.That(original.AllocateStorage(0, 2, ResourceTypeDomain.Stone));
        }
        else
        {
            Assert.That(game.ConstructionStock.TryReserveDeposit(ResourceTypeDomain.Plank, 2, out var planks));
            Assert.That(planks.TryCommit());
            PlaceAt(CellTypeDomain.Conveyor, z - 1);
            PlaceAt(CellTypeDomain.Storage, z + 1);
            sink = (StorageCellAdapter)grid.GetCell(x, z + 1);
            var sender = (ConveyorCellAdapter)grid.GetCell(x, z - 1);
            Assert.That(sender.AllocateStorage(0, 2, ResourceTypeDomain.Stone));
            sender.Reuse(0, ResourceItemObjectPoolAdapter.Instance.CreateIdFromResourceData(ResourceTypeDomain.Stone, 2));
            sender.StoreResource(0, 2);
            var reservation = typeof(ConveyorCellAdapter).GetField("_incomingReservationAmount", BindingFlags.Instance | BindingFlags.NonPublic);
            await UniTask.WaitUntil(() => (int)reservation.GetValue(original) == 2).Timeout(TimeSpan.FromSeconds(5), DelayType.Realtime);
            placer.SetSelectedCellType(CellTypeDomain.Conveyor);
        }
        Drag(split ? x : x - 1, x + 1, split ? 2 : 3, split ? 10 : 15, checkArrows: true);
        if (split)
        {
            await UniTask.NextFrame();
            Drag(x, x - 1, 2, 5, checkArrows: true, bothSides: true);
        }
        var converted = (ConnectableCellBaseAdapter)grid.GetCell(x, z);
        Assert.That(converted, split ? Is.TypeOf<ConveyorJunctionCellAdapter>() : Is.TypeOf<CrossingCellAdapter>());
        foreach (var port in converted.Ports)
        {
            var world = converted.PortDirection(port.Id);
            var output = world.x == 1 || (split && world.x == -1) || (world.z == 1 && (!split || connectForward));
            var input = world.z == -1 || (!split && world.x == -1);
            Assert.That(converted.HasPort(port.Id, CellPortUseDomain.Output), Is.EqualTo(output));
            Assert.That(converted.HasPort(port.Id, CellPortUseDomain.Input), Is.EqualTo(input));
        }
        Assert.That(converted.Connections.Count, Is.EqualTo(split ? (connectForward ? 3 : 2) : 4));
        Assert.That(game.ConstructionStock.Available(ResourceTypeDomain.CopperWire), Is.Zero);
        if (!split)
            await UniTask.WaitUntil(() => sink.CurrentLoad == 2).Timeout(TimeSpan.FromSeconds(5), DelayType.Realtime);
        await UniTask.Yield();
    }
    private static async UniTask VerifyPlacement()
    {
        var initializer=Object.FindAnyObjectByType<GameInitializerAdapter>();var placer=Object.FindAnyObjectByType<CellPlacerAdapter>();var grid=GridFieldDatabaseAdapter.Instance;
        var panel=Object.FindAnyObjectByType<EconomyPanelAdapter>();panel.ShowPage(0);
        void Place(CellTypeDomain type,int x,int z,float angle=0)
        { placer.transform.rotation=Quaternion.Euler(0,angle,0);placer.SetSelectedCellType(type);placer.SelectGrid(grid.GetCell(x,z).gameObject);placer.PointerBegin();placer.PointerDrag();placer.PointerEnd();Assert.That(placer.LastPlacedCount,Is.EqualTo(1)); }
        var strip=grid.Owners.OfType<EmptyCellAdapter>().First(c=>grid.IsWithinBounds(c.XIndex,c.ZIndex+2) &&
            grid.GetCell(c.XIndex,c.ZIndex+1) is EmptyCellAdapter && grid.GetCell(c.XIndex,c.ZIndex+2) is EmptyCellAdapter);
        var x=strip.XIndex;var z=strip.ZIndex;
        Place(CellTypeDomain.ExtractorWood,x,z,180);
        placer.transform.rotation=Quaternion.identity;placer.SetSelectedCellType(CellTypeDomain.Conveyor);placer.SelectGrid(grid.GetCell(x,z+1).gameObject);
        var preview=(GameObject)typeof(CellPlacerAdapter).GetField("_placeholderCell",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(placer);
        Assert.That(preview.name,Does.Contain("ExportConveyor"));
        placer.PointerBegin();placer.SelectGrid(grid.GetCell(x,z+2).gameObject);placer.PointerDrag();placer.PointerEnd();
        Assert.That(placer.LastPlacedCount,Is.EqualTo(2));Assert.That(placer.LastPlacementCost,Is.EqualTo(10));Assert.That(initializer.Wallet.Current.Balance,Is.EqualTo(70));
        Assert.That(((ConveyorCellAdapter)grid.GetCell(x,z+1)).IsExportConveyor);Assert.That(((ConveyorCellAdapter)grid.GetCell(x,z+2)).IsExportConveyor,Is.False);
        Assert.That(placer.SelectedType,Is.EqualTo(CellTypeDomain.Conveyor));
        Assert.That(panel.PlacementButtons.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).Any(b=>b.name=="ExportConveyorButton"),Is.False);
        var original = grid.GetCell(x, z + 1);
        placer.SelectGrid(original.gameObject);
        placer.PointerBegin();
        placer.SelectGrid(grid.GetCell(x, z + 2).gameObject);
        placer.PointerDrag();
        Assert.That(placer.PreviewCount, Is.Zero);
        Assert.That(placer.PlacementNotice, Is.EqualTo("既存セルと重なっています"));
        placer.PointerEnd();
        Assert.That(grid.GetCell(x, z + 1), Is.SameAs(original));
        Assert.That(initializer.Wallet.Current.Balance, Is.EqualTo(70));
        await UniTask.DelayFrame(2);
    }
}
#endif
