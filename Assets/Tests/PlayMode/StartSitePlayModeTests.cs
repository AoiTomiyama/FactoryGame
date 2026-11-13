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
    [UnityTest] public IEnumerator MissingDefinitionFailsBeforeChangingTerrain()
    {
        var initializer = TestScenes.Find<GameInitializerAdapter>();
        var grid = GridFieldDatabaseAdapter.Instance;
        var before = grid.GetComponentsInChildren<CellBaseAdapter>().ToDictionary(c=>c,c=>c.transform.position);
        var previews = initializer.GetComponentsInChildren<LineRenderer>().ToArray();
        var bar = TestScenes.Find<OperationBarAdapter>(); var wasInteractable = bar.WalkButton.interactable;
        var edit = new UnityEditor.SerializedObject(initializer);
        edit.FindProperty("resources").objectReferenceValue = null; edit.ApplyModifiedPropertiesWithoutUndo();
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Game initialization failed during start:[\\s\\S]*GameInitializerAdapter: missing references \\[resources\\][\\s\\S]*ValidateConfiguration"));
        Assert.That(initializer.TryStartGame(), Is.False);
        Assert.That(initializer.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Failed));
        Assert.That(initializer.Wallet, Is.Null);
        Assert.That(grid.GetComponentsInChildren<CellBaseAdapter>(), Is.EquivalentTo(before.Keys));
        foreach (var pair in before) Assert.That(pair.Key.transform.position, Is.EqualTo(pair.Value));
        Assert.That(initializer.TryStartGame(), Is.False);
        yield return null;
        Assert.That(bar.WalkButton.interactable, Is.EqualTo(wasInteractable), "failure preserves the UI state for inspection");
        foreach (var preview in previews) Assert.That(preview != null, Is.True, "failure preserves the preview objects");
    }
    [UnityTest] public IEnumerator FailureAfterPlacementRejectsAnotherPlacement()
    {
        var initializer = TestScenes.Find<GameInitializerAdapter>(); var grid = GridFieldDatabaseAdapter.Instance;
        var candidate = initializer.Session.Selected;
        var outside = grid.GetComponentsInChildren<EmptyCellAdapter>().First(c =>
            Mathf.Abs(c.transform.position.x-candidate.X)>5 && Mathf.Abs(c.transform.position.z-candidate.Z)>5);
        outside.transform.position = new Vector3(-50,0,-50);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Game initialization failed during start"));
        Assert.That(initializer.TryStartGame(), Is.False);
        Assert.That(initializer.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Failed));
        var cells = grid.GetComponentsInChildren<CellBaseAdapter>();
        Assert.That(initializer.TryStartGame(), Is.False);
        Assert.That(grid.GetComponentsInChildren<CellBaseAdapter>(), Is.EquivalentTo(cells));
        Assert.That(TestScenes.Find<CellPlacerAdapter>().PlacementEnabled, Is.False);
        yield return null;
    }
    [UnityTest] public IEnumerator EmptyCandidatesRemainDisabledWhenInputBindsFirst() => VerifyEmptyBindings(true);
    [UnityTest] public IEnumerator EmptyCandidatesRemainDisabledWhenInputBindsLast() => VerifyEmptyBindings(false);
    private static IEnumerator VerifyEmptyBindings(bool bindFirst)
    {
        var original = TestScenes.Find<StartSiteSelectionAdapter>(); original.DisposePreviews();
        var view = TestScenes.NewObject("EmptyStartPresentation").AddComponent<StartSiteSelectionAdapter>();
        UnityEditor.EditorUtility.CopySerialized(original, view);
        var session = new GameStartApplication(); session.Prepare(Array.Empty<StartSiteRulesDomain.Candidate>());
        var bar = TestScenes.Find<OperationBarAdapter>(); var cursor = TestScenes.Find<PlayerCursorBehaviourAdapter>();
        var camera = TestScenes.Find<CameraControllerAdapter>(); var confirmed = false;
        if (bindFirst) bar.Configure(cursor,camera);
        view.Begin(session,new CellFootprintDomain(5,5,-2,-2),()=>confirmed=true);
        if (!bindFirst) bar.Configure(cursor,camera);
        Assert.That(bar.RotateButton.interactable || bar.ViewButton.interactable || bar.WalkButton.interactable, Is.False);
        view.Step(1); view.Confirm(); Assert.That(confirmed, Is.False);
        Assert.That(bar.PlacementInfo.text, Does.Contain("候補がありません"));
        yield return null;
    }
    [UnityTest] public IEnumerator SceneReloadOwnsFreshStateAndDisposesOldViews()
    {
        var previous = TestScenes.Find<GameInitializerAdapter>(); Assert.That(previous.TryStartGame(), Is.True);
        var wallet = previous.Wallet;
        yield return ReloadMainScene();
        var current = TestScenes.Find<GameInitializerAdapter>();
        Assert.That(current.Session.CurrentStage, Is.EqualTo(GameStartApplication.Stage.Choosing));
        Assert.That(current.Wallet, Is.Null); Assert.That(previous == null, Is.True);
        Assert.That(TestScenes.All<GameInitializerAdapter>().Length, Is.EqualTo(1));
        Assert.That(TestScenes.All<LineRenderer>().Count(l=>l.name=="StartSitePreview"), Is.EqualTo(3));
        Assert.That(current.TryStartGame(), Is.True);
        Assert.That(current.Wallet, Is.Not.SameAs(wallet));
        Assert.That(wallet.TryReserveIncome(2,out var income), Is.True); income.TryCommit();
        Assert.That(wallet.LastNotificationException, Is.Null);
        Assert.That(current.Wallet.Current.Balance, Is.EqualTo(100));
    }
    private static async UniTask StartAt(int index)
    {
        var initializer=Object.FindAnyObjectByType<GameInitializerAdapter>();
        var placer=Object.FindAnyObjectByType<CellPlacerAdapter>();
        var bar=Object.FindAnyObjectByType<OperationBarAdapter>();
        var hud=Object.FindAnyObjectByType<HudLayoutAdapter>(FindObjectsInactive.Include);
        Assert.That(initializer.Session.Candidates.Count,Is.EqualTo(3));Assert.That(initializer.Session.CurrentStage == GameStartApplication.Stage.Choosing);
        Assert.That(initializer.Wallet,Is.Null);Assert.That(placer.PlacementEnabled,Is.False);
        Assert.That(hud.gameObject.activeSelf,Is.False);Assert.That(Object.FindObjectsByType<SingularityCellAdapter>(FindObjectsSortMode.None),Is.Empty);
        Assert.That(Object.FindAnyObjectByType<CameraControllerAdapter>().SetFirstPerson(true),Is.False);
        var terrain=Object.FindObjectsByType<CellBaseAdapter>(FindObjectsSortMode.None).Where(c=>c.GetComponentInParent<GridFieldDatabaseAdapter>()!=null).ToDictionary(c=>c,c=>c.transform.position);
        Assert.That(initializer.Session.TrySelect(-1),Is.False);Assert.That(Object.FindAnyObjectByType<StartSiteSelectionAdapter>().SelectAt(new Vector3(-100,0,-100)),Is.False);
        bar.ViewButton.onClick.Invoke();Assert.That(initializer.Session.SelectedIndex,Is.EqualTo(1));
        bar.RotateButton.onClick.Invoke();Assert.That(initializer.Session.SelectedIndex,Is.Zero);
        var selected=initializer.Session.Candidates[index];var position=new Vector2Int(selected.X,selected.Z);Assert.That(Object.FindAnyObjectByType<StartSiteSelectionAdapter>().SelectAt(new Vector3(position.x,0,position.y)));
        Assert.That(initializer.Session.SelectedIndex,Is.EqualTo(index));Assert.That(bar.PlacementInfo.text,Does.Contain((index+1)+" / 3"));
        foreach(var p in terrain) Assert.That(p.Key!=null && p.Key.transform.position==p.Value,"selection leaves every terrain cell untouched");
        var plan=initializer.Session.Candidates[index];
        bar.WalkButton.onClick.Invoke();await UniTask.DelayFrame(4);
        Assert.That(initializer.Session.CurrentStage == GameStartApplication.Stage.Running);Assert.That(initializer.Session.CurrentStage == GameStartApplication.Stage.Choosing,Is.False);Assert.That(initializer.TryStartGame(),Is.False);
        Assert.That(initializer.Wallet.Current.Balance,Is.EqualTo(100));Assert.That(hud.gameObject.activeSelf);Assert.That(placer.PlacementEnabled);
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
        var sale=new Vector2Int(plan.Shipping.x,plan.Shipping.z);
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
        Assert.That(initializer.Wallet.Current.Balance,Is.EqualTo(remaining));
        var end=Time.realtimeSinceStartup+8;
        while(initializer.Wallet.Current.Balance==remaining && Time.realtimeSinceStartup<end) await UniTask.Yield();
        Assert.That(initializer.Wallet.Current.Balance,Is.GreaterThan(remaining),"chosen site's existing wood route earns real income");
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
