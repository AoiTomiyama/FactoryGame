#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture,Category("StartSite")]
public sealed class StartSiteRulesTests
{
    [TestCase(14,1)] [TestCase(42,3)] [TestCase(100,3)]
    public void SeedsProduceDeterministicBoundedNonoverlappingCandidates(int size,int count)
    {
        foreach(var seed in Enumerable.Range(-64,128).Concat(new[]{int.MinValue,int.MaxValue}))
        {
            var sites=StartSiteRulesDomain.Candidates(size,seed,(_,_)=>true);
            Assert.That(sites.Count,Is.EqualTo(count));
            Assert.That(sites,Is.EqualTo(StartSiteRulesDomain.Candidates(size,seed,(_,_)=>true)));
            var occupied=new HashSet<(int,int)>();
            foreach(var p in sites) foreach(var tile in StartSiteRulesDomain.Area(p.x,p.z))
            {
                Assert.That(tile.x>=0 && tile.z>=0 && tile.x<size && tile.z<size);
                Assert.That(occupied.Add(tile),Is.True,"candidate areas stay independent");
            }
        }
    }
    [Test] public void ProtectedEquipmentIsOutsideEveryCandidateArea()
    {
        var sites=StartSiteRulesDomain.Candidates(100,3,(x,z)=>x!=50 || z!=50);
        Assert.That(sites.Count,Is.EqualTo(3));
        Assert.That(sites.All(p=>!StartSiteRulesDomain.Area(p.x,p.z).Contains((50,50))));
        Assert.That(StartSiteRulesDomain.Candidates(100,0,(_,_)=>false),Is.Empty);
    }
}

[TestFixture,Category("StartSite")]
public sealed class StartSiteGenerationTests : FactoryEditModeScope
{
    protected override bool NeedsMainScene=>true;
    [TestCase(0,0f)] [TestCase(int.MinValue,1f)] [TestCase(int.MaxValue,0f)]
    public void DenseOrEmptySeedHasConnectedRoutesAndConstructionSpace(int seed,float threshold)
    {
        var generator=TestScenes.NewObject("StartSiteGenerator").AddComponent<GridFieldGeneratorAdapter>();
        EditorUtility.CopySerialized(TestScenes.Find<GridFieldGeneratorAdapter>(),generator);
        var serialized=new SerializedObject(generator);serialized.FindProperty("gridSize").intValue=42;
        serialized.FindProperty("seedValue").intValue=seed;
        var props=serialized.FindProperty("propPrefabs");
        for(var i=0;i<props.arraySize;i++) props.GetArrayElementAtIndex(i).FindPropertyRelative("threshold").floatValue=threshold;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var field=TestScenes.NewObject("StartSiteField").transform;generator.GenerateGrid(field);
        Assert.That(generator.StartSites.Count,Is.EqualTo(3));
        var cells=field.GetComponentsInChildren<CellBaseAdapter>().ToDictionary(c=>(Mathf.RoundToInt(c.transform.position.x),Mathf.RoundToInt(c.transform.position.z)));
        Assert.That(cells.Count,Is.EqualTo(42*42));
        foreach(var p in generator.StartSites)
        {
            var raw=new[]{(p.x,p.y-7,ResourceTypeDomain.Wood),(p.x-4,p.y-2,ResourceTypeDomain.Iron),
                (p.x+4,p.y-2,ResourceTypeDomain.Copper),(p.x-4,p.y+4,ResourceTypeDomain.Coal),(p.x+4,p.y+4,ResourceTypeDomain.Stone)};
            foreach(var r in raw) Assert.That(((ResourceCellAdapter)cells[(r.Item1,r.Item2)]).ResourceTypeDomain,Is.EqualTo(r.Item3));
            var buildable=StartSiteRulesDomain.Area(p.x,p.y).Where(t=>cells[t] is EmptyCellAdapter &&
                !(Mathf.Abs(t.x-p.x)<=2 && Mathf.Abs(t.z-p.y)<=2) && t!=(p.x,p.y-3)).ToHashSet();
            var queue=new Queue<(int x,int z)>();var reached=new HashSet<(int,int)>();queue.Enqueue((p.x,p.y-6));
            while(queue.Count>0)
            {
                var q=queue.Dequeue();if(!buildable.Contains(q) || !reached.Add(q)) continue;
                foreach(var d in new[]{(0,1),(1,0),(0,-1),(-1,0)}) queue.Enqueue((q.x+d.Item1,q.z+d.Item2));
            }
            Assert.That(reached.Count,Is.EqualTo(buildable.Count),"empty construction area is connected around the core");
            foreach(var r in raw) Assert.That(reached.Contains((r.Item1,r.Item2+(r.Item3==ResourceTypeDomain.Wood?1:-1))));
            foreach(var tile in new CellFootprintDomain(3,2).Coordinates(p.x+3,p.y-6,0)
                        .Concat(new CellFootprintDomain(2,2).Coordinates(p.x-5,p.y-6,0)))
                Assert.That(reached.Contains(tile),"advanced crafter and depot pads are connected");
        }
        Assert.That(field.GetComponentsInChildren<SingularityCellAdapter>(),Is.Empty);
        Assert.That(field.GetComponentsInChildren<ShippingCellAdapter>(),Is.Empty);
    }
}
#endif
