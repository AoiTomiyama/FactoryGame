#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Cysharp.Threading.Tasks;

/// <summary>搬送の各境界を独立したプール・Sceneで実行する。</summary>
[TestFixture, Category("Transfers")]
public sealed class ResourceTransferPlayModeTests : FactoryPlayModeScope
{
    private ResourceItemObjectPoolAdapter _pool;
    [UnitySetUp] public IEnumerator CreatePool()
    {
        _pool = new GameObject("TransferTestPool").AddComponent<ResourceItemObjectPoolAdapter>();
        typeof(ResourceItemObjectPoolAdapter).GetField("resourceDatabase", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_pool, AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset"));
        typeof(ResourceItemObjectPoolAdapter).GetField("transferSecond", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_pool, .8f);
        yield return null;
    }
    [UnityTest] public IEnumerator NonCellEndpointsAndIdentity() => ResourceTransferAssertions.VerifyNonCellEndpoints(_pool).ToCoroutine();
    [UnityTest] public IEnumerator PresentationLifetime() => ResourceTransferAssertions.VerifyPresentationLifetime(_pool).ToCoroutine();
    [UnityTest] public IEnumerator MissingVisualRecovery() => ResourceTransferAssertions.VerifyMissingVisualRecovery(_pool).ToCoroutine();
    [UnityTest] public IEnumerator CrossingMissingVisualRecovery() => ResourceTransferAssertions.VerifyCrossingMissingVisualRecovery(_pool).ToCoroutine();
    [UnityTest] public IEnumerator ExportMissingVisualRecovery() => ResourceTransferAssertions.VerifyExportPresentationRecovery(_pool).ToCoroutine();
    [UnityTest] public IEnumerator ReusableVisualHandoff() => ResourceTransferAssertions.VerifyReusableHandoff(_pool).ToCoroutine();
    [UnityTest] public IEnumerator SuccessfulTransfer() => ResourceTransferAssertions.VerifySuccess(_pool).ToCoroutine();
    [UnityTest] public IEnumerator DestinationDisconnect() => ResourceTransferAssertions.VerifyDestinationDisconnect(_pool).ToCoroutine();
    [UnityTest] public IEnumerator SourceDeletion() => ResourceTransferAssertions.VerifySourceDeletion(_pool).ToCoroutine();
    [UnityTest] public IEnumerator CrossingSuccess() => ResourceTransferAssertions.VerifyCrossingSuccess(_pool).ToCoroutine();
    [UnityTest] public IEnumerator CrossingDeletion() => ResourceTransferAssertions.VerifyCrossingDeletion(_pool).ToCoroutine();
    [UnityTest] public IEnumerator CrossingCancellation() => ResourceTransferAssertions.VerifyCrossingCancellation(_pool).ToCoroutine();
    [UnityTest] public IEnumerator CrossingReconnect() => ResourceTransferAssertions.VerifyCrossingReconnect(_pool).ToCoroutine();
    [UnityTest] public IEnumerator PoolCanBeDestroyedBeforeHeldConveyorCleanup()
    {
        var grid = new GameObject("PoolLifetimeGrid").AddComponent<GridFieldDatabaseAdapter>();
        var conveyor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ConveyorCellAdapter>("Assets/Prefabs/Field/General/F_ConveyorCell.prefab"),
            new Vector3(1,0,1),Quaternion.identity,grid.transform);
        grid.InitializeCells(4); conveyor.InitializeSystem();
        var id = _pool.CreateIdFromResourceData(ResourceTypeDomain.Iron,1);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ConveyorCellAdapter).GetProperty("ResourceId",flags).SetValue(conveyor,id);
        typeof(ConveyorCellAdapter).GetProperty("HasResource",flags).SetValue(conveyor,true);
        Object.Destroy(_pool.gameObject); yield return null;
        var nextPool = new GameObject("NextPool").AddComponent<ResourceItemObjectPoolAdapter>();
        typeof(ResourceItemObjectPoolAdapter).GetField("resourceDatabase",flags).SetValue(nextPool,
            AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Scripts/ScriptableObject/ResourceDB.asset"));
        var nextId = nextPool.CreateIdFromResourceData(ResourceTypeDomain.Wood,2);
        Object.Destroy(conveyor.gameObject); yield return null; yield return null;
        Assert.That(nextPool.Resources.TryGet(nextId,out var data)); Assert.That(data.Amount,Is.EqualTo(2));
        nextPool.DisposeId(nextId); LogAssert.NoUnexpectedReceived();
    }
}
#endif
