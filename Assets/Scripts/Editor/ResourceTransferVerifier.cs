using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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

            await VerifyPresentationLifetime(pool);
            await VerifyMissingVisualRecovery(pool);
            await VerifyCrossingMissingVisualRecovery(pool);
            await VerifyExportPresentationRecovery(pool);
            await VerifyReusableHandoff(pool);
            await VerifySuccess(pool);
            await VerifyDestinationDisconnect(pool);
            await VerifySourceDeletion(pool);
            await VerifyCrossingSuccess(pool);
            await VerifyCrossingDeletion(pool);
            await VerifyCrossingCancellation(pool);
            await VerifyCrossingReconnect(pool);
            Debug.Log("Resource transfer checks passed: animation results, independent data/visual lifetime, visual reuse/recovery, success, destination deletion, source deletion, crossing success/deletion/cancellation/reconnection, reservation cancellation, ID release.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static async UniTask VerifyPresentationLifetime(ResourceItemObjectPool pool)
    {
        var id = pool.CreateIdFromResourceData(ResourceType.Stone, 5);
        var visual = GetVisual(pool, id);
        using var cts = new CancellationTokenSource();
        var move = pool.Transfer(cts.Token, Vector3.zero, Vector3.right, id);
        await UniTask.Delay(120);
        cts.Cancel();
        Require(await move == ResourceAnimationResult.Cancelled &&
                pool.Resources.TryGet(id, out var data) && data.Amount == 5,
            "animation cancellation does not change the transit data");

        move = pool.Transfer(CancellationToken.None, Vector3.zero, Vector3.right, id);
        await UniTask.Delay(120);
        DOTween.Kill(visual.transform);
        Require(await move == ResourceAnimationResult.Cancelled && ContainsId(pool, id),
            "an externally killed tween is not a successful delivery");

        move = pool.Transfer(CancellationToken.None, Vector3.zero, Vector3.right, id);
        await UniTask.Delay(120);
        Require(pool.ReleaseVisual(id) && !pool.ReleaseVisual(id), "visual returns exactly once");
        var nextId = pool.CreateIdFromResourceData(ResourceType.Stone, 3);
        var reused = GetVisual(pool, nextId);
        Require(reused == visual && nextId != id && ContainsId(pool, id),
            "the same pooled visual receives a new data ID while the old data remains");
        var parked = new Vector3(5, 0, 0);
        pool.SetPosition(nextId, parked);
        Require(await move == ResourceAnimationResult.Cancelled,
            "returning an animated visual reports cancellation");
        await UniTask.Delay(900);
        Require(reused.transform.position == parked,
            "the old tween cannot move a reused visual");
        Require(await pool.Transfer(CancellationToken.None, Vector3.zero, Vector3.right, id) ==
                ResourceAnimationResult.MissingVisual && ContainsId(pool, id),
            "a missing visual reports failure without removing data");
        pool.DisposeId(id);
        pool.DisposeId(nextId);
        pool.DisposeId(nextId);
        Require(!ContainsId(pool, id) && !ContainsId(pool, nextId),
            "the resource owner ends each transit record once");
    }

    private static async UniTask VerifyMissingVisualRecovery(ResourceItemObjectPool pool)
    {
        var setup = CreateTransfer(pool);
        pool.ReleaseVisual(setup.id);
        await UniTask.Delay(1100);
        Require(setup.target.CurrentLoad == 0 && setup.target.AllocatedAmount == 0 &&
                ContainsId(pool, setup.id),
            "a conveyor retains data and does not commit without a visual");
        Require(pool.TryRestoreVisual(setup.id), "a retained transit record can restore its visual");
        await UniTask.Delay(1100);
        Require(setup.target.CurrentLoad == 5 && setup.target.AllocatedAmount == 0 &&
                !ContainsId(pool, setup.id), "restored presentation permits one delivery");
        UnityEngine.Object.Destroy(setup.source.gameObject);
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyCrossingMissingVisualRecovery(ResourceItemObjectPool pool)
    {
        var setup = CreateCrossingTransfer(pool);
        pool.ReleaseVisual(setup.id);
        await UniTask.Delay(1100);
        Require(setup.target.CurrentLoad == 0 && setup.target.AllocatedAmount == 0 &&
                ContainsId(pool, setup.id), "a crossing retains data when its visual is missing");
        Require(pool.TryRestoreVisual(setup.id), "the crossing visual can be restored");
        await UniTask.Delay(1100);
        Require(setup.target.CurrentLoad == 5 && !ContainsId(pool, setup.id),
            "the crossing commits once after visual recovery");
        UnityEngine.Object.Destroy(setup.crossing.gameObject);
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyExportPresentationRecovery(ResourceItemObjectPool pool)
    {
        var storage = CreateCell<StorageCell>("TransferCheck:ExportStorage", Vector3.back);
        SetField(storage, "capacity", 10);
        Require(storage.AllocateStorage(Vector3Int.forward, 5, ResourceType.Stone),
            "export fixture reserves its source data");
        storage.StoreResource(Vector3Int.forward, 5);
        var exporter = CreateCell<ExportConveyorCell>("TransferCheck:Exporter", Vector3.zero);
        SetField(exporter, "_backwardCell", storage);
        SetField(exporter, "_backwardCellBase", storage);
        using var cts = new CancellationTokenSource();
        SetField(exporter, "_cts", cts);
        typeof(ConveyorCell).GetField("transferAmount", InstanceFields).SetValue(exporter, 3);
        var send = (UniTask)typeof(ConveyorCell).GetMethod("StoreResourceAsync", InstanceFields)
            .Invoke(exporter, new object[] { cts.Token });
        send.Forget();
        var take = (UniTask)typeof(ExportConveyorCell).GetMethod("TakeResourceAsync", InstanceFields)
            .Invoke(exporter, new object[] { cts.Token });
        take.Forget();
        await UniTask.Delay(120);
        var id = (int)typeof(ConveyorCell).GetProperty("ResourceId", InstanceFields).GetValue(exporter);
        Require(id != 0 && storage.CurrentLoad == 2, "export removes exactly one batch from its source");
        pool.ReleaseVisual(id);
        await UniTask.Delay(1100);
        var readyField = typeof(ConveyorCell).GetField("_readyToSend", InstanceFields);
        Require(!(bool)readyField.GetValue(exporter) && storage.CurrentLoad == 2 &&
                pool.Resources.TryGet(id, out var data) && data.Amount == 3,
            "failed export presentation neither repeats export nor sends forward");
        Require(pool.TryRestoreVisual(id), "export visual can be restored");
        await UniTask.Delay(1100);
        Require((bool)readyField.GetValue(exporter) && storage.CurrentLoad == 2,
            "export becomes ready only after a completed presentation");
        UnityEngine.Object.Destroy(exporter.gameObject);
        await UniTask.Delay(120);
        Require(!ContainsId(pool, id), "exporter deletion ends the held transit data");
        UnityEngine.Object.Destroy(storage.gameObject);
        await UniTask.Yield();
    }

    private static async UniTask VerifyReusableHandoff(ResourceItemObjectPool pool)
    {
        var setup = CreateTransfer(pool, start: false);
        setup.target.transform.position = Vector3.forward * 2;
        var crossing = CreateCell<CrossingCell>("TransferCheck:Handoff", Vector3.forward);
        SetField(crossing, "_cts", new CancellationTokenSource());
        var adjacent = (Dictionary<Vector3Int, IContainable>)GetField(crossing, "_adjacentContainers");
        adjacent[Vector3Int.forward] = setup.target;
        SetField(setup.source, "_forwardCell", crossing);
        SetField(setup.source, "_forwardCellBase", crossing);
        var cts = (CancellationTokenSource)GetField(setup.source, "_cts");
        var run = (UniTask)typeof(ConveyorCell).GetMethod("StoreResourceAsync", InstanceFields)
            .Invoke(setup.source, new object[] { cts.Token });
        run.Forget();
        await UniTask.Delay(1100);
        Require(ContainsId(pool, setup.id) && setup.target.CurrentLoad == 0,
            "a reusable receiver keeps the same transit record during its next animation");
        await UniTask.Delay(1000);
        Require(setup.target.CurrentLoad == 5 && setup.target.AllocatedAmount == 0 &&
                !ContainsId(pool, setup.id) && !pool.ReleaseVisual(setup.id),
            "a conveyor-crossing chain delivers once and returns its shared visual at the end");
        UnityEngine.Object.Destroy(setup.source.gameObject);
        UnityEngine.Object.Destroy(crossing.gameObject);
        UnityEngine.Object.Destroy(setup.target.gameObject);
        await UniTask.Yield();
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

    private static (ConveyorCell source, StorageCell target, int id) CreateTransfer(ResourceItemObjectPool pool,
        bool start = true)
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

        if (start)
        {
            var cts = (CancellationTokenSource)GetField(source, "_cts");
            var run = (UniTask)typeof(ConveyorCell).GetMethod("StoreResourceAsync", InstanceFields)
                .Invoke(source, new object[] { cts.Token });
            run.Forget();
        }
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
        => pool.Resources.TryGet(id, out _);

    private static GameObject GetVisual(ResourceItemObjectPool pool, int id)
    {
        // 作業ツリーで進行中の ResourceSO 名称変更と、このタスクの検証を独立させる。
        var field = typeof(ResourceItemObjectPool).GetField("_rentedObjects", InstanceFields) ??
                    typeof(ResourceItemObjectPool).GetField("_rentedObjectDict", InstanceFields);
        if (field == null) throw new InvalidOperationException("Missing rented object dictionary");
        var dictionary = (IDictionary)field.GetValue(pool);
        var lease = dictionary[id];
        return (GameObject)lease.GetType().GetField("Prefab").GetValue(lease);
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
