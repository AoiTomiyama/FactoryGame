#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[TestFixture, Category("ResourceExpansion")]
public sealed class ResourceExtractionPlayModeTests : FactoryPlayModeScope
{
    [UnityTest] public IEnumerator NewRawResourcesAreMinedAndTransferredToStorage() => Mine().ToCoroutine();
    private static async UniTask Mine()
    {
        var pool = new GameObject("MiningPool").AddComponent<ResourceItemObjectPoolAdapter>();
        typeof(ResourceItemObjectPoolAdapter).GetField("resourceDatabase", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(pool, AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset"));
        Time.timeScale = 10;
        foreach (var type in new[] { ResourceTypeDomain.Iron, ResourceTypeDomain.Copper, ResourceTypeDomain.Coal })
        {
            var grid = new GameObject("MiningRoute").AddComponent<GridFieldDatabaseAdapter>();
            T Cell<T>(string name, int z, float angle = 0) where T : CellBaseAdapter => Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<T>("Assets/Prefabs/Field/General/F_" + name + "Cell.prefab"),
                new Vector3(4, 0, z), Quaternion.Euler(0, angle, 0), grid.transform);
            var resource = Cell<ResourceCellAdapter>("Resource" + type, 6);
            var extractor = Cell<ExtractorCellAdapter>("Extractor" + type, 5);
            var exporter = Cell<ExportConveyorCellAdapter>("ExportConveyor", 4, 180);
            var conveyor = Cell<ConveyorCellAdapter>("Conveyor", 3, 180);
            var storage = Cell<StorageCellAdapter>("Storage", 2);
            grid.InitializeCells(10);
            extractor.InitializeSystem(); storage.InitializeSystem(); conveyor.InitializeSystem(); exporter.InitializeSystem();
            var deadline = Time.realtimeSinceStartup + 5;
            while (storage.CurrentLoad < 1 && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Assert.That(storage.CurrentLoad, Is.GreaterThan(0), "採取と実搬送: " + type);
            Assert.That(storage.StoredResourceType, Is.EqualTo(type));
            var before = extractor.CurrentLoad;
            resource.gameObject.SetActive(false); await UniTask.Delay(200, ignoreTimeScale: true);
            // 中断後に新しい生産が起きないことを、搬出停止後の在庫で確認する。
            exporter.gameObject.SetActive(false); await UniTask.Delay(200, ignoreTimeScale: true);
            var stopped = extractor.CurrentLoad; await UniTask.Delay(200, ignoreTimeScale: true);
            Assert.That(extractor.CurrentLoad, Is.EqualTo(stopped));
            Object.Destroy(grid.gameObject); await UniTask.Yield(); await UniTask.Yield();
        }
    }
}

[TestFixture, Category("ResourceExpansion")]
public sealed class ResourceExpansionHudPlayModeTests : FactoryPlayModeScope
{
    protected override bool NeedsMainScene => true;
    [UnityTest] public IEnumerator MiningChoicesAndFinalRecipeFitBothPcSizes() => VerifyHud().ToCoroutine();
    private static async UniTask VerifyHud()
    {
        var panel = Object.FindAnyObjectByType<EconomyPanelAdapter>();
        var layout = Object.FindAnyObjectByType<HudLayoutAdapter>();
        var canvas = layout.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<CanvasScaler>().enabled = false;
        var bar = Object.FindAnyObjectByType<OperationBarAdapter>();
        var barCanvas = bar.GetComponentInParent<Canvas>();
        barCanvas.renderMode = RenderMode.WorldSpace; barCanvas.GetComponent<CanvasScaler>().enabled = false;
        foreach (var size in new[] { new Vector2(1600,900),new Vector2(1366,768) })
        {
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(size.x*720/size.y,720);
            ((RectTransform)barCanvas.transform).sizeDelta = new Vector2(size.x*720/size.y,720);
            panel.ShowPage(0); await UniTask.DelayFrame(5);
            Object.FindAnyObjectByType<CellPlacerAdapter>().SetSelectedCellType(CellTypeDomain.AdvancedCrafter);
            await UniTask.DelayFrame(4); Canvas.ForceUpdateCanvases();
            Assert.That(panel.PlacementText,Does.Contain("板材").And.Contain("鉄インゴット").And.Contain("不足2"));
            Assert.That(bar.PlacementInfo.preferredHeight,Is.LessThanOrEqualTo(bar.PlacementInfo.rectTransform.rect.height+1));
            var menu = layout.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).Single(b => b.name == "ExtractorWoodSubMenuButton");
            var choices = layout.GetComponentsInChildren<CellSelectButtonUIAdapter>(true).Where(b => b.name.EndsWith("Choice")).ToArray();
            if (!choices.Any(b => b.gameObject.activeSelf)) menu.GetComponentInChildren<Button>(true).onClick.Invoke();
            await UniTask.DelayFrame(5); Canvas.ForceUpdateCanvases();
            Assert.That(choices.Length,Is.EqualTo(5));
            foreach (var choice in choices)
            {
                Assert.That(choice.gameObject.activeInHierarchy);
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(layout.Body.viewport,(RectTransform)choice.transform);
                Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(layout.Body.viewport.rect.xMin-1));
                Assert.That(bounds.max.x,Is.LessThanOrEqualTo(layout.Body.viewport.rect.xMax+1));
                foreach (var text in choice.GetComponentsInChildren<UnityEngine.UI.Text>())
                    Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),text.text);
            }
            panel.ShowPage(1); await UniTask.DelayFrame(5); Canvas.ForceUpdateCanvases();
            foreach (var text in panel.RecipeContent.GetComponentsInChildren<UnityEngine.UI.Text>())
                Assert.That(text.preferredHeight,Is.LessThanOrEqualTo(text.rectTransform.rect.height+1),text.text);
            var last = panel.RecipeContent.GetComponentsInChildren<RecipeResourceRowAdapter>().Last();
            layout.Body.verticalNormalizedPosition = 0; await UniTask.DelayFrame(4); Canvas.ForceUpdateCanvases();
            var end = RectTransformUtility.CalculateRelativeRectTransformBounds(layout.Body.viewport,(RectTransform)last.transform);
            Assert.That(end.min.y,Is.GreaterThanOrEqualTo(layout.Body.viewport.rect.yMin-1));
            Assert.That(end.max.y,Is.LessThanOrEqualTo(layout.Body.viewport.rect.yMax+1));
        }
    }
}
#endif
