using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// バッチの Play Mode で搬送成功と搬送先切断を通し、予約量と表示 ID を確認する。
/// -batchmode -nographics -executeMethod ResourceTransferVerifier.Run で実行する。
/// </summary>
[InitializeOnLoad]
public static class ResourceTransferVerifier
{
    private const string SessionKey = "FactoryGame.ResourceTransferVerifier.Running";
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;

    static ResourceTransferVerifier()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Run()
    {
        SessionState.SetBool(SessionKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(SessionKey, false)) return;
        SessionState.EraseBool(SessionKey);
        VerifyAsync().Forget();
    }

    private static async UniTaskVoid VerifyAsync()
    {
        try
        {
            var resourceDatabase = AssetDatabase.LoadAssetAtPath<ResourceSO>(
                "Assets/Scripts/ScriptableObject/ResourceDB.asset");
            Require(resourceDatabase != null, "ResourceDB asset is available");

            var poolObject = new GameObject("TransferCheck:Pool");
            var pool = poolObject.AddComponent<ResourceItemObjectPool>();
            SetField(pool, "resourceDatabase", resourceDatabase);
            SetField(pool, "transferSecond", 0.8f);
            await UniTask.Yield(); // プールの Start を完了させる。

            await VerifySuccess(pool);
            await VerifyDestinationDisconnect(pool);
            await VerifySourceDeletion(pool);
            await VerifyCrossingSuccess(pool);
            await VerifyCrossingDeletion(pool);
            await VerifyCrossingCancellation(pool);
            await VerifyCrossingReconnect(pool);
            Debug.Log("Resource transfer checks passed: success, destination deletion, source deletion, crossing success/deletion/cancellation/reconnection, reservation cancellation, ID release.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static async UniTask VerifySuccess(ResourceItemObjectPool pool)
    {
        var setup = CreateTransfer(pool);
        await UniTask.Delay(300);
        var operation = (ResourceTransferOperation)GetField(setup.source, "_activeTransfer");
        Require(setup.target.AllocatedAmount == 5 && setup.target.CurrentLoad == 0,
            $"successful path reserves before animation (reserved={setup.target.AllocatedAmount}, load={setup.target.CurrentLoad})");
        Require(operation != null && operation.Source == setup.source && operation.Target == setup.target &&
                operation.ResourceId == setup.id && operation.Type == ResourceType.Stone &&
                operation.Amount == 5 && operation.CurrentStage == ResourceTransferOperation.Stage.Animating,
            "successful path tracks one active transfer");
        await UniTask.Delay(1000);
        Require(setup.target.AllocatedAmount == 0 && setup.target.CurrentLoad == 5,
            "successful path commits exactly once");
        Require(!ContainsId(pool, setup.id), "successful path returns the display ID");
        Require(operation.CurrentStage == ResourceTransferOperation.Stage.Completed &&
                GetField(setup.source, "_activeTransfer") == null,
            "successful path finishes the transfer once");
        UnityEngine.Object.Destroy(setup.source.gameObject);
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyDestinationDisconnect(ResourceItemObjectPool pool)
    {
        var setup = CreateTransfer(pool);
        await UniTask.Delay(120);
        Require(setup.target.AllocatedAmount == 5 && setup.target.CurrentLoad == 0,
            "cancel path reserves before animation");
        var operation = (ResourceTransferOperation)GetField(setup.source, "_activeTransfer");

        setup.target.OnDisconnect();
        await UniTask.Delay(300);
        Require(setup.target.AllocatedAmount == 0 && setup.target.CurrentLoad == 0,
            $"cancel path releases reservation without committing (reserved={setup.target.AllocatedAmount}, load={setup.target.CurrentLoad}, stage={operation.CurrentStage})");
        Require(ContainsId(pool, setup.id), "source retains the ID after destination disconnect");
        Require(operation.CurrentStage == ResourceTransferOperation.Stage.Cancelled &&
                !operation.TryCancel() && !operation.TryComplete(),
            "destination disconnect ends the attempt once while retaining the ID");

        UnityEngine.Object.Destroy(setup.target.gameObject);
        UnityEngine.Object.Destroy(setup.source.gameObject);
        await UniTask.Delay(120);
        Require(!ContainsId(pool, setup.id), "source deletion returns the ID once");
        await UniTask.Yield();
    }

    private static async UniTask VerifySourceDeletion(ResourceItemObjectPool pool)
    {
        var setup = CreateTransfer(pool);
        await UniTask.Delay(120);
        Require(setup.target.AllocatedAmount == 5, "source deletion starts with an active reservation");
        UnityEngine.Object.Destroy(setup.source.gameObject);
        await UniTask.Delay(120);
        Require(setup.target.AllocatedAmount == 0 && setup.target.CurrentLoad == 0,
            "source deletion cancels reservation without delivery");
        Require(!ContainsId(pool, setup.id), "source deletion returns its display ID");
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyCrossingSuccess(ResourceItemObjectPool pool)
    {
        var setup = CreateCrossingTransfer(pool);
        await UniTask.Delay(120);
        var operation = GetCrossingOperation(setup.crossing);
        Require(operation.Source == setup.crossing && operation.Target == setup.target &&
                operation.ResourceId == setup.id && operation.Type == ResourceType.Stone &&
                operation.Amount == 5 && operation.CurrentStage == ResourceTransferOperation.Stage.Animating,
            "crossing tracks one active transfer");
        await UniTask.Delay(1000);
        Require(setup.target.CurrentLoad == 5 && setup.target.AllocatedAmount == 0,
            "crossing commits its downstream reservation");
        Require(!ContainsId(pool, setup.id), "crossing returns the display ID after delivery");
        Require(operation.CurrentStage == ResourceTransferOperation.Stage.Completed &&
                !operation.TryComplete() && !operation.TryCancel(),
            "crossing completes once");
        UnityEngine.Object.Destroy(setup.crossing.gameObject);
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyCrossingDeletion(ResourceItemObjectPool pool)
    {
        var setup = CreateCrossingTransfer(pool);
        await UniTask.Delay(120);
        var operation = GetCrossingOperation(setup.crossing);
        Require(setup.target.AllocatedAmount == 5 && setup.target.CurrentLoad == 0,
            "crossing deletion starts with a downstream reservation");
        UnityEngine.Object.Destroy(setup.crossing.gameObject);
        await UniTask.Delay(120);
        Require(setup.target.AllocatedAmount == 0 && setup.target.CurrentLoad == 0,
            "crossing deletion cancels the downstream reservation");
        Require(!ContainsId(pool, setup.id), "crossing deletion returns the display ID");
        Require(operation.CurrentStage == ResourceTransferOperation.Stage.Cancelled &&
                !operation.TryCancel() && !operation.TryComplete(),
            "crossing deletion cancels once");
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyCrossingCancellation(ResourceItemObjectPool pool)
    {
        var setup = CreateCrossingTransfer(pool);
        await UniTask.Delay(120);
        var operation = GetCrossingOperation(setup.crossing);
        setup.crossing.CancelStorage(Vector3Int.right, 5, ResourceType.Stone);
        await UniTask.Delay(120);
        Require(setup.target.AllocatedAmount == 0 && setup.target.CurrentLoad == 0 &&
                operation.CurrentStage == ResourceTransferOperation.Stage.Cancelled &&
                !ContainsId(pool, setup.id),
            "upstream cancellation releases the crossing reservation and display ID once");
        UnityEngine.Object.Destroy(setup.crossing.gameObject);
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyCrossingReconnect(ResourceItemObjectPool pool)
    {
        var setup = CreateCrossingTransfer(pool);
        await UniTask.Delay(120);
        var operation = GetCrossingOperation(setup.crossing);
        var firstReservation = GetCrossingReservation(setup.crossing);
        Require(setup.target.AllocatedAmount == 5 && firstReservation.CurrentResult ==
            ResourceReservation.Result.Pending, "crossing reconnect starts with one live reservation");

        setup.target.OnDisconnect();
        await UniTask.Delay(120);
        Require(setup.target.AllocatedAmount == 0 && setup.target.CurrentLoad == 0 &&
                firstReservation.CurrentResult == ResourceReservation.Result.Cancelled &&
                !firstReservation.TryCancel() && operation.Target == null &&
                operation.CurrentStage == ResourceTransferOperation.Stage.WaitingForReservation &&
                ContainsId(pool, setup.id),
            "crossing disconnect cancels only the old reservation and retains the ID");

        var replacement = CreateCell<StorageCell>("TransferCheck:Replacement", Vector3.right);
        SetField(replacement, "capacity", 10);
        var adjacent = (Dictionary<Vector3Int, IContainable>)GetField(setup.crossing, "_adjacentContainers");
        adjacent[Vector3Int.right] = replacement;
        await UniTask.Delay(120);
        var secondReservation = GetCrossingReservation(setup.crossing);
        Require(secondReservation.Id != firstReservation.Id &&
                secondReservation.TargetCell == replacement && replacement.AllocatedAmount == 5,
            "crossing reconnect creates a new reservation for the new target");
        await UniTask.Delay(1000);
        Require(replacement.CurrentLoad == 5 && replacement.AllocatedAmount == 0 &&
                secondReservation.CurrentResult == ResourceReservation.Result.Committed &&
                operation.CurrentStage == ResourceTransferOperation.Stage.Completed &&
                !ContainsId(pool, setup.id),
            "crossing reconnect commits once to the replacement");
        UnityEngine.Object.Destroy(setup.crossing.gameObject);
        UnityEngine.Object.Destroy(setup.target.gameObject);
        UnityEngine.Object.Destroy(replacement.gameObject);
        await UniTask.Yield();
    }

    private static (CrossingCell crossing, StorageCell target, int id) CreateCrossingTransfer(
        ResourceItemObjectPool pool)
    {
        var target = CreateCell<StorageCell>("TransferCheck:CrossingTarget", Vector3.right);
        SetField(target, "capacity", 10);
        var crossing = CreateCell<CrossingCell>("TransferCheck:Crossing", Vector3.zero);
        SetField(crossing, "_cts", new CancellationTokenSource());
        var adjacent = (Dictionary<Vector3Int, IContainable>)
            GetField(crossing, "_adjacentContainers");
        adjacent[Vector3Int.right] = target;
        // +X 側の搬送先を切断したとき、交差セルへ実際の通知が届く関係を作る。
        var links = typeof(ConnectableCellBase).GetProperty("AdjacentCells", InstanceFields);
        links?.SetValue(target, new CellBase[] { null, crossing, null, null });
        links?.SetValue(crossing, new CellBase[] { target, null, null, null });
        var onLostMethod = typeof(CrossingCell).GetMethod("OnConnectionLost", InstanceFields);
        var onLost = Delegate.CreateDelegate(typeof(Action<CellBase>), crossing, onLostMethod);
        typeof(ConnectableCellBase).GetField("OnLostConnectedCell", InstanceFields)
            ?.SetValue(crossing, onLost);

        Require(crossing.AllocateStorage(Vector3Int.right, 5, ResourceType.Stone),
            "crossing reserves the downstream target");
        var id = pool.CreateIdFromResourceData(ResourceType.Stone, 5);
        crossing.Reuse(Vector3Int.right, id);
        crossing.StoreResource(Vector3Int.right, 5);
        return (crossing, target, id);
    }

    private static (ConveyorCell source, StorageCell target, int id) CreateTransfer(ResourceItemObjectPool pool)
    {
        var target = CreateCell<StorageCell>("TransferCheck:Storage", new Vector3(0, 0, 1));
        SetField(target, "capacity", 10);

        var source = CreateCell<ConveyorCell>("TransferCheck:Conveyor", Vector3.zero);
        var id = pool.CreateIdFromResourceData(ResourceType.Stone, 5);
        Require(id != 0, "display ID is allocated");
        SetField(source, "_forwardCell", target);
        SetField(source, "_forwardCellBase", target);
        SetField(source, "_readyToSend", true);
        SetField(source, "_cts", new CancellationTokenSource());
        // 実際の切断入口から通知されるよう、孤立した検証セルの隣接関係を設定する。
        var adjacent = typeof(ConnectableCellBase).GetProperty("AdjacentCells", InstanceFields);
        // +Z 側の搬送先と -Z 側の送り元を、固定方向スロットに合わせる。
        adjacent?.SetValue(target, new CellBase[] { null, null, null, source });
        adjacent?.SetValue(source, new CellBase[] { null, null, target, null });
        var onLostMethod = typeof(ConveyorCell).GetMethod("OnConnectionLost", InstanceFields);
        var onLost = Delegate.CreateDelegate(typeof(Action<CellBase>), source, onLostMethod);
        typeof(ConnectableCellBase).GetField("OnLostConnectedCell", InstanceFields)
            ?.SetValue(source, onLost);
        typeof(ConveyorCell).GetProperty("ResourceId", InstanceFields)?.SetValue(source, id);
        typeof(ConveyorCell).GetProperty("HasResource", InstanceFields)?.SetValue(source, true);

        var cts = (CancellationTokenSource)GetField(source, "_cts");
        var run = (UniTask)typeof(ConveyorCell).GetMethod("StoreResourceAsync", InstanceFields)
            .Invoke(source, new object[] { cts.Token });
        run.Forget();
        return (source, target, id);
    }

    private static T CreateCell<T>(string name, Vector3 position) where T : CellBase
    {
        var root = new GameObject(name);
        root.transform.position = position;
        new GameObject("Model").transform.SetParent(root.transform);
        return root.AddComponent<T>();
    }

    private static bool ContainsId(ResourceItemObjectPool pool, int id)
    {
        // 作業ツリーで進行中の ResourceSO 名称変更と、このタスクの検証を独立させる。
        var field = typeof(ResourceItemObjectPool).GetField("_rentedObjects", InstanceFields) ??
                    typeof(ResourceItemObjectPool).GetField("_rentedObjectDict", InstanceFields);
        if (field == null) throw new InvalidOperationException("Missing rented object dictionary");
        var dictionary = (IDictionary)field.GetValue(pool);
        return dictionary.Contains(id);
    }

    private static ResourceTransferOperation GetCrossingOperation(CrossingCell crossing)
    {
        var item = GetCrossingPending(crossing);
        return (ResourceTransferOperation)item.GetType()
            .GetProperty("Operation", BindingFlags.Instance | BindingFlags.Public).GetValue(item);
    }

    private static ResourceReservation GetCrossingReservation(CrossingCell crossing)
    {
        var item = GetCrossingPending(crossing);
        return (ResourceReservation)item.GetType()
            .GetProperty("Reservation", BindingFlags.Instance | BindingFlags.Public).GetValue(item);
    }

    private static object GetCrossingPending(CrossingCell crossing)
    {
        var pending = (IDictionary)GetField(crossing, "_pending");
        var item = pending[Vector3Int.right];
        if (item == null) throw new InvalidOperationException("Missing crossing transfer");
        return item;
    }

    private static object GetField(object target, string name)
    {
        var field = target.GetType().GetField(name, InstanceFields);
        if (field == null) throw new InvalidOperationException($"Missing field: {name}");
        return field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, InstanceFields);
        if (field == null) throw new InvalidOperationException($"Missing field: {name}");
        field.SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Resource transfer check failed: {message}");
    }
}
