#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>保存済みPrefabで加工品を実搬送し、特異点の累計まで到達することを確認する。</summary>
[TestFixture, Category("Delivery"), Category("Delivery.Transfer")]
public sealed class DeliveryTransferPlayModeTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private Scene _scene;
    private bool _muted;
    private float _volume, _timeScale;
    [UnitySetUp]
    public IEnumerator PrepareEmptyScene()
    {
        _muted = EditorUtility.audioMasterMute; _volume = AudioListener.volume; _timeScale = Time.timeScale;
        EditorUtility.audioMasterMute = true; AudioListener.volume = 0; Time.timeScale = 10;
        var previous = new Scene[SceneManager.sceneCount]; for (var i = 0; i < previous.Length; i++) previous[i] = SceneManager.GetSceneAt(i);
        _scene = SceneManager.CreateScene("DeliveryTransferTests"); SceneManager.SetActiveScene(_scene);
        foreach (var scene in previous) if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var empty = SceneManager.CreateScene("DeliveryTransferCleanup"); SceneManager.SetActiveScene(empty);
        if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        Time.timeScale = _timeScale; AudioListener.volume = _volume; EditorUtility.audioMasterMute = _muted;
    }
    [UnityTest, Description("板材と石レンガ→上位加工3×2→搬出路→特異点5×5の累計反映を4回転で確認する")]
    public IEnumerator CraftedProductReachesCumulativeDeliveryThroughAllFourRotations() => VerifyRoute().ToCoroutine();
    private static async UniTask VerifyRoute()
    {
        var grid = new GameObject("DeliveryRouteGrid").AddComponent<GridFieldDatabaseAdapter>();
        var pool = new GameObject("DeliveryRoutePool").AddComponent<ResourceItemObjectPoolAdapter>();
        Set(pool, "resourceDatabase", AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset"));
        await UniTask.Yield();
        for (var turn = 0; turn < 4; turn++)
        {
            var route = new GameObject("DeliveryRoute_" + turn).transform; route.SetParent(grid.transform);
            var rotation = Quaternion.Euler(0, turn * 90, 0);
            var crafter = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CrafterCellAdapter>("Assets/Prefabs/Field/General/F_AdvancedCrafterCell.prefab"), new Vector3(8, 0, 8), rotation, route);
            ConveyorCellAdapter Input(int port, ResourceTypeDomain type)
            {
                var cell = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ConveyorCellAdapter>("Assets/Prefabs/Field/General/F_ConveyorCell.prefab"), crafter.PortPosition(port) + crafter.PortDirection(port), rotation, route);
                var id = pool.CreateIdFromResourceData(type, 2);
                typeof(ConveyorCellAdapter).GetProperty("ResourceId", Fields).SetValue(cell, id);
                typeof(ConveyorCellAdapter).GetProperty("HasResource", Fields).SetValue(cell, true); Set(cell, "_readyToSend", true); return cell;
            }
            var left = Input(0, ResourceTypeDomain.Plank); var right = Input(1, ResourceTypeDomain.StoneBrick);
            var output = crafter.PortPosition(2); var forward = crafter.PortDirection(2);
            var exporter = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExportConveyorCellAdapter>("Assets/Prefabs/Field/General/F_ExportConveyorCell.prefab"), output + forward, rotation, route);
            var core = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SingularityCellAdapter>("Assets/Prefabs/Field/General/F_SingularityCell.prefab"), output + forward * 4 + rotation * Vector3.right, rotation, route);
            var progress = new DeliveryProgressApplication(AssetDatabase.LoadAssetAtPath<DeliveryGoalsDefinition>("Assets/Scripts/ScriptableObject/DeliveryGoals.asset").CreateCatalog());
            core.Configure(progress); grid.InitializeCells(18);
            core.InitializeSystem(); crafter.InitializeSystem(); exporter.InitializeSystem(); left.InitializeSystem(); right.InitializeSystem();
            Assert.That(crafter.Connections.Count, Is.EqualTo(3)); Assert.That(core.Connections.Count, Is.EqualTo(1));
            var deadline = Time.realtimeSinceStartup + 6;
            while (progress.Current.Count(ResourceTypeDomain.BuildingParts) < 1 && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Assert.That(progress.Current.Count(ResourceTypeDomain.BuildingParts), Is.EqualTo(1), "納品への到達: 回転 " + turn);
            Assert.That(crafter.GetInput(0).Amount, Is.Zero); Assert.That(crafter.GetInput(1).Amount, Is.Zero);
            Assert.That(crafter.ExportStorageAmount, Is.Zero); Assert.That(pool.Resources.Count, Is.Zero);
            Object.Destroy(route.gameObject); await UniTask.Yield();
        }
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
}
#endif
