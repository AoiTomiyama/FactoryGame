using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>生成済み地形を読み取り、換金経路と建設場所が成立する候補だけを返す。</summary>
public static class StartSiteRulesDomain
{
    public readonly struct Terrain
    {
        public readonly bool Empty;
        public readonly ResourceTypeDomain Resource;
        public bool Natural => Empty || Resource!=ResourceTypeDomain.None;
        public Terrain(bool empty,ResourceTypeDomain resource=ResourceTypeDomain.None) { Empty=empty;Resource=resource; }
    }
    public sealed class Candidate
    {
        public readonly int X,Z;
        public readonly (int x,int z) Shipping,Extractor,Wood;
        public IReadOnlyList<(int x,int z)> Belts { get; }
        internal Candidate(int x,int z,(int,int) shipping,(int,int) extractor,(int,int) wood,List<(int,int)> belts)
        { X=x;Z=z;Shipping=shipping;Extractor=extractor;Wood=wood;Belts=belts.AsReadOnly(); }
    }
    private static readonly (int x,int z)[] Directions={(0,1),(1,0),(0,-1),(-1,0)};
    private static readonly ResourceTypeDomain[] Required={ResourceTypeDomain.Wood,ResourceTypeDomain.Stone,ResourceTypeDomain.Iron,ResourceTypeDomain.Copper,ResourceTypeDomain.Coal};
    public static IReadOnlyList<Candidate> Candidates(Terrain[,] map,int seed,CellFootprintDomain footprint,(int x,int z) shippingOffset,
        long balance,long extractorPrice,long beltPrice)
    {
        var size=map.GetLength(0);var resources=Required.ToDictionary(t=>t,_=>new List<(int x,int z)>());
        for(var x=0;x<size;x++) for(var z=0;z<map.GetLength(1);z++)
            if(resources.TryGetValue(map[x,z].Resource,out var list)) list.Add((x,z));
        var result=new List<Candidate>();
        if(resources.Values.Any(list=>list.Count==0) || balance<extractorPrice) return result.AsReadOnly();
        bool Free((int x,int z) p)=>p.x>=0 && p.z>=0 && p.x<size && p.z<map.GetLength(1) && map[p.x,p.z].Empty;
        bool ExportExit((int x,int z) raw)
        {
            foreach(var side in Directions)
            {
                var miner=(x:raw.x+side.x,z:raw.z+side.z);if(!Free(miner)) continue;
                foreach(var d in Directions)
                {
                    var first=(x:miner.x+d.x,z:miner.z+d.z);var second=(x:first.x+d.x,z:first.z+d.z);
                    if(!Free(first) || !Free(second)) continue;
                    var seen=new HashSet<(int,int)>();var pending=new Queue<(int x,int z)>();pending.Enqueue(second);
                    // 取り出した先が袋小路なら候補検索を始めない。建設用の2枠だけでも41マスを使う。
                    while(pending.Count>0 && seen.Count<41)
                    {
                        var p=pending.Dequeue();if(p==miner || p==first || !Free(p) || !seen.Add(p)) continue;
                        foreach(var step in Directions) pending.Enqueue((p.x+step.x,p.z+step.z));
                    }
                    if(seen.Count>=41) return true;
                }
            }
            return false;
        }
        if(Required.Any(t=>!resources[t].Any(ExportExit))) return result.AsReadOnly();
        // 最初の換金予算で届かない出荷位置は、地形の詳細な連結探索より先に除外する。
        var woodDistance=new int[size,map.GetLength(1)];
        for(var x=0;x<size;x++) for(var z=0;z<map.GetLength(1);z++) woodDistance[x,z]=int.MaxValue;
        var nearWood=new Queue<(int x,int z)>();
        foreach(var wood in resources[ResourceTypeDomain.Wood]) {woodDistance[wood.x,wood.z]=0;nearWood.Enqueue(wood);}
        while(nearWood.Count>0)
        {
            var p=nearWood.Dequeue();
            foreach(var d in Directions)
            {
                var n=(x:p.x+d.x,z:p.z+d.z);
                if(n.x<0 || n.z<0 || n.x>=size || n.z>=map.GetLength(1) || woodDistance[n.x,n.z]<=woodDistance[p.x,p.z]+1) continue;
                woodDistance[n.x,n.z]=woodDistance[p.x,p.z]+1;nearWood.Enqueue(n);
            }
        }
        var maximumBelts=beltPrice==0?long.MaxValue:(balance-extractorPrice)/beltPrice;
        // 出荷候補の空セルを先に選び、そこから換金経路と特異点の位置を判定する。
        var order=new List<(int x,int z)>{(size/2+shippingOffset.x,size/2+shippingOffset.z),
            (size/4+shippingOffset.x,size/2+shippingOffset.z),(size-size/4-1+shippingOffset.x,size/2+shippingOffset.z)};
        order.AddRange(Enumerable.Range(0,size).SelectMany(x=>Enumerable.Range(0,map.GetLength(1)).Select(z=>(x,z)))
            .OrderBy(p=>Hash(unchecked((uint)seed ^ (uint)p.x*73856093u ^ (uint)p.z*19349663u))));
        foreach(var point in order.Distinct())
        {
            if(point.x<0 || point.z<0 || point.x>=size || point.z>=map.GetLength(1) || !map[point.x,point.z].Empty) continue;
            if((long)woodDistance[point.x,point.z]-2>maximumBelts) continue;
            var anchor=(x:point.x-shippingOffset.x,z:point.z-shippingOffset.z);
            if(result.Any(c=>Math.Abs(c.X-anchor.x)<12 && Math.Abs(c.Z-anchor.z)<12)) continue;
            var candidate=Check(map,footprint,shippingOffset,anchor.x,anchor.z,balance,extractorPrice,beltPrice,resources);
            if(candidate!=null) result.Add(candidate);
            if(result.Count==3) break;
        }
        return result.AsReadOnly();
    }
    private static Candidate Check(Terrain[,] map,CellFootprintDomain footprint,(int x,int z) offset,int x,int z,
        long balance,long extractorPrice,long beltPrice,Dictionary<ResourceTypeDomain,List<(int x,int z)>> resources)
    {
        var width=map.GetLength(0);var depth=map.GetLength(1);
        bool Inside((int x,int z) p)=>p.x>=0 && p.z>=0 && p.x<width && p.z<depth;
        var core=footprint.Coordinates(x,z,0).ToHashSet();var shipping=(x:x+offset.x,z:z+offset.z);
        if(!Inside(shipping) || !map[shipping.x,shipping.z].Empty || core.Contains(shipping) ||
            core.Any(p=>!Inside(p) || !map[p.x,p.z].Natural)) return null;
        bool Free((int x,int z) p)=>Inside(p) && map[p.x,p.z].Empty && !core.Contains(p);
        Dictionary<(int x,int z),(int x,int z)> parents=null;
        var visited=new HashSet<(int,int)>();
        // 出荷セルを通り抜ける経路を使わず、同じ建設可能な連結成分で進行できること。
        foreach(var direction in Directions)
        {
            var entry=(x:shipping.x+direction.x,z:shipping.z+direction.z);
            if(!Free(entry) || visited.Contains(entry)) continue;
            var connected=new Dictionary<(int x,int z),(int x,int z)>{{shipping,shipping},{entry,shipping}};
            var queue=new Queue<(int x,int z)>();queue.Enqueue(entry);
            while(queue.Count>0)
            {
                var p=queue.Dequeue();
                foreach(var d in Directions)
                {
                    var next=(x:p.x+d.x,z:p.z+d.z);
                    if(!Free(next) || connected.ContainsKey(next)) continue;
                    connected[next]=p;queue.Enqueue(next);
                }
            }
            visited.UnionWith(connected.Keys);
            if(parents==null || parents.Count<connected.Count) parents=connected;
        }
        if(parents==null || !core.Any(p=>Directions.Any(d=>
            (p.x+d.x,p.z+d.z)!=shipping && !core.Contains((p.x+d.x,p.z+d.z)) && parents.ContainsKey((p.x+d.x,p.z+d.z))))) return null;
        List<(int x,int z)> Route((int x,int z) miner,(int x,int z) direction)
        {
            var first=(x:miner.x+direction.x,z:miner.z+direction.z);var next=(x:first.x+direction.x,z:first.z+direction.z);
            if(first==shipping || !parents.ContainsKey(first) || !parents.ContainsKey(next)) return null;
            var route=new List<(int x,int z)>{first};
            while(next!=shipping)
            {
                if(next==miner || next==first) return null;
                route.Add(next);next=parents[next];
            }
            return route;
        }
        // 採取設備と、その出力を取り出して先へ送る2マスが既存の空き地に収まること。
        bool Access((int x,int z) raw)
        {
            if(core.Contains(raw)) return false;
            foreach(var side in Directions)
            {
                var miner=(x:raw.x+side.x,z:raw.z+side.z);
                if(miner==shipping || !parents.ContainsKey(miner)) continue;
                foreach(var d in Directions)
                {
                    if(Route(miner,d)!=null) return true;
                }
            }
            return false;
        }
        if(Required.Any(t=>!resources[t].Any(Access))) return null;
        Candidate best=null;
        foreach(var wood in resources[ResourceTypeDomain.Wood].Where(r=>!core.Contains(r)))
        foreach(var side in Directions)
        {
            var miner=(x:wood.x+side.x,z:wood.z+side.z);
            if(miner==shipping || !parents.ContainsKey(miner)) continue;
            foreach(var d in Directions)
            {
                var belts=Route(miner,d);
                if(belts==null || beltPrice>0 && belts.Count>(balance-extractorPrice)/beltPrice || best!=null && best.Belts.Count<=belts.Count) continue;
                best=new Candidate(x,z,shipping,miner,wood,belts);
            }
        }
        if(best==null) return null;
        var busy=core.Concat(best.Belts).Append(best.Extractor).Append(shipping).ToHashSet();
        bool Pad(int px,int pz,int w,int h,HashSet<(int,int)> extra)
        {
            for(var dx=0;dx<w;dx++) for(var dz=0;dz<h;dz++)
                if(!parents.ContainsKey((px+dx,pz+dz)) || busy.Contains((px+dx,pz+dz)) || extra.Contains((px+dx,pz+dz))) return false;
            return true;
        }
        var reserved=new HashSet<(int,int)>();var found=false;
        foreach(var p in parents.Keys)
            if(Pad(p.x,p.z,5,5,reserved))
            { for(var dx=0;dx<5;dx++) for(var dz=0;dz<5;dz++) reserved.Add((p.x+dx,p.z+dz));found=true;break; }
        if(!found || !parents.Keys.Any(p=>Pad(p.x,p.z,4,4,reserved))) return null;
        return best;
    }
    private static uint Hash(uint value)
    { unchecked {value^=value>>16;value*=0x7feb352d;value^=value>>15;value*=0x846ca68b;return value^(value>>16);} }
}
