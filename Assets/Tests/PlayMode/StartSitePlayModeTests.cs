#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[TestFixture,Category("StartSite")]
public sealed class StartSitePlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene=>true;
    protected override bool ChooseDefaultStart=>false;
    [UnityTest] public IEnumerator FirstCandidateStartsWithRealIncome()=>StartAt(0).ToCoroutine();
    [UnityTest] public IEnumerator SecondCandidateStartsWithRealIncome()=>StartAt(1).ToCoroutine();
    [UnityTest] public IEnumerator ThirdCandidateStartsWithRealIncome()=>StartAt(2).ToCoroutine();
    private static async UniTask StartAt(int index)
    {
        var generator=Object.FindAnyObjectByType<GridFieldGeneratorAdapter>();
        var placer=Object.FindAnyObjectByType<CellPlacerAdapter>();
        var bar=Object.FindAnyObjectByType<OperationBarAdapter>();
        var hud=Object.FindAnyObjectByType<HudLayoutAdapter>(FindObjectsInactive.Include);
        Assert.That(generator.StartSites.Count,Is.EqualTo(3));Assert.That(generator.IsChoosingStart);
        Assert.That(generator.Wallet,Is.Null);Assert.That(placer.PlacementEnabled,Is.False);
        Assert.That(hud.gameObject.activeSelf,Is.False);Assert.That(Object.FindObjectsByType<SingularityCellAdapter>(FindObjectsSortMode.None),Is.Empty);
        Assert.That(Object.FindAnyObjectByType<CameraControllerAdapter>().SetFirstPerson(true),Is.False);
        var terrain=Object.FindObjectsByType<CellBaseAdapter>(FindObjectsSortMode.None).Where(c=>c.GetComponentInParent<GridFieldDatabaseAdapter>()!=null).ToDictionary(c=>c,c=>c.transform.position);
        Assert.That(generator.SelectStartSite(-1),Is.False);Assert.That(generator.SelectStartSiteAt(new Vector3(-100,0,-100)),Is.False);
        bar.ViewButton.onClick.Invoke();Assert.That(generator.SelectedStartSite,Is.EqualTo(1));
        bar.RotateButton.onClick.Invoke();Assert.That(generator.SelectedStartSite,Is.Zero);
        var position=generator.StartSites[index];Assert.That(generator.SelectStartSiteAt(new Vector3(position.x,0,position.y)));
        Assert.That(generator.SelectedStartSite,Is.EqualTo(index));Assert.That(bar.PlacementInfo.text,Does.Contain((index+1)+" / 3"));
        foreach(var p in terrain) Assert.That(p.Key!=null && p.Key.transform.position==p.Value,"selection leaves every terrain cell untouched");
        var plan=generator.StartCandidates[index];
        bar.WalkButton.onClick.Invoke();await UniTask.DelayFrame(4);
        Assert.That(generator.HasStarted);Assert.That(generator.IsChoosingStart,Is.False);Assert.That(generator.ConfirmStartSite(),Is.False);
        Assert.That(generator.Wallet.Current.Balance,Is.EqualTo(100));Assert.That(hud.gameObject.activeSelf);Assert.That(placer.PlacementEnabled);
        foreach(var p in terrain)
            if(!(Mathf.Abs(p.Value.x-position.x)<=2 && Mathf.Abs(p.Value.z-position.y)<=2) && p.Value!=new Vector3(position.x,0,position.y-3))
                Assert.That(p.Key!=null && p.Key.transform.position==p.Value,"confirmation changes only the two equipment footprints");
        var after=Object.FindObjectsByType<CellBaseAdapter>(FindObjectsSortMode.None).Where(c=>c.GetComponentInParent<GridFieldDatabaseAdapter>()!=null).ToArray();
        var added=after.Where(c=>!terrain.ContainsKey(c)).ToArray();
        Assert.That(added.Length,Is.EqualTo(2),"initial placement creates exactly two cells");
        Assert.That(added.Count(c=>c is SingularityCellAdapter),Is.EqualTo(1));
        Assert.That(added.Count(c=>c is ShippingCellAdapter),Is.EqualTo(1));
        var grid=GridFieldDatabaseAdapter.Instance;
        Assert.That(grid.Owners.OfType<SingularityCellAdapter>().Single().transform.position,Is.EqualTo(new Vector3(position.x,0,position.y)));
        Assert.That(grid.Owners.OfType<ShippingCellAdapter>().Count(),Is.EqualTo(1));
        Assert.That(bar.RotateButton.GetComponentInChildren<Text>().text,Does.Contain("回転"));
        var sale=generator.ShippingPreviewPosition(position);
        void Place(CellTypeDomain type,int x,int z,Vector3 forward)
        {
            placer.transform.rotation=Quaternion.LookRotation(forward);placer.SetSelectedCellType(type);
            placer.SelectGrid(grid.GetCell(x,z).gameObject);placer.PointerBegin();placer.PointerDrag();placer.PointerEnd();
            Assert.That(placer.LastPlacedCount,Is.EqualTo(1));
        }
        Place(CellTypeDomain.ExtractorWood,plan.Extractor.x,plan.Extractor.z,new Vector3(plan.Wood.x-plan.Extractor.x,0,plan.Wood.z-plan.Extractor.z));
        for(var i=0;i<plan.Belts.Count;i++)
        {
            var current=plan.Belts[i];var next=i+1<plan.Belts.Count?plan.Belts[i+1]:(sale.x,sale.y);
            Place(CellTypeDomain.Conveyor,current.x,current.z,new Vector3(next.Item1-current.x,0,next.Item2-current.z));
        }
        var remaining=100-20-plan.Belts.Count*5;
        Assert.That(generator.Wallet.Current.Balance,Is.EqualTo(remaining));
        var end=Time.realtimeSinceStartup+8;
        while(generator.Wallet.Current.Balance==remaining && Time.realtimeSinceStartup<end) await UniTask.Yield();
        Assert.That(generator.Wallet.Current.Balance,Is.GreaterThan(remaining),"chosen site's existing wood route earns real income");
    }
    [UnityTest] public IEnumerator CandidateControlsFitBothPcSizes()=>Fit().ToCoroutine();
    private static async UniTask Fit()
    {
        var bar=Object.FindAnyObjectByType<OperationBarAdapter>();var canvas=bar.GetComponentInParent<Canvas>();
        canvas.renderMode=RenderMode.WorldSpace;canvas.GetComponent<CanvasScaler>().enabled=false;
        foreach(var size in new[]{new Vector2(1600,900),new Vector2(1366,768)})
        {
            ((RectTransform)canvas.transform).sizeDelta=new Vector2(size.x*720/size.y,720);
            await UniTask.DelayFrame(4);Canvas.ForceUpdateCanvases();
            foreach(var text in bar.Bar.GetComponentsInChildren<Text>().Where(t=>!string.IsNullOrEmpty(t.text)))
            {
                Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),text.text+" "+size);
                var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds((RectTransform)canvas.transform,text.rectTransform);
                var rect=((RectTransform)canvas.transform).rect;
                Assert.That(bounds.min.x>=rect.xMin-1 && bounds.max.x<=rect.xMax+1 && bounds.min.y>=rect.yMin-1 && bounds.max.y<=rect.yMax+1);
            }
        }
    }
}
#endif
