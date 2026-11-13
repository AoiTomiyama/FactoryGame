#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class ConstructionTestRoute
{
    internal static ResourceDefinition Resources => AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset");
    internal static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    internal static bool Loaded(ConveyorCellAdapter belt) => (bool)typeof(ConveyorCellAdapter).GetProperty("HasResource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(belt);
    internal static void Feed(ConveyorCellAdapter belt, ResourceItemObjectPoolAdapter pool, ResourceTypeDomain type, int amount)
    {
        Assert.That(Loaded(belt), Is.False);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        typeof(ConveyorCellAdapter).GetProperty("ResourceId", flags).SetValue(belt, pool.CreateIdFromResourceData(type, amount));
        typeof(ConveyorCellAdapter).GetProperty("HasResource", flags).SetValue(belt, true); Set(belt, "_readyToSend", true);
    }
    internal static async UniTask Until(Func<bool> predicate, string step = "代表経路")
    {
        var deadline = Time.realtimeSinceStartup + 20;
        while (!predicate() && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
        Assert.That(predicate(), step + "が制限時間内に進む");
    }
}

[TestFixture, Category("Construction")]
public sealed class ConstructionDepotPlayModeTests : FactoryPlayModeScope
{
    [UnityTest] public IEnumerator EightInputsAcrossRotationsShareStockAndKeepItAfterRemoval() => Verify().ToCoroutine();
    private static async UniTask Verify()
    {
        var pool = new GameObject("DepotPool").AddComponent<ResourceItemObjectPoolAdapter>();
        ConstructionTestRoute.Set(pool, "resourceDatabase", ConstructionTestRoute.Resources);
        var stock = new ConstructionStockApplication(ConstructionTestRoute.Resources.GetAllResources().Select(r => r.ResourceTypeDomain));
        var source = AssetDatabase.LoadAssetAtPath<ConstructionDepotCellAdapter>("Assets/Prefabs/Field/General/F_ConstructionDepotCell.prefab");
        Assert.That(source.Footprint.Area, Is.EqualTo(4)); Assert.That(source.Ports.Count, Is.EqualTo(8));
        Assert.That(source.Ports.All(p => p.Use == CellPortUseDomain.Input));
        var types = new[] { ResourceTypeDomain.Iron, ResourceTypeDomain.Copper, ResourceTypeDomain.Coal, ResourceTypeDomain.Plank,
            ResourceTypeDomain.IronIngot, ResourceTypeDomain.CopperWire, ResourceTypeDomain.Steel, ResourceTypeDomain.MachineParts };
        Time.timeScale = 10;
        for (var turn = 0; turn < 4; turn++)
        {
            var grid = new GameObject("DepotRoute").AddComponent<GridFieldDatabaseAdapter>();
            var depot = Object.Instantiate(source, new Vector3(10,0,10), Quaternion.Euler(0,turn*90,0), grid.transform); depot.Configure(stock);
            depot.ValidatePorts();
            var other = Object.Instantiate(source, new Vector3(18,0,18), Quaternion.identity, grid.transform); other.Configure(stock);
            var targets = types.ToDictionary(type => type, type => stock.Stored(type) + 2);
            var belts = new List<ConveyorCellAdapter>();
            foreach (var port in depot.Ports)
            {
                var direction = depot.PortDirection(port.Id);
                var belt = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ConveyorCellAdapter>("Assets/Prefabs/Field/General/F_ConveyorCell.prefab"),
                    depot.PortPosition(port.Id) + direction, Quaternion.LookRotation(-direction), grid.transform);
                ConstructionTestRoute.Feed(belt,pool,types[port.Id],2); belts.Add(belt);
            }
            grid.InitializeCells(24); depot.InitializeSystem(); other.InitializeSystem(); foreach(var belt in belts) belt.InitializeSystem();
            Assert.That(depot.GetComponent<BoxCollider>().size.x, Is.EqualTo(2));
            Assert.That(depot.Connections.Count, Is.EqualTo(8));
            foreach (var point in depot.OccupiedCoordinates) Assert.That(grid.GetCell(point.x,point.z), Is.SameAs(depot));
            await ConstructionTestRoute.Until(() => targets.All(p => stock.Stored(p.Key) == p.Value));
            Assert.That(depot.AllocateStorage(0,3,ResourceTypeDomain.Plank));
            depot.gameObject.SetActive(false);
            var retained = stock.Stored(ResourceTypeDomain.Plank);
            Assert.That(other.AllocateStorage(7,1,ResourceTypeDomain.Plank)); other.StoreResource(7,1);
            Assert.That(stock.Stored(ResourceTypeDomain.Plank), Is.EqualTo(retained+1));
            depot.gameObject.SetActive(true);
            Assert.That(depot.AllocateStorage(0,3,ResourceTypeDomain.Plank)); depot.CancelStorage(0,3,ResourceTypeDomain.Plank);
            Object.Destroy(grid.gameObject); await UniTask.Yield(); await UniTask.Yield();
            Assert.That(stock.Stored(ResourceTypeDomain.Plank), Is.EqualTo(retained+1), "全資材庫の撤去後も確定在庫を保持");
        }
        Assert.That(pool.Resources.Count, Is.Zero);
    }
}

[TestFixture, Category("Construction")]
public sealed class ConstructionGameplayPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator MiningCraftingDepotAdvancedConstructionAndNewShipping() => Gameplay().ToCoroutine();
    private static async UniTask Gameplay()
    {
        var initializer = Object.FindAnyObjectByType<GameInitializerAdapter>(); var grid = GridFieldDatabaseAdapter.Instance;
        var placer = Object.FindAnyObjectByType<CellPlacerAdapter>(); var panel = Object.FindAnyObjectByType<EconomyPanelAdapter>();
        var pool = ResourceItemObjectPoolAdapter.Instance; var stock = initializer.ConstructionStock;
        panel.ShowPage(0); Time.timeScale = 20;
        void Place(CellTypeDomain type, int x, int z, float angle=0)
        {
            placer.transform.rotation = Quaternion.Euler(0,angle,0); placer.SetSelectedCellType(type); placer.SelectGrid(grid.GetCell(x,z).gameObject);
            placer.PointerBegin(); placer.PointerDrag(); placer.PointerEnd();
            Assert.That(placer.LastPlacedCount, Is.EqualTo(1), type + "の購入");
        }
        (int x,int z) Area(int width,int depth)
        {
            for(var x=5;x<94-width;x++) for(var z=5;z<94-depth;z++)
                if(Enumerable.Range(0,width).All(dx => Enumerable.Range(0,depth).All(dz => grid.GetCell(x+dx,z+dz) is EmptyCellAdapter))) return (x,z);
            throw new InvalidOperationException("No construction test area.");
        }
        async UniTask Funds(long amount) => await ConstructionTestRoute.Until(() => initializer.Wallet.Current.AvailableBalance >= amount, "残高 " + amount);
        var commands=TestScenes.StarterPlacements(initializer.Session.Selected).ToArray();var first=commands[0];
        Place(first.Type,first.X,first.Z,first.Yaw);
        // 換金経路をつなぐ前に、最初の板材用の木材を採る。
        await Mine((ExtractorCellAdapter)grid.GetCell(first.X,first.Z),4);
        foreach(var command in commands.Skip(1)) Place(command.Type,command.X,command.Z,command.Yaw);
        await Funds(30);
        var site=Area(2,3); Place(CellTypeDomain.ConstructionDepot,site.x,site.z+1); var depot=(ConstructionDepotCellAdapter)grid.GetCell(site.x,site.z+1);
        await Funds(5); Place(CellTypeDomain.Conveyor,site.x,site.z); var feeder=(ConveyorCellAdapter)grid.GetCell(site.x,site.z);
        var basicSite=Area(1,1); await Funds(30); Place(CellTypeDomain.Crafter,basicSite.x,basicSite.z); var basic=(CrafterCellAdapter)grid.GetCell(basicSite.x,basicSite.z);
        await Funds(30);var ironSite=TestScenes.ExtractorSite(ResourceTypeDomain.Iron);Place(CellTypeDomain.ExtractorIron,ironSite.X,ironSite.Z,ironSite.Yaw);var iron=(ExtractorCellAdapter)grid.GetCell(ironSite.X,ironSite.Z);
        async UniTask<int> Mine(ExtractorCellAdapter extractor, int amount)
        {
            await ConstructionTestRoute.Until(() => extractor.CurrentLoad >= amount, "採取 " + extractor.ResourceTypeDomain + " " + amount);
            Assert.That(extractor.TryExport(extractor.Ports.First(p => (p.Use & CellPortUseDomain.Output)!=0).Id,amount,out var count,out _));
            Assert.That(count, Is.EqualTo(amount)); return count;
        }
        async UniTask Produce(CrafterCellAdapter crafter, ResourceTypeDomain output, int amount, params (ResourceTypeDomain type,int amount)[] inputs)
        {
            for(var i=0;i<inputs.Length;i++)
            {
                var remaining = inputs[i].amount;
                while(remaining > 0)
                {
                    var chunk = Math.Min(remaining,crafter.IngredientCapacity);
                    await ConstructionTestRoute.Until(() => crafter.GetInput(i).Amount + crafter.GetInput(i).Allocated + chunk <= crafter.IngredientCapacity, "加工入力 " + inputs[i].type);
                    Assert.That(crafter.AllocateStorage(i,chunk,inputs[i].type), "材料切替 " + inputs[i].type);
                    crafter.StoreResource(i,chunk); remaining -= chunk;
                }
            }
            await ConstructionTestRoute.Until(() => crafter.ExportResourceType==output && crafter.ExportStorageAmount>=amount, "加工 " + output);
            Assert.That(crafter.TryExport(2,amount,out var count,out var type)); Assert.That(type,Is.EqualTo(output)); Assert.That(count,Is.EqualTo(amount));
        }
        async UniTask Deposit(ResourceTypeDomain type,int amount)
        {
            var expected=stock.Stored(type)+amount; ConstructionTestRoute.Feed(feeder,pool,type,amount);
            await ConstructionTestRoute.Until(() => stock.Stored(type)==expected && !ConstructionTestRoute.Loaded(feeder));
        }
        await Funds(90);
        await Produce(basic,ResourceTypeDomain.Plank,2,(ResourceTypeDomain.Wood,4)); await Deposit(ResourceTypeDomain.Plank,2);
        await Mine(iron,4); await Produce(basic,ResourceTypeDomain.IronIngot,2,(ResourceTypeDomain.Iron,4)); await Deposit(ResourceTypeDomain.IronIngot,2);
        var advancedSite=Area(3,2); var before=initializer.Wallet.Current.Balance; Place(CellTypeDomain.AdvancedCrafter,advancedSite.x,advancedSite.z);
        Assert.That(initializer.Wallet.Current.Balance,Is.EqualTo(before-90)); Assert.That(stock.Stored(ResourceTypeDomain.Plank),Is.Zero); Assert.That(stock.Stored(ResourceTypeDomain.IronIngot),Is.Zero);
        var advanced=(CrafterCellAdapter)grid.GetCell(advancedSite.x,advancedSite.z);
        await Funds(55);var copperSite=TestScenes.ExtractorSite(ResourceTypeDomain.Copper);Place(CellTypeDomain.ExtractorCopper,copperSite.X,copperSite.Z,copperSite.Yaw);
        var coalSite=TestScenes.ExtractorSite(ResourceTypeDomain.Coal);Place(CellTypeDomain.ExtractorCoal,coalSite.X,coalSite.Z,coalSite.Yaw);
        await Mine(iron,4); await Produce(basic,ResourceTypeDomain.IronIngot,2,(ResourceTypeDomain.Iron,4));
        await Mine((ExtractorCellAdapter)grid.GetCell(coalSite.X,coalSite.Z),1);
        await Produce(advanced,ResourceTypeDomain.Steel,1,(ResourceTypeDomain.IronIngot,2),(ResourceTypeDomain.Coal,1));
        await Mine((ExtractorCellAdapter)grid.GetCell(copperSite.X,copperSite.Z),8);
        await Produce(basic,ResourceTypeDomain.CopperIngot,4,(ResourceTypeDomain.Copper,8));
        await Produce(basic,ResourceTypeDomain.CopperWire,2,(ResourceTypeDomain.CopperIngot,4));
        await Produce(advanced,ResourceTypeDomain.MachineParts,1,(ResourceTypeDomain.Steel,1),(ResourceTypeDomain.CopperWire,2));
        await Deposit(ResourceTypeDomain.MachineParts,1); await Funds(30);
        var shippingSite=Area(1,1); Place(CellTypeDomain.Shipping,shippingSite.x,shippingSite.z); Assert.That(stock.Stored(ResourceTypeDomain.MachineParts),Is.Zero);
        Assert.That(initializer.Deliveries.Current.Delivered.Values.Sum(),Is.Zero,"建設用納品は特異点の目標を増やさない");
        // 最大品目数と長い数量を既存の詳細スクロールへ表示する。
        foreach(var resource in ConstructionTestRoute.Resources.GetAllResources()) { Assert.That(stock.TryReserveDeposit(resource.ResourceTypeDomain,1234567890,out var ticket)); ticket.TryCommit(); }
        var view=CellStatusViewAdapter.Instance; view.UpdateUIStatusWindow(depot);
        var layout=Object.FindAnyObjectByType<HudLayoutAdapter>(); var canvas=layout.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.WorldSpace; canvas.GetComponent<CanvasScaler>().enabled=false;
        foreach(var size in new[]{new Vector2(1600,900),new Vector2(1366,768)})
        {
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(size.x*720/size.y,720);
            placer.SetSelectedCellType(CellTypeDomain.AdvancedCrafter); await UniTask.DelayFrame(5); Canvas.ForceUpdateCanvases();
            Assert.That(panel.PlacementText,Does.Contain("板材").And.Contain("鉄インゴット"));
            Assert.That(layout.EquipmentText.preferredHeight,Is.LessThanOrEqualTo(layout.EquipmentText.rectTransform.rect.height+1));
            Assert.That(panel.EquipmentText,Does.Contain("機械部品").And.Contain("1234567890")); Assert.That(panel.UpgradeInteractable,Is.False);
            view.StatusScroll.verticalNormalizedPosition=0; await UniTask.DelayFrame(3); Canvas.ForceUpdateCanvases();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(view.StatusScroll.viewport,layout.EquipmentText.rectTransform);
            Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(view.StatusScroll.viewport.rect.yMin-1));
        }
        view.SetStatusWindowActive(false); Assert.That(panel.EquipmentText,Is.Empty);
    }
}
#endif
