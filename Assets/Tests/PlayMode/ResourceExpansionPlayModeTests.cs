#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
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
#endif
