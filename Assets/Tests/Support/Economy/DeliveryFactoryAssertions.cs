#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>保存済みの地形と購入操作だけで最終納品まで進める。資源と資金の注入は行わない。</summary>
public sealed class DeliveryFactoryAssertions
{
    private readonly GridFieldDatabaseAdapter _grid = GridFieldDatabaseAdapter.Instance;
    private readonly GameInitializerAdapter _initializer = TestScenes.Find<GameInitializerAdapter>();
    private readonly CellPlacerAdapter _placer = TestScenes.Find<CellPlacerAdapter>();
    private readonly EconomyPanelAdapter _panel = TestScenes.Find<EconomyPanelAdapter>();
    private static readonly Vector3Int[] Directions = { Vector3Int.forward, Vector3Int.right, Vector3Int.back, Vector3Int.left };
    private float _start;
    private int _purchases, _belts;
    private double _fundingWait;
    private long _earned, _spent;

    public static async UniTask Verify()
    {
        var run = new DeliveryFactoryAssertions();
        await run.Play();
    }
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Delivery factory: " + message); }
    private async UniTask Until(Func<bool> predicate, string step, float seconds = 120)
    {
        var deadline = Time.realtimeSinceStartup + seconds;
        while (!predicate() && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
        Require(predicate(), step);
    }
    private async UniTask Funds(long price)
    {
        var start = Time.time;
        await Until(() => _initializer.Wallet.Current.AvailableBalance >= price, "資金待ち " + price);
        _fundingWait += Time.time - start;
    }
    private async UniTask<CellBaseAdapter> Place(CellTypeDomain type, Vector3Int position, Vector3Int forward)
    {
        var price=0L;
        Require(type==CellTypeDomain.Empty || _initializer.Prices.TryGetPlacementAmount(type, 1, out price), "購入価格 " + type);
        await Funds(price);
        _placer.transform.rotation = Quaternion.LookRotation(forward);
        _placer.SetSelectedCellType(type);
        _placer.SelectGrid(_grid.GetCell(position.x, position.z).gameObject);
        _placer.PointerBegin(); _placer.PointerDrag(); _placer.PointerEnd();
        Require(_placer.LastPlacedCount == 1, $"配置 {type} {position}: {_placer.LastPlacementResult}");
        _spent += _placer.LastPlacementCost;
        if (type != CellTypeDomain.Empty) { _purchases++; if (type == CellTypeDomain.Conveyor) _belts++; }
        return _grid.GetCell(position.x, position.z);
    }
    private bool Empty(Vector3Int p) => _grid.IsWithinBounds(p.x,p.z) && _grid.GetCell(p.x,p.z) is EmptyCellAdapter;
    private async UniTask<ExtractorCellAdapter> Mine(ResourceTypeDomain resource, CellTypeDomain type, Vector3Int near)
    {
        foreach (var source in UnityEngine.Object.FindObjectsByType<ResourceCellAdapter>(FindObjectsSortMode.None)
                     .Where(c => c.ResourceTypeDomain == resource).OrderBy(c => (c.transform.position-near).sqrMagnitude))
        foreach (var direction in Directions)
        {
            var position = Vector3Int.RoundToInt(source.transform.position) - direction;
            if (!Empty(position) || !Directions.Any(d => d != direction && Empty(position+d) && Empty(position+d*2))) continue;
            return (ExtractorCellAdapter)await Place(type,position,direction);
        }
        throw new InvalidOperationException("採取場所が見つからない: " + resource);
    }
    private async UniTask<T> Equipment<T>(CellTypeDomain type, int x, int z) where T:CellBaseAdapter
    {
        var width=type==CellTypeDomain.AdvancedCrafter?3:type==CellTypeDomain.ConstructionDepot?2:1;
        var depth=type==CellTypeDomain.AdvancedCrafter || type==CellTypeDomain.ConstructionDepot?2:1;
        var sites=Enumerable.Range(1,98-width).SelectMany(px=>Enumerable.Range(1,97-depth).Select(pz=>new Vector3Int(px,0,pz)))
            .OrderBy(p=>Mathf.Abs(p.x-x)+Mathf.Abs(p.z-z));
        foreach(var p in sites)
            if(Enumerable.Range(-1,width+2).All(dx=>Enumerable.Range(-1,depth+3).All(dz=>Empty(p+new Vector3Int(dx,0,dz)))))
                return (T)await Place(type,p,Vector3Int.forward);
        throw new InvalidOperationException("No existing construction space for "+type);
    }

    private List<Vector3Int> Path(Vector3Int first, Vector3Int direction, Vector3Int end)
    {
        if (!Empty(first) || !Empty(end)) return null;
        if (first == end) return new List<Vector3Int>{first};
        var second = first + direction;
        if (!Empty(second)) return null;
        var queue = new Queue<Vector3Int>(); var previous = new Dictionary<Vector3Int,Vector3Int>{{second,first}};
        queue.Enqueue(second);
        while(queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (p == end)
            {
                var result = new List<Vector3Int>{p};
                while(p != first) { p=previous[p]; result.Add(p); }
                result.Reverse(); return result;
            }
            foreach(var d in Directions)
            {
                var next=p+d;
                if (next == first || previous.ContainsKey(next) || !Empty(next)) continue;
                previous[next]=p; queue.Enqueue(next);
            }
        }
        return null;
    }
    private async UniTask<List<ConveyorCellAdapter>> Connect(ConnectableCellBaseAdapter source, ConnectableCellBaseAdapter target, int? input = null)
    {
        List<Vector3Int> best=null; var outgoing=Vector3Int.zero; var incoming=Vector3Int.zero;
        foreach(var a in source.Ports.Where(p=>(p.Use & CellPortUseDomain.Output)!=0))
        foreach(var b in target.Ports.Where(p=>(p.Use & CellPortUseDomain.Input)!=0 && (!input.HasValue || p.Id==input)))
        {
            var start=Vector3Int.RoundToInt(source.PortPosition(a.Id))+source.PortDirection(a.Id);
            var end=Vector3Int.RoundToInt(target.PortPosition(b.Id))+target.PortDirection(b.Id);
            var path=Path(start,source.PortDirection(a.Id),end);
            if(path==null || path.Count==1 && source.PortDirection(a.Id)!=-target.PortDirection(b.Id)) continue;
            if(best==null || path.Count<best.Count) { best=path;outgoing=source.PortDirection(a.Id);incoming=-target.PortDirection(b.Id); }
        }
        Require(best!=null,$"配線経路 {source.name} {source.transform.position} -> {target.name} {target.transform.position}");
        var cells=new List<ConveyorCellAdapter>();
        // 受け取り側からつなぎ、敷設途中のラインへ資源が流れ込む時間を抑える。
        for(var i=best.Count-1;i>=0;i--)
        {
            var facing=i==0 ? outgoing : i==best.Count-1 ? incoming : best[i+1]-best[i];
            cells.Add((ConveyorCellAdapter)await Place(CellTypeDomain.Conveyor,best[i],facing));
        }
        Debug.Log($"Delivery factory route: {source.transform.position}->{target.transform.position}, belts={cells.Count}");
        return cells;
    }
    private async UniTask Remove(List<ConveyorCellAdapter> line)
    {
        await Until(()=>line.All(c=>!Loaded(c)),"切替前の搬送完了");
        foreach(var c in line) await Place(CellTypeDomain.Empty,new Vector3Int(c.XIndex,0,c.ZIndex),Vector3Int.forward);
        await UniTask.Yield();
    }
    private static bool Loaded(ConveyorCellAdapter cell)
        => (bool)typeof(ConveyorCellAdapter).GetProperty("HasResource",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(cell);
    private void Record(string phase)
        => Debug.Log($"Delivery factory phase: {phase}, gameSeconds={Time.time-_start:F1}, fundingWait={_fundingWait:F1}, earned={_earned}, spent={_spent}, balance={_initializer.Wallet.Current.Balance}, purchases={_purchases}, beltTiles={_belts}");

    private async UniTask Play()
    {
        _start=Time.time; Require(_initializer.Wallet.Current.Balance==100,"初期資金100");
        Require(UnityEditor.EditorUtility.audioMasterMute,"消音"); _panel.ShowPage(0);
        var last=100L;
        _initializer.Wallet.Changed+=s=>{ if(s.Balance>last)_earned+=s.Balance-last; last=s.Balance; };
        foreach(var command in TestScenes.StarterPlacements(_initializer.Session.Selected))
            await Place(command.Type,new Vector3Int(command.X,0,command.Z),Vector3Int.RoundToInt(Quaternion.Euler(0,command.Yaw,0)*Vector3.forward));
        await Until(()=>_earned>0,"初回換金"); Record("初回換金");
        Time.timeScale=20;
        foreach(var uid in new[]{"basic-research","wood-extraction-1"})
        {
            var price=_initializer.Skills.Catalog.Nodes.Single(n=>n.Uid==uid).Price; await Funds(price);
            Require(_panel.PurchaseSkill(uid)==SkillPurchaseResultApplication.Success,"収益からの研究購入");_spent+=price;
        }
        await Funds(1400); Record("工場への再投資資金");
        var core=TestScenes.Find<SingularityCellAdapter>(); var stock=_initializer.ConstructionStock;
        var wood=await Mine(ResourceTypeDomain.Wood,CellTypeDomain.ExtractorWood,new Vector3Int(58,0,44));
        var stone=await Mine(ResourceTypeDomain.Stone,CellTypeDomain.ExtractorStone,new Vector3Int(54,0,54));
        var rawWood=await Connect(wood,core);var rawStone=await Connect(stone,core);
        await Until(()=>_initializer.Deliveries.Current.CompletedStages>=1,"原料目標の達成");Record("原料目標達成");
        wood.gameObject.SetActive(false);stone.gameObject.SetActive(false);
        await Remove(rawWood);await Remove(rawStone);
        var plank=await Equipment<CrafterCellAdapter>(CellTypeDomain.Crafter,58,46);
        var brick=await Equipment<CrafterCellAdapter>(CellTypeDomain.Crafter,58,56);
        await Connect(wood,plank,0);await Connect(stone,brick,0);
        var depot=await Equipment<ConstructionDepotCellAdapter>(CellTypeDomain.ConstructionDepot,61,44);
        var plankStock=await Connect(plank,depot);
        var iron=await Mine(ResourceTypeDomain.Iron,CellTypeDomain.ExtractorIron,new Vector3Int(46,0,47));
        var ingot=await Equipment<CrafterCellAdapter>(CellTypeDomain.Crafter,44,42);
        await Connect(iron,ingot,0);await Connect(ingot,depot);
        wood.gameObject.SetActive(true);stone.gameObject.SetActive(true);
        await Until(()=>stock.Stored(ResourceTypeDomain.Plank)>=2 && stock.Stored(ResourceTypeDomain.IronIngot)>=2,"上位加工の建設資材");
        plank.gameObject.SetActive(false);await Remove(plankStock);
        var advanced=await Equipment<CrafterCellAdapter>(CellTypeDomain.AdvancedCrafter,59,50);Record("材料を使って上位加工を建設");
        var planks=await Connect(plank,core);var bricks=await Connect(brick,core);plank.gameObject.SetActive(true);
        await Until(()=>_initializer.Deliveries.Current.CompletedStages>=2,"加工品目標の達成");Record("加工品目標達成");
        plank.gameObject.SetActive(false);brick.gameObject.SetActive(false);
        await Remove(planks);await Remove(bricks);
        await Connect(plank,advanced,0);await Connect(brick,advanced,1);await Connect(advanced,core);
        plank.gameObject.SetActive(true);brick.gameObject.SetActive(true);
        await Until(()=>_initializer.Deliveries.Current.IsComplete,"最終目標の達成");Record("最終目標達成");
        var delivered=_initializer.Deliveries.Current.Count(ResourceTypeDomain.BuildingParts);var balance=_initializer.Wallet.Current.Balance;
        var camera=TestScenes.Find<CameraControllerAdapter>();Require(camera.SetFirstPerson(true),"達成後の散策");
        await UniTask.Delay(650,ignoreTimeScale:true);
        await Until(()=>_initializer.Deliveries.Current.Count(ResourceTypeDomain.BuildingParts)>delivered && _initializer.Wallet.Current.Balance>balance,"達成後の生産・換金・吸収");
        Require(camera.SetFirstPerson(false),"散策から復帰");await UniTask.Delay(650,ignoreTimeScale:true);
        Require(_panel.SummaryView.Items[0].text=="目標達成","達成表示の復帰");
        Require(_initializer.Wallet.Current.Balance==100+_earned-_spent,"収益と購入の照合");Record("達成後も継続稼働");
    }
}
#endif
