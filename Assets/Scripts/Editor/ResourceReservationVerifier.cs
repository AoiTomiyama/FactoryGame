using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 容量の境界と交差セル経由の予約を、シーンを変更せずに検証する。
/// バッチ実行: -executeMethod ResourceReservationVerifier.Run
/// </summary>
public static class ResourceReservationVerifier
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/FactoryGame/Verify Resource Reservations")]
    public static void Run()
    {
        VerifyTransferOperation();
        VerifyStorage();
        VerifyCrafter();
        VerifyCrossing();
        VerifyConveyor();
        Debug.Log("Resource reservation checks passed: transfer operation, storage, crafter, crossing, conveyor.");
    }

    private static void VerifyTransferOperation()
    {
        var source = CreateCell<StorageCell>();
        var target = CreateCell<StorageCell>();
        try
        {
            var completed = new ResourceTransferOperation(source, target, 12, ResourceType.Stone, 4);
            Require(completed.Source == source && completed.Target == target &&
                    completed.ResourceId == 12 && completed.Type == ResourceType.Stone && completed.Amount == 4,
                "Transfer: one record keeps source, target, ID, type and amount");
            Require(completed.TryMarkReserved() && !completed.TryMarkReserved() &&
                    completed.TryMarkAnimating() && completed.TryComplete() &&
                    !completed.TryComplete() && !completed.TryCancel(),
                "Transfer: completion happens once");

            var retried = new ResourceTransferOperation(source, target, 0, ResourceType.Wood, 2);
            Require(retried.TryMarkReserved() && retried.TryAttachId(-13) &&
                    !retried.TryAttachId(14) && retried.TryMarkAnimating() &&
                    retried.TryWaitForNewTarget() && retried.ResourceId == -13 && retried.Target == null &&
                    retried.TrySetTarget(target) && retried.TryMarkReserved() &&
                    retried.TryMarkAnimating() && retried.TryCancel() &&
                    !retried.TryCancel() && !retried.TryComplete(),
                "Transfer: reconnect keeps ID and cancellation happens once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source.gameObject);
            UnityEngine.Object.DestroyImmediate(target.gameObject);
        }
    }

    private static void VerifyStorage()
    {
        var storage = CreateCell<StorageCell>();
        try
        {
            SetPrivateField(storage, "capacity", 5);

            Require(!storage.AllocateStorage(Vector3Int.right, 6, ResourceType.Stone), "Storage: reject insufficient capacity");
            Require(storage.StoredResourceType == ResourceType.None, "Storage: rejection leaves type unchanged");
            Require(storage.AllocateStorage(Vector3Int.right, 4, ResourceType.Stone), "Storage: reserve four");
            Require(!storage.AllocateStorage(Vector3Int.right, 2, ResourceType.Stone), "Storage: reject partial reservation");
            Require(!storage.AllocateStorage(Vector3Int.right, 0, ResourceType.Stone), "Storage: reject zero amount");
            Require(!storage.AllocateStorage(Vector3Int.right, 1, ResourceType.None), "Storage: reject missing type");
            Require(storage.AllocatedAmount == 4 && storage.CurrentLoad == 0, "Storage: failed requests leave state intact");

            storage.StoreResource(Vector3Int.right, 5);
            Require(storage.AllocatedAmount == 4 && storage.CurrentLoad == 0, "Storage: reject unreserved amount");
            storage.StoreResource(Vector3Int.right, 4);
            Require(storage.AllocatedAmount == 0 && storage.CurrentLoad == 4, "Storage: commit exact reservation");

            Require(storage.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone), "Storage: reserve remaining space");
            storage.CancelStorage(Vector3Int.right, 1, ResourceType.Stone);
            Require(storage.AllocatedAmount == 0 && storage.CurrentLoad == 4,
                "Storage: cancellation preserves committed stock");
            Require(storage.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone),
                "Storage: canceled capacity can be reserved again");
            Require(storage.TryExport(Vector3.zero, 4, out var exported, out _) && exported == 4,
                "Storage: export existing contents");
            Require(storage.StoredResourceType == ResourceType.Stone, "Storage: retain type while delivery is reserved");
            Require(!storage.AllocateStorage(Vector3Int.right, 1, ResourceType.Wood), "Storage: reject another type in flight");
            storage.StoreResource(Vector3Int.right, 1);
            Require(storage.CurrentLoad == 1 && storage.AllocatedAmount == 0, "Storage: complete in-flight delivery");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(storage.gameObject);
        }
    }

    private static void VerifyCrafter()
    {
        var crafter = CreateCell<CrafterCell>();
        try
        {
            SetPrivateField(crafter, "ingredientCapacity", 5);
            var inputs = GetPrivateField<Dictionary<Vector3Int, CrafterCell.ResourceInputData>>(crafter, "_resourceInputs");
            inputs[Vector3Int.right] = new CrafterCell.ResourceInputData();

            Require(!crafter.AllocateStorage(Vector3Int.right, 6, ResourceType.Stone),
                "Crafter: reject insufficient capacity");
            Require(inputs[Vector3Int.right].Type == ResourceType.None,
                "Crafter: rejection leaves type unchanged");
            Require(crafter.AllocateStorage(Vector3Int.right, 4, ResourceType.Stone), "Crafter: reserve four");
            Require(!crafter.AllocateStorage(Vector3Int.right, 2, ResourceType.Stone), "Crafter: reject partial reservation");
            Require(!crafter.AllocateStorage(Vector3Int.left, 1, ResourceType.Stone), "Crafter: reject absent input");
            Require(!crafter.AllocateStorage(Vector3Int.right, 1, ResourceType.Wood), "Crafter: reject another type");
            Require(inputs[Vector3Int.right].Allocated == 4, "Crafter: failed requests leave reservation intact");

            crafter.StoreResource(Vector3Int.right, 5);
            Require(inputs[Vector3Int.right].Amount == 0 && inputs[Vector3Int.right].Allocated == 4,
                "Crafter: reject unreserved amount");
            crafter.StoreResource(Vector3Int.right, 4);
            Require(inputs[Vector3Int.right].Amount == 4 && inputs[Vector3Int.right].Allocated == 0,
                "Crafter: commit exact reservation");
            Require(crafter.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone), "Crafter: reserve final space");
            crafter.CancelStorage(Vector3Int.right, 1, ResourceType.Stone);
            Require(inputs[Vector3Int.right].Allocated == 0 && inputs[Vector3Int.right].Amount == 4,
                "Crafter: cancellation preserves committed ingredients");
            Require(crafter.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone),
                "Crafter: canceled capacity can be reserved again");
            crafter.StoreResource(Vector3Int.right, 1);
            Require(inputs[Vector3Int.right].Amount == 5 && inputs[Vector3Int.right].Allocated == 0,
                "Crafter: never exceed capacity");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crafter.gameObject);
        }
    }

    private static void VerifyConveyor()
    {
        var conveyor = CreateCell<ConveyorCell>();
        try
        {
            Require(!conveyor.AllocateStorage(Vector3Int.right, 0, ResourceType.Stone),
                "Conveyor: reject zero amount");
            Require(!conveyor.AllocateStorage(Vector3Int.right, 1, ResourceType.None),
                "Conveyor: reject missing type");
            Require(conveyor.AllocateStorage(Vector3Int.right, 5, ResourceType.Stone),
                "Conveyor: accept one full batch");
            Require(!conveyor.AllocateStorage(Vector3Int.right, 1, ResourceType.Stone),
                "Conveyor: reject a second batch");
            conveyor.CancelStorage(Vector3Int.right, 5, ResourceType.Stone);
            Require(conveyor.AllocateStorage(Vector3Int.right, 1, ResourceType.Wood),
                "Conveyor: cancellation frees the input slot");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(conveyor.gameObject);
        }
    }

    private static void VerifyCrossing()
    {
        var storage = CreateCell<StorageCell>();
        var crossing = CreateCell<CrossingCell>();
        try
        {
            SetPrivateField(storage, "capacity", 5);
            var adjacent = GetPrivateField<Dictionary<Vector3Int, IContainable>>(
                crossing, "_adjacentContainers");
            adjacent[Vector3Int.right] = storage;

            Require(crossing.AllocateStorage(Vector3Int.right, 4, ResourceType.Stone),
                "Crossing: forward full reservation");
            Require(!crossing.AllocateStorage(Vector3Int.right, 2, ResourceType.Stone),
                "Crossing: forward partial rejection");
            Require(!crossing.AllocateStorage(Vector3Int.left, 1, ResourceType.Stone),
                "Crossing: reject absent output");
            Require(storage.AllocatedAmount == 4, "Crossing: target reservation remains exact");
            crossing.CancelStorage(Vector3Int.right, 4, ResourceType.Stone);
            Require(storage.AllocatedAmount == 0 && storage.StoredResourceType == ResourceType.None,
                "Crossing: cancellation reaches target and releases type");
            Require(crossing.AllocateStorage(Vector3Int.right, 5, ResourceType.Wood),
                "Crossing: canceled path accepts a different resource");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(crossing.gameObject);
            UnityEngine.Object.DestroyImmediate(storage.gameObject);
        }
    }

    private static T CreateCell<T>() where T : CellBase
    {
        var root = new GameObject($"ReservationCheck:{typeof(T).Name}");
        new GameObject("Model").transform.SetParent(root.transform);
        return root.AddComponent<T>();
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        if (field == null) throw new InvalidOperationException($"Missing field: {target.GetType().Name}.{name}");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, PrivateInstance);
        if (field == null) throw new InvalidOperationException($"Missing field: {target.GetType().Name}.{name}");
        return (T)field.GetValue(target);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Resource reservation check failed: {message}");
    }
}
