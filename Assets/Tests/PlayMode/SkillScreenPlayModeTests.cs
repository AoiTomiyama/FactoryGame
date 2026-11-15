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
        Assert.That(screen.IsNodeVisible("basic-research")); Assert.That(screen.IsNodeVisible("wood-extraction-1"),Is.False);
        Assert.That(screen.NodeButton("basic-research").colors.normalColor,Is.EqualTo(hud.SelectedTabColor));
        screen.Select("basic-research"); game.Wallet.TryReserveExpense(95,out var held);
        Assert.That(screen.Details.Interactable,Is.False); Assert.That(screen.IsNodeVisible("basic-research"));
        Assert.That(screen.NodeButton("basic-research").colors.normalColor,Is.EqualTo(hud.IdleTabColor));
        held.TryCancel(); screen.PurchaseButton.onClick.Invoke();
        Assert.That(game.Skills.IsOwned("basic-research")); Assert.That(screen.IsNodeVisible("wood-extraction-1"));
        Assert.That(screen.IsNodeVisible(TerrainClearingApplication.SkillUid),Is.False);
        screen.Select("wood-extraction-1"); screen.PurchaseButton.onClick.Invoke();
        Assert.That(screen.IsNodeVisible("wood-extraction-2"));
        Assert.That(screen.NodeButton("wood-extraction-2").colors.normalColor,Is.EqualTo(hud.IdleTabColor));
        game.Wallet.TryReserveIncome(10000,out var funds); funds.TryCommit();
        for(var tier=2;tier<=7;tier++) { screen.Select("wood-extraction-"+tier); screen.PurchaseButton.onClick.Invoke(); }
        foreach(var size in new[] {new Vector2Int(1600,900),new Vector2Int(1366,768)})
        {
            Screen.SetResolution(size.x,size.y,false); await UniTask.DelayFrame(4); Canvas.ForceUpdateCanvases();
            screen.Select("wood-extraction-8"); await UniTask.DelayFrame(3); Canvas.ForceUpdateCanvases();
            var canvas = (RectTransform)hud.transform;
            Assert.That(Inside(canvas,screen.ScreenRoot)); Assert.That(Inside(canvas,(RectTransform)screen.CloseButton.transform));
            Assert.That(Inside(screen.TreeScroll.viewport,(RectTransform)screen.NodeButton("wood-extraction-8").transform),Is.True,"deepest visible candidate " + size);
            Assert.That(screen.Description.preferredHeight,Is.LessThanOrEqualTo(screen.Description.rectTransform.rect.height+1));
            Assert.That(Inside(screen.PurchaseButton.GetComponentInParent<ScrollRect>().viewport,(RectTransform)screen.PurchaseButton.transform),Is.True);
            Assert.That(screen.TreeScroll.verticalScrollbar.gameObject.activeInHierarchy);
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
