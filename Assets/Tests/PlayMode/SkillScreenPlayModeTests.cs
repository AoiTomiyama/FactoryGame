#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

[TestFixture, Category("UI")]
public sealed class SkillScreenPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    protected override int? FixedTerrainSeed => 423794866;
    private static int Subscribers(CurrencyWalletApplication wallet) =>
        (typeof(CurrencyWalletApplication).GetField("Changed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wallet) as Delegate)?.GetInvocationList().Length ?? 0;
    private static bool Inside(RectTransform viewport,RectTransform child)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        return corners.Select(viewport.InverseTransformPoint).All(p => p.x >= viewport.rect.xMin-1 && p.x <= viewport.rect.xMax+1 &&
            p.y >= viewport.rect.yMin-1 && p.y <= viewport.rect.yMax+1);
    }
    [UnityTest] public IEnumerator FullScreenPurchaseInputVisibilityAndPcLayout() => Verify().ToCoroutine();
    private static async UniTask Verify()
    {
        var game = TestScenes.Find<GameInitializerAdapter>(); var panel = TestScenes.Find<EconomyPanelAdapter>();
        var hud = TestScenes.Find<HudLayoutAdapter>(); var screen = panel.SkillScreen;
        var cursor = TestScenes.Find<PlayerCursorBehaviourAdapter>(); var placer = TestScenes.Find<CellPlacerAdapter>();
        var camera = TestScenes.Find<CameraControllerAdapter>(); var view = TestScenes.Find<CellStatusViewAdapter>();
        var core = TestScenes.Find<SingularityCellAdapter>(); view.UpdateUIStatusWindow(core);
        var selectedPage = panel.SelectedPage; var expanded = panel.IsExpanded; var listeners = Subscribers(game.Wallet);
        var empty = GridFieldDatabaseAdapter.Instance.Owners.OfType<EmptyCellAdapter>().First();
        placer.SetSelectedCellType(CellTypeDomain.Conveyor); placer.SelectGrid(empty.gameObject); placer.PointerBegin(); placer.PointerDrag();
        panel.PageButton(2).onClick.Invoke();
        Assert.That(screen.IsOpen); Assert.That(placer.PlacementEnabled,Is.False); Assert.That(placer.PreviewCount,Is.Zero);
        Assert.That(hud.GameplayRoot.GetComponent<CanvasGroup>().alpha,Is.Zero);
        Assert.That(Subscribers(game.Wallet),Is.EqualTo(listeners+1)); Assert.That(Time.timeScale,Is.EqualTo(1));
        var rotation = cursor.transform.rotation; cursor.RotatePlacement(); Assert.That(cursor.transform.rotation,Is.EqualTo(rotation));
        Assert.That(camera.SetFirstPerson(true),Is.False);
        Assert.That(screen.IsNodeVisible("basic-research")); Assert.That(screen.IsNodeVisible("extraction-speed-1"),Is.False);
        Assert.That(screen.NodeButton("basic-research").colors.normalColor,Is.EqualTo(hud.SelectedTabColor));
        screen.Select("basic-research"); game.Wallet.TryReserveExpense(95,out var held);
        Assert.That(screen.Details.Interactable,Is.False); Assert.That(screen.IsNodeVisible("basic-research"));
        Assert.That(screen.NodeButton("basic-research").colors.normalColor,Is.EqualTo(hud.IdleTabColor));
        held.TryCancel(); screen.PurchaseButton.onClick.Invoke();
        Assert.That(game.Skills.IsOwned("basic-research"));
        Assert.That(screen.IsNodeVisible("extraction-speed-1"));
        Assert.That(screen.IsNodeVisible("basic-extraction-1"), Is.False);
        Assert.That(screen.IsNodeVisible("industrial-extraction-1"), Is.False);
        Assert.That(screen.IsNodeVisible(TerrainClearingApplication.SkillUid),Is.False);
        screen.Select("extraction-speed-1");
        Assert.That(screen.Description.text, Does.Contain("全採取設備"));
        Assert.That(screen.NodeButton("extraction-speed-1").colors.normalColor, Is.EqualTo(hud.IdleTabColor));
        game.Wallet.TryReserveIncome(10000,out var funds); funds.TryCommit();
        Assert.That(screen.NodeButton("extraction-speed-1").colors.normalColor, Is.EqualTo(hud.SelectedTabColor));
        screen.PurchaseButton.onClick.Invoke();
        Assert.That(screen.IsNodeVisible("extraction-speed-2"));
        foreach (var requirement in game.Deliveries.Catalog.Stages.Take(2).SelectMany(stage => stage.Requirements))
        {
            var missing = requirement.Amount - game.Deliveries.Current.Count(requirement.Resource);
            if (missing <= 0) continue;
            Assert.That(game.Deliveries.TryReserve(requirement.Resource, missing, out var delivery));
            Assert.That(delivery.TryCommit());
        }
        Assert.That(screen.IsNodeVisible("basic-extraction-1"));
        Assert.That(screen.IsNodeVisible("industrial-extraction-1"));
        for (var tier = 1; tier <= 2; tier++)
        {
            screen.Select("industrial-extraction-" + tier);
            screen.PurchaseButton.onClick.Invoke();
        }
        var tree = screen.TreeScroll;
        await UniTask.DelayFrame(2);
        Canvas.ForceUpdateCanvases();
        var previousViewportOffset = tree.viewport.offsetMax;
        tree.viewport.offsetMax -= Vector2.right * 160;
        await UniTask.DelayFrame(2);
        Canvas.ForceUpdateCanvases();
        tree.horizontalNormalizedPosition = 0;
        var visible = screen.NodeButtons.Values.Where(button => button.gameObject.activeSelf &&
            Inside(tree.viewport, (RectTransform)button.transform) && button.name != "SkillNode_industrial-extraction-2" &&
            tree.viewport.InverseTransformPoint(button.transform.position).x > tree.viewport.rect.center.x + 30).First();
        var originalX = tree.content.anchoredPosition.x;
        visible.onClick.Invoke();
        await UniTask.DelayFrame(2);
        Canvas.ForceUpdateCanvases();
        Assert.That(tree.content.anchoredPosition.x, Is.EqualTo(originalX).Within(.01f), "selecting a visible node leaves scroll in place");
        var rightmost = screen.NodeButtons.Values.Where(button => button.gameObject.activeSelf)
            .OrderByDescending(button => tree.viewport.InverseTransformPoint(button.transform.position).x).First();
        Assert.That(Inside(tree.viewport, (RectTransform)rightmost.transform), Is.False);
        rightmost.onClick.Invoke();
        await UniTask.DelayFrame(2);
        Canvas.ForceUpdateCanvases();
        Assert.That(Inside(tree.viewport, (RectTransform)rightmost.transform), Is.True, "selecting a clipped node reveals it");
        tree.viewport.offsetMax = previousViewportOffset;
        foreach(var size in new[] {new Vector2Int(1600,900),new Vector2Int(1366,768)})
        {
            Screen.SetResolution(size.x,size.y,false); await UniTask.DelayFrame(4); Canvas.ForceUpdateCanvases();
            screen.Select("industrial-extraction-3"); await UniTask.DelayFrame(3); Canvas.ForceUpdateCanvases();
            var canvas = (RectTransform)hud.transform;
            Assert.That(Inside(canvas,screen.ScreenRoot)); Assert.That(Inside(canvas,(RectTransform)screen.CloseButton.transform));
            Assert.That(Inside(screen.TreeScroll.viewport,(RectTransform)screen.NodeButton("industrial-extraction-3").transform),Is.True,"deepest visible candidate " + size);
            Assert.That(screen.Description.preferredHeight,Is.LessThanOrEqualTo(screen.Description.rectTransform.rect.height+1));
            Assert.That(Inside(screen.PurchaseButton.GetComponentInParent<ScrollRect>().viewport,(RectTransform)screen.PurchaseButton.transform),Is.True);
            var treeOverflows = screen.TreeScroll.content.rect.height > screen.TreeScroll.viewport.rect.height + .01f;
            Assert.That(screen.TreeScroll.verticalScrollbar.gameObject.activeInHierarchy, Is.EqualTo(treeOverflows),
                "vertical scrollbar is needed only when the current tree exceeds the viewport");
            var position = screen.TreeScroll.content.anchoredPosition;
            game.Wallet.TryReserveIncome(1,out var extra); extra.TryCommit(); await UniTask.DelayFrame(2);
            Assert.That(Vector2.Distance(position,screen.TreeScroll.content.anchoredPosition),Is.LessThan(1),"balance refresh preserves scroll");
            foreach(var button in screen.NodeButtons.Values.Where(b => b.gameObject.activeSelf))
                Assert.That(((RectTransform)button.transform).rect.size,Is.EqualTo(new Vector2(40,40)));
        }
        screen.CloseButton.onClick.Invoke();
        Assert.That(screen.IsOpen,Is.False); Assert.That(screen.BlocksGameplayInput);
        cursor.RotatePlacement(); Assert.That(cursor.transform.rotation,Is.EqualTo(rotation));
        Assert.That(Subscribers(game.Wallet),Is.EqualTo(listeners));
        await UniTask.DelayFrame(1); Assert.That(screen.BlocksGameplayInput,Is.False);
        Assert.That(panel.SelectedPage,Is.EqualTo(selectedPage)); Assert.That(panel.IsExpanded,Is.EqualTo(expanded));
        Assert.That(view.SelectedCell,Is.SameAs(core)); Assert.That(hud.GameplayRoot.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
        for(var i=0;i<3;i++) { screen.Open(); Assert.That(Subscribers(game.Wallet),Is.EqualTo(listeners+1)); screen.Close(); }
        Assert.That(Subscribers(game.Wallet),Is.EqualTo(listeners));
    }
}
#endif
